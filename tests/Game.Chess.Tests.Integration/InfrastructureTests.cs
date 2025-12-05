using Xunit;
using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Infrastructure tests validating Spark connectivity and basic board setup.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Infrastructure")]
public class InfrastructureTests
{
    private readonly SparkSession _spark;

    public InfrastructureTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void BasicDataFrame_CreateAndCount_Returns3Rows()
    {
        // Arrange
        var data = new List<GenericRow>
        {
            new GenericRow(new object[] { 1, "a" }),
            new GenericRow(new object[] { 2, "b" }),
            new GenericRow(new object[] { 3, "c" })
        };

        var schema = new StructType(new[]
        {
            new StructField("id", new IntegerType()),
            new StructField("value", new StringType())
        });

        // Act
        DataFrame df = _spark.CreateDataFrame(data, schema);
        long count = df.Count();
        var dfSchema = df.Schema();

        // Assert
        Assert.Equal(3, count);
        Assert.Contains(dfSchema.Fields, f => f.Name == "id");
        Assert.Contains(dfSchema.Fields, f => f.Name == "value");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void DefaultBoard_Initialize_CreatesStandardChessStartPosition()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;

        // Act
        board.Initialize();

        // Assert - Pawns
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 1]);
            Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 6]);
        }

        // Assert - Rooks
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[0, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[7, 7]);

        // Assert - Knights
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[1, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[6, 7]);

        // Assert - Bishops
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[2, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[5, 7]);

        // Assert - Queens
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 7]);

        // Assert - Kings
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 7]);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void DefaultBoard_Dimensions_Are8x8()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;

        // Act
        board.Initialize();

        // Assert
        Assert.Equal(board.Width * board.Height, board.Cell.Length);
        Assert.Equal(64, board.Cell.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void BasicDataFrame_CreateAndCount_Returns3Rows_Refactored()
    {
        // Arrange - Test works with any implementation
        var data = new List<GenericRow>
        {
            new GenericRow(new object[] { 1, "a" }),
            new GenericRow(new object[] { 2, "b" }),
            new GenericRow(new object[] { 3, "c" })
        };

        var schema = new StructType(new[]
        {
            new StructField("id", new IntegerType()),
            new StructField("value", new StringType())
        });

        // Act
        DataFrame df = _spark.CreateDataFrame(data, schema);
        long count = df.Count();
        var dfSchema = df.Schema();

        // Assert
        Assert.Equal(3, count);
        Assert.Contains(dfSchema.Fields, f => f.Name == "id");
        Assert.Contains(dfSchema.Fields, f => f.Name == "value");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void DefaultBoard_Initialize_CreatesStandardChessStartPosition_Refactored()
    {
        // Arrange - Board is independent of policy
        var board = ChessPolicy.Board.Default;

        // Act
        board.Initialize();

        // Assert - Pawns
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 1]);
            Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 6]);
        }

        // Assert - Rooks
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[0, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[7, 7]);

        // Assert - Knights
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[1, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[6, 7]);

        // Assert - Bishops
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[2, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[5, 7]);

        // Assert - Queens
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 7]);

        // Assert - Kings
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 7]);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void DefaultBoard_Dimensions_Are8x8_Refactored()
    {
        // Arrange - Board is independent of policy
        var board = ChessPolicy.Board.Default;

        // Act
        board.Initialize();

        // Assert
        Assert.Equal(board.Width * board.Height, board.Cell.Length);
        Assert.Equal(64, board.Cell.Length);
    }
}
