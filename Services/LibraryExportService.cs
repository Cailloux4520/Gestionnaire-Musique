using System.Text;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

public static class LibraryExportService
{
    public static void ExportCsv(IEnumerable<LibraryTrackRecord> records, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        var path = Path.Combine(destinationFolder, "MusicOrganizer-library.csv");
        var builder = new StringBuilder();
        builder.AppendLine("Artist;Album;Title;Track;Duration;BitrateKbps;SizeBytes;HasCover;Extension;DestinationPath;Fingerprint");

        foreach (var record in records.OrderBy(r => r.Artist).ThenBy(r => r.Album).ThenBy(r => r.TrackNumber).ThenBy(r => r.Title))
        {
            builder.Append(Escape(record.Artist)).Append(';')
                .Append(Escape(record.Album)).Append(';')
                .Append(Escape(record.Title)).Append(';')
                .Append(record.TrackNumber).Append(';')
                .Append(Escape(record.Duration.ToString(@"hh\:mm\:ss"))).Append(';')
                .Append(record.BitrateKbps).Append(';')
                .Append(record.FileSizeBytes).Append(';')
                .Append(record.HasCover ? "Oui" : "Non").Append(';')
                .Append(Escape(record.Extension)).Append(';')
                .Append(Escape(record.DestinationPath)).Append(';')
                .AppendLine(Escape(record.Fingerprint));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void GeneratePlaylists(IEnumerable<LibraryTrackRecord> records, string destinationFolder)
    {
        var playlistFolder = Path.Combine(destinationFolder, "Playlists");
        Directory.CreateDirectory(playlistFolder);

        foreach (var group in records.GroupBy(r => string.IsNullOrWhiteSpace(r.Artist) ? "Inconnu" : r.Artist))
        {
            var fileName = FileNameSanitizer.SanitizeFileName(group.Key) + ".m3u8";
            var path = Path.Combine(playlistFolder, fileName);
            var lines = group.OrderBy(r => r.Album).ThenBy(r => r.TrackNumber).ThenBy(r => r.Title)
                .Select(r => Path.GetRelativePath(playlistFolder, r.DestinationPath));
            File.WriteAllLines(path, new[] { "#EXTM3U" }.Concat(lines), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}