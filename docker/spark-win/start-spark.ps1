# start-spark.ps1
$ErrorActionPreference = 'Stop'

Start-Process -NoNewWindow -FilePath "C:\spark\sbin\start-master.cmd"
Start-Process -NoNewWindow -FilePath "C:\spark\sbin\start-worker.cmd" -ArgumentList "spark://localhost:7077"

$workerExe = "C:\microsoft-spark-worker\Microsoft.Spark.Worker.exe"
if (Test-Path $workerExe) {
    Start-Process -NoNewWindow -FilePath $workerExe
}

# Keep container alive
while ($true) { Start-Sleep -Seconds 3600 }
