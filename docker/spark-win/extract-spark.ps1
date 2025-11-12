# extract-spark.ps1
param(
    [string]$SparkVersion = "3.5.3",
    [string]$HadoopProfile = "hadoop3"
)

$ErrorActionPreference = 'Stop'
$cachePath = "C:\cache"
$tmpPath = "C:\tmp"
$sparkPath = "C:\spark"

if (!(Test-Path $tmpPath)) { New-Item -ItemType Directory -Path $tmpPath | Out-Null }

$sparkTgz = Join-Path $cachePath "spark-$SparkVersion-bin-$HadoopProfile.tgz"
if (Test-Path $sparkTgz) {
    Copy-Item $sparkTgz "$tmpPath\spark.tgz"
} else {
    Invoke-WebRequest -Uri "https://archive.apache.org/dist/spark/spark-$SparkVersion/spark-$SparkVersion-bin-$HadoopProfile.tgz" -OutFile "$tmpPath\spark.tgz"
}

# Extract Spark
if (!(Test-Path $sparkPath)) { New-Item -ItemType Directory -Path $sparkPath | Out-Null }
tar -xzf "$tmpPath\spark.tgz" -C "C:\"
Rename-Item -Path "C:\spark-$SparkVersion-bin-$HadoopProfile" -NewName $sparkPath
Remove-Item "$tmpPath\spark.tgz" -Force
