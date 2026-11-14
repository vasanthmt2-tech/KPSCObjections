using System;
using System.Diagnostics;
using System.IO;
using System.Web.Hosting;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Writes application errors to App_Data\logs\yyyy-MM-dd.log (App_Data is
    /// never served to browsers). The application pool identity needs write
    /// access to that folder; if it has none, logging is skipped silently
    /// rather than breaking the request.
    ///
    /// Never log OTPs, passwords, keys or full payment payloads.
    /// </summary>
    public static class AppLog
    {
        private static readonly object Sync = new object();

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Write(string level, string message)
        {
            try
            {
                string folder = HostingEnvironment.MapPath("~/App_Data/logs");

                if (folder == null)
                    return;

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + " " +
                    (message ?? "").Replace("\r\n", "\n").Replace("\n", Environment.NewLine + "    ") +
                    Environment.NewLine;

                lock (Sync)
                {
                    Directory.CreateDirectory(folder);
                    File.AppendAllText(
                        Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), line);
                }
            }
            catch
            {
                // Logging must never take the site down.
            }
        }
    }

    /// <summary>
    /// Sends System.Diagnostics.Trace output (Trace.TraceError and so on) to
    /// AppLog. Registered once in Global.asax.
    /// </summary>
    public class AppLogTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            // TraceError writes a "source Error: 0 : " prefix with Write and
            // the text with WriteLine; only the text is worth keeping.
        }

        public override void WriteLine(string message)
        {
            AppLog.Write("TRACE", message);
        }

        public override void TraceEvent(
            TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            AppLog.Write(eventType.ToString().ToUpperInvariant(), message);
        }

        public override void TraceEvent(
            TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            string format, params object[] args)
        {
            AppLog.Write(eventType.ToString().ToUpperInvariant(),
                args == null || args.Length == 0 ? format : string.Format(format, args));
        }
    }
}
