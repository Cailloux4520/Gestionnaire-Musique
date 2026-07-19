using MusicOrganizer.Models;

namespace MusicOrganizer;

public sealed class FolderStatisticsForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly BindingSource _bindingSource = new();
    private List<FolderMusicCount> _rows;
    private bool _sortDescending = true;

    public FolderStatisticsForm(List<FolderMusicCount> rows)
    {
        _rows = rows;
        InitializeComponent();
        BindRows(_rows.OrderByDescending(r => r.MusicFileCount).ThenBy(r => r.FolderName).ToList());
    }

    private void InitializeComponent()
    {
        Text = "Dossiers du répertoire destination";
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 640);
        StartPosition = FormStartPosition.CenterParent;

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Dossier",
            DataPropertyName = nameof(FolderMusicCount.FolderName),
            FillWeight = 35,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Fichiers musicaux",
            DataPropertyName = nameof(FolderMusicCount.MusicFileCount),
            FillWeight = 18,
            SortMode = DataGridViewColumnSortMode.Programmatic,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Chemin",
            DataPropertyName = nameof(FolderMusicCount.FullPath),
            FillWeight = 47,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });

        Controls.Add(_grid);
    }

    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var propertyName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        _sortDescending = !_sortDescending;

        IEnumerable<FolderMusicCount> sorted = propertyName switch
        {
            nameof(FolderMusicCount.MusicFileCount) => _sortDescending
                ? _rows.OrderByDescending(r => r.MusicFileCount).ThenBy(r => r.FolderName)
                : _rows.OrderBy(r => r.MusicFileCount).ThenBy(r => r.FolderName),
            nameof(FolderMusicCount.FullPath) => _sortDescending
                ? _rows.OrderByDescending(r => r.FullPath)
                : _rows.OrderBy(r => r.FullPath),
            _ => _sortDescending
                ? _rows.OrderByDescending(r => r.FolderName)
                : _rows.OrderBy(r => r.FolderName)
        };

        BindRows(sorted.ToList());
    }

    private void BindRows(List<FolderMusicCount> rows)
    {
        _rows = rows;
        _bindingSource.DataSource = _rows;
        _grid.DataSource = _bindingSource;
    }
}