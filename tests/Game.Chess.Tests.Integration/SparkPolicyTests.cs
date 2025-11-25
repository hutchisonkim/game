using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Microsoft.Spark.Sql.Types;
using System.Linq;

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

        // --- Ally assertion ---
        // Choose a different white pawn as the 'piece' (x=1,y=1) and perspective at the white pawn (x=0,y=1)
        var allyRows = perspectivesDf
            .Filter("x = 1 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(allyRows.Any(), "Expected at least one perspective row for an allied piece (1,1) looking at (0,1).");

        bool anyAlly = allyRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Ally) != 0);

        Assert.True(anyAlly, "Expected at least one perspective row to include the Ally flag in generic_piece");

        // --- Foe assertion ---
        // Choose a black pawn as the 'piece' (x=0,y=6) and perspective at the white pawn (x=0,y=1)
        var foeRows = perspectivesDf
            .Filter("x = 0 AND y = 6 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(foeRows.Any(), "Expected at least one perspective row for a foe piece (0,6) looking at (0,1).");

        bool anyFoe = foeRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Foe) != 0);

        Assert.True(anyFoe, "Expected at least one perspective row to include the Foe flag in generic_piece");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void TimelineService_GeneratesCorrectTimesteps()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, factions, maxDepth: 2);

        Assert.Contains("timestep", timelineDf.Columns());

        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        // Because depth is 1-based, for maxDepth=2 we expect timesteps 0, 1
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
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


    [Fact]
    public void Board_Default_Has64Squares()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Board.Cell is a 2D array; Length should equal Width * Height
        Assert.Equal(board.Width * board.Height, board.Cell.Length);

        // Ensure there are exactly 64 positions for the standard default
        Assert.Equal(8 * 8, board.Cell.Length);
    }

    [Fact]
    public void PatternFactory_Contains_KnightMoves()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        // knight moves have abs(delta_x) == 2 and abs(delta_y) == 1 (or swapped)
        long count = patternsDf.Filter("ABS(delta_x) = 2 AND ABS(delta_y) = 1").Count();

        Assert.True(count > 0, "Expected at least one knight move pattern with (2,1) deltas");
    }

    [Fact]
    public void PatternFactory_Contains_PawnCapturePatterns()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int pawn = (int)ChessPolicy.Piece.Pawn;
        int foe = (int)ChessPolicy.Piece.Foe;

        // pawn capture patterns typically have abs(delta_x) = 1 and delta_y = 1 and dst_conditions includes Foe
        string filter = $"(src_conditions & {pawn}) != 0 AND (dst_conditions & {foe}) != 0 AND ABS(delta_x) = 1 AND delta_y = 1";
        long count = patternsDf.Filter(filter).Count();

        Assert.True(count > 0, "Expected at least one pawn capture pattern (diagonal capture)");
    }

    [Fact]
    public void PatternFactory_Contains_PawnPromotionPatterns()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int pawn = (int)ChessPolicy.Piece.Pawn;
        int instantMandatory = (int)ChessPolicy.Sequence.InstantMandatory;

        // promotions should include pawn src and InstantMandatory sequence bits
        string filter = $"(src_conditions & {pawn}) != 0 AND (sequence & {instantMandatory}) != 0";
        long count = patternsDf.Filter(filter).Count();

        Assert.True(count > 0, "Expected at least one pawn promotion-related pattern with InstantMandatory sequence");
    }

    [Fact]
    public void InitialBoard_WhiteHasCorrectNumberOfMoves()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Get candidates
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);

        // Assert: 8 pawn moves + 4 knight moves = 12
        var moves = candidates.Collect();
        Assert.Equal(12, moves.Count());
    }

    [Fact]
    public void Moves_DoNotGoOffBoard()
    {
        // Arrange: King at a1 (x=0, y=0), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, factions, maxDepth: 1);

        // Assert: No moves have x < 0 or x > 7 or y < 0 or y > 7
        var offBoardMoves = timelineDf.Filter("timestep = 1 AND (x < 0 OR x > 7 OR y < 0 OR y > 7)").Collect();
        Assert.Empty(offBoardMoves);
    }

    [Fact]
    public void PatternFactory_Contains_KingMoves()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int king = (int)ChessPolicy.Piece.King;

        // King can move 1 square in any direction
        // Rook-like moves: (1,0), (-1,0), (0,1), (0,-1)
        // Bishop-like moves: (1,1), (-1,1), (1,-1), (-1,-1)
        string filter = $"(src_conditions & {king}) != 0 AND ABS(delta_x) <= 1 AND ABS(delta_y) <= 1 AND NOT (delta_x = 0 AND delta_y = 0)";
        long count = patternsDf.Filter(filter).Count();

        // King should have 16 patterns: 8 directions × 2 (Empty + Foe)
        Assert.True(count >= 16, $"Expected at least 16 king move patterns, got {count}");
    }

    [Fact]
    public void King_InCenter_Has8EmptySquareMoves()
    {
        // Arrange: King in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to king-only patterns
        int king = (int)ChessPolicy.Piece.King;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {king}) != 0");

        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        var moves = candidates.Collect();

        // King in center should be able to move to 8 surrounding empty squares
        Assert.Equal(8, moves.Count());
    }

    [Fact]
    public void King_CanCaptureFoe()
    {
        // Arrange: White King at (4,4), Black pawn at (5,5)
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;
        board.Cell[5, 5] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to king-only patterns
        int king = (int)ChessPolicy.Piece.King;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {king}) != 0");

        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        var moves = candidates.Collect();

        // King should be able to move to 7 empty squares + capture the enemy pawn = 8 moves
        Assert.Equal(8, moves.Count());
        
        // Check that one move lands on (5,5) - the capture
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    public void King_CannotMoveOntoAlly()
    {
        // Arrange: White King at (4,4), White pawn at (5,5)
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;
        board.Cell[5, 5] = ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to king-only patterns
        int king = (int)ChessPolicy.Piece.King;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {king}) != 0");

        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        var moves = candidates.Collect();

        // King should only have 7 moves (cannot land on allied piece at 5,5)
        Assert.Equal(7, moves.Count());
        
        // Verify no move lands on (5,5)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.Null(invalidMove);
    }

    [Fact]
    public void King_AtCorner_HasLimitedMoves()
    {
        // Arrange: King at corner (0,0) of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to king-only patterns
        int king = (int)ChessPolicy.Piece.King;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {king}) != 0");

        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        var moves = candidates.Collect();

        // King at corner should only have 3 valid moves
        Assert.Equal(3, moves.Count());
    }

}
