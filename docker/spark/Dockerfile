FROM eclipse-temurin:17-jdk

ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3

ENV SPARK_VERSION=${SPARK_VERSION}
ENV HADOOP_PROFILE=${HADOOP_PROFILE}
ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH
ENV SPARK_MASTER_HOST=0.0.0.0

# Location for the Microsoft.Spark.Worker (downloaded during image build so
# worker is available on the Spark worker nodes inside the container).
ARG DOTNET_WORKER_VERSION=2.3.0
ENV DOTNET_WORKER_DIR=/opt/microsoft-spark-worker

# Install tools and download/extract Spark
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates tar procps bash \
    && rm -rf /var/lib/apt/lists/*

# Allow CI or local builds to provide pre-downloaded artifacts under .docker_cache
COPY .docker_cache /cache

EXPOSE 7077 8080 4040

# Download Microsoft.Spark.Worker (linux-x64) so the Spark worker nodes can
# launch the .NET worker process. The exact version can be adjusted via the
# DOTNET_WORKER_VERSION build-arg. This will be downloaded from GitHub
# releases when building the image locally.
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

# Install/extract Microsoft.Spark.Worker from cache or download if not present
RUN mkdir -p ${DOTNET_WORKER_DIR} \
    && if [ -f /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz ]; then \
         echo "Using cached Microsoft.Spark.Worker..."; \
         cp /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz /tmp/worker.tgz; \
       elif [ -f /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz ]; then \
         # Support alternate archive name used in local cache
         echo "Using cached Microsoft.Spark.Worker (alternate name)..."; \
         cp /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz /tmp/worker.tgz; \
       else \
         echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..."; \
         curl -fSL --retry 5 --retry-delay 5 --max-time 120 "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" -o /tmp/worker.tgz; \
       fi \
    && tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} \
    && rm /tmp/worker.tgz \
    && chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker || true

ENV DOTNET_WORKER_DIR=${DOTNET_WORKER_DIR}

# Start a standalone master and a local worker so the image is useful for local development/tests.
# We start both in the foreground and tail logs to keep the container running.
CMD ["bash", "-c", "# Ensure Microsoft.Spark.Worker is present (try download at container start if missing)\n  if [ ! -x \"${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker\" ]; then\n    echo 'Microsoft.Spark.Worker not found, attempting to download at container start...';\n    mkdir -p ${DOTNET_WORKER_DIR};\n    curl -fSL \"https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz\" -o /tmp/worker.tgz && \\\\n    tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} && \\\n    rm -f /tmp/worker.tgz || true;\n    chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker || true;\n  fi;\n  # Start Spark master and worker\n  $SPARK_HOME/sbin/start-master.sh && \n  $SPARK_HOME/sbin/start-worker.sh spark://localhost:7077 && \n  echo 'Spark master started'; \n  # Start Microsoft.Spark.Worker in background if available (this provides the .NET/JVM bridge support inside the container)\n  if [ -x \"${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker\" ]; then\n    echo 'Starting Microsoft.Spark.Worker in background';\n    ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker &\n  fi;\n  tail -F $SPARK_HOME/logs/*"]
