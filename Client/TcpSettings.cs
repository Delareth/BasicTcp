using System;
using System.Net.Sockets;

namespace BasicTcp
{
  public class TcpSettings
  {
    internal TcpClient TcpClient = null;

    private int _SendTimeout = 0;
    private int _ReceiveTimeout = 0;

    /// <summary>
    /// Gets or sets the amount of time a TcpClient will wait for a send operation to complete successfully.
    /// </summary>
    public int SendTimeout
    {
      get
      {
        return _SendTimeout;
      }
      set
      {
        if (value < 1) throw new ArgumentOutOfRangeException("SendTimeout must be greater than one");
        if (TcpClient == null) throw new NullReferenceException("TcpClient not initialized, can't set SendTimeout");

        _SendTimeout = value;
        TcpClient.SendTimeout = value;
      }
    }

    /// <summary>
    /// Gets or sets the amount of time a TcpClient will wait to receive data once a read operation is initiated.
    /// </summary>
    public int ReceiveTimeout
    {
      get
      {
        return _ReceiveTimeout;
      }
      set
      {
        if (value < 1) throw new ArgumentOutOfRangeException("ReceiveTimeout must be greater than one");
        if (TcpClient == null) throw new NullReferenceException("TcpClient not initialized, can't set ReceiveTimeout");

        _ReceiveTimeout = value;
        TcpClient.ReceiveTimeout = value;
      }
    }

    public TcpSettings(int sendTimeout, int receiveTimeout)
    {
      if (sendTimeout < 1) throw new ArgumentOutOfRangeException("SendTimeout must be greater than one");
      if (receiveTimeout < 1) throw new ArgumentOutOfRangeException("ReceiveTimeout must be greater than one");

      SendTimeout = sendTimeout;
      ReceiveTimeout = receiveTimeout;
    }
  }
}
