param(
    [string]$HostName = '127.0.0.1',
    [int]$Port = 5567,
    [int]$TimeoutSeconds = 60
)

$end = (Get-Date).AddSeconds($TimeoutSeconds)
Write-Host "Waiting for ${HostName}:$Port to be available (timeout ${TimeoutSeconds}s)..."
while((Get-Date) -lt $end) {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient($HostName, $Port)
        $tcp.Close()
    Write-Host "Port ${HostName}:$Port is available"
        exit 0
    } catch {
        Start-Sleep -Seconds 1
    }
}
Write-Error "Timed out waiting for ${HostName}:$Port"
exit 1
