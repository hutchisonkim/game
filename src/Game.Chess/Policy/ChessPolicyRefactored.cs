using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;

namespace Game.Chess.HistoryB;

/// <summary>
/// Refactored chess policy using clean layered architecture.
/// This class serves as a thin facade over the specialized engines.
/// 
/// Architecture layers:
/// 1. Foundation - BoardStateProvider, PatternRepository
/// 2. Perspectives - PerspectiveEngine
/// 3. Simulation - SimulationEngine
/// 4. (Legacy) - TimelineService for full backward compatibility
/// </summary>
public class ChessPolicyRefactored
{
    private readonly SparkSession _spark;
    private readonly BoardStateProvider _boardStateProvider;
    private readonly PatternRepository _patternRepository;
    private readonly PerspectiveEngine _perspectiveEngine;
    private readonly SimulationEngine _simulationEngine;
    private readonly ChessPolicy.TimelineService _timelineService;

    public ChessPolicyRefactored(SparkSession spark)
    {
        _spark = spark;
        _boardStateProvider = new BoardStateProvider(_spark);
        _patternRepository = new PatternRepository(_spark);
        _perspectiveEngine = new PerspectiveEngine();
        _simulationEngine = new SimulationEngine();
        _timelineService = new ChessPolicy.TimelineService();
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
        var threatenedCellsDf = ChessPolicy.TimelineService.ComputeThreatenedCells(
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
    /// Currently delegates to the legacy TimelineService for full backward compatibility.
    /// </summary>
    public DataFrame BuildTimeline(Board board, Piece[] specificFactions, int maxDepth = 3)
    {
        var perspectivesDf = GetPerspectives(board, specificFactions);
        var patternsDf = _patternRepository.GetPatterns();

        return ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, specificFactions, maxDepth);
    }

    /// <summary>
    /// Simulates board state after a move using the new SimulationEngine.
    /// </summary>
    public DataFrame SimulateBoardAfterMove(DataFrame perspectivesDf, DataFrame candidatesDf, Piece[] specificFactions)
    {
        return _simulationEngine.SimulateBoardAfterMove(perspectivesDf, candidatesDf, specificFactions);
    }
}
