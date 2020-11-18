using System;
using System.Collections.Generic;
using System.Text;
using BasicTcp;
using BasicTcp.Events;

namespace ClientTest
{
  public class Program
  {
    public static BasicTcpClient Client;

    static void Main()
    {
      Client = new BasicTcpClient("127.0.0.1", 11113, 5000);

      Client.Events.Connected += OnClientConnected;
      Client.Events.Disconnected += OnClientDisconnected;
      Client.Events.DataReceived += OnDataReceived;
      Client.Events.ClientLog += OnClientLog;

      Client.Start();

      ReadNewLine();
    }

    public static void ReadNewLine()
    {
      Console.WriteLine("Enter message to send to server");
      string cmd = Console.ReadLine();

      Client.Send(cmd);

      ReadNewLine();
    }

    private static void OnClientLog(object sender, ClientLoggerEventArgs e)
    {
      if (e.LogType == LogType.EXCEPTION)
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception message: {e.Message}");
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception Message: {e.Exception.Message}");
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception StackTrace: {e.Exception.StackTrace}");
        Console.WriteLine($"[BasicTcp][{e.LogType}]: Exception: {e}");
      }
      else
      {
        Console.WriteLine($"[BasicTcp][{e.LogType}]: {e.Message}");
      }
    }

    private static void OnDataReceived(object sender, DataReceivedEventArgs e)
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