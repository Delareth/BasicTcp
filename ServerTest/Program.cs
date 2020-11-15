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