using System.Collections.Concurrent;
using System.Text;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// Central, thread-safe logging service. Multiple worker tasks can call
/// <see cref="Log"/> concurrently; entries are queued and flushed to disk in
/// batches by a single background writer, and also surfaced to the UI in
/// real time via <see cref="EntryLogged"/>.
/// </summary>
public sealed class LoggingService : IDisposable
{
    private readonly ConcurrentQueue<LogEntry> _pendingWrite = new();
    private readonly string _logFilePath;
    private readonly bool _writeToFile;
    private readonly bool _errorsOnlyToUi;
    private readonly SemaphoreSlim _flushSignal = new(0);
    private readonly CancellationTokenSource _writerCts = new();
    private readonly Task? _writerTask;

    public event Action<LogEntry>? EntryLogged;

    public LoggingService(string logFilePath, bool writeToFile, bool errorsOnlyToUi)
    {
        _logFilePath = logFilePath;
        _writeToFile = writeToFile;
        _errorsOnlyToUi = errorsOnlyToUi;
        _writerTask = writeToFile ? Task.Run(WriterLoopAsync) : null;
    }

    public void Log(LogSeverity severity, string reason, string? oldPath = null, string? newPath = null)
    {
        var entry = new LogEntry
        {
            Severity = severity,
            Reason = reason,
            OldPath = oldPath,
            NewPath = newPath
        };

        if (!_errorsOnlyToUi || severity is LogSeverity.Error or LogSeverity.Summary)
        {
            EntryLogged?.Invoke(entry);
        }

        if (_writeToFile)
        {
            _pendingWrite.Enqueue(entry);
            _flushSignal.Release();
        }
    }

    private async Task WriterLoopAsync()
    {
        try
        {
            await using var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };

            while (!_writerCts.IsCancellationRequested)
            {
                await _flushSignal.WaitAsync(TimeSpan.FromMilliseconds(500), _writerCts.Token).ConfigureAwait(false);

                var wroteAny = false;
                while (_pendingWrite.TryDequeue(out var entry))
                {
                    await writer.WriteLineAsync(FormatLine(entry)).ConfigureAwait(false);
                    wroteAny = true;
                }

                if (wroteAny)
                {
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }

            // Final drain after cancellation.
            while (_pendingWrite.TryDequeue(out var entry))
            {
                await writer.WriteLineAsync(FormatLine(entry)).ConfigureAwait(false);
            }
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch
        {
            // The log is best-effort: a logging failure must never crash the application.
        }
    }

    private static string FormatLine(LogEntry entry)
    {
        var parts = new List<string>
        {
            entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            entry.Severity.ToString().ToUpperInvariant()
        };

        if (!string.IsNullOrEmpty(entry.OldPath)) parts.Add($"Ancien: {entry.OldPath}");
        if (!string.IsNullOrEmpty(entry.NewPath)) parts.Add($"Nouveau: {entry.NewPath}");
        parts.Add(entry.Reason);

        return string.Join(" | ", parts);
    }

    public async Task FlushAndCloseAsync()
    {
        _writerCts.Cancel();
        if (_writerTask is null)
        {
            return;
        }

        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch
        {
            // Ignore - shutting down regardless.
        }
    }

    public void Dispose()
    {
        _writerCts.Cancel();
        _flushSignal.Dispose();
        _writerCts.Dispose();
    }
}
