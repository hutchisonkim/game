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

        public RunnerServer(string publishDir)
        {
            _publishDir = publishDir;
        }

        public void Start()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 7009);
            listener.Start();
            Console.WriteLine("[RunnerServer] TCP listening on 127.0.0.1:7009");

            while (true)
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

                string? body = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(body)) { continue; }

                if (body.StartsWith("RUN", StringComparison.OrdinalIgnoreCase))
                {
                    string filter = body.Length > 3 ? body.Substring(3).Trim() : string.Empty;
                    string result = TestExecutor.RunTests(_publishDir, filter);
                    writer.Write(result);
                }
                else if (string.Equals(body, "PING", StringComparison.OrdinalIgnoreCase))
                {
                    writer.Write("PONG");
                }
                else if (string.Equals(body, "STOP", StringComparison.OrdinalIgnoreCase))
                {
                    writer.Write("Stopping");
                    listener.Stop();
                    return;
                }
                else
                {
                    writer.Write("Unknown command");
                }
            }
        }
    }
}
