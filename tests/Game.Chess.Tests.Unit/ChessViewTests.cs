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
    [Trait("Feature", "TransitionRendering")]
    public void RenderTransitionPng_FromEmptyToRook_SavesPng()
    {
        var fromFen = "8/8/8/8/8/8/8/R7";
        var toFen = "8/8/8/8/8/8/8/..R5";
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
    [Trait("Feature", "StateTimelineRendering")]
    public void RenderTimelineGif_MultipleFrames_CreatesGif()
    {
        var frames = new (DummyState state, DummyAction action)[]
        {
            (new DummyState(ChessView<DummyAction, DummyState, DummyView>.ParseFen("8/8/8/8/8/8/8/R7")), new DummyAction("s1")),
            (new DummyState(ChessView<DummyAction, DummyState, DummyView>.ParseFen("8/8/8/8/8/8/8/..R5")), new DummyAction("s2")),
        };

        var view = new DummyView();
        var gif = view.RenderTimelineGif(frames.Select(f => (f.state, (DummyAction)f.action)), 120);

        var path = MakeOutputPath("chess_timeline", ".gif");
        File.WriteAllBytes(path, gif);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        _out.WriteLine(path);
    }

    [Fact]
    [Trait("Feature", "MultiTransitionRendering")]
    public void RenderMultiTransitionPng_StartingPosition_DisplaysAvailableActions()
    {
        // Arrange - starting position
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
        var board = ChessView<DummyAction, DummyState, DummyView>.ParseFen(fen);

        var view = new DummyView();

        var transitions = new List<(DummyState stateFrom, DummyState stateTo, DummyAction action)>();

        static string SquareFromCoords(int r, int f)
        {
            char file = (char)('a' + f);
            int displayRank = 8 - r;
            return string.Concat(file, displayRank.ToString());
        }

        // helper to clone and move a piece
        static char[,] MovePiece(char[,] src, int fr, int ff, int tr, int tf)
        {
            var dst = (char[,])src.Clone();
            dst[tr, tf] = dst[fr, ff];
            dst[fr, ff] = '.';
            return dst;
        }

        // For pawns and knights, generate plausible sample moves from starting position
        for (int r = 0; r < 8; r++)
        for (int f = 0; f < 8; f++)
        {
            char c = board[r, f];
            if (c == '\0' || c == '.') continue;
            // White pieces: Unicode U+2654..U+2659, pawns U+2659 (♙)
            // Black pawns U+265F (♟)
            var code = (int)c;
            var moves = new List<(int tr, int tf)>();

            // Pawn moves
            if (code == 0x2659) // white pawn
            {
                if (r - 1 >= 0) moves.Add((r - 1, f));
                if (r == 6 && r - 2 >= 0) moves.Add((r - 2, f));
            }
            else if (code == 0x265F) // black pawn
            {
                if (r + 1 < 8) moves.Add((r + 1, f));
                if (r == 1 && r + 2 < 8) moves.Add((r + 2, f));
            }
            // Knights (white ♘ U+2658, black ♞ U+265E)
            else if (code == 0x2658 || code == 0x265E)
            {
                int[] dr = { -2, -2, -1, -1, 1, 1, 2, 2 };
                int[] df = { -1, 1, -2, 2, -2, 2, -1, 1 };
                for (int i = 0; i < dr.Length; i++)
                {
                    int tr = r + dr[i];
                    int tf = f + df[i];
                    if (tr >= 0 && tr < 8 && tf >= 0 && tf < 8) moves.Add((tr, tf));
                }
            }
            else
            {
                // For other pieces, include one sample move: try one step forward if empty or capture
                int tr = code >= 0x2654 && code <= 0x2659 ? r - 1 : r + 1;
                if (tr >= 0 && tr < 8) moves.Add((tr, f));
            }

            foreach (var (tr, tf) in moves)
            {
                var toBoard = MovePiece(board, r, f, tr, tf);
                var fromState = new DummyState(board);
                var toState = new DummyState(toBoard);
                var act = new DummyAction(SquareFromCoords(r, f) + SquareFromCoords(tr, tf));
                transitions.Add((fromState, toState, act));
            }
        }

        // Act
        var png = view.RenderMultiTransitionPng(transitions, 600);

        // Save
        var path = MakeOutputPath("chess_multitransitions", ".png");
        File.WriteAllBytes(path, png);

        // Assert
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
