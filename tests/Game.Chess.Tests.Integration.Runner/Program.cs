using System;
using Xunit.Runners;

class Program
{
    static void Main()
    {
        string assemblyPath = "Game.Chess.Tests.Integration.dll";
        string testClass = Environment.GetEnvironmentVariable("TEST_CLASS")
                           ?? "Game.Chess.Tests.Integration.ChessSparkPolicyTests";

        int failCount = 0;

        using var runner = AssemblyRunner.WithoutAppDomain(assemblyPath);

        runner.OnTestPassed = info => Console.WriteLine($"PASS: {info.TestDisplayName}");
        runner.OnTestFailed = info =>
        {
            Console.WriteLine($"FAIL: {info.TestDisplayName} - {info.ExceptionMessage}");
            failCount++;
        };
        runner.OnTestSkipped = info => Console.WriteLine($"SKIP: {info.TestDisplayName}");

        int exitCode = 0;
        using var finished = new System.Threading.ManualResetEventSlim(false);

        runner.OnExecutionComplete = info =>
        {
            Console.WriteLine($"Finished: {info.TotalTests} tests, {failCount} failed");
            exitCode = failCount > 0 ? 1 : 0;
            finished.Set();
        };

        runner.Start();
        finished.Wait();

        Environment.Exit(exitCode);
    }
}
