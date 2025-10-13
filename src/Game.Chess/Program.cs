namespace Game.Chess
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var state = new ChessState();
            var policy = new ChessPolicy();
            var actions = policy.GetAvailableActions(state);

            Console.WriteLine("Initial legal moves:");
            foreach (ChessMove? action in actions.Take(10))
            {
                Console.WriteLine($"{action.From} -> {action.To}");
            }
        }
    }
}
