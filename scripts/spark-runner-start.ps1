<#
Starts the Spark-backed hot-reload test runner once and keeps it running.

It mirrors build/publish/jar resolution from run-spark-tests-local.ps1,
but does NOT pass a test filter and expects the runner to host
an HTTP control endpoint (e.g., http://localhost:7009/) for commands.

Usage:
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\spark-runner-start.ps1

Optional:
  -ProjectPath tests/Game.Chess.Tests.Integration.Runner
  -Framework   net8.0
#>

param(
    [string]$ProjectPath = 'tests/Game.Chess.Tests.Integration.Runner',
    [string]$Framework = 'net8.0'
)

function Fail([string]$msg, [int]$code = 1) {
    Write-Error $msg
    exit $code
}

Write-Host "[spark-runner-start] Starting Spark-backed runner"

# No URL ACL needed when using raw TCP on 7009

# Required / recommended env vars
$required = @('SPARK_HOME', 'DOTNET_WORKER_DIR')
$recommended = @('DOTNETBACKEND_PORT', 'DOTNET_WORKER_SPARK_VERSION', 'PYTHON_WORKER_FACTORY_PORT', 'HADOOP_HOME')

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

# Apply VS Code task env defaults if not already set (mirrors tasks.json)
if (-not $env:DOTNETBACKEND_PORT) { $env:DOTNETBACKEND_PORT = '5000' }
if (-not $env:DOTNET_WORKER_SPARK_VERSION) { $env:DOTNET_WORKER_SPARK_VERSION = '3.5.3' }
if (-not $env:PYTHON_WORKER_FACTORY_PORT) { $env:PYTHON_WORKER_FACTORY_PORT = '5001' }
if (-not $env:HADOOP_HOME) { $env:HADOOP_HOME = 'C:\hadoop\cdarlint\winutils\hadoop-3.3.6' }

Write-Host "Environment variables check OK. SPARK_HOME=$env:SPARK_HOME"

# Detect runtime for dotnet publish
if ($IsWindows) { $runtime = 'win-x64' } else { $runtime = 'linux-x64' }
Write-Host "Using runtime: $runtime"

if (-not (Test-Path -Path $ProjectPath)) {
    Fail("Runner project path not found: $ProjectPath")
}

Push-Location $ProjectPath
try {
    Write-Host "Building project: $ProjectPath"
    $buildExit = & dotnet build -f $Framework -r $runtime 2>&1
    if ($LASTEXITCODE -ne 0) {
        $joined = $buildExit -join "`n"
        Fail("dotnet build failed. Output:`n$joined")
    }

    Write-Host "Publishing project (self-contained=false for VSTest compatibility)"
    $pubArgs = @('-f', $Framework, '-r', $runtime, '-c', 'Release', '--self-contained', 'false')
    & dotnet publish @pubArgs | Write-Host
    if ($LASTEXITCODE -ne 0) { Fail("dotnet publish failed") }

    $publishDir = Join-Path -Path (Get-Location).Path -ChildPath "bin/Release/$Framework/$runtime/publish"
    if (-not (Test-Path $publishDir)) { Fail("Publish directory not found: $publishDir") }
    Write-Host "Publish dir: $publishDir"
    Set-Item -Path Env:SPARK_RUNNER_PUBLISH_DIR -Value $publishDir -Force

    # Copy xUnit runner bits if present in user's NuGet cache (console + utility)
    $nugetCandidates = @()
    if ($env:USERPROFILE) {
        # Prefer net8.0 tools if available, fall back to net6.0
        $nugetCandidates += Join-Path $env:USERPROFILE '.nuget\packages\xunit.runner.console\2.9.3\tools\net8.0'
        $nugetCandidates += Join-Path $env:USERPROFILE '.nuget\packages\xunit.runner.console\2.9.3\tools\net6.0'
        $nugetCandidates += Join-Path $env:USERPROFILE '.nuget\packages\xunit.runner.utility\2.9.3\lib\net6.0'
    }
    $nugetCandidates += '~/.nuget/xunit.runner.console/2.9.3/tools/net8.0'
    $nugetCandidates += '~/.nuget/xunit.runner.console/2.9.3/tools/net6.0'
    $nugetCandidates += '~/.nuget/xunit.runner.utility/2.9.3/lib/net6.0'
    
    Write-Host "[xunit-copy] Candidates:"
    $nugetCandidates | ForEach-Object { Write-Host "  - $_" }
    
    $copiedFrom = @()
    foreach ($c in $nugetCandidates) {
        $path = (Resolve-Path -LiteralPath $c -ErrorAction SilentlyContinue)
        if ($path) {
            Write-Host "[xunit-copy] Found: $($path.Path)"
            try {
                $countBeforeDll = (Get-ChildItem -Path $publishDir -Filter 'xunit*.dll' -ErrorAction SilentlyContinue).Count
                $countBeforeCfg = (Get-ChildItem -Path $publishDir -Filter '*.runtimeconfig.json' -ErrorAction SilentlyContinue).Count
                Copy-Item -Path (Join-Path $path.Path '*') -Destination $publishDir -Recurse -Force -ErrorAction Stop
                $countAfterDll = (Get-ChildItem -Path $publishDir -Filter 'xunit*.dll' -ErrorAction SilentlyContinue).Count
                $countAfterCfg = (Get-ChildItem -Path $publishDir -Filter '*.runtimeconfig.json' -ErrorAction SilentlyContinue).Count
                Write-Host "[xunit-copy] Copied from $($path.Path). dll before/after=$countBeforeDll/$countAfterDll runtimeconfig before/after=$countBeforeCfg/$countAfterCfg"
                $copiedFrom += $path.Path
            }
            catch {
                Write-Warning "[xunit-copy] Copy failed from $($path.Path): $_"
            }
        }
        else {
            Write-Host "[xunit-copy] Not found: $c"
        }
    }
    if ($copiedFrom.Count -eq 0) {
        Write-Warning "[xunit-copy] Could not find any xUnit runner files to copy."
    }
    else {
        Write-Host "[xunit-copy] Copied from: $($copiedFrom -join ', ')"
        $presentDlls = Get-ChildItem -Path $publishDir -Filter 'xunit*.dll' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
        $presentCfgs = Get-ChildItem -Path $publishDir -Filter '*.runtimeconfig.json' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
        Write-Host "[xunit-copy] Present DLLs: $($presentDlls -join ', ')"
        Write-Host "[xunit-copy] Present runtimeconfigs: $($presentCfgs -join ', ')"
    }

    # Find Microsoft.Spark jar
    $jarPattern = 'microsoft-spark*.jar'
    $dockerCachePath = Join-Path -Path (Get-Location).Path -ChildPath '..\..\..\.docker_cache'
    $searchDirs = @($publishDir, $env:SPARK_HOME, $dockerCachePath) | Where-Object { $_ -and (Test-Path $_) }

    $pref = $null
    if ($env:SPARK_HOME -and ($env:SPARK_HOME -match '(\d+)\.(\d+)')) {
        $pref = "microsoft-spark-$($Matches[1])-$($Matches[2])"
        Write-Host "Looking for jar prefixed with: $pref"
    }

    $foundJars = @()
    foreach ($d in $searchDirs) {
        try { $foundJars += Get-ChildItem -Path $d -Filter $jarPattern -Recurse -ErrorAction SilentlyContinue } catch {}
    }
    if ($foundJars.Count -eq 0) {
        Fail("Could not find Microsoft.Spark jar (pattern: $jarPattern). Looked in: $($searchDirs -join ', ')")
    }
    if ($pref) {
        $preferred = $foundJars | Where-Object { $_.Name -like "*$pref*" } | Select-Object -First 1
        if ($preferred) { $jarPath = $preferred.FullName }
    }
    if (-not $jarPath) { $jarPath = $foundJars[0].FullName }
    Write-Host "Using Spark jar: $jarPath"

    # Locate spark-submit
    $sparkBin = Join-Path $env:SPARK_HOME 'bin'
    if (-not (Test-Path $sparkBin)) { Fail("SPARK_HOME/bin not found: $sparkBin") }
    $sparkSubmit = $null
    $candidates = @('spark-submit.cmd', 'spark-submit.ps1', 'spark-submit')
    foreach ($c in $candidates) {
        $p = Join-Path $sparkBin $c
        if (Test-Path $p) { $sparkSubmit = $p; break }
    }
    if (-not $sparkSubmit) { Fail("spark-submit not found under SPARK_HOME/bin.") }

    # Ensure DOTNET env points to system dotnet
    $env:DOTNET_ROOT = "C:\Program Files\dotnet"
    $env:DOTNET_WORKER_DIR = "C:\Program Files\dotnet"

    # Echo key envs for diagnostics
    Write-Host "DOTNETBACKEND_PORT=$env:DOTNETBACKEND_PORT"
    Write-Host "DOTNET_ROOT=$env:DOTNET_ROOT"
    Write-Host "DOTNET_WORKER_SPARK_VERSION=$env:DOTNET_WORKER_SPARK_VERSION"
    Write-Host "PYTHON_WORKER_FACTORY_PORT=$env:PYTHON_WORKER_FACTORY_PORT"
    Write-Host "HADOOP_HOME=$env:HADOOP_HOME"

    Push-Location $publishDir
    try {
        $argsList = @(
            '--conf', 'spark.sql.adaptive.enabled=true',
            '--conf', 'spark.driver.memory=4g',
            '--class', 'org.apache.spark.deploy.dotnet.DotnetRunner',
            '--master', 'local',
            $jarPath,
            './Game.Chess.Tests.Integration.Runner'
        )

        $logDir = Join-Path $publishDir 'test-logs'
        if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
        $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss_fff')
        $sparkLogPath = Join-Path $logDir "spark-runner-output-$timestamp.log"
        New-Item -ItemType File -Path $sparkLogPath -Force | Out-Null

        Write-Host "Invoking spark-submit (detached) to start runner: $sparkSubmit"
        $proc = Start-Process -FilePath $sparkSubmit -ArgumentList ($argsList -join ' ') -WindowStyle Hidden -PassThru
        if (-not $proc) { Fail("Failed to start spark-submit process") }
        Write-Host "spark-submit started (PID=$($proc.Id)). Runner will initialize shortly."
        Write-Host "Logs: $sparkLogPath (will be populated by spark)"

        # Wait for runner TCP endpoint to be ready
        $maxWaitSec = 3
        $elapsed = 0
        Write-Host "Waiting for runner to respond at 127.0.0.1:7009 (max ${maxWaitSec}s)"
        while ($elapsed -lt $maxWaitSec) {
            try {
                $pong = pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'spark-testctl.ps1') -Ping
                if ($pong -eq 'PONG') { Write-Host "Runner responded: PONG"; break }
            }
            catch { }
            Start-Sleep -Seconds 1
            $elapsed += 1
        }
        if ($elapsed -ge $maxWaitSec) {
            Write-Warning "Runner did not respond within ${maxWaitSec}s. Check logs at $sparkLogPath."
        }
    }
    finally { Pop-Location }
}
finally { Pop-Location }

Write-Host "[spark-runner-start] Completed"
