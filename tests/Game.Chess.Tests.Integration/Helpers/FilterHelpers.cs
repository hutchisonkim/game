using Microsoft.Spark.Sql;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Provides fluent API for building type-safe DataFrame filter expressions.
/// Eliminates manual string construction and reduces bitwise operation errors.
/// </summary>
public class FilterBuilder
{
    private readonly List<string> _conditions = new();

    /// <summary>
    /// Filters for patterns matching the specified piece type.
    /// Equivalent to: (src_conditions & {pieceType}) != 0
    /// </summary>
    public FilterBuilder ForPieceType(Piece pieceType)
    {
        int pieceInt = (int)pieceType;
        _conditions.Add($"(src_conditions & {pieceInt}) != 0");
        return this;
    }

    /// <summary>
    /// Filters for patterns with the specified sequence flag(s) set.
    /// Equivalent to: (sequence & {sequenceFlags}) != 0
    /// </summary>
    public FilterBuilder WithSequence(Sequence sequenceFlags)
    {
        int seqInt = (int)sequenceFlags;
        _conditions.Add($"(sequence & {seqInt}) != 0");
        return this;
    }

    /// <summary>
    /// Filters for patterns with sequence flags exactly matching the specified value.
    /// Equivalent to: (sequence & {mask}) = {expectedValue}
    /// </summary>
    public FilterBuilder WithSequenceExact(Sequence mask, Sequence expectedValue)
    {
        int maskInt = (int)mask;
        int valueInt = (int)expectedValue;
        _conditions.Add($"(sequence & {maskInt}) = {valueInt}");
        return this;
    }

    /// <summary>
    /// Filters for patterns where the sequence flags do NOT include the specified flag(s).
    /// Equivalent to: (sequence & {mask}) = 0
    /// </summary>
    public FilterBuilder WithoutSequence(Sequence sequenceFlags)
    {
        int seqInt = (int)sequenceFlags;
        _conditions.Add($"(sequence & {seqInt}) = 0");
        return this;
    }

    /// <summary>
    /// Filters for patterns with the specified destination condition.
    /// Equivalent to: (dst_conditions & {condition}) != 0
    /// </summary>
    public FilterBuilder WithDestinationCondition(Piece condition)
    {
        int condInt = (int)condition;
        _conditions.Add($"(dst_conditions & {condInt}) != 0");
        return this;
    }

    /// <summary>
    /// Filters for patterns with the specified source condition.
    /// Equivalent to: (src_conditions & {condition}) != 0
    /// </summary>
    public FilterBuilder WithSourceCondition(Piece condition)
    {
        int condInt = (int)condition;
        _conditions.Add($"(src_conditions & {condInt}) != 0");
        return this;
    }

    /// <summary>
    /// Adds a custom filter condition (for advanced scenarios not covered by fluent API).
    /// </summary>
    public FilterBuilder WithCustom(string condition)
    {
        _conditions.Add($"({condition})");
        return this;
    }

    /// <summary>
    /// Builds the final filter string by combining all conditions with AND.
    /// </summary>
    public string Build()
    {
        return _conditions.Count == 0 ? "1=1" : string.Join(" AND ", _conditions);
    }

    /// <summary>
    /// Applies the built filter to the DataFrame and returns the filtered result.
    /// </summary>
    public DataFrame ApplyTo(DataFrame df)
    {
        return df.Filter(Build());
    }
}

/// <summary>
/// Static helper methods for common filter patterns.
/// </summary>
public static class FilterHelpers
{
    /// <summary>
    /// Creates a new FilterBuilder instance.
    /// </summary>
    public static FilterBuilder CreateFilter() => new FilterBuilder();

    /// <summary>
    /// Filters DataFrame for patterns of a specific piece type with Public sequence flag.
    /// Common pattern used in most basic movement tests.
    /// </summary>
    public static DataFrame ForPieceTypeWithPublic(this DataFrame patternsDf, Piece pieceType)
    {
        return CreateFilter()
            .ForPieceType(pieceType)
            .WithSequence(Sequence.Public)
            .ApplyTo(patternsDf);
    }

    /// <summary>
    /// Filters DataFrame for entry patterns (Out flag, InstantRecursive, no In flags).
    /// Used for sliding pieces like Rook and Bishop.
    /// </summary>
    public static DataFrame ForEntryPatterns(this DataFrame patternsDf, Piece pieceType, Sequence outFlag)
    {
        return CreateFilter()
            .ForPieceType(pieceType)
            .WithSequence(outFlag)
            .WithSequenceExact(Sequence.InstantRecursive, Sequence.InstantRecursive)
            .WithoutSequence(Sequence.InMask)
            .ApplyTo(patternsDf);
    }

    /// <summary>
    /// Filters DataFrame for continuation patterns (In flag, Public).
    /// Used for sliding pieces to continue movement sequences.
    /// </summary>
    public static DataFrame ForContinuationPatterns(this DataFrame patternsDf, Piece pieceType, Sequence inFlag)
    {
        return CreateFilter()
            .ForPieceType(pieceType)
            .WithSequence(inFlag)
            .WithSequence(Sequence.Public)
            .ApplyTo(patternsDf);
    }
}
