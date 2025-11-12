# install-packages.ps1
$ErrorActionPreference = 'Stop'

# Enable TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Install Chocolatey
Set-ExecutionPolicy Bypass -Scope Process -Force
Invoke-Expression -Command ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# Install OpenJDK 17 and .NET SDK 8
choco install -y openjdk17 dotnet-sdk --version=8.0.416
