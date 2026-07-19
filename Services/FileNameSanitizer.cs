using System.Text;
using System.Text.RegularExpressions;

namespace MusicOrganizer.Services;

/// <summary>
/// Cleans up artist names before they are used as folder names: strips characters
/// forbidden on Windows, collapses repeated spaces, and trims the result.
/// </summary>
public static partial class FileNameSanitizer
{
    private static readonly char[] ForbiddenChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '='];

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    public static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Inconnu";
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (Array.IndexOf(ForbiddenChars, c) >= 0)
            {
                continue;
            }
            builder.Append(c);
        }

        var cleaned = MultipleSpacesRegex().Replace(builder.ToString(), " ").Trim();

        // Windows folder names cannot end with a dot or a space, and cannot be empty.
        cleaned = cleaned.TrimEnd('.', ' ');

        return string.IsNullOrWhiteSpace(cleaned) ? "Inconnu" : cleaned;
    }

    public static string SanitizeFileName(string name)
    {
        var cleaned = SanitizeFolderName(name);
        return string.IsNullOrWhiteSpace(cleaned) ? "Sans titre" : cleaned;
    }
}
