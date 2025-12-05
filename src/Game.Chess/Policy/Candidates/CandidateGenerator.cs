using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.Policy.Patterns;
using Game.Chess.Policy.Sequences;
using Game.Chess.HistoryB;

namespace Game.Chess.Policy.Candidates;

/// <summary>
/// CandidateGenerator - unified interface for move generation.
/// 
/// Composes PatternMatcher (atomic moves) and SequenceEngine (sliding moves)
/// into a single interface that returns all candidate moves for a faction.
/// 
/// This is Phase 3 of the architectural refactoring, merging layers 4 and 6
/// (Pattern Matching and Sequence Expansion) into a composite layer 7.
/// </summary>
public static class CandidateGenerator
{
    /// <summary>
    /// Gets all candidate moves (both atomic and sequenced) for the given faction.
    /// 
    /// Algorithm:
    /// 1. Get atomic pattern matches using PatternMatcher
    /// 2. Get sequenced moves using SequenceEngine
    /// 3. Union both result sets
    /// 4. Optional: deduplicate moves
    /// 
    /// Returns DataFrame with columns:
    /// - src_x, src_y: source position
    /// - dst_x, dst_y: destination position
    /// - src_piece, src_generic_piece: moving piece info
    /// - dst_piece, dst_generic_piece: captured piece info (if any)
    /// - sequence: sequence flags (OutX, InX, etc.)
    /// - All other columns from pattern definitions
    /// </summary>
    public static DataFrame GetMoves(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int turn = 0,
        int maxDepth = 7,
        bool deduplicateResults = true)
    {
        // Get atomic moves (non-sequenced patterns)
        var atomicMoves = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn
        );

        // Get sequenced moves (sliding/multi-step patterns)
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn,
            maxDepth: maxDepth
        );

        // Union both result sets
        var allMoves = atomicMoves.Union(sequencedMoves);

        // Optional: Deduplicate by (src_x, src_y, dst_x, dst_y)
        if (deduplicateResults)
        {
            // Drop duplicates keeping first occurrence
            allMoves = allMoves.DropDuplicates("src_x", "src_y", "dst_x", "dst_y");
        }

        return allMoves;
    }

    /// <summary>
    /// Gets candidate moves for a specific piece type.
    /// 
    /// Filters GetMoves results to only moves from pieces matching the given generic type.
    /// 
    /// Useful for specialized scenarios where you want to analyze moves from
    /// a specific piece type (e.g., all rook moves, all pawn moves).
    /// </summary>
    public static DataFrame GetMovesForPieceType(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        ChessPolicy.Piece targetGenericPiece,
        int turn = 0,
        int maxDepth = 7)
    {
        var allMoves = GetMoves(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn,
            maxDepth: maxDepth,
            deduplicateResults: true
        );

        // Filter to only moves from the target piece type
        var pieceBit = (long)targetGenericPiece;
        return allMoves.Filter(
            Col("src_generic_piece").BitwiseAND(Lit(pieceBit)).NotEqual(Lit(0))
        );
    }

    /// <summary>
    /// Gets candidate moves originating from a specific square.
    /// 
    /// Useful for interactive move selection or board visualization
    /// where you want to show all valid moves from a clicked square.
    /// </summary>
    public static DataFrame GetMovesFromSquare(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int sourceX,
        int sourceY,
        int turn = 0,
        int maxDepth = 7)
    {
        var allMoves = GetMoves(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn,
            maxDepth: maxDepth,
            deduplicateResults: true
        );

        // Filter to only moves from the target square
        return allMoves.Filter(
            Col("src_x").EqualTo(Lit(sourceX))
            .And(Col("src_y").EqualTo(Lit(sourceY)))
        );
    }

    /// <summary>
    /// Gets candidate moves targeting a specific square.
    /// 
    /// Useful for threat analysis or move validation where you want to see
    /// all pieces that can move to (reach) a given destination square.
    /// </summary>
    public static DataFrame GetMovesToSquare(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int destX,
        int destY,
        int turn = 0,
        int maxDepth = 7)
    {
        var allMoves = GetMoves(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn,
            maxDepth: maxDepth,
            deduplicateResults: true
        );

        // Filter to only moves targeting the destination square
        return allMoves.Filter(
            Col("dst_x").EqualTo(Lit(destX))
            .And(Col("dst_y").EqualTo(Lit(destY)))
        );
    }
}
