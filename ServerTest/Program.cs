using System;
using System.Collections.Generic;
using System.Text;
using BasicTcp;
using BasicTcp.Events;

namespace ServerTest
{
  public class Program
  {
    public static BasicTcpServer Server;

    static void Main()
    {
      Server = new BasicTcpServer("*", 11113);

      Server.Events.ClientConnected += OnClientConnected;
      Server.Events.ClientDisconnected += OnClientDisconnected;
      Server.Events.DataReceived += OnDataReceived;
      Server.Events.ServerLog += OnServerLog;

      Server.Start();

      ReadCmd();
    }

    public static void ReadCmd()
    {
      Console.WriteLine("Enter message to send to all clients");

      string cmd = Console.ReadLine();

      Server.SendToAllClients(cmd);

      ReadCmd();
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

    private static void OnDataReceived(object sender, DataReceivedFromClientEventArgs e)
    {
      Console.WriteLine("Received new data");
      Console.WriteLine(Encoding.UTF8.GetString(e.Data));
      Console.WriteLine("Headers:");
      foreach (KeyValuePair<string, string> entry in e.Header)
      {
        Console.WriteLine($"{entry.Key}:{entry.Value}");
      }
      Console.WriteLine($"------------");
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