using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Provides fluent API for building piece type flags.
/// Simplifies creation of complex piece flag combinations.
/// </summary>
public class PieceBuilder
{
    private Piece _piece = Piece.None;

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
        _piece |= Piece.White;
        return this;
    }

    /// <summary>
    /// Adds the Black color flag.
    /// </summary>
    public PieceBuilder Black()
    {
        _piece |= Piece.Black;
        return this;
    }

    /// <summary>
    /// Adds the Mint faction flag.
    /// </summary>
    public PieceBuilder Mint()
    {
        _piece |= Piece.Mint;
        return this;
    }

    /// <summary>
    /// Adds the Pawn piece type flag.
    /// </summary>
    public PieceBuilder Pawn()
    {
        _piece |= Piece.Pawn;
        return this;
    }

    /// <summary>
    /// Adds the Knight piece type flag.
    /// </summary>
    public PieceBuilder Knight()
    {
        _piece |= Piece.Knight;
        return this;
    }

    /// <summary>
    /// Adds the Bishop piece type flag.
    /// </summary>
    public PieceBuilder Bishop()
    {
        _piece |= Piece.Bishop;
        return this;
    }

    /// <summary>
    /// Adds the Rook piece type flag.
    /// </summary>
    public PieceBuilder Rook()
    {
        _piece |= Piece.Rook;
        return this;
    }

    /// <summary>
    /// Adds the Queen piece type flag.
    /// </summary>
    public PieceBuilder Queen()
    {
        _piece |= Piece.Queen;
        return this;
    }

    /// <summary>
    /// Adds the King piece type flag.
    /// </summary>
    public PieceBuilder King()
    {
        _piece |= Piece.King;
        return this;
    }

    /// <summary>
    /// Adds the Self perspective flag.
    /// </summary>
    public PieceBuilder Self()
    {
        _piece |= Piece.Self;
        return this;
    }

    /// <summary>
    /// Adds the Ally perspective flag.
    /// </summary>
    public PieceBuilder Ally()
    {
        _piece |= Piece.Ally;
        return this;
    }

    /// <summary>
    /// Adds the Foe perspective flag.
    /// </summary>
    public PieceBuilder Foe()
    {
        _piece |= Piece.Foe;
        return this;
    }

    /// <summary>
    /// Adds the Empty square flag.
    /// </summary>
    public PieceBuilder Empty()
    {
        _piece |= Piece.Empty;
        return this;
    }

    /// <summary>
    /// Returns the built piece flags.
    /// </summary>
    public Piece Build() => _piece;

    /// <summary>
    /// Implicit conversion to Piece for convenience.
    /// </summary>
    public static implicit operator Piece(PieceBuilder builder) => builder._piece;
}

/// <summary>
/// Provides commonly used piece combinations as constants.
/// Reduces repetitive piece flag combinations throughout tests.
/// </summary>
public static class TestPieces
{
    // White Mint pieces (most common in tests)
    public static readonly Piece WhiteMintPawn = 
        Piece.White | Piece.Mint | Piece.Pawn;
    
    public static readonly Piece WhiteMintKnight = 
        Piece.White | Piece.Mint | Piece.Knight;
    
    public static readonly Piece WhiteMintBishop = 
        Piece.White | Piece.Mint | Piece.Bishop;
    
    public static readonly Piece WhiteMintRook = 
        Piece.White | Piece.Mint | Piece.Rook;
    
    public static readonly Piece WhiteMintQueen = 
        Piece.White | Piece.Mint | Piece.Queen;
    
    public static readonly Piece WhiteMintKing = 
        Piece.White | Piece.Mint | Piece.King;

    // Black Mint pieces
    public static readonly Piece BlackMintPawn = 
        Piece.Black | Piece.Mint | Piece.Pawn;
    
    public static readonly Piece BlackMintKnight = 
        Piece.Black | Piece.Mint | Piece.Knight;
    
    public static readonly Piece BlackMintBishop = 
        Piece.Black | Piece.Mint | Piece.Bishop;
    
    public static readonly Piece BlackMintRook = 
        Piece.Black | Piece.Mint | Piece.Rook;
    
    public static readonly Piece BlackMintQueen = 
        Piece.Black | Piece.Mint | Piece.Queen;
    
    public static readonly Piece BlackMintKing = 
        Piece.Black | Piece.Mint | Piece.King;

    // Generic piece types (no color/faction)
    public static readonly Piece JustPawn = Piece.Pawn;
    public static readonly Piece JustKnight = Piece.Knight;
    public static readonly Piece JustBishop = Piece.Bishop;
    public static readonly Piece JustRook = Piece.Rook;
    public static readonly Piece JustQueen = Piece.Queen;
    public static readonly Piece JustKing = Piece.King;

    // Perspective flags
    public static readonly Piece JustSelf = Piece.Self;
    public static readonly Piece JustAlly = Piece.Ally;
    public static readonly Piece JustFoe = Piece.Foe;
    public static readonly Piece JustEmpty = Piece.Empty;
}
