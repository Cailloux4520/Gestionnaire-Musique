using System.Diagnostics;

namespace MusicOrganizer.Models;

/// <summary>
/// Thread-safe running statistics for the current (or last) processing session.
/// All mutators use Interlocked so they can be called concurrently from the
/// parallel processing pipeline without any external locking.
/// </summary>
public sealed class OrganizerStats
{
    private long _filesAnalyzed;
    private long _filesMoved;
    private long _filesIgnored;
    private long _duplicatesFound;
    private long _duplicatesDeleted;
    private long _errors;
    private long _foldersCreated;
    private long _bytesFreed;
    private long _coverFilesMoved;
    private long _tagsFixed;
    private long _fingerprintDuplicatesFound;
    private long _tracksWithoutCover;
    private long _incompleteAlbums;
    private long _artistNamesNormalized;
    private long _originalYearsFound;
    private long _originalYearsUpdated;
    private long _dateShortcutsCreated;
    private long _styleShortcutsCreated;
    private long _artistsSorted;
    private long _emptyFoldersDeleted;

    public readonly Stopwatch Stopwatch = new();

    public long FilesAnalyzed => Interlocked.Read(ref _filesAnalyzed);
    public long FilesMoved => Interlocked.Read(ref _filesMoved);
    public long FilesIgnored => Interlocked.Read(ref _filesIgnored);
    public long DuplicatesFound => Interlocked.Read(ref _duplicatesFound);
    public long DuplicatesDeleted => Interlocked.Read(ref _duplicatesDeleted);
    public long Errors => Interlocked.Read(ref _errors);
    public long FoldersCreated => Interlocked.Read(ref _foldersCreated);
    public long BytesFreed => Interlocked.Read(ref _bytesFreed);
    public long CoverFilesMoved => Interlocked.Read(ref _coverFilesMoved);
    public long TagsFixed => Interlocked.Read(ref _tagsFixed);
    public long FingerprintDuplicatesFound => Interlocked.Read(ref _fingerprintDuplicatesFound);
    public long TracksWithoutCover => Interlocked.Read(ref _tracksWithoutCover);
    public long IncompleteAlbums => Interlocked.Read(ref _incompleteAlbums);
    public long ArtistNamesNormalized => Interlocked.Read(ref _artistNamesNormalized);
    public long OriginalYearsFound => Interlocked.Read(ref _originalYearsFound);
    public long OriginalYearsUpdated => Interlocked.Read(ref _originalYearsUpdated);
    public long DateShortcutsCreated => Interlocked.Read(ref _dateShortcutsCreated);
    public long StyleShortcutsCreated => Interlocked.Read(ref _styleShortcutsCreated);
    public long ArtistsSorted => Interlocked.Read(ref _artistsSorted);
    public long EmptyFoldersDeleted => Interlocked.Read(ref _emptyFoldersDeleted);

    public long ArtistCount { get; set; }
    public long AlbumCount { get; set; }
    public long TrackCount { get; set; }
    public long TotalLibraryBytes { get; set; }

    public long TotalFilesFound { get; set; }

    public void IncrementAnalyzed() => Interlocked.Increment(ref _filesAnalyzed);
    public void IncrementMoved() => Interlocked.Increment(ref _filesMoved);
    public void IncrementIgnored() => Interlocked.Increment(ref _filesIgnored);
    public void IncrementDuplicatesFound() => Interlocked.Increment(ref _duplicatesFound);
    public void IncrementDuplicatesDeleted() => Interlocked.Increment(ref _duplicatesDeleted);
    public void IncrementErrors() => Interlocked.Increment(ref _errors);
    public void IncrementFoldersCreated() => Interlocked.Increment(ref _foldersCreated);
    public void AddBytesFreed(long bytes) => Interlocked.Add(ref _bytesFreed, bytes);
    public void IncrementCoverFilesMoved() => Interlocked.Increment(ref _coverFilesMoved);
    public void IncrementTagsFixed() => Interlocked.Increment(ref _tagsFixed);
    public void IncrementFingerprintDuplicatesFound() => Interlocked.Increment(ref _fingerprintDuplicatesFound);
    public void IncrementTracksWithoutCover() => Interlocked.Increment(ref _tracksWithoutCover);
    public void SetIncompleteAlbums(long count) => Interlocked.Exchange(ref _incompleteAlbums, count);
    public void IncrementArtistNamesNormalized() => Interlocked.Increment(ref _artistNamesNormalized);
    public void IncrementOriginalYearsFound() => Interlocked.Increment(ref _originalYearsFound);
    public void IncrementOriginalYearsUpdated() => Interlocked.Increment(ref _originalYearsUpdated);
    public void IncrementDateShortcutsCreated() => Interlocked.Increment(ref _dateShortcutsCreated);
    public void IncrementStyleShortcutsCreated() => Interlocked.Increment(ref _styleShortcutsCreated);
    public void IncrementArtistsSorted() => Interlocked.Increment(ref _artistsSorted);
    public void IncrementEmptyFoldersDeleted() => Interlocked.Increment(ref _emptyFoldersDeleted);

    public double FilesPerSecond
    {
        get
        {
            var seconds = Stopwatch.Elapsed.TotalSeconds;
            return seconds > 0 ? FilesAnalyzed / seconds : 0;
        }
    }
}
