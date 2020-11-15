# BasicTcp 1.0.0

## Basic wrapper for TCP client and server in C#
[![NuGet Version](https://img.shields.io/nuget/v/BasicTcpClient?label=client_nuget)](https://www.nuget.org/packages/BasicTcpClient/) [![NuGet](https://img.shields.io/nuget/dt/BasicTcpClient)](https://www.nuget.org/packages/BasicTcpClient/)
[![NuGet Version](https://img.shields.io/nuget/v/BasicTcpServer?label=server_nuget)](https://www.nuget.org/packages/BasicTcpServer/) [![NuGet](https://img.shields.io/nuget/dt/BasicTcpServer)](https://www.nuget.org/packages/BasicTcpServer/)

BasicTcp provides methods for creating your own TCP-based sockets application, enabling easy integration of connection management, sending, and receiving data.  

## Help or Feedback

Need help or have feedback?  Please file an issue here!

## Examples

### Server Example
```csharp
using System;
using System.Collections.Generic;
using BasicTcp;
using BasicTcp.Events;

namespace ServerTest
{
  public class Program
  {
    public static BasicTcpServer Server;

    static void Main()
    {
      Server = new BasicTcpServer("*", 10013);

      Server.Events.ClientConnected += OnClientConnected;
      Server.Events.ClientDisconnected += OnClientDisconnected;
      Server.Events.DataReceived += OnDataRecieved;
      Server.Events.ServerLog += OnServerLog;

      Server.Start();

      Console.ReadKey();
    }

    private static void OnServerLog(object sender, ServerLoggerEventArgs e)
    {
      if (e.LogType == LogType.EXCEPTION)
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception message: {e.Message}");
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception stacktrace: {e.Exception.Message}");
      }
      else
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: {e.Message}");
      }
    }

    private static void OnDataRecieved(object sender, DataReceivedFromClientEventArgs e)
    {
      Console.WriteLine($"Recieved new data from ip: " + e.IpPort);
      Console.WriteLine("Headers:");
      foreach (KeyValuePair<string, string> entry in e.Header)
      {
        Console.WriteLine($"{entry.Key}:{entry.Value}");
      }
      Console.WriteLine("------------");
    }

    private static void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
    {
      Console.WriteLine($"Client {e.IpPort} disconnected with reason: {e.Reason}");
    }

    private static void OnClientConnected(object sender, ClientConnectedEventArgs e)
    {
      Console.WriteLine($"Client {e.IpPort} connected to server");

      Server.SendToClient(e.IpPort, "Hello world!");
    }
  }
}
```

### Client Example
```csharp
using System;
using System.Collections.Generic;
using BasicTcp;
using BasicTcp.Events;

namespace ClientTest
{
  public class Program
  {
    public static BasicTcpClient Client;

    static void Main()
    {
      Client = new BasicTcpClient("127.0.0.1", 10013, 5000);

      Client.Events.Connected += OnClientConnected;
      Client.Events.Disconnected += OnClientDisconnected;
      Client.Events.DataReceived += OnDataRecieved;
      Client.Events.ClientLog += OnClientLog;

      Client.Start();

      ReadNewLine();
    }

    public static void ReadNewLine()
    {
      Console.WriteLine("Enter message to send to server");
      string command = Console.ReadLine();

      Client.Send(command);
      ReadNewLine();
    }

    private static void OnClientLog(object sender, ClientLoggerEventArgs e)
    {
      if (e.LogType == LogType.EXCEPTION)
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception message: {e.Message}");
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception stacktrace: {e.Exception.Message}");
      }
      else
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: {e.Message}");
      }
    }

    private static void OnDataRecieved(object sender, DataReceivedEventArgs e)
    {
      Console.WriteLine($"Recieved new data");
      Console.WriteLine("Headers:");
      foreach (KeyValuePair<string, string> entry in e.Header)
      {
        Console.WriteLine($"{entry.Key}:{entry.Value}");
      }
      Console.WriteLine("------------");
    }

    private static void OnClientDisconnected(object sender, EventArgs e)
    {
      Console.WriteLine($"[TCP] Client disconnected");
    }

    private static void OnClientConnected(object sender, EventArgs e)
    {
      Console.WriteLine("[TCP] Client connected to server");

      Client.Send("Test", new Dictionary<string, string>
      {
        { "Command", "RegisterOnServer" }
      });
    }
  }
}
```

### Local vs External Connections

**IMPORTANT**
* If you specify ```127.0.0.1``` as the listener IP address, it will only be able to accept connections from within the local host.  
* To accept connections from other machines:
  * Use a specific interface IP address, or
  * Use ```null```, ```*```, ```+```, or ```0.0.0.0``` for the listener IP address (requires admin privileges to listen on any IP address)
* Make sure you create a permit rule on your firewall to allow inbound connections on that port
* If you use a port number under 1024, admin privileges will be required