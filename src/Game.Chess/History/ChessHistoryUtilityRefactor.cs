using System;
using System.Collections.Generic;
using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.History;
using Game.Chess.Entity;
namespace Game.Chess.HistoryRefactor;

// ─────────────────────────────────────────────
// ENUMS
// ─────────────────────────────────────────────

// ─────────────────────────────────────────────
// MAIN IMPLEMENTATION
// ─────────────────────────────────────────────
public static class ChessActionCandidates
{
    public static DataFrame GetActionCandidates(
        SparkSession spark,
        DataFrame boardDf,
        bool includeTargetless = false,
        bool includeFriendlyFire = false)
    {
        var remainingPiecesDf = GetRemainingPieces<ChessPieceAttribute>(boardDf);
        var patternsDf = CreatePatterns<ChessPieceAttribute, MirrorBehavior, CaptureBehavior>(spark);
        var mirroredDf = ApplyMirrors<MirrorBehavior, ChessPieceAttribute, CaptureBehavior>(patternsDf, spark);
        var piecePatternsDf = remainingPiecesDf.Join(mirroredDf, nameof(ChessPieceAttribute));

        var appliedDf = ApplyPatterns<ChessPieceAttribute, CaptureBehavior>(piecePatternsDf);
        var joinedDf = JoinWithBoard<ChessPieceAttribute>(boardDf, appliedDf);
        var validDf = FilterValidMoves<ChessPieceAttribute, CaptureBehavior>(joinedDf, includeTargetless, includeFriendlyFire);

        return validDf.OrderBy("from_x", "from_y");
    }

    // ─────────────────────────────────────────────
    // STEP 1: Remaining pieces per type (typed)
    // ─────────────────────────────────────────────
    private static DataFrame GetRemainingPieces<TPiece>(DataFrame boardDf)
        where TPiece : Enum
    {
        return boardDf
            .Filter(Col(nameof(TPiece)).IsNotNull())
            .GroupBy(nameof(TPiece), "color")
            .Agg(CollectList(Struct("x", "y", "is_mint")).Alias("positions"));
    }

    // ─────────────────────────────────────────────
    // STEP 2: Create patterns (already generic)
    // ─────────────────────────────────────────────
    private static DataFrame CreatePatterns<TPiece, TMirror, TCapture>(SparkSession spark)
        where TPiece : Enum
        where TMirror : Enum
        where TCapture : Enum
    {
        var schema = new StructType(new[]
        {
            new StructField(nameof(TPiece), new StringType()),
            new StructField("dx", new IntegerType()),
            new StructField("dy", new IntegerType()),
            new StructField("repeats", new BooleanType()),
            new StructField(nameof(TMirror), new StringType()),
            new StructField(nameof(TCapture), new StringType())
        });

        var rows = new[]
        {
            new GenericRow([nameof(ChessPieceAttribute.Pawn),   0, 1,  false, nameof(MirrorBehavior.Vertical), nameof(CaptureBehavior.MoveOrReplace)]),
            new GenericRow([nameof(ChessPieceAttribute.Rook),   1, 0,  true,  nameof(MirrorBehavior.Both),     nameof(CaptureBehavior.MoveOrReplace)]),
            new GenericRow([nameof(ChessPieceAttribute.Bishop), 1, 1,  true,  nameof(MirrorBehavior.Both),     nameof(CaptureBehavior.MoveOrReplace)]),
            new GenericRow([nameof(ChessPieceAttribute.Queen),  1, 1,  true,  nameof(MirrorBehavior.Both),     nameof(CaptureBehavior.MoveOrReplace)]),
            new GenericRow([nameof(ChessPieceAttribute.Knight), 1, 2,  false, nameof(MirrorBehavior.Both),     nameof(CaptureBehavior.MoveOrReplace)]),
            new GenericRow([nameof(ChessPieceAttribute.King),   1, 0,  false, nameof(MirrorBehavior.Both),     nameof(CaptureBehavior.MoveOrReplace)])
        };

        return spark.CreateDataFrame(rows, schema);
    }

    // ─────────────────────────────────────────────
    // STEP 3: Apply mirrors (typed generics)
    // ─────────────────────────────────────────────
    private static DataFrame ApplyMirrors<TMirror, TPiece, TCapture>(DataFrame patternsDf, SparkSession spark)
        where TMirror : Enum
        where TPiece : Enum
        where TCapture : Enum
    {
        var mirrorsDf = spark.CreateDataFrame(
            [
                new GenericRow([ nameof(MirrorBehavior.Both),
                    new[] { Tuple.Create( 1,  1), Tuple.Create(-1,  1), Tuple.Create( 1, -1), Tuple.Create(-1, -1) } ]),
                new GenericRow([ nameof(MirrorBehavior.Horizontal),
                    new[] { Tuple.Create( 1, 1), Tuple.Create(-1, 1) } ]),
                new GenericRow([ nameof(MirrorBehavior.Vertical),
                    new[] { Tuple.Create( 1, 1), Tuple.Create( 1,-1) } ]),
                new GenericRow([ nameof(MirrorBehavior.None),
                    new[] { Tuple.Create( 1, 1) } ])
            ],
            new StructType(
            [
                new StructField(nameof(TMirror), new StringType()),
                new StructField("mirrors",
                    new ArrayType(new StructType(new[]
                    {
                        new StructField("mx", new IntegerType()),
                        new StructField("my", new IntegerType())
                    })))
            ])
        );

        return patternsDf
            .Join(mirrorsDf, nameof(TMirror))
            .WithColumn("mirror", Explode(Col("mirrors")))
            .Select(
                Col(nameof(TPiece)),
                (Col("dx") * Col("mirror.mx")).Alias("dx"),
                (Col("dy") * Col("mirror.my")).Alias("dy"),
                Col("repeats"),
                Col(nameof(TCapture))
            );
    }

    // ─────────────────────────────────────────────
    // STEP 4–5: Apply patterns and compute targets (typed)
    // ─────────────────────────────────────────────
    private static DataFrame ApplyPatterns<TPiece, TCapture>(DataFrame piecePatternsDf)
        where TPiece : Enum
        where TCapture : Enum
    {
        return piecePatternsDf
            .WithColumn("position", Explode(Col("positions")))
            .Select(
                Col(nameof(TPiece)),
                Col("color"),
                Col("position.x").Alias("from_x"),
                Col("position.y").Alias("from_y"),
                Col("dx"),
                Col("dy"),
                Col("repeats"),
                Col(nameof(TCapture)),
                When(Col("color") == nameof(ChessPieceAttribute.White), Lit(1)).Otherwise(Lit(-1)).Alias("fx"),
                When(Col("color") == nameof(ChessPieceAttribute.White), Lit(1)).Otherwise(Lit(-1)).Alias("fy")
            )
            .WithColumn("to_x", Col("from_x") + Col("dx") * Col("fx"))
            .WithColumn("to_y", Col("from_y") + Col("dy") * Col("fy"));
    }

    // ─────────────────────────────────────────────
    // STEP 6: Join with board (typed)
    // ─────────────────────────────────────────────
    private static DataFrame JoinWithBoard<TPiece>(DataFrame boardDf, DataFrame appliedDf)
        where TPiece : Enum
    {
        var boardCellsDf = boardDf
            .Select(
                Col("x").Alias("to_x"),
                Col("y").Alias("to_y"),
                Col(nameof(TPiece)).Alias("to_piece_type"),
                Col("color").Alias("to_piece_color")
            );

        return appliedDf.Join(boardCellsDf, new[] { "to_x", "to_y" }, "left");
    }

    // ─────────────────────────────────────────────
    // STEP 7: Filter valid moves (typed)
    // ─────────────────────────────────────────────
    private static DataFrame FilterValidMoves<TPiece, TCapture>(
        DataFrame joinedDf,
        bool includeTargetless,
        bool includeFriendlyFire)
        where TPiece : Enum
        where TCapture : Enum
    {
        var df = joinedDf
            .Filter((Col("to_x") >= 0) & (Col("to_x") < 8) & (Col("to_y") >= 0) & (Col("to_y") < 8))
            .Filter(!((Col("to_piece_color") == Col("color")) & Col("to_piece_type").IsNotNull()))
            .Filter(Col("to_piece_type").IsNull() |
                Col(nameof(TCapture)).IsIn(nameof(CaptureBehavior.Replace), nameof(CaptureBehavior.MoveOrReplace)));

        if (!includeTargetless)
        {
            df = df.Filter(Col("to_piece_type").IsNotNull() |
                           Col(nameof(TCapture)) != nameof(CaptureBehavior.Replace));
        }

        if (!includeFriendlyFire)
        {
            df = df.Filter((Col("to_piece_color") != Col("color")) | Col("to_piece_color").IsNull());
        }

        return df.Select(
            Col("from_x"),
            Col("from_y"),
            Col("to_x"),
            Col("to_y"),
            Col(nameof(TPiece)),
            Col("color"),
            Col(nameof(TCapture))
        );
    }

    // ─────────────────────────────────────────────
    // BOARD CREATOR
    // ─────────────────────────────────────────────
    public static DataFrame CreateBoardDataFrame(
        SparkSession spark,
        IEnumerable<(int x, int y, ChessPieceAttribute piece, ChessPieceAttribute color, bool isMint)> cells)
    {
        var rows = new List<GenericRow>();
        foreach (var c in cells)
        {
            rows.Add(new GenericRow(
            [
                c.x, c.y,
                c.piece == ChessPieceAttribute.None ? null : c.piece.ToString(),
                c.color == ChessPieceAttribute.None ? null : c.color.ToString(),
                c.isMint
            ]));
        }

        var schema = new StructType(new[]
        {
            new StructField("x", new IntegerType()),
            new StructField("y", new IntegerType()),
            new StructField(nameof(ChessPieceAttribute), new StringType()),
            new StructField("color", new StringType()),
            new StructField("is_mint", new BooleanType())
        });

        return spark.CreateDataFrame(rows, schema);
    }
}
