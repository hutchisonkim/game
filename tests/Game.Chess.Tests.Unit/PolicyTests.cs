using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using System.Linq;

namespace Game.Chess.Tests.Unit;

[Trait("Feature", "ChessPolicy")]
public class ChessPolicyTests
{
    // Create Spark and ChessPolicy lazily so tests that don't require Spark
    // (for example, BoardInitialization_CreatesCorrectStartingPositions) don't
    // attempt to start a Spark worker/connection when it's not available.
    private SparkSession? _spark;
    private ChessPolicy? _policy;

    private SparkSession Spark
    {
        get
        {
            if (_spark != null) return _spark;
            // Prefer SPARK_MASTER_URL (set by CI or by docker-compose). If not provided,
            // default to the docker-compose mapping on the host so running tests locally
            // against the `spark` service works (compose maps 7077 to host).
            var masterUrl = Environment.GetEnvironmentVariable("SPARK_MASTER_URL");
            var sparkMaster = string.IsNullOrWhiteSpace(masterUrl) ? "spark://localhost:7077" : masterUrl;

            try
            {
                _spark = SparkSession
                    .Builder()
                    .AppName("ChessPolicyTests")
                    .Master(sparkMaster)
                    .Config("spark.sql.shuffle.partitions", "1") // small local jobs
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

        // check at least one Self, Ally, Foe flag present
        var firstRow = perspectivesDf.Collect().First();
        int genericValue = (int)firstRow.Get(4); // generic_piece
        Assert.True((genericValue & (int)ChessPolicy.Piece.Self) != 0
                    || (genericValue & (int)ChessPolicy.Piece.Ally) != 0
                    || (genericValue & (int)ChessPolicy.Piece.Foe) != 0);
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
