param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\TestResults\RunnerSnapshots'),
    [string]$Server = '127.0.0.1',
    [int]$Port = 7009,
    [int]$PingTimeoutSec = 2,
    [string]$DotnetCommandLinePattern = '*Game.Chess.Tests.Integration.Runner*',
    [switch]$Quiet
)

function Invoke-RunnerPing {
    param(
        [string]$RunnerHost,
        [int]$Port,
        [int]$TimeoutSec
    )

    $result = [pscustomobject]@{ Status = 'Unknown'; Detail = '' }
    $client = $null
    $stream = $null
    $writer = $null
    $reader = $null

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($RunnerHost, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutSec * 1000)) {
            $client.Close()
            $result.Status = 'Timeout'
            return $result
        }
        $client.EndConnect($async)

        $stream = $client.GetStream()
        $stream.ReadTimeout = $TimeoutSec * 1000
        $stream.WriteTimeout = $TimeoutSec * 1000
        $writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::UTF8)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)

        $writer.WriteLine('PING')
        $resp = $reader.ReadLine()
        $result.Status = if ($resp) { $resp } else { 'NoResponse' }
        $result.Detail = $resp
    }
    catch {
        $result.Status = 'Error'
        $result.Detail = $_.Exception.Message
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($writer) { $writer.Dispose() }
        if ($stream) { $stream.Dispose() }
        if ($client) { $client.Dispose() }
    }

    return $result
}

function Get-ProcessSummary {
    param(
        [object[]]$Items
    )

    $count = 0
    $wsTotal = 0
    foreach ($p in $Items) {
        if ($p) {
            $count += 1
            $wsTotal += [int64]$p.WorkingSetSize
        }
    }
    return [pscustomobject]@{
        Count = $count
        WorkingSetMB = [math]::Round($wsTotal / 1MB, 2)
    }
}

function Format-ProcessLine {
    param($p)
    $mb = [math]::Round([int64]$p.WorkingSetSize / 1MB, 2)
    return "PID=$($p.ProcessId) Name=$($p.Name) WS(MB)=$mb Start=$($p.CreationDate) Command=$($p.CommandLine)"
}

if (-not (Test-Path -Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$outFile = Join-Path $OutputDir "runner-snapshot-$ts.txt"
function Write-SnapshotLine {
    param([string]$Text)
    Add-Content -Path $script:outFile -Value $Text
}

$os = Get-CimInstance Win32_OperatingSystem
$memTotalMb = [math]::Round([double]$os.TotalVisibleMemorySize / 1024, 2)
$memFreeMb = [math]::Round([double]$os.FreePhysicalMemory / 1024, 2)
$memUsedMb = [math]::Round($memTotalMb - $memFreeMb, 2)

$ping = Invoke-RunnerPing -RunnerHost $Server -Port $Port -TimeoutSec $PingTimeoutSec
$portListening = $false
try {
    $portListening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    $portListening = [bool]$portListening
} catch { $portListening = $false }

$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue
$javaSpark = $procs | Where-Object { $_.Name -eq 'java.exe' -and ($_.CommandLine -like '*spark*' -or $_.CommandLine -like '*Game.Chess*') }
$dotnetRunner = $procs | Where-Object { $_.Name -eq 'dotnet.exe' -and ($_.CommandLine -and $_.CommandLine -like $DotnetCommandLinePattern) }
# Fallback: if no dotnet process matched the specific pattern, include all dotnet.exe processes so we don't miss orphaned runners
if (-not $dotnetRunner -or $dotnetRunner.Count -eq 0) {
    $dotnetRunner = $procs | Where-Object { $_.Name -eq 'dotnet.exe' }
}
$pythonSpark = $procs | Where-Object { $_.Name -eq 'python.exe' -and $_.CommandLine -like '*spark*' }

$javaSummary = Get-ProcessSummary -Items $javaSpark
$dotnetSummary = Get-ProcessSummary -Items $dotnetRunner
$pythonSummary = Get-ProcessSummary -Items $pythonSpark

$topMemory = $procs | Sort-Object -Property WorkingSetSize -Descending | Select-Object -First 10

Write-SnapshotLine '# Runner Snapshot'
Write-SnapshotLine "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-SnapshotLine "OutputFile: $outFile"
Write-SnapshotLine "PingStatus: $($ping.Status)"
Write-SnapshotLine "PingDetail: $($ping.Detail)"
Write-SnapshotLine "RunnerPortListening: $portListening"
Write-SnapshotLine "JavaSparkCount: $($javaSummary.Count)"
Write-SnapshotLine "JavaSparkWorkingSetMB: $($javaSummary.WorkingSetMB)"
Write-SnapshotLine "DotnetRunnerCount: $($dotnetSummary.Count)"
Write-SnapshotLine "DotnetRunnerWorkingSetMB: $($dotnetSummary.WorkingSetMB)"
try {
    $portOwner = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object { $_.OwningProcess }
} catch { $portOwner = $null }
if ($portOwner) { Write-SnapshotLine "RunnerPortOwnerPID: $portOwner" } else { Write-SnapshotLine "RunnerPortOwnerPID: None" }

Write-SnapshotLine "DotnetCommandLinePattern: $DotnetCommandLinePattern"
Write-SnapshotLine "PythonSparkCount: $($pythonSummary.Count)"
Write-SnapshotLine "PythonSparkWorkingSetMB: $($pythonSummary.WorkingSetMB)"
Write-SnapshotLine "SystemMemoryMB: Total=$memTotalMb Free=$memFreeMb Used=$memUsedMb"
Write-SnapshotLine "MachineName: $env:COMPUTERNAME"
Write-SnapshotLine "User: $env:USERNAME"
Write-SnapshotLine "Server: $Server"
Write-SnapshotLine "Port: $Port"
Write-SnapshotLine "PingTimeoutSec: $PingTimeoutSec"
Write-SnapshotLine ""
Write-SnapshotLine '# Process details (java.exe with spark / Game.Chess)'
foreach ($p in $javaSpark) { Write-SnapshotLine (Format-ProcessLine $p) }
Write-SnapshotLine ""
Write-SnapshotLine '# Process details (dotnet.exe Game.Chess.Tests.Integration.Runner)'
foreach ($p in $dotnetRunner) { Write-SnapshotLine (Format-ProcessLine $p) }
Write-SnapshotLine ""
Write-SnapshotLine '# Process details (python.exe with spark)'
foreach ($p in $pythonSpark) { Write-SnapshotLine (Format-ProcessLine $p) }
Write-SnapshotLine ""
Write-SnapshotLine '# TCP listeners on runner port'
try {
    $conn = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    if ($conn) {
        foreach ($c in $conn) { Write-SnapshotLine "Local=$($c.LocalAddress):$($c.LocalPort) Remote=$($c.RemoteAddress):$($c.RemotePort) State=$($c.State) PID=$($c.OwningProcess)" }
    } else {
        Write-SnapshotLine 'None'
    }
}
catch {
    Write-SnapshotLine "Get-NetTCPConnection failed: $($_.Exception.Message)"
}
Write-SnapshotLine ""
Write-SnapshotLine '# Top 10 processes by WorkingSet'
foreach ($p in $topMemory) { Write-SnapshotLine (Format-ProcessLine $p) }
Write-SnapshotLine ""

if (-not $Quiet) {
    Write-Host "Snapshot written to $outFile"
    Write-Host "Ping=$($ping.Status) PortListening=$portListening JavaCount=$($javaSummary.Count) DotnetCount=$($dotnetSummary.Count) MemUsedMB=$memUsedMb"
}
