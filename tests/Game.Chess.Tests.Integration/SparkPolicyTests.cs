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

    private static readonly ChessPolicy.Piece[] DefaultFactions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

    /// <summary>
    /// Creates an 8x8 board with all cells initialized to empty.
    /// </summary>
    private static ChessPolicy.Board CreateEmptyBoard()
    {
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        return board;
    }

    /// <summary>
    /// Creates an 8x8 board with all cells initialized to empty, then places the specified piece at (x, y).
    /// </summary>
    private static ChessPolicy.Board CreateEmptyBoardWithPiece(int x, int y, ChessPolicy.Piece piece)
    {
        var board = CreateEmptyBoard();
        board.Cell[x, y] = piece;
        return board;
    }

    /// <summary>
    /// Gets valid moves for a specific piece type by filtering patterns and computing candidates.
    /// </summary>
    private Row[] GetMovesForPieceType(ChessPolicy.Board board, ChessPolicy.Piece pieceType)
    {
        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        int pieceTypeInt = (int)pieceType;
        int publicSeq = (int)ChessPolicy.Sequence.Public;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {pieceTypeInt}) != 0 AND (sequence & {publicSeq}) != 0");
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, DefaultFactions);
        return candidates.Collect().ToArray();
    }

    /// <summary>
    /// Gets valid moves for a specific piece type using patterns filtered by piece type only (no sequence filter).
    /// Used for pieces like King that don't have InstantRecursive patterns.
    /// </summary>
    private Row[] GetMovesForPieceTypeNoSequenceFilter(ChessPolicy.Board board, ChessPolicy.Piece pieceType)
    {
        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        int pieceTypeInt = (int)pieceType;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {pieceTypeInt}) != 0");
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, DefaultFactions);
        return candidates.Collect().ToArray();
    }

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
        var board = CreateEmptyBoardWithPiece(0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King);

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        // Act: Build timeline for depth 1
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, DefaultFactions, maxDepth: 1);

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
        var board = CreateEmptyBoardWithPiece(4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King);

        // Act
        var moves = GetMovesForPieceTypeNoSequenceFilter(board, ChessPolicy.Piece.King);

        // Assert: King in center should be able to move to 8 surrounding empty squares
        Assert.Equal(8, moves.Count());
    }

    [Fact]
    public void King_CanCaptureFoe()
    {
        // Arrange: White King at (4,4), Black pawn at (5,5)
        var board = CreateEmptyBoard();
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;
        board.Cell[5, 5] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceTypeNoSequenceFilter(board, ChessPolicy.Piece.King);

        // Assert: King should be able to move to 7 empty squares + capture the enemy pawn = 8 moves
        Assert.Equal(8, moves.Count());
        
        // Check that one move lands on (5,5) - the capture
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    public void King_CannotMoveOntoAlly()
    {
        // Arrange: White King at (4,4), White pawn at (5,5)
        var board = CreateEmptyBoard();
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;
        board.Cell[5, 5] = ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceTypeNoSequenceFilter(board, ChessPolicy.Piece.King);

        // Assert: King should only have 7 moves (cannot land on allied piece at 5,5)
        Assert.Equal(7, moves.Count());
        
        // Verify no move lands on (5,5)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.Null(invalidMove);
    }

    [Fact]
    public void King_AtCorner_HasLimitedMoves()
    {
        // Arrange: King at corner (0,0) of empty board
        var board = CreateEmptyBoardWithPiece(0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King);

        // Act
        var moves = GetMovesForPieceTypeNoSequenceFilter(board, ChessPolicy.Piece.King);

        // Assert: King at corner should only have 3 valid moves
        Assert.Equal(3, moves.Count());
    }

    [Fact]
    public void PatternFactory_Contains_RookMoves()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int rook = (int)ChessPolicy.Piece.Rook;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Rook moves horizontally and vertically
        // Public patterns only for actual moves (not pre-moves)
        string filter = $"(src_conditions & {rook}) != 0 AND (sequence & {publicSeq}) != 0";
        long count = patternsDf.Filter(filter).Count();

        // Rook should have 8 patterns: 4 directions × 2 (Empty + Foe)
        Assert.True(count >= 8, $"Expected at least 8 rook public move patterns, got {count}");
    }

    [Fact]
    public void Rook_CanMoveToAdjacentSquares()
    {
        // Arrange: Rook in center of empty board
        var board = CreateEmptyBoardWithPiece(4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Rook);

        // Assert: Rook moves to 4 adjacent squares initially (up, down, left, right)
        // Full sliding requires recursive timeline expansion
        Assert.True(moves.Count() >= 4, $"Expected at least 4 rook moves, got {moves.Count()}");
    }

    [Fact]
    public void Rook_CanCaptureFoe()
    {
        // Arrange: White Rook at (0,0), Black pawn at (1,0) - adjacent
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;
        board.Cell[1, 0] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Rook);

        // Assert: Check that there is a capture move to (1,0)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 0);
        Assert.NotNull(captureMove);
    }

    [Fact]
    public void Rook_CannotMoveOntoAlly()
    {
        // Arrange: White Rook at (0,0), White pawn at (0,1) - adjacent
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;
        board.Cell[0, 1] = ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Rook);

        // Assert: Verify no move lands on (0,1) - the allied piece
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }

    [Fact]
    public void PatternFactory_Contains_BishopMoves()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int bishop = (int)ChessPolicy.Piece.Bishop;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Bishop moves diagonally
        string filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {publicSeq}) != 0";
        long count = patternsDf.Filter(filter).Count();

        // Bishop should have 8 patterns: 4 diagonals × 2 (Empty + Foe)
        Assert.True(count >= 8, $"Expected at least 8 bishop public move patterns, got {count}");
    }

    [Fact]
    public void Bishop_CanMoveToAdjacentDiagonals()
    {
        // Arrange: Bishop in center of empty board
        var board = CreateEmptyBoardWithPiece(4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Bishop);

        // Assert: Bishop moves to 4 adjacent diagonal squares initially
        // Full sliding requires recursive timeline expansion
        Assert.True(moves.Count() >= 4, $"Expected at least 4 bishop moves, got {moves.Count()}");
    }

    [Fact]
    public void Bishop_CanCaptureFoe()
    {
        // Arrange: White Bishop at (2,2), Black pawn at (3,3) - adjacent diagonal
        var board = CreateEmptyBoard();
        board.Cell[2, 2] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;
        board.Cell[3, 3] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Bishop);

        // Assert: Check that there is a capture move to (3,3)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.NotNull(captureMove);
    }

    [Fact]
    public void Bishop_CannotMoveOntoAlly()
    {
        // Arrange: White Bishop at (2,2), White pawn at (3,3) - adjacent diagonal
        var board = CreateEmptyBoard();
        board.Cell[2, 2] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;
        board.Cell[3, 3] = ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Bishop);

        // Assert: Verify no move lands on (3,3) - the allied piece
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.Null(invalidMove);
    }

    [Fact]
    public void PatternFactory_Contains_QueenMoves()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int queen = (int)ChessPolicy.Piece.Queen;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Queen moves like both rook and bishop
        string filter = $"(src_conditions & {queen}) != 0 AND (sequence & {publicSeq}) != 0";
        long count = patternsDf.Filter(filter).Count();

        // Queen should have 16 patterns: 8 directions × 2 (Empty + Foe)
        Assert.True(count >= 16, $"Expected at least 16 queen public move patterns, got {count}");
    }

    [Fact]
    public void Queen_CanMoveToAdjacentSquares()
    {
        // Arrange: Queen in center of empty board
        var board = CreateEmptyBoardWithPiece(4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen);

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Queen);

        // Assert: Queen moves to 8 adjacent squares initially (4 orthogonal + 4 diagonal)
        // Full sliding requires recursive timeline expansion
        Assert.True(moves.Count() >= 8, $"Expected at least 8 queen moves, got {moves.Count()}");
    }

    [Fact]
    public void Queen_CanCaptureFoe()
    {
        // Arrange: White Queen at (0,0), Black pawn at (0,1) - adjacent
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen;
        board.Cell[0, 1] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Queen);

        // Assert: Check that there is a capture move to (0,1)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(captureMove);
    }

    [Fact]
    public void Queen_CannotMoveOntoAlly()
    {
        // Arrange: White Queen at (0,0), White pawn at (1,1) - adjacent diagonal
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen;
        board.Cell[1, 1] = ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn;

        // Act
        var moves = GetMovesForPieceType(board, ChessPolicy.Piece.Queen);

        // Assert: Verify no move lands on (1,1) - the allied piece
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }

    // =====================================================
    // SEQUENCE PARAMETER TESTS
    // Tests validating pattern sequencing functionality
    // =====================================================

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_InMaskAndOutMask_AreCorrectlyDefined()
    {
        // Verify that InMask covers all In* flags
        var inMask = (int)ChessPolicy.Sequence.InMask;
        Assert.True((inMask & (int)ChessPolicy.Sequence.InA) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InB) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InC) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InD) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InE) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InF) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InG) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InH) != 0);
        Assert.True((inMask & (int)ChessPolicy.Sequence.InI) != 0);

        // Verify that OutMask covers all Out* flags
        var outMask = (int)ChessPolicy.Sequence.OutMask;
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutA) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutB) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutC) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutD) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutE) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutF) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutG) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutH) != 0);
        Assert.True((outMask & (int)ChessPolicy.Sequence.OutI) != 0);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_OutToIn_ConversionWorks()
    {
        // Verify that Out >> 1 = In for all pairs
        Assert.Equal((int)ChessPolicy.Sequence.InA, (int)ChessPolicy.Sequence.OutA >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InB, (int)ChessPolicy.Sequence.OutB >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InC, (int)ChessPolicy.Sequence.OutC >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InD, (int)ChessPolicy.Sequence.OutD >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InE, (int)ChessPolicy.Sequence.OutE >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InF, (int)ChessPolicy.Sequence.OutF >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InG, (int)ChessPolicy.Sequence.OutG >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InH, (int)ChessPolicy.Sequence.OutH >> 1);
        Assert.Equal((int)ChessPolicy.Sequence.InI, (int)ChessPolicy.Sequence.OutI >> 1);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_BishopPatterns_HaveCorrectFlags()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int bishop = (int)ChessPolicy.Piece.Bishop;
        int outF = (int)ChessPolicy.Sequence.OutF;
        int inF = (int)ChessPolicy.Sequence.InF;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Bishop should have OutF patterns (pre-move, recursive) - NOT public
        string outFilter = $"(src_conditions & {bishop}) != 0 AND (sequence & {outF}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outCount = patternsDf.Filter(outFilter).Count();
        Assert.True(outCount >= 4, $"Expected at least 4 bishop OutF patterns, got {outCount}");

        // Bishop should have InF patterns (final landing) - public
        string inFilter = $"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0 AND (sequence & {publicSeq}) != 0";
        long inCount = patternsDf.Filter(inFilter).Count();
        Assert.True(inCount >= 4, $"Expected at least 4 bishop InF public patterns, got {inCount}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_RookPatterns_HaveCorrectFlags()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int rook = (int)ChessPolicy.Piece.Rook;
        int outI = (int)ChessPolicy.Sequence.OutI;
        int inI = (int)ChessPolicy.Sequence.InI;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Rook should have OutI patterns (pre-move, recursive) - NOT public
        string outFilter = $"(src_conditions & {rook}) != 0 AND (sequence & {outI}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outCount = patternsDf.Filter(outFilter).Count();
        Assert.True(outCount >= 4, $"Expected at least 4 rook OutI patterns, got {outCount}");

        // Rook should have InI patterns (final landing) - public
        string inFilter = $"(src_conditions & {rook}) != 0 AND (sequence & {inI}) != 0 AND (sequence & {publicSeq}) != 0";
        long inCount = patternsDf.Filter(inFilter).Count();
        Assert.True(inCount >= 4, $"Expected at least 4 rook InI public patterns, got {inCount}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_QueenPatterns_HaveCorrectFlags()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int queen = (int)ChessPolicy.Piece.Queen;
        int outG = (int)ChessPolicy.Sequence.OutG;
        int outH = (int)ChessPolicy.Sequence.OutH;
        int inG = (int)ChessPolicy.Sequence.InG;
        int inH = (int)ChessPolicy.Sequence.InH;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Queen as bishop - OutG patterns (pre-move, recursive)
        string outGFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {outG}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outGCount = patternsDf.Filter(outGFilter).Count();
        Assert.True(outGCount >= 4, $"Expected at least 4 queen OutG patterns, got {outGCount}");

        // Queen as bishop - InG patterns (final landing)
        string inGFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {inG}) != 0 AND (sequence & {publicSeq}) != 0";
        long inGCount = patternsDf.Filter(inGFilter).Count();
        Assert.True(inGCount >= 4, $"Expected at least 4 queen InG patterns, got {inGCount}");

        // Queen as rook - OutH patterns (pre-move, recursive)
        string outHFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {outH}) != 0 AND (sequence & {instantRecursive}) != 0";
        long outHCount = patternsDf.Filter(outHFilter).Count();
        Assert.True(outHCount >= 4, $"Expected at least 4 queen OutH patterns, got {outHCount}");

        // Queen as rook - InH patterns (final landing)
        string inHFilter = $"(src_conditions & {queen}) != 0 AND (sequence & {inH}) != 0 AND (sequence & {publicSeq}) != 0";
        long inHCount = patternsDf.Filter(inHFilter).Count();
        Assert.True(inHCount >= 4, $"Expected at least 4 queen InH patterns, got {inHCount}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_PawnPatterns_HaveCorrectFlags()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int pawn = (int)ChessPolicy.Piece.Pawn;
        int outA = (int)ChessPolicy.Sequence.OutA;
        int outB = (int)ChessPolicy.Sequence.OutB;
        int publicSeq = (int)ChessPolicy.Sequence.Public;

        // Pawn forward move - OutA pattern (public)
        string outAFilter = $"(src_conditions & {pawn}) != 0 AND (sequence & {outA}) != 0 AND (sequence & {publicSeq}) != 0";
        long outACount = patternsDf.Filter(outAFilter).Count();
        Assert.True(outACount >= 1, $"Expected at least 1 pawn OutA pattern, got {outACount}");

        // Pawn capture - OutB pattern (public)
        string outBFilter = $"(src_conditions & {pawn}) != 0 AND (sequence & {outB}) != 0 AND (sequence & {publicSeq}) != 0";
        long outBCount = patternsDf.Filter(outBFilter).Count();
        Assert.True(outBCount >= 2, $"Expected at least 2 pawn OutB patterns (left/right diagonal), got {outBCount}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeNextCandidates_WithNoActiveSequence_ReturnsPublicPatterns()
    {
        // Arrange: Bishop in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to bishop-only patterns
        int bishop = (int)ChessPolicy.Piece.Bishop;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0");

        // Act: Compute candidates with no active sequence - backward compatible behavior
        // All Public patterns should execute (including those with InF flags)
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0,
            activeSequences: ChessPolicy.Sequence.None
        );
        var moves = candidates.Collect();

        // Assert: Should get moves from all Public patterns (backward compatible)
        Assert.True(moves.Count() >= 4, $"Expected at least 4 bishop moves (one per diagonal), got {moves.Count()}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeNextCandidates_WithActiveOutF_EnablesInFPatterns()
    {
        // Arrange: Bishop in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to bishop-only InF patterns (final landing)
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int inF = (int)ChessPolicy.Sequence.InF;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0");

        // Act: Compute candidates WITH active OutF sequence
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0,
            activeSequences: ChessPolicy.Sequence.OutF
        );
        var moves = candidates.Collect();

        // Assert: InF patterns should now be enabled
        Assert.True(moves.Count() >= 4, $"Expected at least 4 bishop InF moves with active OutF, got {moves.Count()}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeNextCandidates_WithWrongActiveSequence_DoesNotEnableInFPatterns()
    {
        // Arrange: Bishop in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to bishop-only InF patterns (final landing)
        int bishop = (int)ChessPolicy.Piece.Bishop;
        int inF = (int)ChessPolicy.Sequence.InF;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {bishop}) != 0 AND (sequence & {inF}) != 0");

        // Act: Compute candidates with WRONG active sequence (OutI instead of OutF)
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0,
            activeSequences: ChessPolicy.Sequence.OutI // Wrong sequence!
        );
        var moves = candidates.Collect();

        // Assert: InF patterns should NOT be enabled with OutI (wrong sequence)
        Assert.Empty(moves);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeNextCandidates_WithActiveOutI_EnablesInIPatterns()
    {
        // Arrange: Rook in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to rook-only InI patterns (final landing)
        int rook = (int)ChessPolicy.Piece.Rook;
        int inI = (int)ChessPolicy.Sequence.InI;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0 AND (sequence & {inI}) != 0");

        // Act: Compute candidates WITH active OutI sequence
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0,
            activeSequences: ChessPolicy.Sequence.OutI
        );
        var moves = candidates.Collect();

        // Assert: InI patterns should be enabled
        Assert.True(moves.Count() >= 4, $"Expected at least 4 rook InI moves with active OutI, got {moves.Count()}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeNextCandidates_CandidatesPreserveSequenceColumn()
    {
        // Arrange: Rook in center of empty board
        var board = new ChessPolicy.Board(8, 8, new ChessPolicy.Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = ChessPolicy.Piece.Empty;
        
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        // Filter to rook-only public patterns
        int rook = (int)ChessPolicy.Piece.Rook;
        int publicSeq = (int)ChessPolicy.Sequence.Public;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0 AND (sequence & {publicSeq}) != 0");

        // Act: Compute candidates
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, 
            patternsDf, 
            factions
        );

        // Assert: The 'sequence' column should be present in the results
        Assert.Contains("sequence", candidates.Columns());
        
        var moves = candidates.Collect();
        Assert.True(moves.Count() > 0, "Expected at least one move");
        
        // Verify sequence values are non-zero (patterns have sequence flags)
        var sequenceValues = moves.Select(r => r.GetAs<int>("sequence")).ToList();
        Assert.All(sequenceValues, seq => Assert.True(seq != 0, "Sequence should have flags set"));
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Sequence_VariantFiltering_Works()
    {
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();

        int bishop = (int)ChessPolicy.Piece.Bishop;
        int variant1 = (int)ChessPolicy.Sequence.Variant1;
        int variant2 = (int)ChessPolicy.Sequence.Variant2;
        int variant3 = (int)ChessPolicy.Sequence.Variant3;
        int variant4 = (int)ChessPolicy.Sequence.Variant4;

        // Each bishop direction should have a different variant
        string v1Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant1}) != 0";
        string v2Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant2}) != 0";
        string v3Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant3}) != 0";
        string v4Filter = $"(src_conditions & {bishop}) != 0 AND (sequence & {variant4}) != 0";

        // Each variant should have at least 2 patterns (OutF + InF for that direction)
        Assert.True(patternsDf.Filter(v1Filter).Count() >= 2, "Expected Variant1 patterns");
        Assert.True(patternsDf.Filter(v2Filter).Count() >= 2, "Expected Variant2 patterns");
        Assert.True(patternsDf.Filter(v3Filter).Count() >= 2, "Expected Variant3 patterns");
        Assert.True(patternsDf.Filter(v4Filter).Count() >= 2, "Expected Variant4 patterns");
    }

    // ==================== Rook Sequenced Move Tests ====================

    /// <summary>
    /// Helper method to build a pattern filter string
    /// </summary>
    private static string BuildPatternFilter(int pieceType, int? sequenceFlags = null)
    {
        if (sequenceFlags.HasValue)
            return $"(src_conditions & {pieceType}) != 0 AND (sequence & {sequenceFlags.Value}) != 0";
        return $"(src_conditions & {pieceType}) != 0";
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ConvertOutFlagsToInFlags_ConvertsCorrectly()
    {
        // OutI >> 1 should give InI
        int outI = (int)ChessPolicy.Sequence.OutI;
        int expectedInI = (int)ChessPolicy.Sequence.InI;
        
        int result = ChessPolicy.TimelineService.ConvertOutFlagsToInFlags(outI);
        
        Assert.Equal(expectedInI, result);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ConvertOutFlagsToInFlags_HandlesMultipleFlags()
    {
        // OutI | OutF should give InI | InF
        int outFlags = (int)(ChessPolicy.Sequence.OutI | ChessPolicy.Sequence.OutF);
        int expectedInFlags = (int)(ChessPolicy.Sequence.InI | ChessPolicy.Sequence.InF);
        
        int result = ChessPolicy.TimelineService.ConvertOutFlagsToInFlags(outFlags);
        
        Assert.Equal(expectedInFlags, result);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Rook_EntryPatterns_HaveOutIAndInstantRecursive()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();
        int rook = (int)ChessPolicy.Piece.Rook;
        int outI = (int)ChessPolicy.Sequence.OutI;
        int instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        int inMask = (int)ChessPolicy.Sequence.InMask;
        
        // Filter to rook entry patterns (OutI, InstantRecursive, no In flags)
        var entryPatterns = patternsDf.Filter(
            $"(src_conditions & {rook}) != 0 " +
            $"AND (sequence & {outI}) != 0 " +
            $"AND (sequence & {instantRecursive}) = {instantRecursive} " +
            $"AND (sequence & {inMask}) = 0"
        );
        
        // Assert: Should have 4 entry patterns (one per direction)
        var count = entryPatterns.Count();
        Assert.True(count >= 4, $"Expected at least 4 rook entry patterns, got {count}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void Rook_ContinuationPatterns_HaveInIAndPublic()
    {
        // Arrange
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns();
        int rook = (int)ChessPolicy.Piece.Rook;
        int inI = (int)ChessPolicy.Sequence.InI;
        int publicFlag = (int)ChessPolicy.Sequence.Public;
        
        // Filter to rook continuation patterns (InI, Public)
        var continuationPatterns = patternsDf.Filter(
            $"(src_conditions & {rook}) != 0 " +
            $"AND (sequence & {inI}) != 0 " +
            $"AND (sequence & {publicFlag}) != 0"
        );
        
        // Assert: Should have 8 continuation patterns (4 directions × 2 target types: Empty + Foe)
        var count = continuationPatterns.Count();
        Assert.True(count >= 8, $"Expected at least 8 rook continuation patterns, got {count}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeSequencedMoves_Rook_CenterOfEmptyBoard_CanReachAllSquaresInLine()
    {
        // Arrange: Rook in center of empty board at (4, 4)
        var board = CreateEmptyBoard();
        board.Cell[4, 4] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        int rook = (int)ChessPolicy.Piece.Rook;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0");

        // Act: Compute sequenced moves
        var moves = ChessPolicy.TimelineService.ComputeSequencedMoves(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0,
            maxDepth: 7,
            debug: false
        );
        var movesList = moves.Collect().ToList();
        
        // Assert: Rook should be able to reach all squares in horizontal and vertical lines
        // From (4,4): columns 0-7 on row 4 (except 4), rows 0-7 on column 4 (except 4) = 7 + 7 = 14 squares
        Assert.True(movesList.Count >= 14, $"Expected at least 14 rook moves from center, got {movesList.Count}");
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeSequencedMoves_Rook_CanCaptureDistantFoe()
    {
        // Arrange: White Rook at (0, 0), Black pawn at (0, 5)
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;
        board.Cell[0, 5] = ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        int rook = (int)ChessPolicy.Piece.Rook;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0");

        // Act: Compute sequenced moves
        var moves = ChessPolicy.TimelineService.ComputeSequencedMoves(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0
        );
        var movesList = moves.Collect().ToList();
        
        // Assert: Should be able to capture the distant foe at (0, 5)
        var captureMove = movesList.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Feature", "Sequence")]
    public void ComputeSequencedMoves_Rook_CanMoveToAdjacentEmpty()
    {
        // Arrange: White Rook at (0, 0)
        var board = CreateEmptyBoard();
        board.Cell[0, 0] = ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;

        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = Policy.GetPerspectives(board, factions);
        
        int rook = (int)ChessPolicy.Piece.Rook;
        var patternsDf = new ChessPolicy.PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {rook}) != 0");

        // Act: Compute sequenced moves
        var moves = ChessPolicy.TimelineService.ComputeSequencedMoves(
            perspectivesDf, 
            patternsDf, 
            factions,
            turn: 0
        );
        var movesList = moves.Collect().ToList();
        
        // Assert: Should be able to move to adjacent empty square (0, 1)
        var adjacentMove = movesList.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(adjacentMove);
    }

}
