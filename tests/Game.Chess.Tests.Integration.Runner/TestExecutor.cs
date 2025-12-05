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
            // Resolve test DLL location
            string testDll = ResolveTestDll();
            if (!File.Exists(testDll))
            {
                return $"[Runner] Test DLL not found: {testDll}";
            }

            // Copy test DLL and its dependencies to publish dir so they can be loaded
            // This allows the runner to stay running while rebuilding the test project
            try
            {
                CopyTestAssemblies(testDll, publishDir);
            }
            catch (Exception ex)
            {
                return $"[Runner] Failed to copy test assemblies: {ex.Message}";
            }

            // Update testDll path to the copied version in publish dir
            testDll = Path.Combine(publishDir, Path.GetFileName(testDll));

            try
            {
                // Prefer VSTest (net8) with xUnit adapter and trait filtering
                var adapterDll = Path.Combine(publishDir, "xunit.runner.visualstudio.testadapter.dll");
                var useVsTest = File.Exists(adapterDll);
                if (useVsTest)
                {
                    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    string dotnetExe = string.IsNullOrWhiteSpace(dotnetRoot)
                        ? "dotnet"
                        : Path.Combine(dotnetRoot, "dotnet.exe");

                    // Build TestCaseFilter for xUnit traits. VSTest filter format: (Trait=value)|(Trait=value)
                    string? testCaseFilter = null;
                    if (!string.IsNullOrWhiteSpace(filter) && filter.Contains("="))
                    {
                        // Expecting Performance=Fast; xUnit exposes traits as properties named by trait name
                        var kv = filter.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var name = kv[0].Trim();
                            var value = kv[1].Trim();
                            // Correct format for xUnit trait filter: "Performance=Fast"
                            testCaseFilter = $"{name}={value}";
                        }
                    }

                    var trxPath = Path.Combine(publishDir, "spark-vstest.trx");
                    var vsArgs = new System.Collections.Generic.List<string>();
                    vsArgs.Add("vstest");
                    vsArgs.Add('"' + testDll + '"');
                    if (!string.IsNullOrWhiteSpace(testCaseFilter))
                    {
                        vsArgs.Add("--TestCaseFilter:" + '"' + testCaseFilter + '"');
                    }
                    // Ensure no parallel to avoid Spark worker issues (no arg means sequential)
                    // Log TRX to inspect failures
                    vsArgs.Add("--Logger:" + '"' + $"trx;LogFileName={Path.GetFileName(trxPath)}" + '"');

                    var psiVs = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dotnetExe,
                        Arguments = string.Join(" ", vsArgs),
                        WorkingDirectory = publishDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    var pVs = System.Diagnostics.Process.Start(psiVs);
                    if (pVs != null)
                    {
                        string outVs = pVs.StandardOutput.ReadToEnd();
                        string errVs = pVs.StandardError.ReadToEnd();
                        pVs.WaitForExit();
                        // Relay logs to both spark-test-output.log and test-output.log
                        var combined = outVs + Environment.NewLine + errVs;
                        File.WriteAllText(Path.Combine(publishDir, "spark-test-output.log"), combined);
                        File.WriteAllText(Path.Combine(publishDir, "test-output.log"), combined);

                        // If TRX exists, parse summary counts
                        if (File.Exists(trxPath))
                        {
                            try
                            {
                                var trx = File.ReadAllText(trxPath);
                                // Simple extraction of totals
                                int total = ExtractInt(trx, "total=");
                                int passed = ExtractInt(trx, "passed=");
                                int failed = ExtractInt(trx, "failed=");
                                int skipped = ExtractInt(trx, "skipped=");
                                return $"VSTest Summary: Total: {total}, Passed: {passed}, Failed: {failed}, Skipped: {skipped}";
                            }
                            catch { }
                        }
                        var lastLineVs = outVs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                        return lastLineVs ?? "[Runner] Tests executed via VSTest.";
                    }
                }

                var consoleDll = Path.Combine(publishDir, "xunit.console.dll");
                var consoleExe = Path.Combine(publishDir, "xunit.console.exe");
                bool useDotnet = File.Exists(consoleDll);
                if (!useDotnet && !File.Exists(consoleExe))
                {
                    return "[Runner] xUnit console runner not found in publish directory.";
                }

                var args = new System.Collections.Generic.List<string>();
                args.Add('"' + testDll + '"');
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    // Support Performance=Fast trait filter
                    if (filter.Contains("="))
                    {
                        args.Add("-trait");
                        args.Add('"' + filter + '"');
                        // Exclude other performance tiers when Fast is requested
                        var kv = filter.Split('=', 2);
                        if (kv.Length == 2 && kv[0].Trim().Equals("Performance", StringComparison.OrdinalIgnoreCase) && kv[1].Trim().Equals("Fast", StringComparison.OrdinalIgnoreCase))
                        {
                            args.Add("-notrait"); args.Add('"' + "Performance=Slow" + '"');
                            args.Add("-notrait"); args.Add('"' + "Performance=Medium" + '"');
                        }
                    }
                }
                args.Add("-nologo");
                args.Add("-parallel");
                args.Add("none");
                // Save detailed results for investigation
                args.Add("-xml");
                args.Add('"' + Path.Combine(publishDir, "spark-xunit-results.xml") + '"');

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
                var combinedConsole = output + Environment.NewLine + error;
                File.WriteAllText(Path.Combine(publishDir, "spark-test-output.log"), combinedConsole);
                File.WriteAllText(Path.Combine(publishDir, "test-output.log"), combinedConsole);
                var lastLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                return lastLine ?? "[Runner] Tests executed via console runner.";
            }
            catch (Exception ex)
            {
                return "[Runner] Exception: " + ex.ToString();
            }
        }

        private static int ExtractInt(string text, string key)
        {
            try
            {
                var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    idx += key.Length;
                    int end = idx;
                    while (end < text.Length && char.IsDigit(text[end])) end++;
                    var num = text.Substring(idx, end - idx);
                    if (int.TryParse(num, out var val)) return val;
                }
            }
            catch { }
            return 0;
        }

        private static void CopyTestAssemblies(string testDllPath, string targetDir)
        {
            // Copy test DLL and dependencies from its build directory to the runner's publish directory
            var sourceDir = Path.GetDirectoryName(testDllPath);
            if (string.IsNullOrEmpty(sourceDir)) return;

            try
            {
                // Copy all DLLs and supporting files from test bin directory to publish directory
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(targetDir, fileName);
                    File.Copy(file, destPath, overwrite: true);
                }

                // Also copy .deps.json and .runtimeconfig.json if they exist
                foreach (var file in Directory.EnumerateFiles(sourceDir, "Game.Chess.Tests.Integration.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(file);
                    if (ext == ".json")
                    {
                        var fileName = Path.GetFileName(file);
                        var destPath = Path.Combine(targetDir, fileName);
                        File.Copy(file, destPath, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestExecutor] Warning: Failed to copy some test assemblies: {ex.Message}");
                // Don't fail completely - maybe enough files were copied
            }
        }

        private static string ResolveTestDll()
        {
            // Try to find the test DLL in its own build output directory
            // This allows independent iteration without locking issues
            
            // Common build output paths
            var candidates = new[]
            {
                // Release builds (most common)
                @"tests\Game.Chess.Tests.Integration\bin\Release\net8.0\Game.Chess.Tests.Integration.dll",
                @"tests\Game.Chess.Tests.Integration\bin\Debug\net8.0\Game.Chess.Tests.Integration.dll",
                // Relative to solution root
                @"..\Game.Chess.Tests.Integration\bin\Release\net8.0\Game.Chess.Tests.Integration.dll",
                @"..\Game.Chess.Tests.Integration\bin\Debug\net8.0\Game.Chess.Tests.Integration.dll",
            };

            // Start from current directory or solution root
            var startPath = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(startPath))
            {
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(startPath, candidate);
                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"[TestExecutor] Found test DLL: {fullPath}");
                        return fullPath;
                    }
                }

                // Move up one directory
                var parent = Path.GetDirectoryName(startPath);
                if (parent == startPath) break; // Reached root
                startPath = parent;
            }

            // Fallback to just the filename in current directory (will fail with clear message if not found)
            return "Game.Chess.Tests.Integration.dll";
        }
    }
}
