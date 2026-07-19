namespace MusicOrganizer.Models;

/// <summary>
/// All audio formats supported by MusicOrganizer.
/// </summary>
public enum AudioFormatType
{
    Unknown,
    Mp3,
    Flac,
    M4a,
    Aac,
    Ogg,
    Opus,
    Wav,
    Aiff,
    Ape,
    Wma
}

public static class AudioFormatExtensions
{
    private static readonly Dictionary<string, AudioFormatType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = AudioFormatType.Mp3,
        [".flac"] = AudioFormatType.Flac,
        [".m4a"] = AudioFormatType.M4a,
        [".aac"] = AudioFormatType.Aac,
        [".ogg"] = AudioFormatType.Ogg,
        [".opus"] = AudioFormatType.Opus,
        [".wav"] = AudioFormatType.Wav,
        [".aiff"] = AudioFormatType.Aiff,
        [".aif"] = AudioFormatType.Aiff,
        [".ape"] = AudioFormatType.Ape,
        [".wma"] = AudioFormatType.Wma,
    };

    public static IReadOnlyCollection<string> SupportedExtensions => ExtensionMap.Keys;

    public static bool TryGetFormat(string extension, out AudioFormatType format)
        => ExtensionMap.TryGetValue(extension, out format);

    public static bool IsSupported(string extension) => ExtensionMap.ContainsKey(extension);
}
