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

    public bool IsListening { get; private set; } = false;

    /// <summary>
    /// Initializing server.
    /// </summary>
    /// <param name="ip">If ip null or empty, it will using loopback address. If ip setting as *, it will use any address in network.</param>
    public BasicTcpServer(string ip, int port)
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

      _Listener = new TcpListener(_IPAddress, port);
      _Listener.Server.SendTimeout = 600000;
      _Listener.Server.ReceiveTimeout = 600000;

      _Token = _TokenSource.Token;
    }

    public void Start()
    {
      if (IsListening) throw new InvalidOperationException("TcpServer already running");

      _Listener.Start();
      _TokenSource = new CancellationTokenSource();
      _Token = _TokenSource.Token;

      Task.Run(() => MonitorForNewClients(), _Token);

      IsListening = true;
    }

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

    public bool SendToClient(string ipPort, long contentLength, Stream stream, Dictionary<string, string> additionalHeaders = null)
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
          client.Send(contentLength, stream, additionalHeaders);

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

    public void SendToAllClients(string data)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, data);
      }
    }

    public void SendToAllClients(byte[] data)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, data);
      }
    }

    public void SendToAllClients(long contentLength, Stream stream)
    {
      foreach (string ip in GetClients())
      {
        SendToClient(ip, contentLength, stream);
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
