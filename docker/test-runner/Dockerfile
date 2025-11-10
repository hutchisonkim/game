FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /workspace

# Install curl for health checks and other small utilities
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates openjdk-17-jdk tar procps bash \
    && rm -rf /var/lib/apt/lists/*

# Download a lightweight Spark distribution so the test-runner can start the JVM
ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3
ENV SPARK_VERSION=${SPARK_VERSION}
ENV HADOOP_PROFILE=${HADOOP_PROFILE}
ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH

RUN mkdir -p /opt \
    && echo "Downloading Spark ${SPARK_VERSION} (profile: ${HADOOP_PROFILE})..." \
    && curl -fSL "https://archive.apache.org/dist/spark/spark-${SPARK_VERSION}/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz" -o /tmp/spark.tgz \
    && tar -xzf /tmp/spark.tgz -C /opt \
    && mv /opt/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE} $SPARK_HOME \
    && rm /tmp/spark.tgz

# Download Microsoft.Spark.Worker so the test-runner can launch the JVM bridge if required
ARG DOTNET_WORKER_VERSION=2.3.0
ENV DOTNET_WORKER_DIR=/opt/microsoft-spark-worker
RUN mkdir -p ${DOTNET_WORKER_DIR} \
    && echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..." \
    && curl -fSL "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" -o /tmp/worker.tgz \
    && tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} \
    && rm /tmp/worker.tgz \
    && chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker || true

ENV DOTNET_WORKER_DIR=${DOTNET_WORKER_DIR}

# By default copy the repository into the image. When running via compose we
# mount the repository as a volume which will override this copy, but copying
# here allows building the image and verifying Dockerfile syntax.
COPY . /workspace

# Default entrypoint: run the single test specified by the compose command.
ENTRYPOINT ["bash","-c"]
