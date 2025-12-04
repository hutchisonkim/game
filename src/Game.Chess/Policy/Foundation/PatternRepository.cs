using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;

namespace Game.Chess.Policy.Foundation;

/// <summary>
/// Repository for chess movement patterns.
/// Caches and provides deduplicated pattern definitions.
/// All pattern definitions are declarative - the engine derives rules from these patterns.
/// </summary>
public class PatternRepository
{
    private readonly SparkSession _spark;
    private DataFrame? _cachedPatterns;

    public PatternRepository(SparkSession spark)
    {
        _spark = spark;
    }

    /// <summary>
    /// Gets the patterns DataFrame, with automatic deduplication and caching.
    /// </summary>
    public DataFrame GetPatterns()
    {
        if (_cachedPatterns != null)
            return _cachedPatterns;

        var schema = new StructType(
        [
            new StructField("src_conditions", new IntegerType()),
            new StructField("dst_conditions", new IntegerType()),
            new StructField("delta_x", new IntegerType()),
            new StructField("delta_y", new IntegerType()),
            new StructField("sequence", new IntegerType()),
            new StructField("dst_effects", new IntegerType()),
        ]);

        var genericRows = PatternDefinitions.Values.Select(r => new GenericRow(
        [
            (int)r.SrcConditions,
            (int)r.DstConditions,
            r.Delta.X,
            r.Delta.Y,
            (int)r.Sequence,
            (int)r.DstEffects
        ])).ToList();

        _cachedPatterns = _spark.CreateDataFrame(genericRows, schema).DropDuplicates();
        return _cachedPatterns;
    }

    /// <summary>
    /// Clears the cached patterns, forcing a refresh on next GetPatterns() call.
    /// </summary>
    public void ClearCache()
    {
        _cachedPatterns = null;
    }
}

/// <summary>
/// Static definitions of all chess movement patterns.
/// These declarative rules define the emergent behavior of the chess engine.
/// </summary>
internal static class PatternDefinitions
{
    public static readonly (Piece SrcConditions, Piece DstConditions, (int X, int Y) Delta, Sequence Sequence, Piece DstEffects)[] Values =
    [
        //=====pawn=====
        // pawn forward (move-only)
        (Piece.Self | Piece.Pawn,     Piece.Empty,        (0, 1).Q1(), Sequence.OutA      | Sequence.VariantAny | Sequence.None                     | Sequence.Public, Piece.Empty),
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
}
