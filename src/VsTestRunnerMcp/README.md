# VsTestRunnerMcp

Small JSON-RPC MCP that exposes tools to control the local Spark test runner via repository PowerShell scripts.

Run:

```pwsh
dotnet run --project src/VsTestRunnerMcp --configuration Release
```

Example initialize request (send as single-line JSON to stdin):

```json
{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {} }
```

Example tool calls:

Start runner:
```json
{ "jsonrpc":"2.0", "id": 2, "method":"tools/call", "params": { "name":"start-spark-runner", "input": {} } }
```

Run tests with filter:
```json
{ "jsonrpc":"2.0", "id": 3, "method":"tools/call", "params": { "name":"run-tests-with-filter", "input": { "filter": "Essential=true" } } }
```

Stop and cleanup:
```json
{ "jsonrpc":"2.0", "id": 4, "method":"tools/call", "params": { "name":"stop-and-cleanup-runner", "input": {} } }
```

Notes:
- The MCP runs `pwsh` with `-NoProfile -ExecutionPolicy Bypass` and passes script `-File` arguments.
- Run the MCP from the repository root so relative script paths resolve correctly.
