using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Game.Chess.Tests.Integration.Runner
{
    public static class TestExecutor
    {
        // Runs tests from the integration test DLL by invoking the xUnit console runner via dotnet.
        // Returns a concise text summary.
        public static string RunTests(string publishDir, string filter, bool skipBuild = false)
        {
            // Use a unique log file per run inside a dedicated subfolder
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var logDir = Path.Combine(publishDir, "test-logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, $"test-output-{timestamp}.log");

            Console.WriteLine("[Runner] ===== TEST EXECUTION START =====");
            Console.WriteLine($"[Runner] Log file: {logPath}");
            File.AppendAllText(logPath, $"[Runner] ===== TEST EXECUTION START at {DateTime.UtcNow:o} =====" + Environment.NewLine);

            // Build the integration test project first to ensure latest binaries (unless skipBuild is true)
            if (!skipBuild)
            {
                var buildResult = BuildIntegrationTestProject(logPath);
                if (!string.IsNullOrEmpty(buildResult))
                {
                    return buildResult;
                }
            }
            else
            {
                Console.WriteLine("[Runner] Skipping build (using cached assemblies)");
                File.AppendAllText(logPath, "[Runner] Skipping build (using cached assemblies)" + Environment.NewLine);
            }

            // Resolve test DLL location
            string testDll = ResolveTestDll();
            if (!File.Exists(testDll))
            {
                var msg = $"[Runner] Test DLL not found: {testDll}";
                Console.WriteLine(msg);
                File.AppendAllText(logPath, msg + Environment.NewLine);
                return msg;
            }

            // Log the resolved DLL and its timestamp
            var testDllInfo = new FileInfo(testDll);
            var resolvedMsg = $"[Runner] Resolved test DLL: {testDll} (Size: {testDllInfo.Length} bytes, Modified: {testDllInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff})";
            Console.WriteLine(resolvedMsg);
            File.AppendAllText(logPath, resolvedMsg + Environment.NewLine);

            // Define lock targets - used throughout the method for cleanup
            var lockTargets = new[]
            {
                Path.Combine(publishDir, "Game.Chess.Tests.Integration.dll"),
                Path.Combine(publishDir, "Game.Chess.dll"),
                Path.Combine(publishDir, "Game.Chess.Serialization.dll"),
                Path.Combine(publishDir, "xunit.core.dll"),
                Path.Combine(publishDir, "xunit.runner.visualstudio.testadapter.dll"),
            };

            // Only clean and copy assemblies when not skipping build
            if (!skipBuild)
            {
                // Proactively kill any lingering vstest/testhost processes holding previous binaries
                try
                {
                    KillProcessesHolding(lockTargets, logPath, DateTime.UtcNow.AddMinutes(-5));
                }
                catch (Exception ex)
                {
                    var warn = $"[Runner] KillProcessesHolding(before clean) failed: {ex.Message}";
                    Console.WriteLine(warn);
                    File.AppendAllText(logPath, warn + Environment.NewLine);
                }

                // Remove any stale test assemblies from publish directory before copying fresh ones
                Console.WriteLine("[Runner] === Cleaning stale assemblies from publish directory ===");
                File.AppendAllText(logPath, "[Runner] === Cleaning stale assemblies from publish directory ===" + Environment.NewLine);
                var stalePatterns = new[] { "Game.Chess.Tests.Integration*.dll", "Game.Chess*.dll" };
                int deletedCount = 0;
                foreach (var pattern in stalePatterns)
                {
                    try
                    {
                        foreach (var staleFile in Directory.EnumerateFiles(publishDir, pattern))
                        {
                            // Skip the runner's own DLL - we can't delete it while it's running
                            if (staleFile.EndsWith("Game.Chess.Tests.Integration.Runner.dll", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[Runner]   Skipping runner's own DLL: {Path.GetFileName(staleFile)}");
                                File.AppendAllText(logPath, $"[Runner]   Skipping runner's own DLL: {Path.GetFileName(staleFile)}" + Environment.NewLine);
                                continue;
                            }

                            try
                            {
                                var staleInfo = new FileInfo(staleFile);
                                Console.WriteLine($"[Runner]   Deleting: {Path.GetFileName(staleFile)} (Size: {staleInfo.Length}, Modified: {staleInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff})");
                                File.AppendAllText(logPath, $"[Runner]   Deleting: {Path.GetFileName(staleFile)} (Size: {staleInfo.Length}, Modified: {staleInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff})" + Environment.NewLine);

                                var retries = 4;
                                bool deleted = false;
                                while (retries-- > 0 && !deleted)
                                {
                                    try
                                    {
                                        File.Delete(staleFile);
                                        deleted = true;
                                        deletedCount++;
                                    }
                                    catch (IOException ioEx) when (ioEx.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase) && retries > 0)
                                    {
                                        var retryMsg = $"[Runner]     Delete retry ({4 - retries}/4): still locked";
                                        Console.WriteLine(retryMsg);
                                        File.AppendAllText(logPath, retryMsg + Environment.NewLine);
                                        System.Threading.Thread.Sleep(750);
                                    }
                                }

                                if (!deleted)
                                {
                                    var warnMsg = $"[Runner]   Failed to delete after retries: {Path.GetFileName(staleFile)}";
                                    Console.WriteLine(warnMsg);
                                    File.AppendAllText(logPath, warnMsg + Environment.NewLine);
                                }
                            }
                            catch (Exception delEx)
                            {
                                var warnMsg = $"[Runner]   Failed to delete: {Path.GetFileName(staleFile)}: {delEx.Message}";
                                Console.WriteLine(warnMsg);
                                File.AppendAllText(logPath, warnMsg + Environment.NewLine);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var warnMsg = $"[Runner] Warning: Could not enumerate pattern {pattern}: {ex.Message}";
                        Console.WriteLine(warnMsg);
                        File.AppendAllText(logPath, warnMsg + Environment.NewLine);
                    }
                }
                var cleanMsg = $"[Runner] Cleaned {deletedCount} stale assemblies";
                Console.WriteLine(cleanMsg);
                File.AppendAllText(logPath, cleanMsg + Environment.NewLine);

                // Copy test DLL and its dependencies to publish dir so they can be loaded
                Console.WriteLine("[Runner] === Copying fresh test assemblies ===");
                File.AppendAllText(logPath, "[Runner] === Copying fresh test assemblies ===" + Environment.NewLine);
                try
                {
                    CopyTestAssemblies(testDll, publishDir, logPath);
                }
                catch (Exception ex)
                {
                    var errMsg = $"[Runner] Failed to copy test assemblies: {ex.Message}";
                    Console.WriteLine(errMsg);
                    File.AppendAllText(logPath, errMsg + Environment.NewLine);
                    return errMsg;
                }
            }
            else
            {
                // When skipping build, just verify the test DLL is already in publish dir
                var publishTestDll = Path.Combine(publishDir, Path.GetFileName(testDll));
                if (!File.Exists(publishTestDll))
                {
                    var errMsg = $"[Runner] Test DLL not found in publish dir (skipBuild mode): {publishTestDll}";
                    Console.WriteLine(errMsg);
                    File.AppendAllText(logPath, errMsg + Environment.NewLine);
                    return errMsg;
                }
            }

            testDll = Path.Combine(publishDir, Path.GetFileName(testDll));
            var copiedInfo = new FileInfo(testDll);
            var copiedMsg = $"[Runner] Using test DLL from publish dir: {testDll} (Size: {copiedInfo.Length} bytes, Modified: {copiedInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff})";
            Console.WriteLine(copiedMsg);
            File.AppendAllText(logPath, copiedMsg + Environment.NewLine);

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

                    var trxDir = Path.Combine(publishDir, "TestResults");
                    Directory.CreateDirectory(trxDir);
                    var trxPath = Path.Combine(trxDir, "TestResults.trx");
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
                        var runStart = DateTime.UtcNow;
                        
                        // Use collectible TestLoadContext to allow unloading after test discovery
                        var testContext = new TestLoadContext(Path.GetDirectoryName(assemblyPath));
                        
                        try
                        {
                            var asm = testContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(assemblyPath)));
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
                                var testResults = new System.Collections.Generic.List<(string Name, string Result, long DurationMs)>();
                                var stopwatch = System.Diagnostics.Stopwatch.StartNew(); // Start measuring duration
                                foreach (var fullyQualified in tests)
                                {
                                    total++;
                                    var testStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                    var singleArgs = new System.Collections.Generic.List<string> { "vstest", '"' + assemblyPath + '"', "--TestCaseFilter:" + '"' + $"FullyQualifiedName={fullyQualified}" + '"', "--UseVsixExtensions:false" };
                                    var safeTestName = fullyQualified.Replace(".", "_").Replace(" ", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace(":", "_").Replace("*", "_").Replace("?", "_").Replace("\"", "_").Replace("\\", "_").Replace("/", "_");
                                    var trxPath = Path.Combine(trxDir, $"{safeTestName}.trx");
                                    singleArgs.Add("--Logger:" + '"' + $"trx;LogFileName={Path.GetFileName(trxPath)}" + '"');
                                    var psiSingle = new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = dotnetExe,
                                        Arguments = string.Join(" ", singleArgs),
                                        WorkingDirectory = publishDir,
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                    };

                                    var testStart = DateTime.UtcNow;
                                    var p = System.Diagnostics.Process.Start(psiSingle);
                                    if (p == null) { failed++; outLog.AppendLine($"[Runner] Failed to start process for {fullyQualified}"); testResults.Add((fullyQualified, "FAILED", 0)); continue; }

                                    var exited = p.WaitForExit(perTestTimeoutMs);
                                    string sout = string.Empty, serr = string.Empty;
                                    
                                    if (!exited)
                                    {
                                        // Process didn't exit in time - kill it forcefully
                                        try 
                                        { 
                                            p.Kill(true); 
                                            // Give it a moment to die, then try to read output
                                            System.Threading.Thread.Sleep(500);
                                        } 
                                        catch { }
                                    
                                    testStopwatch.Stop();
                                    timedOut++;
                                    outLog.AppendLine($"[Runner] Timed out: {fullyQualified}");
                                    testResults.Add((fullyQualified, "TIMEOUT", testStopwatch.ElapsedMilliseconds));

                                    // Skip remaining tests to avoid waiting further
                                    var remaining = tests.Count - total;
                                    if (remaining > 0)
                                    {
                                        skipped += remaining;
                                        total += remaining; // count skipped towards total
                                        outLog.AppendLine($"[Runner] Skipping {remaining} remaining tests after timeout.");
                                    }

                                    try { sout = p.StandardOutput.ReadToEnd(); serr = p.StandardError.ReadToEnd(); } catch { }
                                    KillChildTestProcesses(testStart, publishDir, logPath);
                                    outLog.AppendLine(sout); outLog.AppendLine(serr);
                                    File.AppendAllText(logPath, outLog.ToString());
                                    outLog.Clear();
                                    
                                    // Output individual test results before summary
                                    var resultLines = new System.Text.StringBuilder();
                                    resultLines.AppendLine("[Runner Test Results]");
                                    foreach (var (name, result, duration) in testResults)
                                    {
                                        var durationSecs = duration / 1000.0;
                                        var color = result == "PASSED" ? "\u001b[32m" : result == "TIMEOUT" ? "\u001b[33m" : "\u001b[31m"; // Green for passed, yellow for timeout, red for failed
                                        var reset = "\u001b[0m";
                                        resultLines.AppendLine($"  {color}[{result}]{reset} {name} ({durationSecs:F3}s)");
                                    }
                                    var summary = resultLines.ToString() + $"VSTest Per-Test Summary: Total: {total}, Passed: {passed}, Failed: {failed}, TimedOut: {timedOut}, Skipped: {skipped}";
                                    File.AppendAllText(logPath, summary);
                                    
                                    // Return immediately with results instead of falling back to VSTest,
                                    // which would re-run all tests
                                    return summary;
                                }
                                
                                testStopwatch.Stop();
                                // Process exited normally - read its output
                                try { sout = p.StandardOutput.ReadToEnd(); serr = p.StandardError.ReadToEnd(); } catch { }
                                KillChildTestProcesses(testStart, publishDir, logPath);

                                if (p.ExitCode == 0) { passed++; outLog.AppendLine($"[Runner] Passed: {fullyQualified}"); testResults.Add((fullyQualified, "PASSED", testStopwatch.ElapsedMilliseconds)); }
                                else { failed++; outLog.AppendLine($"[Runner] Failed: {fullyQualified}"); testResults.Add((fullyQualified, "FAILED", testStopwatch.ElapsedMilliseconds)); }

                                outLog.AppendLine(sout); outLog.AppendLine(serr);
                                File.AppendAllText(logPath, outLog.ToString());
                                outLog.Clear();
                            }
                            stopwatch.Stop(); // Stop measuring duration
                            var durationTotalMs = stopwatch.ElapsedMilliseconds; // Calculate total duration
                            
                            // Output individual test results
                            var resultLines2 = new System.Text.StringBuilder();
                            resultLines2.AppendLine("[Runner Test Results]");
                            foreach (var (name, result, duration) in testResults)
                            {
                                var durationSecs = duration / 1000.0;
                                var color = result == "PASSED" ? "\u001b[32m" : result == "TIMEOUT" ? "\u001b[33m" : "\u001b[31m"; // Green for passed, yellow for timeout, red for failed
                                var reset = "\u001b[0m";
                                resultLines2.AppendLine($"  {color}[{result}]{reset} {name} ({durationSecs:F3}s)");
                            }
                            var finalSummary = resultLines2.ToString() + $"VSTest Per-Test Summary: Total: {total}, Passed: {passed}, Failed: {failed}, TimedOut: {timedOut}, Skipped: {skipped} Duration: {durationTotalMs}ms";
                            try { KillProcessesHolding(lockTargets, logPath, runStart); }
                            catch (Exception ex)
                            {
                                var warn = $"[Runner] KillProcessesHolding(final summary) failed: {ex.Message}";
                                Console.WriteLine(warn);
                                File.AppendAllText(logPath, warn + Environment.NewLine);
                            }
                            return finalSummary;
                        }
                        else
                        {
                            // No tests found after filtering
                            try { KillProcessesHolding(lockTargets, logPath, DateTime.UtcNow.AddMinutes(-5)); }
                            catch (Exception ex)
                            {
                                var warn = $"[Runner] KillProcessesHolding(no tests) failed: {ex.Message}";
                                Console.WriteLine(warn);
                                File.AppendAllText(logPath, warn + Environment.NewLine);
                            }
                            return "[Runner] No tests matched the filter criteria.";
                        }
                    }
                    finally
                    {
                        // Only delete test DLLs when NOT in skipBuild mode (to allow reuse of cached assemblies)
                        if (!skipBuild)
                        {
                            // AGGRESSIVE: Delete test DLLs immediately to ensure they're not locked for next run
                            var testDllsToDelete = new[]
                            {
                                Path.Combine(publishDir, "Game.Chess.Tests.Integration.dll"),
                                Path.Combine(publishDir, "Game.Chess.dll"),
                                Path.Combine(publishDir, "xunit.core.dll"),
                                Path.Combine(publishDir, "xunit.runner.visualstudio.testadapter.dll"),
                            };
                            
                            foreach (var dllPath in testDllsToDelete)
                            {
                                if (!File.Exists(dllPath)) continue;
                                
                                for (int attempt = 0; attempt < 10; attempt++)
                                {
                                    try
                                    {
                                        File.Delete(dllPath);
                                        Console.WriteLine($"[Runner] Deleted test DLL: {Path.GetFileName(dllPath)}");
                                        File.AppendAllText(logPath, $"[Runner] Deleted test DLL: {Path.GetFileName(dllPath)}" + Environment.NewLine);
                                        break;
                                    }
                                    catch (IOException) when (attempt < 9)
                                    {
                                        System.Threading.Thread.Sleep(100);
                                    }
                                    catch (Exception ex)
                                    {
                                        var warn = $"[Runner] Failed to delete {Path.GetFileName(dllPath)} (attempt {attempt + 1}): {ex.Message}";
                                        Console.WriteLine(warn);
                                        File.AppendAllText(logPath, warn + Environment.NewLine);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[Runner] Skipping DLL deletion (using cached assemblies for subsequent tests)");
                            File.AppendAllText(logPath, "[Runner] Skipping DLL deletion (using cached assemblies for subsequent tests)" + Environment.NewLine);
                        }

                        // Force explicit handle release before unloading context
                        try
                        {
                            ForceReleaseTestDllHandles(lockTargets, logPath);
                        }
                        catch (Exception ex)
                        {
                            var warn = $"[Runner] ForceReleaseTestDllHandles failed: {ex.Message}";
                            Console.WriteLine(warn);
                            File.AppendAllText(logPath, warn + Environment.NewLine);
                        }

                        // Unload the test assembly context to release all DLL locks
                        Console.WriteLine("[Runner] === UNLOADING TEST ASSEMBLY CONTEXT ===");
                        Console.WriteLine("[Runner] Calling testContext.Unload()...");
                        testContext.Unload();
                        Console.WriteLine("[Runner] Context unloaded. Running GC.Collect()...");
                        GC.Collect();
                        Console.WriteLine("[Runner] Running GC.WaitForPendingFinalizers()...");
                        GC.WaitForPendingFinalizers();
                        Console.WriteLine("[Runner] Final GC.Collect()...");
                        GC.Collect();
                        Console.WriteLine("[Runner] === TEST ASSEMBLY CONTEXT FULLY RELEASED ===");
                        Console.WriteLine("[Runner] All DLL locks should now be released.");
                        File.AppendAllText(logPath, "[Runner] === TEST ASSEMBLY CONTEXT FULLY RELEASED ===" + Environment.NewLine);
                        File.AppendAllText(logPath, "[Runner] All DLL locks released." + Environment.NewLine);

                        // POST-UNLOAD VERIFICATION: Ensure files are ACTUALLY unlocked with exclusive access test
                        Console.WriteLine("[Runner] === POST-UNLOAD FILE ACCESS VERIFICATION ===");
                        File.AppendAllText(logPath, "[Runner] === POST-UNLOAD FILE ACCESS VERIFICATION ===" + Environment.NewLine);
                        VerifyExclusiveFileAccess(lockTargets, logPath);

                        // FINAL CLEANUP: Kill any remaining processes that still hold locks
                        Console.WriteLine("[Runner] === FINAL PROCESS CLEANUP ===");
                        File.AppendAllText(logPath, "[Runner] === FINAL PROCESS CLEANUP ===" + Environment.NewLine);
                        try
                        {
                            KillProcessesHolding(lockTargets, logPath);
                        }
                        catch (Exception ex)
                        {
                            var warn = $"[Runner] Final KillProcessesHolding failed: {ex.Message}";
                            Console.WriteLine(warn);
                            File.AppendAllText(logPath, warn + Environment.NewLine);
                        }

                        // Final verification after cleanup
                        Console.WriteLine("[Runner] === FINAL VERIFICATION AFTER CLEANUP ===");
                        File.AppendAllText(logPath, "[Runner] === FINAL VERIFICATION AFTER CLEANUP ===" + Environment.NewLine);
                        VerifyExclusiveFileAccess(lockTargets, logPath);
                    }
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(logPath, "[Runner] Per-test run failed. " + ex + Environment.NewLine);
                        return "[Runner] Per-test runner failed; no fallback executed.";
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
                    var dotnetRootConsole = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    string dotnetExeConsole = string.IsNullOrWhiteSpace(dotnetRootConsole) ? "dotnet" : Path.Combine(dotnetRootConsole, "dotnet.exe");
                    psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dotnetExeConsole,
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
                proc.WaitForExit();
                var output = SafeRead(proc.StandardOutput); var error = SafeRead(proc.StandardError);
                var combinedConsole = output + Environment.NewLine + error;
                File.WriteAllText(logPath, combinedConsole);
                try { KillChildTestProcesses(proc.StartTime.ToUniversalTime(), publishDir, logPath); } catch { }
                System.Threading.Thread.Sleep(500);
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

        private static void CopyTestAssemblies(string testDllPath, string targetDir, string logPath)
        {
            var sourceDir = Path.GetDirectoryName(testDllPath);
            if (string.IsNullOrEmpty(sourceDir)) return;

            int copiedCount = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(targetDir, fileName);
                    var srcInfo = new FileInfo(file);
                    try
                    {
                        CopyWithRetry(file, destPath, logPath);
                        var destInfo = new FileInfo(destPath);
                        var copyMsg = $"[Runner]   Copied: {fileName} ({srcInfo.Length} -> {destInfo.Length} bytes)";
                        Console.WriteLine(copyMsg);
                        File.AppendAllText(logPath, copyMsg + Environment.NewLine);
                        copiedCount++;
                    }
                    catch (Exception cpEx)
                    {
                        var errMsg = $"[Runner]   Failed to copy {fileName}: {cpEx.Message}";
                        Console.WriteLine(errMsg);
                        File.AppendAllText(logPath, errMsg + Environment.NewLine);
                    }
                }

                foreach (var file in Directory.EnumerateFiles(sourceDir, "Game.Chess.Tests.Integration.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(file);
                    if (ext == ".json")
                    {
                        var fileName = Path.GetFileName(file);
                        var destPath = Path.Combine(targetDir, fileName);
                        try
                        {
                            CopyWithRetry(file, destPath, logPath);
                            var copyMsg = $"[Runner]   Copied config: {fileName}";
                            Console.WriteLine(copyMsg);
                            File.AppendAllText(logPath, copyMsg + Environment.NewLine);
                        }
                        catch (Exception cfEx)
                        {
                            var warnMsg = $"[Runner]   Failed to copy config {fileName}: {cfEx.Message}";
                            Console.WriteLine(warnMsg);
                            File.AppendAllText(logPath, warnMsg + Environment.NewLine);
                        }
                    }
                }
                
                var summaryMsg = $"[Runner] Copied {copiedCount} assemblies total";
                Console.WriteLine(summaryMsg);
                File.AppendAllText(logPath, summaryMsg + Environment.NewLine);
            }
            catch (Exception ex)
            {
                var warnMsg = $"[Runner] Warning: Failed to copy some test assemblies: {ex.Message}";
                Console.WriteLine(warnMsg);
                File.AppendAllText(logPath, warnMsg + Environment.NewLine);
            }
        }

        private static void CopyWithRetry(string source, string dest, string logPath, int attempts = 5, int delayMs = 400)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    File.Copy(source, dest, overwrite: true);
                    return;
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase) && attempt < attempts - 1)
                {
                    attempt++;
                    var retryMsg = $"[Runner]     Copy retry {attempt}/{attempts} for {Path.GetFileName(dest)} (locked)";
                    Console.WriteLine(retryMsg);
                    File.AppendAllText(logPath, retryMsg + Environment.NewLine);
                    System.Threading.Thread.Sleep(delayMs);
                }
            }
        }

        private static string SafeRead(StreamReader reader)
        {
            try { return reader.ReadToEnd(); }
            catch { return string.Empty; }
        }

        // Kill lingering testhost/vstest processes started after 'sinceUtc' that still live in publishDir
        private static void KillChildTestProcesses(DateTime sinceUtc, string publishDir, string logPath)
        {
            var names = new[] { "vstest.console", "testhost", "datacollector" };
            var currentPid = Process.GetCurrentProcess().Id;
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == currentPid) continue;
                try
                {
                    if (!names.Any(n => proc.ProcessName.StartsWith(n, StringComparison.OrdinalIgnoreCase))) continue;
                    if (proc.StartTime.ToUniversalTime() < sinceUtc.AddSeconds(-2)) continue;

                    bool inPublish = false;
                    try
                    {
                        var mainPath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(mainPath) && mainPath.StartsWith(publishDir, StringComparison.OrdinalIgnoreCase)) inPublish = true;
                    }
                    catch { }

                    if (!inPublish) continue;

                    var msg = $"[Runner] Killing lingering test process {proc.ProcessName} (PID={proc.Id})";
                    Console.WriteLine(msg);
                    File.AppendAllText(logPath, msg + Environment.NewLine);
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(3000);
                }
                catch { }
            }
        }

        // Kill processes that currently hold any of the target files
        private static void KillProcessesHolding(string[] targetFiles, string logPath, DateTime sinceUtc)
        {
            var normalizedTargets = targetFiles
                .Where(File.Exists)
                .Select(p => Path.GetFullPath(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedTargets.Count == 0) return;

            var currentPid = Process.GetCurrentProcess().Id;
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == currentPid) continue;
                if (proc.StartTime.ToUniversalTime() < sinceUtc.AddSeconds(-2)) continue;

                bool matched = false;
                try
                {
                    foreach (ProcessModule mod in proc.Modules)
                    {
                        var modPath = mod.FileName;
                        if (normalizedTargets.Contains(modPath))
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                catch { }

                if (!matched) continue;

                var msg = $"[Runner] Detected process holding target DLL(s): {proc.ProcessName} (PID={proc.Id})";
                Console.WriteLine(msg);
                File.AppendAllText(logPath, msg + Environment.NewLine);

                try
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    var warn = $"[Runner]   Failed to kill process {proc.Id}: {ex.Message}";
                    Console.WriteLine(warn);
                    File.AppendAllText(logPath, warn + Environment.NewLine);
                }
            }
        }

        // Kill any lingering testhost/vstest processes that still hold locks on given files.
        // Uses multi-stage termination strategy: graceful → hard kill → force kill → verify
        private static void KillProcessesHolding(string[] targetFiles, string logPath)
        {
            var normalizedTargets = targetFiles
                .Where(File.Exists)
                .Select(p => Path.GetFullPath(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedTargets.Count == 0) return;

            var currentPid = Process.GetCurrentProcess().Id;
            var processesToKill = new System.Collections.Generic.List<(Process proc, string reason)>();
            
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == currentPid) continue; // never kill ourselves

                bool matched = false;
                try
                {
                    foreach (ProcessModule mod in proc.Modules)
                    {
                        var modPath = mod.FileName;
                        if (normalizedTargets.Contains(modPath))
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore access issues (system processes)
                }

                if (!matched) continue;

                var msg = $"[Runner] Detected process holding target DLL(s): {proc.ProcessName} (PID={proc.Id})";
                Console.WriteLine(msg);
                File.AppendAllText(logPath, msg + Environment.NewLine);
                processesToKill.Add((proc, msg));
            }

            if (processesToKill.Count == 0) return;

            // Stage 1: Graceful termination (3 seconds)
            var stage1Start = DateTime.UtcNow;
            Console.WriteLine("[Runner]   Stage 1: Graceful termination (3s timeout)...");
            File.AppendAllText(logPath, "[Runner]   Stage 1: Graceful termination (3s timeout)..." + Environment.NewLine);
            foreach (var (proc, _) in processesToKill)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch { }
            }
            
            var stage1Remaining = new System.Collections.Generic.List<(Process, string)>();
            foreach (var (proc, reason) in processesToKill)
            {
                if (!proc.WaitForExit(3000))
                {
                    var warn = $"[Runner]     Process {proc.Id} ({proc.ProcessName}) did not exit in Stage 1";
                    Console.WriteLine(warn);
                    File.AppendAllText(logPath, warn + Environment.NewLine);
                    stage1Remaining.Add((proc, reason));
                }
                else
                {
                    var ok = $"[Runner]     Killed process {proc.Id} ({proc.ProcessName}) in Stage 1";
                    Console.WriteLine(ok);
                    File.AppendAllText(logPath, ok + Environment.NewLine);
                }
            }

            // Stage 2: Hard kill via taskkill (2 seconds)
            if (stage1Remaining.Count > 0)
            {
                Console.WriteLine("[Runner]   Stage 2: Hard kill via taskkill (2s timeout)...");
                File.AppendAllText(logPath, "[Runner]   Stage 2: Hard kill via taskkill (2s timeout)..." + Environment.NewLine);
                foreach (var (proc, _) in stage1Remaining)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/PID {proc.Id} /F",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        var tk = System.Diagnostics.Process.Start(psi);
                        tk?.WaitForExit(2000);
                        var msg = $"[Runner]     Taskkill issued for {proc.Id} ({proc.ProcessName})";
                        Console.WriteLine(msg);
                        File.AppendAllText(logPath, msg + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        var warn = $"[Runner]     Taskkill failed for {proc.Id}: {ex.Message}";
                        Console.WriteLine(warn);
                        File.AppendAllText(logPath, warn + Environment.NewLine);
                    }
                }
                // Extended wait to allow processes to fully exit asynchronously
                Console.WriteLine("[Runner]   Waiting 3 seconds for processes to fully exit...");
                File.AppendAllText(logPath, "[Runner]   Waiting 3 seconds for processes to fully exit..." + Environment.NewLine);
                System.Threading.Thread.Sleep(3000);
            }

            // Verify files are now unlocked (with extended stability checks)
            VerifyFilesAreUnlocked(normalizedTargets.ToArray(), logPath);
        }

        // Verify exclusive file access with longer timeout - called AFTER context unload
        // CRITICAL: Includes stability check to ensure files don't immediately become locked again
        private static void VerifyExclusiveFileAccess(string[] targetFiles, string logPath)
        {
            const int maxWaitSeconds = 15;
            const int pollIntervalMs = 500;
            const int stabilityChecks = 3; // Must pass this many consecutive unlocked checks
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var unlockedFiles = new System.Collections.Generic.List<string>();
            var lockedFiles = new System.Collections.Generic.List<string>();
            int consecutiveUnlockedChecks = 0;

            Console.WriteLine($"[Runner] Verifying exclusive file access (max {maxWaitSeconds}s, requires {stabilityChecks} consecutive checks)...");
            File.AppendAllText(logPath, $"[Runner] Verifying exclusive file access (max {maxWaitSeconds}s, requires {stabilityChecks} consecutive checks)..." + Environment.NewLine);

            while (stopwatch.Elapsed.TotalSeconds < maxWaitSeconds)
            {
                unlockedFiles.Clear();
                lockedFiles.Clear();

                foreach (var filePath in targetFiles)
                {
                    if (!File.Exists(filePath))
                    {
                        // File doesn't exist - that's OK
                        unlockedFiles.Add(Path.GetFileName(filePath));
                        continue;
                    }

                    try
                    {
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            unlockedFiles.Add(Path.GetFileName(filePath));
                        }
                    }
                    catch (IOException)
                    {
                        lockedFiles.Add(Path.GetFileName(filePath));
                    }
                }

                // If all files are unlocked, check for stability
                if (lockedFiles.Count == 0)
                {
                    consecutiveUnlockedChecks++;
                    var checkMsg = $"[Runner]   Stability check {consecutiveUnlockedChecks}/{stabilityChecks} passed";
                    Console.WriteLine(checkMsg);
                    File.AppendAllText(logPath, checkMsg + Environment.NewLine);

                    // Only succeed after multiple consecutive checks show files are unlocked
                    if (consecutiveUnlockedChecks >= stabilityChecks)
                    {
                        var success = $"[Runner] ✓ All {unlockedFiles.Count} target files have exclusive access (stable)";
                        Console.WriteLine(success);
                        File.AppendAllText(logPath, success + Environment.NewLine);
                        return;
                    }
                    
                    // Wait before next stability check
                    System.Threading.Thread.Sleep(pollIntervalMs);
                }
                else
                {
                    // File(s) locked - reset stability counter
                    if (consecutiveUnlockedChecks > 0)
                    {
                        var resetMsg = $"[Runner]   Stability check reset (files locked): {string.Join(", ", lockedFiles)}";
                        Console.WriteLine(resetMsg);
                        File.AppendAllText(logPath, resetMsg + Environment.NewLine);
                        consecutiveUnlockedChecks = 0;
                    }

                    // Still locked - wait and retry
                    System.Threading.Thread.Sleep(pollIntervalMs);
                }
            }

            // Timed out with some files still locked
            stopwatch.Stop();
            var warning = $"[Runner] ✗ After {stopwatch.Elapsed.TotalSeconds:F1}s: {lockedFiles.Count} file(s) remain locked: {string.Join(", ", lockedFiles)}";
            Console.WriteLine(warning);
            File.AppendAllText(logPath, warning + Environment.NewLine);

            // Try to identify which processes hold the locks
            var lockedFileSet = new System.Collections.Generic.HashSet<string>(lockedFiles, StringComparer.OrdinalIgnoreCase);
            var holdingProcesses = new System.Collections.Generic.List<(string Name, int Id)>();
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        foreach (ProcessModule mod in proc.Modules)
                        {
                            var modPath = mod.FileName;
                            if (!string.IsNullOrEmpty(modPath) && lockedFileSet.Contains(Path.GetFileName(modPath)))
                            {
                                var procMsg = $"[Runner]   Process {proc.ProcessName} (PID {proc.Id}) holds lock on {Path.GetFileName(modPath)}";
                                Console.WriteLine(procMsg);
                                File.AppendAllText(logPath, procMsg + Environment.NewLine);
                                holdingProcesses.Add((proc.ProcessName, proc.Id));
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (holdingProcesses.Count == 0)
            {
                var diagMsg = "[Runner]   WARNING: Files appear locked but no holding processes found. This may be a Windows file system caching issue.";
                Console.WriteLine(diagMsg);
                File.AppendAllText(logPath, diagMsg + Environment.NewLine);
            }
        }

        // Verify that all target files are no longer locked by attempting exclusive open
        private static void VerifyFilesAreUnlocked(string[] targetFiles, string logPath)
        {
            Console.WriteLine("[Runner]   Verifying all target files are unlocked...");
            File.AppendAllText(logPath, "[Runner]   Verifying all target files are unlocked..." + Environment.NewLine);

            var lockedFiles = new System.Collections.Generic.List<string>();
            const int maxRetries = 10;
            const int retryDelayMs = 200;

            foreach (var filePath in targetFiles)
            {
                if (!File.Exists(filePath)) continue;

                bool unlocked = false;
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // Successfully opened exclusively - file is unlocked
                            unlocked = true;
                            var ok = $"[Runner]     ✓ {Path.GetFileName(filePath)} is unlocked";
                            Console.WriteLine(ok);
                            File.AppendAllText(logPath, ok + Environment.NewLine);
                            break;
                        }
                    }
                    catch (IOException) when (attempt < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    catch (Exception ex)
                    {
                        var warn = $"[Runner]     Failed to verify {Path.GetFileName(filePath)}: {ex.Message}";
                        Console.WriteLine(warn);
                        File.AppendAllText(logPath, warn + Environment.NewLine);
                        break;
                    }
                }

                if (!unlocked)
                {
                    lockedFiles.Add(filePath);
                    var warn = $"[Runner]     ✗ {Path.GetFileName(filePath)} remains LOCKED after retries";
                    Console.WriteLine(warn);
                    File.AppendAllText(logPath, warn + Environment.NewLine);
                }
            }

            if (lockedFiles.Count > 0)
            {
                var summary = $"[Runner]   WARNING: {lockedFiles.Count} file(s) remain locked: {string.Join(", ", lockedFiles.Select(Path.GetFileName))}";
                Console.WriteLine(summary);
                File.AppendAllText(logPath, summary + Environment.NewLine);
            }
            else
            {
                var success = "[Runner]   ✓ All target files verified as unlocked";
                Console.WriteLine(success);
                File.AppendAllText(logPath, success + Environment.NewLine);
            }
        }

        // Explicitly release DLL file handles by attempting to open and close each file
        private static void ForceReleaseTestDllHandles(string[] targetFiles, string logPath)
        {
            Console.WriteLine("[Runner] === FORCING TEST DLL HANDLE RELEASE ===");
            File.AppendAllText(logPath, "[Runner] === FORCING TEST DLL HANDLE RELEASE ===" + Environment.NewLine);

            const int maxRetries = 5;
            const int retryDelayMs = 200;
            const int totalTimeoutSec = 5;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var filePath in targetFiles)
            {
                if (!File.Exists(filePath)) continue;

                bool released = false;
                for (int attempt = 0; attempt < maxRetries && stopwatch.Elapsed.TotalSeconds < totalTimeoutSec; attempt++)
                {
                    try
                    {
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // Successfully opened exclusively - handle released
                            released = true;
                            var ok = $"[Runner]   Released handle: {Path.GetFileName(filePath)}";
                            Console.WriteLine(ok);
                            File.AppendAllText(logPath, ok + Environment.NewLine);
                            break;
                        }
                    }
                    catch (IOException) when (attempt < maxRetries - 1)
                    {
                        var retry = $"[Runner]     Handle release retry {attempt + 1}/{maxRetries} for {Path.GetFileName(filePath)}";
                        Console.WriteLine(retry);
                        File.AppendAllText(logPath, retry + Environment.NewLine);
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    catch (Exception ex)
                    {
                        var warn = $"[Runner]   Failed to release handle for {Path.GetFileName(filePath)}: {ex.Message}";
                        Console.WriteLine(warn);
                        File.AppendAllText(logPath, warn + Environment.NewLine);
                        break;
                    }
                }

                if (!released)
                {
                    var warn = $"[Runner]   WARNING: Could not release exclusive handle for {Path.GetFileName(filePath)} within timeout";
                    Console.WriteLine(warn);
                    File.AppendAllText(logPath, warn + Environment.NewLine);
                }
            }

            stopwatch.Stop();
            var complete = $"[Runner] === HANDLE RELEASE COMPLETE (took {stopwatch.ElapsedMilliseconds}ms) ===";
            Console.WriteLine(complete);
            File.AppendAllText(logPath, complete + Environment.NewLine);
        }

        private static string ResolveTestDll()
        {
            // Prioritize RELEASE builds over DEBUG since the runner itself is typically Release
            var candidates = new[]
            {
                Path.Combine("tests", "Game.Chess.Tests.Integration", "bin", "Release", "net8.0", "publish", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("..", "Game.Chess.Tests.Integration", "bin", "Release", "net8.0", "publish", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("tests", "Game.Chess.Tests.Integration", "bin", "Release", "net8.0", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("..", "Game.Chess.Tests.Integration", "bin", "Release", "net8.0", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("tests", "Game.Chess.Tests.Integration", "bin", "Debug", "net8.0", "publish", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("..", "Game.Chess.Tests.Integration", "bin", "Debug", "net8.0", "publish", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("tests", "Game.Chess.Tests.Integration", "bin", "Debug", "net8.0", "Game.Chess.Tests.Integration.dll"),
                Path.Combine("..", "Game.Chess.Tests.Integration", "bin", "Debug", "net8.0", "Game.Chess.Tests.Integration.dll"),
            };

            var startPath = Directory.GetCurrentDirectory();
            Console.WriteLine($"[ResolveTestDll] Starting search from: {startPath}");
            while (!string.IsNullOrEmpty(startPath))
            {
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(startPath, candidate);
                    Console.WriteLine($"[ResolveTestDll] Checking: {fullPath}");
                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"[ResolveTestDll] Found test DLL: {fullPath}");
                        return fullPath;
                    }
                }

                var parent = Path.GetDirectoryName(startPath);
                if (parent == startPath) break;
                startPath = parent;
            }

            return "Game.Chess.Tests.Integration.dll";
        }

        private static string BuildIntegrationTestProject(string logPath)
        {
            try
            {
                // Find the integration test project file (using Path.Combine for cross-platform)
                var projectCandidates = new[]
                {
                    Path.Combine("tests", "Game.Chess.Tests.Integration", "Game.Chess.Tests.Integration.csproj"),
                    Path.Combine("..", "Game.Chess.Tests.Integration", "Game.Chess.Tests.Integration.csproj"),
                };

                string projectPath = null;
                var startPath = Directory.GetCurrentDirectory();
                var searchPath = startPath;

                while (!string.IsNullOrEmpty(searchPath))
                {
                    foreach (var candidate in projectCandidates)
                    {
                        var fullPath = Path.Combine(searchPath, candidate);
                        if (File.Exists(fullPath))
                        {
                            projectPath = fullPath;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(projectPath)) break;

                    var parent = Path.GetDirectoryName(searchPath);
                    if (parent == searchPath) break;
                    searchPath = parent;
                }

                if (string.IsNullOrEmpty(projectPath))
                {
                    var msg = "[Builder] Integration test project file not found";
                    File.AppendAllText(logPath, msg + Environment.NewLine);
                    return msg;
                }

                var buildMsg = $"[Builder] Building project: {projectPath}";
                Console.WriteLine(buildMsg);
                File.AppendAllText(logPath, buildMsg + Environment.NewLine);

                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                string dotnetExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
                string dotnetExe = string.IsNullOrWhiteSpace(dotnetRoot) ? "dotnet" : Path.Combine(dotnetRoot, dotnetExeName);

                // Determine publish output directory based on project location
                var projectDir = Path.GetDirectoryName(projectPath);
                var publishDir = Path.Combine(projectDir, "bin", "Release", "net8.0", "publish");
                var publishMsg = $"[Builder] Expected publish directory: {publishDir}";
                Console.WriteLine(publishMsg);
                File.AppendAllText(logPath, publishMsg + Environment.NewLine);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dotnetExe,
                    Arguments = $"publish \"{projectPath}\" -c Release",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null)
                {
                    var msg = "[Builder] Failed to start build process";
                    File.AppendAllText(logPath, msg + Environment.NewLine);
                    return msg;
                }

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                // Log build output to file for visibility
                File.AppendAllText(logPath, "[Builder] --- Build Output ---" + Environment.NewLine);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("[Builder] --- Build Output ---");
                    Console.WriteLine(output);
                    File.AppendAllText(logPath, output + Environment.NewLine);
                }
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine("[Builder] --- Build Errors/Warnings ---");
                    Console.WriteLine(error);
                    File.AppendAllText(logPath, "[Builder] --- Build Errors/Warnings ---" + Environment.NewLine);
                    File.AppendAllText(logPath, error + Environment.NewLine);
                }

                if (proc.ExitCode != 0)
                {
                    var failMsg = $"[Builder] Build failed with exit code {proc.ExitCode}";
                    Console.WriteLine(failMsg);
                    File.AppendAllText(logPath, failMsg + Environment.NewLine);
                    return failMsg;
                }

                // After successful build, verify the publish output
                var dllPath = Path.Combine(publishDir, "Game.Chess.Tests.Integration.dll");
                if (File.Exists(dllPath))
                {
                    var dllInfo = new FileInfo(dllPath);
                    var verifyMsg = $"[Builder] Verified published DLL: {dllPath} (Size: {dllInfo.Length} bytes, Modified: {dllInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff})";
                    Console.WriteLine(verifyMsg);
                    File.AppendAllText(logPath, verifyMsg + Environment.NewLine);
                }
                else
                {
                    var warnMsg = $"[Builder] WARNING: Published DLL not found at expected location: {dllPath}";
                    Console.WriteLine(warnMsg);
                    File.AppendAllText(logPath, warnMsg + Environment.NewLine);
                }

                var successMsg = "[Builder] Build completed successfully";
                Console.WriteLine(successMsg);
                File.AppendAllText(logPath, successMsg + Environment.NewLine);
                return null; // Success - return null to indicate no error
            }
            catch (Exception ex)
            {
                var errMsg = $"[Builder] Exception during build: {ex.Message}";
                Console.WriteLine(errMsg);
                File.AppendAllText(logPath, errMsg + Environment.NewLine);
                return errMsg;
            }
        }

        // Rebuild and republish test assemblies without running tests
        public static string RebuildTestAssemblies(string publishDir)
        {
            // Use a temporary log file for the rebuild
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var logDir = Path.Combine(publishDir, "test-logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, $"rebuild-{timestamp}.log");

            Console.WriteLine("[Rebuild] ===== REBUILD START =====");
            Console.WriteLine($"[Rebuild] Log file: {logPath}");
            File.AppendAllText(logPath, $"[Rebuild] ===== REBUILD START at {DateTime.UtcNow:o} =====" + Environment.NewLine);

            // Build the integration test project
            var buildResult = BuildIntegrationTestProject(logPath);
            if (!string.IsNullOrEmpty(buildResult))
            {
                return buildResult;
            }

            // Resolve test DLL location
            string testDll = ResolveTestDll();
            if (!File.Exists(testDll))
            {
                var msg = $"[Rebuild] Test DLL not found after build: {testDll}";
                Console.WriteLine(msg);
                File.AppendAllText(logPath, msg + Environment.NewLine);
                return msg;
            }

            // Copy test DLL and its dependencies to publish dir
            Console.WriteLine("[Rebuild] === Copying fresh test assemblies ===");
            File.AppendAllText(logPath, "[Rebuild] === Copying fresh test assemblies ===" + Environment.NewLine);
            try
            {
                CopyTestAssemblies(testDll, publishDir, logPath);
            }
            catch (Exception ex)
            {
                var errMsg = $"[Rebuild] Failed to copy test assemblies: {ex.Message}";
                Console.WriteLine(errMsg);
                File.AppendAllText(logPath, errMsg + Environment.NewLine);
                return errMsg;
            }

            Console.WriteLine("[Rebuild] ✓ Rebuild and republish complete");
            File.AppendAllText(logPath, "[Rebuild] ✓ Rebuild and republish complete" + Environment.NewLine);
            return "[Rebuild] ✓ Test assemblies rebuilt and republished successfully\n";
        }
    }
}
