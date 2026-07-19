namespace MusicOrganizer.Models;

/// <summary>
/// Immutable snapshot of the current processing state, sent regularly to the UI
/// thread through IProgress&lt;ProgressInfo&gt; so the form can update its labels
/// and progress bar without ever touching cross-thread state directly.
/// </summary>
public sealed class ProgressInfo
{
    public required long TotalFiles { get; init; }
    public required long FilesAnalyzed { get; init; }
    public required long FilesMoved { get; init; }
    public required long FilesIgnored { get; init; }
    public required long DuplicatesFound { get; init; }
    public required long Errors { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required double FilesPerSecond { get; init; }
    public string? CurrentFile { get; init; }
}

public enum LogSeverity
{
    Info,
    Moved,
    Duplicate,
    Ignored,
    Error,
    Summary
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogSeverity Severity { get; init; }
    public string? OldPath { get; init; }
    public string? NewPath { get; init; }
    public required string Reason { get; init; }
}
