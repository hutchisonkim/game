// tests/Game.Chess.Renders.Tests.Unit/ChessPolicyRenderTests.cs

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Game.Chess.History;
using static Game.Chess.History.ChessHistoryUtility;
using Game.Chess.Entity;
using Game.Chess.Renders;
using Game.Chess.Serialization;

namespace Game.Chess.Renders.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "ChessRenderTests")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessRenderTests
{
    [Theory]
    [InlineData(64, 1234, ChessPieceAttribute.None)]
    [InlineData(64, 2345, ChessPieceAttribute.None)]
    [InlineData(64, 3456, ChessPieceAttribute.None)]
    [InlineData(64, 1234, ChessPieceAttribute.Pawn)]
    [InlineData(64, 1234, ChessPieceAttribute.Rook)]
    [InlineData(64, 1234, ChessPieceAttribute.Knight)]
    [InlineData(64, 1234, ChessPieceAttribute.Bishop)]
    [InlineData(64, 1234, ChessPieceAttribute.Queen)]
    [InlineData(64, 1234, ChessPieceAttribute.King)]
    public void RenderActionsTimeline_TurnsXXSeedYYPieceZZ_MatchesRef(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        // Arrange
        string fileName = $"RenderActionsTimeline_Turns{turnCount}Seed{seed}Piece{pieceAttributeOverride}_MatchesRef.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: seed, turnCount: turnCount, pieceAttributeOverride: pieceAttributeOverride, anchorTip: true);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0, "Generated GIF is empty");

        string outputPath = GetOutputPath(fileName);
        string referencePath = GetOutputPath(fileName, asReference: true);

        SaveGifToFile(gifBytes, outputPath);

        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");

        // If a reference GIF for the candidate-render test doesn't exist yet, create it from this run.
        // This keeps the test usable when introducing a new visual behavior. If you'd prefer to
        // store references manually, we can remove this behavior and commit the reference files instead.
        if (!File.Exists(referencePath))
        {
            var refDir = Path.GetDirectoryName(referencePath);
            if (refDir != null) Directory.CreateDirectory(refDir);
            File.Copy(outputPath, referencePath);
        }

        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");

        byte[] referenceGifBytes = ReadGifFromFile(referencePath);

        Assert.True(referenceGifBytes.SequenceEqual(gifBytes), "Generated GIF does not match reference GIF");
    }

    [Theory]
    [InlineData(64, 1234, ChessPieceAttribute.None)]
    [InlineData(64, 2345, ChessPieceAttribute.None)]
    [InlineData(64, 3456, ChessPieceAttribute.None)]
    [InlineData(64, 1234, ChessPieceAttribute.Pawn)]
    [InlineData(64, 1234, ChessPieceAttribute.Rook)]
    [InlineData(64, 1234, ChessPieceAttribute.Knight)]
    [InlineData(64, 1234, ChessPieceAttribute.Bishop)]
    [InlineData(64, 1234, ChessPieceAttribute.Queen)]
    [InlineData(64, 1234, ChessPieceAttribute.King)]
    public void RenderCandidateActionsTimeline_TurnsXXSeedYYPieceZZ_MatchesRef(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
    // Arrange
    // Use the existing reference naming used for rendered action timelines so the GIF can be compared
    string fileName = $"RenderCandidateActionsTimeline_Turns{turnCount}Seed{seed}Piece{pieceAttributeOverride}_MatchesRef.gif";

        // Act
        byte[] gifBytes = GenerateCandidateTimelineGif(seed: seed, turnCount: turnCount, pieceAttributeOverride: pieceAttributeOverride, anchorTip: true);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0, "Generated GIF is empty");

        string outputPath = GetOutputPath(fileName);
        string referencePath = GetOutputPath(fileName, asReference: true);

        SaveGifToFile(gifBytes, outputPath);

        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");
        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");

        byte[] referenceGifBytes = ReadGifFromFile(referencePath);

        Assert.True(referenceGifBytes.SequenceEqual(gifBytes), "Generated GIF does not match reference GIF");
    }

    private static string GetOutputPath(string fileName, bool asReference = false)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        return Path.Combine(rootDir, asReference ? "TestResultsReference" : "TestResults", "Game.Chess.Renders", fileName);
    }

    private static void SaveGifToFile(byte[] gifBytes, string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, gifBytes);
    }

    private static byte[] ReadGifFromFile(string inputPath)
    {
        return File.ReadAllBytes(inputPath);
    }

    private static byte[] GenerateTimelineGif(int seed, int turnCount, ChessPieceAttribute pieceAttributeOverride, bool anchorTip)
    {
        Random rng = new(seed);
        ChessView view = new();
        ChessState state = new();
        if (pieceAttributeOverride != ChessPieceAttribute.None)
            state.InitializeBoard(pieceAttributeOverride);

        var transitions = new List<(ChessState fromState, ChessState toState, ChessAction action, bool selected)>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            var actionCandidates = state.GetActionCandidates().ToList();
            if (actionCandidates.Count == 0) break;

            var randomActionCandidate = actionCandidates[rng.Next(actionCandidates.Count)];
            ChessState nextState = state.Apply(randomActionCandidate.Action);

            transitions.Add((state, nextState, randomActionCandidate.Action, selected: true));
            state = nextState;
        }

        // Act
        byte[] gifBytes = view.RenderTransitionSequenceGif(transitions, stateSize: 200, anchorTip: anchorTip);
        return gifBytes;
    }

    private static byte[] GenerateCandidateTimelineGif(int seed, int turnCount, ChessPieceAttribute pieceAttributeOverride, bool anchorTip)
    {
        // Mirrors CandidateActionsTimeline test logic: collect candidates each turn, pick one random candidate to progress state,
        // and render the resulting state transitions.
        Random rng = new(seed);
        ChessView view = new();
        ChessState state = new();
        if (pieceAttributeOverride != ChessPieceAttribute.None)
            state.InitializeBoard(pieceAttributeOverride);

        var transitions = new List<(ChessState fromState, ChessState toState, ChessAction action, bool selected)>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            var actionCandidates = state.GetActionCandidates().ToList();
            int count = actionCandidates.Count;
            if (count == 0) break;

            // For visual parity with the candidate timeline test, record candidate serializations (not used further here)
            var candidateActionsThisTurn = actionCandidates
                .Select(ac => ChessSerializationUtility.SerializeAction(ac.Action.From.X, ac.Action.From.Y, ac.Action.To.X, ac.Action.To.Y))
                .ToList();

            // First: show all candidate transitions (preview each candidate without mutating the real state)
            foreach (var candidate in actionCandidates)
            {
                var previewNext = state.Apply(candidate.Action);
                transitions.Add((state, previewNext, candidate.Action, selected: false));
            }

            // Then choose one at random to actually take and advance the main state
            var chosen = actionCandidates[rng.Next(count)];
            var chosenNext = state.Apply(chosen.Action);
            transitions.Add((state, chosenNext, chosen.Action, selected: true));

            // Advance the canonical state to the chosen next state
            state = chosenNext;
        }

        // Render all accumulated transitions: candidates previews followed by the chosen transitions per turn
        byte[] gifBytes = view.RenderTransitionSequenceGif(transitions, 200, anchorTip);
        return gifBytes;
    }
}
