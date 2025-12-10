using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Game.Chess.Tests.Integration.Runner
{
    public class RunnerServer
    {
        private readonly string _publishDir;
        private volatile bool _shouldStop = false;
        private readonly System.Threading.ManualResetEvent _testExecutionComplete = new System.Threading.ManualResetEvent(true);

        public RunnerServer(string publishDir)
        {
            _publishDir = publishDir;
        }

        public void StopServer()
        {
            Console.WriteLine("[RunnerServer] Initiating graceful shutdown...");
            _shouldStop = true;

            // Wait up to 5 seconds for any active test execution to complete
            if (!_testExecutionComplete.WaitOne(5000))
            {
                Console.WriteLine("[RunnerServer] WARNING: Test execution did not complete within 5 seconds");
            }

            Console.WriteLine("[RunnerServer] Graceful shutdown complete");
        }

        public void Start()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 7009);
            listener.Start();
            Console.WriteLine("[RunnerServer] TCP listening on 127.0.0.1:7009");

            while (!_shouldStop)
            {
                using var client = listener.AcceptTcpClient();
                
                // Set explicit socket timeouts to prevent hanging
                client.ReceiveTimeout = 30000;  // 30 seconds
                client.SendTimeout = 30000;     // 30 seconds

                try
                {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                    using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

                    string? body = null;
                    try
                    {
                        body = reader.ReadLine();
                    }
                    catch (IOException ioEx) when (ioEx.InnerException is TimeoutException)
                    {
                        Console.WriteLine("[RunnerServer] Receive timeout (30s exceeded)");
                        writer.WriteLine("ERROR: Timeout waiting for command");
                        continue;
                    }
                    catch (IOException ioEx)
                    {
                        Console.WriteLine($"[RunnerServer] IO error reading command: {ioEx.Message}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(body)) { continue; }

                    try
                    {
                        if (body.StartsWith("REBUILD", StringComparison.OrdinalIgnoreCase))
                        {
                            _testExecutionComplete.Reset();
                            try
                            {
                                string result = TestExecutor.RebuildTestAssemblies(_publishDir);
                                try
                                {
                                    stream.Flush();
                                    writer.Write(result);
                                    stream.Flush();
                                }
                                catch (IOException ioEx) when (ioEx.InnerException is TimeoutException)
                                {
                                    Console.WriteLine("[RunnerServer] Send timeout (30s exceeded) while writing rebuild results");
                                }
                                catch (IOException ioEx)
                                {
                                    Console.WriteLine($"[RunnerServer] IO error writing rebuild results: {ioEx.Message}");
                                }
                            }
                            finally
                            {
                                _testExecutionComplete.Set();
                            }
                        }
                        else if (body.StartsWith("RUN", StringComparison.OrdinalIgnoreCase))
                        {
                            _testExecutionComplete.Reset();
                            try
                            {
                                string commandLine = body.Length > 3 ? body.Substring(3).Trim() : string.Empty;
                                bool skipBuild = commandLine.Contains("skipBuild", StringComparison.OrdinalIgnoreCase);
                                // Extract filter by removing skipBuild token
                                string filter = commandLine.Replace("skipBuild", "", StringComparison.OrdinalIgnoreCase).Trim();
                                string result = TestExecutor.RunTests(_publishDir, filter, skipBuild);
                                try
                                {
                                    stream.Flush();
                                    writer.Write(result);
                                    stream.Flush();
                                }
                                catch (IOException ioEx) when (ioEx.InnerException is TimeoutException)
                                {
                                    Console.WriteLine("[RunnerServer] Send timeout (30s exceeded) while writing results");
                                }
                                catch (IOException ioEx)
                                {
                                    Console.WriteLine($"[RunnerServer] IO error writing results: {ioEx.Message}");
                                }
                            }
                            finally
                            {
                                _testExecutionComplete.Set();
                            }
                        }
                        else if (string.Equals(body, "PING", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                writer.Write("PONG");
                                stream.Flush();
                            }
                            catch (IOException ioEx)
                            {
                                Console.WriteLine($"[RunnerServer] IO error writing PONG: {ioEx.Message}");
                            }
                        }
                        else if (string.Equals(body, "STOP", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                writer.Write("Stopping");
                                stream.Flush();
                            }
                            catch (IOException ioEx)
                            {
                                Console.WriteLine($"[RunnerServer] IO error writing STOP response: {ioEx.Message}");
                            }
                            listener.Stop();
                            return;
                        }
                        else
                        {
                            try
                            {
                                writer.Write("Unknown command");
                                stream.Flush();
                            }
                            catch (IOException ioEx)
                            {
                                Console.WriteLine($"[RunnerServer] IO error writing unknown command response: {ioEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RunnerServer] Exception processing command: {ex.Message}");
                    }
                    finally
                    {
                        try { stream.Close(); }
                        catch { }
                        try { reader.Dispose(); }
                        catch { }
                        try { writer.Dispose(); }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RunnerServer] Exception handling client: {ex.Message}");
                }
            }
        }
    }
}
