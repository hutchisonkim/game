#!/bin/sh
set -e

echo "Starting Spark master & worker inside test-runner"
export SPARK_HOME=/opt/spark
export DOTNET_WORKER_DIR=/opt/microsoft-spark-worker

# Start Spark master and worker
$SPARK_HOME/sbin/start-master.sh
$SPARK_HOME/sbin/start-worker.sh spark://localhost:7077

# Start Microsoft.Spark.Worker if present
if [ -x "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" ]; then
  echo "Starting Microsoft.Spark.Worker"
  "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" &
fi

# Wait for Spark web UI
i=0
until curl -sSf http://localhost:8080 >/dev/null 2>&1 || [ $i -ge 20 ]; do
  i=$((i+1))
  sleep 1
done

echo "Running vstest"
# When running Spark and the .NET test process in the same container, ensure the JVM will
# connect back to localhost. Allow DOTNETBACKEND_PORT to be provided by the reserved-port helper
# on the host; otherwise the default behavior will apply.
export DOTNETBACKEND_HOST=${DOTNETBACKEND_HOST:-localhost}
echo "[run-tests-inside.sh] DOTNETBACKEND_HOST=$DOTNETBACKEND_HOST DOTNETBACKEND_PORT=${DOTNETBACKEND_PORT:-<unset>}"
# Run the host-built test DLL (mounted via volume)
exec dotnet vstest /workspace/tests/Game.Chess.Tests.Unit/bin/Debug/net8.0/Game.Chess.Tests.Unit.dll --logger "console;verbosity=normal"
