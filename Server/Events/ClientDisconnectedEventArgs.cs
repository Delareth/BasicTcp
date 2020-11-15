using System;

namespace BasicTcp.Events
{
  public enum DisconnectReason
  {
    /// <summary>
    /// Normal disconnection.
    /// </summary>
    Normal = 0,
    /// <summary>
    /// Client connection was intentionally terminated programmatically or by the server.
    /// </summary>
    Kicked = 1,
    /// <summary>
    /// Client connection timed out; server did not receive data within the timeout window.
    /// </summary>
    Timeout = 2
  }

  public class ClientDisconnectedEventArgs : EventArgs
  {
    internal ClientDisconnectedEventArgs(string ipPort, DisconnectReason reason)
    {
      IpPort = ipPort;
      Reason = reason;
    }

    /// <summary>
    /// The IP address and port number of the disconnected client socket.
    /// </summary>
    public string IpPort { get; }

    /// <summary>
    /// The reason for the disconnection.
    /// </summary>
    public DisconnectReason Reason { get; }
  }
}
