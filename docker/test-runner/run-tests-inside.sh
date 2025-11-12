#!/bin/sh
set -e

echo "Starting Spark master & worker inside test-runner"
export SPARK_HOME=/opt/spark
export DOTNET_WORKER_DIR=/opt/microsoft-spark-worker

# Reserve DOTNETBACKEND_PORT early so the .NET runtime picks a stable port
# before any JVM is launched (reduces race between JVM connecting back and
# the .NET backend listener being ready).
if [ -z "$DOTNETBACKEND_PORT" ]; then
  if command -v reserveport >/dev/null 2>&1; then
    echo "[run-tests-inside.sh] Reserving DOTNETBACKEND_PORT early via reserveport helper..."
    DOTNETBACKEND_PORT=$(reserveport)
    export DOTNETBACKEND_PORT
    echo "[run-tests-inside.sh] Reserved DOTNETBACKEND_PORT=$DOTNETBACKEND_PORT"
  else
    echo "[run-tests-inside.sh] reserveport helper not available; DOTNETBACKEND_PORT will be reserved later if needed"
  fi
else
  echo "[run-tests-inside.sh] Using provided DOTNETBACKEND_PORT=$DOTNETBACKEND_PORT"
fi

# Ensure DOTNETBACKEND_HOST is set to a reachable address inside the container.
# Using 0.0.0.0 tells the .NET backend to listen on all interfaces which helps
# the JVM connect back to the host when it resolves to 127.0.0.1.
export DOTNETBACKEND_HOST=${DOTNETBACKEND_HOST:-0.0.0.0}

# Default to using the locally-started Spark master (when running inside the
# test-runner we start master/worker here). If SPARK_MASTER_URL is already set
# by compose to point at the external "spark" service, prefer that value.
export SPARK_MASTER_URL=${SPARK_MASTER_URL:-spark://localhost:7077}

# Start Spark master and worker
$SPARK_HOME/sbin/start-master.sh
$SPARK_HOME/sbin/start-worker.sh spark://localhost:7077

# Start Microsoft.Spark.Worker if present. If not present, try a few
# candidate release archive names when downloading to avoid a single
# hard-coded filename causing 404s when the upstream release naming
# varies between versions.
if [ -x "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" ]; then
  echo "Starting Microsoft.Spark.Worker"
  "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" &
else
  echo "Microsoft.Spark.Worker not found in $DOTNET_WORKER_DIR; attempting to download..."
  # Allow overriding version via DOTNET_WORKER_VERSION build-arg in Dockerfile; fall back to 2.3.0 if not set
  DOTNET_WORKER_VERSION=${DOTNET_WORKER_VERSION:-2.3.0}
  success=0
  # Candidate filenames (covers common naming variations)
  for candidate in \
    "microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tar.gz" \
    "microsoft-spark-worker-${DOTNET_WORKER_VERSION}-linux-x64.tar.gz" \
    "microsoft-spark-worker_${DOTNET_WORKER_VERSION}_linux-x64.tgz"; do
    url="https://github.com/dotnet/spark/releases/download/v${DOTNET_WORKER_VERSION}/${candidate}"
    echo "Attempting download from: $url"
    if curl -fSL --retry 5 --retry-delay 5 --max-time 120 "$url" -o /tmp/worker.tgz; then
      success=1
      break
    fi
  done

  if [ "$success" -eq 1 ]; then
    mkdir -p "$DOTNET_WORKER_DIR"
    tar -xzf /tmp/worker.tgz -C "$DOTNET_WORKER_DIR"
    rm -f /tmp/worker.tgz
    chmod +x "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" || true
    echo "Starting downloaded Microsoft.Spark.Worker"
    "$DOTNET_WORKER_DIR/Microsoft.Spark.Worker" &
  else
    echo "Failed to download Microsoft.Spark.Worker; continuing without worker" >&2
  fi
fi

# Wait for Spark web UI
i=0
until curl -sSf http://localhost:8080 >/dev/null 2>&1 || [ $i -ge 20 ]; do
  i=$((i+1))
  sleep 1
done

echo "Running vstest"
# When running Spark and the .NET test process in the same container, the
# DOTNETBACKEND_HOST was set earlier to a sensible default (0.0.0.0) so don't
# override it here. Just echo the effective values for diagnostics.
echo "[run-tests-inside.sh] DOTNETBACKEND_HOST=$DOTNETBACKEND_HOST DOTNETBACKEND_PORT=${DOTNETBACKEND_PORT:-<unset>}"

# If DOTNETBACKEND_PORT is not set, attempt to reserve a free port inside the container
# using the ReservePort helper (built into the image). This ensures tests start with
# DOTNETBACKEND_PORT set so Microsoft.Spark can bind its backend listener on the expected port.
if [ -z "$DOTNETBACKEND_PORT" ]; then
  if command -v reserveport >/dev/null 2>&1; then
    echo "[run-tests-inside.sh] Reserving DOTNETBACKEND_PORT via reserveport helper..."
    DOTNETBACKEND_PORT=$(reserveport)
    export DOTNETBACKEND_PORT
    echo "[run-tests-inside.sh] Reserved DOTNETBACKEND_PORT=$DOTNETBACKEND_PORT"
  else
    echo "[run-tests-inside.sh] reserveport helper not available; proceeding without pre-reserved port"
  fi
else
  echo "[run-tests-inside.sh] Using provided DOTNETBACKEND_PORT=$DOTNETBACKEND_PORT"
fi

# Allow an optional TEST_CLASS env var to run only tests for a specific class (handy for debugging)
if [ -n "$TEST_CLASS" ]; then
  echo "[run-tests-inside.sh] Running tests for class: $TEST_CLASS"
  # Start the dotnet vstest process in background; use sh -c to handle the
  # TestCaseFilter argument which contains special characters.
  sh -c "dotnet vstest /workspace/tests/Game.Chess.Tests.Unit/bin/Debug/net8.0/Game.Chess.Tests.Unit.dll --TestCaseFilter:FullyQualifiedName~${TEST_CLASS} --logger \"console;verbosity=normal\"" &
else
  # Run the host-built test DLL (mounted via volume)
  sh -c "dotnet vstest /workspace/tests/Game.Chess.Tests.Unit/bin/Debug/net8.0/Game.Chess.Tests.Unit.dll --logger \"console;verbosity=normal\"" &
fi

# Capture PID of backgrounded test process, wait briefly and emit diagnostic
TEST_PID=$!
echo "[run-tests-inside.sh] Started test runner (pid $TEST_PID), waiting 2s for backend to initialize..."
sleep 2
echo "[run-tests-inside.sh] Listening TCP ports (diagnostics):"
ss -ltnp || netstat -ltnp || true

# Wait for the background test process to finish and propagate its exit code
wait $TEST_PID
exit_code=$?
echo "[run-tests-inside.sh] Tests exited with code $exit_code"
exit $exit_code
