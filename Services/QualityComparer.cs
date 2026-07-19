using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// Ranks audio files by quality using the priority order requested:
///
///   1. FLAC / ALAC / APE (lossless)
///   2. WAV / AIFF (lossless)
///   3. MP3 320 kb/s
///   4. AAC / WMA 320 kb/s
///   5. OGG Vorbis / OPUS, high quality
///   6. MP3 256 kb/s
///   7. MP3 192 kb/s
///   8. MP3 128 kb/s (or below)
///
/// Because real-world files rarely land on an exact bitrate, each rank covers a
/// realistic range (e.g. "MP3 320" covers 300 kb/s and up) so that near-320 encodes
/// aren't unfairly demoted. File size is used ONLY as the final tie-breaker when two
/// files land in the exact same rank with the exact same bitrate - never before that,
/// exactly as requested (a small FLAC must never lose to a large low-quality file).
/// </summary>
public static class QualityComparer
{
    /// <summary>
    /// Lower rank number = better quality. Used as the primary sort key.
    /// </summary>
    public static int GetRank(AudioFileInfo file)
    {
        if (file.IsLossless)
        {
            return file.Format switch
            {
                AudioFormatType.Flac or AudioFormatType.M4a or AudioFormatType.Ape => 1,
                AudioFormatType.Wav or AudioFormatType.Aiff => 2,
                _ => 2
            };
        }

        return file.Format switch
        {
            AudioFormatType.Mp3 => RankMp3(file.BitrateKbps),
            AudioFormatType.Aac or AudioFormatType.M4a or AudioFormatType.Wma => file.BitrateKbps >= 280 ? 4 : RankMp3(file.BitrateKbps),
            AudioFormatType.Ogg or AudioFormatType.Opus => file.BitrateKbps >= 192 ? 5 : RankMp3(file.BitrateKbps) + 1,
            _ => 9
        };
    }

    public static int GetRankScore(AudioFileInfo file) => 100 - GetRank(file);

    private static int RankMp3(int bitrateKbps) => bitrateKbps switch
    {
        >= 300 => 3,
        >= 240 => 6,
        >= 160 => 7,
        _ => 8
    };

    /// <summary>
    /// Returns a positive number if <paramref name="a"/> is better quality than
    /// <paramref name="b"/>, negative if worse, and 0 if they are exactly equal in
    /// quality (rank AND bitrate identical) - in which case the caller should fall
    /// back to comparing file size.
    /// </summary>
    public static int Compare(AudioFileInfo a, AudioFileInfo b)
    {
        var rankA = GetRank(a);
        var rankB = GetRank(b);

        // Lower rank number wins, so invert the natural comparison.
        if (rankA != rankB)
        {
            return rankB - rankA;
        }

        if (a.BitrateKbps != b.BitrateKbps)
        {
            return a.BitrateKbps - b.BitrateKbps;
        }

        return 0;
    }

    /// <summary>
    /// True if the two files differ enough in duration/bitrate/sample rate/channels
    /// that they are probably NOT the same recording (e.g. a live version vs studio
    /// version that happen to share a file name), and should therefore be kept
    /// side-by-side ("Title (2).mp3") rather than treated as a duplicate.
    /// </summary>
    public static bool AreLikelyDifferentTracks(AudioFileInfo a, AudioFileInfo b)
    {
        var durationDiff = Math.Abs((a.Duration - b.Duration).TotalSeconds);
        // More than 3 seconds (or >2% of total length) difference => different recording.
        var durationThreshold = Math.Max(3.0, a.Duration.TotalSeconds * 0.02);
        if (durationDiff > durationThreshold)
        {
            return true;
        }

        if (a.SampleRateHz > 0 && b.SampleRateHz > 0 && a.SampleRateHz != b.SampleRateHz)
        {
            return true;
        }

        if (a.Channels > 0 && b.Channels > 0 && a.Channels != b.Channels)
        {
            return true;
        }

        // A very large bitrate gap (e.g. 96 kb/s vs 320 kb/s) with otherwise identical
        // duration is still treated as "same track, different quality" - NOT a different
        // track - since bitrate alone is exactly what this tool is meant to arbitrate.
        return false;
    }
}
