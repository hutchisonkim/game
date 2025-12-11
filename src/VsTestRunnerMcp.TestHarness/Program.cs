using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VsTestRunnerMcp.TestHarness
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Ensure console uses UTF-8 so we preserve special characters and ANSI sequences
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
            Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true });
            var dllPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "VsTestRunnerMcp", "bin", "Release", "net8.0", "VsTestRunnerMcp.dll"));

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"MCP DLL not found at {dllPath}. Build the project first.");
                return 2;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.Start();

            // read background stdout
            // Read MCP stdout line-by-line (JSON responses are emitted as single-line JSON)
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
                {
                    Console.WriteLine("MCP-OUT: " + line);
                }
            });

            // Read MCP stderr as a raw byte/char stream so ANSI escapes and spinner control characters
            // are preserved. We write them directly to the console without prefixes.
            _ = Task.Run(async () =>
            {
                var errStream = proc.StandardError.BaseStream;
                var buf = new byte[4096];
                int n;
                while ((n = await errStream.ReadAsync(buf, 0, buf.Length)) > 0)
                {
                    var s = Encoding.UTF8.GetString(buf, 0, n);
                    Console.Write(s);
                }
            });

            // send initialize
            var init = new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } };
            var initJson = JsonSerializer.Serialize(init);
            await proc.StandardInput.WriteLineAsync(initJson);
            await proc.StandardInput.FlushAsync();

            // wait a bit for response
            await Task.Delay(500);

            // call run-tests-with-filter (requesting a single Essential test by TestId)
            var filterValue = "TestId=L0_Foundation_BoardStateProvider";
            var call = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "run-tests-with-filter",
                    input = new { filter = filterValue }
                }
            };

            var callJson = JsonSerializer.Serialize(call);
            Console.WriteLine("Sending tools/call (run-tests-with-filter): " + callJson);
            await proc.StandardInput.WriteLineAsync(callJson);
            await proc.StandardInput.FlushAsync();

            // wait for the script to run and produce output (may take some time)
            await Task.Delay(60000);

            try
            {
                proc.Kill(true);
            }
            catch { }

            return 0;
        }
    }
}
