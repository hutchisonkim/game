using Game.Chess.HistoryB;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Provides fluent API for building piece type flags.
/// Simplifies creation of complex piece flag combinations.
/// </summary>
public class PieceBuilder
{
    private ChessPolicy.Piece _piece = ChessPolicy.Piece.None;

    private PieceBuilder() { }

    /// <summary>
    /// Starts building a new piece.
    /// </summary>
    public static PieceBuilder Create() => new PieceBuilder();

    /// <summary>
    /// Adds the White color flag.
    /// </summary>
    public PieceBuilder White()
    {
        _piece |= ChessPolicy.Piece.White;
        return this;
    }

    /// <summary>
    /// Adds the Black color flag.
    /// </summary>
    public PieceBuilder Black()
    {
        _piece |= ChessPolicy.Piece.Black;
        return this;
    }

    /// <summary>
    /// Adds the Mint faction flag.
    /// </summary>
    public PieceBuilder Mint()
    {
        _piece |= ChessPolicy.Piece.Mint;
        return this;
    }

    /// <summary>
    /// Adds the Pawn piece type flag.
    /// </summary>
    public PieceBuilder Pawn()
    {
        _piece |= ChessPolicy.Piece.Pawn;
        return this;
    }

    /// <summary>
    /// Adds the Knight piece type flag.
    /// </summary>
    public PieceBuilder Knight()
    {
        _piece |= ChessPolicy.Piece.Knight;
        return this;
    }

    /// <summary>
    /// Adds the Bishop piece type flag.
    /// </summary>
    public PieceBuilder Bishop()
    {
        _piece |= ChessPolicy.Piece.Bishop;
        return this;
    }

    /// <summary>
    /// Adds the Rook piece type flag.
    /// </summary>
    public PieceBuilder Rook()
    {
        _piece |= ChessPolicy.Piece.Rook;
        return this;
    }

    /// <summary>
    /// Adds the Queen piece type flag.
    /// </summary>
    public PieceBuilder Queen()
    {
        _piece |= ChessPolicy.Piece.Queen;
        return this;
    }

    /// <summary>
    /// Adds the King piece type flag.
    /// </summary>
    public PieceBuilder King()
    {
        _piece |= ChessPolicy.Piece.King;
        return this;
    }

    /// <summary>
    /// Adds the Self perspective flag.
    /// </summary>
    public PieceBuilder Self()
    {
        _piece |= ChessPolicy.Piece.Self;
        return this;
    }

    /// <summary>
    /// Adds the Ally perspective flag.
    /// </summary>
    public PieceBuilder Ally()
    {
        _piece |= ChessPolicy.Piece.Ally;
        return this;
    }

    /// <summary>
    /// Adds the Foe perspective flag.
    /// </summary>
    public PieceBuilder Foe()
    {
        _piece |= ChessPolicy.Piece.Foe;
        return this;
    }

    /// <summary>
    /// Adds the Empty square flag.
    /// </summary>
    public PieceBuilder Empty()
    {
        _piece |= ChessPolicy.Piece.Empty;
        return this;
    }

    /// <summary>
    /// Returns the built piece flags.
    /// </summary>
    public ChessPolicy.Piece Build() => _piece;

    /// <summary>
    /// Implicit conversion to ChessPolicy.Piece for convenience.
    /// </summary>
    public static implicit operator ChessPolicy.Piece(PieceBuilder builder) => builder._piece;
}

/// <summary>
/// Provides commonly used piece combinations as constants.
/// Reduces repetitive piece flag combinations throughout tests.
/// </summary>
public static class TestPieces
{
    // White Mint pieces (most common in tests)
    public static readonly ChessPolicy.Piece WhiteMintPawn = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;
    
    public static readonly ChessPolicy.Piece WhiteMintKnight = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight;
    
    public static readonly ChessPolicy.Piece WhiteMintBishop = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;
    
    public static readonly ChessPolicy.Piece WhiteMintRook = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;
    
    public static readonly ChessPolicy.Piece WhiteMintQueen = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen;
    
    public static readonly ChessPolicy.Piece WhiteMintKing = 
        ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

    // Black Mint pieces
    public static readonly ChessPolicy.Piece BlackMintPawn = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn;
    
    public static readonly ChessPolicy.Piece BlackMintKnight = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight;
    
    public static readonly ChessPolicy.Piece BlackMintBishop = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop;
    
    public static readonly ChessPolicy.Piece BlackMintRook = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook;
    
    public static readonly ChessPolicy.Piece BlackMintQueen = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen;
    
    public static readonly ChessPolicy.Piece BlackMintKing = 
        ChessPolicy.Piece.Black | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King;

    // Generic piece types (no color/faction)
    public static readonly ChessPolicy.Piece JustPawn = ChessPolicy.Piece.Pawn;
    public static readonly ChessPolicy.Piece JustKnight = ChessPolicy.Piece.Knight;
    public static readonly ChessPolicy.Piece JustBishop = ChessPolicy.Piece.Bishop;
    public static readonly ChessPolicy.Piece JustRook = ChessPolicy.Piece.Rook;
    public static readonly ChessPolicy.Piece JustQueen = ChessPolicy.Piece.Queen;
    public static readonly ChessPolicy.Piece JustKing = ChessPolicy.Piece.King;

    // Perspective flags
    public static readonly ChessPolicy.Piece JustSelf = ChessPolicy.Piece.Self;
    public static readonly ChessPolicy.Piece JustAlly = ChessPolicy.Piece.Ally;
    public static readonly ChessPolicy.Piece JustFoe = ChessPolicy.Piece.Foe;
    public static readonly ChessPolicy.Piece JustEmpty = ChessPolicy.Piece.Empty;
}
