using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Coms.Desktop.Infrastructure
{
    /// <summary>
    /// Minimal rolling file logger: one file per day under the configured
    /// directory, one line per entry. Enough for a desktop client; the
    /// web host uses the standard console and file logging instead.
    /// </summary>
    internal sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _directory;
        private readonly LogLevel _minimumLevel;
        private readonly object _gate = new object();

        public FileLoggerProvider(string directory, LogLevel minimumLevel)
        {
            _directory = directory;
            _minimumLevel = minimumLevel;
            Directory.CreateDirectory(directory);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
        }

        private void Write(string category, LogLevel level, string message, Exception exception)
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(' ').Append(Level(level))
                .Append(' ').Append(category)
                .Append(": ").Append(message);

            if (exception != null)
            {
                line.AppendLine().Append(exception);
            }

            string path = Path.Combine(_directory, "coms-desktop-" + DateTime.Now.ToString("yyyyMMdd") + ".log");

            lock (_gate)
            {
                File.AppendAllText(path, line.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }

        private static string Level(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace: return "TRC";
                case LogLevel.Debug: return "DBG";
                case LogLevel.Information: return "INF";
                case LogLevel.Warning: return "WRN";
                case LogLevel.Error: return "ERR";
                case LogLevel.Critical: return "CRT";
                default: return "???";
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= _provider._minimumLevel && logLevel != LogLevel.None;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                _provider.Write(_category, logLevel, formatter(state, exception), exception);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            {
            }
        }
    }
}
