using System;
using System.Collections.Generic;
using System.Linq;
using Game.Chess.Entity;

namespace Game.Chess.History
{
    public static class ChessStateUtility
    {
        public static IEnumerable<(int X, int Y)> GetBlockedPositions(ChessState state, ChessPieceAttribute blockedColor)
        {
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var piece = state[x, y];
                    if (piece.IsEmpty) continue;
                    if (!piece.IsSameColor(blockedColor)) continue;
                    var actionsFromCell = state.GetActionCandidates()
                        .Where(c => c.Action.From.X == x && c.Action.From.Y == y)
                        .ToList();
                    if (actionsFromCell.Count != 0) continue;
                    yield return (x, y);
                }
            }
        }

        public static IEnumerable<(int X, int Y)> GetPinnedCells(ChessState state, ChessPieceAttribute pinnedColor, bool includeTargetless, bool includeFriendlyfire)
        {
            var pinnedCells = new List<(int X, int Y)>();

            var kingPositions = GetPiecesByType(state, pinnedColor, ChessPieceAttribute.King);
            if (kingPositions.Count != 1) return pinnedCells;
            var kingPosition = kingPositions.First();
            ChessPiece kingPiece = state[kingPosition.X, kingPosition.Y];
            if (kingPiece.IsEmpty) throw new InvalidOperationException("King piece not found at expected position.");
            if (!kingPiece.IsSameColor(pinnedColor)) throw new InvalidOperationException("King piece color does not match pinned color.");

            var opposingActionCandidates = state.GetActionCandidates()
                .Where(c => !state[c.Action.From.X, c.Action.From.Y].IsSameColor(pinnedColor))
                .ToList();

            foreach (var checkingActionCandidate in opposingActionCandidates)
            {
                ChessState simulatedBoard = state.Apply(checkingActionCandidate.Action);
                var checkingCandidates = simulatedBoard.GetCheckingActionCandidates(simulatedBoard.TurnColor, includeTargetless, includeFriendlyfire);
                // preserve original behavior: if checkingCandidates.Any() then skip
                if (checkingCandidates.Any()) continue;
                var pinnedCell = (checkingActionCandidate.Action.From.X, checkingActionCandidate.Action.From.Y);
                if (pinnedCells.Contains(pinnedCell)) continue;
                pinnedCells.Add(pinnedCell);
            }

            return pinnedCells;
        }

        public static IReadOnlyCollection<(int X, int Y)> GetPiecesByType(ChessState state, ChessPieceAttribute pieceColor, ChessPieceAttribute pieceType)
        {
            var positions = new List<(int X, int Y)>();
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var piece = state[x, y];
                    if (piece.IsEmpty) continue;
                    if (!piece.IsSameColor(pieceColor)) continue;
                    if (!piece.IncludesType(pieceType)) continue;
                    positions.Add((x, y));
                }
            }
            return positions;
        }

        public static ChessPiece[,] CreateInitialBoard(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
        {
            var board = new ChessPiece[8, 8];

            bool hasOverride = pieceAttributeOverride != ChessPieceAttribute.None;
            ChessPieceAttribute pieceAttr;

            // Pawns
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Pawn;
            for (int x = 0; x < 8; x++)
            {
                board[x, 1] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
                board[x, 6] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);
            }

            // Rooks
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Rook;
            board[0, 0] = board[7, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            board[0, 7] = board[7, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

            // Knights
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Knight;
            board[1, 0] = board[6, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            board[1, 7] = board[6, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

            // Bishops
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Bishop;
            board[2, 0] = board[5, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            board[2, 7] = board[5, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

            // Queens
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Queen;
            board[3, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            board[3, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

            // Kings
            pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.King;
            board[4, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            board[4, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

            return board;
        }
    }
}
