# extract-worker.ps1
param(
    [string]$WorkerVersion = "2.3.0"
)

$ErrorActionPreference = 'Stop'
$cachePath = "C:\cache"
$tmpPath = "C:\tmp"
$workerPath = "C:\microsoft-spark-worker"

if (!(Test-Path $tmpPath)) { New-Item -ItemType Directory -Path $tmpPath | Out-Null }
if (!(Test-Path $workerPath)) { New-Item -ItemType Directory -Path $workerPath | Out-Null }

$workerZip = Join-Path $cachePath "Microsoft.Spark.Worker.net8.0.win-x64-$WorkerVersion.zip"
if (Test-Path $workerZip) {
    Copy-Item $workerZip "$tmpPath\worker.zip"
} else {
    Invoke-WebRequest -Uri "https://github.com/dotnet/spark/releases/download/v$WorkerVersion/Microsoft.Spark.Worker.net8.0.win-x64-$WorkerVersion.zip" -OutFile "$tmpPath\worker.zip"
}

Expand-Archive -LiteralPath "$tmpPath\worker.zip" -DestinationPath $workerPath -Force
Remove-Item "$tmpPath\worker.zip" -Force
