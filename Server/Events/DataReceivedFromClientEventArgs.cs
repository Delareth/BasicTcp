using System;
using System.Collections.Generic;

namespace BasicTcp.Events
{
  public class DataReceivedFromClientEventArgs : EventArgs
  {
    internal DataReceivedFromClientEventArgs(string ipPort, byte[] data, Dictionary<string, string> header)
    {
      IpPort = ipPort;
      Data = data;
      Header = header;
    }

    /// <summary>
    /// IP address and port number of the connected client socket.
    /// </summary>
    public string IpPort { get; }

    /// <summary>
    /// Data received from the client.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Header with values received from the client.
    /// </summary>
    public Dictionary<string, string> Header { get; }
  }
}
