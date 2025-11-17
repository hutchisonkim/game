<#
Runs the Spark-based test runner locally, mirroring the CI step
"Start Spark Backend + Run Tests".

Checks required environment variables, builds & publishes
the test runner, copies xUnit runner bits if available, finds
the Microsoft.Spark jar, then invokes spark-submit to run the
specified test filter.

Usage (PowerShell):
  pwsh.exe -File .\scripts\run-spark-tests-local.ps1

Optional parameters:
  -TestFilter  : xUnit filter string (default targets ChessSparkPolicyTests)
  -ProjectPath : path to the runner project (default: tests/Game.Chess.Tests.Unit.Runner)
  -Framework   : .NET target framework (default: net8.0)

This script only *checks* environment variables; it does not change them.
#>

param(
    [string]$TestFilter = 'FullyQualifiedName~Game.Chess.Tests.Unit.ChessSparkPolicyTests',
    [string]$ProjectPath = 'tests/Game.Chess.Tests.Unit.Runner',
    [string]$Framework = 'net8.0'
)

function Fail([string]$msg, [int]$code = 1) {
    Write-Error $msg
    exit $code
}

Write-Host "[run-spark-tests-local] Starting local runner script"

# Required / recommended env vars (we only check, do not set)
$required = @('SPARK_HOME','DOTNET_WORKER_DIR')
$recommended = @('DOTNETBACKEND_PORT','DOTNET_WORKER_SPARK_VERSION','PYTHON_WORKER_FACTORY_PORT')

$missingRequired = @()
foreach ($r in $required) {
    $val = (Get-Item -Path "env:$r" -ErrorAction SilentlyContinue).Value
    if (-not [string]::IsNullOrEmpty($val)) { continue }
    $missingRequired += $r
}
if ($missingRequired.Count -gt 0) {
    Fail("Missing required environment variables: $($missingRequired -join ', '). Please set them before running the script.")
}

foreach ($r in $recommended) {
    $val = (Get-Item -Path "env:$r" -ErrorAction SilentlyContinue).Value
    if (-not [string]::IsNullOrEmpty($val)) { continue }
    Write-Warning "Recommended environment variable not set: $r"
}

Write-Host "Environment variables check OK. SPARK_HOME=$env:SPARK_HOME"

# Detect runtime for dotnet publish
if ($IsWindows) { $runtime = 'win-x64' } else { $runtime = 'linux-x64' }
Write-Host "Using runtime: $runtime"

# Ensure the runner project exists
if (-not (Test-Path -Path $ProjectPath)) {
    Fail("Runner project path not found: $ProjectPath")
}

Push-Location $ProjectPath
try {
    Write-Host "Building project: $ProjectPath"
    $buildExit = & dotnet build -f $Framework -r $runtime 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error $buildExit
        Fail("dotnet build failed")
    }

    Write-Host "Publishing project (self-contained)"
    $pubArgs = @('-f', $Framework, '-r', $runtime, '-c', 'Release', '--self-contained', 'true')
    & dotnet publish @pubArgs | Write-Host
    if ($LASTEXITCODE -ne 0) { Fail("dotnet publish failed") }

    $publishDir = Join-Path -Path (Get-Location).Path -ChildPath "bin/Release/$Framework/$runtime/publish"
    if (-not (Test-Path $publishDir)) { Fail("Publish directory not found: $publishDir") }
    Write-Host "Publish dir: $publishDir"

    # Copy xUnit console runner bits if present in user's NuGet cache
    $nugetCandidates = @()
    if ($env:USERPROFILE) { $nugetCandidates += Join-Path $env:USERPROFILE '.nuget\packages\xunit.runner.console\2.9.3\tools\net6.0' }
    $nugetCandidates += '~/.nuget/xunit.runner.console/2.9.3/tools/net6.0'
    $copied = $false
    foreach ($c in $nugetCandidates) {
        $path = (Resolve-Path -LiteralPath $c -ErrorAction SilentlyContinue)
        if ($path) {
            Write-Host "Copying xUnit runner files from $path to $publishDir"
            Copy-Item -Path (Join-Path $path '*') -Destination $publishDir -Recurse -Force -ErrorAction SilentlyContinue
            $copied = $true
            break
        }
    }
    if (-not $copied) { Write-Warning "Could not find xUnit runner files in NuGet cache; tests may fail if runner binary missing." }

    # Find Microsoft.Spark jar: search publish dir, SPARK_HOME, and .docker_cache
    $jarPattern = 'microsoft-spark*.jar'
    $dockerCachePath = Join-Path -Path (Get-Location).Path -ChildPath '..\..\..\.docker_cache'
    $searchDirs = @($publishDir, $env:SPARK_HOME, $dockerCachePath) | Where-Object { $_ -and (Test-Path $_) }

    # Try to prefer a jar that matches SPARK_HOME's major.minor (e.g. '3-5' for 3.5.x)
    $pref = $null
    if ($env:SPARK_HOME -and ($env:SPARK_HOME -match '(\d+)\.(\d+)')) {
        $pref = "microsoft-spark-$($Matches[1])-$($Matches[2])"
        Write-Host "Looking for jar prefixed with: $pref"
    }

    $foundJars = @()
    foreach ($d in $searchDirs) {
        try {
            $foundJars += Get-ChildItem -Path $d -Filter $jarPattern -Recurse -ErrorAction SilentlyContinue
        } catch { }
    }

    if ($foundJars.Count -eq 0) {
        Fail("Could not find Microsoft.Spark jar (pattern: $jarPattern). Looked in: $($searchDirs -join ', ')")
    }

    if ($pref) {
        $preferred = $foundJars | Where-Object { $_.Name -like "*$pref*" } | Select-Object -First 1
        if ($preferred) { $jarPath = $preferred.FullName }
    }

    if (-not $jarPath) {
        Write-Host "Multiple/other Microsoft.Spark jars found; selecting first candidate:"
        $foundJars | ForEach-Object { Write-Host "  - $($_.FullName)" }
        $jarPath = $foundJars[0].FullName
    }

    Write-Host "Using Spark jar: $jarPath"

    # Locate spark-submit executable/script
    $sparkBin = Join-Path $env:SPARK_HOME 'bin'
    if (-not (Test-Path $sparkBin)) { Fail("SPARK_HOME/bin not found: $sparkBin") }

    $sparkSubmit = $null
    $candidates = @('spark-submit.cmd','spark-submit.ps1','spark-submit')
    foreach ($c in $candidates) {
        $p = Join-Path $sparkBin $c
        if (Test-Path $p) { $sparkSubmit = $p; break }
    }
    if (-not $sparkSubmit) { Fail("spark-submit not found under SPARK_HOME/bin. Ensure Spark is installed and SPARK_HOME is correct.") }

    Write-Host "Invoking spark-submit: $sparkSubmit"

    Push-Location $publishDir
    try {
        $argsList = @(
            '--class', 'org.apache.spark.deploy.dotnet.DotnetRunner',
            '--master', 'local',
            $jarPath,
            './Game.Chess.Tests.Unit.Runner',
            '--filter', $TestFilter
        )

        # On Windows, run spark-submit via & (it may be a .cmd)
        if ($sparkSubmit -like '*.cmd' -or $sparkSubmit -like '*.ps1' -or $sparkSubmit -like '*spark-submit') {
            & $sparkSubmit @argsList
        } else {
            & $sparkSubmit @argsList
        }

        $ec = $LASTEXITCODE
        if ($ec -ne 0) { Fail("spark-submit returned exit code $ec", $ec) }
    }
    finally { Pop-Location }

}
finally { Pop-Location }

Write-Host "[run-spark-tests-local] Completed successfully"
