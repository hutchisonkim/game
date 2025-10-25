# Game

[!INFO]
This repo attempts to generalise game implementation patterns using chess as a concrete example. The goal is to create reusable abstractions for turn-based games, with clear separation of concerns between game logic, state management, and rendering.

- History vs Entity
  - History holds rules, move generation and timelines.
  - Entity holds domain objects (pieces, patterns).

- Core abstraction
  - Game = Policy / State / Action (IPolicy, IState, IAction).

- Rendering
  - View composes board + stamps; GifComposer produces GIFs.

- Tests & emergence
  - Tests validate emergent behaviour by generating timelines and comparing serialized outputs / GIFs against references.
  - Reference artifacts: TestResultsReference; test outputs: TestResults.

- Reference GIFs (examples)
  - gif-move-highlight.gif — board frames showing pre/post move and highlighted candidate moves.
  - gif-capture-sequence.gif — multi-frame capture sequence with stamps/arrows.

- TODO (what stinks)
  - Missing mocks: renderers, GIF composer and IO need mockable interfaces so unit tests can isolate policy/state/action.
  - Move rendering/IO-heavy tests to an integration test project (Game.*.Tests.Integration); keep pure logic tests in unit projects with mocks.
  - Split core vs chess: decouple Game.Core (generic policy/state/action) from Game.Chess specifics so other games can be implemented without chess coupling.
