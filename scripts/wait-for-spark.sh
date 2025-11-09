#!/usr/bin/env bash
HOST=${1:-127.0.0.1}
PORT=${2:-5567}
TIMEOUT=${3:-60}

end=$((SECONDS+TIMEOUT))

echo "Waiting for $HOST:$PORT to be available (timeout ${TIMEOUT}s)..."
while [ $SECONDS -lt $end ]; do
  # Try to open TCP connection using /dev/tcp; if not supported, fall back to timeout+nc if available
  if bash -c ">/dev/tcp/$HOST/$PORT" 2>/dev/null; then
    echo "Port $HOST:$PORT is available"
    exit 0
  fi
  sleep 1
done

echo "Timed out waiting for $HOST:$PORT" >&2
exit 1
