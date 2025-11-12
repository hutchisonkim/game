# Use Windows Server Core with JDK 17
FROM mcr.microsoft.com/windows/servercore:ltsc2022

# --- Arguments ---
ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3
ARG DOTNET_WORKER_VERSION=2.3.0

# --- Environment Variables ---
ENV SPARK_HOME=C:\spark
ENV DOTNET_WORKER_DIR=C:\microsoft-spark-worker
ENV PATH=C:\spark\bin;C:\Progra~1\dotnet;$PATH

# --- Use PowerShell for RUN commands ---
SHELL ["powershell", "-NoProfile", "-Command", "$ErrorActionPreference = 'Stop';"]

# --- Copy scripts and cache ---
COPY .docker_cache C:\cache
COPY install-packages.ps1 extract-spark.ps1 extract-worker.ps1 start-spark.ps1 C:\tmp/

# --- Install Chocolatey + packages ---
RUN powershell -ExecutionPolicy Bypass -File C:\tmp\install-packages.ps1

# --- Extract Spark ---
RUN powershell -ExecutionPolicy Bypass -File C:\tmp\extract-spark.ps1 -SparkVersion $env:SPARK_VERSION -HadoopProfile $env:HADOOP_PROFILE

# --- Extract Microsoft.Spark.Worker ---
RUN powershell -ExecutionPolicy Bypass -File C:\tmp\extract-worker.ps1 -WorkerVersion $env:DOTNET_WORKER_VERSION

# --- Expose Spark ports ---
EXPOSE 7077 8080 4040

# --- Default CMD ---
CMD ["powershell", "-NoProfile", "-File", "C:\\tmp\\start-spark.ps1"]
