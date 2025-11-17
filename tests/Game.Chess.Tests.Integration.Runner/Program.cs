using System;
using System.IO;
using Xunit.Runners;

class Program
{
    static void Main()
    {
        string assemblyPath = "Game.Chess.Tests.Integration.dll";
        string testClass = Environment.GetEnvironmentVariable("TEST_CLASS")
                           ?? "Game.Chess.Tests.Integration.ChessSparkPolicyTests";

        Console.WriteLine($"[Diagnostics] Running tests from assembly: {assemblyPath}");
        Console.WriteLine($"[Diagnostics] Looking for test class: {testClass}");

        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"[ERROR] Assembly not found at path: {assemblyPath}");
            Environment.Exit(1);
        }

        int failCount = 0;

        using var runner = AssemblyRunner.WithoutAppDomain(assemblyPath);

        // Diagnostics for each test discovered
        runner.OnDiscoveryComplete = info =>
        {
            Console.WriteLine($"[Diagnostics] Test discovery complete: {info.TestCasesToRun} test(s) found");
        };

        runner.OnTestStarting = info =>
        {
            Console.WriteLine($"[Diagnostics] Starting test: {info.TestDisplayName}");
        };

        runner.OnTestPassed = info => Console.WriteLine($"PASS: {info.TestDisplayName}");
        runner.OnTestFailed = info =>
        {
            Console.WriteLine($"FAIL: {info.TestDisplayName} - {info.ExceptionMessage}");
            failCount++;
        };
        runner.OnTestSkipped = info => Console.WriteLine($"SKIP: {info.TestDisplayName}");

        using var finished = new System.Threading.ManualResetEventSlim(false);

        runner.OnExecutionComplete = info =>
        {
            Console.WriteLine($"[Diagnostics] Execution complete: {info.TotalTests} test(s) run, {failCount} failed");
            finished.Set();
        };

        // Start all tests (you could add a filter here if desired)
        Console.WriteLine("[Diagnostics] Starting test run...");
        runner.Start();

        finished.Wait();
        Console.WriteLine("[Diagnostics] Test run finished.");

        Environment.Exit(failCount > 0 ? 1 : 0);
    }
}
