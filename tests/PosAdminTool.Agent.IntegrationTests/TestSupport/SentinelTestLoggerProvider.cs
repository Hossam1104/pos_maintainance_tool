using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Captures rendered Agent log messages so secret-scan tests can prove sentinel values
/// never enter application logs.</summary>
public sealed class SentinelLogSink
{
    private readonly ConcurrentQueue<string> _messages = new();

    public string Messages => string.Join(Environment.NewLine, _messages);

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    internal void Add(string message) => _messages.Enqueue(message);
}

public sealed class SentinelTestLoggerProvider(SentinelLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SentinelTestLogger(sink);

    public void Dispose()
    {
    }

    private sealed class SentinelTestLogger(SentinelLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Add(formatter(state, exception));
            if (exception is not null)
            {
                sink.Add(exception.Message);
            }
        }
    }
}
