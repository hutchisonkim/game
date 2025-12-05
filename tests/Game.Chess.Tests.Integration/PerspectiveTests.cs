using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for perspective generation and generic flag assignment (Self/Ally/Foe).
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "Perspective")]
public class PerspectiveTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public PerspectiveTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    
    public void StandardBoard_GetPerspectives_ContainsSelfAllyFoeFlags()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        // Act
        var perspectivesDf = _policy.GetPerspectives(board, factions);

        // Assert
        Assert.Contains("generic_piece", perspectivesDf.Columns());
        Assert.Contains("perspective_id", perspectivesDf.Columns());

        var genericValues = perspectivesDf.Collect().Select(r => r.GetAs<int>("generic_piece")).ToList();
        bool anyHasFlags = genericValues.Any(g => 
            (g & (int)ChessPolicy.Piece.Self) != 0 ||
            (g & (int)ChessPolicy.Piece.Ally) != 0 ||
            (g & (int)ChessPolicy.Piece.Foe) != 0);
        
        Assert.True(anyHasFlags, "No perspective rows contain Self/Ally/Foe flags in generic_piece");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void StandardBoard_GetPerspectives_ContainsSelfAllyFoeFlags_Refactored()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, factions);

        // Assert
        Assert.Contains("generic_piece", perspectivesDf.Columns());
        Assert.Contains("perspective_id", perspectivesDf.Columns());

        var genericValues = perspectivesDf.Collect().Select(r => r.GetAs<int>("generic_piece")).ToList();
        bool anyHasFlags = genericValues.Any(g => 
            (g & (int)ChessPolicy.Piece.Self) != 0 ||
            (g & (int)ChessPolicy.Piece.Ally) != 0 ||
            (g & (int)ChessPolicy.Piece.Foe) != 0);
        
        Assert.True(anyHasFlags, "No perspective rows contain Self/Ally/Foe flags in generic_piece");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void RefactoredPolicy_GetPerspectives_ProducesSameResultAsOriginal()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var originalPerspectives = _policy.GetPerspectives(board, factions);
        var refactoredPerspectives = refactoredPolicy.GetPerspectives(board, factions);

        // Assert - Both should produce the same number of rows
        var originalCount = originalPerspectives.Count();
        var refactoredCount = refactoredPerspectives.Count();
        Assert.Equal(originalCount, refactoredCount);

        // Assert - Both should have the same schema
        var originalColumns = originalPerspectives.Columns().OrderBy(c => c).ToArray();
        var refactoredColumns = refactoredPerspectives.Columns().OrderBy(c => c).ToArray();
        Assert.Equal(originalColumns, refactoredColumns);
    }

    [Fact]
    
    public void StandardBoard_GetPerspectives_SetsSelfAllyFoeFlagsCorrectly()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        // Act
        var perspectivesDf = _policy.GetPerspectives(board, factions);

        // Assert Self flag - Same cell perspective (white pawn at 0,1 looking at itself)
        var sameCellRows = perspectivesDf
            .Filter("x = 0 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(sameCellRows.Any(), "Expected at least one perspective row for the same cell (0,1)");
        bool anySelf = sameCellRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Self) != 0);
        Assert.True(anySelf, "Expected same-cell perspective to include the Self flag");

        // Assert Ally flag - White pawn at (1,1) from perspective of white pawn at (0,1)
        var allyRows = perspectivesDf
            .Filter("x = 1 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(allyRows.Any(), "Expected at least one perspective row for an allied piece");
        bool anyAlly = allyRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Ally) != 0);
        Assert.True(anyAlly, "Expected allied piece to include the Ally flag");

        // Assert Foe flag - Black pawn at (0,6) from perspective of white pawn at (0,1)
        var foeRows = perspectivesDf
            .Filter("x = 0 AND y = 6 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(foeRows.Any(), "Expected at least one perspective row for a foe piece");
        bool anyFoe = foeRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Foe) != 0);
        Assert.True(anyFoe, "Expected enemy piece to include the Foe flag");
    }

    [Fact]
    
    [Trait("Category", "Slow")]
    public void StandardBoard_BuildTimeline_GeneratesMultipleTimesteps()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var perspectivesDf = _policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();

        // Act
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, factions, maxDepth: 2);

        // Assert
        Assert.Contains("timestep", timelineDf.Columns());
        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        
        // For maxDepth=2, we expect timesteps 0 and 1
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
    }

    [Fact]
    
    [Trait("Category", "Slow")]
    
    [Trait("Refactored", "True")]
    public void StandardBoard_GetPerspectives_SetsSelfAllyFoeFlagsCorrectly_Refactored()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, factions);

        // Assert Self flag - Same cell perspective (white pawn at 0,1 looking at itself)
        var sameCellRows = perspectivesDf
            .Filter("x = 0 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(sameCellRows.Any(), "Expected at least one perspective row for the same cell (0,1)");
        bool anySelf = sameCellRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Self) != 0);
        Assert.True(anySelf, "Expected same-cell perspective to include the Self flag");

        // Assert Ally flag - White pawn at (1,1) from perspective of white pawn at (0,1)
        var allyRows = perspectivesDf
            .Filter("x = 1 AND y = 1 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(allyRows.Any(), "Expected at least one perspective row for an allied piece");
        bool anyAlly = allyRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Ally) != 0);
        Assert.True(anyAlly, "Expected allied piece to include the Ally flag");

        // Assert Foe flag - Black pawn at (0,6) from perspective of white pawn at (0,1)
        var foeRows = perspectivesDf
            .Filter("x = 0 AND y = 6 AND perspective_x = 0 AND perspective_y = 1")
            .Collect();

        Assert.True(foeRows.Any(), "Expected at least one perspective row for a foe piece");
        bool anyFoe = foeRows
            .Select(r => r.GetAs<int>("generic_piece"))
            .Any(g => (g & (int)ChessPolicy.Piece.Foe) != 0);
        Assert.True(anyFoe, "Expected enemy piece to include the Foe flag");
    }

    [Fact]
    
    [Trait("Category", "Slow")]
    
    [Trait("Refactored", "True")]
    public void StandardBoard_BuildTimeline_GeneratesMultipleTimesteps_Refactored()
    {
        // Arrange
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };
        var refactoredPolicy = new ChessPolicyRefactored(_spark);
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns();

        // Act
        var timelineDf = ChessPolicy.TimelineService.BuildTimeline(perspectivesDf, patternsDf, factions, maxDepth: 2);

        // Assert
        Assert.Contains("timestep", timelineDf.Columns());
        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        
        // For maxDepth=2, we expect timesteps 0 and 1
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
    }
}
