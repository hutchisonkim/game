using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Castling special move.
/// Castling is a move involving the king and rook that can only occur if neither has moved (Mint flag).
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Castling")]
[Trait("PieceType", "King")]
public class CastlingTests : ChessTestBase
{
    public CastlingTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void CastlingPatterns_MintKing_ExistsWithOutD()
    {
        // Act - Get castling patterns for MintKing with OutD
        var patterns = GetPatternsFor(Piece.MintKing, Sequence.OutD);

        // Assert - Should have castling patterns
        MoveAssertions.HasMinimumRowCount(patterns, 1, "MintKing OutD castling patterns");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void CastlingPatterns_MintRook_ExistsWithOutD()
    {
        // Act - Get castling patterns for MintRook with OutD
        var patterns = GetPatternsFor(Piece.MintRook, Sequence.OutD);

        // Assert - Should have castling patterns
        MoveAssertions.HasMinimumRowCount(patterns, 1, "MintRook OutD castling patterns");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void CastlingPatterns_Variant1And2_CoverBothSides()
    {
        // Act - Get both variants
        var v1Patterns = GetPatternsFor(Piece.MintKing, Sequence.Variant1 | Sequence.OutD);
        var v2Patterns = GetPatternsFor(Piece.MintKing, Sequence.Variant2 | Sequence.OutD);

        // Assert - Both variants exist
        MoveAssertions.HasMinimumRowCount(v1Patterns, 1, "left castling patterns (Variant1)");
        MoveAssertions.HasMinimumRowCount(v2Patterns, 1, "right castling patterns (Variant2)");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void KingMint_WithMintRook_CastlingPatternExists()
    {
        // Arrange - White MintKing at (4, 0), White MintRook at (0, 0)
        // Standard kingside position for castling setup
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 0, WhiteMintRook));

        // Act - Check if castling patterns exist in PatternFactory
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintKing = (int)Piece.MintKing;
        int mintRook = (int)Piece.MintRook;
        
        // Get patterns for both pieces
        var kingCastlePatterns = patternsDf.Filter(
            $"(src_conditions & {mintKing}) != 0 AND (sequence & {outD}) != 0")
            .Count();
        var rookCastlePatterns = patternsDf.Filter(
            $"(src_conditions & {mintRook}) != 0 AND (sequence & {outD}) != 0")
            .Count();

        // Assert - Both king and rook have castling patterns
        Assert.True(kingCastlePatterns > 0, "Expected MintKing to have OutD castling patterns");
        Assert.True(rookCastlePatterns > 0, "Expected MintRook to have OutD castling patterns");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void NonMintKing_NoCastlingPatterns()
    {
        // Act - Get OutD patterns for non-mint king
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintFlag = (int)Piece.Mint;
        int selfKing = (int)(Piece.Self | Piece.King);
        
        // Filter for King patterns that have OutD but DON'T require Mint
        var nonMintKingPatterns = patternsDf.Filter(
            $"(src_conditions & {selfKing}) = {selfKing} AND (sequence & {outD}) != 0 AND (src_conditions & {mintFlag}) = 0");
        var count = nonMintKingPatterns.Count();

        // Assert - Non-mint king should not have castling patterns
        Assert.Equal(0, count);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void CastlingPatterns_RequireAllyKingOrRookDestination()
    {
        // Act - Get InD patterns (completion patterns for castling)
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        int inD = (int)Sequence.InD;
        int allyKing = (int)Piece.AllyKing;
        int allyRook = (int)Piece.AllyRook;
        
        var allyKingDstPatterns = patternsDf.Filter(
            $"(sequence & {inD}) != 0 AND (dst_conditions & {allyKing}) = {allyKing}");
        var allyRookDstPatterns = patternsDf.Filter(
            $"(sequence & {inD}) != 0 AND (dst_conditions & {allyRook}) = {allyRook}");
        
        var kingCount = allyKingDstPatterns.Count();
        var rookCount = allyRookDstPatterns.Count();

        // Assert - InD patterns exist that require AllyKing or AllyRook as destination
        Assert.True(kingCount > 0 || rookCount > 0, 
            "Expected InD patterns to require AllyKing or AllyRook as destination");
    }
}
