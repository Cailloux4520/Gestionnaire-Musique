using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public static class FileNamingService
{
    public static string BuildFileName(AudioFileInfo info, OrganizerSettings settings)
    {
        var artist = FileNameSanitizer.SanitizeFileName(info.Artist);
        var title = FileNameSanitizer.SanitizeFileName(info.Title);

        if (string.IsNullOrWhiteSpace(title) || string.Equals(title, "Sans titre", StringComparison.OrdinalIgnoreCase))
        {
            title = FileNameSanitizer.SanitizeFileName(Path.GetFileNameWithoutExtension(info.FileName));
        }

        return $"{artist} - {title}{info.Extension}";
    }
}