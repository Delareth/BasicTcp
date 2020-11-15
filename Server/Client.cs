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

    // for data receiving
    private bool _IsHeaderReceived = false;
    private MemoryStream _CurrentReceivingMs = null;
    private long _CurrentReceivingMsSize = 0;
    private Dictionary<string, string> _Header = new Dictionary<string, string>();

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

      Task.Run(() => StartRecieveDataAsync(_Token), _Token);

      IsConnected = true;
    }

    public void Send(string data, Dictionary<string, string> additionalHeaders = null)
    {
      if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

      byte[] bytes = Encoding.UTF8.GetBytes(data);
      MemoryStream ms = new MemoryStream();
      ms.Write(bytes, 0, bytes.Length);
      ms.Seek(0, SeekOrigin.Begin);

      _SendLock.Wait();
      SendHeader(bytes.Length, additionalHeaders);
      SendInternal(bytes.Length, ms);
      _SendLock.Release();
    }

    public void Send(byte[] data, Dictionary<string, string> additionalHeaders = null)
    {
      if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

      MemoryStream ms = new MemoryStream();
      ms.Write(data, 0, data.Length);
      ms.Seek(0, SeekOrigin.Begin);

      _SendLock.Wait();
      SendHeader(data.Length, additionalHeaders);
      SendInternal(data.Length, ms);
      _SendLock.Release();
    }

    public void Send(long contentLength, Stream stream, Dictionary<string, string> additionalHeaders = null)
    {
      if (contentLength < 1) return;
      if (stream == null) throw new ArgumentNullException(nameof(stream));
      if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
      if (!IsConnected) throw new IOException($"Not connected to the server {Ip}");

      _SendLock.Wait();
      SendHeader(contentLength, additionalHeaders);
      SendInternal(contentLength, stream);
      _SendLock.Release();
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

    private void SendHeader(long contentLength, Dictionary<string, string> additionalHeaders)
    {
      byte[] bytes = Encoding.UTF8.GetBytes($"Content-length:{contentLength}{Environment.NewLine}{GetAdditionalHeaders(additionalHeaders)}");
      MemoryStream ms = new MemoryStream();
      ms.Write(bytes, 0, bytes.Length);
      ms.Seek(0, SeekOrigin.Begin);

      SendInternal(bytes.Length, ms);

      Task.Delay(30).GetAwaiter().GetResult();
    }

    private void SendInternal(long contentLength, Stream stream)
    {
      long bytesRemaining = contentLength;
      int bytesRead;
      byte[] buffer = new byte[1024];

      try
      {
        while (bytesRemaining > 0)
        {
          bytesRead = stream.Read(buffer, 0, buffer.Length);
          if (bytesRead > 0)
          {
            _NetworkStream.Write(buffer, 0, bytesRead);

            bytesRemaining -= bytesRead;
          }
        }

        _NetworkStream.Flush();
      }
      catch (Exception ex)
      {
        _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.EXCEPTION, "Can't send internal data for client: " + Ip, ex));
      }
    }

    private async Task StartRecieveDataAsync(CancellationToken token)
    {
      try
      {
        while (true)
        {
          if (TcpClient == null || !TcpClient.Connected)
          {
            IsConnected = false;
            _Server.Events.HandleClientDisconnected(this, new Events.ClientDisconnectedEventArgs(Ip, Events.DisconnectReason.Timeout));

            break;
          }

          byte[] data = await DataReadAsync(token);
          if (data == null)
          {
            await Task.Delay(30);
            continue;
          }

          if (!_IsHeaderReceived)
          {
            _Header = NormalizeRawHeader(Encoding.UTF8.GetString(data));

            if (_Header.ContainsKey("Content-length"))
            {
              _IsHeaderReceived = true;
              _CurrentReceivingMs = new MemoryStream();
              _CurrentReceivingMsSize = Convert.ToInt64(_Header["Content-length"]);
            }
          }
          else
          {
            _CurrentReceivingMs.Write(data);

            if (_CurrentReceivingMs.Length >= _CurrentReceivingMsSize)
            {
              _IsHeaderReceived = false;
              _Server.Events.HandleDataReceived(this, new Events.DataReceivedFromClientEventArgs(Ip, _CurrentReceivingMs.ToArray(), _Header));

              _CurrentReceivingMs = null;
              _CurrentReceivingMsSize = 0;
              _Header.Clear();
            }
          }
        }
      }
      catch (SocketException)
      {
        _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.ERROR, "Data receiver socket exception (disconnection)"));
        IsConnected = false;
        _Server.DisconnectClient(Ip);
      }
      catch (IOException)
      {
        _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.ERROR, "Data receiver socket exception (disconnection)"));
        IsConnected = false;
        _Server.DisconnectClient(Ip);
      }
      catch (Exception ex)
      {
        _Server.Events.HandleServerLog(this, new Events.ServerLoggerEventArgs(Events.LogType.EXCEPTION, "Data receiver exception", ex));
      }
    }

    private async Task<byte[]> DataReadAsync(CancellationToken token)
    {
      if (TcpClient == null || !TcpClient.Connected || token.IsCancellationRequested) throw new OperationCanceledException();

      if (!_NetworkStream.CanRead) throw new IOException();

      byte[] buffer = new byte[1024];
      int read;

      using MemoryStream ms = new MemoryStream();
      while (true)
      {
        read = await _NetworkStream.ReadAsync(buffer, 0, buffer.Length);

        if (read > 0)
        {
          ms.Write(buffer, 0, read);
          return ms.ToArray();
        }
        else
        {
          throw new SocketException();
        }
      }
    }

    private Dictionary<string, string> NormalizeRawHeader(string rawHeader)
    {
      string[] headerLines = rawHeader.Split(Environment.NewLine);

      Dictionary<string, string> header = new Dictionary<string, string>();

      foreach (string line in headerLines)
      {
        if (string.IsNullOrEmpty(line)) continue;

        string[] lineInfo = line.Split(":");

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
          headers += $"{entry.Key}:{entry.Value}{Environment.NewLine}";
        }

        return headers;
      }
    }
  }
}
