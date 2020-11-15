using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace BasicTcp
{
  public class Timers
  {
    private static readonly Dictionary<string, Timer> m_Timers;

    static Timers()
    {
      try
      {
        m_Timers = new Dictionary<string, Timer>();
      }
      catch (Exception ex)
      {
        throw ex;
      }
    }

    public static bool Create(string sTimerName, uint uiIntervalMS, bool bRepeat, Action action)
    {
      if (m_Timers.ContainsKey(sTimerName))
      {
        return false;
      }

      Timer callingTimer = new Timer(uiIntervalMS);

      m_Timers.Add(sTimerName, callingTimer);
      callingTimer.AutoReset = bRepeat;
      callingTimer.Elapsed += async (sender, e) => await Task.Run(action);

      if (!bRepeat)
      {
        callingTimer.Elapsed += delegate
        {
          Kill(sTimerName);
        };
      }
      callingTimer.Enabled = true;

      return true;
    }

    public static bool Kill(string sTimerName)
    {
      Timer timer = GetTimer(sTimerName);

      if (timer == null)
      {
        return false;
      }

      timer.Stop();
      timer.Close();

      return m_Timers.Remove(sTimerName);
    }

    public static Timer GetTimer(string sTimerName)
    {
      if (!m_Timers.ContainsKey(sTimerName))
      {
        return null;
      }

      return m_Timers[sTimerName];
    }

    public static bool IsTimerExist(string sTimerName)
    {
      return m_Timers.ContainsKey(sTimerName);
    }
  }
}
