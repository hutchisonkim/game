<#  
    Kill-Runners.ps1
    -----------------
    Cleans up leftover .NET runner processes + their Console Window Host
    instances (conhost.exe) caused by test runners, hot reload development,
    or crashed projects.

    **Excludes:** VS Code .NET processes (CPS/BuildHost)

    Configure $Pattern to match your runner DLL, exe, or project path.

    Example:
        $Pattern = "*Game.Chess.Runner*"

    Run:
        ./Kill-Runners.ps1
#>

param(
    [switch]$ForceKillAllDotnet = $true
)

# ANSI color helper
function Get-AnsiCode {
    param([string]$color)
    switch ($color.ToLower()) {
        'black' { '30' }
        'darkblue' { '34' }
        'darkgreen' { '32' }
        'darkcyan' { '36' }
        'darkred' { '31' }
        'darkmagenta' { '35' }
        'darkyellow' { '33' }
        'gray' { '37' }
        'darkgray' { '90' }
        'blue' { '94' }
        'green' { '92' }
        'cyan' { '96' }
        'red' { '91' }
        'magenta' { '95' }
        'yellow' { '93' }
        'white' { '97' }
        default { '0' }
    }
}

function Write-ColorLine { param([string]$text, [string]$color) try { $code = Get-AnsiCode $color; if ($code -ne '0') { Write-Host "`e[${code}m${text}`e[0m" } else { Write-Host $text } } catch { Write-Host $text } }

Write-Host ""
Write-ColorLine "=== Kill-Runners.ps1 ===" "Cyan"
Write-Host "Pattern: $Pattern"
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

if (-not $IsWindows) {
    Write-ColorLine "Running on non-Windows; using cross-platform cleanup." "Cyan"
    foreach ($name in @('dotnet', 'java')) {
        $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($p in $procs) {
            try {
                Stop-Process -Id $p.Id -Force -ErrorAction Stop
                Write-ColorLine " - Stopped $name (PID $($p.Id))" "Green"
            }
            catch {
                Write-Warning " - Failed to stop $name (PID $($p.Id)): $($_.Exception.Message)"
            }
        }
    }
    Write-ColorLine "Cleanup complete." "Cyan"
    return
}

# Gather dotnet processes that match pattern
# Exclude VS Code dotnet processes (CPS/BuildHost)
$dotnetProcs = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'dotnet.exe' -and
    $_.CommandLine -notmatch 'visualstudio-projectsystem-buildhost' -and
    $_.CommandLine -notmatch 'CPS.*BuildHost'
}

if (-not $dotnetProcs -or $dotnetProcs.Count -eq 0) {
    Write-ColorLine "No dotnet.exe runner processes found (VS Code processes excluded)." "Green"
    return
}

Write-Warning "About to kill detected dotnet.exe runner processes (excluding VS Code)."
Write-ColorLine "Press Ctrl-C to cancel within 1 second(s)..." "Yellow"
Start-Sleep -Seconds 1

foreach ($proc in $dotnetProcs) {

    Write-Host ""
    Write-ColorLine "Found dotnet runner PID $($proc.ProcessId)" "Yellow"
    Write-ColorLine "CommandLine: $($proc.CommandLine)" "DarkGray"

    # Kill conhost.exe children
    $children = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ParentProcessId -eq $proc.ProcessId -and
            $_.Name -eq 'conhost.exe'
        }

    if ($children) {
        foreach ($ch in $children) {
            Write-ColorLine " - Killing conhost.exe (PID $($ch.ProcessId))" "Magenta"
            Stop-Process -Id $ch.ProcessId -Force
        }
    } else {
        Write-ColorLine " - No conhost children found." "DarkGray"
    }

    # Kill the dotnet process
    Write-ColorLine " - Killing dotnet.exe (PID $($proc.ProcessId))" "Red"
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
            Write-ColorLine " - Stopped PID $processId" "Green"
        }
        catch {
            Write-Warning " - Stop-Process failed for PID $processId`: $($_.Exception.Message). Trying taskkill..."
            try {
                Start-Process -FilePath "taskkill" -ArgumentList "/PID $processId /F /T" -NoNewWindow -Wait -ErrorAction Stop
                Write-ColorLine " - taskkill succeeded for PID $processId" "Green"
            }
            catch {
                Write-Warning " - taskkill also failed for PID $processId`: $($_.Exception.Message). Trying WMI Terminate..."
                try {
                    $wp = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
                    if ($wp) { Invoke-CimMethod -InputObject $wp -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null; Write-ColorLine " - WMI Terminate invoked for PID $processId" "Yellow" }
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
Write-ColorLine "Cleanup complete." "Cyan"
Write-Host ""
