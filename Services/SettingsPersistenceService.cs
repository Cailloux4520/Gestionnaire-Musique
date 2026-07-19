using System.Text.Json;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// Saves and loads the user's last folders as JSON under
/// %AppData%\MusicOrganizer\settings.json, so the application remembers them
/// across launches.
/// </summary>
public static class SettingsPersistenceService
{
    private static string SettingsFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "MusicOrganizer");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }
    }

    public static void Save(OrganizerSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Persisting settings is a convenience, not a critical function - never throw.
        }
    }

    public static OrganizerSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<OrganizerSettings>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall through to defaults if the settings file is missing or corrupt.
        }

        return new OrganizerSettings();
    }
}
