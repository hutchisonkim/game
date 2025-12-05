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
        // ===== STEP 0: Deduplicate patterns =====
        var uniquePatternsDf = patternsDf.DropDuplicates();
        if (debug) DebugShow(patternsDf, "Input patternsDf");
        if (debug) DebugShow(uniquePatternsDf, "patternsDf (deduped)");

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

        if (debug) DebugShow(actorPerspectives, "actorPerspectives (before faction filter)");

        // Apply turn-based faction filter
        var turnFaction = specificFactions[turn % specificFactions.Length];
        actorPerspectives = actorPerspectives
            .Filter(
                Col("piece").BitwiseAND(Lit((int)turnFaction)).NotEqual(Lit(0))
            );

        if (debug) DebugShow(actorPerspectives, $"actorPerspectives (after faction filter for {turnFaction})");

        // ===== STEP 2: Cross-join actor pieces with patterns =====
        var dfA = actorPerspectives
            .WithColumnRenamed("piece", "src_piece")
            .WithColumnRenamed("generic_piece", "src_generic_piece")
            .WithColumnRenamed("x", "src_x")
            .WithColumnRenamed("y", "src_y")
            .CrossJoin(uniquePatternsDf);

        if (debug) DebugShow(dfA, "After CrossJoin (dfA)");

        // ===== STEP 3: Apply source condition filtering =====
        // Require ALL bits of src_conditions to be present in src_generic_piece
        var dfB = dfA.Filter(
            Col("src_generic_piece").BitwiseAND(Col("src_conditions"))
            .EqualTo(Col("src_conditions"))
        );

        if (debug) DebugShow(dfB, "After filtering src_conditions (dfB)");

        // ===== STEP 4: Apply sequence filtering =====
        // When activeSequences is None, just filter by Public flag (backward compatible)
        // When activeSequences has Out* flags active, enable patterns with matching In* flags
        DataFrame dfC;
        var activeSeqInt = (int)activeSequences;

        if (activeSeqInt == 0)
        {
            // No active sequences - use simple Public filter (backward compatible)
            dfC = dfB.Filter(
                Col("sequence").BitwiseAND(Lit((int)ChessPolicy.Sequence.Public)).NotEqual(Lit(0))
            );
        }
        else
        {
            // Active sequences present - apply In/Out matching logic
            var inMask = (int)ChessPolicy.Sequence.InMask;

            // Pattern's In* flags (what it requires)
            var patternInFlags = Col("sequence").BitwiseAND(Lit(inMask));

            // Check if pattern has no In* requirements (it's an entry pattern)
            var hasNoInRequirements = patternInFlags.EqualTo(Lit(0));

            // Check if pattern's In* requirements are met by activeSequences
            // The In/Out pairs have consecutive bit positions in the enum:
            // InA = 1 << 5, OutA = 1 << 6, InB = 1 << 7, OutB = 1 << 8, etc.
            // This means OutX >> 1 = InX for all pairs.
            var activeInFlagsFromOut = (activeSeqInt >> 1) & inMask; // Shift Out flags to corresponding In flags
            var activeInFlagsDirect = activeSeqInt & inMask; // In flags passed directly
            var activeInFlags = activeInFlagsFromOut | activeInFlagsDirect; // Combine both
            var inRequirementsMet = patternInFlags.BitwiseAND(Lit(activeInFlags)).EqualTo(patternInFlags);

            // A pattern can execute if it's Public and (has no In requirements OR In requirements are met)
            dfC = dfB.Filter(
                Col("sequence").BitwiseAND(Lit((int)ChessPolicy.Sequence.Public)).NotEqual(Lit(0))
                .And(hasNoInRequirements.Or(inRequirementsMet))
            );
        }

        if (debug) DebugShow(dfC, "After sequence filter (dfC)");

        // ===== STEP 5: Compute dst_x, dst_y =====
        // Build per-faction alternating signs: +1, -1, +1, -1 ...
        var deltaYSignCol = Lit(1); // start with default
        for (int i = specificFactions.Length - 1; i >= 0; i--)
        {
            var condition = Col("perspective_piece").BitwiseAND(Lit((int)specificFactions[i])).NotEqual(Lit(0));
            var value = Lit(i % 2 == 0 ? 1 : -1);
            deltaYSignCol = When(condition, value).Otherwise(deltaYSignCol);
        }

        var dfD = dfC
            .WithColumn("delta_y_sign", deltaYSignCol)
            .WithColumn("dst_x", Col("src_x") + Col("delta_x"))
            .WithColumn("dst_y", Col("src_y") + (Col("delta_y") * Col("delta_y_sign")));

        if (debug) DebugShow(dfD, "After computing dst_x/dst_y (dfD)");

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

        if (debug) DebugShow(lookupDf, "Lookup DF (actor-based)");

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

        if (debug) DebugShow(dfF, "After left join (dfF)");

        // ===== STEP 8: Fill missing generic piece as OutOfBounds =====
        var dfG = dfF.Na().Fill((int)ChessPolicy.Piece.OutOfBounds, new[] { "lookup_generic_piece" });
        if (debug) DebugShow(dfG, "After Na.Fill (dfG)");

        // ===== STEP 9: Remove moves landing out-of-bounds =====
        var dfH = dfG.Filter(
            Col("lookup_generic_piece") != Lit((int)ChessPolicy.Piece.OutOfBounds)
        );

        if (debug) DebugShow(dfH, "After filtering OutOfBounds (dfH)");

        // ===== STEP 10: Rename dst_generic_piece =====
        var dfI = dfH
            .Drop("lookup_x", "lookup_y", "lookup_perspective_x", "lookup_perspective_y")
            .WithColumnRenamed("lookup_generic_piece", "dst_generic_piece");

        if (debug) DebugShow(dfI, "After renaming dst_generic_piece (dfI)");

        // ===== STEP 11: Apply destination condition filtering =====
        // Require ALL bits of dst_conditions to be present in dst_generic_piece
        var dfJ = dfI.Filter(
            Col("dst_generic_piece").BitwiseAND(Col("dst_conditions"))
            .EqualTo(Col("dst_conditions"))
        );

        if (debug) DebugShow(dfJ, "After filtering dst_conditions (dfJ)");

        // ===== STEP 12: Final cleanup =====
        var finalDf = dfJ.Drop("src_conditions", "dst_conditions");

        if (debug) DebugShow(finalDf, "FINAL ATOMIC PATTERNS");

        return finalDf;
    }

    // =============== DEBUG UTILITIES ===============

    private static void DebugShow(DataFrame df, string label)
    {
        System.Console.WriteLine($"\n========== {label} ==========");
        System.Console.WriteLine($"Rows: {df.Count()}");
        System.Console.WriteLine($"Columns: {string.Join(", ", df.Columns())}");
    }
}
