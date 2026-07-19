using System.Diagnostics;
using System.Text;
using MusicOrganizer.Models;
using MusicOrganizer.Services;

namespace MusicOrganizer;

public partial class MainForm : Form
{
    private enum ProcessingMode
    {
        Artist,
        DateLink,
        DateFile,
        Style,
        Sort
    }

    private OrganizerSettings _settings = new();
    private CancellationTokenSource? _cts;
    private LoggingService? _loggingService;
    private OrganizerService? _organizerService;
    private bool _isRunning;
    private ProcessingMode _selectedMode = ProcessingMode.Artist;
    private ProcessingMode _runningMode = ProcessingMode.Artist;

    // Keep the visible log bounded so a 100,000+ file run never bloats the UI's memory,
    // while still showing the most recent activity in real time.
    private const int MaxVisibleLogLines = 3000;
    private readonly StringBuilder _visibleLogBuffer = new();
    private int _visibleLogLineCount;

    public MainForm()
    {
        InitializeComponent();
        TryApplyIcon();
        WireEvents();
        LoadPersistedSettings();
    }

    private void TryApplyIcon()
    {
        try
        {
            var exePath = Application.ExecutablePath;
            if (File.Exists(exePath))
            {
                Icon = Icon.ExtractAssociatedIcon(exePath);
            }
        }
        catch
        {
            // Cosmetic only - never let icon loading break startup.
        }
    }

    private void WireEvents()
    {
        _btnBrowseSource.Click += (_, _) => BrowseFolder(_txtSource);
        _btnBrowseDest.Click += (_, _) => BrowseFolder(_txtDest);
        _btnStart.Click += async (_, _) => await StartProcessingAsync();
        _btnShowFolderStats.Click += (_, _) => ShowFolderStatistics();
        _btnModeArtist.Click += (_, _) => SelectMode(ProcessingMode.Artist);
        _btnModeDate.Click += (_, _) => SelectMode(ProcessingMode.DateLink);
        _btnModeDateFile.Click += (_, _) => SelectMode(ProcessingMode.DateFile);
        _btnModeStyle.Click += (_, _) => SelectMode(ProcessingMode.Style);
        _btnModeSort.Click += (_, _) => SelectMode(ProcessingMode.Sort);
        _btnStop.Click += (_, _) => _cts?.Cancel();
        _btnOpenDest.Click += (_, _) => OpenInExplorer(_txtDest.Text);

        EnableFolderDrop(_txtSource);
        EnableFolderDrop(_txtDest);

        FormClosing += MainForm_FormClosing;
        SelectMode(ProcessingMode.Artist);
    }

    private static void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Sélectionnez un dossier",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(target.Text) ? target.Text : string.Empty
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private void LoadPersistedSettings()
    {
        var savedSettings = SettingsPersistenceService.Load();
        _settings = savedSettings;

        _txtSource.Text = _settings.SourceFolder;
        _txtDest.Text = _settings.DestinationFolder;
        _chkRecursive.Checked = _settings.Recursive;
        _chkUseTags.Checked = _settings.UseTagsFirst;
        _chkMoveCoverArt.Checked = _settings.MoveCoverArt;
        _chkFixTags.Checked = _settings.FixTags;
        _chkFingerprintDuplicates.Checked = _settings.UseFingerprintDuplicates;
        _chkFetchMusicBrainz.Checked = _settings.FetchMusicBrainzMetadata;
        _chkFindOriginalYear.Checked = _settings.FindOriginalYear;
        _chkNormalizeArtists.Checked = _settings.NormalizeArtists;
        _chkPrimaryArtistOnly.Checked = _settings.KeepPrimaryArtistOnly;
        _chkAnalyzeLibrary.Checked = _settings.AnalyzeLibrary;
        _chkStyleRecursive.Checked = _settings.Recursive;
        _chkStyleUseTags.Checked = _settings.UseTagsFirst;
        _chkStyleFetchMusicBrainz.Checked = _settings.FetchMusicBrainzMetadata;
        _chkSortRecursive.Checked = _settings.Recursive;
        _chkSortUseTags.Checked = _settings.UseTagsFirst;
        _chkSortFixTags.Checked = _settings.FixTags;
        _chkSortFingerprintDuplicates.Checked = _settings.UseFingerprintDuplicates;
        _chkSortNormalizeArtists.Checked = _settings.NormalizeArtists;
        _chkSortPrimaryArtistOnly.Checked = _settings.KeepPrimaryArtistOnly;
        UpdateModeControls();
    }

    private void PersistCurrentSettings()
    {
        _settings = new OrganizerSettings();
        _settings.SourceFolder = _txtSource.Text.Trim();
        _settings.DestinationFolder = _txtDest.Text.Trim();
        _settings.Recursive = GetRecursiveOption();
        _settings.UseTagsFirst = GetUseTagsOption();
        _settings.SimulateOnly = false;
        _settings.SendDuplicatesToRecycleBin = true;
        _settings.WriteLogFile = false;
        _settings.ShowErrorsOnly = false;
        _settings.MoveCoverArt = _chkMoveCoverArt.Checked;
        _settings.FixTags = GetFixTagsOption();
        _settings.RenameFiles = true;
        _settings.UseFingerprintDuplicates = GetFingerprintDuplicatesOption();
        _settings.ExportCsv = false;
        _settings.GeneratePlaylists = false;
        _settings.FetchMusicBrainzMetadata = GetFetchMusicBrainzOption();
        _settings.FindOriginalYear = IsDateMode() && _chkFindOriginalYear.Checked;
        _settings.CreateVirtualDjDateFileLinks = _selectedMode == ProcessingMode.DateFile;
        _settings.CreateDateShortcuts = false;
        _settings.CreateStyleShortcuts = false;
        _settings.NormalizeArtists = GetNormalizeArtistsOption();
        _settings.KeepPrimaryArtistOnly = GetPrimaryArtistOnlyOption();
        _settings.AnalyzeLibrary = _chkAnalyzeLibrary.Checked;

        SettingsPersistenceService.Save(_settings);
    }

    private async Task StartProcessingAsync()
    {
        if (_isRunning)
        {
            return; // Guard: never allow two processing runs at once.
        }

        switch (_selectedMode)
        {
            case ProcessingMode.Artist:
                await RunArtistActionAsync();
                return;
            case ProcessingMode.DateLink:
            case ProcessingMode.DateFile:
                await RunDateActionAsync();
                return;
            case ProcessingMode.Style:
                await RunStyleActionAsync();
                return;
            case ProcessingMode.Sort:
                await SortDuplicatesAndArtistsAsync();
                return;
        }
    }

    private async Task RunStyleActionAsync()
    {
        if (_isRunning)
        {
            return;
        }

        PersistCurrentSettings();

        if (!Directory.Exists(_settings.SourceFolder))
        {
            MessageBox.Show(this, "Le dossier source n'existe pas ou n'est pas renseigné.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        _runningMode = ProcessingMode.Style;
        SetUiRunningState(true);
        ClearLog();
        DeleteLegacyLogFile(_settings.SourceFolder);

        _cts = new CancellationTokenSource();
        _loggingService = new LoggingService(string.Empty, writeToFile: false, errorsOnlyToUi: false);
        _loggingService.EntryLogged += OnLogEntry;
        _organizerService = new OrganizerService(_loggingService);

        Exception? failure = null;
        try
        {
            var styleService = new StyleTagCorrectionService(_loggingService);
            var progress = new Progress<ProgressInfo>(OnProgress);
            await styleService.RunAsync(_settings, _organizerService.Stats, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user clicks Arrêter.
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (_loggingService is not null)
            {
                _loggingService.EntryLogged -= OnLogEntry;
                await _loggingService.FlushAndCloseAsync();
                _loggingService.Dispose();
            }

            _isRunning = false;
            SetUiRunningState(false);
            _cts?.Dispose();
            _cts = null;
        }

        if (failure is not null)
        {
            MessageBox.Show(this, $"La correction des styles s'est arrêtée sur une erreur inattendue :\n{failure.Message}",
                "MusicOrganizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            ShowFinalReport();
        }
    }

    private async Task RunDateActionAsync()
    {
        PersistCurrentSettings();

        if (!Directory.Exists(_settings.SourceFolder))
        {
            MessageBox.Show(this, "Le dossier source n'existe pas ou n'est pas renseigné.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.DestinationFolder))
        {
            MessageBox.Show(this, "Veuillez indiquer un dossier de destination.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_settings.SimulateOnly)
        {
            Directory.CreateDirectory(_settings.DestinationFolder);
        }

        var actionSettings = _settings.Clone();
        actionSettings.FindOriginalYear = true;
        actionSettings.FetchMusicBrainzMetadata = true;
        actionSettings.CreateVirtualDjDateFileLinks = _selectedMode == ProcessingMode.DateFile;
        actionSettings.CreateDateShortcuts = false;
        actionSettings.CreateStyleShortcuts = false;

        await RunOrganizerActionAsync(actionSettings, "vérification de date exacte");
    }

    private async Task RunArtistActionAsync()
    {
        PersistCurrentSettings();
        var artistDestination = GetModeDestinationFolder("Artiste");

        if (!Directory.Exists(_settings.SourceFolder))
        {
            MessageBox.Show(this, "Le dossier source n'existe pas ou n'est pas renseigné.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.DestinationFolder))
        {
            MessageBox.Show(this, "Veuillez indiquer un dossier de destination.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_settings.SimulateOnly)
        {
            Directory.CreateDirectory(artistDestination);
        }

        var actionSettings = _settings.Clone();
        actionSettings.DestinationFolder = artistDestination;
        actionSettings.FindOriginalYear = false;
        actionSettings.CreateDateShortcuts = false;
        actionSettings.CreateStyleShortcuts = false;

        await RunOrganizerActionAsync(actionSettings, "création par artiste");
    }

    private string GetModeDestinationFolder(string folderName)
    {
        return Path.Combine(_settings.DestinationFolder.Trim(), folderName);
    }

    private void ShowFolderStatistics()
    {
        PersistCurrentSettings();

        if (!Directory.Exists(_settings.SourceFolder))
        {
            MessageBox.Show(this, "Le dossier source n'existe pas ou n'est pas renseigné.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var rows = FolderStatisticsService.CountMusicFilesByFolder(_settings.DestinationFolder);
        using var form = new FolderStatisticsForm(rows);
        form.ShowDialog(this);
    }

    private async Task RunOrganizerActionAsync(OrganizerSettings actionSettings, string actionLabel)
    {
        _runningMode = _selectedMode;
        _isRunning = true;
        SetUiRunningState(true);
        ClearLog();
        DeleteLegacyLogFile(actionSettings.DestinationFolder);

        _cts = new CancellationTokenSource();
        _loggingService = new LoggingService(string.Empty, writeToFile: false, errorsOnlyToUi: false);
        _loggingService.EntryLogged += OnLogEntry;
        _organizerService = new OrganizerService(_loggingService);

        Exception? failure = null;
        try
        {
            var progress = new Progress<ProgressInfo>(OnProgress);
            await _organizerService.RunAsync(actionSettings, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user clicks Arrêter.
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (_loggingService is not null)
            {
                _loggingService.EntryLogged -= OnLogEntry;
                await _loggingService.FlushAndCloseAsync();
                _loggingService.Dispose();
            }

            _isRunning = false;
            SetUiRunningState(false);
            _cts?.Dispose();
            _cts = null;
        }

        if (failure is not null)
        {
            MessageBox.Show(this, $"La {actionLabel} s'est arrêtée sur une erreur inattendue :\n{failure.Message}",
                "MusicOrganizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            ShowFinalReport();
        }
    }

    private async Task SortDuplicatesAndArtistsAsync()
    {
        if (_isRunning)
        {
            return;
        }

        PersistCurrentSettings();

        if (!Directory.Exists(_settings.SourceFolder))
        {
            MessageBox.Show(this, "Le dossier source n'existe pas ou n'est pas renseigné.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        _runningMode = ProcessingMode.Sort;
        SetUiRunningState(true);
        ClearLog();
        DeleteLegacyLogFile(_settings.SourceFolder);

        _cts = new CancellationTokenSource();
        _loggingService = new LoggingService(string.Empty, writeToFile: false, errorsOnlyToUi: false);
        _loggingService.EntryLogged += OnLogEntry;
        _organizerService = new OrganizerService(_loggingService);

        Exception? failure = null;
        try
        {
            var sorter = new DuplicateArtistSorterService(_loggingService);
            var progress = new Progress<ProgressInfo>(OnProgress);
            await sorter.RunAsync(_settings, _organizerService.Stats, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user clicks Arrêter.
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (_loggingService is not null)
            {
                _loggingService.EntryLogged -= OnLogEntry;
                await _loggingService.FlushAndCloseAsync();
                _loggingService.Dispose();
            }

            _isRunning = false;
            SetUiRunningState(false);
            _cts?.Dispose();
            _cts = null;
        }

        if (failure is not null)
        {
            MessageBox.Show(this, $"Le tri s'est arrêté sur une erreur inattendue :\n{failure.Message}",
                "MusicOrganizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            ShowFinalReport();
        }
    }

    private void ShowFinalReport()
    {
        if (_organizerService is null)
        {
            return;
        }

        var stats = _organizerService.Stats;
        var report = BuildFinalReport(stats, _runningMode);

        AppendLogLine($"--- RAPPORT FINAL ---\n{report}");

        MessageBox.Show(this, report, "MusicOrganizer - Rapport final", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string BuildFinalReport(OrganizerStats stats, ProcessingMode mode)
    {
        var lines = new List<string>
        {
            $"Temps total : {stats.Stopwatch.Elapsed:hh\\:mm\\:ss}",
            $"Fichiers analysés : {stats.FilesAnalyzed:N0}"
        };

        switch (mode)
        {
            case ProcessingMode.Artist:
                lines.Add($"Fichiers déplacés : {stats.FilesMoved:N0}");
                lines.Add($"Doublons détectés : {stats.DuplicatesFound:N0}");
                lines.Add($"Doublons par empreinte : {stats.FingerprintDuplicatesFound:N0}");
                lines.Add($"Fichiers supprimés (Corbeille) : {stats.DuplicatesDeleted:N0}");
                lines.Add($"Fichiers ignorés : {stats.FilesIgnored:N0}");
                lines.Add($"Dossiers créés : {stats.FoldersCreated:N0}");
                lines.Add($"Pochettes copiées : {stats.CoverFilesMoved:N0}");
                lines.Add($"Artistes normalisés : {stats.ArtistNamesNormalized:N0}");
                lines.Add($"Morceaux sans pochette : {stats.TracksWithoutCover:N0}");
                lines.Add($"Artistes / albums / morceaux : {stats.ArtistCount:N0} / {stats.AlbumCount:N0} / {stats.TrackCount:N0}");
                lines.Add($"Albums possiblement incomplets : {stats.IncompleteAlbums:N0}");
                lines.Add($"Taille bibliothèque traitée : {FormatBytes(stats.TotalLibraryBytes)}");
                break;
            case ProcessingMode.DateLink:
            case ProcessingMode.DateFile:
                lines.Add($"Années exactes trouvées : {stats.OriginalYearsFound:N0}");
                lines.Add($"Raccourcis date créés : {stats.DateShortcutsCreated:N0}");
                lines.Add($"Dossiers créés : {stats.FoldersCreated:N0}");
                lines.Add($"Fichiers ignorés : {stats.FilesIgnored:N0}");
                break;
            case ProcessingMode.Style:
                lines.Add($"Styles corrigés : {stats.TagsFixed:N0}");
                lines.Add($"Fichiers ignorés : {stats.FilesIgnored:N0}");
                break;
            case ProcessingMode.Sort:
                lines.Add($"Artistes triés : {stats.ArtistsSorted:N0}");
                lines.Add($"Fichiers déplacés : {stats.FilesMoved:N0}");
                lines.Add($"Doublons détectés : {stats.DuplicatesFound:N0}");
                lines.Add($"Fichiers supprimés (Corbeille) : {stats.DuplicatesDeleted:N0}");
                lines.Add($"Dossiers vides supprimés : {stats.EmptyFoldersDeleted:N0}");
                lines.Add($"Espace disque libéré : {FormatBytes(stats.BytesFreed)}");
                break;
        }

        lines.Add($"Erreurs : {stats.Errors:N0}");
        return string.Join("\n", lines);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.##} {units[unitIndex]}";
    }

    private void SetUiRunningState(bool running)
    {
        _btnStart.Enabled = !running;
        _btnStop.Enabled = running;
        _btnModeArtist.Enabled = !running;
        _btnModeDate.Enabled = !running;
        _btnModeDateFile.Enabled = !running;
        _btnModeStyle.Enabled = !running;
        _btnModeSort.Enabled = !running;
        _btnShowFolderStats.Enabled = !running;
        _btnOpenDest.Enabled = !running && IsDestinationFolderUsed();
        _txtSource.Enabled = !running && IsSourceFolderUsed();
        _txtDest.Enabled = !running && IsDestinationFolderUsed();
        _btnBrowseSource.Enabled = !running && IsSourceFolderUsed();
        _btnBrowseDest.Enabled = !running && IsDestinationFolderUsed();
        UpdateOptionControls(!running);

        if (!running)
        {
            _progressBar.Style = ProgressBarStyle.Blocks;
        }
    }

    private void UpdateModeControls()
    {
        if (_isRunning)
        {
            return;
        }

        var sourceUsed = IsSourceFolderUsed();
        var destinationUsed = IsDestinationFolderUsed();
        _txtSource.Enabled = sourceUsed;
        _btnBrowseSource.Enabled = sourceUsed;
        _txtSource.BackColor = sourceUsed ? Color.White : Color.FromArgb(235, 235, 235);
        _txtDest.Enabled = destinationUsed;
        _btnBrowseDest.Enabled = destinationUsed;
        _btnOpenDest.Enabled = destinationUsed;
        _txtDest.BackColor = destinationUsed ? Color.White : Color.FromArgb(235, 235, 235);
        UpdateOptionControls(enabled: true);
    }

    private void UpdateOptionControls(bool enabled)
    {
        _chkRecursive.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkUseTags.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkMoveCoverArt.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkFixTags.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkFingerprintDuplicates.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkFetchMusicBrainz.Enabled = enabled && IsDateMode();
        _chkFindOriginalYear.Enabled = enabled && IsDateMode();
        _chkNormalizeArtists.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkPrimaryArtistOnly.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkAnalyzeLibrary.Enabled = enabled && _selectedMode == ProcessingMode.Artist;
        _chkStyleRecursive.Enabled = enabled && _selectedMode == ProcessingMode.Style;
        _chkStyleUseTags.Enabled = enabled && _selectedMode == ProcessingMode.Style;
        _chkStyleFetchMusicBrainz.Enabled = enabled && _selectedMode == ProcessingMode.Style;
        _chkSortRecursive.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
        _chkSortUseTags.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
        _chkSortFixTags.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
        _chkSortFingerprintDuplicates.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
        _chkSortNormalizeArtists.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
        _chkSortPrimaryArtistOnly.Enabled = enabled && _selectedMode == ProcessingMode.Sort;
    }

    private bool GetRecursiveOption() => _selectedMode switch
    {
        ProcessingMode.Style => _chkStyleRecursive.Checked,
        ProcessingMode.Sort => _chkSortRecursive.Checked,
        _ => _chkRecursive.Checked
    };

    private bool GetUseTagsOption() => _selectedMode switch
    {
        ProcessingMode.Style => _chkStyleUseTags.Checked,
        ProcessingMode.Sort => _chkSortUseTags.Checked,
        _ => _chkUseTags.Checked
    };

    private bool GetFetchMusicBrainzOption() => _selectedMode switch
    {
        ProcessingMode.Style => _chkStyleFetchMusicBrainz.Checked,
        _ => IsDateMode() && _chkFetchMusicBrainz.Checked
    };

    private bool GetFixTagsOption() => _selectedMode switch
    {
        ProcessingMode.Sort => _chkSortFixTags.Checked,
        _ => _chkFixTags.Checked
    };

    private bool GetFingerprintDuplicatesOption() => _selectedMode switch
    {
        ProcessingMode.Sort => _chkSortFingerprintDuplicates.Checked,
        _ => _chkFingerprintDuplicates.Checked
    };

    private bool GetNormalizeArtistsOption() => _selectedMode switch
    {
        ProcessingMode.Sort => _chkSortNormalizeArtists.Checked,
        _ => _chkNormalizeArtists.Checked
    };

    private bool GetPrimaryArtistOnlyOption() => _selectedMode switch
    {
        ProcessingMode.Sort => _chkSortPrimaryArtistOnly.Checked,
        _ => _chkPrimaryArtistOnly.Checked
    };

    private void SelectMode(ProcessingMode mode)
    {
        _selectedMode = mode;
        _btnModeArtist.SelectedMode = mode == ProcessingMode.Artist;
        _btnModeDate.SelectedMode = mode == ProcessingMode.DateLink;
        _btnModeDateFile.SelectedMode = mode == ProcessingMode.DateFile;
        _btnModeStyle.SelectedMode = mode == ProcessingMode.Style;
        _btnModeSort.SelectedMode = mode == ProcessingMode.Sort;

        if (IsDateMode())
        {
            _chkFetchMusicBrainz.Checked = true;
        }

        if (mode == ProcessingMode.Style)
        {
            _chkStyleFetchMusicBrainz.Checked = true;
        }

        if (IsDateMode())
        {
            _chkFindOriginalYear.Checked = true;
        }

        _btnModeArtist.Invalidate();
        _btnModeDate.Invalidate();
        _btnModeDateFile.Invalidate();
        _btnModeStyle.Invalidate();
        _btnModeSort.Invalidate();
        UpdateModeControls();
    }

    private bool IsSourceFolderUsed()
    {
        return _selectedMode switch
        {
            ProcessingMode.Artist or ProcessingMode.DateLink or ProcessingMode.DateFile or ProcessingMode.Style or ProcessingMode.Sort => true,
            _ => false
        };
    }

    private bool IsDestinationFolderUsed()
    {
        return _selectedMode switch
        {
            ProcessingMode.Artist or ProcessingMode.DateLink or ProcessingMode.DateFile => true,
            _ => false
        };
    }

    private bool IsDateMode()
    {
        return _selectedMode is ProcessingMode.DateLink or ProcessingMode.DateFile;
    }

    private void OnProgress(ProgressInfo info)
    {
        _lblElapsedValue.Text = info.Elapsed.ToString(@"hh\:mm\:ss");
        _lblAnalyzedValue.Text = info.FilesAnalyzed.ToString("N0");
        _lblMovedValue.Text = info.FilesMoved.ToString("N0");
        _lblIgnoredValue.Text = info.FilesIgnored.ToString("N0");
        _lblDuplicatesValue.Text = info.DuplicatesFound.ToString("N0");
        _lblSpeedValue.Text = $"{info.FilesPerSecond:0.#} f/s";

        if (info.TotalFiles > 0)
        {
            _progressBar.Style = ProgressBarStyle.Blocks;
            var percent = (int)Math.Clamp(info.FilesAnalyzed * 100 / info.TotalFiles, 0, 100);
            _progressBar.Value = percent;
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
        }
    }

    private void OnLogEntry(LogEntry entry)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnLogEntry(entry));
            return;
        }

        var prefix = entry.Severity switch
        {
            LogSeverity.Error => "[ERREUR] ",
            LogSeverity.Duplicate => "[DOUBLON] ",
            LogSeverity.Ignored => "[IGNORÉ] ",
            LogSeverity.Moved => "[OK] ",
            LogSeverity.Summary => "[RÉSUMÉ] ",
            _ => ""
        };

        AppendLogLine($"{entry.Timestamp:HH:mm:ss} {GetLogFileColumn(entry),-38} {prefix}{entry.Reason}");
    }

    private static string GetLogFileColumn(LogEntry entry)
    {
        var path = !string.IsNullOrWhiteSpace(entry.OldPath) ? entry.OldPath : entry.NewPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "-";
        }

        var fileName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "-";
        }

        return fileName.Length <= 38 ? fileName : string.Concat(fileName.AsSpan(0, 35), "...");
    }

    private void ClearLog()
    {
        _visibleLogBuffer.Clear();
        _visibleLogLineCount = 0;
        _txtLog.Clear();
        AppendLogLine($"{"Heure",-8} {"Fichier",-38} Message");
        AppendLogLine($"{new string('-', 8)} {new string('-', 38)} {new string('-', 60)}");
    }

    private static void DeleteLegacyLogFile(string destinationFolder)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            return;
        }

        var logPath = Path.Combine(destinationFolder, "MusicOrganizer.log");
        try
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
        catch
        {
            // Best effort: a locked legacy log must not block the music operation.
        }
    }

    private void AppendLogLine(string line)
    {
        _visibleLogBuffer.AppendLine(line);
        _visibleLogLineCount++;

        if (_visibleLogLineCount > MaxVisibleLogLines)
        {
            // Drop the oldest quarter of the buffer to keep the control responsive.
            var text = _visibleLogBuffer.ToString();
            var lines = text.Split('\n');
            var keep = lines.Skip(lines.Length / 4);
            _visibleLogBuffer.Clear();
            _visibleLogBuffer.Append(string.Join('\n', keep));
            _visibleLogLineCount = lines.Length - lines.Length / 4;
        }

        _txtLog.Text = _visibleLogBuffer.ToString();
        _txtLog.SelectionStart = _txtLog.Text.Length;
        _txtLog.ScrollToCaret();
    }

    private void OpenInExplorer(string folder)
    {
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Ce dossier n'existe pas encore.", "MusicOrganizer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        PersistCurrentSettings();

        if (_isRunning)
        {
            var result = MessageBox.Show(this,
                "Un traitement est en cours. Voulez-vous vraiment quitter et l'interrompre ?",
                "MusicOrganizer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            _cts?.Cancel();
        }
    }

    private void EnableFolderDrop(TextBox target)
    {
        target.AllowDrop = true;
        target.DragEnter += (_, e) =>
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        };
        target.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var folder = paths.FirstOrDefault(Directory.Exists);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    target.Text = folder;
                }
            }
        };
    }

    private void ApplyTheme(bool darkMode)
    {
        var back = darkMode ? Color.FromArgb(30, 34, 40) : Color.FromArgb(245, 246, 248);
        var panel = darkMode ? Color.FromArgb(38, 43, 50) : SystemColors.Control;
        var text = darkMode ? Color.FromArgb(235, 238, 242) : SystemColors.ControlText;
        var input = darkMode ? Color.FromArgb(24, 27, 32) : Color.White;

        BackColor = back;
        ApplyThemeToControl(this, back, panel, text, input);
    }

    private static void ApplyThemeToControl(Control control, Color back, Color panel, Color text, Color input)
    {
        foreach (Control child in control.Controls)
        {
            child.ForeColor = text;
            child.BackColor = child switch
            {
                TextBox => input,
                GroupBox => panel,
                TableLayoutPanel => panel,
                FlowLayoutPanel => panel,
                Panel => back,
                _ => child.BackColor
            };

            if (child is Button button && button.FlatStyle != FlatStyle.Flat)
            {
                button.BackColor = panel;
            }

            ApplyThemeToControl(child, back, panel, text, input);
        }
    }

    private void CleanupOnDispose()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
