param(
    [string]$Filter = 'Essential=True',
    [switch]$AlwaysStart
)

Write-Host "[spark-start-and-test] Checking runner readiness..."
try {
    $resp = pwsh -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/spark-testctl.ps1" -Ping
}
catch {
    $resp = $null
}

if ($AlwaysStart -or $resp -ne 'PONG') {
    Write-Host "[spark-start-and-test] Starting runner via spark-runner-start.ps1"
    pwsh -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/spark-runner-start.ps1"
} else {
    Write-Host "[spark-start-and-test] Runner already running (PONG). Skipping start."
}

Write-Host "[spark-start-and-test] Invoking tests with filter: $Filter"
pwsh -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/spark-testctl.ps1" -Filter $Filter

# Wait for ALL subprocess cleanup to complete
Write-Host "[spark-start-and-test] Waiting for all test subprocesses to exit..."
$maxWaitSeconds = 30
$pollIntervalMs = 500
$startTime = Get-Date
$allProcessesGone = $false

while ((Get-Date) -lt $startTime.AddSeconds($maxWaitSeconds)) {
    # Check for testhost/vstest/datacollector processes
    $processesRunning = @(Get-Process -Name "testhost", "vstest.console", "datacollector", "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -in "testhost", "vstest.console", "datacollector" -or ($_.ProcessName -eq "dotnet" -and $_.CommandLine -like "*vstest*") })
    
    if ($processesRunning.Count -eq 0) {
        Write-Host "[spark-start-and-test] ✓ All test subprocesses have exited"
        $allProcessesGone = $true
        break
    }
    
    Write-Host "[spark-start-and-test] Waiting... ($([Math]::Round(((Get-Date) - $startTime).TotalSeconds, 1))s) - Processes still running: $($processesRunning.ProcessName -join ', ')"
    Start-Sleep -Milliseconds $pollIntervalMs
}

if (-not $allProcessesGone) {
    $elapsedSeconds = ((Get-Date) - $startTime).TotalSeconds
    Write-Warning "[spark-start-and-test] WARNING: Not all test subprocesses exited after ${elapsedSeconds:F1}s"
    $remainingProcesses = @(Get-Process -Name "testhost", "vstest.console", "datacollector" -ErrorAction SilentlyContinue)
    if ($remainingProcesses.Count -gt 0) {
        Write-Warning "[spark-start-and-test] Still running: $($remainingProcesses.ProcessName -join ', ')"
    }
}

Write-Host "[spark-start-and-test] ✓ Subprocess cleanup complete"
exit 0
