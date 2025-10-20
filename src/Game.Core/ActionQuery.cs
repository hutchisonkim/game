using System.Collections.Generic;
using System.Linq;
using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;

namespace Game.Core;

/// <summary>
/// Domain-agnostic Spark wrapper: build a DataFrame from a provided schema and rows and filter using SQL predicates.
/// The caller is responsible for mapping domain objects into arrays/GenericRow and interpreting results.
/// </summary>
public sealed class ActionQuerySpark
{
    private readonly SparkSession _spark;
    private DataFrame _df;

    public ActionQuerySpark(SparkSession spark, StructType schema, IEnumerable<GenericRow> rows)
    {
        _spark = spark ?? throw new System.ArgumentNullException(nameof(spark));
        schema = schema ?? throw new System.ArgumentNullException(nameof(schema));
        rows = rows ?? Enumerable.Empty<GenericRow>();
        _df = _spark.CreateDataFrame(rows.ToArray(), schema);
    }

    // Apply a SQL expression predicate (Spark SQL syntax)
    public ActionQuerySpark WhereSql(string sqlPredicate)
    {
        if (string.IsNullOrWhiteSpace(sqlPredicate)) return this;
        _df = _df.Filter(sqlPredicate);
        return this;
    }

    // Collect rows back to the driver as Row[]
    public Row[] CollectRows() => _df.Collect().ToArray();
}
