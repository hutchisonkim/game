# Runner snapshot utilities

Two helper scripts capture and summarize runner health to track leaks or orphaned Spark/.NET processes over time. Reports are timestamped so you can compare runs.

## One-off snapshot

```
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/spark-runner-snapshot.ps1
```

- Output file: `TestResults/RunnerSnapshots/runner-snapshot-<yyyyMMdd-HHmmss>.txt` (folder is created if missing).
- Quick TCP ping to the runner (`127.0.0.1:7009` by default) with a 2s timeout; status is recorded even if the runner is down.
- Captures counts and working-set memory for Spark `java.exe`, the .NET runner, and Spark `python.exe` processes, plus top memory consumers and listener info.
- Flags to tweak defaults:
  - `-OutputDir <path>` to redirect where snapshots land.
  - `-Server <host> -Port <port>` to point at a different runner endpoint.
  - `-PingTimeoutSec <n>` to adjust the 2-second ping window.
  - `-Quiet` to suppress console summary.

## Summarize snapshots

```
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/spark-runner-snapshot-summary.ps1
```

- Reads the most recent 20 snapshots in `TestResults/RunnerSnapshots` and prints a table of ping status, port listener presence, and process/memory totals.
- Flags:
  - `-SnapshotDir <path>` to target a different folder.
  - `-Latest <n>` to change how many snapshots are listed (newest first in the table output).

## Suggested workflow

1) Run the snapshot script before and after a test iteration or suspected OOM to capture state.
2) Use the summary script to see trends (ping failures, orphaned `java.exe`/`dotnet.exe`, growing working sets) without opening individual files.
3) Optionally check individual snapshot files for detailed process command lines and TCP listener details when diagnosing leaks.
