using System.Collections.Generic;

namespace BasicTcp
{
  public class DataPacket
  {
    /// <summary>
    /// Dictionary with headers Key - Value.
    /// </summary>
    public Dictionary<string, string> Header;

    /// <summary>
    /// Byte array with received data from server.
    /// </summary>
    public byte[] Data;

    public DataPacket(Dictionary<string, string> header, byte[] data)
    {
      Header = header;
      Data = data;
    }
  }
}
