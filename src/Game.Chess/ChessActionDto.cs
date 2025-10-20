using System.Collections.Generic;
using System.Linq;
using Microsoft.Spark.Sql.Types;
using Microsoft.Spark.Sql;

namespace Game.Chess.Policy
{
    // Chess-specific flattened DTO for Spark
    public sealed record ChessActionDto(
        int FromRow,
        int FromCol,
        int ToRow,
        int ToCol,
        int PieceColorFlag,
        int PieceTypeFlag,
        int PatternCaptures,
        int DestColorFlag
    );

    public static class ChessActionDtoExtensions
    {
        public static StructType Schema() => new StructType(new[]
        {
            new StructField("FromRow", new IntegerType()),
            new StructField("FromCol", new IntegerType()),
            new StructField("ToRow", new IntegerType()),
            new StructField("ToCol", new IntegerType()),
            new StructField("PieceColorFlag", new IntegerType()),
            new StructField("PieceTypeFlag", new IntegerType()),
            new StructField("PatternCaptures", new IntegerType()),
            new StructField("DestColorFlag", new IntegerType())
        });

        public static GenericRow[] ToRows(this IEnumerable<ChessActionDto> dtos)
        {
            return dtos.Select(d => new GenericRow(new object[] { d.FromRow, d.FromCol, d.ToRow, d.ToCol, d.PieceColorFlag, d.PieceTypeFlag, d.PatternCaptures, d.DestColorFlag })).ToArray();
        }
    }
}
