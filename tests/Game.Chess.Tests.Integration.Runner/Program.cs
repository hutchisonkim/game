using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static int Main()
    {
        // Console.WriteLine("****[DEBUG] Starting test runner****");

        // Path to the test DLL (publish folder)
        string testAssembly = Path.Combine(AppContext.BaseDirectory, "Game.Chess.Tests.Integration.dll");
        if (!File.Exists(testAssembly))
        {
            Console.WriteLine($"[ERROR] Test assembly not found: {testAssembly}");
            return 1;
        }

        // Class filter (optional)
        string testClass = Environment.GetEnvironmentVariable("TEST_CLASS")
                           ?? "Game.Chess.Tests.Integration.ChessSparkPolicyTests";

        string logFile = Path.Combine(AppContext.BaseDirectory, "test-output.log");

        // Build dotnet test command
        var psi = new ProcessStartInfo
        {
            FileName = GetDotnetPath(),
            Arguments = $"vstest \"{testAssembly}\" " +
                        $"--TestCaseFilter:\"FullyQualifiedName~{testClass}\" " +
                        "--logger \"console;verbosity=detailed\" " + //detailed keeps test-output.log relevant (<PUBLISH_DIR>\test-output.log)
                        // "--logger \"console;verbosity=minimal\" " +
                        "--logger \"trx;LogFileName=TestResults.trx\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.EnvironmentVariables["PATH"] = @"C:\Program Files\dotnet;" + Environment.GetEnvironmentVariable("PATH");
        psi.EnvironmentVariables["DOTNET_ROOT"] = @"C:\Program Files\dotnet";
        psi.EnvironmentVariables["DOTNET_WORKER_DIR"] = @"C:\Program Files\dotnet";

        // Console.WriteLine("****[DEBUG] Environment Variables****");
        // foreach (var key in Environment.GetEnvironmentVariables().Keys)
        // {
        //     Console.WriteLine($"{key} = {Environment.GetEnvironmentVariable(key.ToString())}");
        // }

        using var proc = Process.Start(psi);
        using var logWriter = new StreamWriter(logFile, false) { AutoFlush = true };

        // Capture stdout/stderr
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) logWriter.WriteLine(e.Data); };

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.WaitForExit();
        return proc.ExitCode;
    }

    // Resolve dotnet path
    static string GetDotnetPath()
    {
        // string dotnetWorkerDir = Environment.GetEnvironmentVariable("DOTNET_WORKER_DIR");
        // if (!string.IsNullOrEmpty(dotnetWorkerDir))
        // {
        //     string fullPath = Path.Combine(dotnetWorkerDir, "dotnet.exe");
        //     if (File.Exists(fullPath))
        //     {
        //         return fullPath;
        //     }
        // }
        return "dotnet"; // fallback to PATH
    }
}
