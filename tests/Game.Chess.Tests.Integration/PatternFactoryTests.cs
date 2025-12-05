using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for pattern factory functionality and pattern validation.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "Pattern")]
public class PatternFactoryTests
{
    private readonly SparkSession _spark;

    public PatternFactoryTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void PatternFactory_GetPatterns_ReturnsNonEmptyDataFrame()
    {
        // Act
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();

        // Assert
        Assert.True(patternsDf.Count() > 0);
        Assert.Contains("src_conditions", patternsDf.Columns());
        Assert.Contains("dst_conditions", patternsDf.Columns());
        Assert.Contains("delta_x", patternsDf.Columns());
        Assert.Contains("delta_y", patternsDf.Columns());
        Assert.Contains("sequence", patternsDf.Columns());
        Assert.Contains("dst_effects", patternsDf.Columns());
    }

    [Theory]
    [InlineData(ChessPolicy.Piece.Knight, 2, 1, "Knight L-shape moves")]
    [InlineData(ChessPolicy.Piece.Knight, 1, 2, "Knight L-shape moves (reversed)")]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Knight")]
    public void PatternFactory_KnightPatterns_HaveCorrectDeltas(
        ChessPolicy.Piece pieceType,
        int expectedAbsDeltaX,
        int expectedAbsDeltaY,
        string description)
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pieceInt = (int)pieceType;

        // Act
        string filter = $"(src_conditions & {pieceInt}) != 0 AND ABS(delta_x) = {expectedAbsDeltaX} AND ABS(delta_y) = {expectedAbsDeltaY}";
        long count = patternsDf.Filter(filter).Count();

        // Assert
        Assert.True(count > 0, $"Expected at least one {description}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Pawn")]
    public void PatternFactory_PawnCapturePatterns_ExistForDiagonalMoves()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pawn = (int)ChessPolicy.Piece.Pawn;
        int foe = (int)ChessPolicy.Piece.Foe;

        // Act - Pawn diagonal captures have abs(delta_x) = 1 and delta_y = 1
        string filter = $"(src_conditions & {pawn}) != 0 AND (dst_conditions & {foe}) != 0 AND ABS(delta_x) = 1 AND delta_y = 1";
        long count = patternsDf.Filter(filter).Count();

        // Assert
        Assert.True(count > 0, "Expected at least one pawn capture pattern (diagonal capture)");
    }

    [Theory]
    [InlineData(ChessPolicy.Piece.King, 8, "King moves in 8 directions")]
    [InlineData(ChessPolicy.Piece.Rook, 4, "Rook moves in 4 directions (orthogonal)")]
    [InlineData(ChessPolicy.Piece.Bishop, 4, "Bishop moves in 4 directions (diagonal)")]
    [InlineData(ChessPolicy.Piece.Queen, 8, "Queen moves in 8 directions")]
    [Trait("Performance", "Fast")]
    public void PatternFactory_SlidingPiecePatterns_HaveMinimumDirections(
        ChessPolicy.Piece pieceType,
        int minimumDirections,
        string description)
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pieceInt = (int)pieceType;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act - Count distinct delta combinations for public moves
        string filter = $"(src_conditions & {pieceInt}) != 0 AND (sequence & {publicSeq}) != 0";
        var directions = patternsDf
            .Filter(filter)
            .Select("delta_x", "delta_y")
            .Distinct()
            .Count();

        // Assert
        Assert.True(directions >= minimumDirections, 
            $"Expected at least {minimumDirections} distinct move directions for {description}, got {directions}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void PieceFactory_GetPieces_Returns64SquaresWithCorrectSchema()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Act
        var piecesDf = new ChessPolicy.PieceFactory(_spark).GetPieces(board);

        // Assert
        Assert.Equal(64, piecesDf.Count());
        var schema = piecesDf.Schema();
        Assert.Contains(schema.Fields, f => f.Name == "x");
        Assert.Contains(schema.Fields, f => f.Name == "y");
        Assert.Contains(schema.Fields, f => f.Name == "piece");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void PatternFactory_GetPatterns_ReturnsNonEmptyDataFrame_Refactored()
    {
        // Act - PatternFactory is shared, but use via refactored policy
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();

        // Assert
        Assert.True(patternsDf.Count() > 0);
        Assert.Contains("src_conditions", patternsDf.Columns());
        Assert.Contains("dst_conditions", patternsDf.Columns());
        Assert.Contains("delta_x", patternsDf.Columns());
        Assert.Contains("delta_y", patternsDf.Columns());
        Assert.Contains("sequence", patternsDf.Columns());
        Assert.Contains("dst_effects", patternsDf.Columns());
    }

    [Theory]
    [InlineData(ChessPolicy.Piece.Knight, 2, 1, "Knight L-shape moves")]
    [InlineData(ChessPolicy.Piece.Knight, 1, 2, "Knight L-shape moves (reversed)")]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Knight")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void PatternFactory_KnightPatterns_HaveCorrectDeltas_Refactored(
        ChessPolicy.Piece pieceType,
        int expectedAbsDeltaX,
        int expectedAbsDeltaY,
        string description)
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pieceInt = (int)pieceType;

        // Act
        string filter = $"(src_conditions & {pieceInt}) != 0 AND ABS(delta_x) = {expectedAbsDeltaX} AND ABS(delta_y) = {expectedAbsDeltaY}";
        long count = patternsDf.Filter(filter).Count();

        // Assert
        Assert.True(count > 0, $"Expected at least one {description}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Pawn")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void PatternFactory_PawnCapturePatterns_ExistForDiagonalMoves_Refactored()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pawn = (int)ChessPolicy.Piece.Pawn;
        int foe = (int)ChessPolicy.Piece.Foe;

        // Act - Pawn diagonal captures have abs(delta_x) = 1 and delta_y = 1
        string filter = $"(src_conditions & {pawn}) != 0 AND (dst_conditions & {foe}) != 0 AND ABS(delta_x) = 1 AND delta_y = 1";
        long count = patternsDf.Filter(filter).Count();

        // Assert
        Assert.True(count > 0, "Expected at least one pawn capture pattern (diagonal capture)");
    }

    [Theory]
    [InlineData(ChessPolicy.Piece.King, 8, "King moves in 8 directions")]
    [InlineData(ChessPolicy.Piece.Rook, 4, "Rook moves in 4 directions (orthogonal)")]
    [InlineData(ChessPolicy.Piece.Bishop, 4, "Bishop moves in 4 directions (diagonal)")]
    [InlineData(ChessPolicy.Piece.Queen, 8, "Queen moves in 8 directions")]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void PatternFactory_SlidingPiecePatterns_HaveMinimumDirections_Refactored(
        ChessPolicy.Piece pieceType,
        int minimumDirections,
        string description)
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pieceInt = (int)pieceType;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act - Count distinct delta combinations for public moves
        string filter = $"(src_conditions & {pieceInt}) != 0 AND (sequence & {publicSeq}) != 0";
        var directions = patternsDf
            .Filter(filter)
            .Select("delta_x", "delta_y")
            .Distinct()
            .Count();

        // Assert
        Assert.True(directions >= minimumDirections, 
            $"Expected at least {minimumDirections} distinct move directions for {description}, got {directions}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void PieceFactory_GetPieces_Returns64SquaresWithCorrectSchema_Refactored()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Act
        var piecesDf = new ChessPolicy.PieceFactory(_spark).GetPieces(board);

        // Assert
        Assert.Equal(64, piecesDf.Count());
        var schema = piecesDf.Schema();
        Assert.Contains(schema.Fields, f => f.Name == "x");
        Assert.Contains(schema.Fields, f => f.Name == "y");
        Assert.Contains(schema.Fields, f => f.Name == "piece");
    }
}
