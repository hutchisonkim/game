using Xunit;

namespace Game.Chess.Tests.Unit;

[CollectionDefinition("Spark collection")]
public class SparkCollection : ICollectionFixture<SparkIntegrationFixture>
{
    // Collection fixture - no code here. The presence of this class tells xUnit
    // to use SparkIntegrationFixture for any test class that declares
    // [Collection("Spark collection")].
}
