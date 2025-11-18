using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static int Main()
    {
        string testAssembly = "Game.Chess.Tests.Integration.dll";
        string testClass = Environment.GetEnvironmentVariable("TEST_CLASS")
                           ?? "Game.Chess.Tests.Integration.ChessSparkPolicyTests";
        string logFile = Path.Combine(AppContext.BaseDirectory, "test-output.log");

        if (!File.Exists(testAssembly))
        {
            Console.WriteLine($"[ERROR] Test assembly not found at path: {testAssembly}");
            return 1;
        }

        string xunitRunner = Path.Combine(AppContext.BaseDirectory, "xunit.console.dll");
        if (!File.Exists(xunitRunner))
        {
            Console.WriteLine($"[ERROR] xUnit console runner not found: {xunitRunner}");
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{xunitRunner}\" \"{testAssembly}\" -class {testClass} -parallel none -nologo",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        using var proc = Process.Start(psi);
        using var logWriter = new StreamWriter(logFile, false) { AutoFlush = true };

        proc.OutputDataReceived += (s, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.WaitForExit();
        return proc.ExitCode;
    }
}
