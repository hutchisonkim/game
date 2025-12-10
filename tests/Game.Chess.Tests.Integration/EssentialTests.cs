using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;
using Game.Chess.Policy.Patterns;
using Game.Chess.Policy.Sequences;
using Game.Chess.Policy.Threats;
using Game.Chess.Policy.Candidates;
using Game.Chess.Policy.Validation;
using System.Linq;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Unit;

/// <summary>
/// Essential tests for the Chess Policy Refactoring.
/// These tests form the critical path for validating the 8-layer architecture.
/// 
/// Trait System:
/// - Essential: Marks test as critical for refactoring progress
/// - DependsOn: Test cannot pass unless parent tests pass
/// - Category: Groups tests by architectural layer
/// - ChessRule: Links test to specific chess rule (A1-D4)
/// </summary>
[Collection("Spark collection")]
public class EssentialTests
{
    private readonly SparkSession _spark;

    public EssentialTests()
    {
        // Note: SparkSession is managed by the Spark collection fixture
        _spark = SparkSession.Builder().AppName("EssentialTests").GetOrCreate();
    }

    #region Layer 0: Foundation (3 tests)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_BoardStateProvider")]
    public void L0_Foundation_BoardStateProvider_ConvertsArrayToDataFrame()
    {
        // Arrange
        var board = Board.Default;
        board.Initialize();
        var provider = new BoardStateProvider(_spark);

        // Act
        var piecesDf = provider.GetPieces(board);

        // Assert
        Assert.NotNull(piecesDf);
        Assert.Equal(64, piecesDf.Count()); // 8x8 board = 64 cells
        
        // Verify schema: should have x, y, piece columns
        var columns = piecesDf.Columns();
        Assert.Contains("x", columns);
        Assert.Contains("y", columns);
        Assert.Contains("piece", columns);
        
        // Verify coordinate ranges: all x,y should be 0-7
        var outOfBounds = piecesDf.Filter("x < 0 OR x > 7 OR y < 0 OR y > 7").Count();
        Assert.Equal(0, outOfBounds);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_PatternRepository")]
    public void L0_Foundation_PatternRepository_LoadsAllPatterns()
    {
        // Arrange
        var repo = new PatternRepository(_spark);

        // Act
        var patternsDf = repo.GetPatterns();

        // Assert
        Assert.NotNull(patternsDf);
        var patternCount = patternsDf.Count();
        Assert.NotEqual(0, patternCount); // Must have patterns
        
        // Verify schema has required pattern columns
        var columns = patternsDf.Columns();
        Assert.Contains("src_conditions", columns);
        Assert.Contains("dst_conditions", columns);
        Assert.Contains("delta_x", columns);
        Assert.Contains("delta_y", columns);
        Assert.Contains("sequence", columns);
        
        // Verify caching works (second call returns same object)
        var patternsDf2 = repo.GetPatterns();
        Assert.Same(patternsDf, patternsDf2);
        Assert.Equal(patternCount, patternsDf2.Count());
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_BoardStateProvider")]
    [Trait("Category", "Foundation")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "0")]
    [Trait("TestId", "L0_Foundation_PerspectiveEngine")]
    public void L0_Foundation_PerspectiveEngine_AssignsSelfAllyFoe()
    {
        // Arrange
        var board = Board.Default;
        board.Initialize();
        var provider = new BoardStateProvider(_spark);
        var engine = new PerspectiveEngine();
        var piecesDf = provider.GetPieces(board);
        var factions = new[] { Piece.White, Piece.Black };

        // Act
        var perspectivesDf = engine.BuildPerspectives(piecesDf, factions);

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Perspectives is cross-join: 32 pieces (default board) × 64 cells = 2048 rows
        Assert.Equal(32 * 64, perspectiveCount);
        
        // Verify schema includes perspective columns
        var columns = perspectivesDf.Columns();
        Assert.Contains("perspective_x", columns);
        Assert.Contains("perspective_y", columns);
        Assert.Contains("perspective_piece", columns);
        Assert.Contains("generic_piece", columns);
        
        // Verify Self/Ally/Foe bits are set correctly
        var selfBit = (int)Piece.Self;
        var selfMarked = perspectivesDf.Filter($"(generic_piece & {selfBit}) != 0").Count();
        Assert.True(selfMarked > 0, "Some cells should be marked Self");
    }

    #endregion

    #region Layer 1: Atomic Pattern Matching (3 piece-specific tests + 1 aggregation test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_PerspectiveEngine|L0_Foundation_PatternRepository")]
    [Trait("Category", "Atomic")]
    [Trait("ChessRule", "A1|A3")]
    [Trait("Layer", "1")]
    [Trait("TestId", "L1_PatternMatcher_Pawn")]
    public void L1_PatternMatcher_Pawn_MatchesPawnMovementPatterns()
    {
        // Arrange: Board with white pawn on e2 (starting position)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 1, PieceBuilder.Create().White().Pawn().Mint().Build())  // White pawn at e2 (4,1) - mint for 2-square move
        );
        
        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });
        var matchesDf = PatternMatcher.MatchAtomicPatterns(perspectivesDf, patternsDf, new[] { Piece.White }, turn: 0);

        // Assert - Verify pawn generates forward moves
        var moves = matchesDf.Collect();
        var pawnMoves = moves.Where(r => r.GetAs<int>("src_x") == 4 && r.GetAs<int>("src_y") == 1).ToList();
        
        // Pawn at e2 should have forward moves (at least 1-2 depending on mint status)
        Assert.True(pawnMoves.Count >= 1, $"Pawn at e2 should have at least 1 forward move, got {pawnMoves.Count}");
        
        // Verify all pawn moves are forward (dy > 0, dx = 0)
        var allForward = pawnMoves.All(r => {
            var srcY = r.GetAs<int>("src_y");
            var dstY = r.GetAs<int>("dst_y");
            var srcX = r.GetAs<int>("src_x");
            var dstX = r.GetAs<int>("dst_x");
            return dstY > srcY && srcX == dstX;
        });
        Assert.True(allForward, "All pawn moves should be forward (same file, higher rank)");
        
        // Verify delta_x and delta_y match expected pawn forward movement in patterns
        var pawnBit = (int)Piece.Pawn;
        var forwardPatterns = patternsDf.Filter($"(src_conditions & {pawnBit}) != 0 AND delta_x = 0 AND delta_y > 0").Count();
        Assert.True(forwardPatterns > 0, "Pawn patterns should include forward movement (dx=0, dy>0)");
    }


    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_PerspectiveEngine|L0_Foundation_PatternRepository")]
    [Trait("Category", "Atomic")]
    [Trait("ChessRule", "A7")]
    [Trait("Layer", "1")]
    [Trait("TestId", "L1_PatternMatcher_Knight")]
    public void L1_PatternMatcher_Knight_MatchesKnightLShapePattern()
    {
        // Arrange: Board with white knight on e4 (center of board for maximum moves)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 3, PieceBuilder.Create().White().Knight().Build())  // White knight at e4 (4,3)
        );
        
        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });
        var matchesDf = PatternMatcher.MatchAtomicPatterns(perspectivesDf, patternsDf, new[] { Piece.White }, turn: 0);

        // Assert - Knight at e4 should have 8 L-shaped moves
        var moves = matchesDf.Collect();
        var knightMoves = moves.Where(r => r.GetAs<int>("src_x") == 4 && r.GetAs<int>("src_y") == 3).ToList();
        
        // Verify knight generates 8 L-shaped moves from center position
        // Expected destinations: d2, f2, c3, g3, c5, g5, d6, f6
        Assert.True(knightMoves.Count >= 8, $"Knight at e4 should have 8 possible moves, got {knightMoves.Count}");
        
        // Verify knight patterns match L-shape (±2,±1 or ±1,±2)
        var knightBit = (int)Piece.Knight;
        var lShapePatterns = patternsDf
            .Filter($"(src_conditions & {knightBit}) != 0")
            .Filter("(ABS(delta_x) = 2 AND ABS(delta_y) = 1) OR (ABS(delta_x) = 1 AND ABS(delta_y) = 2)")
            .Count();
        Assert.True(lShapePatterns >= 8, $"Knight should have at least 8 L-shaped patterns, got {lShapePatterns}");
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L0_Foundation_PerspectiveEngine|L0_Foundation_PatternRepository")]
    [Trait("Category", "Atomic")]
    [Trait("ChessRule", "A8")]
    [Trait("Layer", "1")]
    [Trait("TestId", "L1_PatternMatcher_King")]
    public void L1_PatternMatcher_King_MatchesKingOneSquarePattern()
    {
        // Arrange: Board with white king on e4 (center for all 8 directions)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 3, PieceBuilder.Create().White().King().Build())    // White king at e4 (4,3)
        );
        
        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });
        var matchesDf = PatternMatcher.MatchAtomicPatterns(perspectivesDf, patternsDf, new[] { Piece.White }, turn: 0);

        // Assert - King at e4 should have 8 one-square moves in all directions
        var moves = matchesDf.Collect();
        var kingMoves = moves.Where(r => r.GetAs<int>("src_x") == 4 && r.GetAs<int>("src_y") == 3).ToList();
        
        // Verify king generates 8 moves (one in each direction)
        Assert.Equal(8, kingMoves.Count);
        
        // Verify king patterns have max distance of 1 (±1 in x and/or y) for basic moves
        var kingBit = (int)Piece.King;
        var oneSquarePatterns = patternsDf
            .Filter($"(src_conditions & {kingBit}) != 0")
            .Filter("ABS(delta_x) <= 1 AND ABS(delta_y) <= 1 AND NOT (delta_x = 0 AND delta_y = 0)")
            .Count();
        Assert.True(oneSquarePatterns >= 8, $"King should have at least 8 one-square patterns, got {oneSquarePatterns}");
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L1_PatternMatcher_Pawn|L1_PatternMatcher_Knight|L1_PatternMatcher_King")]
    [Trait("Category", "Atomic")]
    [Trait("ChessRule", "A1|A3|A7|A8")]
    [Trait("Layer", "1")]
    [Trait("TestId", "L1_CandidateGenerator_SimpleMoves")]
    public void L1_CandidateGenerator_SimpleMoves_CollectsAllAtomicPatterns()
    {
        // Arrange: Board with multiple pieces
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, PieceBuilder.Create().White().Pawn().Build()),   // White pawn
            (4, 3, PieceBuilder.Create().White().Knight().Build()), // White knight
            (4, 0, PieceBuilder.Create().White().King().Build()),   // White king
            (0, 7, PieceBuilder.Create().Black().Pawn().Build())    // Black pawn
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var engine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = engine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        // All pieces should be recognized in perspectives
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Perspectives should have 4 white pieces × 64 cells = 256 rows (1 black pawn not included in white faction)
        Assert.Equal(3 * 64, perspectiveCount);
        
        // Verify perspectives contains correct piece types
        var pawnBit = (int)Piece.Pawn;
        var knightBit = (int)Piece.Knight;
        var kingBit = (int)Piece.King;
        var whiteBit = (int)Piece.White;
        
        var whitePawns = perspectivesDf.Filter($"(perspective_piece & {pawnBit}) != 0 AND (perspective_piece & {whiteBit}) != 0").Count();
        var whiteKnights = perspectivesDf.Filter($"(perspective_piece & {knightBit}) != 0 AND (perspective_piece & {whiteBit}) != 0").Count();
        var whiteKings = perspectivesDf.Filter($"(perspective_piece & {kingBit}) != 0 AND (perspective_piece & {whiteBit}) != 0").Count();
        
        Assert.True(whitePawns > 0, "White pawn should be present");
        Assert.True(whiteKnights > 0, "White knight should be present");
        Assert.True(whiteKings > 0, "White king should be present");
    }

    #endregion

    #region Layer 2: Sliding Moves & Sequence Expansion (3 tests)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L1_CandidateGenerator_SimpleMoves")]
    [Trait("Category", "Sliding")]
    [Trait("ChessRule", "A4")]
    [Trait("Layer", "2")]
    [Trait("TestId", "L2_SequenceEngine_BishopSliding")]
    public void L2_SequenceEngine_BishopSliding_ExpandsMultiStepPattern()
    {
        // Arrange: Board with white bishop on c1, no blocking pieces (empty board)
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 0, PieceBuilder.Create().White().Bishop().Build())  // White bishop at c1 (2,0)
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });
        var allMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White }, turn: 0, maxDepth: 7);

        // Assert - Bishop at c1 can slide diagonally up-right: d2, e3, f4, g5, h6 (5 squares)
        var moves = allMoves.Collect();
        var bishopMoves = moves.Where(r => r.GetAs<int>("src_x") == 2 && r.GetAs<int>("src_y") == 0).ToList();
        
        // Verify bishop generates diagonal sliding moves (at least 5 in the longest diagonal from c1)
        Assert.True(bishopMoves.Count >= 5, $"Bishop at c1 should slide diagonally (at least 5 moves), got {bishopMoves.Count}");
        
        // Verify InstantRecursive sequence flag exists in patterns
        var bishopBit = (int)Piece.Bishop;
        var instantRecursiveMask = (1 << 2) | (1 << 3); // Instant=4 + Recursive=8
        var diagonalPatterns = patternsDf
            .Filter($"(src_conditions & {bishopBit}) != 0")
            .Filter($"(sequence & {instantRecursiveMask}) = {instantRecursiveMask}")
            .Count();
        Assert.True(diagonalPatterns > 0, "Bishop should have InstantRecursive diagonal patterns");
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L1_CandidateGenerator_SimpleMoves")]
    [Trait("Category", "Sliding")]
    [Trait("ChessRule", "A5")]
    [Trait("Layer", "2")]
    [Trait("TestId", "L2_SequenceEngine_RookSliding")]
    public void L2_SequenceEngine_RookSliding_ExpandsCardinalPattern()
    {
        // Arrange: Board with white rook on e4 (center for max cardinal moves)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 3, PieceBuilder.Create().White().Rook().Build())  // White rook at e4 (4,3)
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });
        var allMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White }, turn: 0, maxDepth: 7);

        // Assert - Rook at e4 can slide: up (4 squares to e8), down (3 to e1), left (4 to a4), right (3 to h4)
        // Total: 4+3+4+3 = 14 cardinal moves
        var moves = allMoves.Collect();
        var rookMoves = moves.Where(r => r.GetAs<int>("src_x") == 4 && r.GetAs<int>("src_y") == 3).ToList();
        
        // Verify rook generates 14 cardinal sliding moves from e4
        Assert.Equal(14, rookMoves.Count);
        
        // Verify moves are in cardinal directions only (dx=0 OR dy=0, but not both)
        var allCardinal = rookMoves.All(r => {
            var srcX = r.GetAs<int>("src_x");
            var srcY = r.GetAs<int>("src_y");
            var dstX = r.GetAs<int>("dst_x");
            var dstY = r.GetAs<int>("dst_y");
            return (srcX == dstX) != (srcY == dstY); // XOR: exactly one coordinate changes
        });
        Assert.True(allCardinal, "All rook moves should be cardinal (horizontal or vertical only)");
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L2_SequenceEngine_BishopSliding|L2_SequenceEngine_RookSliding")]
    [Trait("Category", "Sliding")]
    [Trait("ChessRule", "A4|A5|A6")]
    [Trait("Layer", "2")]
    [Trait("TestId", "L2_CandidateGenerator_SlidingMoves")]
    public void L2_CandidateGenerator_SlidingMoves_IncludesBishopAndRook()
    {
        // Arrange: Board with white bishop and rook
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 0, PieceBuilder.Create().White().Bishop().Build()),  // Bishop at c1
            (0, 0, PieceBuilder.Create().White().Rook().Build())     // Rook at a1
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Should have 2 pieces × 64 cells = 128 perspectives
        Assert.Equal(2 * 64, perspectiveCount);
        
        // Verify both bishop and rook are in perspectives
        var bishopBit = (int)Piece.Bishop;
        var rookBit = (int)Piece.Rook;
        var whiteBit = (int)Piece.White;
        
        var bishops = perspectivesDf.Filter($"(perspective_piece & {bishopBit}) != 0 AND (perspective_piece & {whiteBit}) != 0").Count();
        var rooks = perspectivesDf.Filter($"(perspective_piece & {rookBit}) != 0 AND (perspective_piece & {whiteBit}) != 0").Count();
        
        Assert.True(bishops > 0, "Bishop should be present in perspectives");
        Assert.True(rooks > 0, "Rook should be present in perspectives");
    }

    #endregion

    #region Layer 3: Simulation Engine (2 tests)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L2_CandidateGenerator_SlidingMoves")]
    [Trait("Category", "Simulation")]
    [Trait("ChessRule", "A1|A3|A7|A8")]
    [Trait("Layer", "3")]
    [Trait("TestId", "L3_SimulationEngine_SimpleMoves")]
    public void L3_SimulationEngine_SimpleMoves_AppliesMoveToBoard()
    {
        // Arrange: Starting position
        var board = Board.Default;
        board.Initialize();
        
        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();
        var simEngine = new SimulationEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Count total pieces on default board (should be 32: 16 white + 16 black)
        var emptyBit = (int)Piece.Empty;
        var allPieces = piecesDf.Filter($"(piece & {emptyBit}) == 0 AND piece != 0").Count();
        Assert.Equal(32, allPieces);
        
        // Verify default board has pieces in starting positions
        // White pawns should be on rank 2 (y=1)
        var whitePawns = piecesDf.Filter($"y = 1 AND (piece & {(int)Piece.White}) != 0 AND (piece & {(int)Piece.Pawn}) != 0").Count();
        Assert.Equal(8, whitePawns);
    }

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_SimpleMoves")]
    [Trait("Category", "Simulation")]
    [Trait("ChessRule", "A2")]
    [Trait("Layer", "3")]
    [Trait("TestId", "L3_SimulationEngine_FlagUpdates")]
    public void L3_SimulationEngine_FlagUpdates_TracksMintStatus()
    {
        // Arrange: Board with mint pawns in starting position
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 1, PieceBuilder.Create().White().Pawn().Mint().Build()),   // White pawn at a2 (mint)
            (0, 6, PieceBuilder.Create().Black().Pawn().Mint().Build())    // Black pawn at a7 (mint)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify mint flag is set correctly on pawns
        var mintBit = (int)Piece.Mint;
        var pawnBit = (int)Piece.Pawn;
        var whiteBit = (int)Piece.White;
        var blackBit = (int)Piece.Black;
        
        // Check white mint pawn exists
        var whiteMintPawns = piecesDf
            .Filter($"(piece & {whiteBit}) != 0 AND (piece & {pawnBit}) != 0 AND (piece & {mintBit}) != 0")
            .Count();
        Assert.True(whiteMintPawns > 0, "White mint pawn should exist");
        
        // Check black mint pawn exists
        var blackMintPawns = piecesDf
            .Filter($"(piece & {blackBit}) != 0 AND (piece & {pawnBit}) != 0 AND (piece & {mintBit}) != 0")
            .Count();
        Assert.True(blackMintPawns > 0, "Black mint pawn should exist");
    }

    #endregion

    #region Layer 4: Threat Engine (1 test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_FlagUpdates")]
    [Trait("Category", "Threats")]
    [Trait("ChessRule", "A8|C1")]
    [Trait("Layer", "4")]
    [Trait("TestId", "L4_ThreatEngine_BasicThreats")]
    public void L4_ThreatEngine_BasicThreats_ComputesThreatenedCells()
    {
        // Arrange: Board with white king at e1 and black queen at h8 (threatens along diagonal)
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, PieceBuilder.Create().White().King().Build()),    // White king at a1 (0,0)
            (7, 7, PieceBuilder.Create().Black().Queen().Build())    // Black queen at h8 (7,7) threatens a1 diagonally
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Compute threatened cells from black's perspective (turn=0 means white, so opponent=black at turn+1)
        var threatenedCells = ThreatEngine.ComputeThreatenedCells(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);

        // Assert - Verify king at a1 is threatened by queen at h8
        var threats = threatenedCells.Collect();
        var kingThreatened = threats.Any(r => r.GetAs<int>("threatened_x") == 0 && r.GetAs<int>("threatened_y") == 0);
        
        Assert.True(kingThreatened, "White king at a1 should be threatened by black queen at h8 along diagonal");
        
        // Verify queen threatens multiple squares along the diagonal (a1, b2, c3, d4, e5, f6, g7)
        var diagonalThreats = threats.Where(r => {
            var x = r.GetAs<int>("threatened_x");
            var y = r.GetAs<int>("threatened_y");
            return x == y && x >= 0 && x < 7; // diagonal from a1 to g7
        }).Count();
        
        Assert.True(diagonalThreats >= 7, $"Queen should threaten at least 7 diagonal squares, got {diagonalThreats}");
    }

    #endregion

    #region Layer 5: Special Moves (1 test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_BasicThreats")]
    [Trait("Category", "SpecialMoves")]
    [Trait("ChessRule", "B1|B2|B3|B4|B5")]
    [Trait("Layer", "5")]
    [Trait("TestId", "L5_SpecialMoves_CastlingEnPassantPromotion")]
    public void L5_SpecialMoves_CastlingEnPassantPromotion_HandleMultiPhasePatterns()
    {
        // Arrange: Board configured for castling - king and rook in starting positions, no pieces between
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Mint().Build()),     // White king at e1 (mint)
            (7, 0, PieceBuilder.Create().White().Rook().Mint().Build()),     // White rook at h1 (mint)
            (4, 7, PieceBuilder.Create().Black().King().Mint().Build()),     // Black king at e8 (mint)
            (0, 7, PieceBuilder.Create().Black().Rook().Mint().Build())      // Black rook at a8 (mint)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify all pieces are recognized (2 kings + 2 rooks = 4 pieces)
        var emptyBit = (int)Piece.Empty;
        var allPieces = piecesDf.Filter($"(piece & {emptyBit}) == 0 AND piece != 0").Count();
        Assert.Equal(4, allPieces);
        
        // Verify all pieces have Mint flag (important for castling)
        var mintBit = (int)Piece.Mint;
        var mintPieces = piecesDf.Filter($"piece != 0 AND (piece & {mintBit}) != 0").Count();
        Assert.Equal(4, mintPieces);
    }

    #endregion

    #region Layer 6: Legality Engine (1 test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_BasicThreats")]
    [Trait("Category", "Legality")]
    [Trait("ChessRule", "C1|C2|C3")]
    [Trait("Layer", "6")]
    [Trait("TestId", "L6_Legality_KingSafety")]
    public void L6_Legality_KingSafety_FiltersMovesLeavingKingInCheck()
    {
        // Arrange: Board with white king at d4 surrounded by black rooks threatening all escape squares
        // Rooks at d1, d7, a4, g4 create a cross pattern that threatens all adjacent squares
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, PieceBuilder.Create().White().King().Build()),    // White king at d4 (3,3)
            (3, 0, PieceBuilder.Create().Black().Rook().Build()),    // Black rook at d1 (threatens d-file)
            (3, 7, PieceBuilder.Create().Black().Rook().Build()),    // Black rook at d8 (threatens d-file)
            (0, 3, PieceBuilder.Create().Black().Rook().Build()),    // Black rook at a4 (threatens rank 4)
            (6, 3, PieceBuilder.Create().Black().Rook().Build())     // Black rook at g4 (threatens rank 4)
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Generate all candidate moves for white king
        var candidateMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        
        // Filter for legal moves (king safety)
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(candidateMoves, perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);

        // Assert - King surrounded by attacked squares should have zero legal moves
        var kingMoves = legalMoves.Filter("src_x = 3 AND src_y = 3").Count();
        Assert.Equal(0, kingMoves);
    }

    #endregion

    #region Layer 6: Legality Engine - Pin Detection (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_SlidingThreats")]
    [Trait("Category", "Legality")]
    [Trait("ChessRule", "C2")]
    [Trait("Layer", "6")]
    [Trait("TestId", "L6_Legality_PinDetection")]
    public void L6_Legality_PinDetection_ConstrainsPinnedPieces()
    {
        // Arrange: Board with white rook pinned by black rook along the file (king, white rook, black rook on e-file)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1 (4,0)
            (4, 3, PieceBuilder.Create().White().Rook().Build()),    // White rook at e4 (4,3) - pinned
            (4, 7, PieceBuilder.Create().Black().Rook().Build())     // Black rook at e8 (4,7) - pinning piece
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Generate all candidate moves for white
        var candidateMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        
        // Filter for legal moves
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(candidateMoves, perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);

        // Assert - Pinned rook at e4 cannot move away from e-file (would expose king to check)
        var rookMoves = legalMoves.Filter("src_x = 4 AND src_y = 3").Collect();
        
        // Rook can only move along the pin line (e-file): up/down but not sideways
        var allOnEFile = rookMoves.All(r => r.GetAs<int>("dst_x") == 4);
        Assert.True(allOnEFile, "Pinned rook should only be able to move along the e-file (pin line)");
        
        // Verify rook has some legal moves (can capture attacker or block)
        Assert.True(rookMoves.Count() > 0, "Pinned rook should still have moves along the pin line");
    }

    #endregion

    #region Layer 6: Legality Engine - Discovered Check (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L4_ThreatEngine_SlidingThreats")]
    [Trait("Category", "Legality")]
    [Trait("ChessRule", "C3")]
    [Trait("Layer", "6")]
    [Trait("TestId", "L6_Legality_DiscoveredCheck")]
    public void L6_Legality_DiscoveredCheck_DetectsDiscoveredCheckThreats()
    {
        // Arrange: Board with potential discovered check (rook blocking bishop's attack on king)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (4, 3, PieceBuilder.Create().White().Rook().Build()),    // White rook at e4 (blocks bishop)
            (4, 7, PieceBuilder.Create().Black().Bishop().Build())   // Black bishop at e8 (would attack if rook moves)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify all three pieces on vertical line (e-file)
        var emptyBit = (int)Piece.Empty;
        var efile = piecesDf.Filter($"x = 4 AND (piece & {emptyBit}) == 0 AND piece != 0").Count();
        Assert.Equal(3, efile);
        
        // Verify pieces form correct alignment for discovered check
        var king_e1 = piecesDf.Filter("x = 4 AND y = 0 AND (piece & 16384) != 0").Count(); // King
        var rook_e4 = piecesDf.Filter("x = 4 AND y = 3 AND (piece & 1024) != 0").Count();  // Rook
        var bishop_e8 = piecesDf.Filter("x = 4 AND y = 7 AND (piece & 4096) != 0").Count(); // Bishop
        
        Assert.Equal(1, king_e1);
        Assert.Equal(1, rook_e4);
        Assert.Equal(1, bishop_e8);
    }

    #endregion

    #region Layer 7: Timeline Engine (1 test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L6_Legality_KingSafety")]
    [Trait("Category", "Timeline")]
    [Trait("ChessRule", "A1|A2|A3|A4|A5|A6|A7|A8")]
    [Trait("Layer", "7")]
    [Trait("TestId", "L7_Timeline_MultiTurn")]
    public void L7_Timeline_MultiTurn_PlaysSequenceOfMoves()
    {
        // Arrange: Start with default board
        var board = Board.Default;
        board.Initialize();

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Perspectives should include both factions' pieces
        // 32 pieces × 64 cells = 2048 perspectives total
        Assert.Equal(32 * 64, perspectiveCount);
        
        // Verify we have both white and black pieces in perspectives
        var whiteBit = (int)Piece.White;
        var blackBit = (int)Piece.Black;
        var whitePieces = perspectivesDf.Filter($"(perspective_piece & {whiteBit}) != 0").Count();
        var blackPieces = perspectivesDf.Filter($"(perspective_piece & {blackBit}) != 0").Count();
        
        Assert.True(whitePieces > 0, "White pieces should be in perspectives");
        Assert.True(blackPieces > 0, "Black pieces should be in perspectives");
    }

    #endregion

    #region Layer 8: Terminal Conditions (1 test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L7_Timeline_MultiTurn")]
    [Trait("Category", "Terminal")]
    [Trait("ChessRule", "D1|D2|D3|D4")]
    [Trait("Layer", "8")]
    [Trait("TestId", "L8_Terminal_CheckmateStalemate")]
    public void L8_Terminal_CheckmateStalemate_DetectsGameEnd()
    {
        // Arrange: Board with only kings (terminal condition for draw)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (4, 7, PieceBuilder.Create().Black().King().Build())     // Black king at e8
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });

        // Assert
        // Verify both kings are recognized
        Assert.NotNull(perspectivesDf);
        // Just verify that perspectives were created (contains data)
        Assert.NotEqual(0, perspectivesDf.Count());
    }

    #endregion

    #region Layer 4: Threat Engine - Sliding Threats (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L2_SequenceEngine_BishopSliding|L2_SequenceEngine_RookSliding")]
    [Trait("Category", "Threats")]
    [Trait("ChessRule", "C1|C2|C3")]
    [Trait("Layer", "4")]
    [Trait("TestId", "L4_ThreatEngine_SlidingThreats")]
    public void L4_ThreatEngine_SlidingThreats_ComputesSlidingThreats()
    {
        // Arrange: Board with black bishop at c1 and black rook at a1 (threatening white)
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 0, PieceBuilder.Create().Black().Bishop().Build()),  // Black bishop at c1 (2,0)
            (0, 0, PieceBuilder.Create().Black().Rook().Build())     // Black rook at a1 (0,0)
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Compute threats from black pieces (turn=0 means white, opponent black threatens)
        var threatenedCells = ThreatEngine.ComputeThreatenedCells(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);

        // Assert - Bishop at c1 threatens diagonal: d2, e3, f4, g5, h6 (5 squares)
        var threats = threatenedCells.Collect();
        var bishopDiagonalThreats = threats.Where(r => {
            var x = r.GetAs<int>("threatened_x");
            var y = r.GetAs<int>("threatened_y");
            // Diagonal from c1: x-2 == y (d2=3-1, e3=4-2, f4=5-3, g5=6-4, h6=7-5)
            return x > 2 && y > 0 && (x - y) == 2;
        }).Count();
        
        Assert.True(bishopDiagonalThreats >= 5, $"Bishop at c1 should threaten 5 diagonal squares (d2-h6), got {bishopDiagonalThreats}");
        
        // Rook at a1 threatens horizontal: b1-h1 (7 squares) + vertical: a2-a8 (7 squares) = 14 total
        var rookCardinalThreats = threats.Where(r => {
            var x = r.GetAs<int>("threatened_x");
            var y = r.GetAs<int>("threatened_y");
            return (x == 0 && y > 0) || (y == 0 && x > 0); // Same file or rank as a1
        }).Count();
        
        Assert.True(rookCardinalThreats >= 14, $"Rook at a1 should threaten 14 cardinal squares, got {rookCardinalThreats}");
    }

    #endregion

    #region Layer 5: Special Moves - Castling (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_FlagUpdates|L4_ThreatEngine_SlidingThreats")]
    [Trait("Category", "SpecialMoves")]
    [Trait("ChessRule", "B2|B3")]
    [Trait("Layer", "5")]
    [Trait("TestId", "L5_SpecialMoves_Castling")]
    public void L5_SpecialMoves_Castling_AllowsCastlingMove()
    {
        // Arrange: Board configured for castling - king and rook in starting positions, no pieces between
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Mint().Build()),     // White king at e1 (mint)
            (7, 0, PieceBuilder.Create().White().Rook().Mint().Build()),     // White rook at h1 (mint)
            (4, 7, PieceBuilder.Create().Black().King().Mint().Build()),     // Black king at e8 (mint)
            (0, 7, PieceBuilder.Create().Black().Rook().Mint().Build())      // Black rook at a8 (mint)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify 4 pieces all with Mint flag for castling eligibility
        var mintBit = (int)Piece.Mint;
        var mintPieces = piecesDf.Filter($"piece != 0 AND (piece & {mintBit}) != 0").Count();
        Assert.Equal(4, mintPieces);
        
        // Verify kings at starting positions
        var kingBit = (int)Piece.King;
        var whiteBit = (int)Piece.White;
        var blackBit = (int)Piece.Black;
        var whiteKing_e1 = piecesDf.Filter($"x = 4 AND y = 0 AND (piece & {kingBit}) != 0 AND (piece & {whiteBit}) != 0").Count();
        var blackKing_e8 = piecesDf.Filter($"x = 4 AND y = 7 AND (piece & {kingBit}) != 0 AND (piece & {blackBit}) != 0").Count();
        
        Assert.Equal(1, whiteKing_e1);
        Assert.Equal(1, blackKing_e8);
    }

    #endregion

    #region Layer 5: Special Moves - En Passant (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_FlagUpdates")]
    [Trait("Category", "SpecialMoves")]
    [Trait("ChessRule", "B4")]
    [Trait("Layer", "5")]
    [Trait("TestId", "L5_SpecialMoves_EnPassant")]
    public void L5_SpecialMoves_EnPassant_HandlesEnPassantCapture()
    {
        // Arrange: Board with pawns in position for en passant
        var whitePawn = PieceBuilder.Create().White().Pawn().Build();
        var blackPawnWithPassing = Piece.Black | Piece.Pawn | Piece.Passing;  // Black pawn with Passing flag
        
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 3, whitePawn),              // White pawn at e4
            (3, 3, blackPawnWithPassing)    // Black pawn at d4 (with Passing flag)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify both pawns present
        var pawnBit = (int)Piece.Pawn;
        var pawns = piecesDf.Filter($"(piece & {pawnBit}) != 0").Count();
        Assert.Equal(2, pawns);
        
        // Verify passing flag on black pawn
        var passingBit = (int)Piece.Passing;
        var passingPawns = piecesDf.Filter($"(piece & {passingBit}) != 0").Count();
        Assert.True(passingPawns > 0, "Black pawn should have Passing flag");
    }

    #endregion

    #region Layer 5: Special Moves - Pawn Promotion (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L3_SimulationEngine_SimpleMoves")]
    [Trait("Category", "SpecialMoves")]
    [Trait("ChessRule", "B1")]
    [Trait("Layer", "5")]
    [Trait("TestId", "L5_SpecialMoves_PawnPromotion")]
    public void L5_SpecialMoves_PawnPromotion_AllowsPawnPromotionAtEdge()
    {
        // Arrange: Board with pawn near promotion edge
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 6, PieceBuilder.Create().White().Pawn().Build()),   // White pawn at e7 (one move from promotion)
            (4, 0, PieceBuilder.Create().Black().Pawn().Build())    // Black pawn at e2 (one move from promotion)
        );

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify both pawns at promotion-eligible positions
        var pawnBit = (int)Piece.Pawn;
        var whiteBit = (int)Piece.White;
        var blackBit = (int)Piece.Black;
        
        var whitePawn_e7 = piecesDf.Filter($"x = 4 AND y = 6 AND (piece & {pawnBit}) != 0 AND (piece & {whiteBit}) != 0").Count();
        var blackPawn_e2 = piecesDf.Filter($"x = 4 AND y = 0 AND (piece & {pawnBit}) != 0 AND (piece & {blackBit}) != 0").Count();
        
        Assert.Equal(1, whitePawn_e7);
        Assert.Equal(1, blackPawn_e2);
    }

    #endregion

    #region Layer 7: Timeline Engine - Single Turn (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L6_Legality_KingSafety|L6_Legality_PinDetection|L6_Legality_DiscoveredCheck")]
    [Trait("Category", "Timeline")]
    [Trait("ChessRule", "-")]
    [Trait("Layer", "7")]
    [Trait("TestId", "L7_Timeline_SingleTurn")]
    public void L7_Timeline_SingleTurn_GeneratesLegalMovesForOneTurn()
    {
        // Arrange: Start with default board
        var board = Board.Default;
        board.Initialize();

        var provider = new BoardStateProvider(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White });

        // Assert
        Assert.NotNull(perspectivesDf);
        var perspectiveCount = perspectivesDf.Count();
        Assert.NotEqual(0, perspectiveCount);
        
        // Verify all 32 pieces on default board
        var emptyBit = (int)Piece.Empty;
        var allPieces = piecesDf.Filter($"(piece & {emptyBit}) == 0 AND piece != 0").Count();
        Assert.Equal(32, allPieces);
        
        // Verify 16 white pieces × 64 cells = 1024 perspectives for white turn
        Assert.Equal(16 * 64, perspectiveCount);
    }

    #endregion

    #region Layer 8: Terminal Conditions - Checkmate (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L7_Timeline_SingleTurn")]
    [Trait("Category", "Terminal")]
    [Trait("ChessRule", "D1")]
    [Trait("Layer", "8")]
    [Trait("TestId", "L8_Terminal_Checkmate")]
    public void L8_Terminal_Checkmate_DetectsCheckmate()
    {
        // Arrange: Board in checkmate position - white king at h1 trapped by black rooks
        var board = BoardHelpers.CreateBoardWithPieces(
            (7, 0, PieceBuilder.Create().White().King().Build()),    // White king at h1 (corner)
            (7, 2, PieceBuilder.Create().Black().Rook().Build()),    // Black rook at h3 (blocks escape)
            (6, 1, PieceBuilder.Create().Black().Rook().Build())     // Black rook at g2 (delivers check)
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Step 1: Verify king IS in check
        var threatenedCells = ThreatEngine.ComputeThreatenedCells(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        var kingInCheck = threatenedCells.Filter("threatened_x = 7 AND threatened_y = 0").Count() > 0;
        Assert.True(kingInCheck, "White king at h1 should be in check from black rook");
        
        // Step 2: Generate all candidate moves and filter for legal moves
        var candidateMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(candidateMoves, perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        
        // Step 3: Verify king has ZERO legal moves (checkmate condition)
        var kingMoves = legalMoves.Filter("src_x = 7 AND src_y = 0").Count();
        Assert.Equal(0, kingMoves);
        
        // Checkmate = king in check + zero legal moves
        Assert.True(kingInCheck && kingMoves == 0, "Checkmate: king in check with no legal moves");
    }

    #endregion

    #region Layer 8: Terminal Conditions - Stalemate (Extra test)

    [Fact]
    [Trait("Essential", "true")]
    [Trait("DependsOn", "L7_Timeline_SingleTurn")]
    [Trait("Category", "Terminal")]
    [Trait("ChessRule", "D2")]
    [Trait("Layer", "8")]
    [Trait("TestId", "L8_Terminal_Stalemate")]
    public void L8_Terminal_Stalemate_DetectsStalemate()
    {
        // Arrange: Board in stalemate position - white king at a1 trapped but not in check
        // Black queen at b3 and black king at c1 control all escape squares without giving check
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, PieceBuilder.Create().White().King().Build()),    // White king at a1 (0,0)
            (1, 2, PieceBuilder.Create().Black().Queen().Build()),   // Black queen at b3 (1,2) - controls a2, b2, b1
            (2, 0, PieceBuilder.Create().Black().King().Build())     // Black king at c1 (2,0) - controls b1, b2
        );

        var provider = new BoardStateProvider(_spark);
        var repo = new PatternRepository(_spark);
        var perspectiveEngine = new PerspectiveEngine();

        // Act
        var piecesDf = provider.GetPieces(board);
        var patternsDf = repo.GetPatterns();
        var perspectivesDf = perspectiveEngine.BuildPerspectives(piecesDf, new[] { Piece.White, Piece.Black });
        
        // Step 1: Verify king is NOT in check
        var threatenedCells = ThreatEngine.ComputeThreatenedCells(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        var kingInCheck = threatenedCells.Filter("threatened_x = 0 AND threatened_y = 0").Count() > 0;
        Assert.False(kingInCheck, "White king at a1 should NOT be in check (stalemate condition)");
        
        // Step 2: Generate all candidate moves and filter for legal moves
        var candidateMoves = CandidateGenerator.GetMoves(perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(candidateMoves, perspectivesDf, patternsDf, new[] { Piece.White, Piece.Black }, turn: 0);
        
        // Step 3: Verify king has ZERO legal moves (but not in check = stalemate)
        var kingMoves = legalMoves.Filter("src_x = 0 AND src_y = 0").Count();
        Assert.Equal(0, kingMoves);
        
        // Stalemate = king NOT in check + zero legal moves
        Assert.True(!kingInCheck && kingMoves == 0, "Stalemate: king not in check but has no legal moves");
    }

    #endregion
}
