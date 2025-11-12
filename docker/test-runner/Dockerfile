FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /workspace

# Signal to tests that they are running inside the compose/test-runner container.
ENV RUN_TESTS_IN_COMPOSE=1

# Install curl for health checks and other small utilities
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates openjdk-17-jdk tar procps bash iproute2 net-tools \
        && rm -rf /var/lib/apt/lists/*

# Allow CI or local builds to provide pre-downloaded artifacts under .docker_cache
COPY .docker_cache /cache

# Download a lightweight Spark distribution so the test-runner can start the JVM
ARG SPARK_VERSION=3.5.3
ARG HADOOP_PROFILE=hadoop3
ENV SPARK_VERSION=${SPARK_VERSION}
ENV HADOOP_PROFILE=${HADOOP_PROFILE}
ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH

RUN mkdir -p /opt \
        && echo "Acquiring Spark ${SPARK_VERSION} (profile: ${HADOOP_PROFILE})..." \
        && if [ -f /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz ]; then \
                 echo "Using cached Spark tarball from /cache"; \
                 cp /cache/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz /tmp/spark.tgz; \
             else \
                 echo "Downloading Spark ${SPARK_VERSION}..."; \
                 curl -fSL --retry 5 --retry-delay 5 --max-time 120 "https://archive.apache.org/dist/spark/spark-${SPARK_VERSION}/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE}.tgz" -o /tmp/spark.tgz; \
             fi \
        && tar -xzf /tmp/spark.tgz -C /opt \
        && mv /opt/spark-${SPARK_VERSION}-bin-${HADOOP_PROFILE} $SPARK_HOME \
        && rm /tmp/spark.tgz

# Download Microsoft.Spark.Worker so the test-runner can launch the JVM bridge if required
ARG DOTNET_WORKER_VERSION=2.3.0
ENV DOTNET_WORKER_DIR=/opt/microsoft-spark-worker
RUN mkdir -p ${DOTNET_WORKER_DIR} \
        && echo "Acquiring Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..." \
        && if [ -f /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz ]; then \
                 echo "Using cached Microsoft.Spark.Worker from /cache"; \
                 cp /cache/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz /tmp/worker.tgz; \
             elif [ -f /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz ]; then \
                 echo "Using cached Microsoft.Spark.Worker (alternate name) from /cache"; \
                 cp /cache/Microsoft.Spark.Worker.net8.0.linux-x64-${DOTNET_WORKER_VERSION}.tar.gz /tmp/worker.tgz; \
             else \
                 echo "Downloading Microsoft.Spark.Worker ${DOTNET_WORKER_VERSION}..."; \
                 curl -fSL --retry 5 --retry-delay 5 --max-time 120 "https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" -o /tmp/worker.tgz; \
             fi \
        && tar -xzf /tmp/worker.tgz -C ${DOTNET_WORKER_DIR} \
        && rm /tmp/worker.tgz \
        && chmod +x ${DOTNET_WORKER_DIR}/Microsoft.Spark.Worker || true

ENV DOTNET_WORKER_DIR=${DOTNET_WORKER_DIR}

# By default copy the repository into the image. When running via compose we
# mount the repository as a volume which will override this copy, but copying
# here allows building the image and verifying Dockerfile syntax.
COPY . /workspace

# Build the reserve-port helper and make it available in PATH so the runtime
# entry script can pick an ephemeral port inside the container before running tests.
RUN if [ -d /workspace/ReservePort ]; then \
            echo "Building ReservePort helper..." && \
            dotnet publish /workspace/ReservePort -c Release -r linux-x64 -o /opt/tools/reserveport --self-contained false; \
            printf '#!/bin/sh\nexec dotnet /opt/tools/reserveport/ReservePort.dll "$@"' > /usr/local/bin/reserveport; \
            chmod +x /usr/local/bin/reserveport; \
        else \
            echo "No ReservePort project found, skipping build"; \
        fi

# Default entrypoint: run the single test specified by the compose command.
ENTRYPOINT ["bash","-c"]
