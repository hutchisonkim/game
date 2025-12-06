<#  
    Kill-Runners.ps1
    -----------------
    Cleans up leftover .NET runner processes + their Console Window Host
    instances (conhost.exe) caused by test runners, hot reload development,
    or crashed projects.

    Configure $Pattern to match your runner DLL, exe, or project path.

    Example:
        $Pattern = "*Game.Chess.Runner*"

    Run:
        ./Kill-Runners.ps1
#>

param(
    [switch]$ForceKillAllDotnet = $true
)

Write-Host ""
Write-Host "=== Kill-Runners.ps1 ===" -ForegroundColor Cyan
Write-Host "Pattern: $Pattern"
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" 
Write-Host ""

# Gather dotnet processes that match pattern
$dotnetProcs = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'dotnet.exe' }
if (-not $dotnetProcs -or $dotnetProcs.Count -eq 0) {
    Write-Host "No dotnet.exe processes found." -ForegroundColor Green
    return
}

Write-Warning "About to kill ALL detected dotnet.exe processes (this will stop other dotnet apps)."
Write-Host "Press Ctrl-C to cancel within 3 seconds..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

foreach ($proc in $dotnetProcs) {

    Write-Host ""
    Write-Host "Found dotnet runner PID $($proc.ProcessId)" -ForegroundColor Yellow
    Write-Host "CommandLine:" $proc.CommandLine -ForegroundColor DarkGray

    # Kill conhost.exe children
    $children = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ParentProcessId -eq $proc.ProcessId -and
            $_.Name -eq 'conhost.exe'
        }

    if ($children) {
        foreach ($ch in $children) {
            Write-Host " - Killing conhost.exe (PID $($ch.ProcessId))" -ForegroundColor Magenta
            Stop-Process -Id $ch.ProcessId -Force
        }
    } else {
        Write-Host " - No conhost children found." -ForegroundColor DarkGray
    }

    # Kill the dotnet process
    Write-Host " - Killing dotnet.exe (PID $($proc.ProcessId))" -ForegroundColor Red
    # Build a list of this process and all descendant child processes so we terminate the whole tree
    $toTerminate = @()
    $queue = @($proc.ProcessId)
    while ($queue.Count -gt 0) {
        $cur = $queue[0]
        if ($queue.Count -gt 1) { $queue = $queue[1..($queue.Count - 1)] } else { $queue = @() }
        if ($toTerminate -notcontains $cur) { $toTerminate += $cur }
        $children = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.ParentProcessId -eq $cur }
        foreach ($c in $children) {
            if ($toTerminate -notcontains $c.ProcessId) { $queue += $c.ProcessId }
        }
    }

    foreach ($processId in $toTerminate) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
            Write-Host " - Stopped PID $processId" -ForegroundColor Green
        }
        catch {
            Write-Warning " - Stop-Process failed for PID $processId`: $($_.Exception.Message). Trying taskkill..."
            try {
                Start-Process -FilePath "taskkill" -ArgumentList "/PID $processId /F /T" -NoNewWindow -Wait -ErrorAction Stop
                Write-Host " - taskkill succeeded for PID $processId" -ForegroundColor Green
            }
            catch {
                Write-Warning " - taskkill also failed for PID $processId`: $($_.Exception.Message). Trying WMI Terminate..."
                try {
                    $wp = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
                    if ($wp) { Invoke-CimMethod -InputObject $wp -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null; Write-Host " - WMI Terminate invoked for PID $processId" -ForegroundColor Yellow }
                    else { Write-Warning " - Process $processId not found via WMI." }
                }
                catch {
                    Write-Warning " - Failed to terminate PID $processId via WMI: $($_.Exception.Message)"
                }
            }
        }
    }
}

Write-Host ""
Write-Host "Cleanup complete." -ForegroundColor Cyan
Write-Host ""
