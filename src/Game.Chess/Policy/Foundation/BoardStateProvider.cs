using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;

namespace Game.Chess.Policy.Foundation;

/// <summary>
/// Provides board state as DataFrame representations.
/// This is the foundational layer that converts Board objects into Spark DataFrames.
/// </summary>
public class BoardStateProvider
{
    private readonly SparkSession _spark;

    public BoardStateProvider(SparkSession spark)
    {
        _spark = spark;
    }

    /// <summary>
    /// Converts a Board into a DataFrame of pieces.
    /// Each row represents one cell on the board with its (x, y, piece) values.
    /// </summary>
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
