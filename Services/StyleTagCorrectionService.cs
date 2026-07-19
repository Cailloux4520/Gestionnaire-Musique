using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public sealed class StyleTagCorrectionService
{
    private readonly LoggingService _logger;

    public StyleTagCorrectionService(LoggingService logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(OrganizerSettings settings, OrganizerStats stats, IProgress<ProgressInfo> progress, CancellationToken token)
    {
        stats.Stopwatch.Start();
        var files = EnumerateSupportedFiles(settings.SourceFolder, settings.Recursive).ToList();
        stats.TotalFilesFound = files.Count;
        progress.Report(BuildProgress(stats, null));

        try
        {
            foreach (var path in files)
            {
                token.ThrowIfCancellationRequested();
                await ProcessFileAsync(path, settings, stats, token).ConfigureAwait(false);
                progress.Report(BuildProgress(stats, path));
            }
        }
        finally
        {
            stats.Stopwatch.Stop();
            progress.Report(BuildProgress(stats, null));
        }
    }

    private async Task ProcessFileAsync(string path, OrganizerSettings settings, OrganizerStats stats, CancellationToken token)
    {
        try
        {
            var info = await MetadataService.ReadAsync(path, settings.UseTagsFirst, token).ConfigureAwait(false);
            stats.IncrementAnalyzed();

            var genre = await MusicBrainzService.TryFindPrimaryGenreAsync(info, settings, token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(genre))
            {
                stats.IncrementIgnored();
                _logger.Log(LogSeverity.Ignored, $"Style principal introuvable pour {info.Artist} - {info.Title}.", oldPath: path);
                return;
            }

            if (string.Equals(info.Genre.Trim(), genre, StringComparison.OrdinalIgnoreCase))
            {
                stats.IncrementIgnored();
                _logger.Log(LogSeverity.Info, $"Style déjà correct: {genre}.", oldPath: path);
                return;
            }

            if (settings.SimulateOnly)
            {
                _logger.Log(LogSeverity.Info, $"Simulation: modifier le style {info.Genre} -> {genre}.", oldPath: path);
                return;
            }

            if (MetadataService.WriteGenreTag(path, genre))
            {
                stats.IncrementTagsFixed();
                _logger.Log(LogSeverity.Info, $"Style principal corrigé: {genre}.", oldPath: path);
                return;
            }

            stats.IncrementIgnored();
            _logger.Log(LogSeverity.Ignored, $"Style trouvé mais tag non modifié: {genre}.", oldPath: path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stats.IncrementErrors();
            _logger.Log(LogSeverity.Error, $"Erreur correction style: {ex.Message}", oldPath: path);
        }
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
}