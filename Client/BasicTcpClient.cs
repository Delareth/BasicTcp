using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using BasicTcp.Events;
using System.Net;
using System.Collections.Generic;

namespace BasicTcp
{
  public class BasicTcpClient : IDisposable
  {
    private TcpClient _Client;
    private NetworkStream _NetworkStream;

    private CancellationTokenSource _TokenSource = new CancellationTokenSource();
    private CancellationToken _Token;

    private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);

    private bool _IsConnected = false;
    private ClientEvents _Events = new ClientEvents();

    private bool _IsInitialized = false;
    private bool _IsDisposed = false;

    private readonly uint _AutoReconnectTime = 0;
    private readonly int _Port = 0;

    private readonly IPAddress _IPAddress = null;

    public ClientEvents Events
    {
      get
      {
        return _Events;
      }
      set
      {
        if (value == null) _Events = new ClientEvents();
        else _Events = value;
      }
    }

    public bool IsConnected
    {
      get
      {
        return _IsConnected;
      }
      private set
      {
        if (_IsConnected == value) return;

        _IsConnected = value;

        if (_IsConnected)
        {
          Events.HandleConnected(this);

          if (Timers.IsTimerExist("AutoReconnect")) Timers.Kill("AutoReconnect");
        }
        else
        {
          Events.HandleDisconnected(this);
          if (_AutoReconnectTime != 0) StartAutoReconnect();
        }
      }
    }

    /// <summary>
    /// Initializing TCP client.
    /// </summary>
    /// <param name="autoReconnectTime">Time to reconnect to server in MS. Disabled if set to 0.</param>
    public BasicTcpClient(string ip, int port, uint autoReconnectTime = 0)
    {
      if (string.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
      if (port < 0) throw new ArgumentException("Port must be zero or greater.");

      try
      {
        _AutoReconnectTime = autoReconnectTime;

        if (!IPAddress.TryParse(ip, out _IPAddress))
        {
          _IPAddress = Dns.GetHostEntry(ip).AddressList[0];
        }

        _Port = port;

        _Client = new TcpClient
        {
          ReceiveTimeout = 600000,
          SendTimeout = 600000
        };
        _Token = _TokenSource.Token;

        _IsInitialized = true;
      }
      catch (Exception ex)
      {
        if (autoReconnectTime != 0) StartAutoReconnect();
        else throw ex;
      }
    }

    /// <summary>
    /// Start connection to server.
    /// </summary>
    public void Start()
    {
      if (Timers.IsTimerExist("AutoReconnect")) return;
      if (IsConnected) throw new InvalidOperationException("TcpClient already running");
      if (!_IsInitialized) throw new InvalidOperationException("TcpClient not initialized");

      IAsyncResult ar = _Client.BeginConnect(_IPAddress, _Port, null, null);
      WaitHandle wh = ar.AsyncWaitHandle;

      try
      {
        if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
        {
          _Client.Close();
          _IsInitialized = false;
          Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, $"Timeout connecting to {_IPAddress}:{_Port}"));

          throw new TimeoutException("Timeout connecting to " + _IPAddress + ":" + _Port);
        }

        if (!_Client.Connected)
        {
          Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, $"Timeout connecting to {_IPAddress}:{_Port}"));
          throw new TimeoutException("Timeout connecting to " + _IPAddress + ":" + _Port);
        }

        _Client.EndConnect(ar);
        _NetworkStream = _Client.GetStream();

        StartWithoutChecks();
      }
      catch (Exception ex)
      {
        if (_AutoReconnectTime != 0) StartAutoReconnect();
        else throw ex;
      }
      finally
      {
        wh.Close();
      }
    }

    /// <summary>
    /// Stop client.
    /// </summary>
    public void Stop()
    {
      if (!IsConnected) throw new InvalidOperationException("TcpClient is not running");

      Dispose();

      if (IsConnected)
      {
        Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, "Can't stop client"));
      }
      else
      {
        Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.TCP, "Client successful stopped"));
      }
    }

    public void Dispose()
    {
      try
      {
        if (_TokenSource != null)
        {
          if (!_TokenSource.IsCancellationRequested) _TokenSource.Cancel();
          _TokenSource.Dispose();
          _TokenSource = null;
        }

        if (_NetworkStream != null)
        {
          _NetworkStream.Close();
          _NetworkStream.Dispose();
          _NetworkStream = null;
        }

        if (_Client != null)
        {
          _Client.Close();
          _Client.Dispose();
          _Client = null;
        }
      }
      catch (Exception ex)
      {
        Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.EXCEPTION, "Can't dispose client", ex));
      }

      _IsDisposed = true;
      IsConnected = false;
      _IsInitialized = false;
      GC.SuppressFinalize(this);
    }

    public void Send(string data, Dictionary<string, string> additionalHeaders = null)
    {
      if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
      if (!_IsConnected) throw new IOException("Client not connected to server.");

      byte[] bytes = Encoding.UTF8.GetBytes(data);

      CreateDataAndSend(bytes, additionalHeaders);
    }

    public void Send(byte[] data, Dictionary<string, string> additionalHeaders = null)
    {
      if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
      if (!_IsConnected) throw new IOException("Client not connected to server.");

      CreateDataAndSend(data, additionalHeaders);
    }

    public void Send(Stream stream, Dictionary<string, string> additionalHeaders = null)
    {
      if (stream == null) throw new ArgumentNullException(nameof(stream));
      if (stream.Length < 1) throw new ArgumentException("Cannot send empty stream");
      if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
      if (!_IsConnected) throw new IOException("Client not connected to server.");

      CreateDataAndSend(ConvertStreamToByteArray(stream), additionalHeaders);
    }

    private void CreateDataAndSend(byte[] data, Dictionary<string, string> additionalHeaders)
    {
      _SendLock.Wait();

      byte[] header = GetHeaderBytes(data.Length, additionalHeaders);
      byte[] headerLength = BitConverter.GetBytes(header.Length);
      byte[] sendData = SerializeDataToSend(headerLength, header, data);

      try
      {
        _NetworkStream.Write(sendData, 0, sendData.Length);
      }
      catch (Exception ex)
      {
        Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.EXCEPTION, "Can't send data to server", ex));
      }

      _SendLock.Release();
    }

    private byte[] GetHeaderBytes(long contentLength, Dictionary<string, string> additionalHeaders)
    {
      return Encoding.UTF8.GetBytes($"Content-length:{contentLength}{"\r\n"}{GetAdditionalHeaders(additionalHeaders)}");
    }

    private byte[] SerializeDataToSend(byte[] headerLength, byte[] headerData, byte[] data)
    {
      byte[] outArray = new byte[headerLength.Length + headerData.Length + data.Length];
      Buffer.BlockCopy(headerLength, 0, outArray, 0, headerLength.Length);
      Buffer.BlockCopy(headerData, 0, outArray, headerLength.Length, headerData.Length);
      Buffer.BlockCopy(data, 0, outArray, headerLength.Length + headerData.Length, data.Length);

      return outArray;
    }

    private byte[] ConvertStreamToByteArray(Stream input)
    {
      using MemoryStream memoryStream = new MemoryStream();

      input.CopyTo(memoryStream);
      return memoryStream.ToArray();
    }

    private void StartReceiveData(CancellationToken token)
    {
      while (true)
      {
        try
        {
          if (_Client == null || !_Client.Connected)
          {
            Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, "Disconnection detected"));
            _IsConnected = false;
            break;
          }

          if (token.IsCancellationRequested)
          {
            Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, "Cancellation operation detected"));
            break;
          }

          DataPacket packet = ReadPacket();

          if (packet == null) continue;

          Events.HandleDataReceived(this, new DataReceivedEventArgs(packet.Data, packet.Header));
        }
        catch (SocketException)
        {
          Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, "Data receiver socket exception (disconnection)"));
          IsConnected = false;
        }
        catch (IOException)
        {
          Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.ERROR, "Data receiver io exception (disconnection)"));
          _Client.Close();
          _IsInitialized = false;
          IsConnected = false;
        }
        catch (Exception ex)
        {
          Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.EXCEPTION, "Data receiver exception", ex));
        }
      }
    }

    private DataPacket ReadPacket()
    {
      int headerLength = ReceiveBytes<int>(sizeof(int));

      Dictionary<string, string> header = ReceiveBytes<Dictionary<string, string>>(headerLength);

      byte[] data = ReceiveBytes<byte[]>(Convert.ToInt32(header["Content-length"]));

      return new DataPacket(header, data);
    }

    private T ReceiveBytes<T>(int contentLength)
    {
      if (!_NetworkStream.CanRead) throw new IOException();

      int read = 0;
      byte[] buffer = new byte[contentLength];

      while (read < contentLength)
      {
        int readedFromStream = _NetworkStream.Read(buffer, read, buffer.Length - read);

        if (readedFromStream <= 0) throw new SocketException();

        read += readedFromStream;
      }

      return ConvertDataToType<T>(buffer);
    }

    private T ConvertDataToType<T>(byte[] data)
    {
      if (typeof(T) == typeof(int))
      {
        return (T)Convert.ChangeType(BitConverter.ToInt32(data), typeof(T));
      }
      else if (typeof(T) == typeof(Dictionary<string, string>))
      {
        string rawHeader = Encoding.UTF8.GetString(data);

        Dictionary<string, string> header = NormalizeRawHeader(rawHeader);

        return (T)Convert.ChangeType(header, typeof(T));
      }
      else if (typeof(T) == typeof(byte[]))
      {
        return (T)Convert.ChangeType(data, typeof(T));
      }

      return ConvertDataToUnknownType<T>(data);
    }

    /// <summary>
    /// Override this if you need more types to convert after receiving data.
    /// </summary>
    public T ConvertDataToUnknownType<T>(byte[] data)
    {
      throw new Exception("Incompatible receiver type");
    }

    private void StartAutoReconnect()
    {
      if (_IsDisposed) return;

      Timers.Create("AutoReconnect", _AutoReconnectTime, false, () =>
      {
        if (_Client != null && _Client.Connected) return;

        if (!_IsInitialized)
        {
          try
          {
            _Client = new TcpClient(_IPAddress.ToString(), _Port)
            {
              SendTimeout = 600000,
              ReceiveTimeout = 600000
            };

            _NetworkStream = _Client.GetStream();
            _Token = _TokenSource.Token;

            _IsInitialized = true;

            StartWithoutChecks();
          }
          catch (Exception ex)
          {
            Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.EXCEPTION, "Can't reconnect to server...", ex));
            StartAutoReconnect();
          }
        }
        else
        {
          try
          {
            _Client.Connect(_IPAddress, _Port);

            if (_Client.Connected)
            {
              _NetworkStream = _Client.GetStream();

              StartWithoutChecks();
            }
          }
          catch (Exception ex)
          {
            Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.EXCEPTION, "Can't reconnect to server...", ex));
            StartAutoReconnect();
          }
        }
      });
    }

    private void StartWithoutChecks()
    {
      _TokenSource = new CancellationTokenSource();
      _Token = _TokenSource.Token;

      Task.Run(() => StartReceiveData(_Token), _Token);

      IsConnected = true;
      Events.HandleClientLog(this, new ClientLoggerEventArgs(LogType.TCP, "Client successful connected to server"));
    }

    private Dictionary<string, string> NormalizeRawHeader(string rawHeader)
    {
      string[] headerLines = rawHeader.Split("\r\n");

      Dictionary<string, string> header = new Dictionary<string, string>();

      if (headerLines.Length == 0) return header;

      foreach (string line in headerLines)
      {
        if (string.IsNullOrEmpty(line)) continue;

        string[] lineInfo = line.Split(":");

        if (lineInfo.Length != 2) continue;

        if (header.ContainsKey(lineInfo[0])) continue;

        header.Add(lineInfo[0], lineInfo[1]);
      }

      return header;
    }

    private string GetAdditionalHeaders(Dictionary<string, string> additionalHeaders)
    {
      if (additionalHeaders == null) return "";
      else
      {
        string headers = "";

        foreach (KeyValuePair<string, string> entry in additionalHeaders)
        {
          headers += $"{entry.Key}:{entry.Value}{"\r\n"}";
        }

        return headers;
      }
    }
  }
}
