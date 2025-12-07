using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.HistoryB;

namespace Game.Chess.Policy.Patterns;

/// <summary>
/// Atomic pattern matching engine. Matches pieces against patterns without sequencing.
/// 
/// This layer encapsulates the core pattern matching algorithm:
/// 1. Filter actors at their perspective positions
/// 2. Cross-join with patterns
/// 3. Apply source and destination conditions
/// 4. Compute destination coordinates
/// 5. Filter out-of-bounds moves
/// 6. Return all matching patterns
/// 
/// Public methods:
/// - MatchAtomicPatterns: Standard pattern matching (filters by Public flag)
/// - MatchPatternsWithSequence: Pattern matching with sequence filtering (for continuations)
/// 
/// Design:
/// - Stateless, pure functions
/// - No side effects
/// - Each step is clearly delineated with comments
/// - Reusable for threat computation and move generation
/// </summary>
public static class PatternMatcher
{
    /// <summary>
    /// Matches pieces against patterns, returning all atomic moves.
    /// This is the standard pattern matching without sequencing support.
    /// 
    /// Parameters:
    /// - perspectivesDf: Full perspectives dataframe (actor piece × board state)
    /// - patternsDf: Pattern definitions
    /// - specificFactions: List of factions to determine turn order and sign alternation
    /// - turn: Current turn (modulo factions.length to get current faction)
    /// - debug: Enable debug output
    /// 
    /// Returns:
    /// DataFrame with columns:
    /// - src_x, src_y: Source coordinates
    /// - src_piece, src_generic_piece: Source piece and generic flags
    /// - dst_x, dst_y: Destination coordinates
    /// - dst_piece, dst_generic_piece: Destination piece and generic flags
    /// - plus all pattern columns (pattern_id, name, etc.)
    /// </summary>
    public static DataFrame MatchAtomicPatterns(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int turn = 0,
        bool debug = false)
    {
        return MatchPatternsInternal(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn,
            activeSequences: ChessPolicy.Sequence.None,
            debug
        );
    }

    /// <summary>
    /// Matches pieces against patterns with sequence filtering.
    /// Used for continuing sliding moves where certain sequence flags must be active.
    /// 
    /// When activeSequences has Out* flags set, only patterns with matching In* flags can execute.
    /// This enables multi-step sliding sequences (e.g., Rook moving multiple squares).
    /// 
    /// The bit relationship: OutX >> 1 = InX for all In/Out pairs.
    /// </summary>
    public static DataFrame MatchPatternsWithSequence(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int turn = 0,
        ChessPolicy.Sequence activeSequences = ChessPolicy.Sequence.None,
        bool debug = false)
    {
        return MatchPatternsInternal(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn,
            activeSequences,
            debug
        );
    }

    // =============== INTERNAL IMPLEMENTATION ===============

    /// <summary>
    /// Internal implementation of pattern matching.
    /// All logic is contained here for clarity and testability.
    /// </summary>
    private static DataFrame MatchPatternsInternal(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int turn,
        ChessPolicy.Sequence activeSequences,
        bool debug)
    {
        // Cache perspectives early since we use it multiple times
        perspectivesDf.Cache();

        // ===== STEP 0: Deduplicate patterns =====
        var uniquePatternsDf = patternsDf.DropDuplicates();

        // ===== STEP 1: Build ACTOR perspectives =====
        // Filter to pieces at their own perspective position (where x == perspective_x AND y == perspective_y)
        // AND must have a faction bit (not empty).
        var actorPerspectives = perspectivesDf
            .Filter(
                Col("x").EqualTo(Col("perspective_x")).And(
                Col("y").EqualTo(Col("perspective_y"))).And(
                    Col("piece") != Lit((int)ChessPolicy.Piece.Empty)
                )
            );


        // Apply turn-based faction filter
        var turnFaction = specificFactions[turn % specificFactions.Length];
        actorPerspectives = actorPerspectives
            .Filter(
                Col("piece").BitwiseAND(Lit((int)turnFaction)).NotEqual(Lit(0))
            );

        // Additional filter pushdown for sequence filtering - filter patterns before cross-join
        var filteredPatternsDf = uniquePatternsDf;
        if ((int)activeSequences != 0)
        {
            // If activeSequences are specified, filter patterns early
            var inMask = (int)ChessPolicy.Sequence.InMask;
            var patternInFlags = Col("sequence").BitwiseAND(Lit(inMask));
            var hasNoInRequirements = patternInFlags.EqualTo(Lit(0));

            var activeSeqInt = (int)activeSequences;
            var activeInFlagsFromOut = (activeSeqInt >> 1) & inMask;
            var activeInFlagsDirect = activeSeqInt & inMask;
            var activeInFlags = activeInFlagsFromOut | activeInFlagsDirect;
            var inRequirementsMet = patternInFlags.BitwiseAND(Lit(activeInFlags)).EqualTo(patternInFlags);

            filteredPatternsDf = uniquePatternsDf.Filter(
                Col("sequence").BitwiseAND(Lit((int)ChessPolicy.Sequence.Public)).NotEqual(Lit(0))
                .And(hasNoInRequirements.Or(inRequirementsMet))
            );
        }
        else
        {
            // No active sequences - filter by Public flag only
            filteredPatternsDf = uniquePatternsDf.Filter(
                Col("sequence").BitwiseAND(Lit((int)ChessPolicy.Sequence.Public)).NotEqual(Lit(0))
            );
        }

        // ===== STEP 2: Cross-join actor pieces with patterns =====
        // Using pre-filtered patterns to reduce cartesian product
        var dfA = actorPerspectives
            .WithColumnRenamed("piece", "src_piece")
            .WithColumnRenamed("generic_piece", "src_generic_piece")
            .WithColumnRenamed("x", "src_x")
            .WithColumnRenamed("y", "src_y")
            .CrossJoin(filteredPatternsDf);

        // CRITICAL: Repartition immediately after cross-join to break up the massive logical plan
        // This forces Spark to materialize and process in smaller chunks
        var dfA_repartitioned = dfA.Repartition(100);


        // ===== STEP 3: Apply source condition filtering =====
        // Require ALL bits of src_conditions to be present in src_generic_piece
        // This aggressively reduces the dataframe size before further processing
        var dfB = dfA_repartitioned.Filter(
            Col("src_generic_piece").BitwiseAND(Col("src_conditions"))
            .EqualTo(Col("src_conditions"))
        );


        // ===== STEP 4: Skip sequence filtering (done in STEP 1) =====
        // Sequence filtering was moved to pattern preprocessing in STEP 1
        var dfC = dfB;


        // ===== STEP 5: Compute dst_x, dst_y =====
        // Build per-faction alternating signs: +1, -1, +1, -1 ...
        var deltaYSignCol = Lit(1); // start with default
        for (int i = specificFactions.Length - 1; i >= 0; i--)
        {
            // Use src_piece (actor piece) for faction sign; perspective_piece may be absent in some pipelines
            var condition = Col("src_piece").BitwiseAND(Lit((int)specificFactions[i])).NotEqual(Lit(0));
            var value = Lit(i % 2 == 0 ? 1 : -1);
            deltaYSignCol = When(condition, value).Otherwise(deltaYSignCol);
        }

        // Ensure source coordinate columns exist; fall back to perspective or raw coordinates if missing.
        var hasSrcX = dfC.Columns().Contains("src_x");
        var hasSrcY = dfC.Columns().Contains("src_y");

        if (!hasSrcX)
        {
            if (dfC.Columns().Contains("x"))
            {
                dfC = dfC.WithColumn("src_x", Col("x"));
            }
            else if (dfC.Columns().Contains("perspective_x"))
            {
                dfC = dfC.WithColumn("src_x", Col("perspective_x"));
            }
        }

        if (!hasSrcY)
        {
            if (dfC.Columns().Contains("y"))
            {
                dfC = dfC.WithColumn("src_y", Col("y"));
            }
            else if (dfC.Columns().Contains("perspective_y"))
            {
                dfC = dfC.WithColumn("src_y", Col("perspective_y"));
            }
        }

        var srcXCol = Col("src_x");
        var srcYCol = Col("src_y");

        var dfD = dfC
            .WithColumn("delta_y_sign", deltaYSignCol)
            .WithColumn("dst_x", srcXCol + Col("delta_x"))
            .WithColumn("dst_y", srcYCol + (Col("delta_y") * Col("delta_y_sign")));


        dfD = dfD.Drop("delta_x", "delta_y", "delta_y_sign");

        // ===== STEP 6: Build lookup dataframe from actor perspectives =====
        // This ensures dst_generic_piece is computed using the SAME perspective
        // as the source piece.
        var lookupDf = perspectivesDf
            .Select(
                Col("x").Alias("lookup_x"),
                Col("y").Alias("lookup_y"),
                Col("perspective_x").Alias("lookup_perspective_x"),
                Col("perspective_y").Alias("lookup_perspective_y"),
                Col("generic_piece").Alias("lookup_generic_piece")
            );


        // ===== STEP 7: Join src perspective to dst square =====
        // Use SAME perspective_x/perspective_y to ensure consistency
        var dfF = dfD.Join(
            lookupDf,
            (Col("perspective_x") == Col("lookup_perspective_x"))
            .And(Col("perspective_y") == Col("lookup_perspective_y"))
            .And(Col("dst_x") == Col("lookup_x"))
            .And(Col("dst_y") == Col("lookup_y")),
            "left_outer"
        );


        // ===== STEP 8: Fill missing generic piece as OutOfBounds =====
        var dfG = dfF.Na().Fill((int)ChessPolicy.Piece.OutOfBounds, new[] { "lookup_generic_piece" });

        // ===== STEP 9: Remove moves landing out-of-bounds =====
        var dfH = dfG.Filter(
            Col("lookup_generic_piece") != Lit((int)ChessPolicy.Piece.OutOfBounds)
        );

        // ===== STEP 10: Rename dst_generic_piece =====
        var dfI = dfH
            .Drop("lookup_x", "lookup_y", "lookup_perspective_x", "lookup_perspective_y")
            .WithColumnRenamed("lookup_generic_piece", "dst_generic_piece");

        // ===== STEP 11: Apply destination condition filtering =====
        // Require ALL bits of dst_conditions to be present in dst_generic_piece
        var dfJ = dfI.Filter(
            Col("dst_generic_piece").BitwiseAND(Col("dst_conditions"))
            .EqualTo(Col("dst_conditions"))
        );

        // ===== STEP 12: Final cleanup =====
        var finalDf = dfJ.Drop("src_conditions", "dst_conditions");

        return finalDf;
    }
}
