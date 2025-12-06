param(
    [string]$SnapshotDir = (Join-Path $PSScriptRoot '..\TestResults\RunnerSnapshots'),
    [int]$Latest = 20
)

if (-not (Test-Path $SnapshotDir)) {
    Write-Error "Snapshot directory not found: $SnapshotDir"
    exit 1
}

$files = Get-ChildItem -Path $SnapshotDir -Filter 'runner-snapshot-*.txt' -File -ErrorAction SilentlyContinue |
    Sort-Object -Property Name -Descending |
    Select-Object -First $Latest

if (-not $files) {
    Write-Error "No snapshot files found in $SnapshotDir"
    exit 1
}

function Get-SnapshotMetrics {
    param([string]$Path)

    $lines = Get-Content -Path $Path -TotalCount 50
    $map = @{ }
    foreach ($ln in $lines) {
        if ($ln -notmatch '^[A-Za-z]') { continue }
        $parts = $ln.Split(':', 2)
        if ($parts.Count -ne 2) { continue }
        $key = $parts[0].Trim()
        $val = $parts[1].Trim()
        $map[$key] = $val
    }

    $ts = $map['Timestamp']
    $tsObj = $null
    if ($ts) { $tsObj = [datetime]::Parse($ts) }

    return [pscustomobject]@{
        Timestamp   = $tsObj
        PingStatus  = $map['PingStatus']
        PortListening = $map['RunnerPortListening']
        JavaCount   = [int]($map['JavaSparkCount'] | ForEach-Object { $_ })
        JavaMB      = [double]($map['JavaSparkWorkingSetMB'] | ForEach-Object { $_ })
        DotnetCount = [int]($map['DotnetRunnerCount'] | ForEach-Object { $_ })
        DotnetMB    = [double]($map['DotnetRunnerWorkingSetMB'] | ForEach-Object { $_ })
        PythonCount = [int]($map['PythonSparkCount'] | ForEach-Object { $_ })
        PythonMB    = [double]($map['PythonSparkWorkingSetMB'] | ForEach-Object { $_ })
        SystemMemory = $map['SystemMemoryMB']
        FileName    = (Split-Path $Path -Leaf)
        FilePath    = $Path
    }
}

$rows = foreach ($f in $files) { Get-SnapshotMetrics -Path $f.FullName }
$rows = $rows | Sort-Object -Property Timestamp

Write-Host "Showing $($rows.Count) snapshots from $SnapshotDir (newest last):" -ForegroundColor Cyan
$rows |
    Select-Object Timestamp, PingStatus, PortListening, JavaCount, JavaMB, DotnetCount, DotnetMB, PythonCount, PythonMB, SystemMemory, FileName |
    Format-Table -AutoSize
