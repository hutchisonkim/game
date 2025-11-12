# --- Base image ---
FROM eclipse-temurin:17-jdk

ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3
ARG DOTNET_WORKER_VERSION=2.3.0

ENV SPARK_VERSION=${SPARK_VERSION}
ENV HADOOP_PROFILE=${HADOOP_PROFILE}
ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH
ENV SPARK_MASTER_HOST=0.0.0.0

# Updated DOTNET_WORKER_DIR to include version folder
ENV DOTNET_WORKER_DIR=/opt/microsoft-spark-worker/Microsoft.Spark.Worker-${DOTNET_WORKER_VERSION}

# --- Install tools ---
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates tar procps bash wget gnupg apt-transport-https \
    && rm -rf /var/lib/apt/lists/*

# --- Copy local cache ---
COPY .docker_cache /cache

EXPOSE 7077 8080 4040

# --- Download and extract Spark ---
RUN mkdir -p /opt \
    && if [ -f /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz ]; then \
         echo "Using cached Spark tarball..."; \
         cp /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz /tmp/spark.tgz; \
       else \
         echo "Downloading Spark ${SPARK_VERSION}..."; \
         curl -fSL "https://archive.apache.org/dist/spark/spark-${SPARK_VERSION}/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz" -o /tmp/spark.tgz; \
       fi \
    && tar -xzf /tmp/spark.tgz -C /opt \
    && mv /opt/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE} $SPARK_HOME \
    && rm /tmp/spark.tgz

# --- Download and extract Microsoft.Spark.Worker into versioned folder ---
RUN mkdir -p /opt/microsoft-spark-worker \
    && if [ -f /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz ]; then \
         echo "Using cached Microsoft.Spark.Worker..."; \
         cp /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz /tmp/worker.tgz; \
       elif [ -f /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz ]; then \
         echo "Using cached Microsoft.Spark.Worker (alternate name)..."; \
         cp /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz /tmp/worker.tgz; \
       else \
         echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..."; \
         curl -fSL --retry 5 --retry-delay 5 --max-time 120 \
           "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" \
           -o /tmp/worker.tgz; \
       fi \
    && tar -xzf /tmp/worker.tgz -C /opt/microsoft-spark-worker \
    && rm /tmp/worker.tgz \
    && chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker

# --- Install .NET 8 SDK ---
RUN wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update && apt-get install -y dotnet-sdk-8.0

# --- Startup CMD ---
CMD ["bash", "-c", "\
  if [ ! -x \"${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker\" ]; then \
      echo 'Microsoft.Spark.Worker not found, attempting download at container start...'; \
      mkdir -p /opt/microsoft-spark-worker && \
      curl -fSL \"https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz\" -o /tmp/worker.tgz && \
      tar -xzf /tmp/worker.tgz -C /opt/microsoft-spark-worker && rm -f /tmp/worker.tgz && \
      chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker; \
  fi; \
  $SPARK_HOME/sbin/start-master.sh && \
  $SPARK_HOME/sbin/start-worker.sh spark://localhost:7077 && \
  echo 'Spark master started'; \
  if [ -x \"${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker\" ]; then \
      echo 'Starting Microsoft.Spark.Worker in background'; \
      ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker & \
  fi; \
  tail -F $SPARK_HOME/logs/*"]
