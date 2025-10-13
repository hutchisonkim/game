# Contributing
## Rules
- Always follow MSDN / xUnit conventions;
- Avoid creating new documentation files;
- Avoid modifying existing documentation files;
- Avoid writing comments in code and project files;
- - Except for comment summaries in public APIs;
- - Except for placeholder comments like `// TODO: Implement this method`;
- Never cheat a test: Always let the test fail if the functionality is not implemented or broken;

## MSDN / xUnit conventions reminder
- Source project names use the convention: `Game.{GameName}` (e.g., `Game.Chess`, `Game.Snake`).
- Test project names use the convention: `Game.{GameName}.Tests.{TestType}` (e.g., `Game.Chess.Tests.Unit`, `Game.Chess.Tests.Integration`).
- Test class names use the convention: `{ClassName}Tests` (e.g., `ChessActionTests`, `ChessPolicyTests`).
- Test method names use the convention: `{MethodName}_{Scenario}_{Expected}` (e.g., `MovePiece_ValidMove_PieceMoved`).
- Test category traits are at the class level and use either the attribute `[Trait("Category","Unit")]` or `[Trait("Category","Integration")]`.
- Test feature traits are at the method level and use the attribute `[Trait("Feature","{FeatureName}")]` (e.g., `[Trait("Feature","Movement")]`, `[Trait("Feature","Capture")]`).
