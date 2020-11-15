using System;

namespace BasicTcp.Events
{
  public class ClientConnectedEventArgs : EventArgs
  {
    internal ClientConnectedEventArgs(string ipPort)
    {
      IpPort = ipPort;
    }

    /// <summary>
    /// The IP address and port number of the connected client socket.
    /// </summary>
    public string IpPort { get; }
  }
}
