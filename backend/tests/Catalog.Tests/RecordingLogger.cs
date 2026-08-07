using Microsoft.Extensions.Logging;

namespace Nestly.Catalog.Tests;

/// <summary>One log call, already rendered the way a sink would render it.</summary>
public sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was told, so a
/// test can assert on diagnostics as a behaviour rather than as a side effect -
/// in particular that a credential never reaches the log stream.
/// </summary>
/// <remarks>
/// <see cref="Text"/> deliberately includes the exception, because "the key is
/// not in the message" is no protection at all if the key is in the stack
/// trace or the exception's own message instead.
/// </remarks>
public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<RecordedLogEntry> Entries { get; } = [];

    /// <summary>Everything logged so far, messages and exceptions together, as one searchable string.</summary>
    public string Text => string.Join(
        Environment.NewLine,
        Entries.Select(entry => entry.Exception is null ? entry.Message : $"{entry.Message} {entry.Exception}"));

    public IEnumerable<RecordedLogEntry> At(LogLevel level) => Entries.Where(entry => entry.Level == level);

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
    }
}
