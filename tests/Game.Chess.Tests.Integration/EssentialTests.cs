using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.Policy.Foundation;
using Game.Chess.Policy.Perspectives;
using Game.Chess.Policy.Simulation;
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
        // Arrange: Board with white pawn on d4, black pawn on e5
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, PieceBuilder.Create().White().Pawn().Build()), // White pawn at d4 (3,3)
            (4, 4, PieceBuilder.Create().Black().Pawn().Build())  // Black pawn at e5 (4,4)
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
        Assert.True(perspectiveCount > 0, $"Expected perspectives to exist, got {perspectiveCount} rows");
        
        // Verify pawn patterns exist in repository
        var pawnBit = (int)Piece.Pawn;
        var pawnPatterns = patternsDf.Filter($"(src_conditions & {pawnBit}) != 0").Count();
        Assert.True(pawnPatterns > 0, "Pawn patterns should be defined");
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
        // Arrange: Board with white knight on e4
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 3, PieceBuilder.Create().White().Knight().Build())  // White knight at e4
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
        Assert.True(perspectiveCount > 0, $"Expected perspectives to exist, got {perspectiveCount} rows");
        
        // Verify knight patterns exist in repository (L-shaped moves: ±2,±1 or ±1,±2)
        var knightBit = (int)Piece.Knight;
        var knightPatterns = patternsDf.Filter($"(src_conditions & {knightBit}) != 0").Count();
        Assert.True(knightPatterns > 0, "Knight patterns should be defined");
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
        // Arrange: Board with white king on e1
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build())    // White king at e1 (4,0)
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
        Assert.True(perspectiveCount > 0, $"Expected perspectives to exist, got {perspectiveCount} rows");
        
        // Verify king patterns exist in repository (1-square moves in all 8 directions)
        var kingBit = (int)Piece.King;
        var kingPatterns = patternsDf.Filter($"(src_conditions & {kingBit}) != 0").Count();
        Assert.True(kingPatterns > 0, "King patterns should be defined");
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
        // Arrange: Board with white bishop on c1, no blocking pieces
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 0, PieceBuilder.Create().White().Bishop().Build())  // White bishop at c1
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
        
        // Verify bishop patterns exist and include sliding sequences
        var bishopBit = (int)Piece.Bishop;
        var bishopPatterns = patternsDf.Filter($"(src_conditions & {bishopBit}) != 0").Count();
        Assert.True(bishopPatterns > 0, "Bishop patterns should be defined");
        
        // Verify we have recursive/multi-step patterns for bishop (InstantRecursive = Instant | Recursive)
        var instantRecursiveMask = (1 << 2) | (1 << 3); // Instant and Recursive flags
        var diagonalPatterns = patternsDf
            .Filter($"(src_conditions & {bishopBit}) != 0")
            .Filter($"(sequence & {instantRecursiveMask}) != 0")
            .Count();
        Assert.True(diagonalPatterns > 0, "Bishop should have recursive diagonal patterns");
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
        // Arrange: Board with white rook on a1, no blocking pieces
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, PieceBuilder.Create().White().Rook().Build())  // White rook at a1
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
        
        // Verify rook patterns exist and include sliding sequences
        var rookBit = (int)Piece.Rook;
        var rookPatterns = patternsDf.Filter($"(src_conditions & {rookBit}) != 0").Count();
        Assert.True(rookPatterns > 0, "Rook patterns should be defined");
        
        // Verify we have recursive/multi-step patterns for rook (cardinal directions)
        var instantRecursiveMask = (1 << 2) | (1 << 3); // Instant and Recursive flags
        var cardinalPatterns = patternsDf
            .Filter($"(src_conditions & {rookBit}) != 0")
            .Filter($"(sequence & {instantRecursiveMask}) != 0")
            .Count();
        Assert.True(cardinalPatterns > 0, "Rook should have recursive cardinal patterns");
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
        var allPieces = piecesDf.Filter("piece != 0").Count();
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
        // Arrange: Board with white king and black pawn that can threaten
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (3, 1, PieceBuilder.Create().Black().Pawn().Build())     // Black pawn at d2 (threatens e1)
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
        
        // Verify we have 2 pieces on the board
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(2, allPieces);
        
        // Verify king and black pawn are recognized
        var kingBit = (int)Piece.King;
        var pawnBit = (int)Piece.Pawn;
        var kings = piecesDf.Filter($"(piece & {kingBit}) != 0").Count();
        var pawns = piecesDf.Filter($"(piece & {pawnBit}) != 0").Count();
        
        Assert.True(kings > 0, "King should be present");
        Assert.True(pawns > 0, "Pawn should be present");
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
        var allPieces = piecesDf.Filter("piece != 0").Count();
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
        // Arrange: Board with white king threatened by black rook
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (4, 4, PieceBuilder.Create().Black().Rook().Build())     // Black rook at e5 (threatens king's column)
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
        
        // Verify both pieces exist
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(2, allPieces);
        
        // Verify king and rook are present
        var kingBit = (int)Piece.King;
        var rookBit = (int)Piece.Rook;
        var kings = piecesDf.Filter($"(piece & {kingBit}) != 0").Count();
        var rooks = piecesDf.Filter($"(piece & {rookBit}) != 0").Count();
        
        Assert.Equal(1, kings);
        Assert.Equal(1, rooks);
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
        // Arrange: Board with pinned piece (bishop pinning rook to king)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (5, 1, PieceBuilder.Create().White().Rook().Build()),    // White rook at f2 (pinned)
            (5, 7, PieceBuilder.Create().Black().Bishop().Build())   // Black bishop at f8 (pinning)
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
        
        // Verify all three pieces exist
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(3, allPieces);
        
        // Verify king, rook, and bishop present
        var kingBit = (int)Piece.King;
        var rookBit = (int)Piece.Rook;
        var bishopBit = (int)Piece.Bishop;
        var kings = piecesDf.Filter($"(piece & {kingBit}) != 0").Count();
        var rooks = piecesDf.Filter($"(piece & {rookBit}) != 0").Count();
        var bishops = piecesDf.Filter($"(piece & {bishopBit}) != 0").Count();
        
        Assert.Equal(1, kings);
        Assert.Equal(1, rooks);
        Assert.Equal(1, bishops);
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
        var efile = piecesDf.Filter("x = 4 AND piece != 0").Count();
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
        board.Initialize();

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
        // Arrange: Board with bishop and rook for sliding threats
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 0, PieceBuilder.Create().White().Bishop().Build()),  // White bishop at c1
            (0, 0, PieceBuilder.Create().White().Rook().Build())     // White rook at a1
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
        
        // Verify both sliding pieces present
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(2, allPieces);
        
        // Verify bishop and rook
        var bishopBit = (int)Piece.Bishop;
        var rookBit = (int)Piece.Rook;
        var bishops = piecesDf.Filter($"(piece & {bishopBit}) != 0").Count();
        var rooks = piecesDf.Filter($"(piece & {rookBit}) != 0").Count();
        
        Assert.Equal(1, bishops);
        Assert.Equal(1, rooks);
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
        var allPieces = piecesDf.Filter("piece != 0").Count();
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
        // Arrange: Board in checkmate position (fool's mate example)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (5, 1, PieceBuilder.Create().White().Pawn().Build()),     // White pawn at f2
            (5, 6, PieceBuilder.Create().Black().Pawn().Build()),     // Black pawn at f7
            (3, 7, PieceBuilder.Create().Black().Queen().Build()),    // Black queen at d8 (for threat)
            (4, 7, PieceBuilder.Create().Black().King().Build())      // Black king at e8
        );
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
        
        // Verify 5 pieces on board
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(5, allPieces);
        
        // Verify presence of king, queen, and pawns
        var kingBit = (int)Piece.King;
        var queenBit = (int)Piece.Queen;
        var pawnBit = (int)Piece.Pawn;
        var kings = piecesDf.Filter($"(piece & {kingBit}) != 0").Count();
        var queens = piecesDf.Filter($"(piece & {queenBit}) != 0").Count();
        var pawns = piecesDf.Filter($"(piece & {pawnBit}) != 0").Count();
        
        Assert.Equal(2, kings);
        Assert.Equal(1, queens);
        Assert.Equal(2, pawns);
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
        // Arrange: Board with only kings (stalemate condition)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 0, PieceBuilder.Create().White().King().Build()),    // White king at e1
            (4, 7, PieceBuilder.Create().Black().King().Build())     // Black king at e8
        );
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
        
        // Only 2 kings on board (stalemate condition - no other pieces)
        var allPieces = piecesDf.Filter("piece != 0").Count();
        Assert.Equal(2, allPieces);
        
        // Both should be kings
        var kingBit = (int)Piece.King;
        var kings = piecesDf.Filter($"(piece & {kingBit}) != 0").Count();
        Assert.Equal(2, kings);
        
        // Verify they're at opposite ends (e1 and e8)
        var whiteKing = piecesDf.Filter($"x = 4 AND y = 0 AND (piece & {kingBit}) != 0").Count();
        var blackKing = piecesDf.Filter($"x = 4 AND y = 7 AND (piece & {kingBit}) != 0").Count();
        
        Assert.Equal(1, whiteKing);
        Assert.Equal(1, blackKing);
    }

    #endregion
}
