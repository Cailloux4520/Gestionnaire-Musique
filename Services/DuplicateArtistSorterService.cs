using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public sealed partial class DuplicateArtistSorterService
{
    private readonly LoggingService _logger;

    public DuplicateArtistSorterService(LoggingService logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(OrganizerSettings settings, OrganizerStats stats, IProgress<ProgressInfo> progress, CancellationToken token)
    {
        var rootFolder = settings.SourceFolder;
        stats.Stopwatch.Start();
        EmptyFolderCleanupService.DeleteFoldersWithoutMusic(rootFolder, settings, stats, _logger, includeRoot: true);
        var files = EnumerateSupportedFiles(rootFolder, settings.Recursive).ToList();
        stats.TotalFilesFound = files.Count;

        var tracks = new List<AudioFileInfo>(files.Count);
        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested();
            var info = await MetadataService.ReadAsync(path, settings.UseTagsFirst, token).ConfigureAwait(false);
            VerifyAndCorrectTags(info, settings, stats);
            tracks.Add(info);
            stats.IncrementAnalyzed();
            progress.Report(BuildProgress(stats, path));
        }

        SortNumberedFileDuplicateGroups(tracks, settings, stats);

        foreach (var group in tracks.Where(i => File.Exists(i.FullPath)).GroupBy(GetDuplicateKey).Where(g => g.Count() > 1))
        {
            token.ThrowIfCancellationRequested();
            SortDuplicateGroup(group.ToList(), settings, stats);
        }

        if (settings.UseFingerprintDuplicates)
        {
            await SortFingerprintDuplicateGroupsAsync(tracks, settings, stats, token).ConfigureAwait(false);
        }

        var remainingTracks = EnumerateSupportedFiles(rootFolder, settings.Recursive)
            .Select(path => MetadataService.ReadAsync(path, settings.UseTagsFirst, token).GetAwaiter().GetResult())
            .ToList();
        SortNumberedFileDuplicateGroups(remainingTracks, settings, stats);

        foreach (var group in remainingTracks.GroupBy(GetDuplicateKey).Where(g => g.Count() > 1))
        {
            token.ThrowIfCancellationRequested();
            SortDuplicateGroup(group.ToList(), settings, stats);
        }

        DeleteEmptyFolders(rootFolder, settings, stats);
        stats.Stopwatch.Stop();
        progress.Report(BuildProgress(stats, null));
    }

    private void VerifyAndCorrectTags(AudioFileInfo info, OrganizerSettings settings, OrganizerStats stats)
    {
        if (!settings.FixTags)
        {
            return;
        }

        var fileArtist = MetadataService.ParseArtistFromFileName(info.FileName, out var fileTitle);
        var correctedArtist = string.IsNullOrWhiteSpace(fileArtist) ? info.Artist : fileArtist;
        var correctedTitle = string.IsNullOrWhiteSpace(fileTitle) ? info.Title : fileTitle;

        if (settings.KeepPrimaryArtistOnly)
        {
            correctedArtist = GetPrimaryArtist(correctedArtist);
        }

        if (settings.NormalizeArtists)
        {
            correctedArtist = ArtistNameNormalizer.Normalize(correctedArtist);
            correctedTitle = ArtistNameNormalizer.NormalizeTitle(correctedTitle);
        }

        if (!info.HasArtistTag || !info.HasTitleTag
            || !string.Equals(info.Artist, correctedArtist, StringComparison.Ordinal)
            || !string.Equals(info.Title, correctedTitle, StringComparison.Ordinal))
        {
            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Info, $"Simulation: corriger les tags artiste/titre vers {correctedArtist} - {correctedTitle}.", oldPath: info.FullPath);
                return;
            }

            if (MetadataService.WriteArtistAndTitleTags(info.FullPath, correctedArtist, correctedTitle))
            {
                info.Artist = correctedArtist;
                info.Title = correctedTitle;
                stats.IncrementTagsFixed();
                _logger.Log(LogSeverity.Info, "Tags artiste/titre corrigés d'après le morceau; année et style conservés.", oldPath: info.FullPath);
                return;
            }

            stats.IncrementIgnored();
            _logger.Log(LogSeverity.Ignored, "Tags artiste/titre contrôlés mais non modifiés.", oldPath: info.FullPath);
        }
    }

    private async Task SortFingerprintDuplicateGroupsAsync(List<AudioFileInfo> tracks, OrganizerSettings settings, OrganizerStats stats, CancellationToken token)
    {
        foreach (var info in tracks.Where(i => File.Exists(i.FullPath)))
        {
            token.ThrowIfCancellationRequested();
            info.Fingerprint = await AudioFingerprintService.ComputeAsync(info.FullPath, token).ConfigureAwait(false);
        }

        foreach (var group in tracks.Where(i => File.Exists(i.FullPath) && !string.IsNullOrWhiteSpace(i.Fingerprint)).GroupBy(i => i.Fingerprint).Where(g => g.Count() > 1))
        {
            token.ThrowIfCancellationRequested();
            SortDuplicateGroup(group.ToList(), settings, stats);
        }
    }

    private void SortDuplicateGroup(List<AudioFileInfo> group, OrganizerSettings settings, OrganizerStats stats)
    {
        var best = group.OrderByDescending(QualityComparer.GetRankScore)
            .ThenByDescending(i => i.BitrateKbps)
            .ThenByDescending(i => i.FileSizeBytes)
            .First();

        _logger.Log(LogSeverity.Info, $"Doublons probables: conservation de {best.FileName}.", oldPath: best.FullPath);
        foreach (var loser in group.Where(i => !string.Equals(i.FullPath, best.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (AreDifferentVersions(best, loser))
            {
                _logger.Log(LogSeverity.Ignored, "Titre proche mais propriétés audio différentes - conservé.", oldPath: loser.FullPath);
                continue;
            }

            stats.IncrementDuplicatesFound();
            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Duplicate, $"Simulation: doublon inférieur à envoyer à la Corbeille. Gardé: {best.FileName}", oldPath: loser.FullPath);
                continue;
            }

            RecycleBinService.SendToRecycleBin(loser.FullPath);
            stats.IncrementDuplicatesDeleted();
            stats.AddBytesFreed(loser.FileSizeBytes);
            EmptyFolderCleanupService.DeleteEmptyAncestors(loser.FullPath, settings.DestinationFolder, settings, stats, _logger);
            _logger.Log(LogSeverity.Duplicate, $"Doublon envoyé à la Corbeille. Gardé: {best.FileName}", oldPath: loser.FullPath);
        }
    }

    private void SortNumberedFileDuplicateGroups(List<AudioFileInfo> tracks, OrganizerSettings settings, OrganizerStats stats)
    {
        var groups = tracks
            .Where(i => File.Exists(i.FullPath))
            .GroupBy(GetNumberedFileDuplicateKey)
            .Where(g => g.Count() > 1 && g.Any(i => HasTrailingCopyNumber(Path.GetFileNameWithoutExtension(i.FileName))));

        foreach (var group in groups)
        {
            var candidates = group.Where(i => File.Exists(i.FullPath)).ToList();
            if (candidates.Count <= 1)
            {
                continue;
            }

            var best = candidates.OrderByDescending(QualityComparer.GetRankScore)
                .ThenByDescending(i => i.BitrateKbps)
                .ThenByDescending(i => i.FileSizeBytes)
                .First();

            _logger.Log(LogSeverity.Info, $"Doublons numérotés: conservation de {best.FileName}.", oldPath: best.FullPath);
            foreach (var loser in candidates.Where(i => !string.Equals(i.FullPath, best.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                if (HasLiveMarker(best.Title) != HasLiveMarker(loser.Title))
                {
                    _logger.Log(LogSeverity.Ignored, "Doublon numéroté ignoré: version live/studio différente.", oldPath: loser.FullPath);
                    continue;
                }

                stats.IncrementDuplicatesFound();
                if (settings.SimulateOnly)
                {
                    _logger.Log(LogSeverity.Duplicate,
                        $"Simulation: doublon numéroté à envoyer à la Corbeille. Gardé: {best.FileName}",
                        oldPath: loser.FullPath);
                    continue;
                }

                RecycleBinService.SendToRecycleBin(loser.FullPath);
                stats.IncrementDuplicatesDeleted();
                stats.AddBytesFreed(loser.FileSizeBytes);
                EmptyFolderCleanupService.DeleteEmptyAncestors(loser.FullPath, settings.DestinationFolder, settings, stats, _logger);
                _logger.Log(LogSeverity.Duplicate,
                    $"Doublon numéroté envoyé à la Corbeille. Gardé: {best.FileName}",
                    oldPath: loser.FullPath);
            }

        }
    }

    private void RenameBestNumberedDuplicate(AudioFileInfo best, OrganizerSettings settings, OrganizerStats stats)
    {
        if (!File.Exists(best.FullPath))
        {
            return;
        }

        var primaryArtist = ArtistNameNormalizer.Normalize(GetPrimaryArtistFromFileName(best));
        var title = ArtistNameNormalizer.NormalizeTitle(GetTitleWithoutCopyNumber(best));
        var currentFolder = Path.GetDirectoryName(best.FullPath) ?? settings.DestinationFolder;
        var targetFolder = GetTargetArtistFolder(settings.DestinationFolder, currentFolder, primaryArtist);
        var targetPath = Path.Combine(targetFolder, FileNameSanitizer.SanitizeFileName($"{primaryArtist} - {title}") + Path.GetExtension(best.FullPath));

        if (string.Equals(Path.GetFullPath(best.FullPath), Path.GetFullPath(targetPath), StringComparison.Ordinal))
        {
            return;
        }

        if (settings.SimulateOnly)
        {
            _logger.Log(LogSeverity.Info, "Simulation: renommer le meilleur doublon numéroté sans suffixe.", oldPath: best.FullPath, newPath: targetPath);
            return;
        }

        if (File.Exists(targetPath) && !string.Equals(Path.GetFullPath(best.FullPath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            ResolveMoveConflict(best, targetPath, settings, stats);
            return;
        }

        Directory.CreateDirectory(targetFolder);
        var originalPath = best.FullPath;
        MoveFileAllowingCaseOnlyRename(best.FullPath, targetPath);
        EmptyFolderCleanupService.DeleteEmptyAncestors(originalPath, settings.DestinationFolder, settings, stats, _logger);
        stats.IncrementMoved();
        _logger.Log(LogSeverity.Moved, "Meilleur doublon numéroté renommé sans suffixe.", oldPath: best.FullPath, newPath: targetPath);
    }

    private void MoveToPrimaryArtistFolder(AudioFileInfo info, OrganizerSettings settings, OrganizerStats stats)
    {
        if (!File.Exists(info.FullPath))
        {
            return;
        }

        var primaryArtist = ArtistNameNormalizer.Normalize(GetPrimaryArtistFromFileName(info));
        var normalizedTitle = ArtistNameNormalizer.NormalizeTitle(GetTitleWithoutCopyNumber(info));

        var currentFolder = Path.GetDirectoryName(info.FullPath) ?? settings.DestinationFolder;
        var targetFolder = GetTargetArtistFolder(settings.DestinationFolder, currentFolder, primaryArtist);
        var targetFileName = FileNameSanitizer.SanitizeFileName($"{primaryArtist} - {normalizedTitle}") + Path.GetExtension(info.FullPath);
        var targetPath = Path.Combine(targetFolder, targetFileName);

        if (string.Equals(Path.GetFullPath(info.FullPath), Path.GetFullPath(targetPath), StringComparison.Ordinal))
        {
            return;
        }

        if (File.Exists(targetPath) && !string.Equals(Path.GetFullPath(info.FullPath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            ResolveMoveConflict(info, targetPath, settings, stats);
            return;
        }

        if (settings.SimulateOnly)
        {
            _logger.Log(LogSeverity.Info, $"Simulation: déplacer vers le premier artiste {primaryArtist}.", oldPath: info.FullPath, newPath: targetPath);
            return;
        }

        Directory.CreateDirectory(targetFolder);
    var originalPath = info.FullPath;
        MoveFileAllowingCaseOnlyRename(info.FullPath, targetPath);
    EmptyFolderCleanupService.DeleteEmptyAncestors(originalPath, settings.DestinationFolder, settings, stats, _logger);
        stats.IncrementMoved();
        stats.IncrementArtistsSorted();
        _logger.Log(LogSeverity.Moved, $"Déplacé dans le dossier du premier artiste: {primaryArtist}.", oldPath: currentFolder, newPath: targetPath);
    }

    private void ResolveMoveConflict(AudioFileInfo incoming, string existingPath, OrganizerSettings settings, OrganizerStats stats)
    {
        var existing = MetadataService.ReadAsync(existingPath, settings.UseTagsFirst, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (AreDifferentVersions(incoming, existing) || GetDuplicateKey(incoming) != GetDuplicateKey(existing))
        {
            stats.IncrementIgnored();
            _logger.Log(LogSeverity.Ignored,
                "Conflit de nom mais titre/version différent - aucun doublon créé automatiquement.",
                oldPath: incoming.FullPath,
                newPath: existingPath);
            return;
        }

        stats.IncrementDuplicatesFound();
        var comparison = CompareQuality(incoming, existing);
        if (settings.SimulateOnly)
        {
            var loser = comparison > 0 ? existingPath : incoming.FullPath;
            var winner = comparison > 0 ? incoming.FullPath : existingPath;
            _logger.Log(LogSeverity.Duplicate,
                $"Simulation: conflit résolu sans créer de copie. Garder: {Path.GetFileName(winner)} / Corbeille: {Path.GetFileName(loser)}.",
                oldPath: loser,
                newPath: winner);
            return;
        }

        if (comparison > 0)
        {
            RecycleBinService.SendToRecycleBin(existingPath);
            stats.IncrementDuplicatesDeleted();
            stats.AddBytesFreed(existing.FileSizeBytes);
            EmptyFolderCleanupService.DeleteEmptyAncestors(existingPath, settings.DestinationFolder, settings, stats, _logger);
            Directory.CreateDirectory(Path.GetDirectoryName(existingPath) ?? settings.DestinationFolder);
            var originalPath = incoming.FullPath;
            MoveFileAllowingCaseOnlyRename(incoming.FullPath, existingPath);
            EmptyFolderCleanupService.DeleteEmptyAncestors(originalPath, settings.DestinationFolder, settings, stats, _logger);
            stats.IncrementMoved();
            _logger.Log(LogSeverity.Duplicate,
                "Conflit: fichier entrant de meilleure qualité, ancien fichier envoyé à la Corbeille.",
                oldPath: incoming.FullPath,
                newPath: existingPath);
            return;
        }

        RecycleBinService.SendToRecycleBin(incoming.FullPath);
        stats.IncrementDuplicatesDeleted();
        stats.AddBytesFreed(incoming.FileSizeBytes);
        EmptyFolderCleanupService.DeleteEmptyAncestors(incoming.FullPath, settings.DestinationFolder, settings, stats, _logger);
        _logger.Log(LogSeverity.Duplicate,
            "Conflit: fichier existant de qualité égale ou supérieure, entrant envoyé à la Corbeille.",
            oldPath: incoming.FullPath,
            newPath: existingPath);
    }

    private static int CompareQuality(AudioFileInfo a, AudioFileInfo b)
    {
        var quality = QualityComparer.Compare(a, b);
        if (quality != 0)
        {
            return quality;
        }

        return a.FileSizeBytes.CompareTo(b.FileSizeBytes);
    }

    private static string GetTargetArtistFolder(string destinationFolder, string currentFolder, string primaryArtist)
    {
        var currentFolderName = Path.GetFileName(currentFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (IsSameArtistFolder(currentFolderName, primaryArtist))
        {
            return currentFolder;
        }

        var parentFolder = Directory.GetParent(currentFolder)?.FullName;
        var baseFolder = string.IsNullOrWhiteSpace(parentFolder) ? destinationFolder : parentFolder;
        return Path.Combine(baseFolder, FileNameSanitizer.SanitizeFolderName(primaryArtist));
    }

    private static bool IsSameArtistFolder(string folderName, string artist)
    {
        return string.Equals(NormalizeForMatch(folderName), NormalizeForMatch(artist), StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveFileAllowingCaseOnlyRename(string sourcePath, string targetPath)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var targetFullPath = Path.GetFullPath(targetPath);
        if (string.Equals(sourceFullPath, targetFullPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, $".{Guid.NewGuid():N}.tmp{Path.GetExtension(sourcePath)}");
            File.Move(sourcePath, tempPath, overwrite: false);
            File.Move(tempPath, targetPath, overwrite: false);
            return;
        }

        File.Move(sourcePath, targetPath, overwrite: false);
    }

    private void DeleteEmptyFolders(string root, OrganizerSettings settings, OrganizerStats stats)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(f => f.Length))
        {
            if (ContainsSupportedAudioFiles(folder) || IsGeneratedShortcutFolder(root, folder))
            {
                continue;
            }

            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Info, "Simulation: supprimer le dossier sans fichier musical.", oldPath: folder);
                continue;
            }

            Directory.Delete(folder, recursive: true);
            stats.IncrementEmptyFoldersDeleted();
            _logger.Log(LogSeverity.Ignored, "Dossier sans fichier musical supprimé.", oldPath: folder);
        }
    }

    private static bool ContainsSupportedAudioFiles(string folder)
    {
        return Directory.EnumerateFiles(folder, "*.*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Any(file => AudioFormatExtensions.IsSupported(Path.GetExtension(file)));
    }

    private static bool IsGeneratedShortcutFolder(string root, string folder)
    {
        var relative = Path.GetRelativePath(root, folder);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault() ?? string.Empty;
        return firstSegment.Equals("Date", StringComparison.OrdinalIgnoreCase)
            || firstSegment.Equals("Style", StringComparison.OrdinalIgnoreCase)
            || firstSegment.Equals("Playlists", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSupportedFiles(string root, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.*", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true }))
        {
            if (AudioFormatExtensions.IsSupported(Path.GetExtension(file)))
            {
                yield return file;
            }
        }
    }

    private static ProgressInfo BuildProgress(OrganizerStats stats, string? currentFile) => new()
    {
        TotalFiles = stats.TotalFilesFound,
        FilesAnalyzed = stats.FilesAnalyzed,
        FilesMoved = stats.FilesMoved,
        FilesIgnored = stats.FilesIgnored,
        DuplicatesFound = stats.DuplicatesFound,
        Errors = stats.Errors,
        Elapsed = stats.Stopwatch.Elapsed,
        FilesPerSecond = stats.FilesPerSecond,
        CurrentFile = currentFile
    };

    private static string GetDuplicateKey(AudioFileInfo info)
    {
        var artist = NormalizeForMatch(GetPrimaryArtist(info.Artist));
        var title = NormalizeForMatch(RemoveVersionNoise(RemoveTrailingCopyNumbers(info.Title)));
        return $"{artist}\u001f{title}";
    }

    private static string GetNumberedFileDuplicateKey(AudioFileInfo info)
    {
        var folder = NormalizeForMatch(Path.GetDirectoryName(info.FullPath) ?? string.Empty);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(info.FileName);
        var baseFileName = RemoveTrailingCopyNumbers(fileNameWithoutExtension);
        return $"{folder}\u001f{NormalizeForMatch(baseFileName)}";
    }

    public static string GetNumberedFileDuplicateKeyForTest(string fullPath)
    {
        return $"{NormalizeForMatch(Path.GetDirectoryName(fullPath) ?? string.Empty)}\u001f{NormalizeForMatch(RemoveTrailingCopyNumbers(Path.GetFileNameWithoutExtension(fullPath)))}";
    }

    public static string GetCanonicalTitleForTest(string fileName)
    {
        var info = new AudioFileInfo
        {
            FullPath = fileName,
            FileName = Path.GetFileName(fileName),
            Extension = Path.GetExtension(fileName),
            Format = AudioFormatType.Unknown,
            FileSizeBytes = 0
        };

        return ArtistNameNormalizer.NormalizeTitle(GetTitleWithoutCopyNumber(info));
    }

    private static string GetTitleWithoutCopyNumber(AudioFileInfo info)
    {
        var fileNameWithoutExtension = RemoveTrailingCopyNumbers(Path.GetFileNameWithoutExtension(info.FileName));
        var fileArtist = MetadataService.ParseArtistFromFileName(fileNameWithoutExtension, out var fileTitle);
        if (!string.IsNullOrWhiteSpace(fileArtist) && !string.IsNullOrWhiteSpace(fileTitle))
        {
            return RemoveVersionNoise(fileTitle);
        }

        return RemoveVersionNoise(RemoveTrailingCopyNumbers(info.Title));
    }

    private static string GetPrimaryArtist(string artist)
    {
        var match = ArtistSeparatorRegex().Match(artist);
        return (match.Success ? artist[..match.Index] : artist).Trim();
    }

    private static string GetPrimaryArtistFromFileName(AudioFileInfo info)
    {
        var fileNameWithoutExtension = RemoveTrailingCopyNumbers(Path.GetFileNameWithoutExtension(info.FileName));
        var fileArtist = MetadataService.ParseArtistFromFileName(fileNameWithoutExtension, out _);
        return !string.IsNullOrWhiteSpace(fileArtist) ? GetPrimaryArtist(fileArtist) : GetPrimaryArtist(info.Artist);
    }

    private static string RemoveVersionNoise(string title)
    {
        var cleaned = RemoveTrailingCopyNumbers(title);
        cleaned = ParentheticalNoiseRegex().Replace(cleaned, " $1 ");
        return cleaned;
    }

    private static string RemoveTrailingCopyNumbers(string title)
    {
        var cleaned = title;
        string previous;
        do
        {
            previous = cleaned;
            cleaned = TrailingCopyNumberRegex().Replace(cleaned, string.Empty).Trim();
        } while (!string.Equals(previous, cleaned, StringComparison.Ordinal));

        return cleaned;
    }

    private static bool HasTrailingCopyNumber(string title) => TrailingCopyNumberRegex().IsMatch(title);

    private static bool AreDifferentVersions(AudioFileInfo a, AudioFileInfo b)
    {
        if (HasLiveMarker(a.Title) != HasLiveMarker(b.Title))
        {
            return true;
        }

        if (a.Duration <= TimeSpan.Zero || b.Duration <= TimeSpan.Zero)
        {
            return false;
        }

        var durationDiff = Math.Abs((a.Duration - b.Duration).TotalSeconds);
        var durationThreshold = Math.Max(8.0, Math.Min(a.Duration.TotalSeconds, b.Duration.TotalSeconds) * 0.05);
        return durationDiff > durationThreshold;
    }

    private static bool HasLiveMarker(string title) => LiveMarkerRegex().IsMatch(title);

    private static string NormalizeForMatch(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString()
            .Replace("donot", "dont", StringComparison.Ordinal)
            .Replace("dont", "dont", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\s+(?:,|;|vs\.?|feat\.?|featuring|en duo|with|&|/|\+)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex ArtistSeparatorRegex();

    [GeneratedRegex(@"(?:\s*[\(\[]\d+[\)\]]\s*)+$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingCopyNumberRegex();

    [GeneratedRegex(@"(?:^|[\s\(\[])(?:live|concert|unplugged)(?:$|[\s\)\]])", RegexOptions.IgnoreCase)]
    private static partial Regex LiveMarkerRegex();

    [GeneratedRegex(@"\s*\((remaster(?:ed)?|version|mono|stereo|edit|single|album version)\)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ParentheticalNoiseRegex();
}