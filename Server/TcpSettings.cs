using System;
using System.Net.Sockets;

namespace BasicTcp
{
  public class TcpSettings
  {
    internal Socket Socket = null;

    private int _SendTimeout = 0;
    private int _ReceiveTimeout = 0;

    /// <summary>
    /// Gets or sets a value that specifies the amount of time after which a synchronous Socket.Send call will time out.
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
        if (Socket == null) throw new NullReferenceException("Socket not initialized, can't set SendTimeout");

        _SendTimeout = value;
        Socket.SendTimeout = value;
      }
    }

    /// <summary>
    /// Gets or sets a value that specifies the amount of time after which a synchronous Socket.Receive call will time out.
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
        if (Socket == null) throw new NullReferenceException("Socket not initialized, can't set ReceiveTimeout");

        _ReceiveTimeout = value;
        Socket.ReceiveTimeout = value;
      }
    }

    public TcpSettings(int sendTimeout, int receiveTimeout)
    {
      if (sendTimeout < 1) throw new ArgumentOutOfRangeException("SendTimeout must be greater than one");
      if (receiveTimeout < 1) throw new ArgumentOutOfRangeException("ReceiveTimeout must be greater than one");

      _SendTimeout = sendTimeout;
      _ReceiveTimeout = receiveTimeout;
    }
  }
}
