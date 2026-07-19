using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public static class MusicBrainzService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RequestGate = new(1, 1);

    public static async Task<bool> TryCompleteMissingMetadataAsync(AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        if (!settings.FetchMusicBrainzMetadata || string.IsNullOrWhiteSpace(info.Title))
        {
            return false;
        }

        try
        {
            var server = string.IsNullOrWhiteSpace(settings.MusicBrainzServer)
                ? "https://musicbrainz.org"
                : settings.MusicBrainzServer.TrimEnd('/');
            var query = Uri.EscapeDataString($"recording:\"{info.Title}\" artist:\"{info.Artist}\"");
            var url = $"{server}/ws/2/recording/?query={query}&fmt=json&limit=1";
            using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
            {
                return false;
            }

            var recording = recordings[0];
            var changed = false;
            if (string.IsNullOrWhiteSpace(info.Title) && recording.TryGetProperty("title", out var title))
            {
                info.Title = title.GetString() ?? info.Title;
                changed = true;
            }

            if (recording.TryGetProperty("artist-credit", out var artists) && artists.GetArrayLength() > 0)
            {
                var name = artists[0].TryGetProperty("name", out var artistName) ? artistName.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && (string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"))
                {
                    info.Artist = name;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(info.Album) && recording.TryGetProperty("releases", out var releases) && releases.GetArrayLength() > 0)
            {
                var release = releases[0];
                if (release.TryGetProperty("title", out var album))
                {
                    info.Album = album.GetString() ?? info.Album;
                    changed = true;
                }
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<int?> TryFindOriginalYearAsync(AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(info.Title))
        {
            return null;
        }

        try
        {
            var server = string.IsNullOrWhiteSpace(settings.MusicBrainzServer)
                ? "https://musicbrainz.org"
                : settings.MusicBrainzServer.TrimEnd('/');
            var queryText = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
                ? $"recording:\"{info.Title}\""
                : $"recording:\"{info.Title}\" artist:\"{info.Artist}\"";
            var query = Uri.EscapeDataString(queryText);
            var url = $"{server}/ws/2/recording/?query={query}&fmt=json&limit=5";

            using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
            {
                return null;
            }

            var firstReleaseYears = new List<int>();
            var nonCompilationReleaseYears = new List<int>();
            foreach (var recording in recordings.EnumerateArray())
            {
                if (recording.TryGetProperty("first-release-date", out var firstReleaseDate) && TryParseYear(firstReleaseDate.GetString(), out var firstYear))
                {
                    firstReleaseYears.Add(firstYear);
                }

                if (recording.TryGetProperty("releases", out var releases))
                {
                    foreach (var release in releases.EnumerateArray())
                    {
                        if (IsCompilationRelease(release))
                        {
                            continue;
                        }

                        if (release.TryGetProperty("date", out var date) && TryParseYear(date.GetString(), out var releaseYear))
                        {
                            nonCompilationReleaseYears.Add(releaseYear);
                        }
                    }
                }
            }

            if (firstReleaseYears.Count > 0)
            {
                return firstReleaseYears.Min();
            }

            return nonCompilationReleaseYears.Count == 0 ? null : nonCompilationReleaseYears.Min();
        }
        catch
        {
            return null;
        }
    }

    public static async Task<int?> TryFindOriginalReleaseYearAsync(AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(info.Title))
        {
            return null;
        }

        await RequestGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var server = string.IsNullOrWhiteSpace(settings.MusicBrainzServer)
                ? "https://musicbrainz.org"
                : settings.MusicBrainzServer.TrimEnd('/');
            var strictQueryText = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
                ? $"recording:\"{info.Title}\""
                : $"recording:\"{info.Title}\" artist:\"{info.Artist}\"";
            var queryTexts = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
                ? new[] { strictQueryText }
                : new[] { strictQueryText, $"recording:\"{info.Title}\"" };

            var releaseYears = new List<ReleaseYearCandidate>();
            foreach (var queryText in queryTexts)
            {
                var query = Uri.EscapeDataString(queryText);
                var url = $"{server}/ws/2/recording/?query={query}&fmt=json&limit=10";

                using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
                CollectReleaseYearCandidates(document, info, releaseYears);
                if (releaseYears.Count > 0)
                {
                    break;
                }

                await Task.Delay(1100, token).ConfigureAwait(false);
            }

            try
            {
                await CollectITunesReleaseYearCandidatesAsync(info, releaseYears, token).ConfigureAwait(false);
            }
            catch
            {
                // Keep MusicBrainz candidates if the secondary internet check fails.
            }

            if (releaseYears.Count == 0)
            {
                return null;
            }

            return releaseYears
                .OrderBy(candidate => candidate.Year)
                .ThenByDescending(candidate => candidate.TrustedFirstReleaseDate)
                .Select(candidate => candidate.Year)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                await Task.Delay(1100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is handled by the caller.
            }

            RequestGate.Release();
        }
    }

    public static async Task<string?> TryFindPrimaryGenreAsync(AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        if (!settings.FetchMusicBrainzMetadata || string.IsNullOrWhiteSpace(info.Title))
        {
            return null;
        }

        await RequestGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var server = string.IsNullOrWhiteSpace(settings.MusicBrainzServer)
                ? "https://musicbrainz.org"
                : settings.MusicBrainzServer.TrimEnd('/');
            var queryText = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
                ? $"recording:\"{info.Title}\""
                : $"recording:\"{info.Title}\" artist:\"{info.Artist}\"";
            var query = Uri.EscapeDataString(queryText);
            var url = $"{server}/ws/2/recording/?query={query}&fmt=json&limit=10&inc=tags+artist-credits";

            using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
                var musicBrainzGenre = FindBestMusicBrainzTag(document, info);
                if (!string.IsNullOrWhiteSpace(musicBrainzGenre))
                {
                    return musicBrainzGenre;
                }
            }

            return await TryFindITunesPrimaryGenreAsync(info, token).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                await Task.Delay(1100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is handled by the caller.
            }

            RequestGate.Release();
        }
    }

    private static string? FindBestMusicBrainzTag(JsonDocument document, AudioFileInfo info)
    {
        if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
        {
            return null;
        }

        var tags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var recording in recordings.EnumerateArray())
        {
            if (!IsMatchingRecording(recording, info) || !recording.TryGetProperty("tags", out var recordingTags))
            {
                continue;
            }

            foreach (var tag in recordingTags.EnumerateArray())
            {
                var name = tag.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || IsNoiseGenreTag(name))
                {
                    continue;
                }

                var count = tag.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var parsedCount)
                    ? parsedCount
                    : 1;
                tags[name.Trim()] = tags.TryGetValue(name.Trim(), out var existingCount) ? existingCount + count : count;
            }
        }

        return tags.Count == 0
            ? null
            : NormalizeGenre(tags.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First().Key);
    }

    private static async Task<string?> TryFindITunesPrimaryGenreAsync(AudioFileInfo info, CancellationToken token)
    {
        var term = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
            ? info.Title
            : $"{info.Artist} {info.Title}";
        var query = Uri.EscapeDataString(term);
        var url = $"https://itunes.apple.com/search?term={query}&media=music&entity=song&limit=20";

        using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var result in results.EnumerateArray())
        {
            var trackName = result.TryGetProperty("trackName", out var trackNameElement) ? trackNameElement.GetString() : null;
            var artistName = result.TryGetProperty("artistName", out var artistNameElement) ? artistNameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(trackName)
                || string.IsNullOrWhiteSpace(artistName)
                || !AreComparableNamesCompatible(trackName, info.Title)
                || (!string.IsNullOrWhiteSpace(info.Artist) && info.Artist != "Inconnu" && !AreArtistNamesCompatible(artistName, info.Artist)))
            {
                continue;
            }

            if (result.TryGetProperty("primaryGenreName", out var genreElement))
            {
                var genre = NormalizeGenre(genreElement.GetString() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    return genre;
                }
            }
        }

        return null;
    }

    private static bool IsNoiseGenreTag(string value)
    {
        var normalized = NormalizeComparableName(value);
        return normalized.Length < 3
            || normalized is "seenlive" or "favorites" or "favourite" or "favorite" or "spotify" or "lastfm" or "femalevocalists";
    }

    private static string NormalizeGenre(string value)
    {
        var primary = Regex.Split(value, @"\s*(?:/|,|;|\+|&| and | et )\s*", RegexOptions.IgnoreCase)
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.Length > 0) ?? string.Empty;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(primary.ToLowerInvariant());
    }

    private static void CollectReleaseYearCandidates(JsonDocument document, AudioFileInfo info, List<ReleaseYearCandidate> releaseYears)
    {
        if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
        {
            return;
        }

            foreach (var recording in recordings.EnumerateArray())
            {
                if (!IsMatchingRecording(recording, info))
                {
                    continue;
                }

                if (recording.TryGetProperty("first-release-date", out var firstReleaseDate)
                    && TryParseYear(firstReleaseDate.GetString(), out var firstYear))
                {
                    releaseYears.Add(new ReleaseYearCandidate(firstYear, TrustedFirstReleaseDate: true));
                }

                if (recording.TryGetProperty("releases", out var releases))
                {
                    foreach (var release in releases.EnumerateArray())
                    {
                        if (IsCompilationRelease(release))
                        {
                            continue;
                        }

                        if (release.TryGetProperty("date", out var date)
                            && TryParseYear(date.GetString(), out var releaseYear))
                        {
                            releaseYears.Add(new ReleaseYearCandidate(releaseYear, TrustedFirstReleaseDate: false));
                        }
                    }
                }
            }
    }

    private static async Task CollectITunesReleaseYearCandidatesAsync(AudioFileInfo info, List<ReleaseYearCandidate> releaseYears, CancellationToken token)
    {
        var term = string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu"
            ? info.Title
            : $"{info.Artist} {info.Title}";
        var query = Uri.EscapeDataString(term);
        var url = $"https://itunes.apple.com/search?term={query}&media=music&entity=song&limit=20";

        using var response = await Client.GetAsync(url, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return;
        }

        foreach (var result in results.EnumerateArray())
        {
            var trackName = result.TryGetProperty("trackName", out var trackNameElement) ? trackNameElement.GetString() : null;
            var artistName = result.TryGetProperty("artistName", out var artistNameElement) ? artistNameElement.GetString() : null;
            var collectionName = result.TryGetProperty("collectionName", out var collectionNameElement) ? collectionNameElement.GetString() : string.Empty;
            if (string.IsNullOrWhiteSpace(trackName)
                || string.IsNullOrWhiteSpace(artistName)
                || !AreComparableNamesCompatible(trackName, info.Title)
                || (!string.IsNullOrWhiteSpace(info.Artist) && info.Artist != "Inconnu" && !AreArtistNamesCompatible(artistName, info.Artist))
                || IsCompilationTitle(collectionName))
            {
                continue;
            }

            if (result.TryGetProperty("releaseDate", out var releaseDate)
                && TryParseYear(releaseDate.GetString(), out var year))
            {
                releaseYears.Add(new ReleaseYearCandidate(year, TrustedFirstReleaseDate: false));
            }
        }
    }

    private static bool IsCompilationRelease(JsonElement release)
    {
        if (release.TryGetProperty("secondary-types", out var secondaryTypes))
        {
            foreach (var secondaryType in secondaryTypes.EnumerateArray())
            {
                if (string.Equals(secondaryType.GetString(), "Compilation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (release.TryGetProperty("title", out var title))
        {
            return IsCompilationTitle(title.GetString() ?? string.Empty);
        }

        return false;
    }

    private static bool IsCompilationTitle(string? title)
    {
        var releaseTitle = title ?? string.Empty;
        return releaseTitle.Contains("best of", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("greatest hits", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("anthology", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("collection", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("compilation", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("hits", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("essential", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("ultimate", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("remaster", StringComparison.OrdinalIgnoreCase)
            || releaseTitle.Contains("anniversary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMatchingRecording(JsonElement recording, AudioFileInfo info)
    {
        if (!recording.TryGetProperty("title", out var titleElement))
        {
            return false;
        }

        var recordingTitle = titleElement.GetString() ?? string.Empty;
        if (!AreComparableNamesCompatible(recordingTitle, info.Title))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(info.Artist) || info.Artist == "Inconnu")
        {
            return true;
        }

        if (!recording.TryGetProperty("artist-credit", out var artists))
        {
            return false;
        }

        foreach (var artist in artists.EnumerateArray())
        {
            var name = artist.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (AreArtistNamesCompatible(name ?? string.Empty, info.Artist))
            {
                return true;
            }

            if (artist.TryGetProperty("artist", out var artistInfo)
                && artistInfo.TryGetProperty("name", out var canonicalName)
                && AreArtistNamesCompatible(canonicalName.GetString() ?? string.Empty, info.Artist))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseYear(string? value, out int year)
    {
        year = 0;
        return !string.IsNullOrWhiteSpace(value)
            && value.Length >= 4
            && int.TryParse(value[..4], out year)
            && year is >= 1877 and <= 2100;
    }

    private static bool AreComparableNamesCompatible(string left, string right)
    {
        var normalizedLeft = NormalizeComparableName(left);
        var normalizedRight = NormalizeComparableName(right);
        if (normalizedLeft.Length < 3 || normalizedRight.Length < 3)
        {
            return normalizedLeft == normalizedRight;
        }

        return normalizedLeft == normalizedRight
            || normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal)
            || normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal);
    }

    private static bool AreArtistNamesCompatible(string left, string right)
    {
        var normalizedLeft = NormalizeComparableName(left);
        var normalizedRight = NormalizeComparableName(right);
        if (normalizedLeft == normalizedRight)
        {
            return true;
        }

        var rightParts = Regex.Split(right, @"\s*(?:&|,|;|/|\+|\bfeat\.?\b|\bfeaturing\b|\bft\.?\b|\band\b)\s*", RegexOptions.IgnoreCase)
            .Select(NormalizeComparableName)
            .Where(part => part.Length >= 3);

        return rightParts.Any(part => normalizedLeft == part || normalizedLeft.Contains(part, StringComparison.Ordinal));
    }

    private static string NormalizeComparableName(string value)
    {
        value = Regex.Replace(value, @"\([^)]*\)|\[[^\]]*\]", " ");
        value = Regex.Replace(value, @"\b(radio edit|extended mix|club mix|original mix|remaster(?:ed)?|version|edit|mix|single version|album version)\b", " ", RegexOptions.IgnoreCase);
        value = value.Normalize(NormalizationForm.FormD);

        var chars = value
            .ToLowerInvariant()
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private sealed record ReleaseYearCandidate(int Year, bool TrustedFirstReleaseDate);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GestionnaireMusique", "1.0"));
        return client;
    }
}