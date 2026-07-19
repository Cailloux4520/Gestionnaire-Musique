using System.Runtime.InteropServices;

namespace MusicOrganizer.Services;

public static class ShortcutService
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, nint lpSecurityAttributes);

    public static bool FolderContainsShortcutToTarget(string folder, string targetPath)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        foreach (var shortcutPath in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.TopDirectoryOnly))
        {
            var shortcutTarget = TryGetShortcutTarget(shortcutPath);
            if (!string.IsNullOrWhiteSpace(shortcutTarget) &&
                string.Equals(Path.GetFullPath(shortcutTarget), fullTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string CreateMusicFileLink(string targetPath, string linkPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? string.Empty);

        if (CreateHardLink(linkPath, targetPath, nint.Zero))
        {
            return "lien dur";
        }

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return "lien symbolique";
        }
        catch (Exception ex)
        {
            throw new IOException($"Impossible de créer un lien audio visible par VirtualDJ: {linkPath}", ex);
        }
    }

    public static void CreateShortcut(string targetPath, string shortcutPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath) ?? string.Empty);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new IOException("WScript.Shell n'est pas disponible pour créer les raccourcis.");
        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                throw new IOException("Impossible d'initialiser WScript.Shell.");
            }

            shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            shortcut.Save();
        }
        catch (Exception ex)
        {
            throw new IOException($"Impossible de créer le raccourci: {shortcutPath}", ex);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    public static string? TryGetShortcutTarget(string shortcutPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return null;
        }

        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath as string;
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}