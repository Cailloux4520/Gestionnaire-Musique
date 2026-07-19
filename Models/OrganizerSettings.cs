namespace MusicOrganizer.Models;

/// <summary>
/// Runtime settings used by the organizer services. This class is also serialized as JSON
/// to remember the last used folders between application launches.
/// </summary>
public sealed class OrganizerSettings
{
    public string SourceFolder { get; set; } = string.Empty;
    public string DestinationFolder { get; set; } = string.Empty;

    public bool Recursive { get; set; } = true;
    public bool UseTagsFirst { get; set; } = true;
    public bool SimulateOnly { get; set; } = false;
    public bool SendDuplicatesToRecycleBin { get; set; } = true;
    public bool WriteLogFile { get; set; } = false;
    public bool ShowErrorsOnly { get; set; } = false;
    public bool MoveCoverArt { get; set; } = true;
    public bool FixTags { get; set; } = false;
    public bool RenameFiles { get; set; } = true;
    public bool UseFingerprintDuplicates { get; set; } = true;
    public bool ExportCsv { get; set; } = false;
    public bool GeneratePlaylists { get; set; } = false;
    public bool FetchMusicBrainzMetadata { get; set; } = false;
    public bool FindOriginalYear { get; set; } = false;
    public bool CreateVirtualDjDateFileLinks { get; set; } = false;
    public bool CreateDateShortcuts { get; set; } = false;
    public bool CreateStyleShortcuts { get; set; } = false;
    public bool NormalizeArtists { get; set; } = true;
    public bool KeepPrimaryArtistOnly { get; set; } = true;
    public bool AnalyzeLibrary { get; set; } = true;
    public string FileNameTemplate { get; set; } = "{Artist} - {Title}";
    public string MusicBrainzServer { get; set; } = "https://musicbrainz.org";

    public OrganizerSettings Clone() => new()
    {
        SourceFolder = SourceFolder,
        DestinationFolder = DestinationFolder,
        Recursive = Recursive,
        UseTagsFirst = UseTagsFirst,
        SimulateOnly = SimulateOnly,
        SendDuplicatesToRecycleBin = SendDuplicatesToRecycleBin,
        WriteLogFile = WriteLogFile,
        ShowErrorsOnly = ShowErrorsOnly,
        MoveCoverArt = MoveCoverArt,
        FixTags = FixTags,
        RenameFiles = RenameFiles,
        UseFingerprintDuplicates = UseFingerprintDuplicates,
        ExportCsv = ExportCsv,
        GeneratePlaylists = GeneratePlaylists,
        FetchMusicBrainzMetadata = FetchMusicBrainzMetadata,
        FindOriginalYear = FindOriginalYear,
        CreateVirtualDjDateFileLinks = CreateVirtualDjDateFileLinks,
        CreateDateShortcuts = CreateDateShortcuts,
        CreateStyleShortcuts = CreateStyleShortcuts,
        NormalizeArtists = NormalizeArtists,
        KeepPrimaryArtistOnly = KeepPrimaryArtistOnly,
        AnalyzeLibrary = AnalyzeLibrary,
        FileNameTemplate = FileNameTemplate,
        MusicBrainzServer = MusicBrainzServer
    };
}
