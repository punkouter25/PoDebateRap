using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging; // Required for ILogger, ILoggerProvider etc.

namespace PoDebateRap.Server.Logging // Assuming a Logging namespace
{
    /// <summary>
    /// Provides file logging capabilities.
    /// NOTE: This is a basic implementation for demonstration.
    /// Consider using established libraries like Serilog or NLog for production scenarios.
    /// </summary>
    [ProviderAlias("File")]
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private static readonly object _lock = new(); // Lock for file writing

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
             // Optional: Clear log file on startup?
             // try { File.WriteAllText(_filePath, string.Empty); } catch { /* Ignore potential errors */ }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _filePath, _lock));
        }

        public void Dispose()
        {
            _loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Basic file logger implementation.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _name;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string name, string filePath, object fileLock)
        {
            _name = name;
            _filePath = filePath;
            _lock = fileLock;
        }

        // Basic scope handling - could be enhanced
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            // Log all levels for this basic implementation
            // Could be configured via appsettings.json if needed
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null) // Don't log if no message and no exception
            {
                return;
            }

            var logRecord = new StringBuilder();
            logRecord.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC] ");
            logRecord.Append($"[{logLevel.ToString().ToUpperInvariant(),-11}] "); // Pad level for alignment
            logRecord.Append($"[{_name}] "); // Category name
            logRecord.Append(message);

            if (exception != null)
            {
                logRecord.AppendLine();
                logRecord.Append(exception.ToString()); // Include exception details
            }

            lock (_lock) // Simple lock to prevent concurrent writes corrupting the file
            {
                try
                {
                    // Ensure directory exists (might be redundant but safe)
                    var directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.AppendAllText(_filePath, logRecord.ToString() + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Log error writing to file (e.g., to console/debug)
                    Console.WriteLine($"CRITICAL: Error writing to log file {_filePath}: {ex.Message}");
                }
            }
        }
    }
}
