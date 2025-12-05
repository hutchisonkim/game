using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for pattern sequencing functionality.
/// Sequence parameters control multi-step moves like sliding pieces, en passant, castling, etc.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Sequence")]
public class SequenceParameterTests : ChessTestBase
{
    public SequenceParameterTests(SparkFixture fixture) : base(fixture)
    {
    }

    #region Sequence Flag Validation Tests

    [Fact]
    
    [Trait("Essential", "True")]
    public void SequenceMasks_InMaskAndOutMask_CoverAllInAndOutFlags()
    {
        // Assert InMask covers all In* flags
        var inMask = (int)ChessPolicy.Sequence.InMask;
        Assert.True((inMask & (int)ChessPolicy.Sequence.InA) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InB) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InC) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InD) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InE) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InF) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InG) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InH) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InI) != 0);

        // Assert OutMask covers all Out* flags
        var outMask = (int)ChessPolicy.Sequence.OutMask;
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutA) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutB) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutC) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutD) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutE) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutF) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutG) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutH) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutI) != 0);
    }

    [Fact]
    
    [Trait("Essential", "True")]
    public void SequenceConversion_OutShiftedRight_EqualsCorrespondingIn()
    {
        // Verify that Out >> 1 = In for all pairs (validates bit position design)
        Assert.Equal((int)ChessPolicy.Sequence.InA, (int)ChessPolicy.Sequence.OutA >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InB, (int)ChessPolicy.Sequence.OutB >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InC, (int)ChessPolicy.Sequence.OutC >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InD, (int)ChessPolicy.Sequence.OutD >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InE, (int)ChessPolicy.Sequence.OutE >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InF, (int)ChessPolicy.Sequence.OutF >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InG, (int)ChessPolicy.Sequence.OutG >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InH, (int)ChessPolicy.Sequence.OutH >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InI, (int)ChessPolicy.Sequence.OutI >> 1);
    }

    [Fact]
    
    public void ConvertOutFlagsToInFlags_SingleFlag_ConvertsCorrectly()
    {
        // Arrange
        int outI = (int)ChessPolicy.Sequence.OutI;
        int expectedInI = (int)ChessPolicy.Sequence.InI;

        // Act
        int result = ChessPolicy.TimelineService.ConvertOutFlagsToInFlags(outI);

        // Assert
        Assert.Equal(expectedInI, result);
    }

    [Fact]
    
    public void ConvertOutFlagsToInFlags_MultipleFlags_ConvertsAll()
    {
        // Arrange - Multiple Out flags
        int outFlags = (int)(ChessPolicy.Sequence.OutI | ChessPolicy.Sequence.OutF);
        int expectedInFlags = (int)(ChessPolicy.Sequence.InI | ChessPolicy.Sequence.InF);

        // Act
        int result = ChessPolicy.TimelineService.ConvertOutFlagsToInFlags(outFlags);

        // Assert
        Assert.Equal(expectedInFlags, result);
    }

    #endregion

    #region Pattern Structure Tests

    [Fact]
    
    [Trait("PieceType", "Bishop")]
    public void BishopPatterns_HaveCorrectSequenceFlags()
    {
        // Act & Assert - OutF patterns (entry/recursive, not public)
        var outPatterns = GetPatternsFor(Piece.Bishop, Sequence.OutF | Sequence.InstantRecursive);
        MoveAssertions.HasMinimumRowCount(outPatterns, 4, "bishop OutF entry patterns");

        // Act & Assert - InF patterns (final landing, public)
        var inPatterns = GetPatternsFor(Piece.Bishop, Sequence.InF | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(inPatterns, 4, "bishop InF public patterns");
    }

    [Fact]
    
    [Trait("PieceType", "Rook")]
    public void RookPatterns_HaveCorrectSequenceFlags()
    {
        // Act & Assert - OutI patterns (entry/recursive, not public)
        var outPatterns = GetPatternsFor(Piece.Rook, Sequence.OutI | Sequence.InstantRecursive);
        MoveAssertions.HasMinimumRowCount(outPatterns, 4, "rook OutI entry patterns");

        // Act & Assert - InI patterns (final landing, public)
        var inPatterns = GetPatternsFor(Piece.Rook, Sequence.InI | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(inPatterns, 4, "rook InI public patterns");
    }

    [Fact]
    
    [Trait("PieceType", "Queen")]
    public void QueenPatterns_HaveCorrectSequenceFlags()
    {
        // Act & Assert - Queen as bishop (OutG patterns)
        var outGPatterns = GetPatternsFor(Piece.Queen, Sequence.OutG | Sequence.InstantRecursive);
        MoveAssertions.HasMinimumRowCount(outGPatterns, 4, "queen OutG patterns");

        // Act & Assert - Queen as bishop (InG patterns)
        var inGPatterns = GetPatternsFor(Piece.Queen, Sequence.InG | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(inGPatterns, 4, "queen InG patterns");

        // Act & Assert - Queen as rook (OutH patterns)
        var outHPatterns = GetPatternsFor(Piece.Queen, Sequence.OutH | Sequence.InstantRecursive);
        MoveAssertions.HasMinimumRowCount(outHPatterns, 4, "queen OutH patterns");

        // Act & Assert - Queen as rook (InH patterns)
        var inHPatterns = GetPatternsFor(Piece.Queen, Sequence.InH | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(inHPatterns, 4, "queen InH patterns");
    }

    [Fact]
    
    [Trait("PieceType", "Pawn")]
    public void PawnPatterns_HaveCorrectSequenceFlags()
    {
        // Act & Assert - Pawn forward move (OutA, public)
        var outAPatterns = GetPatternsFor(Piece.Pawn, Sequence.OutA | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(outAPatterns, 1, "pawn OutA forward patterns");

        // Act & Assert - Pawn captures (OutB, public, left/right diagonal)
        var outBPatterns = GetPatternsFor(Piece.Pawn, Sequence.OutB | Sequence.Public);
        MoveAssertions.HasMinimumRowCount(outBPatterns, 2, "pawn OutB capture patterns");
    }

    [Fact]
    
    [Trait("PieceType", "Rook")]
    public void RookPatterns_EntryPatterns_HaveOutIAndInstantRecursive()
    {
        // Act - Get entry patterns for Rook
        var entryPatterns = GetEntryPatterns(Piece.Rook, Sequence.OutI);

        // Assert - 4 entry patterns (one per orthogonal direction)
        MoveAssertions.HasMinimumRowCount(entryPatterns, 4, "rook entry patterns");
    }

    [Fact]
    
    [Trait("PieceType", "Rook")]
    public void RookPatterns_ContinuationPatterns_HaveInIAndPublic()
    {
        // Act - Get continuation patterns for Rook
        var continuationPatterns = GetContinuationPatterns(Piece.Rook, Sequence.InI);

        // Assert - 8 continuation patterns (4 directions × 2 target types: Empty + Foe)
        MoveAssertions.HasMinimumRowCount(continuationPatterns, 8, "rook continuation patterns");
    }

    #endregion

    #region Sequence Activation Tests

    [Fact]
    
    [Trait("PieceType", "Bishop")]
    public void BishopWithNoActiveSequence_ComputeNextCandidates_ReturnsPublicPatterns()
    {
        // Arrange - Bishop in center of empty board
        var board = CreateEmptyBoardWithPiece(4, 4, WhiteMintBishop);
        
        // Act - No active sequence (backward compatible behavior)
        var moves = GetMovesFor(board, Piece.Bishop);

        // Assert - All Public patterns execute (including InF patterns for backward compatibility)
        MoveAssertions.HasMinimumMoveCount(moves, 4);
    }

    [Fact]
    
    [Trait("PieceType", "Bishop")]
    public void BishopWithActiveOutF_ComputeNextCandidates_EnablesInFPatterns()
    {
        // Arrange - Bishop in center, filter to InF patterns only
        var board = CreateEmptyBoardWithPiece(4, 4, WhiteMintBishop);
        
        // Act - Active OutF sequence enables InF patterns
        var moves = GetMovesWithActiveSequence(board, Piece.Bishop, Sequence.InF, Sequence.OutF);

        // Assert - InF patterns are now enabled
        MoveAssertions.HasMinimumMoveCount(moves, 4);
    }

    [Fact]
    
    [Trait("PieceType", "Bishop")]
    public void BishopWithWrongActiveSequence_ComputeNextCandidates_DoesNotEnableInFPatterns()
    {
        // Arrange - Bishop in center, filter to InF patterns only
        var board = CreateEmptyBoardWithPiece(4, 4, WhiteMintBishop);
        
        // Act - Wrong active sequence (OutI instead of OutF)
        var moves = GetMovesWithActiveSequence(board, Piece.Bishop, Sequence.InF, Sequence.OutI);

        // Assert - InF patterns should NOT be enabled with wrong sequence
        Assert.Empty(moves);
    }

    [Fact]
    
    [Trait("PieceType", "Rook")]
    public void RookWithActiveOutI_ComputeNextCandidates_EnablesInIPatterns()
    {
        // Arrange - Rook in center, filter to InI patterns only
        var board = CreateEmptyBoardWithPiece(4, 4, WhiteMintRook);
        
        // Act - Active OutI sequence enables InI patterns
        var moves = GetMovesWithActiveSequence(board, Piece.Rook, Sequence.InI, Sequence.OutI);

        // Assert - InI patterns are enabled
        MoveAssertions.HasMinimumMoveCount(moves, 4);
    }

    [Fact]
    
    public void ComputeNextCandidates_ResultDataFrame_PreservesSequenceColumn()
    {
        // Arrange - Rook in center
        var board = CreateEmptyBoardWithPiece(4, 4, WhiteMintRook);
        
        // Act
        var moves = GetMovesFor(board, Piece.Rook, Sequence.Public);

        // Assert - Moves should exist with non-zero sequence values
        Assert.True(moves.Length > 0, "Expected at least one move");
        MoveAssertions.AllMovesHaveNonZeroSequence(moves);
    }

    [Fact]
    
    [Trait("PieceType", "Bishop")]
    public void BishopPatterns_VariantFiltering_EachVariantHasPatterns()
    {
        // Act & Assert - Each diagonal direction should have a different variant
        var v1Patterns = GetPatternsFor(Piece.Bishop, Sequence.Variant1);
        MoveAssertions.HasMinimumRowCount(v1Patterns, 2, "Variant1 patterns");

        var v2Patterns = GetPatternsFor(Piece.Bishop, Sequence.Variant2);
        MoveAssertions.HasMinimumRowCount(v2Patterns, 2, "Variant2 patterns");

        var v3Patterns = GetPatternsFor(Piece.Bishop, Sequence.Variant3);
        MoveAssertions.HasMinimumRowCount(v3Patterns, 2, "Variant3 patterns");

        var v4Patterns = GetPatternsFor(Piece.Bishop, Sequence.Variant4);
        MoveAssertions.HasMinimumRowCount(v4Patterns, 2, "Variant4 patterns");
    }

    #endregion
}
