namespace MusicOrganizer.Services;

public static class CoverArtService
{
    private static readonly string[] CoverPatterns = ["cover.jpg", "folder.jpg", "AlbumArt*.jpg"];

    public static bool HasSidecarCover(string audioPath)
    {
        var folder = Path.GetDirectoryName(audioPath);
        return !string.IsNullOrWhiteSpace(folder) && EnumerateCoverFiles(folder).Any();
    }

    public static int CopyCoverArt(string audioPath, string destinationFolder, bool simulateOnly)
    {
        var sourceFolder = Path.GetDirectoryName(audioPath);
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
        {
            return 0;
        }

        var copied = 0;
        foreach (var coverPath in EnumerateCoverFiles(sourceFolder))
        {
            var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(coverPath));
            if (File.Exists(destinationPath))
            {
                continue;
            }

            if (!simulateOnly)
            {
                Directory.CreateDirectory(destinationFolder);
                File.Copy(coverPath, destinationPath, overwrite: false);
            }

            copied++;
        }

        return copied;
    }

    private static IEnumerable<string> EnumerateCoverFiles(string folder)
    {
        foreach (var pattern in CoverPatterns)
        {
            foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }
}