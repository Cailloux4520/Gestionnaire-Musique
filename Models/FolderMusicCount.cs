namespace MusicOrganizer.Models;

public sealed class FolderMusicCount
{
    public required string FolderName { get; init; }
    public required string FullPath { get; init; }
    public int MusicFileCount { get; init; }
}