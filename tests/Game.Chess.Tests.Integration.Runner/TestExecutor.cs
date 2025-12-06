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
            try
            {
                CopyTestAssemblies(testDll, publishDir);
            }
            catch (Exception ex)
            {
                return $"[Runner] Failed to copy test assemblies: {ex.Message}";
            }

            testDll = Path.Combine(publishDir, Path.GetFileName(testDll));

            try
            {
                var adapterDll = Path.Combine(publishDir, "xunit.runner.visualstudio.testadapter.dll");
                var useVsTest = File.Exists(adapterDll);
                if (useVsTest)
                {
                    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    string dotnetExe = string.IsNullOrWhiteSpace(dotnetRoot) ? "dotnet" : Path.Combine(dotnetRoot, "dotnet.exe");

                    string? testCaseFilter = null;
                    if (!string.IsNullOrWhiteSpace(filter) && filter.Contains("="))
                    {
                        var kv = filter.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var name = kv[0].Trim();
                            var value = kv[1].Trim();
                            testCaseFilter = $"{name}={value}";
                        }
                    }

                    var trxPath = Path.Combine(publishDir, "spark-vstest.trx");
                    var vsArgs = new System.Collections.Generic.List<string> { "vstest", '"' + testDll + '"' };
                    if (!string.IsNullOrWhiteSpace(testCaseFilter)) vsArgs.Add("--TestCaseFilter:" + '"' + testCaseFilter + '"');
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

                    // Try discovery + per-test runs
                    try
                    {
                        var assemblyPath = testDll;
                        var timedOut = 0; var passed = 0; var failed = 0; var skipped = 0; var total = 0;
                        var asm = Assembly.LoadFrom(assemblyPath);
                        var tests = new System.Collections.Generic.List<string>();

                        foreach (var t in asm.GetTypes())
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                        {
                            var attrs = m.CustomAttributes;
                            if (!attrs.Any(a => a.AttributeType.Name == "FactAttribute" || a.AttributeType.Name == "TheoryAttribute")) continue;

                            var include = true;
                            if (!string.IsNullOrWhiteSpace(filter) && filter.Contains("="))
                            {
                                include = false;
                                var kv = filter.Split('=', 2);
                                if (kv.Length == 2)
                                {
                                    var name = kv[0].Trim();
                                    var value = kv[1].Trim();
                                    foreach (var ca in attrs)
                                    {
                                        if (ca.AttributeType.Name == "TraitAttribute")
                                        {
                                            try
                                            {
                                                if (ca.ConstructorArguments != null && ca.ConstructorArguments.Count >= 2)
                                                {
                                                    var an = ca.ConstructorArguments[0].Value?.ToString();
                                                    var av = ca.ConstructorArguments[1].Value?.ToString();
                                                    if (string.Equals(an, name, StringComparison.OrdinalIgnoreCase) && string.Equals(av, value, StringComparison.OrdinalIgnoreCase)) { include = true; break; }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }

                            if (include) tests.Add(t.FullName + "." + m.Name);
                        }

                        if (tests.Count > 0)
                        {
                            const int perTestTimeoutMs = 20_000;
                            var outLog = new System.Text.StringBuilder();
                            foreach (var fullyQualified in tests)
                            {
                                total++;
                                var singleArgs = new System.Collections.Generic.List<string> { "vstest", '"' + assemblyPath + '"', "--TestCaseFilter:" + '"' + $"FullyQualifiedName={fullyQualified}" + '"' };
                                var psiSingle = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = dotnetExe,
                                    Arguments = string.Join(" ", singleArgs),
                                    WorkingDirectory = publishDir,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                };

                                var p = System.Diagnostics.Process.Start(psiSingle);
                                if (p == null) { failed++; outLog.AppendLine($"[Runner] Failed to start process for {fullyQualified}"); continue; }

                                var exited = p.WaitForExit(perTestTimeoutMs);
                                string sout = string.Empty, serr = string.Empty;
                                try { sout = p.StandardOutput.ReadToEnd(); serr = p.StandardError.ReadToEnd(); } catch { }

                                if (!exited)
                                {
                                    try { p.Kill(true); } catch { }
                                    timedOut++;
                                    outLog.AppendLine($"[Runner] Timed out: {fullyQualified}");

                                    // Skip remaining tests to avoid waiting further
                                    var remaining = tests.Count - total;
                                    if (remaining > 0)
                                    {
                                        skipped += remaining;
                                        total += remaining; // count skipped towards total
                                        outLog.AppendLine($"[Runner] Skipping {remaining} remaining tests after timeout.");
                                    }

                                    outLog.AppendLine(sout); outLog.AppendLine(serr);
                                    File.AppendAllText(Path.Combine(publishDir, "spark-test-output.log"), outLog.ToString());
                                    File.AppendAllText(Path.Combine(publishDir, "test-output.log"), outLog.ToString());
                                    outLog.Clear();
                                    break;
                                }
                                else
                                {
                                    if (p.ExitCode == 0) { passed++; outLog.AppendLine($"[Runner] Passed: {fullyQualified}"); }
                                    else { failed++; outLog.AppendLine($"[Runner] Failed: {fullyQualified}"); }
                                }

                                outLog.AppendLine(sout); outLog.AppendLine(serr);
                                File.AppendAllText(Path.Combine(publishDir, "spark-test-output.log"), outLog.ToString());
                                File.AppendAllText(Path.Combine(publishDir, "test-output.log"), outLog.ToString());
                                outLog.Clear();
                            }

                            return $"VSTest Per-Test Summary: Total: {total}, Passed: {passed}, Failed: {failed}, TimedOut: {timedOut}, Skipped: {skipped}";
                        }
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(Path.Combine(publishDir, "test-output.log"), "[Runner] Per-test run failed, falling back. " + ex + Environment.NewLine);
                    }

                    var pVs = System.Diagnostics.Process.Start(psiVs);
                    if (pVs != null)
                    {
                        string outVs = pVs.StandardOutput.ReadToEnd();
                        string errVs = pVs.StandardError.ReadToEnd();
                        pVs.WaitForExit();
                        var combined = outVs + Environment.NewLine + errVs;
                        File.WriteAllText(Path.Combine(publishDir, "spark-test-output.log"), combined);
                        File.WriteAllText(Path.Combine(publishDir, "test-output.log"), combined);

                        if (File.Exists(trxPath))
                        {
                            try
                            {
                                var trx = File.ReadAllText(trxPath);
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

                var args = new System.Collections.Generic.List<string> { '"' + testDll + '"' };
                if (!string.IsNullOrWhiteSpace(filter) && filter.Contains("="))
                {
                    args.Add("-trait"); args.Add('"' + filter + '"');
                    var kv = filter.Split('=', 2);
                    if (kv.Length == 2 && kv[0].Trim().Equals("Performance", StringComparison.OrdinalIgnoreCase) && kv[1].Trim().Equals("Fast", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Add("-notrait"); args.Add('"' + "Performance=Slow" + '"'); args.Add("-notrait"); args.Add('"' + "Performance=Medium" + '"');
                    }
                }

                args.Add("-nologo"); args.Add("-parallel"); args.Add("none"); args.Add("-xml"); args.Add('"' + Path.Combine(publishDir, "spark-xunit-results.xml") + '"');

                System.Diagnostics.ProcessStartInfo psi;
                if (useDotnet)
                {
                    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    string dotnetExe = string.IsNullOrWhiteSpace(dotnetRoot) ? "dotnet" : Path.Combine(dotnetRoot, "dotnet.exe");
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
            var sourceDir = Path.GetDirectoryName(testDllPath);
            if (string.IsNullOrEmpty(sourceDir)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(targetDir, fileName);
                    File.Copy(file, destPath, overwrite: true);
                }

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
            }
        }

        private static string ResolveTestDll()
        {
            var candidates = new[]
            {
                @"tests\Game.Chess.Tests.Integration\bin\Release\net8.0\Game.Chess.Tests.Integration.dll",
                @"tests\Game.Chess.Tests.Integration\bin\Debug\net8.0\Game.Chess.Tests.Integration.dll",
                @"..\Game.Chess.Tests.Integration\bin\Release\net8.0\Game.Chess.Tests.Integration.dll",
                @"..\Game.Chess.Tests.Integration\bin\Debug\net8.0\Game.Chess.Tests.Integration.dll",
            };

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

                var parent = Path.GetDirectoryName(startPath);
                if (parent == startPath) break;
                startPath = parent;
            }

            return "Game.Chess.Tests.Integration.dll";
        }
    }
}
