using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for pattern sequencing functionality.
/// Sequence parameters control multi-step moves like sliding pieces, en passant, castling, etc.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Sequence")]
public class SequenceParameterTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public SequenceParameterTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    #region Sequence Flag Validation Tests

    [Fact]
    [Trait("Performance", "Fast")]
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
    [Trait("Performance", "Fast")]
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
    [Trait("Performance", "Fast")]
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
    [Trait("Performance", "Fast")]
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
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Bishop")]
    public void BishopPatterns_HaveCorrectSequenceFlags()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int outF = (int)ChessPolicy.Sequence.OutF;
        int inF = (int)ChessPolicy.Sequence.InF;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act & Assert - OutF patterns (entry/recursive, not public)
        string outFilter = $"(src_conditions & {bishop}) != 0 AND (sequence & {outF}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outCount = patternsDf.Filter(outFilter).Count();
        Assert.True(outCount >= 4, $"Expected at least 4 bishop OutF patterns, got {outCount}");

        // Act & Assert - InF patterns (final landing, public)
        string inFilter = $"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0 AND (sequence & {publicSeq}) != 0";
        long inCount = patternsDf.Filter(inFilter).Count();
        Assert.True(inCount >= 4, $"Expected at least 4 bishop InF public patterns, got {inCount}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Rook")]
    public void RookPatterns_HaveCorrectSequenceFlags()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int rook = (int)ChessPolicy.Piece.Rook;
        int outI = (int)ChessPolicy.Sequence.OutI;
        int inI = (int)ChessPolicy.Sequence.InI;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act & Assert - OutI patterns (entry/recursive, not public)
        string outFilter = $"(src_conditions & {rook}) != 0 AND (sequence & {outI}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outCount = patternsDf.Filter(outFilter).Count();
        Assert.True(outCount >= 4, $"Expected at least 4 rook OutI patterns, got {outCount}");

        // Act & Assert - InI patterns (final landing, public)
        string inFilter = $"(src_conditions & {rook}) != 0 AND (sequence & {inI}) != 0 AND (sequence & {publicSeq}) != 0";
        long inCount = patternsDf.Filter(inFilter).Count();
        Assert.True(inCount >= 4, $"Expected at least 4 rook InI public patterns, got {inCount}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Queen")]
    public void QueenPatterns_HaveCorrectSequenceFlags()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int queen = (int)ChessPolicy.Piece.Queen;
        int outG = (int)ChessPolicy.Sequence.OutG;
        int outH = (int)ChessPolicy.Sequence.OutH;
        int inG = (int)ChessPolicy.Sequence.InG;
        int inH = (int)ChessPolicy.Sequence.InH;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act & Assert - Queen as bishop (OutG patterns)
        string outGFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {outG}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outGCount = patternsDf.Filter(outGFilter).Count();
        Assert.True(outGCount >= 4, $"Expected at least 4 queen OutG patterns, got {outGCount}");

        // Act & Assert - Queen as bishop (InG patterns)
        string inGFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {inG}) != 0 AND (sequence & {publicSeq}) != 0";
        long inGCount = patternsDf.Filter(inGFilter).Count();
        Assert.True(inGCount >= 4, $"Expected at least 4 queen InG patterns, got {inGCount}");

        // Act & Assert - Queen as rook (OutH patterns)
        string outHFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {outH}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outHCount = patternsDf.Filter(outHFilter).Count();
        Assert.True(outHCount >= 4, $"Expected at least 4 queen OutH patterns, got {outHCount}");

        // Act & Assert - Queen as rook (InH patterns)
        string inHFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {inH}) != 0 AND (sequence & {publicSeq}) != 0";
        long inHCount = patternsDf.Filter(inHFilter).Count();
        Assert.True(inHCount >= 4, $"Expected at least 4 queen InH patterns, got {inHCount}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Pawn")]
    public void PawnPatterns_HaveCorrectSequenceFlags()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int pawn = (int)ChessPolicy.Piece.Pawn;
        int outA = (int)ChessPolicy.Sequence.OutA;
        int outB = (int)ChessPolicy.Sequence.OutB;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Act & Assert - Pawn forward move (OutA, public)
        string outAFilter = $"(src_conditions & {pawn}) != 0 AND (sequence & {outA}) != 0 AND (sequence & {publicSeq}) != 0";
        long outACount = patternsDf.Filter(outAFilter).Count();
        Assert.True(outACount >= 1, $"Expected at least 1 pawn OutA pattern, got {outACount}");

        // Act & Assert - Pawn captures (OutB, public, left/right diagonal)
        string outBFilter = $"(src_conditions & {pawn}) != 0 AND (sequence & {outB}) != 0 AND (sequence & {publicSeq}) != 0";
        long outBCount = patternsDf.Filter(outBFilter).Count();
        Assert.True(outBCount >= 2, $"Expected at least 2 pawn OutB patterns (left/right diagonal), got {outBCount}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Rook")]
    public void RookPatterns_EntryPatterns_HaveOutIAndInstantRecursive()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int rook = (int)ChessPolicy.Piece.Rook;
        int outI = (int)ChessPolicy.Sequence.OutI;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int inMask = (int)ChessPolicy.Sequence.InMask;

        // Act - Filter to entry patterns (OutI, InstantRecursive, no In flags)
        var entryPatterns = patternsDf.Filter(
            $"(src_conditions & {rook}) != 0 " +
            $"AND (sequence & {outI}) != 0 " +
            $"AND (sequence & {instantRecursive}) = {instantRecursive} " +
            $"AND (sequence & {inMask}) = 0");

        // Assert - 4 entry patterns (one per orthogonal direction)
        var count = entryPatterns.Count();
        Assert.True(count >= 4, $"Expected at least 4 rook entry patterns, got {count}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Rook")]
    public void RookPatterns_ContinuationPatterns_HaveInIAndPublic()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int rook = (int)ChessPolicy.Piece.Rook;
        int inI = (int)ChessPolicy.Sequence.InI;
        int publicFlag = (int)ChessPolicy.Sequence.Public;

        // Act - Filter to continuation patterns (InI, Public)
        var continuationPatterns = patternsDf.Filter(
            $"(src_conditions & {rook}) != 0 " +
            $"AND (sequence & {inI}) != 0 " +
            $"AND (sequence & {publicFlag}) != 0");

        // Assert - 8 continuation patterns (4 directions × 2 target types: Empty + Foe)
        var count = continuationPatterns.Count();
        Assert.True(count >= 8, $"Expected at least 8 rook continuation patterns, got {count}");
    }

    #endregion

    #region Sequence Activation Tests

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Bishop")]
    public void BishopWithNoActiveSequence_ComputeNextCandidates_ReturnsPublicPatterns()
    {
        // Arrange - Bishop in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);
        
        var perspectivesDf = _policy.GetPerspectives(board, MoveHelpers.DefaultFactions);
        int bishop = (int)ChessPolicy.Piece.Bishop;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0");

        // Act - No active sequence (backward compatible behavior)
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, MoveHelpers.DefaultFactions,
            turn: 0, activeSequences: ChessPolicy.Sequence.None);

        // Assert - All Public patterns execute (including InF patterns for backward compatibility)
        var moves = candidates.Collect();
        Assert.True(moves.Count() >= 4, $"Expected at least 4 bishop moves, got {moves.Count()}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Bishop")]
    public void BishopWithActiveOutF_ComputeNextCandidates_EnablesInFPatterns()
    {
        // Arrange - Bishop in center, filter to InF patterns only
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);
        
        var perspectivesDf = _policy.GetPerspectives(board, MoveHelpers.DefaultFactions);
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int inF = (int)ChessPolicy.Sequence.InF;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0");

        // Act - Active OutF sequence enables InF patterns
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, MoveHelpers.DefaultFactions,
            turn: 0, activeSequences: ChessPolicy.Sequence.OutF);

        // Assert - InF patterns are now enabled
        var moves = candidates.Collect();
        Assert.True(moves.Count() >= 4, $"Expected at least 4 bishop InF moves with active OutF, got {moves.Count()}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Bishop")]
    public void BishopWithWrongActiveSequence_ComputeNextCandidates_DoesNotEnableInFPatterns()
    {
        // Arrange - Bishop in center, filter to InF patterns only
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);
        
        var perspectivesDf = _policy.GetPerspectives(board, MoveHelpers.DefaultFactions);
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int inF = (int)ChessPolicy.Sequence.InF;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0");

        // Act - Wrong active sequence (OutI instead of OutF)
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, MoveHelpers.DefaultFactions,
            turn: 0, activeSequences: ChessPolicy.Sequence.OutI);

        // Assert - InF patterns should NOT be enabled with wrong sequence
        var moves = candidates.Collect();
        Assert.Empty(moves);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Rook")]
    public void RookWithActiveOutI_ComputeNextCandidates_EnablesInIPatterns()
    {
        // Arrange - Rook in center, filter to InI patterns only
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);
        
        var perspectivesDf = _policy.GetPerspectives(board, MoveHelpers.DefaultFactions);
        int rook = (int)ChessPolicy.Piece.Rook;
        int inI = (int)ChessPolicy.Sequence.InI;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0 AND (sequence & {inI}) != 0");

        // Act - Active OutI sequence enables InI patterns
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, MoveHelpers.DefaultFactions,
            turn: 0, activeSequences: ChessPolicy.Sequence.OutI);

        // Assert - InI patterns are enabled
        var moves = candidates.Collect();
        Assert.True(moves.Count() >= 4, $"Expected at least 4 rook InI moves with active OutI, got {moves.Count()}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ComputeNextCandidates_ResultDataFrame_PreservesSequenceColumn()
    {
        // Arrange - Rook in center
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);
        
        var perspectivesDf = _policy.GetPerspectives(board, MoveHelpers.DefaultFactions);
        int rook = (int)ChessPolicy.Piece.Rook;
        int publicSeq = (int)ChessPolicy.Sequence.Public;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0 AND (sequence & {publicSeq}) != 0");

        // Act
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, MoveHelpers.DefaultFactions);

        // Assert - Sequence column should be present and have non-zero values
        Assert.Contains("sequence", candidates.Columns());
        
        var moves = candidates.Collect();
        Assert.True(moves.Count() > 0, "Expected at least one move");
        
        var sequenceValues = moves.Select(r => r.GetAs<int>("sequence")).ToList();
        Assert.All(sequenceValues, seq => Assert.True(seq != 0, "Sequence should have flags set"));
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("PieceType", "Bishop")]
    public void BishopPatterns_VariantFiltering_EachVariantHasPatterns()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int variant1 = (int)ChessPolicy.Sequence.Variant1;
        int variant2 = (int)ChessPolicy.Sequence.Variant2;
        int variant3 = (int)ChessPolicy.Sequence.Variant3;
        int variant4 = (int)ChessPolicy.Sequence.Variant4;

        // Act & Assert - Each diagonal direction should have a different variant
        string v1Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant1}) != 0";
        string v2Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant2}) != 0";
        string v3Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant3}) != 0";
        string v4Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant4}) != 0";

        // Each variant should have at least 2 patterns (OutF + InF for that direction)
        Assert.True(patternsDf.Filter(v1Filter).Count() >= 2, "Expected Variant1 patterns");
        Assert.True(patternsDf.Filter(v2Filter).Count() >= 2, "Expected Variant2 patterns");
        Assert.True(patternsDf.Filter(v3Filter).Count() >= 2, "Expected Variant3 patterns");
        Assert.True(patternsDf.Filter(v4Filter).Count() >= 2, "Expected Variant4 patterns");
    }

    #endregion
}
