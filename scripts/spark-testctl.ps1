param(
    [string]$Filter = "",
    [string]$Server = "127.0.0.1",
    [int]$Port = 7009,
    [switch]$Ping,
    [switch]$Stop,
    [switch]$Tail,
    [switch]$IncludeSparkLog,
    [switch]$SkipBuild,
    [switch]$RebuildOnly,
    [int]$TailLines = 40,
    [int]$TailWaitSeconds = 15,
    [string]$ProjectPath = 'tests/Game.Chess.Tests.Integration.Runner',
    [string]$Framework = 'net8.0',
    [string]$Runtime
)

if (-not $Runtime) { $Runtime = if ($IsWindows) { 'win-x64' } else { 'linux-x64' } }

function Fail([string]$msg) {
    Write-Error $msg
    exit 1
}

function Invoke-RunnerSync([string]$Body) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect($Server, $Port)
        $stream = $client.GetStream()
        $writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::UTF8)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)

        $writer.WriteLine($Body)
        $resp = $reader.ReadToEnd()

        $reader.Dispose(); $writer.Dispose(); $stream.Dispose(); $client.Dispose()
        return $resp
    }
    catch {
        Write-Error "spark-testctl: request failed. $_"
        exit 1
    }
}

function Invoke-RunnerAsync([string]$Body) {
    $job = Start-Job -ScriptBlock {
        param($Srv, $Prt, $Command)
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $client.Connect($Srv, $Prt)
            $stream = $client.GetStream()
            $writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::UTF8)
            $writer.AutoFlush = $true
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)

            $writer.WriteLine($Command)
            $resp = $reader.ReadToEnd()

            $reader.Dispose(); $writer.Dispose(); $stream.Dispose(); $client.Dispose()
            return $resp
        }
        catch {
            return "ERROR: $_"
        }
    } -ArgumentList $Server, $Port, $Body

    return $job
}

function Resolve-PublishDir {
    if ($env:SPARK_RUNNER_PUBLISH_DIR -and (Test-Path $env:SPARK_RUNNER_PUBLISH_DIR)) {
        return (Resolve-Path -LiteralPath $env:SPARK_RUNNER_PUBLISH_DIR).Path
    }

    if (-not (Test-Path -LiteralPath $ProjectPath)) {
        return $null
    }

    $resolvedProject = (Resolve-Path -LiteralPath $ProjectPath).Path
    $pub = Join-Path $resolvedProject "bin/Release/$Framework/$Runtime/publish"
    if (Test-Path $pub) { return $pub }
    return $null
}

function Wait-ForLog([string]$Dir, [string]$Pattern, [Nullable[datetime]]$SinceUtc, [int]$MaxWaitSec) {
    for ($i = 0; $i -le $MaxWaitSec; $i++) {
        $candidate = Get-ChildItem -Path $Dir -Filter $Pattern -ErrorAction SilentlyContinue |
            Where-Object { -not $SinceUtc -or $_.LastWriteTimeUtc -ge $SinceUtc } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
        Start-Sleep -Seconds 1
    }
    return $null
}

function Start-TailJob([string]$Path, [string]$Label, [int]$Lines) {
    Start-Job -ScriptBlock {
        param($P, $Lbl, $TailLines)
        Get-Content -Path $P -Tail $TailLines -Wait | ForEach-Object { "[$Lbl] $_" }
    } -ArgumentList $Path, $Label, $Lines
}

if ($Ping) {
    $resp = Invoke-RunnerSync "PING"
    Write-Host $resp
    exit 0
}

if ($Stop) {
    $resp = Invoke-RunnerSync "STOP"
    Write-Host $resp
    exit 0
}

if ($RebuildOnly) {
    Write-Host "Triggering rebuild and republish of test assemblies..." -ForegroundColor Cyan
    $resp = Invoke-RunnerSync "REBUILD"
    Write-Host $resp
    exit 0
}

if ($SkipBuild) {
    Write-Host "Running tests with cached assemblies (skipping rebuild)..." -ForegroundColor Gray
}

# Default: RUN, optionally with filter text
# Format: RUN [skipBuild] [filter]
$body = "RUN"
if ($SkipBuild) { $body += " skipBuild" }
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $body += " $Filter" }

if (-not $Tail) {
    $resp = Invoke-RunnerSync $body
    Write-Host $resp
    exit 0
}

$publishDir = Resolve-PublishDir
if (-not $publishDir) { Fail "Could not resolve publish directory. Set SPARK_RUNNER_PUBLISH_DIR or pass -ProjectPath/-Framework/-Runtime." }
$logDir = Join-Path $publishDir 'test-logs'
if (-not (Test-Path $logDir)) { Fail "Log directory not found: $logDir" }

$runStartUtc = (Get-Date).ToUniversalTime()
$tailJobs = @()
$green = "`e[32m"; $brightGreen = "`e[92m"; $yellow = "`e[33m"; $magenta = "`e[35m"; $cyan = "`e[36m"; $reset = "`e[0m"
# Hide cursor
Write-Host -NoNewline "`e[?25l"
$accumulator = ""
$spinnerFrames = @(
    "${yellow}-${reset}",
    "${yellow}/${reset}",
    "${yellow}|${reset}",
    "${yellow}\\${reset}",
    "${green}=${reset}${yellow}-${reset}",
    "${green}=${reset}${yellow}/${reset}",
    "${green}=${reset}${yellow}|${reset}",
    "${green}=${reset}${yellow}\\${reset}",
    "${green}==${reset}${yellow}-${reset}",
    "${green}==${reset}${yellow}/${reset}",
    "${green}==${reset}${yellow}|${reset}",
    "${green}==${reset}${yellow}\\${reset}",
    "${green}===${reset}${yellow}-${reset}",
    "${green}===${reset}${yellow}/${reset}",
    "${green}===${reset}${yellow}|${reset}",
    "${green}===${reset}${yellow}\\${reset}",
    "${brightGreen}====${reset}${yellow}-${reset}",
    "${brightGreen}====${reset}${yellow}/${reset}",
    "${brightGreen}====${reset}${yellow}|${reset}",
    "${brightGreen}====${reset}${yellow}\\${reset}",
    "${brightGreen}=====${reset}${yellow}-${reset}",
    "${brightGreen}=====${reset}${yellow}/${reset}",
    "${brightGreen}=====${reset}${yellow}|${reset}",
    "${brightGreen}=====${reset}${yellow}\\${reset}",
    "${cyan}======${reset}${yellow}-${reset}",
    "${cyan}======${reset}${yellow}/${reset}",
    "${cyan}======${reset}${yellow}|${reset}",
    "${cyan}======${reset}${yellow}\\${reset}",
    "${cyan}=======${reset}${magenta}-${reset}",
    "${cyan}=======${reset}${magenta}/${reset}",
    "${cyan}=======${reset}${magenta}|${reset}",
    "${cyan}=======${reset}${magenta}\\${reset}"
)
$spinIndex = 0
$spinnerActive = $false
$spinnerClear = ' '.PadRight(80)

$runJob = Invoke-RunnerAsync $body
if (-not $runJob) { Fail "Failed to start RUN request" }

Write-Host "Waiting for runner logs in $logDir ..."
$testLog = Wait-ForLog -Dir $logDir -Pattern 'test-output-*.log' -SinceUtc $runStartUtc -MaxWaitSec $TailWaitSeconds
if ($testLog) {
    Write-Host "Tailing test log: $testLog"
    $tailJobs += Start-TailJob -Path $testLog -Label 'test' -Lines $TailLines
}
else {
    Write-Warning "No test log appeared within ${TailWaitSeconds}s."
}

if ($IncludeSparkLog) {
    $sparkLog = Wait-ForLog -Dir $logDir -Pattern 'spark-runner-output-*.log' -SinceUtc $null -MaxWaitSec $TailWaitSeconds
    if ($sparkLog) {
        Write-Host "Tailing spark log: $sparkLog"
        $tailJobs += Start-TailJob -Path $sparkLog -Label 'spark' -Lines $TailLines
    }
    else {
        Write-Warning "No spark runner log found to tail."
    }
}

# Drain tail output while tests run
if ($tailJobs.Count -gt 0) {
    while ($true) {
        $output = Receive-Job -Job $tailJobs -ErrorAction SilentlyContinue
        if ($output) {
            if ($spinnerActive) {
                Write-Host -NoNewline "`r       `r"
                $spinnerActive = $false
            }
            $spinIndex = 0
            $accumulator = ""
            $output | ForEach-Object { Write-Host $_ }
        }

        $runState = (Get-Job -Id $runJob.Id).State
        if ($runState -ne 'Running') { break }

        $spin = $spinnerFrames[$spinIndex % $spinnerFrames.Count]
        $spinIndex++
        if ($spinIndex % $spinnerFrames.Count -eq 0) { $accumulator += "${cyan}*${reset}" }
        Write-Host -NoNewline "`r$spinnerClear`r[tail] $accumulator$spin"
        $spinnerActive = $true

        Start-Sleep -Milliseconds 300
    }
}

Wait-Job $runJob | Out-Null
$resp = Receive-Job $runJob
Write-Host "Runner response: $resp"

if ($tailJobs.Count -gt 0) {
    Write-Host "Draining tail output..."
    $output = Receive-Job -Job $tailJobs -ErrorAction SilentlyContinue
    if ($output) {
        if ($spinnerActive) { Write-Host -NoNewline "`r       `r"; $spinnerActive = $false }
        $output | ForEach-Object { Write-Host $_ }
    }
    Write-Host "`r$spinnerClear`r"
    Write-Host "Stopping tail jobs..."
    Stop-Job -Job $tailJobs -ErrorAction SilentlyContinue | Out-Null
    Remove-Job -Job $tailJobs -Force -ErrorAction SilentlyContinue | Out-Null
}
# Show cursor
Write-Host -NoNewline "`e[?25h"

exit 0
