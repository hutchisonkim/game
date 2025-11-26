using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;
using static Microsoft.Spark.Sql.Functions;

namespace Game.Chess.HistoryB;

public class ChessPolicy
{
    private readonly SparkSession _spark;
    private readonly PieceFactory _pieceFactory;
    private readonly PatternFactory _patternFactory;
    private readonly TimelineService _timelineService;

    public ChessPolicy(SparkSession spark)
    {
        _spark = spark;
        _pieceFactory = new PieceFactory(_spark);
        _patternFactory = new PatternFactory(_spark);
        _timelineService = new TimelineService();
    }

    /// <summary>
    /// Generates initial perspectives for the given board and factions
    /// </summary>
    public DataFrame GetPerspectives(Board board, Piece[] specificFactions)
    {
        var piecesDf = _pieceFactory.GetPieces(board);

        var perspectivesDf = GetPerspectivesDfFromPieces(piecesDf, specificFactions)
            .WithColumn("perspective_id",
                Sha2(ConcatWs("_",
                    Col("x").Cast("string"),
                    Col("y").Cast("string"),
                    Col("generic_piece").Cast("string")),
                256));

        return perspectivesDf;
    }

    /// <summary>
    /// Builds the timeline of moves for the board up to maxDepth
    /// </summary>
    public DataFrame BuildTimeline(Board board, Piece[] specificFactions, int maxDepth = 3)
    {
        var perspectivesDf = GetPerspectives(board, specificFactions);
        var patternsDf = _patternFactory.GetPatterns();

        return TimelineService.BuildTimeline(perspectivesDf, patternsDf, specificFactions, maxDepth);
    }

    // ----------------- PRIVATE HELPERS -----------------
    private static DataFrame GetPerspectivesDfFromPieces(DataFrame piecesDf, Piece[] specificFactions)
    {
        // 1. Filter to only those pieces that actually have a faction.
        // These are the "actors" who generate perspectives.
        var actorDf = piecesDf.Filter(
            specificFactions
                .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
                .Aggregate((acc, cond) => acc.Or(cond))
        );

        // Rename for perspective origin (actor)
        actorDf = actorDf
            .WithColumnRenamed("x", "perspective_x")
            .WithColumnRenamed("y", "perspective_y")
            .WithColumnRenamed("piece", "perspective_piece");

        // Cross join: actor perspective × full board state
        var perspectivesDf = actorDf.CrossJoin(piecesDf);

        // 2. Build Self / Ally / Foe logic without removing faction bits
        // --------------------------------------------------------------

        // pieceHasFaction (dest piece has any listed faction)
        var pieceHasFaction = specificFactions
            .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            .Aggregate((acc, cond) => acc.Or(cond));

        // piece and perspective share the same faction
        var pieceAndPerspectiveShareFaction = specificFactions
            .Select(f =>
                Col("piece").BitwiseAND((int)f).NotEqual(Lit(0))
                .And(Col("perspective_piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            )
            .Aggregate((acc, cond) => acc.Or(cond));

        // 3. generic_piece preserves all original bits (faction + type)
        // and simply ORs in Self/Ally/Foe relationship bits.
        Column genericPieceCol =
            When(
                (Col("x") == Col("perspective_x")) &
                (Col("y") == Col("perspective_y")),
                Col("piece").BitwiseOR(Lit((int)Piece.Self))
            )
            .When(
                pieceAndPerspectiveShareFaction,
                Col("piece").BitwiseOR(Lit((int)Piece.Ally))
            )
            .When(
                pieceHasFaction &
                Not(pieceAndPerspectiveShareFaction),
                Col("piece").BitwiseOR(Lit((int)Piece.Foe))
            )
            .Otherwise(Col("piece")); // empty or no-faction stays unchanged

        return perspectivesDf.WithColumn("generic_piece", genericPieceCol);
    }


    // ----------------- SUB-SERVICES -----------------
    public class TimelineService()
    {
        public static DataFrame BuildTimeline(DataFrame perspectivesDf, DataFrame patternsDf, Piece[] specificFactions, int maxDepth = 3)
        {
            var timelineDf = perspectivesDf.WithColumn("timestep", Lit(0));

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                var candidatesDf = ComputeNextCandidates(timelineDf.Filter(Col("timestep") == depth - 1), patternsDf, specificFactions);

                var nextPerspectivesDf = ComputeNextPerspectives(candidatesDf)
                    .WithColumn("timestep", Lit(depth));

                timelineDf = timelineDf.Union(nextPerspectivesDf);
            }

            return timelineDf;
        }
        private static void DebugShow(DataFrame df, string label)
        {
            Console.WriteLine($"\n========== {label} ==========");
            Console.WriteLine($"Row count: {df.Count()}");
        }

        public static DataFrame ComputeNextCandidates(DataFrame perspectivesDf, DataFrame patternsDf, Piece[] specificFactions, int turn = 0, Sequence activeSequences = Sequence.None, bool debug = false)
        {
            //
            // 0. Deduplicate patterns
            //
            var uniquePatternsDf = patternsDf.DropDuplicates();
            if(debug) DebugShow(patternsDf, "patternsDf");
            if(debug) DebugShow(uniquePatternsDf, "patternsDf (deduped)");

            //
            // 1. Build ACTOR perspectives: (x,y) == (perspective_x, perspective_y)
            //    AND must have a faction bit (not empty).
            //
            var actorPerspectives = perspectivesDf
                .Filter(
                    Col("x").EqualTo(Col("perspective_x")).And(
                    Col("y").EqualTo(Col("perspective_y"))).And(
                        Col("piece") != Lit((int)Piece.Empty) // ensure only real pieces generate perspectives
                    )
                );

            if(debug) DebugShow(actorPerspectives, "actorPerspectives");

            //TODO: using the turn argument, find the current faction whose turn it is (modulo specificFactions.length).
            // then when getting actor perspectives, filter to only that faction.
            var turnFaction = specificFactions[turn % specificFactions.Length];
            actorPerspectives = actorPerspectives
                .Filter(
                    Col("piece").BitwiseAND(Lit((int)turnFaction)).NotEqual(Lit(0))
                );

            //
            // 2. Cross-join actor pieces with patterns
            //
            var dfA = actorPerspectives
                .WithColumnRenamed("piece", "src_piece")
                .WithColumnRenamed("generic_piece", "src_generic_piece")
                .WithColumnRenamed("x", "src_x")
                .WithColumnRenamed("y", "src_y")
                .CrossJoin(uniquePatternsDf);

            if(debug) DebugShow(dfA, "After CrossJoin (dfA)");

            //
            // 3. Require ALL bits of src_conditions
            //
            var dfB = dfA.Filter(
                Col("src_generic_piece").BitwiseAND(Col("src_conditions"))
                .EqualTo(Col("src_conditions"))
            );

            if(debug) DebugShow(dfB, "After filtering src_conditions (dfB)");

            //
            // 4. Sequence filter - support pattern sequencing
            // When activeSequences is None, just filter by Public flag (backward compatible)
            // When activeSequences has Out* flags active, enable patterns with matching In* flags
            //
            DataFrame dfC;
            var activeSeqInt = (int)activeSequences;
            
            if (activeSeqInt == 0)
            {
                // No active sequences - use simple Public filter (backward compatible)
                dfC = dfB.Filter(
                    Col("sequence").BitwiseAND(Lit((int)Sequence.Public)).NotEqual(Lit(0))
                );
            }
            else
            {
                // Active sequences present - apply In/Out matching logic
                var inMask = (int)Sequence.InMask;
                
                // Pattern's In* flags (what it requires)
                var patternInFlags = Col("sequence").BitwiseAND(Lit(inMask));
                
                // Check if pattern has no In* requirements (it's an entry pattern)
                var hasNoInRequirements = patternInFlags.EqualTo(Lit(0));
                
                // Check if pattern's In* requirements are met by activeSequences
                // The In/Out pairs have consecutive bit positions in the enum:
                // InA = 1 << 5, OutA = 1 << 6, InB = 1 << 7, OutB = 1 << 8, etc.
                // This means OutX >> 1 = InX for all pairs, allowing us to convert
                // active Out flags to their corresponding In flags via a single right shift.
                var activeInFlags = (activeSeqInt >> 1) & inMask; // Shift Out flags to corresponding In flags
                var inRequirementsMet = patternInFlags.BitwiseAND(Lit(activeInFlags)).EqualTo(patternInFlags);
                
                // A pattern can execute if it's Public and (has no In requirements OR In requirements are met)
                dfC = dfB.Filter(
                    Col("sequence").BitwiseAND(Lit((int)Sequence.Public)).NotEqual(Lit(0))
                    .And(hasNoInRequirements.Or(inRequirementsMet))
                );
            }

            if(debug) DebugShow(dfC, "After sequence filter (dfC)");

            //
            // 5. Compute dst_x, dst_y
            //
            // Build per-faction alternating signs: +1, -1, +1, -1 ...
            var signCases = specificFactions
                .Select((faction, index) =>
                    When(
                        Col("perspective_piece").BitwiseAND(Lit((int)faction)).NotEqual(Lit(0)),
                        Lit(index % 2 == 0 ? 1 : -1)
                    )
                );

            // Build a CASE expression by chaining .When().When().When()...
            Column deltaYSignCol = Lit(1); // start with default
            for (int i = specificFactions.Length - 1; i >= 0; i--)
            {
                var condition = Col("perspective_piece").BitwiseAND(Lit((int)specificFactions[i])).NotEqual(Lit(0));
                var value = Lit(i % 2 == 0 ? 1 : -1);
                deltaYSignCol = When(condition, value).Otherwise(deltaYSignCol);
            }

            // Default if no faction matched (empty squares) → 1
            // deltaYSignCol = deltaYSignCol.Otherwise(Lit(1));

            var dfD = dfC
                .WithColumn("delta_y_sign", deltaYSignCol)
                .WithColumn("dst_x", Col("src_x") + Col("delta_x"))
                .WithColumn("dst_y", Col("src_y") + (Col("delta_y") * Col("delta_y_sign")));

            if(debug) dfD.Show();
            
            dfD =dfD
                .Drop("delta_x", "delta_y", "delta_y_sign");


            if(debug) DebugShow(dfD, "After computing dst_x/dst_y (dfD)");

            //
            // 6. Lookup DF: only perspectives *from actors*.
            //    This ensures dst_generic_piece is computed using the SAME perspective
            //    as the source piece.
            //
            var lookupDf = perspectivesDf
                .Select(
                    Col("x").Alias("lookup_x"),
                    Col("y").Alias("lookup_y"),
                    Col("perspective_x").Alias("lookup_perspective_x"),
                    Col("perspective_y").Alias("lookup_perspective_y"),
                    Col("generic_piece").Alias("lookup_generic_piece")
                );

            if(debug) DebugShow(lookupDf, "Lookup DF (actor-based)");

            //
            // 7. Join src perspective to dst square using SAME perspective_x/perspective_y
            //
            var dfF = dfD.Join(
                lookupDf,
                (Col("perspective_x") == Col("lookup_perspective_x"))
                .And(Col("perspective_y") == Col("lookup_perspective_y"))
                .And(Col("dst_x") == Col("lookup_x"))
                .And(Col("dst_y") == Col("lookup_y")),
                "left_outer"
            );

            if(debug) DebugShow(dfF, "After left join (dfF)");

            //
            // 8. Fill missing generic piece as OutOfBounds
            //
            var dfG = dfF.Na().Fill((int)Piece.OutOfBounds, new[] { "lookup_generic_piece" });
            if(debug) DebugShow(dfG, "After Na.Fill (dfG)");
            if(debug) dfG.Show();

            //
            // 9. Remove moves landing out-of-bounds
            //
            var dfH = dfG.Filter(
                Col("lookup_generic_piece") != Lit((int)Piece.OutOfBounds)
            );

            if(debug) DebugShow(dfH, "After filtering OutOfBounds (dfH)");

            //
            // 10. Rename dst_generic_piece
            //
            var dfI = dfH
                .Drop("lookup_x", "lookup_y", "lookup_perspective_x", "lookup_perspective_y")
                .WithColumnRenamed("lookup_generic_piece", "dst_generic_piece");

            if(debug) DebugShow(dfI, "After renaming dst_generic_piece (dfI)");
            if(debug) dfI.Show();

            //
            // 11. Require ALL bits from dst_conditions
            //
            var dfJ = dfI.Filter(
                Col("dst_generic_piece").BitwiseAND(Col("dst_conditions"))
                .EqualTo(Col("dst_conditions"))
            );

            if(debug) DebugShow(dfJ, "After filtering dst_conditions (dfJ)");

            //
            // 12. Final cleanup
            //
            var finalDf = dfJ.Drop("src_conditions", "dst_conditions");

            if(debug) DebugShow(finalDf, "FINAL CANDIDATES");
            if(debug) finalDf.Show();

            return finalDf;
        }

        // Static validation: Verify the bit shift relationship between Out and In flags at compile time
        // This ensures OutX >> 1 = InX for all pairs
        static TimelineService()
        {
            // Validate that OutX >> 1 = InX for all pairs
            System.Diagnostics.Debug.Assert((int)Sequence.OutA >> 1 == (int)Sequence.InA, "OutA >> 1 should equal InA");
            System.Diagnostics.Debug.Assert((int)Sequence.OutB >> 1 == (int)Sequence.InB, "OutB >> 1 should equal InB");
            System.Diagnostics.Debug.Assert((int)Sequence.OutC >> 1 == (int)Sequence.InC, "OutC >> 1 should equal InC");
            System.Diagnostics.Debug.Assert((int)Sequence.OutD >> 1 == (int)Sequence.InD, "OutD >> 1 should equal InD");
            System.Diagnostics.Debug.Assert((int)Sequence.OutE >> 1 == (int)Sequence.InE, "OutE >> 1 should equal InE");
            System.Diagnostics.Debug.Assert((int)Sequence.OutF >> 1 == (int)Sequence.InF, "OutF >> 1 should equal InF");
            System.Diagnostics.Debug.Assert((int)Sequence.OutG >> 1 == (int)Sequence.InG, "OutG >> 1 should equal InG");
            System.Diagnostics.Debug.Assert((int)Sequence.OutH >> 1 == (int)Sequence.InH, "OutH >> 1 should equal InH");
            System.Diagnostics.Debug.Assert((int)Sequence.OutI >> 1 == (int)Sequence.InI, "OutI >> 1 should equal InI");
        }

        /// <summary>
        /// Converts Out flags to corresponding In flags via bit shift.
        /// The In/Out pairs have consecutive bit positions in the enum:
        /// InA = 1 << 5, OutA = 1 << 6, InB = 1 << 7, OutB = 1 << 8, etc.
        /// This means OutX >> 1 = InX for all pairs.
        /// </summary>
        public static int ConvertOutFlagsToInFlags(int outFlags)
        {
            var inMask = (int)Sequence.InMask;
            return (outFlags >> 1) & inMask;
        }

        /// <summary>
        /// Computes all sequenced/recursive moves for sliding pieces like Rook/Bishop/Queen.
        /// This follows the timeline architecture: iteratively expand perspectives and compute candidates.
        /// 
        /// Algorithm:
        /// 1. Find entry patterns (OutX | InstantRecursive, no Public) - these start the sequence
        /// 2. From entry move destinations, compute continuation patterns (InX | Public) - final landing spots
        /// 3. For InstantRecursive, allow the entry pattern to repeat from each new position
        /// 4. Accumulate all Public moves as valid final moves
        /// </summary>
        public static DataFrame ComputeSequencedMoves(
            DataFrame perspectivesDf,
            DataFrame patternsDf,
            Piece[] specificFactions,
            int turn = 0,
            int maxDepth = 7,
            bool debug = false)
        {
            // Filter patterns for entry (OutX, InstantRecursive, no Public) and continuation (InX, Public)
            var outMask = (int)Sequence.OutMask;
            var inMask = (int)Sequence.InMask;
            var instantRecursive = (int)Sequence.InstantRecursive;
            var publicFlag = (int)Sequence.Public;

            // Entry patterns: have Out* flag and InstantRecursive, but NOT Public
            var entryPatternsDf = patternsDf.Filter(
                Col("sequence").BitwiseAND(Lit(outMask)).NotEqual(Lit(0))
                .And(Col("sequence").BitwiseAND(Lit(instantRecursive)).EqualTo(Lit(instantRecursive)))
                .And(Col("sequence").BitwiseAND(Lit(publicFlag)).EqualTo(Lit(0)))
            );

            // Continuation patterns: have In* flag and Public flag
            var continuationPatternsDf = patternsDf.Filter(
                Col("sequence").BitwiseAND(Lit(inMask)).NotEqual(Lit(0))
                .And(Col("sequence").BitwiseAND(Lit(publicFlag)).NotEqual(Lit(0)))
            );

            if (debug)
            {
                Console.WriteLine($"Entry patterns count: {entryPatternsDf.Count()}");
                Console.WriteLine($"Continuation patterns count: {continuationPatternsDf.Count()}");
            }

            // Start by computing entry moves
            // These need to bypass the Public filter since entry patterns don't have Public flag
            var entryMoves = ComputeNextCandidatesInternal(
                perspectivesDf,  // actor perspectives
                perspectivesDf,  // lookup perspectives (full board)
                entryPatternsDf,
                specificFactions,
                turn,
                Sequence.None,
                skipPublicFilter: true,
                debug: debug
            );

            if (debug) Console.WriteLine($"Initial entry moves: {entryMoves.Count()}");

            // Collect all valid final moves (continuation moves with Public flag)
            DataFrame? allFinalMoves = null;

            // Current frontier of positions to expand from
            var currentEntryMoves = entryMoves;

            for (int depth = 0; depth < maxDepth && currentEntryMoves.Count() > 0; depth++)
            {
                if (debug) Console.WriteLine($"Depth {depth}: {currentEntryMoves.Count()} entry moves");

                // Get the Out flags from current entry moves
                var outFlagsRows = currentEntryMoves
                    .Select(Col("sequence").BitwiseAND(Lit(outMask)).Alias("out_flags"))
                    .Distinct()
                    .Collect();

                int activeOutFlags = 0;
                foreach (var row in outFlagsRows)
                {
                    activeOutFlags |= row.GetAs<int>("out_flags");
                }

                if (activeOutFlags == 0) break;

                var activeSequence = (Sequence)activeOutFlags;
                if (debug) Console.WriteLine($"Active Out flags: {activeOutFlags:X}");

                // Create new perspectives from entry move destinations
                // The piece "moves" to dst, so we need perspectives from there
                var nextPerspectives = ComputeNextPerspectivesFromMoves(currentEntryMoves, perspectivesDf);

                if (debug) Console.WriteLine($"Next perspectives: {nextPerspectives.Count()}");
                if (nextPerspectives.Count() == 0) break;

                // Compute continuation moves (InX | Public) from new perspectives
                // Use original perspectivesDf for lookup (to see what's at destination squares)
                var continuationMoves = ComputeNextCandidatesInternal(
                    nextPerspectives,  // actor perspectives (moved pieces)
                    perspectivesDf,    // lookup perspectives (full board)
                    continuationPatternsDf,
                    specificFactions,
                    turn,
                    activeSequence,
                    skipPublicFilter: false,
                    debug: debug
                );

                if (debug) Console.WriteLine($"Continuation moves: {continuationMoves.Count()}");

                // Add continuation moves to final results
                if (continuationMoves.Count() > 0)
                {
                    allFinalMoves = allFinalMoves == null ? continuationMoves : allFinalMoves.Union(continuationMoves);
                }

                // For InstantRecursive, also compute more entry moves from the new perspectives
                var nextEntryMoves = ComputeNextCandidatesInternal(
                    nextPerspectives,  // actor perspectives (moved pieces)
                    perspectivesDf,    // lookup perspectives (full board)
                    entryPatternsDf,
                    specificFactions,
                    turn,
                    Sequence.None,
                    skipPublicFilter: true,
                    debug: debug
                );

                if (debug) Console.WriteLine($"Next entry moves: {nextEntryMoves.Count()}");

                currentEntryMoves = nextEntryMoves;
            }

            // Return all final moves, or empty DataFrame if none found
            if (allFinalMoves == null)
            {
                return entryMoves.Limit(0); // Return empty with same schema
            }

            return allFinalMoves;
        }

        /// <summary>
        /// Creates new perspectives centered on move destinations.
        /// The piece at src moves to dst, so we need to compute perspectives from dst.
        /// </summary>
        private static DataFrame ComputeNextPerspectivesFromMoves(DataFrame movesDf, DataFrame originalPerspectivesDf)
        {
            // Get unique source perspectives from the moves
            var moveSources = movesDf.Select(
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_x"),
                Col("dst_y")
            ).Distinct();

            // The piece at src moves to dst
            // We need to update the perspective to see from dst
            // The piece itself doesn't change, but its position does
            var newPerspectives = moveSources
                .WithColumn("x", Col("dst_x"))
                .WithColumn("y", Col("dst_y"))
                .WithColumn("piece", Col("src_piece"))
                .WithColumn("generic_piece", Col("src_generic_piece"))
                .WithColumn("perspective_id",
                    Sha2(ConcatWs("_",
                        Col("dst_x").Cast("string"),
                        Col("dst_y").Cast("string"),
                        Col("src_generic_piece").Cast("string")),
                    256))
                .Select(
                    Col("perspective_x"),
                    Col("perspective_y"),
                    Col("perspective_piece"),
                    Col("x"),
                    Col("y"),
                    Col("piece"),
                    Col("generic_piece"),
                    Col("perspective_id")
                );

            return newPerspectives;
        }

        /// <summary>
        /// Internal version of ComputeNextCandidates with skipPublicFilter option.
        /// </summary>
        private static DataFrame ComputeNextCandidatesInternal(
            DataFrame actorPerspectivesDf,
            DataFrame lookupPerspectivesDf,
            DataFrame patternsDf,
            Piece[] specificFactions,
            int turn,
            Sequence activeSequences,
            bool skipPublicFilter,
            bool debug)
        {
            // Deduplicate patterns
            var uniquePatternsDf = patternsDf.DropDuplicates();

            // Build ACTOR perspectives - filter to pieces that are at their own perspective position
            var actorPerspectives = actorPerspectivesDf
                .Filter(
                    Col("x").EqualTo(Col("perspective_x")).And(
                    Col("y").EqualTo(Col("perspective_y"))).And(
                        Col("piece") != Lit((int)Piece.Empty)
                    )
                );

            var turnFaction = specificFactions[turn % specificFactions.Length];
            actorPerspectives = actorPerspectives
                .Filter(
                    Col("piece").BitwiseAND(Lit((int)turnFaction)).NotEqual(Lit(0))
                );

            // Cross-join actor pieces with patterns
            var dfA = actorPerspectives
                .WithColumnRenamed("piece", "src_piece")
                .WithColumnRenamed("generic_piece", "src_generic_piece")
                .WithColumnRenamed("x", "src_x")
                .WithColumnRenamed("y", "src_y")
                .CrossJoin(uniquePatternsDf);

            // Require ALL bits of src_conditions
            var dfB = dfA.Filter(
                Col("src_generic_piece").BitwiseAND(Col("src_conditions"))
                .EqualTo(Col("src_conditions"))
            );

            // Sequence filter
            DataFrame dfC;
            var activeSeqInt = (int)activeSequences;

            if (skipPublicFilter)
            {
                // Skip Public filter for entry patterns
                if (activeSeqInt == 0)
                {
                    dfC = dfB;
                }
                else
                {
                    var inMask = (int)Sequence.InMask;
                    var patternInFlags = Col("sequence").BitwiseAND(Lit(inMask));
                    var hasNoInRequirements = patternInFlags.EqualTo(Lit(0));
                    var activeInFlags = (activeSeqInt >> 1) & inMask;
                    var inRequirementsMet = patternInFlags.BitwiseAND(Lit(activeInFlags)).EqualTo(patternInFlags);
                    dfC = dfB.Filter(hasNoInRequirements.Or(inRequirementsMet));
                }
            }
            else if (activeSeqInt == 0)
            {
                dfC = dfB.Filter(
                    Col("sequence").BitwiseAND(Lit((int)Sequence.Public)).NotEqual(Lit(0))
                );
            }
            else
            {
                var inMask = (int)Sequence.InMask;
                var patternInFlags = Col("sequence").BitwiseAND(Lit(inMask));
                var hasNoInRequirements = patternInFlags.EqualTo(Lit(0));
                var activeInFlags = (activeSeqInt >> 1) & inMask;
                var inRequirementsMet = patternInFlags.BitwiseAND(Lit(activeInFlags)).EqualTo(patternInFlags);
                dfC = dfB.Filter(
                    Col("sequence").BitwiseAND(Lit((int)Sequence.Public)).NotEqual(Lit(0))
                    .And(hasNoInRequirements.Or(inRequirementsMet))
                );
            }

            // Compute dst_x, dst_y with faction-based delta_y sign
            Column deltaYSignCol = Lit(1);
            for (int i = specificFactions.Length - 1; i >= 0; i--)
            {
                var condition = Col("perspective_piece").BitwiseAND(Lit((int)specificFactions[i])).NotEqual(Lit(0));
                var value = Lit(i % 2 == 0 ? 1 : -1);
                deltaYSignCol = When(condition, value).Otherwise(deltaYSignCol);
            }

            var dfD = dfC
                .WithColumn("delta_y_sign", deltaYSignCol)
                .WithColumn("dst_x", Col("src_x") + Col("delta_x"))
                .WithColumn("dst_y", Col("src_y") + (Col("delta_y") * Col("delta_y_sign")))
                .Drop("delta_x", "delta_y", "delta_y_sign");

            // Lookup destination piece using the lookup perspectives (original board state)
            var lookupDf = lookupPerspectivesDf
                .Select(
                    Col("x").Alias("lookup_x"),
                    Col("y").Alias("lookup_y"),
                    Col("perspective_x").Alias("lookup_perspective_x"),
                    Col("perspective_y").Alias("lookup_perspective_y"),
                    Col("generic_piece").Alias("lookup_generic_piece")
                );

            var dfF = dfD.Join(
                lookupDf,
                (Col("perspective_x") == Col("lookup_perspective_x"))
                .And(Col("perspective_y") == Col("lookup_perspective_y"))
                .And(Col("dst_x") == Col("lookup_x"))
                .And(Col("dst_y") == Col("lookup_y")),
                "left_outer"
            );

            var dfG = dfF.Na().Fill((int)Piece.OutOfBounds, new[] { "lookup_generic_piece" });

            var dfH = dfG.Filter(
                Col("lookup_generic_piece") != Lit((int)Piece.OutOfBounds)
            );

            var dfI = dfH
                .Drop("lookup_x", "lookup_y", "lookup_perspective_x", "lookup_perspective_y")
                .WithColumnRenamed("lookup_generic_piece", "dst_generic_piece");

            var dfJ = dfI.Filter(
                Col("dst_generic_piece").BitwiseAND(Col("dst_conditions"))
                .EqualTo(Col("dst_conditions"))
            );

            var finalDf = dfJ.Drop("src_conditions", "dst_conditions");

            return finalDf;
        }

        private static DataFrame ComputeNextPerspectives(DataFrame candidatesDf)
        {
            return candidatesDf
                .WithColumnRenamed("src_piece", "piece")
                .WithColumnRenamed("src_generic_piece", "generic_piece")
                .WithColumnRenamed("src_x", "x")
                .WithColumnRenamed("src_y", "y")
                .WithColumn("perspective_id",
                    Sha2(ConcatWs("_",
                        Col("x").Cast("string"),
                        Col("y").Cast("string"),
                        Col("generic_piece").Cast("string")),
                    256))
                .Select(
                    Col("perspective_x"),
                    Col("perspective_y"),
                    Col("perspective_piece"),
                    Col("x"),
                    Col("y"),
                    Col("piece"),
                    Col("generic_piece"),
                    Col("perspective_id")
                );
        }
    }

    public class PieceFactory(SparkSession spark)
    {
        private readonly SparkSession _spark = spark;

        public DataFrame GetPieces(Board board)
        {
            var boardSchema = new StructType(
            [
                new StructField("x", new IntegerType()),
                new StructField("y", new IntegerType()),
                new StructField("piece", new IntegerType())
            ]);

            var boardData = Enumerable.Range(0, board.Width)
                .SelectMany(x => Enumerable.Range(0, board.Height)
                    .Select(y => new GenericRow([x, y, (int)board.Cell[x, y]])))
                .ToList();

            return _spark.CreateDataFrame(boardData, boardSchema);
        }
    }

    public class PatternFactory(SparkSession spark)
    {
        private readonly SparkSession _spark = spark;
        private static readonly (Piece SrcConditions, Piece DstConditions, (int X, int Y) Delta, Sequence Sequence, Piece DstEffects)[] values =
        [
            //=====pawn=====
            // pawn forward (move-only)
            (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q1(), Sequence.OutA      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.Empty),
            // pawn forward (post, do nothing)
            // (Piece.Self | Piece.MintPawn, Piece.Empty,        (0, 0).Q1(), Sequence.InA       | Sequence.VariantAny | Sequence.Instant                  | Sequence.Public, ~Piece.Mint),
            // // pawn forward (post, move-only)
            // (Piece.Self | Piece.MintPawn, Piece.Empty,        (0, 1).Q1(), Sequence.InA       | Sequence.VariantAny | Sequence.Instant                  | Sequence.Public, ~Piece.Mint | Piece.Passing),
            // // pawn forward (capture-only)
            (Piece.Self | Piece.Pawn,     Piece.Foe,          (1, 1).Q1(), Sequence.OutB      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Pawn,     Piece.Foe,          (1, 1).Q2(), Sequence.OutB      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // // pawn promotion trigger
            // (Piece.Self | Piece.Pawn,     Piece.OutOfBounds,  (0, 1).Q1(), Sequence.InAB_OutC | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.None,   Piece.None),
            // // pawn promotions
            // (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Knight),
            // (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Rook),
            // (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Bishop),
            // (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Queen),
            //=====bishop=====
            // bishop (pre, move-only)
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q1(), Sequence.OutF      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q2(), Sequence.OutF      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q3(), Sequence.OutF      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q4(), Sequence.OutF      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            // bishop (move to empty)
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q1(), Sequence.InF       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q2(), Sequence.InF       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q3(), Sequence.InF       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q4(), Sequence.InF       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            // bishop (capture foe)
            (Piece.Self | Piece.Bishop,   Piece.Foe,          (1, 1).Q1(), Sequence.InF       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Foe,          (1, 1).Q2(), Sequence.InF       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Foe,          (1, 1).Q3(), Sequence.InF       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Bishop,   Piece.Foe,          (1, 1).Q4(), Sequence.InF       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            //=====queen=====
            // queen as bishop (pre, move-only)
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q1(), Sequence.OutG      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q2(), Sequence.OutG      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q3(), Sequence.OutG      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q4(), Sequence.OutG      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            // queen as bishop (move to empty)
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q1(), Sequence.InG       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q2(), Sequence.InG       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q3(), Sequence.InG       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q4(), Sequence.InG       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            // queen as bishop (capture foe)
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 1).Q1(), Sequence.InG       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 1).Q2(), Sequence.InG       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 1).Q3(), Sequence.InG       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 1).Q4(), Sequence.InG       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            // queen as rook (pre, move-only)
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q1(), Sequence.OutH      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q3(), Sequence.OutH      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q1(), Sequence.OutH      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q3(), Sequence.OutH      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
            // queen as rook (move to empty)
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q1(), Sequence.InH       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q3(), Sequence.InH       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q1(), Sequence.InH       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q3(), Sequence.InH       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            // queen as rook (capture foe)
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 0).Q1(), Sequence.InH       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (1, 0).Q3(), Sequence.InH       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (0, 1).Q1(), Sequence.InH       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Queen,    Piece.Foe,          (0, 1).Q3(), Sequence.InH       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
            //=====rook=====
            // rook (pre, move-only)
            (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q1(), Sequence.OutI      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q3(), Sequence.OutI      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q1(), Sequence.OutI      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q3(), Sequence.OutI      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
            // rook (move to empty)
            (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q1(), Sequence.InI       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q3(), Sequence.InI       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q1(), Sequence.InI       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q3(), Sequence.InI       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // rook (capture foe)
            (Piece.Self | Piece.Rook,     Piece.Foe,          (1, 0).Q1(), Sequence.InI       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Foe,          (1, 0).Q3(), Sequence.InI       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Foe,          (0, 1).Q1(), Sequence.InI       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.Rook,     Piece.Foe,          (0, 1).Q3(), Sequence.InI       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            //=====knight=====
            (Piece.Self | Piece.Knight,   Piece.Empty,   (2, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (2, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (2, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (2, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (1, 2).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (1, 2).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (1, 2).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Empty,   (1, 2).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            
            (Piece.Self | Piece.Knight,   Piece.Foe,   (2, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (2, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (2, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (2, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (1, 2).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (1, 2).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (1, 2).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            (Piece.Self | Piece.Knight,   Piece.Foe,   (1, 2).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            //=====king=====
            // king as rook (move to empty)
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 0).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 0).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (0, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (0, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // king as rook (capture foe)
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 0).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 0).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (0, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (0, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // king as bishop (move to empty)
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Empty,   (1, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // king as bishop (capture foe)
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            (Piece.Self | Piece.King,     Piece.Foe,     (1, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
            // // castling moves (left)
            // (Piece.Self | Piece.MintRook, Piece.Empty,        (0, 1).Q1(), Sequence.OutD      | Sequence.Variant1   | Sequence.ParallelInstantRecursive | Sequence.Public, Piece.None),
            // (Piece.Self | Piece.MintRook, Piece.AllyKing,     (0, 1).Q1(), Sequence.InD       | Sequence.Variant1   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
            // (Piece.Self | Piece.MintKing, Piece.EmptyAndSafe, (0, 1).Q3(), Sequence.OutD      | Sequence.Variant1   | Sequence.ParallelInstantRecursive | Sequence.Public, Piece.None),
            // (Piece.Self | Piece.MintKing, Piece.AllyRook,     (0, 1).Q3(), Sequence.InD       | Sequence.Variant1   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
            // // castling moves (right)
            // (Piece.Self | Piece.MintRook, Piece.Empty,        (0, 1).Q3(), Sequence.OutD      | Sequence.Variant2   | Sequence.ParallelInstantRecursive | Sequence.None,   Piece.None),
            // (Piece.Self | Piece.MintRook, Piece.AllyKing,     (0, 1).Q3(), Sequence.InD       | Sequence.Variant2   | Sequence.ParallelMandatory        | Sequence.None,   ~Piece.Mint),
            // (Piece.Self | Piece.MintKing, Piece.EmptyAndSafe, (0, 1).Q1(), Sequence.OutD      | Sequence.Variant2   | Sequence.ParallelInstantRecursive | Sequence.None,   Piece.None),
            // (Piece.Self | Piece.MintKing, Piece.AllyRook,     (0, 1).Q1(), Sequence.InD       | Sequence.Variant2   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
            // // en passant (1. capture sideways)
            // (Piece.Self | Piece.Pawn,     Piece.PassingFoe,   (1, 0).Q1(), Sequence.OutE      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            // (Piece.Self | Piece.Pawn,     Piece.PassingFoe,   (1, 0).Q3(), Sequence.OutE      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
            // // en passant (2. move forward)
            // (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q1(), Sequence.InE       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, Piece.None),
            // // en passant (reset passing flag)
            // (Piece.Self | Piece.Passing,  Piece.None,         (0, 0).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.None,   ~Piece.Passing),
        ];

        public DataFrame GetPatterns()
        {
            var schema = new StructType(
            [
                new StructField("src_conditions", new IntegerType()),
                new StructField("dst_conditions", new IntegerType()),
                new StructField("delta_x", new IntegerType()),
                new StructField("delta_y", new IntegerType()),
                new StructField("sequence", new IntegerType()),
                new StructField("dst_effects", new IntegerType()),
            ]);

            var genericRows = values.Select(r => new GenericRow(
            [
                (int)r.SrcConditions,
                (int)r.DstConditions,
                r.Delta.X,
                r.Delta.Y,
                (int)r.Sequence,
                (int)r.DstEffects
            ])).ToList();

            return _spark.CreateDataFrame(genericRows, schema);
        }
    }

    // ----------------- BOARD & PIECES -----------------
    public readonly record struct Board(int Width, int Height, Piece[,] Cell)
    {
        public void Initialize(Piece pieceOverride = Piece.None)
        {
            var initial = CreateInitialBoard(pieceOverride);
            for (int i = 0; i < initial.GetLength(0); i++)
                for (int j = 0; j < initial.GetLength(1); j++)
                    Cell[i, j] = initial[i, j];
        }

        public static Board Default => new(8, 8, new Piece[8, 8]);

        public static Piece[,] CreateInitialBoard(Piece pieceOverride = Piece.None)
        {
            var board = Default.Cell;
            bool hasOverride = pieceOverride != Piece.None;

            Piece Set(Piece p) => hasOverride ? pieceOverride : p;

            // pawns
            for (int x = 0; x < 8; x++)
            {
                board[x, 1] = Set(Piece.White | Piece.Mint | Piece.Pawn);
                board[x, 6] = Set(Piece.Black | Piece.Mint | Piece.Pawn);
            }

            // rooks
            board[0, 0] = board[7, 0] = Set(Piece.White | Piece.Mint | Piece.Rook);
            board[0, 7] = board[7, 7] = Set(Piece.Black | Piece.Mint | Piece.Rook);

            // knights
            board[1, 0] = board[6, 0] = Set(Piece.White | Piece.Mint | Piece.Knight);
            board[1, 7] = board[6, 7] = Set(Piece.Black | Piece.Mint | Piece.Knight);

            // bishops
            board[2, 0] = board[5, 0] = Set(Piece.White | Piece.Mint | Piece.Bishop);
            board[2, 7] = board[5, 7] = Set(Piece.Black | Piece.Mint | Piece.Bishop);

            // queens
            board[3, 0] = Set(Piece.White | Piece.Mint | Piece.Queen);
            board[3, 7] = Set(Piece.Black | Piece.Mint | Piece.Queen);

            // kings
            board[4, 0] = Set(Piece.White | Piece.Mint | Piece.King);
            board[4, 7] = Set(Piece.Black | Piece.Mint | Piece.King);

            // empties
            for (int x = 0; x < 8; x++)
                for (int y = 2; y <= 5; y++)
                    board[x, y] = Piece.Empty;

            return board;
        }
    }

    [Flags]
    public enum Piece
    {
        None = 0,

        // local faction state
        Self = 1 << 0,
        Ally = 1 << 1,
        Foe = 1 << 2,

        // global faction state
        White = 1 << 3,
        Black = 1 << 4,

        Mint = 1 << 5,
        Passing = 1 << 6,
        Threatened = 1 << 7,
        OutOfBounds = 1 << 8,

        Pawn = 1 << 9,
        Rook = 1 << 10,
        Knight = 1 << 11,
        Bishop = 1 << 12,
        Queen = 1 << 13,
        King = 1 << 14,

        Empty = 1 << 15,


        MintPawn = Mint | Pawn,
        MintRook = Mint | Rook,
        MintKing = Mint | King,

        AllyRook = Ally | Rook,
        AllyKing = Ally | King,

        Any = Ally | Foe,

        EmptyAndSafe = Empty | ~Threatened,

        PassingFoe = Passing | Foe,

    }

    [Flags]
    public enum Sequence
    {
        None = 0,

        Mandatory = 1 << 0,
        Parallel = 1 << 1,
        Instant = 1 << 2,
        Recursive = 1 << 3,
        Public = 1 << 4,

        InA = 1 << 5,
        OutA = 1 << 6,
        InB = 1 << 7,
        OutB = 1 << 8,
        InC = 1 << 9,
        OutC = 1 << 10,
        InD = 1 << 11,
        OutD = 1 << 12,
        InE = 1 << 13,
        OutE = 1 << 14,
        InF = 1 << 15,
        OutF = 1 << 16,
        InG = 1 << 17,
        OutG = 1 << 18,
        InH = 1 << 19,
        OutH = 1 << 20,
        InI = 1 << 21,
        OutI = 1 << 22,

        Variant1 = 1 << 23,
        Variant2 = 1 << 24,
        Variant3 = 1 << 25,
        Variant4 = 1 << 26,
        VariantAny = 1 << Variant1 | Variant2 | Variant3 | Variant4,


        // Combinations
        InA_OutE = InA | OutE,
        InAB_OutC = InA | OutC,
        InstantMandatory = Instant | Mandatory,
        InstantRecursive = Instant | Recursive,
        ParallelInstantRecursive = Parallel | Instant | Recursive,
        ParallelMandatory = Parallel | Mandatory,

        // Masks for In/Out flags
        InMask = InA | InB | InC | InD | InE | InF | InG | InH | InI,
        OutMask = OutA | OutB | OutC | OutD | OutE | OutF | OutG | OutH | OutI,
    }
}

public static class ChessPolicyUtility
{
    public static (int Dx, int Dy) Q1(this (int Dx, int Dy) delta) => (delta.Dx, delta.Dy);
    public static (int Dx, int Dy) Q2(this (int Dx, int Dy) delta) => (-delta.Dx, delta.Dy);
    public static (int Dx, int Dy) Q3(this (int Dx, int Dy) delta) => (-delta.Dx, -delta.Dy);
    public static (int Dx, int Dy) Q4(this (int Dx, int Dy) delta) => (delta.Dx, -delta.Dy);

}