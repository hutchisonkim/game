using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Xunit;

namespace Game.Chess.Tests.Unit;

// Integration fixture that starts a Spark container for tests using DotNet.Testcontainers
public class SparkIntegrationFixture : IAsyncLifetime
{
    private readonly TestcontainersContainer _sparkContainer;

    public SparkIntegrationFixture()
    {
        // Reserve a host port for the .NET backend and set it in the process env before we start the container.
        // This allows the JVM inside the Spark container to connect back to the test process running on the host.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var backendPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        // Set the DOTNETBACKEND_PORT for the current test process so the .NET Spark backend uses this port.
        Environment.SetEnvironmentVariable("DOTNETBACKEND_PORT", backendPort.ToString());

        _sparkContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("game-spark:latest")
            .WithName($"game_spark_{Guid.NewGuid():N}")
            .WithCleanUp(true)
            .WithPortBinding(7077, 7077)
            .WithPortBinding(8080, 8080)
            // Tell the JVM inside the container how to connect back to the host .NET backend.
            .WithEnvironment("DOTNETBACKEND_HOST", "host.docker.internal")
            .WithEnvironment("DOTNETBACKEND_PORT", backendPort.ToString())
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sparkContainer.StartAsync();

        // Ensure code that reads SPARK_MASTER_URL picks the Compose-style host mapping on the host
        Environment.SetEnvironmentVariable("SPARK_MASTER_URL", "spark://localhost:7077");
    }

    public async Task DisposeAsync()
    {
        await _sparkContainer.StopAsync();
    }
}
