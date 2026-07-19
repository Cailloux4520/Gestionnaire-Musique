using System.Security.Cryptography;

namespace MusicOrganizer.Services;

public static class AudioFingerprintService
{
    public static async Task<string> ComputeAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}