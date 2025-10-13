using Xunit;
using Xunit.Abstractions;
using Game.Core;

namespace Game.Chess.Tests.View.Unit;

[Trait("Category", "Unit")]
public class ChessViewTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _out = output;

    private static string MakeOutputPath(string name, string ext)
    {
        // Try to find the repository root by walking up from the test assembly directory
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? repoRoot = start;

        while (repoRoot != null)
        {
            // look for solution file as indicator of repo root
            if (repoRoot.GetFiles("*.sln").Length != 0) break;
            repoRoot = repoRoot.Parent;
        }

        string rootPath = repoRoot?.FullName ?? AppContext.BaseDirectory;
        var dir = Path.Combine(rootPath, "TestResults", "View");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ext);
    }

    [Fact]
    [Trait("Feature", "Rendering")]
    public void RenderStatePng_FromParsedFen_SavesPng()
    {
        // Arrange - starting position
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
        var board = ChessView<DummyAction, DummyState, DummyView>.ParseFen(fen);

        // Act - render to PNG bytes via the instance API (state is char[,])
        var view = new DummyView();
        var pngBytes = view.RenderStatePng(new DummyState(board), 200);

        // Save
        var path = MakeOutputPath("chess_fromfen_instance", ".png");
        File.WriteAllBytes(path, pngBytes);

        // Quick checks
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        _out.WriteLine(path);
    }

    [Fact]
    [Trait("Feature", "Rendering")]
    public void RenderTransitionPng_FromEmptyToRook_SavesPng()
    {
        var fromFen = "8/8/8/8/8/8/8/8"; // empty
        var toFen = "8/8/8/8/8/8/8/R7"; // one rook at a1 (bottom-left)
        var from = ChessView<DummyAction, DummyState, DummyView>.ParseFen(fromFen);
        var to = ChessView<DummyAction, DummyState, DummyView>.ParseFen(toFen);

        var view = new DummyView();
        var bytes = view.RenderTransitionPng(new DummyState(from), new DummyState(to), new DummyAction("move"), 200);

        var path = MakeOutputPath("chess_transition", ".png");
        File.WriteAllBytes(path, bytes);
        Assert.True(File.Exists(path));
        _out.WriteLine(path);
    }

    [Fact]
    [Trait("Feature", "Rendering")]
    public void RenderTimelineGif_MultipleFrames_CreatesGif()
    {
        var frames = new (DummyState state, DummyAction action)[]
        {
            (new DummyState(ChessView<DummyAction, DummyState, DummyView>.ParseFen("8/8/8/8/8/8/8/R7")), new DummyAction("s1")),
            (new DummyState(ChessView<DummyAction, DummyState, DummyView>.ParseFen("8/8/8/8/8/8/8/.R6")), new DummyAction("s2")),
            (new DummyState(ChessView<DummyAction, DummyState, DummyView>.ParseFen("8/8/8/8/8/8/8/..R5")), new DummyAction("s3")),
        };

        var view = new DummyView();
        var gif = view.RenderTimelineGif(frames.Select(f => (f.state, (DummyAction)f.action)), 120);

        var path = MakeOutputPath("chess_timeline", ".gif");
        File.WriteAllBytes(path, gif);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        _out.WriteLine(path);
    }
}

// Helper types to satisfy generic constraints of ChessView
public record DummyAction(string Description) : IAction
{
    public override string ToString() => Description;
}

public class DummyState(char[,] board) : IState<DummyAction, DummyState>
{
    public char[,] Board { get; } = board ?? throw new ArgumentNullException(nameof(board));

    public DummyState Clone() => new DummyState((char[,])Board.Clone());
    public DummyState Apply(DummyAction action) => Clone();
    public override string ToString() => string.Empty;
}

public class DummyView : ChessView<DummyAction, DummyState, DummyView>
{
}
