using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VsTestRunnerMcp
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var runner = new ProcessRunner();
            // Ensure console streams use UTF-8 so we don't lose non-ASCII characters
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
            Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true });
            string? line;

            while ((line = Console.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    JsonElement idElement = default;
                    object? idValue = null;
                    if (root.TryGetProperty("id", out idElement))
                    {
                        idValue = idElement.ValueKind switch
                        {
                            JsonValueKind.Number => idElement.GetInt32(),
                            JsonValueKind.String => idElement.GetString(),
                            _ => idElement.ToString()
                        };
                    }

                    var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                    object resultObj;

                    if (method == "initialize")
                    {
                        resultObj = new
                        {
                            capabilities = new
                            {
                                tools = new
                                {
                                    start_spark_runner = new
                                    {
                                        description = "Starts the local Spark runner via script.",
                                        inputSchema = new { type = "object", properties = new { } }
                                    },
                                    run_tests_with_filter = new
                                    {
                                        description = "Runs dependency-aware tests via scripts/run-tests-with-dependencies.ps1.",
                                        inputSchema = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                filter = new { type = "string", description = "Test filter string", @default = "Essential=true" }
                                            }
                                        }
                                    },
                                    stop_and_cleanup_runner = new
                                    {
                                        description = "Stops the runner and performs cleanup (force kill fallback).",
                                        inputSchema = new { type = "object", properties = new { } }
                                    }
                                }
                            }
                        };
                    }
                    else if (method == "tools/call")
                    {
                        var p = root.GetProperty("params");
                        var name = p.GetProperty("name").GetString();
                        var input = p.TryGetProperty("input", out var inputEl) ? inputEl : default;

                        ProcessResult pres;
                        switch (name)
                        {
                            case "start-spark-runner":
                                pres = await runner.RunPwshCommandAsync("-NoProfile -ExecutionPolicy Bypass -File \"scripts/spark-runner-start.ps1\"");
                                break;
                            case "run-tests-with-filter":
                            {
                                string filter = "Essential=true";
                                if (inputEl.ValueKind != JsonValueKind.Undefined && inputEl.TryGetProperty("filter", out var f) && f.ValueKind == JsonValueKind.String)
                                {
                                    filter = f.GetString() ?? filter;
                                }
                                var escaped = filter.Replace("\"", "\\\"");
                                pres = await runner.RunPwshCommandAsync($"-NoProfile -ExecutionPolicy Bypass -File \"scripts/run-tests-with-dependencies.ps1\" -Filter \"{escaped}\"");
                                break;
                            }
                            case "stop-and-cleanup-runner":
                            {
                                var r1 = await runner.RunPwshCommandAsync("-NoProfile -ExecutionPolicy Bypass -File \"scripts/spark-testctl.ps1\" -Stop");
                                var r2 = await runner.RunPwshCommandAsync("-NoProfile -ExecutionPolicy Bypass -File \"scripts/force-kill-runners.ps1\"");
                                pres = new ProcessResult
                                {
                                    ExitCode = r1.ExitCode != 0 ? r1.ExitCode : r2.ExitCode,
                                    Stdout = string.Concat(r1.Stdout, "\n", r2.Stdout),
                                    Stderr = string.Concat(r1.Stderr, "\n", r2.Stderr),
                                    Combined = string.Concat(r1.Combined, "\n", r2.Combined)
                                };
                                break;
                            }
                            default:
                                pres = new ProcessResult { ExitCode = -2, Stdout = "", Stderr = "", Combined = $"Unknown tool: {name}" };
                                break;
                        }

                        // Provide both raw output (with ANSI sequences) and a plain sanitized version
                        string raw = pres.Combined ?? string.Empty;
                        string plain = StripAnsiAndControl(raw);

                        resultObj = new { exitCode = pres.ExitCode, output = raw, plainOutput = plain, stdout = pres.Stdout, stderr = pres.Stderr };
                    }
                    else
                    {
                        resultObj = new { error = "unsupported method" };
                    }

                    var response = new
                    {
                        jsonrpc = "2.0",
                        id = idValue,
                        result = resultObj
                    };

                    var opts = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                    var outJson = JsonSerializer.Serialize(response, opts);
                    Console.Out.WriteLine(outJson);
                }
                catch (Exception ex)
                {
                    var resp = new
                    {
                        jsonrpc = "2.0",
                        id = (object?)null,
                        result = new { exitCode = -1, output = ex.Message, plainOutput = ex.Message, stdout = "", stderr = ex.ToString() }
                    };
                    Console.Out.WriteLine(JsonSerializer.Serialize(resp));
                }

                static string StripAnsiAndControl(string input)
                {
                    if (string.IsNullOrEmpty(input)) return string.Empty;
                    try
                    {
                        // Remove ANSI escape sequences
                        var withoutAnsi = Regex.Replace(input, "\u001B\[[0-9;?]*[ -/]*[@-~]", string.Empty);

                        // Remove C0 control chars except CR (\r) and LF (\n)
                        var cleaned = Regex.Replace(withoutAnsi, "[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]", string.Empty);

                        // Convert carriage returns used by spinners into nothing so recorded text doesn't contain overwrite artifacts
                        cleaned = cleaned.Replace("\r", string.Empty);

                        // Normalize Windows newlines
                        cleaned = cleaned.Replace("\r\n", "\n");

                        return cleaned;
                    }
                    catch
                    {
                        return input;
                    }
                }
            }

            return 0;
        }
    }
}
