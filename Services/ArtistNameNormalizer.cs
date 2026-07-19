using System.Text.RegularExpressions;

namespace MusicOrganizer.Services;

public static partial class ArtistNameNormalizer
{
    private static readonly Dictionary<string, string> KnownArtists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acdc"] = "AC/DC",
        ["ac dc"] = "AC/DC",
        ["guns n roses"] = "Guns N' Roses",
        ["guns and roses"] = "Guns N' Roses",
        ["the beatles"] = "The Beatles"
    };

    public static string Normalize(string artist)
    {
        var cleaned = CollapseSpaces(artist).Trim();
        if (cleaned.Length == 0)
        {
            return "Inconnu";
        }

        var key = cleaned.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("&", "and", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (KnownArtists.TryGetValue(key, out var known))
        {
            return FileNameSanitizer.SanitizeFolderName(known);
        }

        return FileNameSanitizer.SanitizeFolderName(ToTitleCasePreservingAcronyms(cleaned));
    }

    public static string ExtractPrimaryArtist(string artist)
    {
        var cleaned = CollapseSpaces(artist).Trim();
        if (cleaned.Length == 0)
        {
            return "Inconnu";
        }

        var parts = PrimaryArtistSeparatorRegex().Split(cleaned);
        var primary = parts.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part))?.Trim();
        return FileNameSanitizer.SanitizeFolderName(string.IsNullOrWhiteSpace(primary) ? cleaned : primary);
    }

    public static string NormalizeTitle(string text)
    {
        var cleaned = CollapseSpaces(text).Trim();
        cleaned = cleaned.Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("’", string.Empty, StringComparison.Ordinal);
        return cleaned.Length == 0 ? cleaned : ToTitleCasePreservingAcronyms(cleaned);
    }

    private static string CollapseSpaces(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string ToTitleCasePreservingAcronyms(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (word.Length <= 1 || word.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                continue;
            }

            var chars = word.ToLowerInvariant().ToCharArray();
            for (var charIndex = 0; charIndex < chars.Length; charIndex++)
            {
                if (char.IsLetter(chars[charIndex]))
                {
                    chars[charIndex] = char.ToUpperInvariant(chars[charIndex]);
                    break;
                }
            }

            words[i] = new string(chars);
        }

        return string.Join(' ', words);
    }

    [GeneratedRegex(@"\s*(?:,|;|&|\+|/|\bfeat\.?\b|\bfeaturing\b|\bft\.?\b|\bwith\b|\ben duo\b|\bduo avec\b|\band\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PrimaryArtistSeparatorRegex();
}