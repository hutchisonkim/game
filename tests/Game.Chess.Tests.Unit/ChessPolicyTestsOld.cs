// using Xunit;
// using Game.Chess.Policy;

// namespace Game.Chess.Tests.Unit;

// [Trait("Category", "Unit")]
// [Trait("Feature", "GetAvailableActions")]
// public class ChessPolicyTests
// {
//     [Fact]
//     public void GetAvailableActions_Turns64Seed1234_MatchingCount()
//     {
//         Assert.Equal(20, GetActionsCount(turnCount: 1, seed: 1234));
//         Assert.Equal(22, GetActionsCount(turnCount: 2, seed: 1234));
//         Assert.Equal(21, GetActionsCount(turnCount: 4, seed: 1234));
//         Assert.Equal(31, GetActionsCount(turnCount: 8, seed: 1234));
//     }

//     [Fact]
//     public void GetAvailableActions_Turns64Seed2345_MatchingCount()
//     {
//         Assert.Equal(20, GetActionsCount(turnCount: 1, seed: 2345));
//         Assert.Equal(22, GetActionsCount(turnCount: 2, seed: 2345));
//         Assert.Equal(21, GetActionsCount(turnCount: 4, seed: 2345));
//         Assert.Equal(31, GetActionsCount(turnCount: 8, seed: 2345));
//     }

//     [Fact]
//     public void GetAvailableActions_Turns64Seed3456_MatchingCount()
//     {
//         Assert.Equal(20, GetActionsCount(turnCount: 1, seed: 3456));
//         Assert.Equal(20, GetActionsCount(turnCount: 2, seed: 3456));
//         Assert.Equal(27, GetActionsCount(turnCount: 4, seed: 3456));
//         Assert.Equal(28, GetActionsCount(turnCount: 8, seed: 3456));
//     }

//     [Fact]
//     public void GetAvailableActions_Turns64Seed4567_MatchingCount()
//     {
//         Assert.Equal(20, GetActionsCount(turnCount: 1, seed: 4567));
//         Assert.Equal(30, GetActionsCount(turnCount: 2, seed: 4567));
//         Assert.Equal(29, GetActionsCount(turnCount: 4, seed: 4567));
//         Assert.Equal(45, GetActionsCount(turnCount: 8, seed: 4567));
//     }

//     internal static int GetActionsCount(int turnCount, int seed)
//     {
//         ChessState state = new();
//         Random rng = new(seed);
//         List<ChessAction> actions = [.. state.GetAvailableActions()];
//         for (int i = 0; i < turnCount; i++)
//         {
//             state = state.Apply(actions[rng.Next(actions.Count)]);
//             actions = [.. state.GetAvailableActions()];
//         }
//         return actions.Count;
//     }
// }
