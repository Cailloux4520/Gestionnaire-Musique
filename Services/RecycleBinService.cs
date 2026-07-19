using Microsoft.VisualBasic.FileIO;

namespace MusicOrganizer.Services;

/// <summary>
/// The ONLY way MusicOrganizer ever removes a file. Every "deletion" in this
/// application is in fact a move to the Windows Recycle Bin - nothing is ever
/// permanently erased, so any decision the tool makes can be undone by the user.
/// </summary>
public static class RecycleBinService
{
    public static void SendToRecycleBin(string path)
    {
        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin);
    }
}
