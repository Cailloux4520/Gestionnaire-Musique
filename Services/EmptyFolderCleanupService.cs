using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public static class EmptyFolderCleanupService
{
    public static void DeleteFoldersWithoutMusic(string rootFolder, OrganizerSettings settings, OrganizerStats stats, LoggingService logger, bool includeRoot = false)
    {
        if (settings.SimulateOnly || string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return;
        }

        var folders = Directory.EnumerateDirectories(rootFolder, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            })
            .OrderByDescending(folder => folder.Length)
            .ToList();

        if (includeRoot)
        {
            folders.Add(rootFolder);
        }

        foreach (var folder in folders)
        {
            DeleteFolderIfNoMusic(folder, stats, logger);
        }
    }

    public static void DeleteEmptyAncestors(string originalFilePath, string rootFolder, OrganizerSettings settings, OrganizerStats stats, LoggingService logger)
    {
        if (settings.SimulateOnly || string.IsNullOrWhiteSpace(rootFolder))
        {
            return;
        }

        var folder = Path.GetDirectoryName(originalFilePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var rootFullPath = Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        while (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            var folderFullPath = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsSameOrChildOf(folderFullPath, rootFullPath))
            {
                return;
            }

            if (ContainsSupportedAudioFiles(folder))
            {
                return;
            }

            if (!DeleteFolderIfNoMusic(folder, stats, logger))
            {
                return;
            }

            if (string.Equals(folderFullPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            folder = Directory.GetParent(folder)?.FullName;
        }
    }

    private static bool DeleteFolderIfNoMusic(string folder, OrganizerStats stats, LoggingService logger)
    {
        if (!Directory.Exists(folder) || ContainsSupportedAudioFiles(folder))
        {
            return false;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
            stats.IncrementEmptyFoldersDeleted();
            logger.Log(LogSeverity.Ignored, "Dossier sans fichier musical supprimé.", oldPath: folder);
            return true;
        }
        catch (Exception ex)
        {
            stats.IncrementIgnored();
            logger.Log(LogSeverity.Ignored, $"Dossier sans fichier musical non supprimé: {ex.Message}", oldPath: folder);
            return false;
        }
    }

    private static bool ContainsSupportedAudioFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                })
                .Any(file => AudioFormatExtensions.IsSupported(Path.GetExtension(file)));
        }
        catch
        {
            return true;
        }
    }

    private static bool IsSameOrChildOf(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
