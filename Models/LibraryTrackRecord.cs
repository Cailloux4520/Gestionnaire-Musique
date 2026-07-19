namespace MusicOrganizer.Models;

public sealed class LibraryTrackRecord
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required string Artist { get; init; }
    public required string Album { get; init; }
    public required string Title { get; init; }
    public required string Extension { get; init; }
    public required string Fingerprint { get; init; }
    public required bool HasCover { get; init; }
    public long FileSizeBytes { get; init; }
    public int TrackNumber { get; init; }
    public TimeSpan Duration { get; init; }
    public int BitrateKbps { get; init; }
}