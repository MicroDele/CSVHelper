using System.Data;
using System.Text;

namespace CsvReaderApp;

public sealed class MainForm : Form
{
    private static readonly Color SelectedRowBackColor = Color.FromArgb(232, 240, 254);
    private static readonly Color CurrentCellSelectionBackColor = Color.FromArgb(154, 192, 255);
    private static readonly Color MatchHighlightBackColor = Color.FromArgb(255, 243, 176);

    private readonly Button openButton = new();
    private readonly Button saveButton = new();
    private readonly Label fileLabel = new();
    private readonly DataGridView grid = new();
    private readonly StatusStrip statusStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly ContextMenuStrip gridMenu = new();
    private readonly ToolStripMenuItem copyCellItem = new("Copy Cell");
    private readonly ToolStripMenuItem copyHeaderItem = new("Copy Header");

    private readonly Label searchLabel = new();
    private readonly TextBox searchBox = new();
    private readonly Button prevMatchButton = new();
    private readonly Button nextMatchButton = new();
    private readonly Label matchCountLabel = new();
    private readonly System.Windows.Forms.Timer searchDebounceTimer = new();

    private string? currentFilePath;
    private bool isLoading;
    private bool isDirty;
    private int contextColumnIndex = -1;
    private int contextRowIndex = -1;
    private int highlightedRowIndex = -1;
    private List<(int RowIndex, int ColumnIndex)> currentMatches = new();
    private int currentMatchIndex = -1;

    public MainForm(string? initialPath)
    {
        Text = "CSVHelper";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1200;
        Height = 760;
        KeyPreview = true;

        BuildUi();
        FormClosing += MainFormOnFormClosing;
        KeyDown += MainFormOnKeyDown;

        if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
        {
            LoadCsvFile(initialPath);
        }
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            Padding = new Padding(8, 8, 8, 4)
        };

        openButton.Text = "Open CSV";
        openButton.SetBounds(8, 7, 100, 28);
        openButton.Click += (_, _) => OpenCsvFromDialog();

        saveButton.Text = "Save";
        saveButton.SetBounds(116, 7, 76, 28);
        saveButton.Enabled = false;
        saveButton.Click += (_, _) => SaveCurrentFile(showSavedStatus: true);

        fileLabel.Text = "No file loaded";
        fileLabel.AutoEllipsis = true;
        fileLabel.SetBounds(204, 13, 900, 20);
        fileLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

        topPanel.Controls.Add(openButton);
        topPanel.Controls.Add(saveButton);
        topPanel.Controls.Add(fileLabel);

        var searchPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 36,
            Padding = new Padding(8, 2, 8, 4)
        };

        searchLabel.Text = "Search:";
        searchLabel.AutoSize = false;
        searchLabel.SetBounds(8, 10, 50, 20);
        searchLabel.TextAlign = ContentAlignment.MiddleLeft;

        searchBox.SetBounds(60, 6, 240, 24);
        searchBox.BorderStyle = BorderStyle.FixedSingle;
        searchBox.TextChanged += (_, _) => ResetSearchDebounce();
        searchBox.KeyDown += SearchBoxOnKeyDown;

        prevMatchButton.Text = "↑";
        prevMatchButton.SetBounds(306, 6, 32, 24);
        prevMatchButton.Enabled = false;
        prevMatchButton.Click += (_, _) => NavigateMatch(-1);

        nextMatchButton.Text = "↓";
        nextMatchButton.SetBounds(342, 6, 32, 24);
        nextMatchButton.Enabled = false;
        nextMatchButton.Click += (_, _) => NavigateMatch(1);

        matchCountLabel.SetBounds(380, 10, 90, 20);
        matchCountLabel.Text = string.Empty;
        matchCountLabel.ForeColor = Color.Gray;
        matchCountLabel.TextAlign = ContentAlignment.MiddleLeft;

        searchPanel.Controls.Add(searchLabel);
        searchPanel.Controls.Add(searchBox);
        searchPanel.Controls.Add(prevMatchButton);
        searchPanel.Controls.Add(nextMatchButton);
        searchPanel.Controls.Add(matchCountLabel);

        searchDebounceTimer.Interval = 200;
        searchDebounceTimer.Tick += (_, _) =>
        {
            searchDebounceTimer.Stop();
            ApplySearch(searchBox.Text);
        };

        statusStrip.Dock = DockStyle.Fill;
        statusStrip.Items.Add(statusLabel);
        statusLabel.Text = "Ready";

        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.Fixed3D;
        grid.ColumnHeadersHeight = 28;
        grid.RowTemplate.Height = 24;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = false;
        grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        grid.DefaultCellStyle.SelectionBackColor = CurrentCellSelectionBackColor;
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.MouseDown += GridOnMouseDown;
        grid.CellMouseDown += GridOnCellMouseDown;
        grid.SelectionChanged += UpdateGridSelectionHighlight;
        grid.Sorted += (_, _) => ApplySearch(searchBox.Text);
        grid.CellValueChanged += (_, _) =>
        {
            MarkDirty();
            RefreshSearchIfActive();
        };

        gridMenu.Items.Add(copyCellItem);
        gridMenu.Items.Add(copyHeaderItem);
        gridMenu.Opening += (_, _) =>
        {
            copyCellItem.Enabled = contextRowIndex >= 0 && contextColumnIndex >= 0;
            copyHeaderItem.Enabled = contextColumnIndex >= 0;
        };
        copyCellItem.Click += (_, _) => CopySelectedCell();
        copyHeaderItem.Click += (_, _) => CopySelectedHeader();
        grid.ContextMenuStrip = gridMenu;

        layout.Controls.Add(topPanel, 0, 0);
        layout.Controls.Add(searchPanel, 0, 1);
        layout.Controls.Add(grid, 0, 2);
        layout.Controls.Add(statusStrip, 0, 3);
        Controls.Add(layout);

        RegisterClearSelectionOnMouseDown(layout);
    }

    private void RegisterClearSelectionOnMouseDown(Control control)
    {
        if (ReferenceEquals(control, grid))
        {
            return;
        }

        control.MouseDown += ClearGridSelectionOnOutsideMouseDown;
        foreach (Control child in control.Controls)
        {
            RegisterClearSelectionOnMouseDown(child);
        }
    }

    private void ClearGridSelectionOnOutsideMouseDown(object? sender, MouseEventArgs e)
    {
        CommitGridInputAndClearSelection();
    }

    private void GridOnMouseDown(object? sender, MouseEventArgs e)
    {
        var hit = grid.HitTest(e.X, e.Y);
        if (hit.Type != DataGridViewHitTestType.Cell || hit.RowIndex < 0 || hit.ColumnIndex < 0)
        {
            CommitGridInputAndClearSelection();
        }
    }

    private void CommitGridInputAndClearSelection()
    {
        grid.EndEdit();
        grid.ClearSelection();
        grid.CurrentCell = null;
        ClearHighlightedRow();
    }

    private void UpdateGridSelectionHighlight(object? sender, EventArgs e)
    {
        ClearHighlightedRow();

        highlightedRowIndex = -1;

        if (grid.SelectedCells.Count == 0 || grid.CurrentCell is null || grid.CurrentCell.RowIndex < 0)
        {
            return;
        }

        highlightedRowIndex = grid.CurrentCell.RowIndex;
        grid.Rows[highlightedRowIndex].DefaultCellStyle.BackColor = SelectedRowBackColor;
    }

    private void ClearHighlightedRow()
    {
        if (highlightedRowIndex >= 0 && highlightedRowIndex < grid.Rows.Count)
        {
            grid.Rows[highlightedRowIndex].DefaultCellStyle.BackColor = Color.Empty;
        }
    }

    private void OpenCsvFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Open CSV file"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadCsvFile(dialog.FileName);
        }
    }

    private void LoadCsvFile(string path)
    {
        try
        {
            isLoading = true;
            var text = File.ReadAllText(path, new UTF8Encoding(false, true));
            var parsed = CsvDocument.Parse(text);
            var table = new DataTable();

            foreach (var header in parsed.Headers)
            {
                var name = string.IsNullOrWhiteSpace(header) ? $"Column {table.Columns.Count + 1}" : header;
                if (table.Columns.Contains(name))
                {
                    name = $"{name} {table.Columns.Count + 1}";
                }

                table.Columns.Add(name, typeof(string));
            }

            foreach (var row in parsed.Rows)
            {
                while (table.Columns.Count < row.Length)
                {
                    table.Columns.Add($"Column {table.Columns.Count + 1}", typeof(string));
                }

                var dataRow = table.NewRow();
                for (var colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    dataRow[colIndex] = row[colIndex];
                }

                table.Rows.Add(dataRow);
            }

            grid.DataSource = table;
            CommitGridInputAndClearSelection();
            currentFilePath = path;
            SetDirty(false);
            fileLabel.Text = path;
            Text = $"CSVHelper - {Path.GetFileName(path)}";
            SetStatus($"Loaded {table.Rows.Count} rows, {table.Columns.Count} columns");
            ResetSearch();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Open failed");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void SaveCurrentFile(bool showSavedStatus)
    {
        if (isLoading || string.IsNullOrWhiteSpace(currentFilePath) || grid.DataSource is not DataTable table)
        {
            return;
        }

        var headers = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
        var rows = table.Rows.Cast<DataRow>()
            .Select(row => table.Columns.Cast<DataColumn>()
                .Select(column => row[column]?.ToString() ?? string.Empty)
                .ToArray())
            .ToList();

        var document = new CsvDocument(headers, rows);
        File.WriteAllText(currentFilePath, document.ToCsvText(), new UTF8Encoding(false));
        SetDirty(false);
        if (showSavedStatus)
        {
            SetStatus("Saved");
        }
    }

    private void GridOnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        contextColumnIndex = e.ColumnIndex;
        contextRowIndex = e.RowIndex;

        if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
        {
            grid.ClearSelection();
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.Selected = true;
            grid.CurrentCell = cell;
        }
    }

    private void CopySelectedCell()
    {
        if (contextRowIndex < 0 || contextColumnIndex < 0)
        {
            return;
        }

        Clipboard.SetText(grid.Rows[contextRowIndex].Cells[contextColumnIndex].Value?.ToString() ?? string.Empty);
        SetStatus("Cell copied");
    }

    private void CopySelectedHeader()
    {
        if (contextColumnIndex < 0)
        {
            return;
        }

        Clipboard.SetText(grid.Columns[contextColumnIndex].HeaderText);
        SetStatus("Header copied");
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
    }

    private void MarkDirty()
    {
        if (isLoading || currentFilePath is null)
        {
            return;
        }

        SetDirty(true);
        SetStatus("Modified");
    }

    private void SetDirty(bool dirty)
    {
        isDirty = dirty;
        saveButton.Enabled = dirty && currentFilePath is not null;

        if (currentFilePath is null)
        {
            Text = "CSVHelper";
            return;
        }

        var marker = dirty ? "*" : string.Empty;
        Text = $"CSVHelper - {Path.GetFileName(currentFilePath)}{marker}";
    }

    private void MainFormOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            grid.EndEdit();
            SaveCurrentFile(showSavedStatus: true);
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F)
        {
            searchBox.Focus();
            searchBox.SelectAll();
            e.SuppressKeyPress = true;
        }
    }

    private void MainFormOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!isDirty)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Save changes before closing?",
            "CSVHelper",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            grid.EndEdit();
            SaveCurrentFile(showSavedStatus: false);
        }
    }

    private void ResetSearchDebounce()
    {
        searchDebounceTimer.Stop();
        searchDebounceTimer.Start();
    }

    private static List<(int RowIndex, int ColumnIndex)> SearchMatches(DataGridView gridView, string query)
    {
        var matches = new List<(int RowIndex, int ColumnIndex)>();
        if (string.IsNullOrEmpty(query))
        {
            return matches;
        }

        for (var row = 0; row < gridView.Rows.Count; row++)
        {
            var gridViewRow = gridView.Rows[row];
            for (var col = 0; col < gridViewRow.Cells.Count; col++)
            {
                if (gridViewRow.Cells[col].Value is string value &&
                    value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add((row, col));
                }
            }
        }

        return matches;
    }

    private void ApplySearch(string query)
    {
        ClearHighlights();
        currentMatches = SearchMatches(grid, query);

        if (currentMatches.Count == 0)
        {
            currentMatchIndex = -1;
            prevMatchButton.Enabled = false;
            nextMatchButton.Enabled = false;
            UpdateMatchCountLabel();
            SetStatus(string.IsNullOrEmpty(query) ? "Ready" : "No matches");
            return;
        }

        currentMatchIndex = 0;
        HighlightMatches();
        NavigateToCurrentMatch(scroll: true);
        prevMatchButton.Enabled = true;
        nextMatchButton.Enabled = true;
        UpdateMatchCountLabel();
    }

    private void HighlightMatches()
    {
        foreach (var (row, col) in currentMatches)
        {
            if (row >= 0 && row < grid.Rows.Count && col >= 0 && col < grid.Columns.Count)
            {
                grid.Rows[row].Cells[col].Style.BackColor = MatchHighlightBackColor;
            }
        }
    }

    private void ClearHighlights()
    {
        foreach (var (row, col) in currentMatches)
        {
            if (row >= 0 && row < grid.Rows.Count && col >= 0 && col < grid.Columns.Count)
            {
                grid.Rows[row].Cells[col].Style.BackColor = Color.Empty;
            }
        }
    }

    private void NavigateMatch(int direction)
    {
        if (currentMatches.Count == 0)
        {
            return;
        }

        currentMatchIndex = (currentMatchIndex + direction + currentMatches.Count) % currentMatches.Count;
        NavigateToCurrentMatch(scroll: true);
        UpdateMatchCountLabel();
    }

    private void NavigateToCurrentMatch(bool scroll)
    {
        if (currentMatchIndex < 0 || currentMatchIndex >= currentMatches.Count)
        {
            return;
        }

        var (row, col) = currentMatches[currentMatchIndex];
        if (row < 0 || row >= grid.Rows.Count || col < 0 || col >= grid.Columns.Count)
        {
            return;
        }

        grid.CurrentCell = grid.Rows[row].Cells[col];

        if (scroll && row < grid.Rows.Count)
        {
            var firstVisible = grid.FirstDisplayedScrollingRowIndex;
            var visibleCount = grid.DisplayedRowCount(false);
            if (row < firstVisible)
            {
                grid.FirstDisplayedScrollingRowIndex = row;
            }
            else if (visibleCount > 0 && row > firstVisible + visibleCount - 1)
            {
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row - visibleCount + 1);
            }
        }
    }

    private void UpdateMatchCountLabel()
    {
        if (currentMatches.Count == 0)
        {
            matchCountLabel.Text = string.IsNullOrEmpty(searchBox.Text) ? string.Empty : "0 / 0";
            return;
        }

        matchCountLabel.Text = $"{currentMatchIndex + 1} / {currentMatches.Count}";
    }

    private void SearchBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            NavigateMatch(e.Shift ? -1 : 1);
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            searchBox.Text = string.Empty;
            ApplySearch(string.Empty);
            e.SuppressKeyPress = true;
        }
    }

    private void RefreshSearchIfActive()
    {
        if (isLoading || string.IsNullOrEmpty(searchBox.Text))
        {
            return;
        }

        ApplySearch(searchBox.Text);
    }

    private void ResetSearch()
    {
        searchDebounceTimer.Stop();
        ClearHighlights();
        currentMatches = new List<(int RowIndex, int ColumnIndex)>();
        currentMatchIndex = -1;
        prevMatchButton.Enabled = false;
        nextMatchButton.Enabled = false;
        UpdateMatchCountLabel();
        searchBox.Text = string.Empty;
    }
}
