using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// Reads audio metadata (artist, title, duration, bitrate, sample rate, channels)
/// using TagLib#. If the artist tag is missing, or reading fails, falls back to
/// parsing the file name using the "Artist - Title.ext" convention.
///
/// Only metadata is ever read - the audio stream itself is never loaded into memory,
/// keeping memory usage flat even for libraries of 100,000+ files.
/// </summary>
public static class MetadataService
{
    /// <summary>
    /// Reads metadata for a single file. Never throws: on failure it returns a best-effort
    /// AudioFileInfo built purely from the file name/size so the pipeline can keep going.
    /// </summary>
    public static Task<AudioFileInfo> ReadAsync(string path, bool useTagsFirst, CancellationToken token)
    {
        return Task.Run(() => Read(path, useTagsFirst), token);
    }

    private static AudioFileInfo Read(string path, bool useTagsFirst)
    {
        var fileInfo = new FileInfo(path);
        var extension = fileInfo.Extension;
        AudioFormatExtensions.TryGetFormat(extension, out var format);

        string tagArtist = string.Empty;
        string tagTitle = string.Empty;
        string tagAlbum = string.Empty;
        string tagGenre = string.Empty;
        int trackNumber = 0;
        int trackCount = 0;
        int year = 0;
        bool hasEmbeddedCover = false;
        TimeSpan duration = TimeSpan.Zero;
        int bitrate = 0;
        int sampleRate = 0;
        int channels = 0;
        bool isLossless = format is AudioFormatType.Flac or AudioFormatType.Wav
            or AudioFormatType.Aiff or AudioFormatType.Ape;
        bool tagReadOk = false;

        try
        {
            using var tagFile = TagLib.File.Create(path);

            tagArtist = tagFile.Tag.FirstPerformer ?? string.Empty;
            tagTitle = tagFile.Tag.Title ?? string.Empty;
            tagAlbum = tagFile.Tag.Album ?? string.Empty;
            tagGenre = tagFile.Tag.FirstGenre ?? string.Empty;
            trackNumber = checked((int)tagFile.Tag.Track);
            trackCount = checked((int)tagFile.Tag.TrackCount);
            year = checked((int)tagFile.Tag.Year);
            hasEmbeddedCover = tagFile.Tag.Pictures.Length > 0;
            duration = tagFile.Properties.Duration;
            bitrate = tagFile.Properties.AudioBitrate;
            sampleRate = tagFile.Properties.AudioSampleRate;
            channels = tagFile.Properties.AudioChannels;

            // M4A can contain either lossy AAC or lossless ALAC - TagLib# exposes the
            // codec description, which we use to refine the lossless guess.
            if (format == AudioFormatType.M4a)
            {
                var codecDescription = string.Join(" ", tagFile.Properties.Codecs.Select(c => c.Description));
                if (codecDescription.Contains("ALAC", StringComparison.OrdinalIgnoreCase) ||
                    codecDescription.Contains("Apple Lossless", StringComparison.OrdinalIgnoreCase))
                {
                    isLossless = true;
                }
            }

            tagReadOk = true;
        }
        catch
        {
            // Corrupt/unsupported tag data - we will fall back to the file name below.
        }

        string artist;
        string title;
        bool artistFromTag;

        var fileNameArtist = ParseArtistFromFileName(fileInfo.Name, out var fileNameTitle);

        if (useTagsFirst && tagReadOk && !string.IsNullOrWhiteSpace(tagArtist))
        {
            artist = tagArtist.Trim();
            title = !string.IsNullOrWhiteSpace(tagTitle) ? tagTitle.Trim() : fileNameTitle;
            artistFromTag = true;
        }
        else
        {
            artist = fileNameArtist;
            title = fileNameTitle;
            artistFromTag = false;
        }

        if (string.IsNullOrWhiteSpace(artist))
        {
            artist = "Inconnu";
        }

        return new AudioFileInfo
        {
            FullPath = path,
            FileName = fileInfo.Name,
            Extension = extension,
            Format = format,
            FileSizeBytes = fileInfo.Length,
            Artist = artist,
            Album = tagAlbum.Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileInfo.Name) : title,
            Genre = tagGenre.Trim(),
            TrackNumber = trackNumber,
            TrackCount = trackCount,
            Year = year,
            HasEmbeddedCover = hasEmbeddedCover,
            ArtistFromTag = artistFromTag,
            HasArtistTag = tagReadOk && !string.IsNullOrWhiteSpace(tagArtist),
            HasTitleTag = tagReadOk && !string.IsNullOrWhiteSpace(tagTitle),
            Duration = duration,
            BitrateKbps = bitrate,
            SampleRateHz = sampleRate,
            Channels = channels,
            IsLossless = isLossless
        };
    }

    /// <summary>
    /// Parses "Artist - Title.ext" file names. The artist is always the part before the
    /// FIRST " - " separator. If no separator is found, the whole name (minus extension)
    /// is treated as the title and the artist is left empty for the caller to default.
    /// </summary>
    public static string ParseArtistFromFileName(string fileName, out string title)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var separatorIndex = nameWithoutExt.IndexOf(" - ", StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            title = nameWithoutExt.Trim();
            return string.Empty;
        }

        var artist = nameWithoutExt[..separatorIndex].Trim();
        title = nameWithoutExt[(separatorIndex + 3)..].Trim();
        return artist;
    }

    public static bool WriteCorrectedTags(AudioFileInfo info)
    {
        try
        {
            using var tagFile = TagLib.File.Create(info.FullPath);
            var changed = false;

            if (string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) && !string.IsNullOrWhiteSpace(info.Artist))
            {
                tagFile.Tag.Performers = [info.Artist];
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(tagFile.Tag.Title) && !string.IsNullOrWhiteSpace(info.Title))
            {
                tagFile.Tag.Title = info.Title;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(info.Album) && string.IsNullOrWhiteSpace(tagFile.Tag.Album))
            {
                tagFile.Tag.Album = info.Album;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            tagFile.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool WriteYearTag(string path, int year)
    {
        if (year <= 0)
        {
            return false;
        }

        try
        {
            using var tagFile = TagLib.File.Create(path);
            if (tagFile.Tag.Year == year)
            {
                return false;
            }

            tagFile.Tag.Year = checked((uint)year);
            tagFile.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool WriteArtistAndTitleTags(string path, string artist, string title)
    {
        try
        {
            using var tagFile = TagLib.File.Create(path);
            var changed = false;

            if (!string.IsNullOrWhiteSpace(artist) && !string.Equals(tagFile.Tag.FirstPerformer, artist, StringComparison.Ordinal))
            {
                tagFile.Tag.Performers = [artist];
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(title) && !string.Equals(tagFile.Tag.Title, title, StringComparison.Ordinal))
            {
                tagFile.Tag.Title = title;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            tagFile.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool WriteGenreTag(string path, string genre)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return false;
        }

        try
        {
            using var tagFile = TagLib.File.Create(path);
            var currentGenre = tagFile.Tag.FirstGenre ?? string.Empty;
            if (string.Equals(currentGenre.Trim(), genre.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            tagFile.Tag.Genres = [genre.Trim()];
            tagFile.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
