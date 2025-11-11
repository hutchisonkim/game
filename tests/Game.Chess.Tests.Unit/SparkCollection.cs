using Xunit;

namespace Game.Chess.Tests.Unit
{
    [CollectionDefinition("Spark collection")]
    public class SparkCollection : ICollectionFixture<SparkTestcontainersFixture>
    {
        // Collection fixture - no code here. The presence of this class tells xUnit
        // to use SparkTestcontainersFixture for any test class that declares
        // [Collection("Spark collection")].
    }
}
