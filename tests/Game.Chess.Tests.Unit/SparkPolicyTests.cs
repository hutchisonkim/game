using Xunit;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using System.Net.Sockets;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using System.Linq;

namespace Game.Chess.Tests.Unit;

[Trait("Feature", "ChessSparkPolicy")]
[Collection("Spark collection")]
public class ChessSparkPolicyTests
{
    // Static constructor runs before any instance code or test execution. We reserve
    // and set DOTNETBACKEND_PORT here so Microsoft.Spark picks it up early (it
    // reads the env on static initialization in some code paths).
    static ChessSparkPolicyTests()
    {
        try
        {
            // If DOTNETBACKEND_PORT is already set (for example when running inside
            // the compose test-runner which reserves it), reuse that value instead
            // of reserving a new port. This avoids races where the test overrides
            // the port reserved by the runner and causes the JVM/backend mismatch.
            var existing = Environment.GetEnvironmentVariable("DOTNETBACKEND_PORT");
            if (!string.IsNullOrWhiteSpace(existing) && int.TryParse(existing, out var existingPort))
            {
                Console.WriteLine($"[STATIC] Using existing DOTNETBACKEND_PORT={existingPort} from environment");
                return;
            }

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var backendPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            Environment.SetEnvironmentVariable("DOTNETBACKEND_PORT", backendPort.ToString());
            Console.WriteLine($"[STATIC] Reserved DOTNETBACKEND_PORT={backendPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[STATIC] Failed to reserve DOTNETBACKEND_PORT: " + ex);
        }
    }
    // Create Spark and ChessPolicy lazily so tests that don't require Spark
    // (for example, BoardInitialization_CreatesCorrectStartingPositions) don't
    // attempt to start a Spark worker/connection when it's not available.
    private SparkSession? _spark;
    private ChessPolicy? _policy;

    // Fallback: ensure a Testcontainers-managed Spark container is started if collection fixture
    // didn't run for any reason. Using a static container avoids starting multiple times.
    private static TestcontainersContainer? s_testContainer;

    private SparkSession Spark
    {
        get
        {
            if (_spark != null) return _spark;

            EnsureSparkContainerStarted();
            // Prefer SPARK_MASTER_URL (set by CI or by docker-compose). If not provided,
            // default to the docker-compose mapping on the host so running tests locally
            // against the `spark` service works (compose maps 7077 to host).
            var masterUrl = Environment.GetEnvironmentVariable("SPARK_MASTER_URL");
            var sparkMaster = string.IsNullOrWhiteSpace(masterUrl) ? "spark://localhost:7077" : masterUrl;

            try
            {
                _spark = SparkSession
                    .Builder()
                    .AppName("ChessPolicyTests")
                    .Master(sparkMaster)
                    .Config("spark.sql.shuffle.partitions", "1") // small local jobs
                    .GetOrCreate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create Spark session: {ex}");
                throw;
            }

            return _spark!;
        }
    }

    private static void EnsureSparkContainerStarted()
    {
        if (s_testContainer != null) return;

        // If running inside the compose/test-runner container, do not attempt to
        // start a new Docker container via Testcontainers (no Docker daemon access).
        // Rely on the compose-provided `spark` service and env vars instead.
        var runningInCompose = string.Equals(Environment.GetEnvironmentVariable("RUN_TESTS_IN_COMPOSE"), "1")
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
        if (runningInCompose)
        {
            Console.WriteLine("[TEST] Running inside compose/test-runner; skipping Testcontainers creation.");
            Environment.SetEnvironmentVariable("SPARK_MASTER_URL", Environment.GetEnvironmentVariable("SPARK_MASTER_URL") ?? "spark://spark:7077");
            Environment.SetEnvironmentVariable("DOTNETBACKEND_HOST", Environment.GetEnvironmentVariable("DOTNETBACKEND_HOST") ?? "test-runner");
            return;
        }

        // Reuse the DOTNETBACKEND_PORT if the static constructor already set it,
        // otherwise reserve one now (rare). This ensures the same port is used by
        // the test process and passed into the container.
        var envPort = Environment.GetEnvironmentVariable("DOTNETBACKEND_PORT");
        int backendPort;
        if (!string.IsNullOrWhiteSpace(envPort) && int.TryParse(envPort, out backendPort))
        {
            Console.WriteLine($"[TEST] Using existing DOTNETBACKEND_PORT={backendPort} from environment");
        }
        else
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            backendPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            Environment.SetEnvironmentVariable("DOTNETBACKEND_PORT", backendPort.ToString());
            Console.WriteLine($"[TEST] Reserved DOTNETBACKEND_PORT={backendPort}");
        }

            s_testContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("game-spark:latest")
            .WithName($"game_spark_{System.Guid.NewGuid():N}")
            .WithCleanUp(true)
            .WithPortBinding(7077, 7077)
            .WithPortBinding(8080, 8080)
            .WithEnvironment("DOTNETBACKEND_HOST", "host.docker.internal")
            .WithEnvironment("DOTNETBACKEND_PORT", backendPort.ToString())
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();

        // Start synchronously and set SPARK_MASTER_URL for tests
        s_testContainer.StartAsync().GetAwaiter().GetResult();
        Console.WriteLine($"[TEST] Started spark container: {s_testContainer.Id}");

        // Wait for the JVM backend port inside the container to be reachable from the host.
        // The container is configured to listen on the backend port we passed in via env and bound to the same host port.
        var attempts = 0;
        var maxAttempts = 30; // ~30s
        var connected = false;
        while (attempts++ < maxAttempts)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var task = client.ConnectAsync("127.0.0.1", backendPort);
                    if (task.Wait(TimeSpan.FromSeconds(1)) && client.Connected)
                    {
                        connected = true;
                        Console.WriteLine($"[TEST] JVM backend is accepting connections on 127.0.0.1:{backendPort}");
                        break;
                    }
                }
            }
            catch
            {
                // ignore and retry
            }

            System.Threading.Thread.Sleep(1000);
        }

        if (!connected)
        {
            Console.WriteLine($"[TEST] Warning: JVM backend did not accept connection on 127.0.0.1:{backendPort} after {maxAttempts}s");
        }

        // Emit lots of diagnostic info from inside the container so we can see what the JVM/worker sees.
        try
        {
            var printenv = s_testContainer.ExecAsync(new[] { "/bin/sh", "-c", "printenv" }).GetAwaiter().GetResult();
            var ps = s_testContainer.ExecAsync(new[] { "/bin/sh", "-c", "ps aux | head -n 200" }).GetAwaiter().GetResult();
            var ss = s_testContainer.ExecAsync(new[] { "/bin/sh", "-c", "ss -ltnp || netstat -ltnp || true" }).GetAwaiter().GetResult();
            var logs = s_testContainer.ExecAsync(new[] { "/bin/sh", "-c", "ls -l $SPARK_HOME/logs || true; tail -n 200 $SPARK_HOME/logs/* 2>/dev/null || true" }).GetAwaiter().GetResult();

            // Print to console (best-effort) and persist to a diagnostics file in the test run directory so we can inspect it later.
            var diag = new System.Text.StringBuilder();
            diag.AppendLine($"[TEST] Container id: {s_testContainer.Id}");
            diag.AppendLine("--- CONTAINER ENV ---");
            diag.AppendLine(printenv.Stdout ?? string.Empty);
            diag.AppendLine("--- PROCESS LIST ---");
            diag.AppendLine(ps.Stdout ?? string.Empty);
            diag.AppendLine("--- LISTENING PORTS ---");
            diag.AppendLine(ss.Stdout ?? string.Empty);
            diag.AppendLine(ss.Stderr ?? string.Empty);
            diag.AppendLine("--- SPARK LOGS (tail) ---");
            diag.AppendLine(logs.Stdout ?? string.Empty);
            diag.AppendLine(logs.Stderr ?? string.Empty);

            Console.WriteLine(diag.ToString());

            try
            {
                var diagPath = System.IO.Path.Combine(Environment.CurrentDirectory, $"ContainerDiagnostics_{s_testContainer.Id}.log");
                System.IO.File.WriteAllText(diagPath, diag.ToString());
                Console.WriteLine($"[TEST] Wrote container diagnostics to: {diagPath}");
            }
            catch (Exception fileEx)
            {
                Console.WriteLine("[TEST] Failed to write diagnostics file: " + fileEx);
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine("[TEST] Failed to exec diagnostics inside container: " + ex);
        }

        Environment.SetEnvironmentVariable("SPARK_MASTER_URL", "spark://localhost:7077");
    }

    private ChessPolicy Policy => _policy ??= new ChessPolicy(Spark);



    [Fact]
    public void BoardInitialization_CreatesCorrectStartingPositions()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Pawns
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 1]);
            Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 6]);
        }

        // Rooks
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[0, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[7, 7]);

        // Knights
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[1, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[6, 7]);

        // Bishops
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[2, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[5, 7]);

        // Queens
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 7]);

        // Kings
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 7]);
    }

    [Fact]
    public void PieceFactory_ReturnsCorrectNumberOfRows()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var piecesDf = new ChessPolicy.PieceFactory(Spark).GetPieces(board);

        Assert.Equal(64, piecesDf.Count());
        var schema = piecesDf.Schema();
        Assert.Contains(schema.Fields, f => f.Name == "x");
        Assert.Contains(schema.Fields, f => f.Name == "y");
        Assert.Contains(schema.Fields, f => f.Name == "piece");
    }

    [Fact]
    public void GetPerspectives_AssignsGenericFlagsCorrectly()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);

        Assert.Contains("generic_piece", perspectivesDf.Columns());
        Assert.Contains("perspective_id", perspectivesDf.Columns());

        // check at least one Self, Ally, Foe flag present
        var firstRow = perspectivesDf.Collect().First();
        int genericValue = (int)firstRow.Get(4); // generic_piece
        Assert.True((genericValue & (int)ChessPolicy.Piece.Self) != 0
                    || (genericValue & (int)ChessPolicy.Piece.Ally) != 0
                    || (genericValue & (int)ChessPolicy.Piece.Foe) != 0);
    }

    [Fact]
    public void TimelineService_GeneratesCorrectTimesteps()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White };

        var perspectivesDf = Policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 2);

        Assert.Contains("timestep", timelineDf.Columns());

        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
        Assert.Contains(2, timesteps);
    }

    [Fact]
    public void PatternFactory_ReturnsPatterns()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();
        Assert.True(patternsDf.Count() > 0);
        Assert.Contains("src_conditions", patternsDf.Columns());
        Assert.Contains("dst_conditions", patternsDf.Columns());
        Assert.Contains("delta_x", patternsDf.Columns());
        Assert.Contains("delta_y", patternsDf.Columns());
    }
}
