using Microsoft.Spark.Sql;
using Xunit;
using Game.Chess.HistoryB;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Provides domain-specific assertions for chess move validation.
/// Follows xUnit best practices for custom assertions.
/// </summary>
public static class MoveAssertions
{
    /// <summary>
    /// Asserts that a move to the specified destination exists in the collection.
    /// </summary>
    /// <param name="moves">The collection of moves to check</param>
    /// <param name="x">Destination x-coordinate</param>
    /// <param name="y">Destination y-coordinate</param>
    public static void HasMoveTo(IEnumerable<Row> moves, int x, int y)
    {
        var move = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == x && 
            r.GetAs<int>("dst_y") == y);
        
        Assert.NotNull(move);
    }

    /// <summary>
    /// Asserts that a move to the specified destination exists in the collection.
    /// Tuple overload for convenience.
    /// </summary>
    public static void HasMoveTo(IEnumerable<Row> moves, (int X, int Y) destination)
    {
        HasMoveTo(moves, destination.X, destination.Y);
    }

    /// <summary>
    /// Asserts that NO move to the specified destination exists in the collection.
    /// </summary>
    public static void HasNoMoveTo(IEnumerable<Row> moves, int x, int y)
    {
        var move = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == x && 
            r.GetAs<int>("dst_y") == y);
        
        Assert.Null(move);
    }

    /// <summary>
    /// Asserts that NO move to the specified destination exists in the collection.
    /// Tuple overload for convenience.
    /// </summary>
    public static void HasNoMoveTo(IEnumerable<Row> moves, (int X, int Y) destination)
    {
        HasNoMoveTo(moves, destination.X, destination.Y);
    }

    /// <summary>
    /// Asserts that the move collection has exactly the expected count.
    /// </summary>
    public static void HasMoveCount(IEnumerable<Row> moves, int expected)
    {
        var actual = moves.Count();
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Asserts that the move collection has at least the expected count.
    /// Useful when exact count may vary but minimum is known.
    /// </summary>
    public static void HasMinimumMoveCount(IEnumerable<Row> moves, int minimum)
    {
        var actual = moves.Count();
        Assert.True(actual >= minimum, 
            $"Expected at least {minimum} moves, but found {actual}");
    }

    /// <summary>
    /// Asserts that the move collection has at most the expected count.
    /// </summary>
    public static void HasMaximumMoveCount(IEnumerable<Row> moves, int maximum)
    {
        var actual = moves.Count();
        Assert.True(actual <= maximum, 
            $"Expected at most {maximum} moves, but found {actual}");
    }

    /// <summary>
    /// Asserts that all moves in the collection satisfy the given predicate.
    /// </summary>
    public static void AllMovesMatch(IEnumerable<Row> moves, Func<Row, bool> predicate, string? because = null)
    {
        var failedMoves = moves.Where(m => !predicate(m)).ToList();
        
        if (failedMoves.Any())
        {
            var message = because != null 
                ? $"Expected all moves to match predicate because {because}, but {failedMoves.Count} did not"
                : $"Expected all moves to match predicate, but {failedMoves.Count} did not";
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// Asserts that at least one move in the collection satisfies the given predicate.
    /// </summary>
    public static void AnyMoveMatches(IEnumerable<Row> moves, Func<Row, bool> predicate, string? because = null)
    {
        var hasMatch = moves.Any(predicate);
        
        if (!hasMatch)
        {
            var message = because != null 
                ? $"Expected at least one move to match predicate because {because}"
                : "Expected at least one move to match predicate";
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// Asserts that the move collection contains moves to all specified destinations.
    /// </summary>
    public static void HasMovesToAll(IEnumerable<Row> moves, params (int X, int Y)[] destinations)
    {
        foreach (var dest in destinations)
        {
            HasMoveTo(moves, dest);
        }
    }

    /// <summary>
    /// Asserts that the move collection contains NO moves to any of the specified destinations.
    /// </summary>
    public static void HasNoMovesToAny(IEnumerable<Row> moves, params (int X, int Y)[] destinations)
    {
        foreach (var dest in destinations)
        {
            HasNoMoveTo(moves, dest);
        }
    }

    /// <summary>
    /// Asserts that all moves have the specified sequence flags set.
    /// </summary>
    public static void AllMovesHaveSequence(IEnumerable<Row> moves, ChessPolicy.Sequence expectedFlags)
    {
        int flagsInt = (int)expectedFlags;
        
        AllMovesMatch(moves, 
            r => (r.GetAs<int>("sequence") & flagsInt) != 0,
            $"all moves should have sequence flags {expectedFlags}");
    }

    /// <summary>
    /// Asserts that all moves have non-zero sequence values.
    /// </summary>
    public static void AllMovesHaveNonZeroSequence(IEnumerable<Row> moves)
    {
        AllMovesMatch(moves, 
            r => r.GetAs<int>("sequence") != 0,
            "all moves should have sequence flags set");
    }

    /// <summary>
    /// Asserts that the DataFrame contains the specified column.
    /// </summary>
    public static void HasColumn(DataFrame df, string columnName)
    {
        Assert.Contains(columnName, df.Columns());
    }

    /// <summary>
    /// Asserts that the DataFrame row count matches expectations.
    /// </summary>
    public static void HasRowCount(DataFrame df, long expected)
    {
        var actual = df.Count();
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Asserts that the DataFrame has at least the minimum row count.
    /// </summary>
    public static void HasMinimumRowCount(DataFrame df, long minimum, string? because = null)
    {
        var actual = df.Count();
        var message = because != null
            ? $"Expected at least {minimum} rows because {because}, but found {actual}"
            : $"Expected at least {minimum} rows, but found {actual}";
        
        Assert.True(actual >= minimum, message);
    }
}
