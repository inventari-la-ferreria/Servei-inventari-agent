using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace InventariAgentSvc.Services;

public class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;

    public SimpleFileLoggerProvider(string path)
    {
        _path = path;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleFileLogger(categoryName, _path);
    }

    public void Dispose() { }
}

public class SimpleFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _path;
    private static readonly object _lock = new object();

    public SimpleFileLogger(string categoryName, string path)
    {
        _categoryName = categoryName;
        _path = path;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message)) return;

        var logRecord = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {message}";
        if (exception != null)
        {
            logRecord += Environment.NewLine + exception.ToString();
        }

        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.AppendAllText(_path, logRecord + Environment.NewLine);
            }
        }
        catch
        {
            // No podemos hacer nada si falla el log
        }
    }
}
