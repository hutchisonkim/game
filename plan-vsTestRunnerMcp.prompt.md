## Plan: VsTestRunnerMcp (MCP to control local Spark test runner

TL;DR — Create a small .NET console MCP server `VsTestRunnerMcp` that speaks JSON-RPC over stdio, advertises three tools (start runner, run tests with filter, stop/cleanup runner), and dispatches each tool to the repo's PowerShell scripts (`scripts/spark-runner-start.ps1`, `scripts/run-tests-with-dependencies.ps1`, `scripts/spark-testctl.ps1` + `scripts/force-kill-runners.ps1`). Use a reusable Process helper to run `pwsh` with redirected stdout/stderr and return a single-line JSON-RPC response per request.

### Steps
1. Create project: add new console project at `src/VsTestRunnerMcp` with `dotnet new console -n VsTestRunnerMcp` targeting `net8.0`. Ensure `bin/Release/net8.0/VsTestRunnerMcp.dll` will be produced when built.
2. Implement JSON-RPC stdio loop: add `Program.cs` in `src/VsTestRunnerMcp` that reads stdin line-by-line, parses JSON, and routes `"initialize"` and `"tools/call"` requests, writing one-line JSON responses to stdout.
3. Define initialize response tools: in the `"initialize"` handler return `capabilities.tools` exposing:
   - `start-spark-runner` — Description: "Starts the local Spark runner via script." Input schema: empty object.
   - `run-tests-with-filter` — Description: "Runs dependency-aware tests via scripts/run-tests-with-dependencies.ps1." Input schema: object with optional `"filter"` string (default "Essential=true").
   - `stop-and-cleanup-runner` — Description: "Stops the runner and performs cleanup (force kill fallback)." Input schema: empty object.
   (Return tools under `capabilities.tools` with the basic JSON schemas.)
4. Implement tool dispatching and command templates: when `"tools/call"` arrives, match `params.name` to the tool and build a PowerShell command template (relative to repo root):
   - start-spark-runner -> `pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-runner-start.ps1"`
   - run-tests-with-filter -> `pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/run-tests-with-dependencies.ps1" -Filter "<filter-value>"`
     (use provided `params.input.configuration` or default `Essential=true`)
   - stop-and-cleanup-runner -> run `pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-testctl.ps1" -Stop` then `pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/force-kill-runners.ps1"`
   (Describe these strings in code and pass them to the helper runner.)
5. Add Process helper: create `src/VsTestRunnerMcp/ProcessRunner.cs` exposing `RunPwshCommand(string command, TimeSpan? timeout)` that:
   - Uses `System.Diagnostics.Process` to start `pwsh` (preferred) with `-NoProfile -ExecutionPolicy Bypass -Command "<command>"` or `-File` form when running a script file.
   - Redirects stdout and stderr, waits for exit (or timeout), returns an object with `ExitCode`, `Stdout`, `Stderr`, and combined `Output` string.
6. JSON responses: for every request reply one-line JSON: `{ "jsonrpc":"2.0", "id": <id>, "result": { "exitCode": <int>, "output": "<combined>", "stdout": "...", "stderr": "..." } }` and ensure UTF-8 and newline termination.
7. Build & verify: build `dotnet build -c Release` and confirm `bin/Release/net8.0/VsTestRunnerMcp.dll` exists. Optionally run locally with `dotnet bin/Release/net8.0/VsTestRunnerMcp.dll` and manually send JSON messages on stdin to verify the initialize/tools/call flow.

### Further Considerations
1. PowerShell executable: prefer `pwsh` (used in CI and repo scripts). Option: detect `pwsh` first and fallback to `powershell.exe` if absent.
2. Long-running start: `spark-runner-start.ps1` may detach or be long-lived; the helper should wait for the script to exit and return its output, but not attempt to keep the runner process alive — runner lifecycle is managed by the script. Consider adding a `--async` flag later to start and return immediately.
3. Environment: scripts expect certain env vars (e.g., `SPARK_HOME`, `DOTNET_WORKER_DIR`) and TestDependencyGraph.json at repo root; run the MCP from repo root or have it detect the repo root before invoking scripts.

### Example JSON-RPC interactions (for manual testing)
- Initialize request (stdin -> MCP):
  { "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {} }

- MCP initialize response (stdout -> IDE):
  { "jsonrpc":"2.0", "id": 1, "result": { "capabilities": { "tools": { "start-spark-runner": { "description": "..." }, "run-tests-with-filter": { "description": "..." }, "stop-and-cleanup-runner": { "description": "..." } } } } }

- Call tool to start runner:
  { "jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": { "name": "start-spark-runner", "input": {} } }

- Call tool to run tests:
  { "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": { "name": "run-tests-with-filter", "input": { "filter": "Essential=true" } } }

- Call tool to stop runner:
  { "jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": { "name": "stop-and-cleanup-runner", "input": {} } }

### Next steps after plan approval
- Implement code skeleton in `src/VsTestRunnerMcp` (Program.cs + ProcessRunner.cs)
- Add minimal README with run examples (how to call via `dotnet` and sample JSON payloads)
- Build and manually test locally from repo root

### Notes / Caveats
- `spark-testctl.ps1` uses a TCP control port; local port may differ from CI. The MCP should not hardcode ports; rely on the scripts which already handle defaults.
- Be conservative with `force-kill-runners.ps1` use — it will kill processes aggressively. The MCP `stop-and-cleanup-runner` will run it only after attempting the graceful stop.

