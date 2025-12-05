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
