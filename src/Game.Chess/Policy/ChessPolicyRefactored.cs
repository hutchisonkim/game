using Microsoft.Spark.Sql;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;
using Game.Chess.Policy.Validation;
using Game.Chess.Policy.Timeline;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;
using Game.Chess.Policy.Threats;

namespace Game.Chess.HistoryRefactor;

/// <summary>
/// Refactored chess policy using clean layered architecture.
/// This class serves as a thin facade over the specialized engines.
/// 
/// Architecture layers (9 total):
/// 1. Foundation - BoardStateProvider, PatternRepository
/// 2. Perspectives - PerspectiveEngine
/// 3. Simulation - SimulationEngine
/// 4. Patterns - PatternMatcher (Phase 2A)
/// 5. Threats - ThreatEngine (Phase 2B)
/// 6. Sequences - SequenceEngine (Phase 2C)
/// 7. Candidates - CandidateGenerator (Phase 3)
/// 8. Validation - LegalityEngine (Phase 5)
/// 9. Timeline - TimelineEngine (Phase 6, orchestrator)
/// </summary>
public class ChessPolicyRefactored
{
    private readonly SparkSession _spark;
    private readonly BoardStateProvider _boardStateProvider;
    private readonly PatternRepository _patternRepository;
    private readonly PerspectiveEngine _perspectiveEngine;
    private readonly SimulationEngine _simulationEngine;

    public ChessPolicyRefactored(SparkSession spark)
    {
        _spark = spark;
        _boardStateProvider = new BoardStateProvider(_spark);
        _patternRepository = new PatternRepository(_spark);
        _perspectiveEngine = new PerspectiveEngine();
        _simulationEngine = new SimulationEngine();
    }

    /// <summary>
    /// Generates initial perspectives for the given board and factions
    /// </summary>
    public DataFrame GetPerspectives(Board board, Piece[] specificFactions)
    {
        var piecesDf = _boardStateProvider.GetPieces(board);
        var perspectivesDf = _perspectiveEngine.BuildPerspectives(piecesDf, specificFactions);
        return _perspectiveEngine.AddPerspectiveId(perspectivesDf);
    }

    /// <summary>
    /// Generates perspectives for the given board with the Threatened bit set on cells
    /// that are under attack by the opponent.
    /// </summary>
    public DataFrame GetPerspectivesWithThreats(Board board, Piece[] specificFactions, int turn = 0)
    {
        var piecesDf = _boardStateProvider.GetPieces(board);
        var patternsDf = _patternRepository.GetPatterns();

        var perspectivesDf = _perspectiveEngine.BuildPerspectives(piecesDf, specificFactions);
        perspectivesDf = _perspectiveEngine.AddPerspectiveId(perspectivesDf);

        // Compute threatened cells from opponent's perspective
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn
        );

        // Add Threatened bit to the perspectives
        return _perspectiveEngine.ApplyThreatMask(perspectivesDf, threatenedCellsDf);
    }

    /// <summary>
    /// Builds the timeline of moves for the board up to maxDepth.
    /// Uses the refactored TimelineEngine with full CandidateGenerator integration.
    /// </summary>
    public DataFrame BuildTimeline(Board board, Piece[] specificFactions, int maxDepth = 3)
    {
        var perspectivesDf = GetPerspectives(board, specificFactions);
        var patternsDf = _patternRepository.GetPatterns();

        return TimelineEngine.BuildTimeline(perspectivesDf, patternsDf, specificFactions, maxDepth);
    }

    /// <summary>
    /// Simulates board state after a move using the new SimulationEngine.
    /// </summary>
    public DataFrame SimulateBoardAfterMove(DataFrame perspectivesDf, DataFrame candidatesDf, Piece[] specificFactions)
    {
        return _simulationEngine.SimulateBoardAfterMove(perspectivesDf, candidatesDf, specificFactions);
    }

    /// <summary>
    /// Filters candidate moves to only those that leave the king safe.
    /// This is the legality validation layer - Phase 5 of the refactoring.
    /// 
    /// Handles:
    /// - King safety: can't move to threatened squares
    /// - Pin detection: can't move a pinned piece away from the pinning line
    /// - Discovered checks: can't move a piece that blocks an attack on the king
    /// </summary>
    public DataFrame FilterLegalMoves(
        DataFrame candidatesDf,
        DataFrame perspectivesDf,
        Piece[] specificFactions,
        int turn = 0)
    {
        var patternsDf = _patternRepository.GetPatterns();
        return LegalityEngine.FilterMovesLeavingKingInCheck(
            candidatesDf,
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn
        );
    }
}
