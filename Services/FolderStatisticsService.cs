using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public static class FolderStatisticsService
{
    public static List<FolderMusicCount> CountMusicFilesByFolder(string destinationFolder)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder) || !Directory.Exists(destinationFolder))
        {
            return [];
        }

        return Directory.EnumerateDirectories(destinationFolder, "*", SearchOption.TopDirectoryOnly)
            .Select(folder => new FolderMusicCount
            {
                FolderName = Path.GetFileName(folder),
                FullPath = folder,
                MusicFileCount = CountSupportedFiles(folder)
            })
            .OrderByDescending(item => item.MusicFileCount)
            .ThenBy(item => item.FolderName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int CountSupportedFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            }).Count(file => AudioFormatExtensions.IsSupported(Path.GetExtension(file)));
        }
        catch
        {
            return 0;
        }
    }
}