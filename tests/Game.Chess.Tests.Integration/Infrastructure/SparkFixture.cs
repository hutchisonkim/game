using Microsoft.Spark.Sql;

namespace Game.Chess.Tests.Integration.Infrastructure;

/// <summary>
/// Provides a shared SparkSession for integration tests.
/// The session is created once and reused across all tests in the collection.
/// </summary>
public class SparkFixture : IDisposable
{
    private SparkSession? _spark;
    private bool _disposed;

    /// <summary>
    /// Gets the shared SparkSession instance.
    /// </summary>
    public SparkSession Spark
    {
        get
        {
            if (_spark != null) return _spark;

            try
            {
                var backendPort = Environment.GetEnvironmentVariable("DOTNETBACKEND_PORT");
                var workerPort = Environment.GetEnvironmentVariable("PYTHON_WORKER_FACTORY_PORT");

                _spark = SparkSession
                    .Builder()
                    .AppName("ChessPolicyIntegrationTests")
                    .Master("local[*]")
                    .Config("spark.sql.shuffle.partitions", "4")
                    .Config("spark.driver.memory", "4g")
                    .Config("spark.executor.memory", "2g")
                    .Config("spark.driver.maxResultSize", "2g")
                    .Config("spark.dotnet.backend.port", backendPort)
                    .Config("spark.dotnet.worker.factory.port", workerPort)
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

    /// <summary>
    /// Disposes the SparkSession when tests complete.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _spark?.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Error stopping Spark session: {ex.Message}");
        }
        finally
        {
            _spark = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
