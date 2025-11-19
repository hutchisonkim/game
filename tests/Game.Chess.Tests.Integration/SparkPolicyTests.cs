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
    public void TimelineService_GeneratesCorrectTimesteps()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

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

    [Fact]
    public void BuildTimeline_FromPolicy_ReturnsTimestepsAndPerspectiveId()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var timelineDf = Policy.BuildTimeline(board, factions, maxDepth: 2);

        Assert.Contains("timestep", timelineDf.Columns());
        Assert.Contains("perspective_id", timelineDf.Columns());

        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
        Assert.Contains(2, timesteps);
    }

    [Fact]
    public void GetPerspectives_PerspectiveId_IsUniqueAndDeterministic()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        // First calculation
        var perspectivesDf1 = Policy.GetPerspectives(board, factions);
        long total1 = perspectivesDf1.Count();
        long distinct1 = perspectivesDf1.Select("perspective_id").Distinct().Count();

        // Recompute and compare sets to ensure determinism
        var perspectivesDf2 = Policy.GetPerspectives(board, factions);
        long total2 = perspectivesDf2.Count();
        long distinct2 = perspectivesDf2.Select("perspective_id").Distinct().Count();

        // We expect the total row count to be stable across calls
        Assert.Equal(total1, total2);

        // Distinct perspective id counts should be stable (deterministic hashing)
        Assert.Equal(distinct1, distinct2);
        Assert.True(distinct1 > 0, "Expected at least one distinct perspective_id");

        // Ensure both DataFrames contain the same set of perspective rows (deterministic ids)
        var diffCount = perspectivesDf1.Except(perspectivesDf2).Count();
        Assert.Equal(0, diffCount);
    }

    [Fact]
    public void GetPerspectives_GenericPiece_RemovesFactionBits()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);

        var genericValues = perspectivesDf.Collect().Select(r => r.GetAs<int>("generic_piece")).ToList();

        int factionMask = (int)ChessPolicy.Piece.White | (int)ChessPolicy.Piece.Black;
        bool anyHasFactionBits = genericValues.Any(g => (g & factionMask) != 0);

        Assert.False(anyHasFactionBits, "Expected no White/Black faction bits to remain in generic_piece");
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
    public void TimelineService_Timesteps_IncludeRequestedDepth()
    {
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        var perspectivesDf = Policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        Assert.Contains("timestep", timelineDf.Columns());

        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => (int)r.Get(0)).ToList();

        // Expect 0..1 timesteps present (use a small depth to keep the job lightweight)
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
    }

    // [Fact]
    public void Pawn_GeneratesForwardMove_FromIsolatedPosition()
    {
        // Arrange: Create a board with only a white pawn at e2 (x=4, y=1)
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[4, 1] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves: no recursive, no castling, no en passant
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: There should be a move to e3 (x=4, y=2)
        var moveRows = timelineDf.Filter("timestep = 1 AND x = 4 AND y = 2").Collect();
        Assert.True(moveRows.Any(), "Expected a pawn move from e2 to e3.");
    }

    [Fact]
    public void Pawn_GeneratesCaptureMove_WhenFoePresent()
    {
        // Arrange: White pawn at e2 (x=4, y=1), black pawn at d3 (x=3, y=2)
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[4, 1] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;
        board.Cell[3, 2] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: There should be a capture move to d3 (x=3, y=2)
        var captureRows = timelineDf.Filter("timestep = 1 AND x = 3 AND y = 2").Collect();
        Assert.True(captureRows.Any(), "Expected a pawn capture from e2 to d3.");
    }

    // [Fact]
    public void Knight_GeneratesAllMoves_FromCenter()
    {
        // Arrange: White knight at d4 (x=3, y=3), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 8 possible knight moves
        var knightMoves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(8, knightMoves.Count());
    }

    // [Fact]
    public void King_GeneratesAllMoves_FromCenter()
    {
        // Arrange: White king at d4 (x=3, y=3), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 8 possible king moves
        var kingMoves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(8, kingMoves.Count());
    }

    // [Fact]
    public void Rook_GeneratesSingleStepMoves_FromCenter()
    {
        // Arrange: White rook at d4 (x=3, y=3), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 4 possible rook moves (single steps)
        var rookMoves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(4, rookMoves.Count());
    }

    // [Fact]
    public void Bishop_GeneratesSingleStepMoves_FromCenter()
    {
        // Arrange: White bishop at d4 (x=3, y=3), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 4 possible bishop moves (single steps)
        var bishopMoves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(4, bishopMoves.Count());
    }

    // [Fact]
    public void Queen_GeneratesSingleStepMoves_FromCenter()
    {
        // Arrange: White queen at d4 (x=3, y=3), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 8 possible queen moves (single steps)
        var queenMoves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(8, queenMoves.Count());
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

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: 8 pawn moves + 4 knight moves = 12
        var moves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Equal(12, moves.Count());
    }

    // [Fact]
    public void PawnPromotion_MoveGenerated()
    {
        // Arrange: White pawn at e7 (x=4, y=6), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[4, 6] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: Pawn can move to e8 (x=4, y=7)
        var promotionMove = timelineDf.Filter("timestep = 1 AND x = 4 AND y = 7").Collect();
        Assert.True(promotionMove.Any(), "Expected pawn promotion move to 8th rank.");
    }

    [Fact]
    public void PawnCapture_InGamePosition()
    {
        // Arrange: White pawn at d4 (x=3, y=3), black pawn at e5 (x=4, y=4)
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;
        board.Cell[4, 4] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: Capture move to e5 (x=4, y=4)
        var captureMove = timelineDf.Filter("timestep = 1 AND x = 4 AND y = 4").Collect();
        Assert.True(captureMove.Any(), "Expected pawn capture to e5.");
    }

    [Fact]
    public void Moves_DoNotGoOffBoard()
    {
        // Arrange: King at a1 (x=0, y=0), empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: No moves have x < 0 or x > 7 or y < 0 or y > 7
        var offBoardMoves = timelineDf.Filter("timestep = 1 AND (x < 0 OR x > 7 OR y < 0 OR y > 7)").Collect();
        Assert.Empty(offBoardMoves);
    }

    // [Fact]
    public void OpeningSequence_WhiteE2E4_BlackE7E6()
    {
        // Arrange: Initial board
        var board = ChessPolicy.Board.Default;
        board.Initialize();

        // Act: White moves e2-e4
        var whiteFactions = new[] { ChessPolicy.Piece.White };
        var whitePerspectives = Policy.GetPerspectives(board, whiteFactions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");
        var whiteTimeline = ChessPolicy.TimelineService.BuildTimeline(whitePerspectives, patternsDf, maxDepth: 1);

        // Assert: e2-e4 is generated
        var e2e4 = whiteTimeline.Filter("timestep = 1 AND x = 4 AND y = 3").Collect();
        Assert.True(e2e4.Any(), "Expected white e2-e4 move.");

        // Update board for e2-e4
        board.Cell[4, 1] = ChessPolicy.Piece.None;
        board.Cell[4, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;

        // Act: Black moves e7-e6
        var blackFactions = new[] { ChessPolicy.Piece.Black };
        var blackPerspectives = Policy.GetPerspectives(board, blackFactions);
        var blackTimeline = ChessPolicy.TimelineService.BuildTimeline(blackPerspectives, patternsDf, maxDepth: 1);

        // Assert: e7-e6 is generated (e7 is x=4, y=6, e6 is x=4, y=5)
        var e7e6 = blackTimeline.Filter("timestep = 1 AND x = 4 AND y = 5").Collect();
        Assert.True(e7e6.Any(), "Expected black e7-e6 move.");
    }

    // [Fact]
    public void Checkmate_NoLegalMovesForLosingSide()
    {
        // Arrange: Simple checkmate position - black king at e8, white queen at d8
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        board.Cell[4, 7] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King; // e8
        board.Cell[3, 7] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen; // d8
        board.Cell[4, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King; // e1

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);

        // Filter patterns to exclude special moves
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter("NOT (sequence & 12 != 0 OR sequence & 14 != 0 OR sequence & 4096 != 0 OR sequence & 2048 != 0 OR sequence & 16384 != 0 OR sequence & 8192 != 0)");

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, maxDepth: 1);

        // Assert: No legal moves for black (checkmate)
        var moves = timelineDf.Filter("timestep = 1").Collect();
        Assert.Empty(moves);
    }
}
