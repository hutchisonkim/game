# Game

This repo is a starter kit for creating basic games using autonomous coding agents.

> [!WARNING]
> Work in progress. Not everything you see here is final.

- TODO  
  - Missing mocks: renderers, GIF composer and IO need mockable interfaces so unit tests can isolate.  
  - Move rendering/IO-heavy tests to an integration test project (`Game.*.Tests.Integration`); keep pure logic tests in unit projects with mocks.  
  - Split core vs chess: decouple `Game.Core` (generic policy/state/action) from `Game.Chess` specifics so other games can be implemented using a domain-agnostic core.  

## Output Preview

Below are the reference render GIFs produced by the `Game.Chess.Renders` tests.

### Actions timeline

![RenderActionsTimeline_Turns32Seed1234PieceNone_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PieceNone_MatchesRef.gif)
![RenderActionsTimeline_Turns32Seed1234PieceKnight_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PieceKnight_MatchesRef.gif)
![RenderActionsTimeline_Turns32Seed1234PiecePawn_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PiecePawn_MatchesRef.gif)

### Candidate actions timeline

![RenderCandidateActionsTimeline_Turns16Seed1234PieceNone_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderCandidateActionsTimeline_Turns16Seed1234PieceNone_MatchesRef.gif)
![RenderCandidateActionsTimeline_Turns16Seed1234PieceKnight_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderCandidateActionsTimeline_Turns16Seed1234PieceKnight_MatchesRef.gif)
![RenderCandidateActionsTimeline_Turns16Seed1234PiecePawn_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderCandidateActionsTimeline_Turns16Seed1234PiecePawn_MatchesRef.gif)

### Threat timeline

![RenderThreatTimeline_Turns64Seed1234PieceNone_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderThreatTimeline_Turns64Seed1234PieceNone_MatchesRef.gif)
![RenderThreatTimeline_Turns64Seed1234PieceKnight_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderThreatTimeline_Turns64Seed1234PieceKnight_MatchesRef.gif)
![RenderThreatTimeline_Turns64Seed1234PiecePawn_MatchesRef](TestResultsReference/Game.Chess.Renders/RenderThreatTimeline_Turns64Seed1234PiecePawn_MatchesRef.gif)