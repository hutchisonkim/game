using Xunit;

namespace Game.Chess.Tests.Integration.Infrastructure;

/// <summary>
/// Defines the "Spark collection" for sharing SparkSession across integration tests.
/// All tests marked with [Collection("Spark collection")] will share the same SparkFixture instance.
/// </summary>
[CollectionDefinition("Spark collection")]
public class SparkCollection : ICollectionFixture<SparkFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
