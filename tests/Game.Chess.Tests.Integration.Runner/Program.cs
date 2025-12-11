using System;

namespace Game.Chess.Tests.Integration.Runner
{
    class Program
    {
        static int Main(string[] args)
        {
            string publishDir = AppContext.BaseDirectory;
            Console.WriteLine("[RunnerHost] Starting hot-reload test server...");

            var server = new RunnerServer(publishDir);
            server.Start();

            return 0;
        }
    }
}

