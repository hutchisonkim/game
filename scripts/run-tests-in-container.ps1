try {
	$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
	$rootInfo = Resolve-Path (Join-Path $scriptDir "..")
	$root = $rootInfo.Path
	$composeFile = Join-Path $root "docker-compose.yml"

	Write-Host "Bringing up spark service..."
	& docker compose -f "$composeFile" up -d spark

	Write-Host "Running tests inside test-runner container..."
	& docker compose -f "$composeFile" run --rm test-runner bash -c "until curl -sf http://spark:8080 ; do echo 'Waiting for Spark...'; sleep 3; done; dotnet test /workspace/tests/Game.Chess.Tests.Unit/Game.Chess.Tests.Unit.csproj -v minimal"
	$exit = $LASTEXITCODE

	Write-Host "Tearing down compose stack..."
	& docker compose -f "$composeFile" down -v

	exit $exit
} catch {
	Write-Error "Error running containerized tests: $_"
	exit 1
}
