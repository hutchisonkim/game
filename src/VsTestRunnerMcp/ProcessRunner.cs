using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VsTestRunnerMcp
{
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
        public string Combined { get; set; } = string.Empty;
    }

    public class ProcessRunner
    {
        private string FindPwshExecutable()
        {
            // Prefer pwsh, fallback to powershell.exe
            return "pwsh"; // rely on PATH; allow environment/host to override
        }

        public async Task<ProcessResult> RunPwshCommandAsync(string args, TimeSpan? timeout = null)
        {
            var exe = FindPwshExecutable();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Encourage child processes to emit ANSI and UTF-8 where possible
            try
            {
                psi.Environment["TERM"] = "xterm-256color";
                psi.Environment["LANG"] = "en_US.UTF-8";
                psi.Environment["LC_ALL"] = "en_US.UTF-8";
            }
            catch { }

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // builders removed; we'll accumulate raw bytes into MemoryStreams

            // We'll read the streams as raw char streams (not line-based) so we preserve ANSI escapes,
            // unicode symbols, and carriage-return based spinners without losing characters or introducing artifacts.
            Task stdoutPump = null!;
            Task stderrPump = null!;

            // allocate accumulators outside try so catch/finally can access them
            var stdoutBytes = new System.IO.MemoryStream();
            var stderrBytes = new System.IO.MemoryStream();

            try
            {
                if (!process.Start())
                {
                    return new ProcessResult { ExitCode = -1, Stdout = "", Stderr = "Failed to start process", Combined = "Failed to start process" };
                }

                // Start reading raw byte streams so we don't perform intermediate decoding that can corrupt
                // unicode glyphs or ANSI sequences. We'll copy bytes from the child's stdout/stderr to
                // the MCP's stderr stream for live visibility and also accumulate raw bytes to decode later.

                stdoutPump = Task.Run(async () =>
                {
                    try
                    {
                        var inStream = process.StandardOutput.BaseStream;
                        var outStream = Console.OpenStandardError();
                        var buffer = new byte[4096];
                        int read;
                        while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            // write raw bytes to MCP stderr (live)
                            try { await outStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false); await outStream.FlushAsync().ConfigureAwait(false); } catch { }
                            // accumulate for result
                            await stdoutBytes.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        }
                    }
                    catch { }
                });

                stderrPump = Task.Run(async () =>
                {
                    try
                    {
                        var inStream = process.StandardError.BaseStream;
                        var outStream = Console.OpenStandardError();
                        var buffer = new byte[4096];
                        int read;
                        while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            try { await outStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false); await outStream.FlushAsync().ConfigureAwait(false); } catch { }
                            await stderrBytes.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        }
                    }
                    catch { }
                });

                var processExitedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.Exited += (s, e) => processExitedTcs.TrySetResult(true);

                var tasks = new[] { processExitedTcs.Task, stdoutPump, stderrPump };

                if (timeout.HasValue)
                {
                    var delay = Task.Delay(timeout.Value);
                    var completed = await Task.WhenAny(Task.WhenAll(tasks), delay).ConfigureAwait(false);
                    if (completed == delay)
                    {
                        try { process.Kill(true); } catch { }
                        string so = string.Empty, se = string.Empty;
                        try { if (stdoutBytes.Length > 0) so = Encoding.UTF8.GetString(stdoutBytes.ToArray()); } catch { }
                        try { if (stderrBytes.Length > 0) se = Encoding.UTF8.GetString(stderrBytes.ToArray()); } catch { }
                        return new ProcessResult { ExitCode = -2, Stdout = so, Stderr = se, Combined = so + "\n" + se };
                    }
                }
                else
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                // ensure process has exited
                if (!process.HasExited)
                {
                    process.Kill(true);
                }

                var exit = process.ExitCode;

                string stdout = string.Empty;
                string stderr = string.Empty;
                try
                {
                    if (stdoutBytes.Length > 0)
                    {
                        stdout = Encoding.UTF8.GetString(stdoutBytes.ToArray());
                    }
                    if (stderrBytes.Length > 0)
                    {
                        stderr = Encoding.UTF8.GetString(stderrBytes.ToArray());
                    }
                }
                catch
                {
                    // Best-effort: fall back to empty strings if decoding fails
                    stdout = string.Empty;
                    stderr = string.Empty;
                }

                return new ProcessResult { ExitCode = exit, Stdout = stdout, Stderr = stderr, Combined = stdout + "\n" + stderr };
            }
            catch (Exception ex)
            {
                string so = string.Empty, se = string.Empty;
                try { if (stdoutBytes != null && stdoutBytes.Length > 0) so = Encoding.UTF8.GetString(stdoutBytes.ToArray()); } catch { }
                try { if (stderrBytes != null && stderrBytes.Length > 0) se = Encoding.UTF8.GetString(stderrBytes.ToArray()); } catch { }
                return new ProcessResult { ExitCode = -99, Stdout = so, Stderr = se + "\n" + ex.ToString(), Combined = so + "\n" + se + "\n" + ex.ToString() };
            }
        }
    }
}
