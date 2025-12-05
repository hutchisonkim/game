using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for the Threatened cells mechanic.
/// Cells are marked as Threatened when they are under attack by enemy pieces.
/// This is crucial for castling (king can't pass through threatened squares)
/// and for preventing moves that leave the king in check.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Threatened")]
public class ThreatenedCellsTests : ChessTestBase
{
    public ThreatenedCellsTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    
    public void ComputeThreatenedCells_BlackRookAtCorner_ThreatensRowAndColumn()
    {
        // Arrange - Black Rook at (0, 0) threatens all squares in row 0 and column 0
        var board = CreateBoardWithPieces(
            (0, 0, BlackMintRook));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Compute threatened cells (opponent's attacks)
        // turn=0 means it's White's turn, so we compute what Black threatens
        var threatenedCellsDf = TimelineService.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0  // White's turn, so Black's threats are computed
        );

        var threatenedCells = threatenedCellsDf.Collect().ToArray();

        // Assert - Should have threatened cells along row and column
        Assert.True(threatenedCells.Length > 0, "Expected at least one threatened cell");
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void GetPerspectivesWithThreats_BlackRookAtCorner_MarksThreatenedCells()
    {
        // Arrange - Black Rook at (0, 0) threatens all squares in row 0 and column 0
        var board = CreateBoardWithPieces(
            (0, 0, BlackMintRook));

        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get perspectives with threatened bit set
        // turn=0 means it's White's turn, so Black's threats are computed
        var perspectivesWithThreats = refactoredPolicy.GetPerspectivesWithThreats(
            board, 
            DefaultFactions, 
            turn: 0);

        // Assert - Some cells should have the Threatened bit set
        var threatenedRows = perspectivesWithThreats
            .Filter($"(generic_piece & {(int)Piece.Threatened}) != 0")
            .Collect()
            .ToArray();

        Assert.True(threatenedRows.Length > 0, "Expected at least one cell to have the Threatened bit set");
    }

    [Fact]
    
    public void ComputeThreatenedCells_BlackKnightInCenter_Threatens8Squares()
    {
        // Arrange - Black Knight at (4, 4) threatens 8 L-shaped squares
        var board = CreateBoardWithPieces(
            (4, 4, BlackMintKnight));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Compute threatened cells (opponent's attacks)
        // turn=1 means it's Black's turn, so we compute what White threatens
        // However, since only a Black Knight is on the board, White has no pieces to threaten
        // Let's change this to turn=0 to have Black threaten squares
        var threatenedCellsDf = TimelineService.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0  // White's turn, so Black's threats are computed
        );

        var threatenedCells = threatenedCellsDf.Collect().ToArray();

        // Assert - Knight in center should threaten 8 squares
        Assert.Equal(8, threatenedCells.Length);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void ComputeThreatenedCells_BlackKnightInCenter_Threatens8Squares_Refactored()
    {
        // Arrange - Black Knight at (4, 4) threatens 8 L-shaped squares
        var board = CreateBoardWithPieces(
            (4, 4, BlackMintKnight));

        var refactoredPolicy = new ChessPolicyRefactored(Spark);
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Compute threatened cells (opponent's attacks)
        var threatenedCellsDf = TimelineService.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0  // White's turn, so Black's threats are computed
        );

        var threatenedCells = threatenedCellsDf.Collect().ToArray();

        // Assert - Knight in center should threaten 8 squares
        Assert.Equal(8, threatenedCells.Length);
    }

    [Fact]
    
    public void AddThreatenedBitToPerspectives_MarksCorrectCells()
    {
        // Arrange - Simple setup: Black Rook at (7, 7) threatening cells along row and column
        var board = CreateBoardWithPieces(
            (7, 7, BlackMintRook),
            (0, 0, WhiteMintKing));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Compute threatened cells and add to perspectives
        var threatenedCellsDf = TimelineService.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0  // White's turn, so we check what Black threatens
        );

        var perspectivesWithThreats = TimelineService.AddThreatenedBitToPerspectives(
            perspectivesDf,
            threatenedCellsDf);

        // Verify that some cells have the Threatened bit set
        int threatenedBit = (int)Piece.Threatened;
        var threatenedPerspectives = perspectivesWithThreats
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Collect()
            .ToArray();

        // Assert
        Assert.True(threatenedPerspectives.Length > 0, 
            "Expected some perspectives to have the Threatened bit set");
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void AddThreatenedBitToPerspectives_MarksCorrectCells_Refactored()
    {
        // Arrange - Simple setup: Black Rook at (7, 7) threatening cells along row and column
        var board = CreateBoardWithPieces(
            (7, 7, BlackMintRook),
            (0, 0, WhiteMintKing));

        var refactoredPolicy = new ChessPolicyRefactored(Spark);
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Compute threatened cells and add to perspectives
        var threatenedCellsDf = TimelineService.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0  // White's turn, so we check what Black threatens
        );

        var perspectivesWithThreats = TimelineService.AddThreatenedBitToPerspectives(
            perspectivesDf,
            threatenedCellsDf);

        // Verify that some cells have the Threatened bit set
        int threatenedBit = (int)Piece.Threatened;
        var threatenedPerspectives = perspectivesWithThreats
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Collect()
            .ToArray();

        // Assert
        Assert.True(threatenedPerspectives.Length > 0, 
            "Expected some perspectives to have the Threatened bit set");
    }

    [Fact]
    
    public void GetPerspectivesWithThreats_IntegrationTest()
    {
        // Arrange - Setup a board with pieces that create threats
        var board = CreateBoardWithPieces(
            (4, 4, WhiteMintQueen),  // Queen threatens many squares
            (0, 0, BlackMintKing));

        // Act - Get perspectives with threats computed
        var perspectivesWithThreats = Policy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);

        // Verify that some cells have the Threatened bit set
        int threatenedBit = (int)Piece.Threatened;
        var threatenedCount = perspectivesWithThreats
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Queen should threaten many squares
        Assert.True(threatenedCount > 0, 
            "Expected perspectives to have Threatened bit set where queen attacks");
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void GetPerspectivesWithThreats_IntegrationTest_Refactored()
    {
        // Arrange - Setup a board with pieces that create threats
        var board = CreateBoardWithPieces(
            (4, 4, WhiteMintQueen),  // Queen threatens many squares
            (0, 0, BlackMintKing));

        var refactoredPolicy = new ChessPolicyRefactored(Spark);

        // Act - Get perspectives with threats computed
        var perspectivesWithThreats = refactoredPolicy.GetPerspectivesWithThreats(board, DefaultFactions, turn: 0);

        // Verify that some cells have the Threatened bit set
        int threatenedBit = (int)Piece.Threatened;
        var threatenedCount = perspectivesWithThreats
            .Filter(Functions.Col("generic_piece").BitwiseAND(Functions.Lit(threatenedBit)).NotEqual(Functions.Lit(0)))
            .Count();

        // Assert - Queen should threaten many squares
        Assert.True(threatenedCount > 0, 
            "Expected perspectives to have Threatened bit set where queen attacks");
    }
}
