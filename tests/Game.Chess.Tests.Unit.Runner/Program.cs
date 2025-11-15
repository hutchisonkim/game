using Xunit.Runners;

class Program
{
    static void Main(string[] args)
    {
        using var runner = AssemblyRunner.WithoutAppDomain(typeof(Program).Assembly.Location);
        runner.OnDiscoveryComplete = info => Console.WriteLine($"{info.TestCasesToRun} tests discovered");
        runner.OnExecutionComplete = info => Console.WriteLine($"{info.TotalTests} tests executed");
        runner.Start();
        runner.WaitForCompletion();
    }
}
