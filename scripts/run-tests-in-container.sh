#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"

echo "Bringing up spark service..."
docker compose -f "$COMPOSE_FILE" up -d spark

echo "Running tests inside test-runner container..."
docker compose -f "$COMPOSE_FILE" run --rm test-runner bash -c 'until curl -sf http://spark:8080 ; do echo "Waiting for Spark..."; sleep 3; done; dotnet test /workspace/tests/Game.Chess.Tests.Unit/Game.Chess.Tests.Unit.csproj -v minimal'
EXIT_CODE=$?

echo "Tearing down compose stack..."
docker compose -f "$COMPOSE_FILE" down -v

exit $EXIT_CODE
