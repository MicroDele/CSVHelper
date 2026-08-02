using System.Data;
using System.Text;

namespace CsvReaderApp;

public sealed record SortKey(string ColumnName, bool Ascending);

public sealed class MainForm : Form
{
    private static readonly Color SelectedRowBackColor = UiTheme.Selection;
    private static readonly Color CurrentCellSelectionBackColor = UiTheme.ActiveCell;
    private static readonly Color MatchHighlightBackColor = UiTheme.Match;
    private const int SortGlyphReservedHeaderWidth = 30;
    private const int MinimumSortableHeaderWidth = 56;
    private const int GridHorizontalPadding = 8;

    private readonly Button openButton = new();
    private readonly Button saveButton = new();
    private readonly Button reloadButton = new();
    private readonly Label fileLabel = new();
    private readonly Panel cellEditPanel = new();
    private readonly TextBox cellEditBox = new();
    private readonly DataGridView grid = new();
    private readonly StatusStrip statusStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly ToolTip toolTip = new();
    private readonly ContextMenuStrip gridMenu = new();
    private readonly ToolStripMenuItem copyCellItem = new("Copy Cell");
    private readonly ToolStripMenuItem copyHeaderItem = new("Copy Header");

    private readonly Button searchButton = new();
    private SearchDialog? searchDialog;
    private string lastSearchQuery = string.Empty;
    private bool lastSearchWholeCell;

    private string? currentFilePath;
    private bool isLoading;
    private bool isUpdatingCellEditBox;
    private bool isDirty;
    private int contextColumnIndex = -1;
    private int contextRowIndex = -1;
    private int highlightedRowIndex = -1;
    private List<(int RowIndex, int ColumnIndex)> currentMatches = new();
    private int currentMatchIndex = -1;

    private readonly Button multiSortButton = new();
    private readonly List<SortKey> sortKeys = new();

    public MainForm(string? initialPath)
    {
        Text = "CSVHelper";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1200;
        Height = 760;
        KeyPreview = true;
        UiTheme.ApplyForm(this);

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
            RowCount = 3,
            BackColor = UiTheme.PageBackground
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 84,
            Margin = Padding.Empty,
            Padding = new Padding(8, 8, 8, 4),
            BackColor = UiTheme.PageBackground
        };

        openButton.SetBounds(8, 6, 34, 30);
        UiTheme.ApplyIconButton(openButton, toolTip, UiIconKind.Open, "Open CSV");
        openButton.Click += (_, _) => OpenCsvFromDialog();

        saveButton.SetBounds(48, 6, 34, 30);
        UiTheme.ApplyIconButton(saveButton, toolTip, UiIconKind.Save, "Save");
        saveButton.Enabled = false;
        saveButton.Click += (_, _) => SaveCurrentFile(showSavedStatus: true);

        reloadButton.SetBounds(88, 6, 34, 30);
        UiTheme.ApplyIconButton(reloadButton, toolTip, UiIconKind.Reload, "Reload from disk (Ctrl+R)");
        reloadButton.Enabled = false;
        reloadButton.Click += (_, _) => ReloadCurrentFile();

        multiSortButton.SetBounds(128, 6, 34, 30);
        UiTheme.ApplyIconButton(multiSortButton, toolTip, UiIconKind.Sort, "Multi-sort");
        multiSortButton.Enabled = false;
        multiSortButton.Click += (_, _) => OpenMultiSortDialog();

        searchButton.SetBounds(168, 6, 34, 30);
        UiTheme.ApplyIconButton(searchButton, toolTip, UiIconKind.Search, "Search (Ctrl+F)");
        searchButton.Enabled = false;
        searchButton.Click += (_, _) => OpenSearchDialog();

        fileLabel.Text = "No file loaded";
        fileLabel.AutoEllipsis = true;
        fileLabel.ForeColor = UiTheme.MutedText;
        fileLabel.SetBounds(208, 12, 892, 20);
        fileLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

        cellEditPanel.BackColor = UiTheme.Surface;
        cellEditPanel.BorderStyle = BorderStyle.FixedSingle;
        cellEditPanel.Padding = new Padding(8, 5, 8, 0);
        cellEditPanel.SetBounds(GridHorizontalPadding, 46, topPanel.ClientSize.Width - GetCellEditHorizontalInset(), 30);
        cellEditPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        topPanel.Resize += (_, _) => AlignCellEditPanel(topPanel);

        cellEditBox.ReadOnly = true;
        cellEditBox.Dock = DockStyle.Fill;
        cellEditBox.BorderStyle = BorderStyle.None;
        cellEditBox.BackColor = UiTheme.Surface;
        cellEditBox.ForeColor = UiTheme.Text;
        cellEditBox.PlaceholderText = "Select a cell to edit its full value";
        cellEditBox.TextChanged += CellEditBoxOnTextChanged;
        toolTip.SetToolTip(cellEditBox, "Edit current cell");
        cellEditPanel.Controls.Add(cellEditBox);

        topPanel.Controls.Add(openButton);
        topPanel.Controls.Add(saveButton);
        topPanel.Controls.Add(reloadButton);
        topPanel.Controls.Add(multiSortButton);
        topPanel.Controls.Add(searchButton);
        topPanel.Controls.Add(fileLabel);
        topPanel.Controls.Add(cellEditPanel);
        AlignCellEditPanel(topPanel);

        statusStrip.Dock = DockStyle.Fill;
        statusStrip.Margin = Padding.Empty;
        statusStrip.BackColor = UiTheme.Surface;
        statusStrip.Items.Add(statusLabel);
        statusLabel.ForeColor = UiTheme.MutedText;
        statusLabel.Text = "Ready";

        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(GridHorizontalPadding, 0, GridHorizontalPadding, 0);
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        // 关闭自动尺寸：AllCells 会在每次行变化时全表重算尺寸，导致逐行可见的填充/排序。
        // 列宽改为固定起点（表头宽度 + 排序箭头，见 EnsureSortableHeaderWidth）+ 用户可拖动；行高固定。
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        // 双缓冲：绘制一次性完成，不再逐行可见。
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(grid, true);
        grid.BackgroundColor = UiTheme.Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.ColumnHeadersHeight = 28;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = UiTheme.Border;
        grid.RowTemplate.Height = 24;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = false;
        grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.DefaultCellStyle.BackColor = UiTheme.Surface;
        grid.DefaultCellStyle.ForeColor = UiTheme.Text;
        grid.DefaultCellStyle.SelectionBackColor = CurrentCellSelectionBackColor;
        grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 251);
        grid.MouseDown += GridOnMouseDown;
        grid.CellMouseDown += GridOnCellMouseDown;
        grid.SelectionChanged += UpdateGridSelectionHighlight;
        grid.DataBindingComplete += GridOnDataBindingComplete;
        grid.ColumnHeaderMouseClick += GridOnColumnHeaderMouseClick;
        grid.CellValueChanged += (_, _) =>
        {
            UpdateCellEditBoxFromCurrentCell();
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
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(statusStrip, 0, 2);
        Controls.Add(layout);

        RegisterClearSelectionOnMouseDown(layout);
    }

    private void AlignCellEditPanel(Control parent)
    {
        cellEditPanel.SetBounds(
            GridHorizontalPadding,
            46,
            Math.Max(0, parent.ClientSize.Width - GetCellEditHorizontalInset()),
            30);
    }

    private static int GetCellEditHorizontalInset()
    {
        return GridHorizontalPadding * 2 + SystemInformation.VerticalScrollBarWidth;
    }

    private void RegisterClearSelectionOnMouseDown(Control control)
    {
        if (ReferenceEquals(control, grid) || ReferenceEquals(control, cellEditBox))
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
        UpdateCellEditBoxFromCurrentCell();
    }

    private void UpdateGridSelectionHighlight(object? sender, EventArgs e)
    {
        ClearHighlightedRow();

        highlightedRowIndex = -1;

        if (grid.SelectedCells.Count == 0 || grid.CurrentCell is null || grid.CurrentCell.RowIndex < 0)
        {
            UpdateCellEditBoxFromCurrentCell();
            return;
        }

        highlightedRowIndex = grid.CurrentCell.RowIndex;
        grid.Rows[highlightedRowIndex].DefaultCellStyle.BackColor = SelectedRowBackColor;
        UpdateCellEditBoxFromCurrentCell();
    }

    private void ClearHighlightedRow()
    {
        if (highlightedRowIndex >= 0 && highlightedRowIndex < grid.Rows.Count)
        {
            grid.Rows[highlightedRowIndex].DefaultCellStyle.BackColor = Color.Empty;
        }
    }

    private void GridOnDataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        // 绑定真正完成的时机：DataGridView 绑定后会默认选中首个单元格并触发行高亮，
        // 而 LoadCsvFile 里紧接着的清除（CommitGridInputAndClearSelection）会因行尚未生成而无效，
        // 导致"打开文件后整片高亮"。在此处彻底重置选中与所有行高亮。
        grid.ClearSelection();
        grid.CurrentCell = null;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.DefaultCellStyle.BackColor = Color.Empty;
        }

        // 列改用手动排序模式：默认 Automatic 会触发内置单列排序且不显示我们的 glyph，
        // 改为 Programmatic 后由 ColumnHeaderMouseClick + ApplySort 全权管理排序与箭头。
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
            EnsureSortableHeaderWidth(column);
        }
        FillTrailingColumn();

        highlightedRowIndex = -1;
        UpdateCellEditBoxFromCurrentCell();
    }

    private void FillTrailingColumn()
    {
        if (grid.Columns.Count == 0)
        {
            return;
        }

        grid.Columns[^1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void UpdateCellEditBoxFromCurrentCell()
    {
        isUpdatingCellEditBox = true;
        try
        {
            if (grid.CurrentCell is null
                || grid.CurrentCell.RowIndex < 0
                || grid.CurrentCell.ColumnIndex < 0)
            {
                cellEditBox.Text = string.Empty;
                cellEditBox.ReadOnly = true;
                cellEditPanel.BackColor = Color.FromArgb(250, 252, 251);
                cellEditBox.BackColor = cellEditPanel.BackColor;
                return;
            }

            var column = grid.Columns[grid.CurrentCell.ColumnIndex];
            toolTip.SetToolTip(cellEditBox, column.HeaderText);
            cellEditBox.ReadOnly = false;
            cellEditPanel.BackColor = UiTheme.Surface;
            cellEditBox.BackColor = UiTheme.Surface;
            cellEditBox.Text = grid.CurrentCell.Value?.ToString() ?? string.Empty;
        }
        finally
        {
            isUpdatingCellEditBox = false;
        }
    }

    private void CellEditBoxOnTextChanged(object? sender, EventArgs e)
    {
        if (isUpdatingCellEditBox || isLoading || grid.CurrentCell is null)
        {
            return;
        }

        var currentValue = grid.CurrentCell.Value?.ToString() ?? string.Empty;
        if (currentValue == cellEditBox.Text)
        {
            return;
        }

        grid.CurrentCell.Value = cellEditBox.Text;
    }

    private void GridOnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        // 表头单击 = 单列排序（替换多列排序）。
        // 同一列重复点击在 升序 → 降序 → 取消 间轮转；取消后若链空则恢复文件原始顺序。
        if (grid.DataSource is not DataTable table
            || e.ColumnIndex < 0
            || e.ColumnIndex >= table.Columns.Count)
        {
            return;
        }

        var columnName = table.Columns[e.ColumnIndex].ColumnName;

        if (sortKeys.Count > 0 && sortKeys[0].ColumnName == columnName)
        {
            if (sortKeys[0].Ascending)
            {
                sortKeys[0] = sortKeys[0] with { Ascending = false };
            }
            else
            {
                sortKeys.RemoveAt(0);
            }
        }
        else
        {
            sortKeys.Clear();
            sortKeys.Add(new SortKey(columnName, Ascending: true));
        }

        ApplySort();
    }

    private void ApplySort()
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        // DataView.Sort 仅重排视图，不改 DataTable.Rows 的实际顺序，因此保存仍是文件原始顺序。
        // 列名含空格（如 "Column 1"）需用 [] 转义，否则 DataView 解析失败。
        var expression = string.Join(", ",
            sortKeys.Select(k => $"[{k.ColumnName}] {(k.Ascending ? "ASC" : "DESC")}"));
        table.DefaultView.Sort = expression;

        // 逐列显式设置 glyph（含 type 列），根治「点击列头不显示上下箭头」。
        for (var i = 0; i < grid.Columns.Count && i < table.Columns.Count; i++)
        {
            var columnName = table.Columns[i].ColumnName;
            var match = sortKeys.FirstOrDefault(k => k.ColumnName == columnName);
            grid.Columns[i].HeaderCell.SortGlyphDirection =
                match is null ? SortOrder.None
                : match.Ascending ? SortOrder.Ascending
                : SortOrder.Descending;
        }

        RefreshSearchIfActive();
    }

    private void EnsureSortableHeaderWidth(DataGridViewColumn column)
    {
        var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
        var headerText = string.IsNullOrEmpty(column.HeaderText) ? " " : column.HeaderText;
        var textWidth = TextRenderer.MeasureText(headerText, headerFont).Width;
        // MinimumWidth 保证拖动时不会窄到放不下表头 + 排序箭头。
        var minimum = Math.Max(MinimumSortableHeaderWidth, textWidth + SortGlyphReservedHeaderWidth);
        column.MinimumWidth = Math.Max(column.MinimumWidth, minimum);
        // 初始列宽 = 表头 + 箭头（AutoSize=None 下的固定起点）；用户可拖动调整。
        column.Width = Math.Max(column.MinimumWidth, textWidth + SortGlyphReservedHeaderWidth);
    }

    private void OpenMultiSortDialog()
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        var columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        using var dialog = new MultiSortDialog(columns, sortKeys);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            sortKeys.Clear();
            sortKeys.AddRange(dialog.Result);
            ApplySort();
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

    private void ReloadCurrentFile()
    {
        if (isLoading || string.IsNullOrWhiteSpace(currentFilePath))
        {
            return;
        }

        grid.EndEdit();
        if (isDirty)
        {
            var result = MessageBox.Show(
                this,
                "Discard unsaved changes and reload from disk?",
                "Reload CSV",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        LoadCsvFile(currentFilePath);
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

            grid.SuspendLayout();
            grid.DataSource = table;
            sortKeys.Clear();
            multiSortButton.Enabled = true;
            searchButton.Enabled = true;
            CommitGridInputAndClearSelection();
            grid.ResumeLayout();
            currentFilePath = path;
            SetDirty(false);
            fileLabel.Text = path;
            Text = $"CSVHelper - {Path.GetFileName(path)}";
            SetStatus($"Loaded {table.Rows.Count} rows, {table.Columns.Count} columns");
            ResetSearch();
            if (searchDialog is { IsDisposed: false } dialog)
            {
                dialog.ClearInput();
            }
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
        reloadButton.Enabled = currentFilePath is not null;

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

        if (e.Control && e.KeyCode == Keys.R)
        {
            ReloadCurrentFile();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F)
        {
            OpenSearchDialog();
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

    private static List<(int RowIndex, int ColumnIndex)> SearchMatches(DataGridView gridView, string query, bool wholeCell)
    {
        var matches = new List<(int RowIndex, int ColumnIndex)>();
        var isEmptySearch = string.IsNullOrEmpty(query);

        for (var row = 0; row < gridView.Rows.Count; row++)
        {
            var gridViewRow = gridView.Rows[row];
            for (var col = 0; col < gridViewRow.Cells.Count; col++)
            {
                if (gridViewRow.Cells[col].Value is not string value)
                {
                    continue;
                }

                bool hit;
                if (isEmptySearch)
                {
                    // 空查询 = 搜索空单元格（CSV ",," 之间的空）。
                    hit = value.Length == 0;
                }
                else if (wholeCell)
                {
                    hit = value.Equals(query, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    hit = value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (hit)
                {
                    matches.Add((row, col));
                }
            }
        }

        return matches;
    }

    private void ApplySearch(string query, bool wholeCell)
    {
        ClearHighlights();
        currentMatches = SearchMatches(grid, query, wholeCell);

        if (currentMatches.Count == 0)
        {
            currentMatchIndex = -1;
            UpdateSearchDialogCount();
            SetStatus(string.IsNullOrEmpty(query) ? "No empty cells" : "No matches");
            return;
        }

        currentMatchIndex = 0;
        HighlightMatches();
        NavigateToCurrentMatch(scroll: true);
        UpdateSearchDialogCount();
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
        UpdateSearchDialogCount();
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

    private void UpdateSearchDialogCount()
    {
        searchDialog?.UpdateMatchCount(currentMatchIndex, currentMatches.Count);
    }

    private void RefreshSearchIfActive()
    {
        if (isLoading || searchDialog is null || searchDialog.IsDisposed || string.IsNullOrEmpty(searchDialog.Query))
        {
            return;
        }

        ApplySearch(searchDialog.Query, searchDialog.WholeCell);
    }

    private void ResetSearch()
    {
        ClearHighlights();
        currentMatches = new List<(int RowIndex, int ColumnIndex)>();
        currentMatchIndex = -1;
        UpdateSearchDialogCount();
    }

    private void OpenSearchDialog()
    {
        if (searchDialog is not null && !searchDialog.IsDisposed)
        {
            searchDialog.Activate();
            searchDialog.FocusQuery();
            return;
        }

        searchDialog = new SearchDialog(lastSearchQuery, lastSearchWholeCell);
        searchDialog.SearchRequested += (_, _) =>
        {
            if (searchDialog is null || searchDialog.IsDisposed)
            {
                return;
            }

            ApplySearch(searchDialog.Query, searchDialog.WholeCell);
        };
        searchDialog.NavigateRequested += (_, direction) =>
        {
            if (searchDialog is null || searchDialog.IsDisposed)
            {
                return;
            }

            NavigateMatch(direction);
        };
        searchDialog.FormClosed += (_, _) =>
        {
            if (searchDialog is null || searchDialog.IsDisposed)
            {
                return;
            }

            lastSearchQuery = searchDialog.Query;
            lastSearchWholeCell = searchDialog.WholeCell;
            searchDialog = null;
            ResetSearch();
        };
        searchDialog.Show(this);
        // CenterParent 对非模态 Show 不可靠，手动将对话框居中于主窗体。
        searchDialog.Location = new Point(
            this.Left + (this.Width - searchDialog.Width) / 2,
            this.Top + (this.Height - searchDialog.Height) / 2);
        // 仅回填上一次查询，不自动搜索——等用户点「查找」。
    }
}
