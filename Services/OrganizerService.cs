using System.Collections.Concurrent;
using System.Diagnostics;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// The heart of MusicOrganizer: scans the source folder, reads each file's metadata,
/// determines the destination "Destination\Artist" folder, and moves the file there -
/// handling duplicates according to the quality-priority rules.
///
/// Designed to scale to 100,000+ files:
///   - Only metadata is ever read (never the full audio stream).
///   - Files are processed in parallel via Parallel.ForEachAsync.
///   - Filesystem decisions for a given artist folder are serialized via a per-folder
///     lock, so two threads can never race to move two files into the same slot,
///     while different artist folders are still processed fully in parallel.
///   - Cancellation is checked continuously and never leaves the app in a bad state.
/// </summary>
public sealed class OrganizerService
{
    private readonly LoggingService _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _folderLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _fingerprintDestinations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<LibraryTrackRecord> _libraryRecords = [];

    public OrganizerStats Stats { get; } = new();

    public OrganizerService(LoggingService logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(OrganizerSettings settings, IProgress<ProgressInfo> progress, CancellationToken token)
    {
        Stats.Stopwatch.Start();

        if (settings.FindOriginalYear)
        {
            await RunOriginalYearSearchAsync(settings, progress, token).ConfigureAwait(false);
            return;
        }

        if (settings.CreateDateShortcuts || settings.CreateStyleShortcuts)
        {
            await RunShortcutCreationAsync(settings, progress, token).ConfigureAwait(false);
            return;
        }

        EmptyFolderCleanupService.DeleteFoldersWithoutMusic(settings.SourceFolder, settings, Stats, _logger, includeRoot: true);
        var files = EnumerateSupportedFiles(settings.SourceFolder, settings.Recursive).ToList();
        Stats.TotalFilesFound = files.Count;

        ReportProgress(progress, null);

        using var progressTimer = new System.Threading.Timer(_ => ReportProgress(progress, null), null, 250, 250);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = token
        };

        try
        {
            await Parallel.ForEachAsync(files, options, async (path, ct) =>
            {
                await ProcessFileAsync(path, settings, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            FinalizeLibraryOutputs(settings);
        }
        catch (OperationCanceledException)
        {
            _logger.Log(LogSeverity.Info, "Traitement interrompu par l'utilisateur.");
        }
        finally
        {
            Stats.Stopwatch.Stop();
            ReportProgress(progress, null);
        }
    }

    private async Task RunOriginalYearSearchAsync(OrganizerSettings settings, IProgress<ProgressInfo> progress, CancellationToken token)
    {
        var files = EnumerateSupportedFiles(settings.SourceFolder, settings.Recursive).ToList();
        Stats.TotalFilesFound = files.Count;
        ReportProgress(progress, null);

        using var progressTimer = new System.Threading.Timer(_ => ReportProgress(progress, null), null, 250, 250);
        _logger.Log(LogSeverity.Info, "Recherche de l'année d'origine dans le dossier source.", oldPath: settings.SourceFolder, newPath: settings.DestinationFolder);

        try
        {
            foreach (var path in files)
            {
                token.ThrowIfCancellationRequested();
                await ProcessOriginalYearAsync(path, settings, token).ConfigureAwait(false);
                ReportProgress(progress, path);

                // MusicBrainz asks clients to stay around one request per second.
                await Task.Delay(1100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Log(LogSeverity.Info, "Recherche d'année interrompue par l'utilisateur.");
        }
        finally
        {
            Stats.Stopwatch.Stop();
            ReportProgress(progress, null);
        }
    }

    private async Task ProcessOriginalYearAsync(string path, OrganizerSettings settings, CancellationToken token)
    {
        try
        {
            var info = await MetadataService.ReadAsync(path, settings.UseTagsFirst, token).ConfigureAwait(false);
            Stats.IncrementAnalyzed();

            var year = await MusicBrainzService.TryFindOriginalReleaseYearAsync(info, settings, token).ConfigureAwait(false);
            if (year is null)
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored,
                    $"Année d'origine introuvable pour {info.Artist} - {info.Title}.", oldPath: path);
                return;
            }

            Stats.IncrementOriginalYearsFound();
            CreateOriginalYearShortcut(path, settings, info, year.Value);
            if (info.Year == year.Value)
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Info,
                    $"Année déjà correcte ({year.Value}) pour {info.Artist} - {info.Title}.", oldPath: path);
                return;
            }

            Stats.IncrementIgnored();
            _logger.Log(LogSeverity.Info,
                $"Année proposée {info.Year} -> {year.Value} pour {info.Artist} - {info.Title}; tag laissé inchangé.", oldPath: path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Stats.IncrementErrors();
            _logger.Log(LogSeverity.Error, $"Erreur recherche année: {ex.Message}", oldPath: path);
        }
    }

    private void CreateOriginalYearShortcut(string path, OrganizerSettings settings, AudioFileInfo info, int year)
    {
        var dateRoot = Path.Combine(Path.GetFullPath(settings.DestinationFolder), "Date");
        var decade = year / 10 * 10;
        var decadeFolder = Path.Combine(dateRoot, FileNameSanitizer.SanitizeFolderName(decade.ToString()));
        var shortcutPath = settings.CreateVirtualDjDateFileLinks
            ? BuildOriginalYearMusicLinkPath(decadeFolder, info, year)
            : BuildOriginalYearShortcutPath(decadeFolder, info, year);

        if (!settings.CreateVirtualDjDateFileLinks)
        {
            ReconcileExistingDateShortcuts(path, dateRoot, shortcutPath, settings);
        }

        if (settings.CreateVirtualDjDateFileLinks)
        {
            CreateShortcutIfNeeded(path, shortcutPath, settings,
                $"Simulation: créer le lien fichier VirtualDJ date exacte {year} dans la décennie {decade}.",
                $"Lien fichier VirtualDJ créé pour la date exacte {year} dans la décennie {decade}.",
                () => Stats.IncrementDateShortcutsCreated());
        }
        else
        {
            CreateWindowsShortcutIfNeeded(path, shortcutPath, settings,
                $"Simulation: créer le raccourci date exacte {year} dans la décennie {decade}.",
                $"Raccourci créé pour la date exacte {year} dans la décennie {decade}.",
                () => Stats.IncrementDateShortcutsCreated());
        }
    }

    private void ReconcileExistingDateShortcuts(string targetPath, string dateRoot, string expectedShortcutPath, OrganizerSettings settings)
    {
        if (!Directory.Exists(dateRoot))
        {
            return;
        }

        var expectedFullPath = Path.GetFullPath(expectedShortcutPath);
        var targetFullPath = Path.GetFullPath(targetPath);
        foreach (var shortcutPath in Directory.EnumerateFiles(dateRoot, "*.lnk", SearchOption.AllDirectories))
        {
            var shortcutFullPath = Path.GetFullPath(shortcutPath);
            var shortcutTarget = ShortcutService.TryGetShortcutTarget(shortcutPath);
            var pointsToTarget = !string.IsNullOrWhiteSpace(shortcutTarget)
                && string.Equals(Path.GetFullPath(shortcutTarget), targetFullPath, StringComparison.OrdinalIgnoreCase);
            var isExpectedPath = string.Equals(shortcutFullPath, expectedFullPath, StringComparison.OrdinalIgnoreCase);

            if (pointsToTarget && !isExpectedPath)
            {
                MoveDateShortcutToExpectedFolder(shortcutPath, expectedShortcutPath, settings);
                return;
            }

            if (isExpectedPath && !pointsToTarget)
            {
                DeleteWrongDateShortcut(shortcutPath, targetPath, settings);
                return;
            }
        }
    }

    private void MoveDateShortcutToExpectedFolder(string shortcutPath, string expectedShortcutPath, OrganizerSettings settings)
    {
        if (settings.SimulateOnly)
        {
            _logger.Log(LogSeverity.Info, "Simulation: déplacer le raccourci date dans la bonne décennie.", oldPath: shortcutPath, newPath: expectedShortcutPath);
            return;
        }

        var expectedFolder = Path.GetDirectoryName(expectedShortcutPath) ?? settings.DestinationFolder;
        Directory.CreateDirectory(expectedFolder);
        if (File.Exists(expectedShortcutPath))
        {
            File.Delete(shortcutPath);
            _logger.Log(LogSeverity.Ignored, "Raccourci date en mauvaise décennie supprimé: le bon raccourci existe déjà.", oldPath: shortcutPath, newPath: expectedShortcutPath);
            return;
        }

        File.Move(shortcutPath, expectedShortcutPath);
        _logger.Log(LogSeverity.Moved, "Raccourci date déplacé dans la bonne décennie.", oldPath: shortcutPath, newPath: expectedShortcutPath);
    }

    private void DeleteWrongDateShortcut(string shortcutPath, string targetPath, OrganizerSettings settings)
    {
        if (settings.SimulateOnly)
        {
            _logger.Log(LogSeverity.Info, "Simulation: supprimer le raccourci date qui pointe vers un mauvais fichier.", oldPath: shortcutPath, newPath: targetPath);
            return;
        }

        File.Delete(shortcutPath);
        _logger.Log(LogSeverity.Ignored, "Raccourci date supprimé: il pointait vers un autre fichier.", oldPath: shortcutPath, newPath: targetPath);
    }

    private static IEnumerable<string> EnumerateSupportedFiles(string root, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        IEnumerable<string> all;
        try
        {
            all = Directory.EnumerateFiles(root, "*.*", options);
        }
        catch
        {
            yield break;
        }

        foreach (var file in all)
        {
            if (AudioFormatExtensions.IsSupported(Path.GetExtension(file)))
            {
                yield return file;
            }
        }
    }

    private async Task RunShortcutCreationAsync(OrganizerSettings settings, IProgress<ProgressInfo> progress, CancellationToken token)
    {
        Stats.Stopwatch.Start();
        EmptyFolderCleanupService.DeleteFoldersWithoutMusic(settings.SourceFolder, settings, Stats, _logger, includeRoot: true);
        var files = EnumerateSupportedFiles(settings.SourceFolder, settings.Recursive).ToList();
        Stats.TotalFilesFound = files.Count;
        var dateRoot = settings.CreateDateShortcuts && !settings.CreateStyleShortcuts
            ? settings.DestinationFolder
            : Path.Combine(settings.DestinationFolder, "Date");
        var styleRoot = settings.CreateStyleShortcuts && !settings.CreateDateShortcuts
            ? settings.DestinationFolder
            : Path.Combine(settings.DestinationFolder, "Style");
        ReportProgress(progress, null);
        if (settings.CreateDateShortcuts)
        {
            _logger.Log(LogSeverity.Info, "Création des raccourcis par année.", newPath: dateRoot);
        }

        if (settings.CreateStyleShortcuts)
        {
            _logger.Log(LogSeverity.Info, "Création des raccourcis par style.", newPath: styleRoot);
        }

        try
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
                CancellationToken = token
            };

            await Parallel.ForEachAsync(files, options, async (path, ct) =>
            {
                await ProcessShortcutAsync(path, dateRoot, styleRoot, settings, ct).ConfigureAwait(false);
                ReportProgress(progress, path);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Log(LogSeverity.Info, "Création de raccourcis interrompue par l'utilisateur.");
        }
        finally
        {
            Stats.Stopwatch.Stop();
            ReportProgress(progress, null);
        }
    }

    private async Task ProcessShortcutAsync(string path, string dateRoot, string styleRoot, OrganizerSettings settings, CancellationToken token)
    {
        try
        {
            var info = await MetadataService.ReadAsync(path, settings.UseTagsFirst, token).ConfigureAwait(false);
            Stats.IncrementAnalyzed();

            if (settings.CreateDateShortcuts)
            {
                await CreateDateShortcutAsync(path, dateRoot, info, settings, token).ConfigureAwait(false);
            }

            if (settings.CreateStyleShortcuts)
            {
                CreateStyleShortcut(path, styleRoot, info, settings);
            }
        }
        catch (Exception ex)
        {
            Stats.IncrementErrors();
            _logger.Log(LogSeverity.Error, $"Erreur création raccourci: {ex.Message}", oldPath: path);
        }
    }

    private async Task CreateDateShortcutAsync(string path, string dateRoot, AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        var dateFolderName = info.Year > 0 ? info.Year.ToString() : string.Empty;
        if (info.Year <= 0)
        {
            var releaseYear = await MusicBrainzService.TryFindOriginalReleaseYearAsync(info, settings, token).ConfigureAwait(false);
            if (releaseYear is null)
            {
                RemoveObsoleteZeroYearLink(path, dateRoot, info, settings);
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, $"Aucune année dans les tags et aucune année originale concordante trouvée sur internet pour {info.Artist} - {info.Title}.", oldPath: path);
                return;
            }

            dateFolderName = releaseYear.Value.ToString();
            RemoveObsoleteZeroYearLink(path, dateRoot, info, settings);
            _logger.Log(LogSeverity.Info, $"Année originale concordante trouvée sur internet pour {info.Artist} - {info.Title}: {dateFolderName}.", oldPath: path);
        }

        var yearFolder = Path.Combine(dateRoot, FileNameSanitizer.SanitizeFolderName(dateFolderName));
        var shortcutPath = BuildShortcutPath(yearFolder, info.FileName);
        CreateShortcutIfNeeded(path, shortcutPath, settings,
            $"Simulation: créer le lien audio année {dateFolderName}.",
            $"Lien audio créé pour l'année {dateFolderName}.",
            () => Stats.IncrementDateShortcutsCreated());
    }

    private void RemoveObsoleteZeroYearLink(string targetPath, string dateRoot, AudioFileInfo info, OrganizerSettings settings)
    {
        var obsoleteFolder = Path.Combine(dateRoot, "0");
        var obsoletePath = BuildShortcutPath(obsoleteFolder, info.FileName);
        if (settings.SimulateOnly || !File.Exists(obsoletePath))
        {
            return;
        }

        if (string.Equals(Path.GetFullPath(obsoletePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            File.Delete(obsoletePath);
            _logger.Log(LogSeverity.Info, "Ancien lien année 0 supprimé après identification de la vraie année.", oldPath: obsoletePath, newPath: targetPath);

            if (Directory.Exists(obsoleteFolder) && !Directory.EnumerateFileSystemEntries(obsoleteFolder).Any())
            {
                Directory.Delete(obsoleteFolder);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogSeverity.Error, $"Impossible de supprimer l'ancien lien année 0: {ex.Message}", oldPath: obsoletePath);
        }
    }

    private void CreateStyleShortcut(string path, string styleRoot, AudioFileInfo info, OrganizerSettings settings)
    {
        if (string.IsNullOrWhiteSpace(info.Genre))
        {
            Stats.IncrementIgnored();
            _logger.Log(LogSeverity.Ignored, $"Aucun genre dans les tags pour {info.Artist} - {info.Title}.", oldPath: path);
            return;
        }

        var genreFolder = Path.Combine(styleRoot, FileNameSanitizer.SanitizeFolderName(info.Genre));
        var shortcutPath = BuildShortcutPath(genreFolder, info.FileName);
        CreateShortcutIfNeeded(path, shortcutPath, settings,
            $"Simulation: créer le lien audio style {info.Genre}.",
            $"Lien audio créé pour le style {info.Genre}.",
            () => Stats.IncrementStyleShortcutsCreated());
    }

    private void CreateShortcutIfNeeded(string targetPath, string shortcutPath, OrganizerSettings settings, string simulationMessage, string createdMessage, Action incrementCounter)
    {
        var shortcutFolder = Path.GetDirectoryName(shortcutPath) ?? settings.DestinationFolder;
        var folderLock = _folderLocks.GetOrAdd(shortcutFolder, _ => new SemaphoreSlim(1, 1));
        folderLock.Wait();
        try
        {
            if (ShortcutService.FolderContainsShortcutToTarget(shortcutFolder, targetPath))
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, "Un raccourci vers ce fichier existe déjà dans ce dossier.", oldPath: targetPath, newPath: shortcutFolder);
                return;
            }

            if (File.Exists(shortcutPath))
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, "Lien audio déjà existant.", oldPath: targetPath, newPath: shortcutPath);
                return;
            }

            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Info, simulationMessage, oldPath: targetPath, newPath: shortcutPath);
                return;
            }

            if (!Directory.Exists(shortcutFolder))
            {
                Directory.CreateDirectory(shortcutFolder);
                Stats.IncrementFoldersCreated();
            }

            var linkType = ShortcutService.CreateMusicFileLink(targetPath, shortcutPath);
            incrementCounter();
            _logger.Log(LogSeverity.Moved, $"{createdMessage} Type: {linkType}.", oldPath: targetPath, newPath: shortcutPath);
        }
        finally
        {
            folderLock.Release();
        }
    }

    private static string BuildShortcutPath(string folder, string sourceFileName)
    {
        var fileNameWithoutExtension = FileNameSanitizer.SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFileName));
        var extension = Path.GetExtension(sourceFileName);
        return Path.Combine(folder, fileNameWithoutExtension + extension);
    }

    private void CreateWindowsShortcutIfNeeded(string targetPath, string shortcutPath, OrganizerSettings settings, string simulationMessage, string createdMessage, Action incrementCounter)
    {
        var shortcutFolder = Path.GetDirectoryName(shortcutPath) ?? settings.DestinationFolder;
        var folderLock = _folderLocks.GetOrAdd(shortcutFolder, _ => new SemaphoreSlim(1, 1));
        folderLock.Wait();
        try
        {
            if (ShortcutService.FolderContainsShortcutToTarget(shortcutFolder, targetPath))
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, "Un raccourci vers ce fichier existe déjà dans ce dossier.", oldPath: targetPath, newPath: shortcutFolder);
                return;
            }

            if (File.Exists(shortcutPath))
            {
                Stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, "Raccourci déjà existant.", oldPath: targetPath, newPath: shortcutPath);
                return;
            }

            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Info, simulationMessage, oldPath: targetPath, newPath: shortcutPath);
                return;
            }

            if (!Directory.Exists(shortcutFolder))
            {
                Directory.CreateDirectory(shortcutFolder);
                Stats.IncrementFoldersCreated();
            }

            ShortcutService.CreateShortcut(targetPath, shortcutPath);
            incrementCounter();
            _logger.Log(LogSeverity.Moved, $"{createdMessage} Type: raccourci Windows.", oldPath: targetPath, newPath: shortcutPath);
        }
        finally
        {
            folderLock.Release();
        }
    }

    private static string BuildWindowsShortcutPath(string folder, string sourceFileName)
    {
        var fileNameWithoutExtension = FileNameSanitizer.SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFileName));
        return Path.Combine(folder, fileNameWithoutExtension + ".lnk");
    }

    private static string BuildOriginalYearShortcutPath(string folder, AudioFileInfo info, int year)
    {
        var artist = FileNameSanitizer.SanitizeFileName(string.IsNullOrWhiteSpace(info.Artist) ? "Inconnu" : info.Artist);
        var title = FileNameSanitizer.SanitizeFileName(string.IsNullOrWhiteSpace(info.Title) ? Path.GetFileNameWithoutExtension(info.FileName) : info.Title);
        return Path.Combine(folder, $"{year} - {artist} - {title}.lnk");
    }

    private static string BuildOriginalYearMusicLinkPath(string folder, AudioFileInfo info, int year)
    {
        var artist = FileNameSanitizer.SanitizeFileName(string.IsNullOrWhiteSpace(info.Artist) ? "Inconnu" : info.Artist);
        var title = FileNameSanitizer.SanitizeFileName(string.IsNullOrWhiteSpace(info.Title) ? Path.GetFileNameWithoutExtension(info.FileName) : info.Title);
        var extension = string.IsNullOrWhiteSpace(info.Extension) ? Path.GetExtension(info.FileName) : info.Extension;
        return Path.Combine(folder, $"{year} - {artist} - {title}{extension}");
    }

    private async Task ProcessFileAsync(string path, OrganizerSettings settings, CancellationToken token)
    {
        try
        {
            var info = await MetadataService.ReadAsync(path, settings.UseTagsFirst, token).ConfigureAwait(false);
            Stats.IncrementAnalyzed();

            await EnrichMetadataAsync(info, settings, token).ConfigureAwait(false);

            if (settings.UseFingerprintDuplicates)
            {
                info.Fingerprint = await AudioFingerprintService.ComputeAsync(info.FullPath, token).ConfigureAwait(false);
                if (_fingerprintDestinations.TryGetValue(info.Fingerprint, out var existingPath) && File.Exists(existingPath))
                {
                    Stats.IncrementDuplicatesFound();
                    Stats.IncrementFingerprintDuplicatesFound();
                    HandleLosingFile(info.FullPath, info.FileSizeBytes, settings,
                        $"Doublon détecté par empreinte audio/contenu. Original: {existingPath}");
                    return;
                }
            }

            var artistFolderName = FileNameSanitizer.SanitizeFolderName(info.Artist);
            var artistFolderPath = Path.Combine(settings.DestinationFolder, artistFolderName);

            var folderLock = _folderLocks.GetOrAdd(artistFolderPath, _ => new SemaphoreSlim(1, 1));
            await folderLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await MoveOrResolveAsync(info, artistFolderPath, settings, token).ConfigureAwait(false);
            }
            finally
            {
                folderLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Stats.IncrementErrors();
            _logger.Log(LogSeverity.Error, $"Erreur inattendue: {ex.Message}", oldPath: path);
        }
    }

    private async Task MoveOrResolveAsync(AudioFileInfo info, string artistFolderPath, OrganizerSettings settings, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(artistFolderPath))
            {
                if (!settings.SimulateOnly)
                {
                    Directory.CreateDirectory(artistFolderPath);
                }
                Stats.IncrementFoldersCreated();
                _logger.Log(LogSeverity.Info, "Dossier créé.", newPath: artistFolderPath);
            }

            var destinationFileName = FileNamingService.BuildFileName(info, settings);
            var destinationPath = Path.Combine(artistFolderPath, destinationFileName);

            if (!File.Exists(destinationPath))
            {
                var originalPath = info.FullPath;
                MoveFile(info.FullPath, destinationPath, settings.SimulateOnly);
                AfterSuccessfulMove(info, destinationPath, artistFolderPath, settings);
                EmptyFolderCleanupService.DeleteEmptyAncestors(originalPath, settings.SourceFolder, settings, Stats, _logger);
                Stats.IncrementMoved();
                _logger.Log(LogSeverity.Moved, "Fichier déplacé.", info.FullPath, destinationPath);
                return;
            }

            // A file with the exact same name already exists - resolve as a possible duplicate.
            var existingInfo = await MetadataService.ReadAsync(destinationPath, settings.UseTagsFirst, token).ConfigureAwait(false);
            var decision = DuplicateResolver.Resolve(info, existingInfo);

            switch (decision)
            {
                case DuplicateDecision.KeepExisting:
                {
                    Stats.IncrementDuplicatesFound();
                    HandleLosingFile(info.FullPath, info.FileSizeBytes, settings, "Doublon: qualité inférieure ou égale au fichier déjà présent.");
                    break;
                }

                case DuplicateDecision.PerfectDuplicate:
                {
                    Stats.IncrementDuplicatesFound();
                    HandleLosingFile(info.FullPath, info.FileSizeBytes, settings, "Doublon parfait (fichier identique).");
                    break;
                }

                case DuplicateDecision.ReplaceExisting:
                {
                    Stats.IncrementDuplicatesFound();
                    if (!settings.SimulateOnly)
                    {
                        RecycleBinService.SendToRecycleBin(destinationPath);
                        Stats.IncrementDuplicatesDeleted();
                        Stats.AddBytesFreed(existingInfo.FileSizeBytes);
                        EmptyFolderCleanupService.DeleteEmptyAncestors(destinationPath, settings.DestinationFolder, settings, Stats, _logger);
                    }
                    var originalPath = info.FullPath;
                    MoveFile(info.FullPath, destinationPath, settings.SimulateOnly);
                    AfterSuccessfulMove(info, destinationPath, artistFolderPath, settings);
                    EmptyFolderCleanupService.DeleteEmptyAncestors(originalPath, settings.SourceFolder, settings, Stats, _logger);
                    Stats.IncrementMoved();
                    _logger.Log(LogSeverity.Duplicate,
                        "Doublon remplacé par une meilleure qualité.", info.FullPath, destinationPath);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Stats.IncrementErrors();
            _logger.Log(LogSeverity.Error, $"Erreur lors du traitement: {ex.Message}", oldPath: info.FullPath);
        }
    }

    private void HandleLosingFile(string losingPath, long losingSizeBytes, OrganizerSettings settings, string reason)
    {
        if (settings.SimulateOnly)
        {
            _logger.Log(LogSeverity.Duplicate, reason + " (simulation - rien déplacé)", losingPath);
            return;
        }

        RecycleBinService.SendToRecycleBin(losingPath);
        Stats.IncrementDuplicatesDeleted();
        Stats.AddBytesFreed(losingSizeBytes);
        EmptyFolderCleanupService.DeleteEmptyAncestors(losingPath, settings.SourceFolder, settings, Stats, _logger);
        _logger.Log(LogSeverity.Duplicate, reason + " Envoyé à la Corbeille.", losingPath);
    }

    private static void MoveFile(string source, string destination, bool simulateOnly)
    {
        if (simulateOnly)
        {
            return;
        }

        File.Move(source, destination, overwrite: false);
    }

    private async Task EnrichMetadataAsync(AudioFileInfo info, OrganizerSettings settings, CancellationToken token)
    {
        if (settings.FetchMusicBrainzMetadata && await MusicBrainzService.TryCompleteMissingMetadataAsync(info, settings, token).ConfigureAwait(false))
        {
            _logger.Log(LogSeverity.Info, "Métadonnées complétées depuis MusicBrainz.", oldPath: info.FullPath);
        }

        if (settings.FixTags)
        {
            WriteMissingTagsPreservingValues(info);
        }

        if (settings.KeepPrimaryArtistOnly)
        {
            var primaryArtist = ArtistNameNormalizer.ExtractPrimaryArtist(info.Artist);
            if (!string.Equals(info.Artist, primaryArtist, StringComparison.Ordinal))
            {
                info.Artist = primaryArtist;
                info.PrimaryArtistWasExtracted = true;
                Stats.IncrementArtistNamesNormalized();
            }
        }

        if (settings.NormalizeArtists)
        {
            var normalizedArtist = ArtistNameNormalizer.Normalize(info.Artist);
            if (!string.Equals(info.Artist, normalizedArtist, StringComparison.Ordinal))
            {
                info.Artist = normalizedArtist;
                Stats.IncrementArtistNamesNormalized();
            }

            info.Title = ArtistNameNormalizer.NormalizeTitle(info.Title);
        }

        if (!info.HasEmbeddedCover && !CoverArtService.HasSidecarCover(info.FullPath))
        {
            Stats.IncrementTracksWithoutCover();
        }
    }

    private void WriteMissingTagsPreservingValues(AudioFileInfo info)
    {
        var missing = new List<string>();
        if (!info.HasArtistTag)
        {
            missing.Add("artiste");
        }

        if (!info.HasTitleTag)
        {
            missing.Add("titre");
        }

        if (missing.Count == 0)
        {
            return;
        }

        if (MetadataService.WriteCorrectedTags(info))
        {
            Stats.IncrementTagsFixed();
            _logger.Log(LogSeverity.Info, $"Tags manquants corrigés sans nettoyage des caractères: {string.Join(", ", missing)}.", oldPath: info.FullPath);
            return;
        }

        Stats.IncrementIgnored();
        _logger.Log(LogSeverity.Ignored, $"Tags manquants contrôlés mais non modifiés: {string.Join(", ", missing)}.", oldPath: info.FullPath);
    }

    private void AfterSuccessfulMove(AudioFileInfo info, string destinationPath, string artistFolderPath, OrganizerSettings settings)
    {
        if (settings.UseFingerprintDuplicates && !string.IsNullOrWhiteSpace(info.Fingerprint) && !settings.SimulateOnly)
        {
            _fingerprintDestinations.TryAdd(info.Fingerprint, destinationPath);
        }

        if (settings.MoveCoverArt)
        {
            var copied = CoverArtService.CopyCoverArt(info.FullPath, artistFolderPath, settings.SimulateOnly);
            for (var i = 0; i < copied; i++)
            {
                Stats.IncrementCoverFilesMoved();
            }
        }

        _libraryRecords.Add(new LibraryTrackRecord
        {
            SourcePath = info.FullPath,
            DestinationPath = destinationPath,
            Artist = info.Artist,
            Album = string.IsNullOrWhiteSpace(info.Album) ? "Album inconnu" : info.Album,
            Title = info.Title,
            Extension = info.Extension,
            Fingerprint = info.Fingerprint,
            HasCover = info.HasEmbeddedCover || CoverArtService.HasSidecarCover(info.FullPath),
            FileSizeBytes = info.FileSizeBytes,
            TrackNumber = info.TrackNumber,
            Duration = info.Duration,
            BitrateKbps = info.BitrateKbps
        });
    }

    private void FinalizeLibraryOutputs(OrganizerSettings settings)
    {
        var records = _libraryRecords.ToArray();
        if (records.Length == 0)
        {
            return;
        }

        if (settings.AnalyzeLibrary)
        {
            Stats.ArtistCount = records.Select(r => r.Artist).Distinct(StringComparer.OrdinalIgnoreCase).LongCount();
            Stats.AlbumCount = records.Select(r => $"{r.Artist}\u001f{r.Album}").Distinct(StringComparer.OrdinalIgnoreCase).LongCount();
            Stats.TrackCount = records.Length;
            Stats.TotalLibraryBytes = records.Sum(r => r.FileSizeBytes);
            Stats.SetIncompleteAlbums(CountIncompleteAlbums(records));
        }

        if (settings.SimulateOnly)
        {
            return;
        }

        if (settings.ExportCsv)
        {
            LibraryExportService.ExportCsv(records, settings.DestinationFolder);
            _logger.Log(LogSeverity.Info, "Export CSV généré.", newPath: Path.Combine(settings.DestinationFolder, "MusicOrganizer-library.csv"));
        }

        if (settings.GeneratePlaylists)
        {
            LibraryExportService.GeneratePlaylists(records, settings.DestinationFolder);
            _logger.Log(LogSeverity.Info, "Playlists M3U générées.", newPath: Path.Combine(settings.DestinationFolder, "Playlists"));
        }
    }

    private static long CountIncompleteAlbums(IEnumerable<LibraryTrackRecord> records)
    {
        return records.GroupBy(r => $"{r.Artist}\u001f{r.Album}", StringComparer.OrdinalIgnoreCase)
            .LongCount(group =>
            {
                var maxTrack = group.Max(r => r.TrackNumber);
                return maxTrack > 0 && group.Select(r => r.TrackNumber).Where(n => n > 0).Distinct().Count() < maxTrack;
            });
    }

    private void ReportProgress(IProgress<ProgressInfo> progress, string? currentFile)
    {
        progress.Report(new ProgressInfo
        {
            TotalFiles = Stats.TotalFilesFound,
            FilesAnalyzed = Stats.FilesAnalyzed,
            FilesMoved = Stats.FilesMoved,
            FilesIgnored = Stats.FilesIgnored,
            DuplicatesFound = Stats.DuplicatesFound,
            Errors = Stats.Errors,
            Elapsed = Stats.Stopwatch.Elapsed,
            FilesPerSecond = Stats.FilesPerSecond,
            CurrentFile = currentFile
        });
    }
}
