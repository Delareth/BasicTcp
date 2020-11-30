using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BasicTcp.Events;

namespace BasicTcp
{
  public class BasicTcpServer : IDisposable
  {
    private readonly TcpListener _Listener = null;
    private readonly ConcurrentDictionary<string, Client> _Clients = new ConcurrentDictionary<string, Client>();
    private ServerEvents _Events = new ServerEvents();
    private CancellationTokenSource _TokenSource = new CancellationTokenSource();
    private CancellationToken _Token;

    public ServerEvents Events
    {
      get
      {
        return _Events;
      }
      set
      {
        if (value == null) _Events = new ServerEvents();
        else _Events = value;
      }
    }

    public TcpSettings TcpSettings { get; }

    public bool IsListening { get; private set; } = false;

    /// <summary>
    /// Initializing server.
    /// </summary>
    /// <param name="ip">If ip null or empty, it will using loopback address. If ip setting as *, it will use any address in network.</param>
    public BasicTcpServer(string ip, int port, TcpSettings tcpSettings = null)
    {
      IPAddress _IPAddress;

      if (string.IsNullOrEmpty(ip))
      {
        _IPAddress = IPAddress.Loopback;
      }
      else if (ip == "*")
      {
        _IPAddress = IPAddress.Any;
      }
      else
      {
        if (!IPAddress.TryParse(ip, out _IPAddress))
        {
          _IPAddress = Dns.GetHostEntry(ip).AddressList[0];
        }
      }

      if (tcpSettings == null)
      {
        TcpSettings = new TcpSettings(600000, 600000);
      }

      _Listener = new TcpListener(_IPAddress, port);
      _Listener.Server.SendTimeout = TcpSettings.SendTimeout;
      _Listener.Server.ReceiveTimeout = TcpSettings.ReceiveTimeout;

      TcpSettings.Socket = _Listener.Server;

      _Token = _TokenSource.Token;
    }

    /// <summary>
    /// Start receiving connections to server.
    /// </summary>
    public void Start()
    {
      if (IsListening) throw new InvalidOperationException("TcpServer already running");

      _Listener.Start();
      _TokenSource = new CancellationTokenSource();
      _Token = _TokenSource.Token;

      Task.Run(() => MonitorForNewClients(), _Token);

      IsListening = true;
    }

    /// <summary>
    /// Disconnect all clients and stop server.
    /// </summary>
    public void Stop()
    {
      if (!IsListening) throw new InvalidOperationException("TcpServer is not running");

      Dispose();

      if (IsListening)
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.ERROR, "Can't stop server"));
      }
      else
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.TCP, "Server successful stopped"));
      }
    }

    public List<string> GetClients()
    {
      List<string> clients = new List<string>(_Clients.Keys);
      return clients;
    }

    public bool IsClientConnected(string ipPort)
    {
      if (!_Clients.TryGetValue(ipPort, out Client client)) return false;

      return client.IsConnected;
    }

    public bool DisconnectClient(string ipPort)
    {
      if (!_Clients.TryGetValue(ipPort, out Client client)) return false;

      if (_Clients.TryRemove(client.Ip, out _))
      {
        Events.HandleClientDisconnected(this, 
          new ClientDisconnectedEventArgs(client.Ip, 
          client.IsConnected ? DisconnectReason.Kicked : DisconnectReason.Timeout));

        client.Dispose();
        return true;
      }
      else
      {
        return false;
      }
    }

    public bool SendToClient(string ipPort, string data, Dictionary<string, string> additionalHeaders = null)
    {
      if (!_Clients.ContainsKey(ipPort))
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.ERROR, $"Client [{ipPort}] not connected to server to send data"));
        return false;
      }
      
      if (_Clients.TryGetValue(ipPort, out Client client))
      {
        try
        {
          client.Send(data, additionalHeaders);

          return true;
        }
        catch (Exception ex)
        {
          Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.EXCEPTION, $"Can't send data to {ipPort}", ex));
          return false;
        }
      }
      else
      {
        return false;
      }
    }

    public bool SendToClient(string ipPort, byte[] data, Dictionary<string, string> additionalHeaders = null)
    {
      if (!_Clients.ContainsKey(ipPort))
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.ERROR, $"Client [{ipPort}] not connected to server to send data"));
        return false;
      }

      if (_Clients.TryGetValue(ipPort, out Client client))
      {
        try
        {
          client.Send(data, additionalHeaders);

          return true;
        }
        catch (Exception ex)
        {
          Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.EXCEPTION, $"Can't send data to {ipPort}", ex));
          return false;
        }
      }
      else
      {
        return false;
      }
    }

    public bool SendToClient(string ipPort, Stream stream, Dictionary<string, string> additionalHeaders = null)
    {
      if (!_Clients.ContainsKey(ipPort))
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.ERROR, $"Client [{ipPort}] not connected to server to send data"));
        return false;
      }

      if (_Clients.TryGetValue(ipPort, out Client client))
      {
        try
        {
          client.Send(stream, additionalHeaders);

          return true;
        }
        catch (Exception ex)
        {
          Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.EXCEPTION, $"Can't send data to {ipPort}", ex));
          return false;
        }
      }
      else
      {
        return false;
      }
    }

    public void SendToAllClients(string data, Dictionary<string, string> additionalHeaders = null)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, data, additionalHeaders);
      }
    }

    public void SendToAllClients(byte[] data, Dictionary<string, string> additionalHeaders = null)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, data, additionalHeaders);
      }
    }

    public void SendToAllClients(Stream stream, Dictionary<string, string> additionalHeaders = null)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, stream, additionalHeaders);
      }
    }

    public void Dispose()
    {
      try
      {
        foreach (KeyValuePair<string, Client> entry in _Clients)
        {
          entry.Value.Send("You was disconnected from server");
          Events.HandleClientDisconnected(this, new ClientDisconnectedEventArgs(entry.Value.Ip, DisconnectReason.Kicked));

          entry.Value.Dispose();
        }

        _TokenSource.Cancel();
        _TokenSource.Dispose();

        if (_Listener != null && _Listener.Server != null)
        {
          _Listener.Server.Close();
          _Listener.Server.Dispose();
        }

        if (_Listener != null)
        {
          _Listener.Stop();
        }
      }
      catch (Exception ex)
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.EXCEPTION, "Can't dispose server", ex));
      }

      IsListening = false;
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Force check is client connected, sometimes it can help because client timeout could be delayed.
    /// </summary>
    public bool ForceIsClientConnected(string ipPort)
    {
      if (!_Clients.ContainsKey(ipPort)) return false;

      TcpClient client = _Clients[ipPort].TcpClient;

      return ForceIsClientConnected(client);
    }

    /// <summary>
    /// Force check is client connected, sometimes it can help because client timeout could be delayed.
    /// </summary>
    public bool ForceIsClientConnected(TcpClient client)
    {
      if (client == null) return false;
      if (client.Client == null) return false;
      if (!client.Connected) return false;

      if (client.Client.Poll(0, SelectMode.SelectRead))
      {
        byte[] buff = new byte[1];

        return !(client.Client.Receive(buff, SocketFlags.Peek) == 0);
      }

      return true;
    }

    private void MonitorForNewClients()
    {
      Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.TCP, "Server initialized, waiting for connections..."));

      while (!_Token.IsCancellationRequested)
      {
        string ip = "unknown";

        try
        {
          TcpClient client = _Listener.AcceptTcpClient();
          ip = client.Client.RemoteEndPoint.ToString();

          RegisterNewClient(client);
        }
        catch (Exception ex)
        {
          Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.EXCEPTION, $"Can't register {ip} on server", ex));
        }
      }
    }

    private void RegisterNewClient(TcpClient client)
    {
      string ip = client.Client.RemoteEndPoint.ToString();

      if (_Clients.ContainsKey(ip)) throw new IOException("Client already connected to server");

      Client newClient = new Client(this, client);

      if (_Clients.TryAdd(ip, newClient))
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.TCP, $"Client {ip} - successful registered"));
        Events.HandleClientConnected(this, new ClientConnectedEventArgs(ip));
      }
      else
      {
        Events.HandleServerLog(this, new ServerLoggerEventArgs(LogType.TCP, $"Can't register client with ip: {ip}"));
      }
    }
  }
}
