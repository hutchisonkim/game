# Game

This repo is a starter kit for creating basic games using autonomous coding agents.

> [!WARNING]
> Work in progress. Not everything you see here is final.

- TODO  
  - Missing mocks: renderers, GIF composer and IO need mockable interfaces so unit tests can isolate policy/state/action.  
  - Move rendering/IO-heavy tests to an integration test project (`Game.*.Tests.Integration`); keep pure logic tests in unit projects with mocks.  
  - Split core vs chess: decouple `Game.Core` (generic policy/state/action) from `Game.Chess` specifics so other games can be implemented without chess coupling.  

# test 1

![Alt1](TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PieceNone_MatchesRef.gif) ![Alt2](TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed2345PieceNone_MatchesRef.gif)

# test 2

<table>
  <tr>
    <td><img src="TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PieceNone_MatchesRef.gif" alt="Seed 1234" width="45%"></td>
    <td><img src="TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed2345PieceNone_MatchesRef.gif" alt="Seed 2345" width="45%"></td>
  </tr>
</table>

# test 3

<img src="TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed1234PieceNone_MatchesRef.gif" width="45%">
<img src="TestResultsReference/Game.Chess.Renders/RenderActionsTimeline_Turns32Seed2345PieceNone_MatchesRef.gif" width="45%">
