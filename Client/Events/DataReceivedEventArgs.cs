using System;
using System.Collections.Generic;

namespace BasicTcp.Events
{
  public class DataReceivedEventArgs : EventArgs
  {
    internal DataReceivedEventArgs(byte[] data, Dictionary<string, string> header)
    {
      Data = data;
      Header = header;
    }

    /// <summary>
    /// Data received from server.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Header with values received from the client.
    /// </summary>
    public Dictionary<string, string> Header { get; }
  }
}
