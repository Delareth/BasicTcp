using System;

namespace BasicTcp.Events
{
  public class ClientEvents
  {
    /// <summary>
    /// Invoking when client connected to server.
    /// </summary>
    public event EventHandler Connected;

    /// <summary>
    /// Invoking when client disconnected from server.
    /// </summary>
    public event EventHandler Disconnected;

    /// <summary>
    /// Invoking when client received data from server.
    /// </summary>
    public event EventHandler<DataReceivedEventArgs> DataReceived;

    /// <summary>
    /// Invoking when client send log message.
    /// </summary>
    public event EventHandler<ClientLoggerEventArgs> ClientLog;

    public ClientEvents()
    {

    }

    internal void HandleConnected(object sender)
    {
      Connected?.Invoke(sender, EventArgs.Empty);
    }

    internal void HandleDisconnected(object sender)
    {
      Disconnected?.Invoke(sender, EventArgs.Empty);
    }

    internal void HandleDataReceived(object sender, DataReceivedEventArgs args)
    {
      DataReceived?.Invoke(sender, args);
    }

    internal void HandleClientLog(object sender, ClientLoggerEventArgs args)
    {
      ClientLog?.Invoke(sender, args);
    }
  }
}
