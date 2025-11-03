FROM openjdk:17-jdk-slim

ARG SPARK_VERSION=3.4.1
ARG HADOOP_PROFILE=hadoop3

ENV SPARK_VERSION=${SPARK_VERSION}
ENV HADOOP_PROFILE=${HADOOP_PROFILE}
ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$SPARK_HOME/sbin:$PATH
ENV SPARK_MASTER_HOST=0.0.0.0

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

# Start a standalone master and a local worker so the image is useful for local development/tests.
# We start both in the foreground and tail logs to keep the container running.
CMD ["bash", "-c", "$SPARK_HOME/sbin/start-master.sh && \
  $SPARK_HOME/sbin/start-worker.sh spark://localhost:7077 && \
  echo 'Spark master started'; \
  tail -F $SPARK_HOME/logs/*"]
