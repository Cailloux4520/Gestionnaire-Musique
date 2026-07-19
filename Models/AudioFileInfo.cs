namespace MusicOrganizer.Models;

/// <summary>
/// Holds every piece of information MusicOrganizer needs about a single audio file:
/// its metadata (read from tags, or inferred from the file name) and its technical
/// audio properties, used later for duplicate / quality comparison.
/// </summary>
public sealed class AudioFileInfo
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required AudioFormatType Format { get; init; }
    public required long FileSizeBytes { get; init; }

    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int TrackCount { get; set; }
    public int Year { get; set; }
    public bool HasEmbeddedCover { get; set; }
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>True if the artist/title came from tags rather than the file name.</summary>
    public bool ArtistFromTag { get; set; }
    public bool HasArtistTag { get; set; }
    public bool HasTitleTag { get; set; }
    public bool PrimaryArtistWasExtracted { get; set; }

    public TimeSpan Duration { get; set; }
    public int BitrateKbps { get; set; }
    public int SampleRateHz { get; set; }
    public int Channels { get; set; }

    /// <summary>
    /// Whether the underlying codec is lossless (FLAC, ALAC-in-M4A, WAV, AIFF, APE).
    /// Filled in by MetadataService since it requires inspecting codec details.
    /// </summary>
    public bool IsLossless { get; set; }
}
