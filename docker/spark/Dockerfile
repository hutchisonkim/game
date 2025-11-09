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

RUN mkdir -p /opt \
    && echo "Downloading Spark ${SPARK_VERSION} (profile: ${HADOOP_PROFILE})..." \
    && curl -fSL "https://archive.apache.org/dist/spark/spark-${SPARK_VERSION}/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz" -o /tmp/spark.tgz \
    && tar -xzf /tmp/spark.tgz -C /opt \
    && mv /opt/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE} $SPARK_HOME \
    && rm /tmp/spark.tgz

EXPOSE 7077 8080 4040

# Download Microsoft.Spark.Worker (linux-x64) so the Spark worker nodes can
# launch the .NET worker process. The exact version can be adjusted via the
# DOTNET_WORKER_VERSION build-arg. This will be downloaded from GitHub
# releases when building the image locally.
RUN mkdir -p ${DOTNET_WORKER_DIR} \
    && echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..." \
    && curl -fSL "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" -o /tmp/worker.tgz \
    && tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} \
    && rm /tmp/worker.tgz \
    && chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker || true

ENV DOTNET_WORKER_DIR=${DOTNET_WORKER_DIR}

# Start a standalone master and a local worker so the image is useful for local development/tests.
# We start both in the foreground and tail logs to keep the container running.
CMD ["bash", "-c", "$SPARK_HOME/sbin/start-master.sh && \
  $SPARK_HOME/sbin/start-worker.sh spark://localhost:7077 && \
  echo 'Spark master started'; \
  tail -F $SPARK_HOME/logs/*"]
