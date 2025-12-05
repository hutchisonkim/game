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
    
    public void CastlingPatterns_MintKing_ExistsWithOutD()
    {
        // Act - Get castling patterns for MintKing with OutD
        var patterns = GetPatternsFor(Piece.MintKing, Sequence.OutD);

        // Assert - Should have castling patterns
        MoveAssertions.HasMinimumRowCount(patterns, 1, "MintKing OutD castling patterns");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void CastlingPatterns_MintKing_ExistsWithOutD_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get castling patterns for MintKing with OutD from refactored policy
        var patternFactory = new PatternFactory(Spark);
        var patternsDf = patternFactory.GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintKing = (int)Piece.MintKing;
        var patterns = patternsDf
            .Filter($"(src_conditions & {mintKing}) != 0 AND (sequence & {outD}) != 0")
            .Collect()
            .ToArray();

        // Assert - Should have castling patterns
        Assert.True(patterns.Length >= 1, $"Expected at least 1 MintKing OutD castling pattern, got {patterns.Length}");
    }

    [Fact]
    
    public void CastlingPatterns_MintRook_ExistsWithOutD()
    {
        // Act - Get castling patterns for MintRook with OutD
        var patterns = GetPatternsFor(Piece.MintRook, Sequence.OutD);

        // Assert - Should have castling patterns
        MoveAssertions.HasMinimumRowCount(patterns, 1, "MintRook OutD castling patterns");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void CastlingPatterns_MintRook_ExistsWithOutD_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get castling patterns for MintRook with OutD from refactored policy
        var patternFactory = new PatternFactory(Spark);
        var patternsDf = patternFactory.GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintRook = (int)Piece.MintRook;
        var patterns = patternsDf
            .Filter($"(src_conditions & {mintRook}) != 0 AND (sequence & {outD}) != 0")
            .Collect()
            .ToArray();

        // Assert - Should have castling patterns
        Assert.True(patterns.Length >= 1, $"Expected at least 1 MintRook OutD castling pattern, got {patterns.Length}");
    }

    [Fact]
    
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
    
    
    [Trait("Refactored", "True")]
    public void CastlingPatterns_Variant1And2_CoverBothSides_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get castling patterns from refactored policy
        var patternFactory = new PatternFactory(Spark);
        var patternsDf = patternFactory.GetPatterns();
        int outD = (int)Sequence.OutD;
        int variant1 = (int)Sequence.Variant1;
        int variant2 = (int)Sequence.Variant2;
        int mintKing = (int)Piece.MintKing;
        
        var v1Patterns = patternsDf
            .Filter($"(src_conditions & {mintKing}) != 0 AND (sequence & {variant1}) != 0 AND (sequence & {outD}) != 0")
            .Collect()
            .ToArray();
        var v2Patterns = patternsDf
            .Filter($"(src_conditions & {mintKing}) != 0 AND (sequence & {variant2}) != 0 AND (sequence & {outD}) != 0")
            .Collect()
            .ToArray();

        // Assert - Both variants exist
        Assert.True(v1Patterns.Length >= 1, $"Expected at least 1 left castling pattern (Variant1), got {v1Patterns.Length}");
        Assert.True(v2Patterns.Length >= 1, $"Expected at least 1 right castling pattern (Variant2), got {v2Patterns.Length}");
    }

    [Fact]
    
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
    
    
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void KingMint_WithMintRook_CastlingPatternExists_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

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
    
    
    [Trait("Refactored", "True")]
    public void NonMintKing_NoCastlingPatterns_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get OutD patterns for non-mint king using refactored policy
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

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void CastlingPatterns_RequireAllyKingOrRookDestination_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);

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

    [Fact]
    
    public void CastlingPatterns_UseEmptyAndSafe_ForKingMovement()
    {
        // Arrange - EmptyAndSafe = Empty | ~Threatened
        // King castling patterns should use EmptyAndSafe as destination condition
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintKing = (int)Piece.MintKing;
        int emptyAndSafe = (int)Piece.EmptyAndSafe;
        
        // Act - Get king castling patterns that require EmptyAndSafe destination
        var kingEmptyAndSafePatterns = patternsDf.Filter(
            $"(src_conditions & {mintKing}) != 0 AND (sequence & {outD}) != 0 AND (dst_conditions & {emptyAndSafe}) = {emptyAndSafe}");
        
        var count = kingEmptyAndSafePatterns.Count();

        // Assert - King castling patterns should require EmptyAndSafe destination
        Assert.True(count > 0, 
            "Expected MintKing OutD castling patterns to require EmptyAndSafe as destination condition");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void CastlingPatterns_UseEmptyAndSafe_ForKingMovement_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);
        
        // EmptyAndSafe = Empty | ~Threatened
        // King castling patterns should use EmptyAndSafe as destination condition
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        int outD = (int)Sequence.OutD;
        int mintKing = (int)Piece.MintKing;
        int emptyAndSafe = (int)Piece.EmptyAndSafe;
        
        // Act - Get king castling patterns that require EmptyAndSafe destination
        var kingEmptyAndSafePatterns = patternsDf.Filter(
            $"(src_conditions & {mintKing}) != 0 AND (sequence & {outD}) != 0 AND (dst_conditions & {emptyAndSafe}) = {emptyAndSafe}");
        
        var count = kingEmptyAndSafePatterns.Count();

        // Assert - King castling patterns should require EmptyAndSafe destination
        Assert.True(count > 0, 
            "Expected MintKing OutD castling patterns to require EmptyAndSafe as destination condition");
    }

    [Fact]
    
    public void ThreatenedBit_IntegratedInBuildTimeline()
    {
        // Arrange - Setup board with a rook threatening a cell
        // White MintKing at (4, 0), White MintRook at (0, 0), Black Rook at (3, 7)
        // Black Rook threatens the entire file 3 (x=3), including the castling path
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 0, WhiteMintRook),
            (3, 7, BlackMintRook));

        // Act - Build timeline and check if threatened cells are marked
        var perspectivesDf = Policy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);
        
        // Verify that threatened bit is set on cell (3, 0) - threatened by Black Rook
        int threatenedBit = (int)Piece.Threatened;
        var threatenedCells = perspectivesDf
            .Filter(Functions.Col("x").EqualTo(3).And(Functions.Col("y").EqualTo(0)))
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Cell (3, 0) should be marked as threatened
        Assert.True(threatenedCells > 0, 
            "Expected cell (3, 0) to be marked as Threatened by Black Rook at (3, 7)");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void ThreatenedBit_IntegratedInBuildTimeline_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);
        
        // Setup board with a rook threatening a cell
        // White MintKing at (4, 0), White MintRook at (0, 0), Black Rook at (3, 7)
        // Black Rook threatens the entire file 3 (x=3), including the castling path
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 0, WhiteMintRook),
            (3, 7, BlackMintRook));

        // Act - Build timeline and check if threatened cells are marked
        var perspectivesDf = refactoredPolicy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);
        
        // Verify that threatened bit is set on cell (3, 0) - threatened by Black Rook
        int threatenedBit = (int)Piece.Threatened;
        var threatenedCells = perspectivesDf
            .Filter(Functions.Col("x").EqualTo(3).And(Functions.Col("y").EqualTo(0)))
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Cell (3, 0) should be marked as threatened
        Assert.True(threatenedCells > 0, 
            "Expected cell (3, 0) to be marked as Threatened by Black Rook at (3, 7)");
    }

    [Fact]
    
    public void CastlingPath_NotBlockedWhenNotThreatened()
    {
        // Arrange - Setup board for castling with clear path
        // White MintKing at (4, 0), White MintRook at (0, 0)
        // No enemy pieces threatening the castling path
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 0, WhiteMintRook));

        // Act - Check if castling candidates exist
        var perspectivesDf = Policy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        
        // Get OutD candidates for MintKing (castling initiation)
        int mintKing = (int)Piece.MintKing;
        int outD = (int)Sequence.OutD;
        
        var candidatesDf = TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            DefaultFactions, 
            turn: 0);
        
        // Filter for MintKing OutD patterns
        var castlingCandidates = candidatesDf
            .Filter(Functions.Col("src_generic_piece").BitwiseAND(Functions.Lit(mintKing)).NotEqual(Functions.Lit(0)))
            .Filter(Functions.Col("sequence").BitwiseAND(Functions.Lit(outD)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Castling should be possible
        Assert.True(castlingCandidates > 0, 
            "Expected castling to be possible when path is not threatened");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void CastlingPath_NotBlockedWhenNotThreatened_Refactored()
    {
        // Arrange - Use refactored policy
        var refactoredPolicy = new ChessPolicyRefactored(Spark);
        
        // Setup board for castling with clear path
        // White MintKing at (4, 0), White MintRook at (0, 0)
        // No enemy pieces threatening the castling path
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 0, WhiteMintRook));

        // Act - Check if castling candidates exist
        var perspectivesDf = refactoredPolicy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        
        // Get OutD candidates for MintKing (castling initiation)
        int mintKing = (int)Piece.MintKing;
        int outD = (int)Sequence.OutD;
        
        var candidatesDf = TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            DefaultFactions, 
            turn: 0);
        
        // Filter for MintKing OutD patterns
        var castlingCandidates = candidatesDf
            .Filter(Functions.Col("src_generic_piece").BitwiseAND(Functions.Lit(mintKing)).NotEqual(Functions.Lit(0)))
            .Filter(Functions.Col("sequence").BitwiseAND(Functions.Lit(outD)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Castling should be possible
        Assert.True(castlingCandidates > 0, 
            "Expected castling to be possible when path is not threatened");
    }
}
