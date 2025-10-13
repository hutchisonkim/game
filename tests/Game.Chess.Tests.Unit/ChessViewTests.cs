using Xunit;
using Xunit.Abstractions;
using Game.Core;

namespace Game.Chess.Tests.View.Unit;

[Trait("Category", "Unit")]
public class ChessViewTests
{
    private readonly ITestOutputHelper _out;

    public ChessViewTests(ITestOutputHelper output) => _out = output;

    private static string MakeOutputPath(string name, string ext)
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? repoRoot = start;

        while (repoRoot != null)
        {
            if (repoRoot.GetFiles("*.sln").Length != 0) break;
            repoRoot = repoRoot.Parent;
        }

        string rootPath = repoRoot?.FullName ?? AppContext.BaseDirectory;
        var dir = Path.Combine(rootPath, "TestResults", "View");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ext);
    }

    // Shared helpers used across multiple tests
    private static string SquareFromPosition(Position p)
    {
        char file = (char)('a' + p.Col);
        char rank = (char)('1' + (7 - p.Row));
        return string.Concat(file, rank);
    }

    private static char[,] ToCharBoard(ChessBoard cb)
    {
        var outBoard = new char[8, 8];
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var piece = cb.Board[r, c];
            if (piece == null)
            {
                outBoard[r, c] = '.';
                continue;
            }

            outBoard[r, c] = (piece.Color, piece.Type) switch
            {
                (PieceColor.White, PieceType.King) => '\u2654',
                (PieceColor.White, PieceType.Queen) => '\u2655',
                (PieceColor.White, PieceType.Rook) => '\u2656',
                (PieceColor.White, PieceType.Bishop) => '\u2657',
                (PieceColor.White, PieceType.Knight) => '\u2658',
                (PieceColor.White, PieceType.Pawn) => '\u2659',
                (PieceColor.Black, PieceType.King) => '\u265A',
                (PieceColor.Black, PieceType.Queen) => '\u265B',
                (PieceColor.Black, PieceType.Rook) => '\u265C',
                (PieceColor.Black, PieceType.Bishop) => '\u265D',
                (PieceColor.Black, PieceType.Knight) => '\u265E',
                (PieceColor.Black, PieceType.Pawn) => '\u265F',
                _ => '.'
            };
        }
        return outBoard;
    }

    [Fact]
    [Trait("Feature", "Rendering")]
    public void RenderStatePng_FromParsedFen_SavesPng()
    {
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
        var board = ChessView<DummyAction, DummyState, DummyView>.ParseFen(fen);

        var view = new DummyView();
        var pngBytes = view.RenderStatePng(new DummyState(board), 200);

        var path = MakeOutputPath("chess_fromfen_instance", ".png");
        File.WriteAllBytes(path, pngBytes);

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
        var boardState = new ChessBoard();
        var policy = new ChessRules();

        var view = new DummyView();
        var transitions = new List<(DummyState stateFrom, DummyState stateTo, DummyAction action)>();

        var allMoves = policy.GetAvailableActions(boardState).Take(64);

        foreach (var mv in allMoves)
        {
            var fromBoard = ToCharBoard(boardState);
            var toChess = boardState.Apply(mv);
            var toBoard = ToCharBoard(toChess);
            var actionStr = SquareFromPosition(mv.From) + SquareFromPosition(mv.To);
            transitions.Add((new DummyState(fromBoard), new DummyState(toBoard), new DummyAction(actionStr)));
        }

        var png = view.RenderMultiTransitionPng(transitions, 600);
        var path = MakeOutputPath("chess_multitransitions", ".png");
        File.WriteAllBytes(path, png);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        _out.WriteLine(path);
    }

    [Fact]
    [Trait("Feature", "AvailableActionsTimeline")]
    public void RenderAvailableActionsTimelineGif_StartingPosition_CreatesGif()
    {
        var boardState = new ChessBoard();
        var policy = new ChessRules();

        var view = new DummyView();
        var frames = new List<(DummyState state, DummyAction action)>();

        var allMoves = policy.GetAvailableActions(boardState).Take(64);

        foreach (var mv in allMoves)
        {
            var toChess = boardState.Apply(mv);
            var toBoard = ToCharBoard(toChess);
            var actionStr = SquareFromPosition(mv.From) + SquareFromPosition(mv.To);
            frames.Add((new DummyState(toBoard), new DummyAction(actionStr)));
        }

        var gif = view.RenderTimelineGif(frames.Select(f => (f.state, (DummyAction)f.action)), 200);
        var path = MakeOutputPath("chess_available_actions_timeline", ".gif");
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

public class DummyState : IState<DummyAction, DummyState>
{
    public char[,] Board { get; }

    public DummyState(char[,] board) => Board = board ?? throw new ArgumentNullException(nameof(board));

    public DummyState Clone() => new((char[,])Board.Clone());
    public DummyState Apply(DummyAction action) => Clone();
    public override string ToString() => string.Empty;
}

public class DummyView : ChessView<DummyAction, DummyState, DummyView>
{
}
