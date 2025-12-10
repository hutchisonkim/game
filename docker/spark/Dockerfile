# --------------------------------------------------------------
#  .NET for Apache Spark + Spark 3.5.3 (hadoop3) – CI-ready image
# --------------------------------------------------------------
FROM eclipse-temurin:17-jdk

# ---- Build arguments ------------------------------------------------
ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3
ARG DOTNET_WORKER_VERSION=2.3.0

# ---- Environment ----------------------------------------------------
ENV SPARK_HOME=/opt/spark \
    DOTNET_WORKER_DIR=/opt/microsoft-spark-worker
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH

# ---- Cache directory (CI can drop pre-downloaded files here) -------
COPY .docker_cache /cache

# ---- Spark ----------------------------------------------------------
RUN mkdir -p /opt && \
    if [ -f /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz ]; then \
      echo "Using cached Spark tarball" && \
      cp /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz /tmp/spark.tgz; \
    else \
      echo "Downloading Spark ${SPARK_VERSION}" && \
      curl -fSL \
        "https://archive.apache.org/dist/spark/spark-${SPARK_VERSION}/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz" \
        -o /tmp/spark.tgz; \
    fi && \
    tar -xzf /tmp/spark.tgz -C /opt && \
    mv /opt/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE} ${SPARK_HOME} && \
    rm /tmp/spark.tgz

# ---- Microsoft.Spark.Worker (rename + chmod) -----------------------
RUN mkdir -p ${DOTNET_WORKER_DIR} && \
    if [ -f /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz ]; then \
      echo "Using cached Microsoft.Spark.Worker" && \
      cp /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz /tmp/worker.tgz; \
    elif [ -f /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz ]; then \
      echo "Using cached Microsoft.Spark.Worker (alt name)" && \
      cp /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz /tmp/worker.tgz; \
    else \
      echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}" && \
      curl -fSL --retry 5 --retry-delay 5 --max-time 120 \
        "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" \
        -o /tmp/worker.tgz; \
    fi && \
    tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} && \
    rm /tmp/worker.tgz && \
    # <-- EXACT RENAME YOU NEED --> \
    mv ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker-${DOTNET_WORKER_VERSION} \
       ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker && \
    chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker

# ---- Expose Spark UI ports (optional, useful for local dev) --------
EXPOSE 7077 8080 4040

# ---- Install base tools + .NET SDK at the end (reduces layer churn) ----
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      curl ca-certificates tar procps bash wget gnupg apt-transport-https libxml2-utils netcat-openbsd zip net-tools && \
    rm -rf /var/lib/apt/lists/* && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb \
      -O /tmp/ms.deb && \
    dpkg -i /tmp/ms.deb && rm /tmp/ms.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-8.0 && \
    rm -rf /var/lib/apt/lists/*

# ---- Default command – just a shell (CI starts the worker) --------
CMD ["bash"]