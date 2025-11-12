using System;
using System.Net;
using System.Net.Sockets;

// Simple helper: bind to an ephemeral port on loopback, print the port number
// and exit. This helps reserve a port number before launching the test process.
try
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    // Close quickly; the test process will use the printed port value.
    listener.Stop();
    Console.WriteLine(port);
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Failed to reserve port: " + ex);
    Environment.Exit(2);
}
