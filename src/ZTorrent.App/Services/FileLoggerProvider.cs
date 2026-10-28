using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ZTorrent.App.Services;

/// <summary>
/// Minimal, dependency-free rolling file logger. ZTorrent does not send
/// telemetry anywhere; this file is the only diagnostic surface the user has.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly string _directory;
    private StreamWriter? _writer;
    private DateOnly _writerDate;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        lock (_sync)
        {
            EnsureWriter(date);
            if (_writer is null)
            {
                return;
            }

            var builder = new StringBuilder(256);
            builder.Append(now.ToString("HH:mm:ss.fff"))
                .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
                .Append(category);
            if (eventId.Id != 0)
            {
                builder.Append(" (").Append(eventId.Id).Append(')');
            }

            builder.Append(' ').Append(message);
            _writer.WriteLine(builder.ToString());
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
        }
    }

    private void EnsureWriter(DateOnly date)
    {
        if (_writer is not null && _writerDate == date)
        {
            return;
        }

        _writer?.Dispose();
        _writerDate = date;
        var path = Path.Combine(_directory, $"ztorrent-{date:yyyy-MM-dd}.log");
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
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

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_category, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}
