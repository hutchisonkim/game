param(
    [string]$Filter = 'Performance=Fast',
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
