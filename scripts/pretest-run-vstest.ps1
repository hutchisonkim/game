param(
    [string]$targetPath,
    [string]$trxPath
)

try {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback,0)
    $listener.Start()
    $port = ($listener.LocalEndpoint).Port
    $listener.Stop()

    Write-Host "[PRETEST] Reserved DOTNETBACKEND_PORT=$port"

    # Set environment variable for this process so dotnet vstest inherits it
    $env:DOTNETBACKEND_PORT = $port

    # Use quoted logger argument so PowerShell doesn't split on ';'
    dotnet vstest $targetPath --logger:"trx;LogFileName=$trxPath"
}
catch {
    Write-Error $_
    exit 1
}
