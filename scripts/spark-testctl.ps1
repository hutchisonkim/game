param(
    [string]$Filter = "",
    [string]$Server = "127.0.0.1",
    [int]$Port = 7009,
    [switch]$Ping,
    [switch]$Stop
)

function Invoke-Runner([string]$Body) {
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

if ($Ping) {
    $resp = Invoke-Runner "PING"
    Write-Host $resp
    exit 0
}

if ($Stop) {
    $resp = Invoke-Runner "STOP"
    Write-Host $resp
    exit 0
}


# Default: RUN, optionally with filter text
$body = if ([string]::IsNullOrWhiteSpace($Filter)) { "RUN" } else { "RUN $Filter" }
$resp = Invoke-Runner $body
Write-Host $resp

# After RUN, tail the latest test log from the publish dir
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'tests\Game.Chess.Tests.Integration.Runner\bin\Release\net8.0\win-x64\publish'
$logDir = Join-Path $publishDir 'test-logs'

if (Test-Path $logDir) {
    # Start tail job for the newest test log
    $tailJob = Start-Job -ArgumentList $logDir -ScriptBlock {
        param($dir)
        $maxWait = 5
        $elapsed = 0
        # Wait up to 5s for a new test log to appear
        while ($elapsed -lt $maxWait) {
            $f = Get-ChildItem -Path $dir -Filter 'test-output-*.log' -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($f) { 
                Get-Content -Path $f.FullName -Wait
                break 
            }
            Start-Sleep -Seconds 1
            $elapsed += 1
        }
    }

    # Stream tail job output while it runs
    while ($tailJob.State -eq 'Running') {
        Receive-Job -Job $tailJob -ErrorAction SilentlyContinue | Write-Host
        Start-Sleep -Milliseconds 300
    }
    # Drain final output
    Receive-Job -Job $tailJob -ErrorAction SilentlyContinue | Write-Host
    Remove-Job -Job $tailJob -Force -ErrorAction SilentlyContinue
}
