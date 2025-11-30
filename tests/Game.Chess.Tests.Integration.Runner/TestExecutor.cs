using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Game.Chess.Tests.Integration.Runner
{
    public static class TestExecutor
    {
        // Runs tests from the integration test DLL by invoking the xUnit console runner via dotnet.
        // Returns a concise text summary.
        public static string RunTests(string publishDir, string filter)
        {
            string dll = Path.Combine(publishDir, "Game.Chess.Tests.Integration.dll");
            if (!File.Exists(dll))
            {
                return $"[Runner] Test DLL not found: {dll}";
            }

            try
            {
                var consoleDll = Path.Combine(publishDir, "xunit.console.dll");
                var consoleExe = Path.Combine(publishDir, "xunit.console.exe");
                bool useDotnet = File.Exists(consoleDll);
                if (!useDotnet && !File.Exists(consoleExe))
                {
                    return "[Runner] xUnit console runner not found in publish directory.";
                }

                var args = new System.Collections.Generic.List<string>();
                args.Add('"' + dll + '"');
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    // Support Performance=Fast trait filter
                    if (filter.Contains("="))
                    {
                        args.Add("-trait");
                        args.Add('"' + filter + '"');
                    }
                }
                args.Add("-nologo");
                args.Add("-parallel");
                args.Add("none");

                System.Diagnostics.ProcessStartInfo psi;
                if (useDotnet)
                {
                    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    string dotnetExe = string.IsNullOrWhiteSpace(dotnetRoot)
                        ? "dotnet"
                        : Path.Combine(dotnetRoot, "dotnet.exe");
                    psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dotnetExe,
                        Arguments = '"' + consoleDll + '"' + " " + string.Join(" ", args),
                        WorkingDirectory = publishDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                }
                else
                {
                    psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = consoleExe,
                        Arguments = string.Join(" ", args),
                        WorkingDirectory = publishDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                }

                var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return "[Runner] Failed to start xunit console runner";
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                File.WriteAllText(Path.Combine(publishDir, "spark-test-output.log"), output + Environment.NewLine + error);
                var lastLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                return lastLine ?? "[Runner] Tests executed via console runner.";
            }
            catch (Exception ex)
            {
                return "[Runner] Exception: " + ex.ToString();
            }
        }
    }
}
