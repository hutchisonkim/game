using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;
using static Microsoft.Spark.Sql.Functions;

namespace Game.Chess.HistoryRefactor;

public static class ChessPolicyUtility
{
    public static DataFrame GetActionCandidates(
        SparkSession spark,
        Board currentBoard,
        Piece[] specificFactions,
        bool includeTargetless = false,
        bool includeFriendlyFire = false)
    {
        // first get the remaining pieces on the board (board piece table)
        // for each piece:
        // - convert the board faction (black/white) attributes to local piece attributes (ally/foe)
        // - join the board piece table with the patterns table by matching board_table's piece attributes with pattern_table's src_conditions (this)
        // - for each matched pattern:
        // -- calculate the target position by applying the pattern delta to the piece position
        // -- lookup the target position on the board to get the target piece attributes
        // -- filter by matching target piece attributes with pattern dst_conditions
        // -- check for Pattern.Out*** and matching Pattern.In*** flags to determine possible next actions

        // first, convert the board to a DataFrame. the board has a Cell which is a 2D array of Pieces which are ints



        DataFrame piecesDf = PieceFactory.GetPieces(spark, currentBoard);//contains faction-specific values like Black/White, with global positions like (x,y)
        DataFrame perspectivesDf = GetPerspectives(piecesDf, specificFactions)//contains faction-generic values like Self/Ally/Foe, with global positions like (x,y)
            .WithColumn("perspective_id",
                Sha2(ConcatWs("_",
                    Col("x").Cast("string"),
                    Col("y").Cast("string"),
                    Col("piece").Cast("string")),
                256));

        DataFrame patternsDf = PatternFactory.GetPatterns(spark)//contains faction-generic values like Self/Ally/Foe, with local deltas like (delta_x, delta_y)
            .WithColumn("pattern_id",
                Sha2(ConcatWs("_",
                    Col("src_conditions").Cast("string"),
                    Col("dst_conditions").Cast("string"),
                    Col("delta_x").Cast("string"),
                    Col("delta_y").Cast("string"),
                    Col("sequence").Cast("string"),
                    Col("dst_effects").Cast("string")),
                256));

        DataFrame candidatesDf = perspectivesDf
            .WithColumnRenamed("piece", "src_piece")
            .WithColumnRenamed("generic_piece", "src_generic_piece")
            .WithColumnRenamed("x", "src_x")
            .WithColumnRenamed("y", "src_y")
            .CrossJoin(patternsDf)
            .WithColumn("dst_x", Col("src_x") + Col("delta_x"))
            .WithColumn("dst_y", Col("src_y") + Col("delta_y"))
            .Drop("delta_x", "delta_y")
            .Join(perspectivesDf, (Col("dst_x") == Col("x")).And(Col("dst_y") == Col("y")), "left_outer")
            .Na().Fill((int)Piece.OutOfBounds, ["generic_piece"])
            .WithColumnRenamed("generic_piece", "dst_generic_piece")
            .Drop("x", "y", "piece")
            .Filter(Col("src_generic_piece").BitwiseAND(Col("src_conditions")).NotEqual(Lit(0)))
            .Filter(Col("dst_generic_piece").BitwiseAND(Col("dst_conditions")).NotEqual(Lit(0)))
            .Drop("src_conditions", "dst_conditions");

        // --- Compute next perspectives directly from candidates ---
        DataFrame nextPerspectivesDf =
            perspectivesDf
                // Each perspective starts as its previous value
                .WithColumn("next_generic_piece", Col("generic_piece"))
                // Overwrite where a candidate applies
                .Join(
                    candidatesDf
                        .Select(
                            Col("pattern_id"),
                            Col("perspective_id"),
                            Col("src_x").Alias("x"),
                            Col("src_y").Alias("y"),
                            Lit((int)Piece.Empty).Alias("override_piece"))
                        .Union(
                            candidatesDf.Select(
                                Col("pattern_id"),
                                Col("perspective_id"),
                                Col("dst_x").Alias("x"),
                                Col("dst_y").Alias("y"),
                                Col("src_generic_piece").BitwiseAND(Col("dst_effects")).Alias("override_piece"))
                        ),
                    ["x", "y"],
                    "left_outer"
                )
                .WithColumn(
                    "next_generic_piece",
                    When(Col("override_piece").IsNotNull(), Col("override_piece"))
                        .Otherwise(Col("generic_piece"))
                )
                .Drop("override_piece")
                .WithColumn("next_perspective_id",
                    Sha2(ConcatWs("_",
                        Col("x").Cast("string"),
                        Col("y").Cast("string"),
                        Col("next_generic_piece").Cast("string"),
                        Col("pattern_id").Cast("string"),
                        Col("perspective_id").Cast("string")),
                    256));

        // --- Generate next candidates (future transitions) ---
        DataFrame nextCandidatesDf =
            nextPerspectivesDf
                .WithColumnRenamed("next_generic_piece", "src_generic_piece")
                .WithColumnRenamed("next_perspective_id", "perspective_id")
                .WithColumnRenamed("x", "src_x")
                .WithColumnRenamed("y", "src_y")
                .CrossJoin(patternsDf)
                .WithColumn("dst_x", Col("src_x") + Col("delta_x"))
                .WithColumn("dst_y", Col("src_y") + Col("delta_y"))
                .Join(nextPerspectivesDf,
                    (Col("dst_x") == Col("x")).And(Col("dst_y") == Col("y")),
                    "left_outer")
                .Na().Fill((int)Piece.OutOfBounds, ["next_generic_piece"])
                .WithColumnRenamed("next_generic_piece", "dst_generic_piece")
                .Drop("x", "y")
                .Filter(Col("src_generic_piece").BitwiseAND(Col("src_conditions")).NotEqual(Lit(0)))
                .Filter(Col("dst_generic_piece").BitwiseAND(Col("dst_conditions")).NotEqual(Lit(0)))
                .Drop("src_conditions", "dst_conditions");


        //next, we need to compute the timeline sequences based on the patterns
        //this means possibly expanding each pattern into multiple sequences based on the Pattern.Sequence flags

        //what's more, for checks and preventing moves that would leave the king in check, we need to simulate forward
        // this means running all steps again, but as if it were the next turn, and checking if the king is threatened
        // if so, we need to mark those moves as illegal, along with the entire sequence leading up to it


        throw new NotImplementedException();
    }


    public static DataFrame GetPerspectives(
        DataFrame piecesDf,
        Piece[] specificFactions)
    {
        // piecesDf: contains (x, y, piece)
        DataFrame perspectivesDf = piecesDf
            .WithColumnRenamed("x", "perspective_x")
            .WithColumnRenamed("y", "perspective_y")
            .WithColumnRenamed("piece", "perspective_piece")
            .CrossJoin(piecesDf);

        // Build Columns for each faction dynamically
        var allyConditions = specificFactions
            .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0))
                         .And(Col("perspective_piece").BitwiseAND((int)f).NotEqual(Lit(0))))
            .ToArray();

        // Aggregate with OR to combine
        Column pieceAndPerspectiveShareFaction = allyConditions.Aggregate((acc, c) => acc.Or(c));

        // Similarly for pieceHasFaction
        var pieceFactionConditions = specificFactions
            .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            .ToArray();

        Column pieceHasFaction = pieceFactionConditions.Aggregate((acc, c) => acc.Or(c));

        // compute faction mask once
        int factionMask = specificFactions.Aggregate(0, (acc, f) => acc | (int)f);

        // Now the rest is unchanged
        Column genericPieceCol =
            When(
                (Col("x") == Col("perspective_x")).And(Col("y") == Col("perspective_y")),
                Col("piece").BitwiseAND(Lit(~factionMask)).BitwiseOR(Lit((int)Piece.Self))
            )
            .When(pieceAndPerspectiveShareFaction,
                Col("piece").BitwiseAND(Lit(~factionMask)).BitwiseOR(Lit((int)Piece.Ally))
            )
            .When(pieceHasFaction.And(Not(pieceAndPerspectiveShareFaction)),
                Col("piece").BitwiseAND(Lit(~factionMask)).BitwiseOR(Lit((int)Piece.Foe))
            )
            .Otherwise(Col("piece"));

        perspectivesDf = perspectivesDf.WithColumn("generic_piece", genericPieceCol);

        return perspectivesDf;
    }


    private static class PieceFactory
    {
        public static DataFrame GetPieces(SparkSession spark, Board board)
        {

            StructType boardSchema = new(
            [
                new StructField("x", new IntegerType()),
                new StructField("y", new IntegerType()),
                new StructField("piece", new IntegerType())
            ]);

            List<GenericRow> boardData = [
                .. Enumerable.Range(0, board.Width)
                .SelectMany(x => Enumerable.Range(0, board.Height)
                .Select(y => new GenericRow([x, y, (int)board.Cell[x, y]])))
            ];

            DataFrame boardDf = spark.CreateDataFrame(boardData, boardSchema);

            return boardDf;
        }
    }


    private static readonly (Piece SrcConditions, Piece DstConditions, (int X, int Y) Delta, Sequence Sequence, Piece DstEffects)[] values =
    [
        //=====pawn=====
        // pawn forward (move-only)
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q1(), Sequence.OutA      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        // pawn forward (post, do nothing)
        (Piece.Self | Piece.MintPawn, Piece.Empty,        (0, 0).Q1(), Sequence.InA       | Sequence.VariantAny | Sequence.Instant                  | Sequence.Public, ~Piece.Mint),
        // pawn forward (post, move-only)
        (Piece.Self | Piece.MintPawn, Piece.Empty,        (0, 1).Q1(), Sequence.InA       | Sequence.VariantAny | Sequence.Instant                  | Sequence.Public, ~Piece.Mint | Piece.Passing),
        // pawn forward (capture-only)
        (Piece.Self | Piece.Pawn,     Piece.Foe,          (1, 1).Q1(), Sequence.OutB      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.Pawn,     Piece.Foe,          (1, 1).Q2(), Sequence.OutB      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        // pawn promotion trigger
        (Piece.Self | Piece.Pawn,     Piece.OutOfBounds,  (0, 1).Q1(), Sequence.InAB_OutC | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.None,   Piece.None),
        // pawn promotions
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Knight),
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Rook),
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Bishop),
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q3(), Sequence.InC       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, ~Piece.Pawn | Piece.Queen),
        //=====bishop=====
        // bishop (pre, move-only)
        (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q1(), Sequence.OutF      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q2(), Sequence.OutF      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q3(), Sequence.OutF      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.Empty,        (1, 1).Q4(), Sequence.OutF      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        // bishop (move or capture)
        (Piece.Self | Piece.Bishop,   Piece.EmptyOrFoe,   (1, 1).Q1(), Sequence.InF       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.EmptyOrFoe,   (1, 1).Q2(), Sequence.InF       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.EmptyOrFoe,   (1, 1).Q3(), Sequence.InF       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Bishop,   Piece.EmptyOrFoe,   (1, 1).Q4(), Sequence.InF       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
        //=====queen=====
        // queen as bishop (pre, move-only)
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q1(), Sequence.OutG      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q2(), Sequence.OutG      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q3(), Sequence.OutG      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 1).Q4(), Sequence.OutG      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        // queen as bishop (move or capture)
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 1).Q1(), Sequence.InG       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 1).Q2(), Sequence.InG       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 1).Q3(), Sequence.InG       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 1).Q4(), Sequence.InG       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
        // queen as rook (pre, move-only)
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q1(), Sequence.OutH      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (1, 0).Q3(), Sequence.OutH      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q1(), Sequence.OutH      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        (Piece.Self | Piece.Queen,    Piece.Empty,        (0, 1).Q3(), Sequence.OutH      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   Piece.None),
        // queen as rook (move or capture)
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 0).Q1(), Sequence.InH       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (1, 0).Q3(), Sequence.InH       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (0, 1).Q1(), Sequence.InH       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Queen,    Piece.EmptyOrFoe,   (0, 1).Q3(), Sequence.InH       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, Piece.None),
        //=====rook=====
        // rook (pre, move-only)
        (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q1(), Sequence.OutI      | Sequence.Variant1   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.Empty,        (1, 0).Q3(), Sequence.OutI      | Sequence.Variant2   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q1(), Sequence.OutI      | Sequence.Variant3   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.Empty,        (0, 1).Q3(), Sequence.OutI      | Sequence.Variant4   | Sequence.InstantRecursive         | Sequence.None,   ~Piece.Mint),
        // rook (move or capture)
        (Piece.Self | Piece.Rook,     Piece.EmptyOrFoe,   (1, 0).Q1(), Sequence.InI       | Sequence.Variant1   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.EmptyOrFoe,   (1, 0).Q3(), Sequence.InI       | Sequence.Variant2   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.EmptyOrFoe,   (0, 1).Q1(), Sequence.InI       | Sequence.Variant3   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.Rook,     Piece.EmptyOrFoe,   (0, 1).Q3(), Sequence.InI       | Sequence.Variant4   | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        //=====knight=====
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (2, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (2, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (2, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (2, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (1, 2).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (1, 2).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (1, 2).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Knight,   Piece.EmptyOrFoe,   (1, 2).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        //=====king=====
        // king as rook
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 0).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 0).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (0, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (0, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        // king as bishop
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 1).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 1).Q2(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 1).Q3(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.King,     Piece.EmptyOrFoe,   (1, 1).Q4(), Sequence.None      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, ~Piece.Mint),
        // castling moves (left)
        (Piece.Self | Piece.MintRook, Piece.Empty,        (0, 1).Q1(), Sequence.OutD      | Sequence.Variant1   | Sequence.ParallelInstantRecursive | Sequence.Public, Piece.None),
        (Piece.Self | Piece.MintRook, Piece.AllyKing,     (0, 1).Q1(), Sequence.InD       | Sequence.Variant1   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
        (Piece.Self | Piece.MintKing, Piece.EmptyAndSafe, (0, 1).Q3(), Sequence.OutD      | Sequence.Variant1   | Sequence.ParallelInstantRecursive | Sequence.Public, Piece.None),
        (Piece.Self | Piece.MintKing, Piece.AllyRook,     (0, 1).Q3(), Sequence.InD       | Sequence.Variant1   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
        // castling moves (right)
        (Piece.Self | Piece.MintRook, Piece.Empty,        (0, 1).Q3(), Sequence.OutD      | Sequence.Variant2   | Sequence.ParallelInstantRecursive | Sequence.None,   Piece.None),
        (Piece.Self | Piece.MintRook, Piece.AllyKing,     (0, 1).Q3(), Sequence.InD       | Sequence.Variant2   | Sequence.ParallelMandatory        | Sequence.None,   ~Piece.Mint),
        (Piece.Self | Piece.MintKing, Piece.EmptyAndSafe, (0, 1).Q1(), Sequence.OutD      | Sequence.Variant2   | Sequence.ParallelInstantRecursive | Sequence.None,   Piece.None),
        (Piece.Self | Piece.MintKing, Piece.AllyRook,     (0, 1).Q1(), Sequence.InD       | Sequence.Variant2   | Sequence.ParallelMandatory        | Sequence.Public, ~Piece.Mint),
        // en passant (1. capture sideways)
        (Piece.Self | Piece.Pawn,     Piece.PassingFoe,   (1, 0).Q1(), Sequence.OutE      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        (Piece.Self | Piece.Pawn,     Piece.PassingFoe,   (1, 0).Q3(), Sequence.OutE      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.None),
        // en passant (2. move forward)
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q1(), Sequence.InE       | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.Public, Piece.None),
        // en passant (reset passing flag)
        (Piece.Self | Piece.Passing,  Piece.None,         (0, 0).Q1(), Sequence.None      | Sequence.VariantAny | Sequence.InstantMandatory         | Sequence.None,   ~Piece.Passing),
    ];
    
    public static class PatternFactory
    {
        public static DataFrame GetPatterns(SparkSession spark)
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

            var rows = values;

            var genericRows = rows.Select(r => new GenericRow(
            [
                (int)r.SrcConditions,
                (int)r.DstConditions,
                r.Delta.X,
                r.Delta.Y,
                (int)r.Sequence,
                (int)r.DstEffects,
            ])).ToList();

            return spark.CreateDataFrame(genericRows, schema);
        }
    }

    public readonly record struct Board(int Width, int Height, Piece[,] Cell)
    {
        public void Initialize(Piece pieceOverride = Piece.None)
        {
            var initial = CreateInitialBoard(pieceOverride);
            for (int i = 0; i < initial.GetLength(0); i++)
            {
                for (int j = 0; j < initial.GetLength(1); j++)
                {
                    Cell[i, j] = initial[i, j];
                }
            }
        }

        public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public static Board Default => new(8, 8, new Piece[8, 8]);


        public static Piece[,] CreateInitialBoard(Piece pieceAttributeOverride = Piece.None)
        {
            var board = Default.Cell;

            bool hasOverride = pieceAttributeOverride != Piece.None;
            Piece pieceAttr;

            // Pawns
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.Pawn;
            for (int x = 0; x < 8; x++)
            {
                board[x, 1] = Piece.White | Piece.Mint | pieceAttr;
                board[x, 6] = Piece.Black | Piece.Mint | pieceAttr;
            }

            // Rooks
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.Rook;
            board[0, 0] = board[7, 0] = Piece.White | Piece.Mint | pieceAttr;
            board[0, 7] = board[7, 7] = Piece.Black | Piece.Mint | pieceAttr;

            // Knights
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.Knight;
            board[1, 0] = board[6, 0] = Piece.White | Piece.Mint | pieceAttr;
            board[1, 7] = board[6, 7] = Piece.Black | Piece.Mint | pieceAttr;

            // Bishops
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.Bishop;
            board[2, 0] = board[5, 0] = Piece.White | Piece.Mint | pieceAttr;
            board[2, 7] = board[5, 7] = Piece.Black | Piece.Mint | pieceAttr;

            // Queens
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.Queen;
            board[3, 0] = Piece.White | Piece.Mint | pieceAttr;
            board[3, 7] = Piece.Black | Piece.Mint | pieceAttr;

            // Kings
            pieceAttr = hasOverride ? pieceAttributeOverride : Piece.King;
            board[4, 0] = Piece.White | Piece.Mint | pieceAttr;
            board[4, 7] = Piece.Black | Piece.Mint | pieceAttr;

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


        MintPawn = Mint | Pawn,
        MintRook = Mint | Rook,
        MintKing = Mint | King,

        AllyRook = Ally | Rook,
        AllyKing = Ally | King,

        Any = Ally | Foe,

        EmptyOrFoe = Empty | Foe,
        Empty = ~Any,

        EmptyAndSafe = Empty & ~Threatened,

        PassingFoe = Passing | Foe,

    }
    
    // ─────────────────────────────────────────────
    // STEP 2: Patterns
    // ─────────────────────────────────────────────
    
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

    }

    public static (int Dx, int Dy) Q1(this (int Dx, int Dy) delta) => (delta.Dx, delta.Dy);
    public static (int Dx, int Dy) Q2(this (int Dx, int Dy) delta) => (-delta.Dx, delta.Dy);
    public static (int Dx, int Dy) Q3(this (int Dx, int Dy) delta) => (-delta.Dx, -delta.Dy);
    public static (int Dx, int Dy) Q4(this (int Dx, int Dy) delta) => (delta.Dx, -delta.Dy);

}
