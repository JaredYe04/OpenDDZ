using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.Utils
{
    public class Logger
    {
        internal enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
        internal static LogLevel CurrentLogLevel { get; set; } = LogLevel.Debug;

        internal string loggerFileName;
        private Logger()
        {
            lock (this)
            {
                loggerFileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            }
            //目录放在可执行文件同目录的logs文件夹下，如果没有则创建
            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!System.IO.Directory.Exists(logDir))
            {
                System.IO.Directory.CreateDirectory(logDir);
            }
            loggerFileName = System.IO.Path.Combine(logDir, loggerFileName);
            //Info($"Logger initialized. Log file: {loggerFileName}");

        }
        public static Logger Instance { get; private set; } = new Logger();
        internal void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level < CurrentLogLevel) return;
            string prefix;
            switch (level)
            {
                case LogLevel.Debug:
                    prefix = "[DEBUG]";
                    break;
                case LogLevel.Info:
                    prefix = "[INFO]";
                    break;
                case LogLevel.Warning:
                    prefix = "[WARNING]";
                    break;
                case LogLevel.Error:
                    prefix = "[ERROR]";
                    break;
                default:
                    prefix = "[LOG]";
                    break;
            }
            prefix = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {prefix}";
            Console.WriteLine($"{prefix} {message}");
            try
            {
                System.IO.File.AppendAllText(Instance.loggerFileName, $"{prefix} {message}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to write log to file: {e.Message}");
            }
        }

        internal void Debug(string message) => Log(message, LogLevel.Debug);
        internal void Info(string message) => Log(message, LogLevel.Info);
        internal void Warning(string message) => Log(message, LogLevel.Warning);
        internal void Error(string message) => Log(message, LogLevel.Error);
    }
}
