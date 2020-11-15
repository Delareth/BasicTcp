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