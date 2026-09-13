using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SafeErrorPersistenceTests
{
    private sealed class ThrowingErrorsRepository : IErrorsRepository
    {
        public Task<Guid> Create(Error error) =>
            throw new InvalidOperationException("Could not find stored procedure 'Errors_Create'.");
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<(LogLevel Level, string Message, Exception? Ex)> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerFactory factory) : ILogger
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
                factory.Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }

    /// <summary>
    /// Mirrors ExceptionMiddleware persistence try/catch: Errors_Create failure must not escape.
    /// </summary>
    private static async Task PersistOrLogAsync(
        IErrorsRepository repository,
        ILogger? logger,
        Exception original)
    {
        try
        {
            await repository.Create(new Error
            {
                Date = DateTime.UtcNow,
                ErrorMessage = original.Message,
                StackTrace = original.StackTrace
            });
        }
        catch (Exception persistEx)
        {
            logger?.LogError(
                persistEx,
                "Failed to persist exception to Errors store. Original: {OriginalMessage}",
                original.Message);
        }
    }

    [Fact]
    public async Task Errors_Create_Failure_Is_Swallowed_And_Logged_Without_Rethrow()
    {
        var factory = new CapturingLoggerFactory();
        var logger = factory.CreateLogger("GlobalExceptionHandler");
        var original = new InvalidOperationException("The incoming request has too many parameters.");

        var ex = await Record.ExceptionAsync(() =>
            PersistOrLogAsync(new ThrowingErrorsRepository(), logger, original));

        Assert.Null(ex);
        Assert.Contains(factory.Entries, e =>
            e.Level == LogLevel.Error
            && e.Message.Contains("Failed to persist", StringComparison.Ordinal)
            && e.Message.Contains("too many parameters", StringComparison.Ordinal));
    }
}
