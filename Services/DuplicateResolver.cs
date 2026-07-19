using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public enum DuplicateDecision
{
    /// <summary>Same track: the existing file is equal or better quality - discard the incoming one.</summary>
    KeepExisting,

    /// <summary>Same track: the incoming file is better quality - replace the existing one.</summary>
    ReplaceExisting,

    /// <summary>Same track, identical quality and identical size - a perfect duplicate.
    /// The incoming file is discarded, exactly like KeepExisting, but logged distinctly.</summary>
    PerfectDuplicate
}

/// <summary>
/// Implements the duplicate-handling rules:
///   1. When two files target the same final file name, keep the better-quality file.
///   2. If quality is exactly tied, file size is used as the final tie-breaker.
///   3. If everything is identical, it's a perfect duplicate - the incoming copy is discarded.
/// The loser is always sent to the Recycle Bin, never permanently deleted.
/// </summary>
public static class DuplicateResolver
{
    public static DuplicateDecision Resolve(AudioFileInfo incoming, AudioFileInfo existing)
    {
        var qualityCompare = QualityComparer.Compare(incoming, existing);

        if (qualityCompare > 0)
        {
            return DuplicateDecision.ReplaceExisting;
        }

        if (qualityCompare < 0)
        {
            return DuplicateDecision.KeepExisting;
        }

        // Exact quality tie - fall back to file size (bigger wins).
        if (incoming.FileSizeBytes > existing.FileSizeBytes)
        {
            return DuplicateDecision.ReplaceExisting;
        }

        if (incoming.FileSizeBytes < existing.FileSizeBytes)
        {
            return DuplicateDecision.KeepExisting;
        }

        return DuplicateDecision.PerfectDuplicate;
    }
}
