using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;
using Game.Chess.Policy.Threats;
using Game.Chess.Policy.Candidates;
using Game.Chess.Policy.Validation;
using Game.Chess.HistoryRefactor;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Policy.Timeline;

/// <summary>
/// TimelineEngine - Thin orchestrator layer combining all 9 architectural layers.
/// 
/// This is Phase 6 of the architectural refactoring - Layer 9 (final).
/// 
/// Algorithm (multi-depth game tree exploration):
/// 1. Initialize: Add threatened bits to starting position (depth 0)
/// 2. For each depth (1 to maxDepth):
///    a. Get candidates for current turn using CandidateGenerator
///    b. Filter legal moves using LegalityEngine
///    c. Simulate board after each legal move
///    d. Compute threats on simulated board
///    e. Union with timeline from previous depth
/// 3. Return complete timeline
/// 
/// This layer is remarkably thin because the heavy lifting is done by:
/// - CandidateGenerator (move generation)
/// - LegalityEngine (move validation)
/// - SimulationEngine (board state transformation)
/// - ThreatEngine (threat computation)
/// - PerspectiveEngine (board context)
/// - PatternRepository (pattern definitions)
/// - BoardStateProvider (initial state)
/// 
/// All we do here is orchestrate the composition!
/// </summary>
public static class TimelineEngine
{
    /// <summary>
    /// Builds a multi-depth timeline of legal moves for the given board position.
    /// 
    /// This is the main entry point for the refactored chess policy.
    /// It composes all 9 architectural layers into a unified move exploration system.
    /// 
    /// Returns a DataFrame containing all perspectives at all depths,
    /// representing all possible board states reachable within maxDepth moves.
    /// </summary>
    public static DataFrame BuildTimeline(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        Piece[] specificFactions,
        int maxDepth = 3,
        bool debug = false)
    {
        // Ensure perspectives carry a stable identifier for unions
        var perspectiveEngine = new PerspectiveEngine();
        var perspectivesWithId = perspectiveEngine.AddPerspectiveId(perspectivesDf);

        // Step 1: Initialize timeline at depth 0 with threatened cells
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesWithId,
            patternsDf,
            specificFactions,
            turn: 0,
            debug: debug
        );

        var perspectivesWithThreats = perspectiveEngine.ApplyThreatMask(
            perspectivesWithId,
            threatenedCellsDf
        );

        // Ensure schema matches the explicit selection we'll use for depth N+
        var timelineColumns = new[] { "x", "y", "piece", "perspective_x", "perspective_y", "perspective_piece", "generic_piece", "perspective_id", "timestep" };
        var perspectivesForTimeline = perspectivesWithThreats
            .WithColumn("timestep", Lit(0))
            .Select(timelineColumns.Select(c => Col(c)).ToArray());

        var timelineDf = perspectivesForTimeline;

        if (debug)
        {
            Console.WriteLine("[TimelineEngine] Initialized timeline at depth 0");
        }

        // Step 2: Iteratively expand timeline for each depth
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            // Current turn is based on the previous depth
            int currentTurn = (depth - 1) % specificFactions.Length;
            
            if (debug)
            {
                Console.WriteLine($"[TimelineEngine] Expanding depth {depth}, turn {currentTurn}");
            }

            // Get perspectives from previous timestep
            var currentPerspectives = timelineDf
                .Filter(Col("timestep") == (depth - 1))
                .Drop("timestep");  // Drop timestep before passing to CandidateGenerator

            // Get candidate moves using CandidateGenerator
            var candidatesDf = CandidateGenerator.GetMoves(
                currentPerspectives,
                patternsDf,
                specificFactions,
                turn: currentTurn,
                maxDepth: maxDepth
            );

            // Filter legal moves using LegalityEngine
            var legalMovesDf = LegalityEngine.FilterMovesLeavingKingInCheck(
                candidatesDf,
                currentPerspectives,
                patternsDf,
                specificFactions,
                turn: currentTurn,
                debug: debug
            );

            // Simulate board after legal moves
            var simulationEngine = new SimulationEngine();
            var nextPerspectivesDf = simulationEngine.SimulateBoardAfterMove(
                currentPerspectives,
                legalMovesDf,
                specificFactions
            );

            // Compute threats for next turn on simulated board
            int nextTurn = depth % specificFactions.Length;
            var nextThreatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
                nextPerspectivesDf,
                patternsDf,
                specificFactions,
                turn: nextTurn,
                debug: debug
            );

            var nextPerspectivesWithThreats = perspectiveEngine.AddPerspectiveId(
                    perspectiveEngine.ApplyThreatMask(
                        nextPerspectivesDf,
                        nextThreatenedCellsDf
                    )
                )
                .WithColumn("timestep", Lit(depth));

            // Ensure schema matches timelineDf before union
            // Both must have exactly the same columns in the same order
            var nextPerspectivesForUnion = nextPerspectivesWithThreats
                .Select(timelineColumns.Select(c => Col(c)).ToArray());

            // Add to timeline
            timelineDf = timelineDf.Union(nextPerspectivesForUnion);

            if (debug)
            {
                Console.WriteLine($"[TimelineEngine] Completed depth {depth}");
            }
        }

        return timelineDf;
    }
}
