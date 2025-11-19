using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Microsoft.Spark.Sql.Types;

namespace Game.Chess.Tests.Integration;

[Trait("Feature", "ChessSparkPolicy")]
[Collection("Spark collection")]
public class ChessSparkPolicyTests
{
    private SparkSession? _spark;
    private ChessPolicy? _policy;

    private SparkSession Spark
    {
        get
        {
            if (_spark != null) return _spark;

            try
            {
                var backendPort = Environment.GetEnvironmentVariable("DOTNETBACKEND_PORT");
                var workerPort = Environment.GetEnvironmentVariable("PYTHON_WORKER_FACTORY_PORT");

                _spark = SparkSession
                    .Builder()
                    .AppName("ChessPolicyTests")
                    // .Config("spark.sql.shuffle.partitions", "1") // small local jobs
                    .Config("spark.dotnet.backend.port", backendPort)
                    .Config("spark.dotnet.worker.factory.port", workerPort)
                    .GetOrCreate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create Spark session: {ex}");
                throw;
            }

            return _spark!;
        }
    }

    private ChessPolicy Policy => _policy ??= new ChessPolicy(Spark);

    [Fact]
    public void Spark_BasicDataFrameOperations_Work()
    {
        Console.WriteLine("[DEBUG] Spark_BasicDataFrameOperations_Work started");

        // Arrange
        var spark = SparkSession
            .Builder()
            .AppName("BasicSparkTest")
            .GetOrCreate();

        // A trivial in-memory dataset
        var data = new List<GenericRow>
        {
            new GenericRow(new object[] { 1, "a" }),
            new GenericRow(new object[] { 2, "b" }),
            new GenericRow(new object[] { 3, "c" })
        };

        // Schema with simple Spark primitives
        var schema = new StructType(new[]
        {
            new StructField("id", new IntegerType()),
            new StructField("value", new StringType())
        });

        // Act
        DataFrame df = spark.CreateDataFrame(data, schema);

        long count = df.Count();
        var dfSchema = df.Schema();

        // Assert
        Assert.Equal(3, count);
        Assert.Contains(dfSchema.Fields, f => f.Name == "id");
        Assert.Contains(dfSchema.Fields, f => f.Name == "value");
    }

    [Fact]
    public void BoardInitialization_CreatesCorrectStartingPositions()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Pawns
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 1]);
            Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn, board.Cell[x, 6]);
        }

        // Rooks
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[0, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook, board.Cell[7, 7]);

        // Knights
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[1, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight, board.Cell[6, 7]);

        // Bishops
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[2, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop, board.Cell[5, 7]);

        // Queens
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen, board.Cell[3, 7]);

        // Kings
        Assert.Equal(ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 0]);
        Assert.Equal(ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King, board.Cell[4, 7]);
    }

    [Fact]
    public void PieceFactory_ReturnsCorrectNumberOfRows()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var piecesDf = new ChessPolicy.PieceFactory(Spark).GetPieces(board);

        Assert.Equal(64, piecesDf.Count());
        var schema = piecesDf.Schema();
        Assert.Contains(schema.Fields, f => f.Name == "x");
        Assert.Contains(schema.Fields, f => f.Name == "y");
        Assert.Contains(schema.Fields, f => f.Name == "piece");
    }

    [Fact]
    public void GetPerspectives_AssignsGenericFlagsCorrectly()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);

        Assert.Contains("generic_piece", perspectivesDf.Columns());
        Assert.Contains("perspective_id", perspectivesDf.Columns());

        // check at least one Self, Ally, Foe flag present in any row
        var genericValues = perspectivesDf.Collect().Select(r => r.GetAs<int>("generic_piece")).ToList();
        bool anyHasFlags = genericValues.Any(g => (g & (int)ChessPolicy.Piece.Self) != 0
                              || (g & (int)ChessPolicy.Piece.Ally) != 0
                              || (g & (int)ChessPolicy.Piece.Foe) != 0);
        Assert.True(anyHasFlags, "No perspective rows contain Self/Ally/Foe flags in generic_piece");
    }

    [Fact]
    public void GetPerspectives_SetsSelfFlagForOwnCell()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter for the perspective where the source and perspective are the same cell (e.g., a white pawn at x=0,y=1)
        var sameCellRows = perspectivesDf
            .Filter("x = 0 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(sameCellRows.Any(), "Expected at least one perspective row for the same cell (0,1).");

        bool anySelf = sameCellRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Self) != 0);

        Assert.True(anySelf, "Expected at least one same-cell perspective to include the Self flag in generic_piece");
    }

    [Fact]
    public void TimelineService_GeneratesCorrectTimesteps()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White };

        var perspectivesDf = Policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 2);

        Assert.Contains("timestep", timelineDf.Columns());

        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
        Assert.Contains(2, timesteps);
    }

    [Fact]
    public void PatternFactory_ReturnsPatterns()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();
        Assert.True(patternsDf.Count() > 0);
        Assert.Contains("src_conditions", patternsDf.Columns());
        Assert.Contains("dst_conditions", patternsDf.Columns());
        Assert.Contains("delta_x", patternsDf.Columns());
        Assert.Contains("delta_y", patternsDf.Columns());
    }
}
