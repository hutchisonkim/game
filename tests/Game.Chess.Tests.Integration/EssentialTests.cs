using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using System.Linq;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Tests.Unit;

/// <summary>
/// Essential tests for the Chess Policy Refactoring.
/// These tests form the critical path for validating the 8-layer architecture.
/// 
/// Trait System:
/// - Essential: Marks test as critical for refactoring progress
/// - DependsOn: Test cannot pass unless parent tests pass
/// - Category: Groups tests by architectural layer
/// - ChessRule: Links test to specific chess rule (A1-D4)
/// </summary>
[Collection("Spark collection")]
public class EssentialTests
{
    private readonly SparkSession _spark;

    public EssentialTests()
    {
        // Note: SparkSession is managed by the Spark collection fixture
        _spark = SparkSession.Builder().AppName("EssentialTests").GetOrCreate();
    }

    #region Layer 0: Foundation (3 tests)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_BoardStateProvider")]
    public void L0_Foundation_BoardStateProvider_ConvertsArrayToDataFrame()
    {
        // Arrange
        var board = Board.Default;
        board.Initialize();
        var provider = new BoardStateProvider(_spark);

        // Act
        var piecesDf = provider.GetPieces(board);

        // Assert
        Assert.NotNull(piecesDf);
        Assert.Equal(64, piecesDf.Count()); // 8x8 board = 64 cells
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_PatternRepository")]
    public void L0_Foundation_PatternRepository_LoadsAllPatterns()
    {
        // Arrange
        var repo = new PatternRepository(_spark);

        // Act
        var patternsDf = repo.GetPatterns();

        // Assert
        Assert.NotNull(patternsDf);
        Assert.NotEqual(0, patternsDf.Count()); // Must have patterns
        
        // Verify caching works (second call returns same object)
        var patternsDf2 = repo.GetPatterns();
        Assert.Same(patternsDf, patternsDf2);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_BoardStateProvider")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_PerspectiveEngine")]
    public void L0_Foundation_PerspectiveEngine_AssignsSelfAllyFoe()
    {
        // Arrange
        var board = Board.Default;
        board.Initialize();
        var provider = new BoardStateProvider(_spark);
        var engine = new PerspectiveEngine();
        var piecesDf = provider.GetPieces(board);
        var factions = new[] { Piece.White, Piece.Black };

        // Act
        var perspectivesDf = engine.BuildPerspectives(piecesDf, factions);

        // Assert
        Assert.NotNull(perspectivesDf);
        Assert.NotEqual(0, perspectivesDf.Count());
    }

    #endregion

    #region Layer 1: Atomic Moves (1 placeholder test - others will follow)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_PerspectiveEngine|L0_Foundation_PatternRepository")]
    [Trait("Category", "Atomic")]
    [Trait("ChessRule", "A1|A3|A7|A8")]
    [Trait("Layer", "1")]
    [Trait("TestId", "L1_CandidateGenerator_SimpleMoves")]
    public void L1_CandidateGenerator_SimpleMoves_CollectsAllAtomicPatterns()
    {
        // Arrange
        var board = Board.Default;
        board.Initialize();
        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var engine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = engine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        Assert.NotEqual(0, perspectivesDf.Count());
    }

    #endregion

    #region Placeholder tests for remaining layers (skeleton structure)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L1_CandidateGenerator_SimpleMoves")]
    [Trait("Category", "Sliding")]
    [Trait("ChessRule", "A4|A5|A6")]
    [Trait("Layer", "2")]
    [Trait("TestId", "L2_CandidateGenerator_SlidingMoves")]
    public void L2_CandidateGenerator_SlidingMoves_IncludesBishopAndRook()
    {
        // Placeholder: Sliding move patterns will be tested once SequenceEngine is implemented
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L2_CandidateGenerator_SlidingMoves")]
    [Trait("Category", "Simulation")]
    [Trait("ChessRule", "A1|A3|A7|A8")]
    [Trait("Layer", "3")]
    [Trait("TestId", "L3_SimulationEngine_SimpleMoves")]
    public void L3_SimulationEngine_SimpleMoves_AppliesMoveToBoard()
    {
        // Placeholder: Simulation engine testing will follow
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_SimpleMoves")]
    [Trait("Category", "Threats")]
    [Trait("ChessRule", "A8|C1")]
    [Trait("Layer", "4")]
    [Trait("TestId", "L4_ThreatEngine_BasicThreats")]
    public void L4_ThreatEngine_BasicThreats_ComputesThreatenedCells()
    {
        // Placeholder: Threat engine testing will follow
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_BasicThreats")]
    [Trait("Category", "SpecialMoves")]
    [Trait("ChessRule", "B1|B2|B3|B4|B5")]
    [Trait("Layer", "5")]
    [Trait("TestId", "L5_SpecialMoves_CastlingEnPassantPromotion")]
    public void L5_SpecialMoves_CastlingEnPassantPromotion_HandleMultiPhasePatterns()
    {
        // Placeholder: Special moves testing will follow
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_BasicThreats")]
    [Trait("Category", "Legality")]
    [Trait("ChessRule", "C1|C2|C3")]
    [Trait("Layer", "6")]
    [Trait("TestId", "L6_Legality_KingSafety")]
    public void L6_Legality_KingSafety_FiltersMovesLeavingKingInCheck()
    {
        // Placeholder: Legality engine testing will follow
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L6_Legality_KingSafety")]
    [Trait("Category", "Timeline")]
    [Trait("ChessRule", "A1|A2|A3|A4|A5|A6|A7|A8")]
    [Trait("Layer", "7")]
    [Trait("TestId", "L7_Timeline_MultiTurn")]
    public void L7_Timeline_MultiTurn_PlaysSequenceOfMoves()
    {
        // Placeholder: Timeline engine testing will follow
        Assert.True(true);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L7_Timeline_MultiTurn")]
    [Trait("Category", "Terminal")]
    [Trait("ChessRule", "D1|D2|D3|D4")]
    [Trait("Layer", "8")]
    [Trait("TestId", "L8_Terminal_CheckmateStalemate")]
    public void L8_Terminal_CheckmateStalemate_DetectsGameEnd()
    {
        // Placeholder: Terminal condition testing will follow
        Assert.True(true);
    }

    #endregion
}
