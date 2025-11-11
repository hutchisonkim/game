using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;

namespace Game.Chess.Tests.Unit;

/// <summary>
/// Starts a Spark container for tests using DotNet.Testcontainers.
/// This fixture reserves a host port for the .NET backend and passes
/// it into the container so the Spark JVM can connect back to the test process.
/// </summary>
public class SparkTestcontainersFixture : Xunit.IAsyncLifetime
{
    private readonly TestcontainersContainer _sparkContainer;
    private readonly bool _runningInCompose;

    public SparkTestcontainersFixture()
    {
        // Detect if tests are running inside the compose/test-runner container.
        // In that environment we should not try to create new Docker containers
        // using Testcontainers (the container won't have access to the host
        // Docker daemon). When running in-compose we rely on the external
        // `spark` service provided by docker-compose.
        _runningInCompose = string.Equals(Environment.GetEnvironmentVariable("RUN_TESTS_IN_COMPOSE"), "1")
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));

        if (_runningInCompose)
        {
            // Do not build a Testcontainers container when running inside compose.
            // Set placeholders for _sparkContainer so DisposeAsync can skip operations.
            _sparkContainer = null;
            return;
        }
        // Reserve a host port for the .NET backend so the Spark JVM can connect back.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var backendPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        // Make the port available to the test process and to the container.
        Environment.SetEnvironmentVariable("DOTNETBACKEND_PORT", backendPort.ToString());

        _sparkContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("game-spark:latest")
            .WithName($"game_spark_{Guid.NewGuid():N}")
            .WithCleanUp(true)
            .WithPortBinding(7077, 7077)
            .WithPortBinding(8080, 8080)
            // Also bind the dynamically reserved backend port so the JVM inside the container
            // is reachable from the host at the same port number.
            .WithPortBinding(backendPort, backendPort)
            .WithEnvironment("DOTNETBACKEND_HOST", "host.docker.internal")
            .WithEnvironment("DOTNETBACKEND_PORT", backendPort.ToString())
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();
    }

    public async Task InitializeAsync()
    {
        if (_runningInCompose)
        {
            // Running inside docker-compose test-runner. Do not attempt to start new containers.
            Console.WriteLine("[TEST FIXTURE] Running inside compose/test-runner; skipping Testcontainers creation.");

            // When running in compose, the spark service is reachable at the compose service name 'spark'.
            // Ensure tests know the SPARK master URL and which host the JVM should connect back to.
            Environment.SetEnvironmentVariable("SPARK_MASTER_URL", Environment.GetEnvironmentVariable("SPARK_MASTER_URL") ?? "spark://spark:7077");

            // The JVM inside the 'spark' container should connect to this test-runner container. Use the
            // compose service name 'test-runner' (Docker DNS resolves it) unless the env overrides it.
            Environment.SetEnvironmentVariable("DOTNETBACKEND_HOST", Environment.GetEnvironmentVariable("DOTNETBACKEND_HOST") ?? "test-runner");

            Console.WriteLine($"[TEST FIXTURE] SPARK_MASTER_URL={Environment.GetEnvironmentVariable("SPARK_MASTER_URL")}");
            Console.WriteLine($"[TEST FIXTURE] DOTNETBACKEND_HOST={Environment.GetEnvironmentVariable("DOTNETBACKEND_HOST")}");

            // Nothing more to do here.
            return;
        }

        await _sparkContainer.StartAsync();

        // Log helpful debugging info immediately after start so test output contains container status.
        Console.WriteLine($"[TEST FIXTURE] Spark container id: {_sparkContainer.Id}");
        Console.WriteLine($"[TEST FIXTURE] DOTNETBACKEND_PORT set in host: {Environment.GetEnvironmentVariable("DOTNETBACKEND_PORT")}");

        try
        {
            // Exec `printenv` inside the container to show environment variables the container sees.
            var execResult = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", "printenv" });
            Console.WriteLine("[TEST FIXTURE] Container environment:\n" + execResult.Stdout);
        }
        catch (System.Exception ex)
        {
            Console.WriteLine("[TEST FIXTURE] Failed to exec into container: " + ex);
        }

        // Ensure Microsoft.Spark.Worker is present and running inside the container.
        // This helps when images built without the worker are used (download & start at runtime).
        try
        {
            var checkWorker = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", "[ -x $DOTNET_WORKER_DIR/Microsoft.Spark.Worker ] && echo present || echo missing" });
            var workerStatus = (checkWorker.Stdout ?? string.Empty).Trim();
            Console.WriteLine($"[TEST FIXTURE] Microsoft.Spark.Worker status inside container: {workerStatus}");

            if (workerStatus == "missing")
            {
                Console.WriteLine("[TEST FIXTURE] Attempting to download Microsoft.Spark.Worker inside container...");
                var downloadCmd = $"mkdir -p $DOTNET_WORKER_DIR && curl -fSL https://github.com/dotnet/spark/releases/download/v2.3.0/microsoft-spark-worker_2.3.0_linux-x64.tar.gz -o /tmp/worker.tgz && tar -xzf /tmp/worker.tgz -C $DOTNET_WORKER_DIR && rm -f /tmp/worker.tgz && chmod +x $DOTNET_WORKER_DIR/Microsoft.Spark.Worker";
                var dl = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", downloadCmd });
                Console.WriteLine("[TEST FIXTURE] Download stdout:\n" + dl.Stdout);
                Console.WriteLine("[TEST FIXTURE] Download stderr:\n" + dl.Stderr);
            }

            // Try to start the worker in background if present
            var startCmd = "[ -x $DOTNET_WORKER_DIR/Microsoft.Spark.Worker ] && ($DOTNET_WORKER_DIR/Microsoft.Spark.Worker &>/tmp/dotnet-spark-worker.log &) || echo 'no-worker'";
            var startRes = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", startCmd });
            Console.WriteLine("[TEST FIXTURE] Start worker stdout:\n" + startRes.Stdout);
            Console.WriteLine("[TEST FIXTURE] Start worker stderr:\n" + startRes.Stderr);

            // Wait a moment and then capture process list and listening ports
            await Task.Delay(1000);
            var ps = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", "ps aux | head -n 200" });
            Console.WriteLine("[TEST FIXTURE] Container process list:\n" + ps.Stdout);

            var ss = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", "ss -ltnp || netstat -ltnp || true" });
            Console.WriteLine("[TEST FIXTURE] Container listening TCP ports:\n" + ss.Stdout + ss.Stderr);

            var workerLog = await _sparkContainer.ExecAsync(new[] { "/bin/sh", "-c", "tail -n 200 /tmp/dotnet-spark-worker.log 2>/dev/null || true" });
            Console.WriteLine("[TEST FIXTURE] Worker log (tail):\n" + workerLog.Stdout + workerLog.Stderr);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[TEST FIXTURE] Failed to ensure/start Microsoft.Spark.Worker in container: " + ex);
        }

        // Prefer SPARK_MASTER_URL from env; if not present, set a sensible default for localhost mapping.
        Environment.SetEnvironmentVariable("SPARK_MASTER_URL", "spark://localhost:7077");
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_sparkContainer is null)
            {
                // Nothing to stop when running in compose/test-runner mode.
                return;
            }

            await _sparkContainer.StopAsync();
        }
        catch (Exception ex)
        {
            // Surface but don't rethrow during test cleanup — log to stdout so CI logs contain the info.
            Console.WriteLine("[TEST FIXTURE] Error while stopping Spark container: " + ex);
        }
    }
}
