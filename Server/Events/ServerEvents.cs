using System;

namespace BasicTcp.Events
{
  public class ServerEvents
  {
    /// <summary>
    /// Invoking when a client connects.
    /// </summary>
    public event EventHandler<ClientConnectedEventArgs> ClientConnected;

    /// <summary>
    /// Invoking when a client disconnects.
    /// </summary>
    public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

    /// <summary>
    /// Invoking when byte data has become available from the client.
    /// </summary>
    public event EventHandler<DataReceivedFromClientEventArgs> DataReceived;

    /// <summary>
    /// Invoking when server send log message.
    /// </summary>
    public event EventHandler<ServerLoggerEventArgs> ServerLog;

    public ServerEvents()
    {

    }

    internal void HandleClientConnected(object sender, ClientConnectedEventArgs args)
    {
      ClientConnected?.Invoke(sender, args);
    }

    internal void HandleClientDisconnected(object sender, ClientDisconnectedEventArgs args)
    {
      ClientDisconnected?.Invoke(sender, args);
    }

    internal void HandleDataReceived(object sender, DataReceivedFromClientEventArgs args)
    {
      DataReceived?.Invoke(sender, args);
    }

    internal void HandleServerLog(object sender, ServerLoggerEventArgs args)
    {
      ServerLog?.Invoke(sender, args);
    }
  }
}
