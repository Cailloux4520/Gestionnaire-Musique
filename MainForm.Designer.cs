namespace MusicOrganizer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                CleanupOnDispose();
            }
            base.Dispose(disposing);
        }

        // ----- Controls -----
        private TableLayoutPanel _rootLayout = null!;
        private GroupBox _grpFolders = null!;
        private TextBox _txtSource = null!;
        private TextBox _txtDest = null!;
        private Button _btnBrowseSource = null!;
        private Button _btnBrowseDest = null!;

        private GroupBox _grpModes = null!;
        private RoundedButton _btnModeArtist = null!;
        private RoundedButton _btnModeDate = null!;
        private RoundedButton _btnModeDateFile = null!;
        private RoundedButton _btnModeStyle = null!;
        private RoundedButton _btnModeSort = null!;

        private GroupBox _grpOptions = null!;
        private TableLayoutPanel _optionsLayout = null!;
        private CheckBox _chkRecursive = null!;
        private CheckBox _chkUseTags = null!;
        private CheckBox _chkMoveCoverArt = null!;
        private CheckBox _chkFixTags = null!;
        private CheckBox _chkFingerprintDuplicates = null!;
        private CheckBox _chkFetchMusicBrainz = null!;
        private CheckBox _chkFindOriginalYear = null!;
        private CheckBox _chkNormalizeArtists = null!;
        private CheckBox _chkPrimaryArtistOnly = null!;
        private CheckBox _chkAnalyzeLibrary = null!;
        private CheckBox _chkStyleRecursive = null!;
        private CheckBox _chkStyleUseTags = null!;
        private CheckBox _chkStyleFetchMusicBrainz = null!;
        private CheckBox _chkSortRecursive = null!;
        private CheckBox _chkSortUseTags = null!;
        private CheckBox _chkSortFixTags = null!;
        private CheckBox _chkSortFingerprintDuplicates = null!;
        private CheckBox _chkSortNormalizeArtists = null!;
        private CheckBox _chkSortPrimaryArtistOnly = null!;

        private FlowLayoutPanel _pnlActions = null!;
        private RoundedButton _btnStart = null!;
        private RoundedButton _btnStop = null!;
        private RoundedButton _btnShowFolderStats = null!;
        private Button _btnOpenDest = null!;

        private ProgressBar _progressBar = null!;

        private GroupBox _grpStats = null!;
        private Label _lblElapsedValue = null!;
        private Label _lblAnalyzedValue = null!;
        private Label _lblMovedValue = null!;
        private Label _lblIgnoredValue = null!;
        private Label _lblDuplicatesValue = null!;
        private Label _lblSpeedValue = null!;

        private GroupBox _grpLog = null!;
        private TextBox _txtLog = null!;

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            SuspendLayout();

            Text = "Gestionnaire Musique";
            MinimumSize = new Size(1220, 760);
            Size = new Size(1380, 900);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.FromArgb(245, 246, 248);

            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                Padding = new Padding(14),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            BuildFoldersGroup();
            BuildModesGroup();
            BuildOptionsGroup();
            BuildActionsPanel();
            BuildProgressBar();
            BuildStatsGroup();
            BuildLogGroup();

            _rootLayout.Controls.Add(_grpFolders, 0, 0);
            _rootLayout.Controls.Add(_grpModes, 0, 1);
            _rootLayout.Controls.Add(_grpOptions, 0, 2);
            _rootLayout.Controls.Add(_pnlActions, 0, 3);
            _rootLayout.Controls.Add(_progressBar, 0, 4);
            _rootLayout.Controls.Add(_grpStats, 0, 5);
            _rootLayout.Controls.Add(_grpLog, 0, 6);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            scrollHost.Controls.Add(_rootLayout);
            Controls.Add(scrollHost);

            ResumeLayout(false);
        }

        private void BuildFoldersGroup()
        {
            _grpFolders = new GroupBox
            {
                Text = "Dossiers",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 5, 10, 10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblSource = new Label { Text = "Dossier source :", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) };
            _txtSource = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 3) };
            _btnBrowseSource = new Button { Text = "Parcourir...", AutoSize = true, Margin = new Padding(3, 3, 3, 3) };

            var lblDest = new Label { Text = "Dossier destination :", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) };
            _txtDest = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 3) };
            _btnBrowseDest = new Button { Text = "Parcourir...", AutoSize = true, Margin = new Padding(3, 3, 3, 3) };

            layout.Controls.Add(lblSource, 0, 0);
            layout.Controls.Add(_txtSource, 1, 0);
            layout.Controls.Add(_btnBrowseSource, 2, 0);
            layout.Controls.Add(lblDest, 0, 1);
            layout.Controls.Add(_txtDest, 1, 1);
            layout.Controls.Add(_btnBrowseDest, 2, 1);

            _grpFolders.Controls.Add(layout);
        }

        private void BuildModesGroup()
        {
            _grpModes = new GroupBox
            {
                Text = "Mode de traitement",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 8, 10, 10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (var i = 0; i < 4; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            }

            _btnModeArtist = CreateModeButton("Créer par artiste (Base)");
            _btnModeDate = CreateModeButton("Vérifier Date Exacte (Lien)");
            _btnModeDateFile = CreateModeButton("Vérifier Date Exacte (Fichier)");
            _btnModeStyle = CreateModeButton("Créer par style");
            _btnModeSort = CreateModeButton("Trier et Corriger (Artistes et doublons)");

            layout.Controls.Add(_btnModeArtist, 0, 0);
            layout.Controls.Add(_btnModeDate, 1, 0);
            layout.Controls.Add(_btnModeDateFile, 1, 1);
            layout.Controls.Add(_btnModeStyle, 2, 0);
            layout.Controls.Add(_btnModeSort, 3, 0);
            _grpModes.Controls.Add(layout);
        }

        private static RoundedButton CreateModeButton(string text)
        {
            return new RoundedButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 44,
                BorderRadius = 8,
                SelectedBackColor = Color.FromArgb(21, 101, 192),
                NormalBackColor = Color.FromArgb(46, 125, 50),
                NormalForeColor = Color.White,
                Margin = new Padding(4, 4, 4, 4),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
        }

        private void BuildOptionsGroup()
        {
            _grpOptions = new GroupBox
            {
                Text = "Options",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 8, 10, 10)
            };

            _optionsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36)
            };
            _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            _chkRecursive = CreateOptionCheckBox("Inclure les sous-dossiers (parcourt les dossiers enfants)", true);
            _chkUseTags = CreateOptionCheckBox("Lire les tags avant le nom du fichier (priorise les métadonnées)", true);
            _chkMoveCoverArt = CreateOptionCheckBox("Copier les pochettes associées (déplace les images avec le morceau)", true);
            _chkFixTags = CreateOptionCheckBox("Corriger les tags artiste/titre (aligne sur le morceau réel)", false);
            _chkFingerprintDuplicates = CreateOptionCheckBox("Détecter les doublons par empreinte (compare le contenu audio)", true);
            _chkFetchMusicBrainz = CreateOptionCheckBox("Compléter via MusicBrainz (cherche les métadonnées manquantes)", false);
            _chkFindOriginalYear = CreateOptionCheckBox("Recherche exacte de la date de sortie (trouve l'année originale)", true);
            _chkNormalizeArtists = CreateOptionCheckBox("Normaliser artiste/titre (corrige la casse et les espaces)", true);
            _chkPrimaryArtistOnly = CreateOptionCheckBox("Garder seulement l'artiste principal (retire les feats du nom artiste)", true);
            _chkAnalyzeLibrary = CreateOptionCheckBox("Analyser la bibliothèque finale (compte artistes, albums et morceaux)", true);
            _chkStyleRecursive = CreateOptionCheckBox("Inclure les sous-dossiers (parcourt les dossiers enfants)", true);
            _chkStyleUseTags = CreateOptionCheckBox("Lire les tags avant le nom du fichier (identifie mieux le morceau)", true);
            _chkStyleFetchMusicBrainz = CreateOptionCheckBox("Chercher le style sur internet (MusicBrainz puis iTunes)", true);
            _chkSortRecursive = CreateOptionCheckBox("Inclure les sous-dossiers (parcourt les dossiers enfants)", true);
            _chkSortUseTags = CreateOptionCheckBox("Lire les tags avant le nom du fichier (priorise les métadonnées)", true);
            _chkSortFixTags = CreateOptionCheckBox("Corriger les tags artiste/titre (aligne sur le morceau réel)", false);
            _chkSortFingerprintDuplicates = CreateOptionCheckBox("Détecter les doublons par empreinte (compare le contenu audio)", true);
            _chkSortNormalizeArtists = CreateOptionCheckBox("Normaliser artiste/titre (corrige la casse et les espaces)", true);
            _chkSortPrimaryArtistOnly = CreateOptionCheckBox("Garder seulement l'artiste principal (retire les feats du nom artiste)", true);

            AddOptionCheckBox(_chkMoveCoverArt, 0, 0);
            AddOptionCheckBox(_chkAnalyzeLibrary, 0, 1);
            AddOptionCheckBox(_chkRecursive, 0, 2);
            AddOptionCheckBox(_chkUseTags, 0, 3);
            AddOptionCheckBox(_chkFixTags, 0, 4);
            AddOptionCheckBox(_chkFingerprintDuplicates, 0, 5);
            AddOptionCheckBox(_chkNormalizeArtists, 0, 6);
            AddOptionCheckBox(_chkPrimaryArtistOnly, 0, 7);
            AddOptionCheckBox(_chkFindOriginalYear, 1, 0);
            AddOptionCheckBox(_chkFetchMusicBrainz, 1, 1);
            AddOptionCheckBox(_chkStyleRecursive, 2, 0);
            AddOptionCheckBox(_chkStyleUseTags, 2, 1);
            AddOptionCheckBox(_chkStyleFetchMusicBrainz, 2, 2);
            AddOptionCheckBox(_chkSortRecursive, 3, 0);
            AddOptionCheckBox(_chkSortUseTags, 3, 1);
            AddOptionCheckBox(_chkSortFixTags, 3, 2);
            AddOptionCheckBox(_chkSortFingerprintDuplicates, 3, 3);
            AddOptionCheckBox(_chkSortNormalizeArtists, 3, 4);
            AddOptionCheckBox(_chkSortPrimaryArtistOnly, 3, 5);

            _grpOptions.Controls.Add(_optionsLayout);
        }

        private static CheckBox CreateOptionCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Margin = new Padding(4, 6, 18, 6)
            };
        }

        private void AddOptionCheckBox(CheckBox checkBox, int column, int row)
        {
            _optionsLayout.Controls.Add(checkBox, column, row);
        }

        private void BuildActionsPanel()
        {
            _pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0)
            };

            _btnStart = new RoundedButton { Text = "Lancer", Width = 120, Height = 36, SelectedMode = true, SelectedBackColor = Color.FromArgb(46, 125, 50), Margin = new Padding(0, 0, 8, 0) };
            _btnStop = new RoundedButton { Text = "Arrêter", Width = 120, Height = 36, Enabled = false, SelectedMode = true, SelectedBackColor = Color.FromArgb(198, 40, 40), Margin = new Padding(0, 0, 8, 0) };
            _btnShowFolderStats = new RoundedButton { Text = "Afficher dossiers", AutoSize = true, Height = 36, SelectedMode = true, SelectedBackColor = Color.FromArgb(69, 90, 100), Margin = new Padding(0, 0, 8, 0) };

            _btnOpenDest = new Button { Text = "Ouvrir le dossier destination", AutoSize = true, Height = 34, Margin = new Padding(0, 0, 8, 0) };

            _pnlActions.Controls.Add(_btnStart);
            _pnlActions.Controls.Add(_btnStop);
            _pnlActions.Controls.Add(_btnShowFolderStats);
            _pnlActions.Controls.Add(_btnOpenDest);
        }

        private void BuildProgressBar()
        {
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 22,
                Margin = new Padding(0, 10, 0, 0),
                Minimum = 0,
                Maximum = 100
            };
        }

        private void BuildStatsGroup()
        {
            _grpStats = new GroupBox
            {
                Text = "Statistiques",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 5, 10, 10)
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 6,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (var i = 0; i < 6; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
            }

            _lblElapsedValue = AddStatCell(grid, 0, "Temps écoulé", "00:00:00");
            _lblAnalyzedValue = AddStatCell(grid, 1, "Analysés", "0");
            _lblMovedValue = AddStatCell(grid, 2, "Déplacés", "0");
            _lblIgnoredValue = AddStatCell(grid, 3, "Ignorés", "0");
            _lblDuplicatesValue = AddStatCell(grid, 4, "Doublons", "0");
            _lblSpeedValue = AddStatCell(grid, 5, "Vitesse", "0 f/s");

            _grpStats.Controls.Add(grid);
        }

        private static Label AddStatCell(TableLayoutPanel grid, int column, string caption, string initialValue)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            var captionLabel = new Label { Text = caption, AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f), Dock = DockStyle.Top };
            var valueLabel = new Label { Text = initialValue, AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Dock = DockStyle.Top };
            panel.Controls.Add(captionLabel, 0, 0);
            panel.Controls.Add(valueLabel, 0, 1);
            grid.Controls.Add(panel, column, 0);
            return valueLabel;
        }

        private void BuildLogGroup()
        {
            _grpLog = new GroupBox
            {
                Text = "Journal en temps réel",
                Dock = DockStyle.Top,
                Height = 300,
                MinimumSize = new Size(0, 240),
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 5, 10, 10)
            };

            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = Color.White
            };

            _grpLog.Controls.Add(_txtLog);
        }

        #endregion
    }
}
