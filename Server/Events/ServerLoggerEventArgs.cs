using System;

namespace BasicTcp.Events
{
  public enum LogType
  {
    /// <summary>
    /// Basic messages.
    /// </summary>
    TCP = 0,
    /// <summary>
    /// Error messages.
    /// </summary>
    ERROR = 1,
    /// <summary>
    /// Exception messages.
    /// </summary>
    EXCEPTION = 2
  }

  public class ServerLoggerEventArgs : EventArgs
  {
    internal ServerLoggerEventArgs(LogType logType, string message, Exception exception = null)
    {
      LogType = logType;
      Message = message;
      Exception = exception;
    }

    /// <summary>
    /// Message with log info.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Exception that was occured before logging.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Type of log message.
    /// </summary>
    public LogType LogType { get; }
  }
}
