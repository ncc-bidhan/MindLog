using Microsoft.Extensions.Logging;

namespace MindLog.Helpers
{
    public static class Logger
    {
        private static ILoggerFactory? _loggerFactory;
        private static readonly object _lock = new();

        public static void Initialize(ILoggerFactory loggerFactory)
        {
            lock (_lock)
            {
                _loggerFactory = loggerFactory;
            }
        }

        public static ILogger<T> GetLogger<T>()
        {
            if (_loggerFactory == null)
            {
                return new NullLogger<T>();
            }

            return _loggerFactory.CreateLogger<T>();
        }

        private class NullLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) => new DisposableScope();

            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }

            private class DisposableScope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
