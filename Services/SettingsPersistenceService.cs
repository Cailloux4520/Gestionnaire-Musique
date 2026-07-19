using System.Text.Json;
using MusicOrganizer.Models;

namespace MusicOrganizer.Services;

/// <summary>
/// Saves and loads the user's last folders as JSON under
/// %AppData%\Gestionnaire Musique\settings.json, so the application remembers them
/// across launches.
/// </summary>
public static class SettingsPersistenceService
{
    private const string AppDataFolderName = "Gestionnaire Musique";
    private const string LegacyAppDataFolderName = "MusicOrganizer";

    private static string SettingsFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppDataFolderName);
            Directory.CreateDirectory(folder);
            var settingsPath = Path.Combine(folder, "settings.json");
            var legacySettingsPath = Path.Combine(appData, LegacyAppDataFolderName, "settings.json");
            if (!File.Exists(settingsPath) && File.Exists(legacySettingsPath))
            {
                File.Copy(legacySettingsPath, settingsPath);
            }

            return settingsPath;
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
