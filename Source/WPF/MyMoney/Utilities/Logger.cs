using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Walkabout.Utilities
{

    public interface ILogger : IDisposable
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception ex);
        void FatalUnhandledException(string message, Exception ex);
        string LogPath { get; }
    }

    /// <summary>
    /// A simple to use logger that does thread safe write to disk using a Channel object.
    /// </summary>
    public class Log : ILogger
    {
        class LogEvent
        {
            public LogLevel level;
            public string component;
            public string message;
            public DateTime timestamp;

            public LogEvent(LogLevel level, string component, string message)
            {
                this.timestamp = DateTime.Now;
                this.level = level;
                this.component = component;
                this.message = message;
                this.timestamp = DateTime.Now;
            }

            public LogEvent(LogLevel level, string component, string message, Exception ex)
            {
                this.timestamp = DateTime.Now;
                this.level = level;
                this.component = component;
                this.message = message;
                if (ex != null)
                {
                    this.message += "\n" + ex.GetType() + ": " + ex.Message + "\n" + ex.StackTrace;
                }
                this.timestamp = DateTime.Now;
            }

            public override string ToString()
            {
                return this.timestamp.ToString("yyyy-MM-dd HH:mm:ss.ffff") + " " + this.Message + "\n";
            }

            public string Message
            {
                get
                {
                    return this.component.ToUpper() + " " + this.level.ToString().ToUpper() + ": " + this.message;
                }
            }
        }

        public const string ReportLogging = "Please report the details and the most recent logs in %TEMP%\\MyMoney\\Logs to https://github.com/MoneyTools/MyMoney.Net/issues";

        string component;
        private Channel<LogEvent> channel;
        private string folder;
        private string filePath;
        private DateTime today;
        private bool disposed;
        private CancellationTokenSource cts;
        private static Log instance;

        public string LogPath => this.folder;

        public static ILogger Instance { get { return instance; } }

        public Log(string folder)
        {
            instance?.Dispose();
            instance = this;
            this.folder = folder;
            this.cts = new CancellationTokenSource();
            this.channel = Channel.CreateUnbounded<LogEvent>();
            this.UpdateFilePath();
            Task.Run(this.FlushEventsToDisk);
        }

        public static Log GetLogger(string component)
        {
            if (instance != null)
            {
                return new Log(instance, component);
            }
            throw new Exception("Base logger is not created yet");
        }

        private Log(Log rootLog, string component)
        {
            this.channel = rootLog.channel;
            this.component = component;
            this.folder = rootLog.folder;
        }

        private void UpdateFilePath()
        {
            var today = DateTime.Today;
            if (this.today != today)
            {
                // roll over to new log file.
                this.filePath = Path.Combine(folder, "MyMoney_" + DateTime.Now.ToString("yyyy-MM-dd") + "_log.txt");
                this.today = today;
            }
        }

        public void Info(string message)
        {
            this.channel.Writer.TryWrite(new LogEvent(LogLevel.Information, component, message));
        }

        public void Warning(string message)
        {
            this.channel.Writer.TryWrite(new LogEvent(LogLevel.Warning, component, message));
        }

        public void Error(string message, Exception ex = null)
        {
            this.channel.Writer.TryWrite(new LogEvent(LogLevel.Error, component, message, ex));
        }

        public void FatalUnhandledException(string message, Exception ex)
        {
            string details = "";
            if (ex != null)
            {
                details = "\n" + ex.GetType() + ": " + ex.Message + "\n" + ex.StackTrace;
            }
            this.SaveCrashLog(message, details);
        }

        static object crashSyncObject = new object();

        private void SaveCrashLog(string message, string details)
        {
            lock (crashSyncObject)
            {
                var crash = new CrashReport()
                {
                    Date = DateTime.Now,
                    Message = message,
                    Details = details
                };
                crash.Save(this.folder);
            }
        }

        public static void CheckCrashLog(string folder)
        {
            var crash = CrashReport.Load(folder);
            if (crash != null) { 
                MessageBoxEx.Show($"Money app crashed on {crash.Date} - " + ReportLogging + "\r\n" + crash.Message, 
                    "Crash Report", crash.Details, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            disposed = true;
            this.cts?.Cancel();
            this.channel?.Writer.TryWrite(new LogEvent(LogLevel.Critical, "terminate", "terminate"));
        }

        private async void FlushEventsToDisk()
        {
            var token = cts.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var e = await this.channel.Reader.ReadAsync(token);
                    if (disposed) return;
                    this.UpdateFilePath();
                    Debug.WriteLine(e.Message);
                    File.AppendAllText(this.filePath, e.ToString());
                }
                catch (TaskCanceledException)
                {}
            }
        }
    }

    public class CrashReport 
    {
        public CrashReport()
        {
        }

        public DateTime Date { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }

        public void Save(string folder)
        { 
            XmlSerializer s = new XmlSerializer(typeof(CrashReport));
            string crashFile = Path.Combine(Path.GetDirectoryName(folder), "crash.xml");
            using var stream = File.OpenWrite(crashFile);
            s.Serialize(stream, this);
        }

        public static CrashReport Load(string folder)
        {
            string crashFile = Path.Combine(Path.GetDirectoryName(folder), "crash.xml");
            if (File.Exists(crashFile))
            {
                try
                {
                    XmlSerializer s = new XmlSerializer(typeof(CrashReport));
                    using var stream = File.OpenRead(crashFile);
                    return (CrashReport)s.Deserialize(stream);
                }
                catch
                {
                }
                try
                {
                    File.Delete(crashFile);
                }
                catch { }
            }
            return null;
        }
    }
}
