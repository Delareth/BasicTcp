using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BasicTcp
{
  public class Client : IDisposable
  {
    private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
    private readonly CancellationToken _Token;

    private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
    private bool _IsConnected = false;

    private readonly BasicTcpServer _Server;
    private readonly NetworkStream _NetworkStream;

    public bool IsConnected
    {
      get
      {
        return _IsConnected;
      }
      set
      {
        if (value == _IsConnected) return;

        _IsConnected = value;
      }
    }

    public TcpClient TcpClient { get; }
    public string Ip { get; }

    public Client(BasicTcpServer server, TcpClient client)
    {
      _Server = server;

      Ip = client.Client.RemoteEndPoint.ToString();
      TcpClient = client;

      _Token = _TokenSource.Token;

      _NetworkStream = TcpClient.GetStream();

      Task.Run(() => StartReceiveData(_Token), _Token);

      IsConnected = true;
    }

    public void Dispose()
    {
      if (_TokenSource != null)
      {
        if (!_TokenSource.IsCancellationRequested) _TokenSource.Cancel();
        _TokenSource.Dispose();
      }

      if (_NetworkStream != null)
      {
        _NetworkStream.Close();
      }

      if (TcpClient != null)
      {
        TcpClient.Close();
        TcpClient.Dispose();
      }
    }

    public void Send(string data, Dictionary<string, string> additionalHeaders = null)
    {
      if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

      byte[] bytes = Encoding.UTF8.GetBytes(data);

      CreateDataAndSend(bytes, additionalHeaders);
    }

    public void Send(byte[] data, Dictionary<string, string> additionalHeaders = null)
    {
      if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

      CreateDataAndSend(data, additionalHeaders);
    }

    public void Send(Stream stream, Dictionary<string, string> additionalHeaders = null)
    {
      if (stream == null) throw new ArgumentNullException(nameof(stream));
      if (stream.Length < 1) throw new ArgumentException("Cannot send empty stream");
      if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

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
        _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.EXCEPTION, "Can't send data for client: " + Ip, ex));
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
          if (TcpClient == null || !TcpClient.Connected)
          {
            IsConnected = false;
            _Server.Events.HandleClientDisconnected(this, new Events.ClientDisconnectedEventArgs(Ip, Events.DisconnectReason.Timeout));

            break;
          }

          if (token.IsCancellationRequested)
          {
            _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.ERROR, "Cancellation operation detected"));
            break;
          }

          DataPacket packet = ReadPacket();

          if (packet == null) continue;

          _Server.Events.HandleDataReceived(this, new Events.DataReceivedFromClientEventArgs(Ip, packet.Data, packet.Header));
        }
        catch (Exception ex)
        {
          if (ex is SocketException || ex is IOException)
          {
            _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.ERROR, "Data receiver socket exception (disconnection)"));
            IsConnected = false;
            _Server.DisconnectClient(Ip);
            return;
          }

          _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.EXCEPTION, "Data receiver exception", ex));
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
