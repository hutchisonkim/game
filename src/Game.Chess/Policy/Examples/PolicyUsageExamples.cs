using Microsoft.Spark.Sql;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;

namespace Game.Chess.Policy.Examples;

/// <summary>
/// Examples demonstrating usage of the refactored layered architecture.
/// These examples show how to use each layer independently and how they compose.
/// </summary>
public class PolicyUsageExamples
{
    private readonly SparkSession _spark;

    public PolicyUsageExamples(SparkSession spark)
    {
        _spark = spark;
    }

    /// <summary>
    /// Example 1: Using Foundation layer to get board state
    /// </summary>
    public void Example1_GetBoardState()
    {
        // Create a board
        var board = Board.Default;
        board.Initialize();

        // Use BoardStateProvider to get pieces DataFrame
        var provider = new BoardStateProvider(_spark);
        var piecesDf = provider.GetPieces(board);

        // Result: DataFrame with schema (x, y, piece)
        // Each row represents one cell on the board
        Console.WriteLine($"Board has {piecesDf.Count()} cells");
    }

    /// <summary>
    /// Example 2: Using Foundation layer to get patterns
    /// </summary>
    public void Example2_GetPatterns()
    {
        var repository = new PatternRepository(_spark);
        var patternsDf = repository.GetPatterns();

        // Result: DataFrame with all chess movement patterns
        // Patterns are automatically deduplicated and cached
        Console.WriteLine($"Chess has {patternsDf.Count()} unique patterns");

        // Clear cache if patterns change (rare)
        repository.ClearCache();
    }

    /// <summary>
    /// Example 3: Using PerspectiveEngine to compute relationships
    /// </summary>
    public void Example3_BuildPerspectives()
    {
        var board = Board.Default;
        board.Initialize();

        var provider = new BoardStateProvider(_spark);
        var engine = new PerspectiveEngine();

        var piecesDf = provider.GetPieces(board);
        var factions = new[] { Piece.White, Piece.Black };

        // Build perspectives: each actor sees Self/Ally/Foe
        var perspectivesDf = engine.BuildPerspectives(piecesDf, factions);

        // Add perspective IDs for tracking
        perspectivesDf = engine.AddPerspectiveId(perspectivesDf);

        // Result: Each piece has a complete view of the board
        // with relationships (Self/Ally/Foe) encoded
        Console.WriteLine($"Generated {perspectivesDf.Count()} perspective rows");
    }

    /// <summary>
    /// Example 4: Using SimulationEngine to test a move
    /// </summary>
    public void Example4_SimulateMove()
    {
        // Start with initial perspectives
        var board = Board.Default;
        board.Initialize();

        var provider = new BoardStateProvider(_spark);
        var perspEngine = new PerspectiveEngine();
        var simEngine = new SimulationEngine();

        var piecesDf = provider.GetPieces(board);
        var factions = new[] { Piece.White, Piece.Black };
        var perspectivesDf = perspEngine.BuildPerspectives(piecesDf, factions);

        // Simulate moving white pawn from (0,1) to (0,2)
        // (In real use, candidatesDf comes from pattern matching)
        var candidatesDf = CreateMockMove(
            srcX: 0, srcY: 1, 
            dstX: 0, dstY: 2, 
            piece: Piece.White | Piece.Pawn);

        // Simulate the move
        var newPerspectivesDf = simEngine.SimulateBoardAfterMove(
            perspectivesDf, 
            candidatesDf, 
            factions);

        // Result: New perspective DataFrame with pawn at (0,2)
        // and (0,1) now empty
        Console.WriteLine("Move simulated successfully");
    }

    /// <summary>
    /// Example 5: Using ChessPolicyRefactored facade (recommended)
    /// </summary>
    public void Example5_UsingFacade()
    {
        var policy = new ChessPolicyRefactored(_spark);

        var board = Board.Default;
        board.Initialize();

        var factions = new[] { Piece.White, Piece.Black };

        // Get perspectives (combines Foundation + Perspectives layers)
        var perspectives = policy.GetPerspectives(board, factions);

        // Get perspectives with threats (adds threat computation)
        var perspectivesWithThreats = policy.GetPerspectivesWithThreats(
            board, 
            factions, 
            turn: 0);

        // Build complete game tree
        var timeline = policy.BuildTimeline(board, factions, maxDepth: 3);

        Console.WriteLine($"Timeline has {timeline.Count()} rows");
    }

    /// <summary>
    /// Example 6: Layer composition for custom logic
    /// </summary>
    public void Example6_CustomComposition()
    {
        // You can compose layers however you need
        var provider = new BoardStateProvider(_spark);
        var perspEngine = new PerspectiveEngine();
        var simEngine = new SimulationEngine();

        var board = Board.Default;
        board.Initialize();

        var factions = new[] { Piece.White, Piece.Black };

        // Step 1: Get board state
        var piecesDf = provider.GetPieces(board);

        // Step 2: Build perspectives
        var perspectives = perspEngine.BuildPerspectives(piecesDf, factions);

        // Step 3: Custom logic here (e.g., filter to specific pieces)
        var whitePiecesOnly = perspectives.Filter(
            Microsoft.Spark.Sql.Functions.Col("perspective_piece")
                .BitwiseAND((int)Piece.White)
                .NotEqual(Microsoft.Spark.Sql.Functions.Lit(0)));

        // Step 4: Simulate moves (when you have candidates)
        // var newState = simEngine.SimulateBoardAfterMove(...);

        Console.WriteLine($"White has {whitePiecesOnly.Count()} perspective rows");
    }

    /// <summary>
    /// Example 7: Testing individual layers
    /// </summary>
    public void Example7_TestingLayers()
    {
        // Each layer can be tested independently with small DataFrames

        // Test Foundation layer
        var board = new Board(3, 3, new Piece[3, 3]);
        board.Cell[0, 0] = Piece.White | Piece.King;
        board.Cell[2, 2] = Piece.Black | Piece.King;

        var provider = new BoardStateProvider(_spark);
        var piecesDf = provider.GetPieces(board);

        // Verify: should have 9 cells
        System.Diagnostics.Debug.Assert(piecesDf.Count() == 9);

        // Test Perspective layer
        var engine = new PerspectiveEngine();
        var perspectives = engine.BuildPerspectives(
            piecesDf, 
            new[] { Piece.White, Piece.Black });

        // Verify: 2 actors × 9 cells = 18 perspective rows
        System.Diagnostics.Debug.Assert(perspectives.Count() == 18);

        Console.WriteLine("Layer tests passed");
    }

    /// <summary>
    /// Helper method to create a mock move for examples
    /// </summary>
    private DataFrame CreateMockMove(int srcX, int srcY, int dstX, int dstY, Piece piece)
    {
        // This is a simplified version - real moves come from pattern matching
        var schema = new Microsoft.Spark.Sql.Types.StructType(
        [
            new Microsoft.Spark.Sql.Types.StructField("perspective_x", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("perspective_y", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("perspective_piece", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("src_x", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("src_y", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("src_piece", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("src_generic_piece", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("dst_x", new Microsoft.Spark.Sql.Types.IntegerType()),
            new Microsoft.Spark.Sql.Types.StructField("dst_y", new Microsoft.Spark.Sql.Types.IntegerType()),
        ]);

        var row = new Microsoft.Spark.Sql.GenericRow(
        [
            srcX, srcY, (int)piece,
            srcX, srcY, (int)piece, (int)(piece | Piece.Self),
            dstX, dstY
        ]);

        return _spark.CreateDataFrame([row], schema);
    }
}
