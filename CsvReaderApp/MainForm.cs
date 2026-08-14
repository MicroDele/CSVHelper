using System.Data;
using System.Text;

namespace CsvReaderApp;

public sealed record SortKey(string ColumnName, bool Ascending);

public sealed class MainForm : Form
{
    private static readonly Color CurrentCellSelectionBackColor = UiTheme.ActiveCell;
    private static readonly Color MatchHighlightBackColor = UiTheme.Match;
    private const int SortGlyphReservedHeaderWidth = 30;
    private const int MinimumSortableHeaderWidth = 56;
    private const int GridHorizontalPadding = 8;

    private readonly Button openButton = new();
    private readonly Button saveButton = new();
    private readonly Button reloadButton = new();
    private readonly Label fileNameLabel = new();
    private readonly TextBox fileNameEditBox = new();
    private readonly Panel cellEditPanel = new();
    private readonly TextBox cellEditBox = new();
    private readonly CsvDataGridView grid = new();
    private readonly StatusStrip statusStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly ToolTip toolTip = new();
    private readonly ContextMenuStrip gridMenu = new();
    private readonly ToolStripMenuItem copyItem = new("Copy");
    private readonly ToolStripMenuItem copyHeaderItem = new("Copy Header");
    private readonly ToolStripMenuItem copyRowsAsCsvItem = new("Copy as CSV");

    private readonly Button searchButton = new();
    private readonly Button filterButton = new();
    private readonly Button pasteButton = new();
    private SearchDialog? searchDialog;
    private string lastSearchQuery = string.Empty;
    private bool lastSearchWholeCell;

    private string? currentFilePath;
    private bool isLoading;
    private bool isUpdatingCellEditBox;
    private bool isUpdatingGridFromCellEditBox;
    private bool isDirty;
    private bool isRenamingFile;
    private bool isCommittingFileRename;
    private bool isUpdatingRowSelection;
    private int contextColumnIndex = -1;
    private int contextRowIndex = -1;
    private int rowSelectionAnchorIndex = -1;
    private List<(int RowIndex, int ColumnIndex)> currentMatches = new();
    private int currentMatchIndex = -1;

    private readonly Button multiSortButton = new();
    private readonly List<SortKey> sortKeys = new();
    private StringFilter? activeFilter;

    private readonly ToolStripMenuItem pasteHeadersItem = new("Paste Headers...");
    private readonly ToolStripMenuItem pasteDataItem = new("Paste Data...");

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
            Shown += async (_, _) => await LoadCsvFileAsync(initialPath);
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

        pasteButton.SetBounds(8, 6, 34, 30);
        UiTheme.ApplyIconButton(pasteButton, toolTip, UiIconKind.Paste, "New CSV View");
        pasteButton.Click += (_, _) => OpenEmptyView();

        openButton.SetBounds(48, 6, 34, 30);
        UiTheme.ApplyIconButton(openButton, toolTip, UiIconKind.Open, "Open CSV");
        openButton.Click += (_, _) => OpenCsvFromDialog();

        saveButton.SetBounds(88, 6, 34, 30);
        UiTheme.ApplyIconButton(saveButton, toolTip, UiIconKind.Save, "Save");
        saveButton.Enabled = false;
        saveButton.Click += (_, _) => SaveCurrentFile(showSavedStatus: true);

        reloadButton.SetBounds(128, 6, 34, 30);
        UiTheme.ApplyIconButton(reloadButton, toolTip, UiIconKind.Reload, "Reload from disk (Ctrl+R)");
        reloadButton.Enabled = false;
        reloadButton.Click += (_, _) => ReloadCurrentFile();

        multiSortButton.SetBounds(168, 6, 34, 30);
        UiTheme.ApplyIconButton(multiSortButton, toolTip, UiIconKind.Sort, "Multi-sort");
        multiSortButton.Enabled = false;
        multiSortButton.Click += (_, _) => OpenMultiSortDialog();

        searchButton.SetBounds(208, 6, 34, 30);
        UiTheme.ApplyIconButton(searchButton, toolTip, UiIconKind.Search, "Search (Ctrl+F)");
        searchButton.Enabled = false;
        searchButton.Click += (_, _) => OpenSearchDialog();

        filterButton.SetBounds(248, 6, 34, 30);
        UiTheme.ApplyIconButton(filterButton, toolTip, UiIconKind.Filter, "Filter rows");
        filterButton.Enabled = false;
        filterButton.Click += (_, _) => OpenFilterDialog();

        fileNameLabel.Text = "No file loaded";
        fileNameLabel.AutoEllipsis = true;
        fileNameLabel.ForeColor = UiTheme.MutedText;
        fileNameLabel.SetBounds(288, 12, 340, 20);
        fileNameLabel.DoubleClick += (_, _) => BeginFileRename();

        fileNameEditBox.SetBounds(288, 9, 340, 24);
        fileNameEditBox.BorderStyle = BorderStyle.FixedSingle;
        fileNameEditBox.BackColor = UiTheme.Surface;
        fileNameEditBox.ForeColor = UiTheme.Text;
        fileNameEditBox.Visible = false;
        fileNameEditBox.KeyDown += FileNameEditBoxOnKeyDown;
        fileNameEditBox.Leave += (_, _) => CommitFileRename();

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

        topPanel.Controls.Add(pasteButton);
        topPanel.Controls.Add(openButton);
        topPanel.Controls.Add(saveButton);
        topPanel.Controls.Add(reloadButton);
        topPanel.Controls.Add(multiSortButton);
        topPanel.Controls.Add(searchButton);
        topPanel.Controls.Add(filterButton);
        topPanel.Controls.Add(fileNameLabel);
        topPanel.Controls.Add(fileNameEditBox);
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
        grid.RowHeadersVisible = true;
        grid.RowHeadersWidth = 48;
        grid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
        grid.MultiSelect = true;
        grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.RowHeadersDefaultCellStyle.BackColor = UiTheme.Surface;
        grid.RowHeadersDefaultCellStyle.ForeColor = UiTheme.MutedText;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Selection;
        grid.RowHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.DefaultCellStyle.BackColor = UiTheme.Surface;
        grid.DefaultCellStyle.ForeColor = UiTheme.Text;
        grid.DefaultCellStyle.SelectionBackColor = CurrentCellSelectionBackColor;
        grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 251);
        grid.MouseDown += GridOnMouseDown;
        grid.CellMouseDown += GridOnCellMouseDown;
        grid.RowHeaderMouseDown += GridOnRowHeaderMouseDown;
        grid.SelectionChanged += UpdateGridSelectionHighlight;
        grid.DataBindingComplete += GridOnDataBindingComplete;
        grid.CellPainting += GridOnCellPainting;
        grid.ColumnHeaderMouseClick += GridOnColumnHeaderMouseClick;
        grid.CellValueChanged += (_, _) =>
        {
            if (!isUpdatingGridFromCellEditBox)
            {
                UpdateCellEditBoxFromCurrentCell();
            }

            MarkDirty();
            RefreshSearchIfActive();
            if (activeFilter is not null && grid.DataSource is DataTable table)
            {
                UpdateFilterStatus(table);
            }
        };

        gridMenu.Items.Add(pasteHeadersItem);
        gridMenu.Items.Add(pasteDataItem);
        gridMenu.Items.Add(new ToolStripSeparator());
        gridMenu.Items.Add(copyItem);
        gridMenu.Items.Add(copyHeaderItem);
        gridMenu.Items.Add(copyRowsAsCsvItem);
        gridMenu.Opening += (_, e) =>
        {
            var isClipboardView = currentFilePath is null && grid.DataSource is DataTable;
            var isColumnHeader = contextRowIndex < 0 && contextColumnIndex >= 0;
            var isSelectedRow = contextRowIndex >= 0
                && contextRowIndex < grid.Rows.Count
                && grid.Rows[contextRowIndex].Selected;
            var isCell = contextRowIndex >= 0 && contextColumnIndex >= 0 && !isSelectedRow;

            pasteHeadersItem.Visible = isClipboardView;
            pasteDataItem.Visible = isClipboardView;
            copyItem.Visible = isCell || isSelectedRow;
            copyHeaderItem.Visible = isColumnHeader;
            copyRowsAsCsvItem.Visible = isSelectedRow;
            e.Cancel = !isClipboardView && !isCell && !isColumnHeader && !isSelectedRow;
        };
        pasteHeadersItem.Click += (_, _) => PasteHeadersIntoGrid();
        pasteDataItem.Click += (_, _) => PasteDataIntoGrid();
        copyItem.Click += (_, _) => CopySelection();
        copyHeaderItem.Click += (_, _) => CopySelectedHeader();
        copyRowsAsCsvItem.Click += (_, _) => CopySelectedRowsAsCsv();
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
        if (ReferenceEquals(control, grid) || ReferenceEquals(control, cellEditBox) || ReferenceEquals(control, fileNameEditBox))
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
        CommitFileRename();
        CommitGridInputAndClearSelection();
    }

    private void GridOnMouseDown(object? sender, MouseEventArgs e)
    {
        CommitFileRename();
        var hit = grid.HitTest(e.X, e.Y);
        if (hit.Type == DataGridViewHitTestType.RowHeader && hit.RowIndex >= 0)
        {
            grid.EndEdit();
            return;
        }

        if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
        {
            rowSelectionAnchorIndex = -1;
            return;
        }

        CommitGridInputAndClearSelection();
    }

    private void CommitGridInputAndClearSelection()
    {
        grid.EndEdit();
        grid.ClearSelection();
        grid.CurrentCell = null;
        rowSelectionAnchorIndex = -1;
        grid.Invalidate();
        UpdateCellEditBoxFromCurrentCell();
    }

    private void UpdateGridSelectionHighlight(object? sender, EventArgs e)
    {
        if (isUpdatingRowSelection)
        {
            return;
        }

        grid.Invalidate();
        if (!isUpdatingGridFromCellEditBox)
        {
            UpdateCellEditBoxFromCurrentCell();
        }

        var selectedRowCount = grid.SelectedRows.Count;
        if (selectedRowCount > 0)
        {
            SetStatus(selectedRowCount == 1
                ? "1 row selected; Shift+click for a range, Ctrl+click to select more"
                : $"{selectedRowCount} rows selected; right-click to copy as CSV");
            return;
        }
    }

    private void GridOnDataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        // DataBindingComplete 也会在编辑绑定单元格后触发。只有加载文件时才清除默认选中，
        // 否则会把正在通过顶部编辑框修改的 CurrentCell 置空，使编辑框清空并变成只读。
        if (isLoading)
        {
            grid.ClearSelection();
            grid.CurrentCell = null;
            rowSelectionAnchorIndex = -1;
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.DefaultCellStyle.BackColor = Color.Empty;
            }
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

        UpdateCellEditBoxFromCurrentCell();
    }

    private void GridOnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is null)
        {
            return;
        }

        if (e.ColumnIndex < 0 && e.RowIndex >= 0)
        {
            var isHighlighted = grid.Rows[e.RowIndex].Selected
                || (grid.SelectedRows.Count == 0
                    && grid.CurrentCell?.Selected == true
                    && grid.CurrentCell.RowIndex == e.RowIndex);
            using var backgroundBrush = new SolidBrush(isHighlighted ? UiTheme.Selection : UiTheme.Surface);
            e.Graphics.FillRectangle(backgroundBrush, e.CellBounds);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            var font = grid.RowHeadersDefaultCellStyle.Font ?? grid.Font;
            var foreColor = isHighlighted ? UiTheme.Text : UiTheme.MutedText;
            TextRenderer.DrawText(
                e.Graphics,
                (e.RowIndex + 1).ToString(),
                font,
                e.CellBounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Handled = true;
            return;
        }

        if (e.RowIndex < 0
            && e.ColumnIndex >= 0
            && grid.SelectedRows.Count == 0
            && grid.CurrentCell?.Selected == true
            && grid.CurrentCell.ColumnIndex == e.ColumnIndex)
        {
            using var backgroundBrush = new SolidBrush(UiTheme.Selection);
            e.Graphics.FillRectangle(backgroundBrush, e.CellBounds);
            e.Paint(
                e.ClipBounds,
                DataGridViewPaintParts.Border | DataGridViewPaintParts.ContentForeground);
            e.Handled = true;
        }
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

        isUpdatingGridFromCellEditBox = true;
        try
        {
            grid.CurrentCell.Value = cellEditBox.Text;
        }
        finally
        {
            isUpdatingGridFromCellEditBox = false;
        }
    }

    private void GridOnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        // 表头单击 = 单列排序（替换多列排序）。
        // 同一列重复点击在 升序 → 降序 → 取消 间轮转；取消后若链空则恢复文件原始顺序。
        if (e.Button != MouseButtons.Left
            || grid.DataSource is not DataTable table
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

    private void OpenFilterDialog()
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        var columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        using var dialog = new FilterDialog(columns, activeFilter);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplyFilter(dialog.Result);
        }
    }

    private void ApplyFilter(StringFilter? filter)
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        var previousExpression = table.DefaultView.RowFilter;
        try
        {
            table.DefaultView.RowFilter = filter?.ToRowFilterExpression() ?? string.Empty;
        }
        catch (Exception ex)
        {
            table.DefaultView.RowFilter = previousExpression;
            MessageBox.Show(this, ex.Message, "Filter failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        activeFilter = filter;
        CommitGridInputAndClearSelection();
        RefreshSearchIfActive();
        UpdateFilterStatus(table);
    }

    private void UpdateFilterStatus(DataTable table)
    {
        SetStatus(activeFilter is null
            ? $"Loaded {table.Rows.Count} rows, {table.Columns.Count} columns"
            : $"Filtered: {table.DefaultView.Count} / {table.Rows.Count} rows");
    }

    // ────────────── Paste / New CSV View ──────────────

    /// <summary>当前是否为无文件剪贴板视图。</summary>
    private bool IsClipboardView => currentFilePath is null && grid.DataSource is DataTable;

    /// <summary>
    /// 新建 CSV 视图：显示一个完全空白（无列无行）的表格，
    /// 等待用户通过右键菜单 → Paste Headers / Paste Data 填充。
    /// </summary>
    private void OpenEmptyView()
    {
        if (isLoading)
        {
            return;
        }

        var emptyTable = new DataTable();
        grid.SuspendLayout();
        grid.DataSource = emptyTable;
        sortKeys.Clear();
        activeFilter = null;
        multiSortButton.Enabled = false;
        searchButton.Enabled = false;
        filterButton.Enabled = false;
        reloadButton.Enabled = false;
        CommitGridInputAndClearSelection();
        grid.ResumeLayout();

        currentFilePath = null;
        SetDirty(false);
        UpdateFileNameDisplay();
        Text = "CSVHelper - (clipboard)";
        ResetSearch();
        if (searchDialog is { IsDisposed: false } dialog)
        {
            dialog.ClearInput();
        }
        SetStatus("Empty view — right-click to paste headers or data");
    }

    /// <summary>
    /// 右键菜单：仅粘贴表头到空视图。
    /// </summary>
    private void PasteHeadersIntoGrid()
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        using var dialog = new PasteHeadersDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var headers = dialog.ParsedHeaders;
            // 保留已有数据行（按列索引对齐到新表头），表头决定最终列数。
            var existingRows = table.Rows.Cast<DataRow>()
                .Select(row => table.Columns.Cast<DataColumn>()
                    .Select(c => row[c]?.ToString() ?? string.Empty)
                    .ToArray())
                .ToList();
            var newTable = CsvDocument.CreateTableFixedColumns(headers, existingRows);
            grid.DataSource = newTable;
            multiSortButton.Enabled = true;
            searchButton.Enabled = true;
            filterButton.Enabled = true;
            SetStatus(existingRows.Count == 0
                ? $"Empty table — {headers.Length} columns. Right-click again to paste data."
                : $"Applied {headers.Length} headers to {existingRows.Count} row(s)");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Paste failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 右键菜单：粘贴 CSV 数据。粘贴内容全部为数据行（不抽表头）。
    /// 表头规则：已有表头则沿用（数据更宽时追加 "Column N"）；否则按列数生成空表头。
    /// </summary>
    private void PasteDataIntoGrid()
    {
        if (grid.DataSource is not DataTable table)
        {
            return;
        }

        using var dialog = new PasteDataDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var dataRows = dialog.ParsedRows;
            if (dataRows.Count == 0)
            {
                return;
            }

            var dataColumnCount = dataRows.Max(row => row.Length);
            var headers = ResolveHeaders(table, dataColumnCount);
            var newTable = CsvDocument.CreateTable(headers, dataRows);
            grid.DataSource = newTable;
            multiSortButton.Enabled = true;
            searchButton.Enabled = true;
            filterButton.Enabled = true;
            SetStatus($"Loaded {dataRows.Count} rows, {headers.Length} columns");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Paste failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 推导粘贴数据所需的表头：沿用已有表头，不足部分补 "Column N"；无表头则全部生成。
    /// </summary>
    private static string[] ResolveHeaders(DataTable table, int dataColumnCount)
    {
        var existing = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var length = Math.Max(existing.Count, dataColumnCount);
        var headers = new string[length];
        for (var i = 0; i < length; i++)
        {
            headers[i] = i < existing.Count ? existing[i] : $"Column {i + 1}";
        }

        return headers;
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
            _ = LoadCsvFileAsync(dialog.FileName);
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

        _ = LoadCsvFileAsync(currentFilePath);
    }

    private async Task LoadCsvFileAsync(string path)
    {
        isLoading = true;
        SetLoadingUi(isLoading: true);
        SetLoadProgress("Loading file...");
        Refresh();

        try
        {
            IProgress<(int Value, string Text)> progress = new Progress<(int Value, string Text)>(
                update => SetLoadProgress(update.Text));
            var table = await Task.Run(() => CsvDocument.LoadTableAsync(path, progress));

            grid.SuspendLayout();
            grid.DataSource = table;
            sortKeys.Clear();
            activeFilter = null;
            multiSortButton.Enabled = true;
            searchButton.Enabled = true;
            filterButton.Enabled = true;
            CommitGridInputAndClearSelection();
            grid.ResumeLayout();
            currentFilePath = path;
            SetDirty(false);
            UpdateFileNameDisplay();
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
            SetLoadingUi(isLoading: false);
        }
    }

    private void SetLoadingUi(bool isLoading)
    {
        openButton.Enabled = !isLoading;
        saveButton.Enabled = !isLoading;
        reloadButton.Enabled = !isLoading && currentFilePath is not null;
        multiSortButton.Enabled = !isLoading && grid.DataSource is DataTable && !IsClipboardView;
        searchButton.Enabled = !isLoading && grid.DataSource is DataTable && !IsClipboardView;
        filterButton.Enabled = !isLoading && grid.DataSource is DataTable && !IsClipboardView;
    }

    private void SetLoadProgress(string text)
    {
        SetStatus(text);
    }

    private bool SaveCurrentFile(bool showSavedStatus)
    {
        if (isLoading || grid.DataSource is not DataTable table)
        {
            return false;
        }

        // 无文件路径（剪贴板视图）→ 弹出另存为
        if (string.IsNullOrWhiteSpace(currentFilePath))
        {
            return SaveAs(table, showSavedStatus);
        }

        return SaveToPath(currentFilePath, table, showSavedStatus);
    }

    private bool SaveAs(DataTable table, bool showSavedStatus)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Save CSV As",
            FileName = "export.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        if (SaveToPath(dialog.FileName, table, showSavedStatus))
        {
            currentFilePath = dialog.FileName;
            UpdateFileNameDisplay();
            Text = $"CSVHelper - {Path.GetFileName(currentFilePath)}";
            SetDirty(isDirty);
            return true;
        }

        return false;
    }

    private bool SaveToPath(string path, DataTable table, bool showSavedStatus)
    {
        try
        {
            var headers = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
            var rows = table.Rows.Cast<DataRow>()
                .Select(row => table.Columns.Cast<DataColumn>()
                    .Select(column => row[column]?.ToString() ?? string.Empty)
                    .ToArray())
                .ToList();

            var document = new CsvDocument(headers, rows);
            File.WriteAllText(path, document.ToCsvText(), new UTF8Encoding(false));
            SetDirty(false);
            if (showSavedStatus)
            {
                SetStatus("Saved");
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Save failed");
            return false;
        }
    }

    private void BeginFileRename()
    {
        if (isLoading || string.IsNullOrWhiteSpace(currentFilePath) || isRenamingFile)
        {
            return;
        }

        isRenamingFile = true;
        fileNameEditBox.Text = Path.GetFileName(currentFilePath);
        fileNameLabel.Visible = false;
        fileNameEditBox.Visible = true;
        fileNameEditBox.Focus();
        fileNameEditBox.SelectAll();
    }

    private void FileNameEditBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CommitFileRename();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            CancelFileRename();
            e.SuppressKeyPress = true;
        }
    }

    private void CommitFileRename()
    {
        if (!isRenamingFile || isCommittingFileRename || string.IsNullOrWhiteSpace(currentFilePath))
        {
            return;
        }

        isCommittingFileRename = true;
        try
        {
            if (!CsvFileNameRules.TryCreateTargetPath(
                    currentFilePath,
                    fileNameEditBox.Text,
                    out var targetPath,
                    out var errorMessage))
            {
                ShowFileRenameError(errorMessage);
                return;
            }

            if (string.Equals(currentFilePath, targetPath, StringComparison.Ordinal))
            {
                CancelFileRename();
                return;
            }

            if (isDirty)
            {
                var result = MessageBox.Show(
                    this,
                    "Save changes before renaming?",
                    "Rename CSV",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    CancelFileRename();
                    return;
                }

                if (result == DialogResult.Yes)
                {
                    grid.EndEdit();
                    if (!SaveCurrentFile(showSavedStatus: false))
                    {
                        return;
                    }
                }
            }

            if (File.Exists(targetPath))
            {
                ShowFileRenameError("A file with this name already exists.");
                return;
            }

            File.Move(currentFilePath, targetPath);
            currentFilePath = targetPath;
            UpdateFileNameDisplay();
            SetDirty(isDirty);
            SetStatus("Renamed");
            EndFileRename();
        }
        catch (Exception ex)
        {
            ShowFileRenameError(ex.Message);
        }
        finally
        {
            isCommittingFileRename = false;
        }
    }

    private void CancelFileRename()
    {
        EndFileRename();
    }

    private void EndFileRename()
    {
        isRenamingFile = false;
        fileNameEditBox.Visible = false;
        fileNameLabel.Visible = true;
    }

    private void ShowFileRenameError(string message)
    {
        MessageBox.Show(this, message, "Rename CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        fileNameEditBox.Focus();
        fileNameEditBox.SelectAll();
    }

    private void UpdateFileNameDisplay()
    {
        if (string.IsNullOrWhiteSpace(currentFilePath))
        {
            fileNameLabel.Text = "No file loaded";
            fileNameLabel.Cursor = Cursors.Default;
            return;
        }

        fileNameLabel.Text = Path.GetFileName(currentFilePath);
        fileNameLabel.Cursor = Cursors.Hand;
        toolTip.SetToolTip(fileNameLabel, "Double-click to rename");
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
            if (grid.Rows[e.RowIndex].Selected)
            {
                return;
            }

            grid.ClearSelection();
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.Selected = true;
            grid.CurrentCell = cell;
        }
    }

    private void GridOnRowHeaderMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right))
        {
            return;
        }

        CommitFileRename();
        grid.EndEdit();

        var toggleSelection = e.Button == MouseButtons.Left
            && (ModifierKeys & Keys.Control) == Keys.Control;
        var selectRange = e.Button == MouseButtons.Left
            && (ModifierKeys & Keys.Shift) == Keys.Shift
            && rowSelectionAnchorIndex >= 0
            && rowSelectionAnchorIndex < grid.Rows.Count;
        SelectRowFromHeader(e.RowIndex, selectRange, toggleSelection, e.Button == MouseButtons.Right);

        if (e.Button == MouseButtons.Left && !selectRange)
        {
            rowSelectionAnchorIndex = e.RowIndex;
        }

        contextColumnIndex = -1;
        contextRowIndex = e.Button == MouseButtons.Right ? e.RowIndex : -1;
        grid.Invalidate();
    }

    private void SelectRowFromHeader(
        int rowIndex,
        bool selectRange,
        bool toggleSelection,
        bool preserveOnRightClick)
    {
        var selectedRowIndexes = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .ToArray();
        var clickedRowWasSelected = selectedRowIndexes.Contains(rowIndex);

        isUpdatingRowSelection = true;
        try
        {
            grid.CurrentCell = null;
            grid.ClearSelection();

            if (selectRange)
            {
                var firstRowIndex = Math.Min(rowSelectionAnchorIndex, rowIndex);
                var lastRowIndex = Math.Max(rowSelectionAnchorIndex, rowIndex);
                for (var selectedRowIndex = firstRowIndex; selectedRowIndex <= lastRowIndex; selectedRowIndex++)
                {
                    grid.Rows[selectedRowIndex].Selected = true;
                }
            }
            else if (toggleSelection)
            {
                foreach (var selectedRowIndex in selectedRowIndexes)
                {
                    if (selectedRowIndex != rowIndex)
                    {
                        grid.Rows[selectedRowIndex].Selected = true;
                    }
                }

                if (!clickedRowWasSelected)
                {
                    grid.Rows[rowIndex].Selected = true;
                }
            }
            else if (preserveOnRightClick && clickedRowWasSelected)
            {
                foreach (var selectedRowIndex in selectedRowIndexes)
                {
                    grid.Rows[selectedRowIndex].Selected = true;
                }
            }
            else
            {
                grid.Rows[rowIndex].Selected = true;
            }
        }
        finally
        {
            isUpdatingRowSelection = false;
        }

        UpdateGridSelectionHighlight(grid, EventArgs.Empty);
    }

    private void CopySelection()
    {
        if (contextRowIndex >= 0
            && contextRowIndex < grid.Rows.Count
            && grid.Rows[contextRowIndex].Selected)
        {
            CopySelectedRows();
            return;
        }

        if (contextRowIndex < 0 || contextColumnIndex < 0)
        {
            return;
        }

        Clipboard.SetText(grid.Rows[contextRowIndex].Cells[contextColumnIndex].Value?.ToString() ?? string.Empty);
        SetStatus("Cell copied");
    }

    private void CopySelectedRows()
    {
        var columns = grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(column => column.Visible)
            .OrderBy(column => column.DisplayIndex)
            .ToArray();
        var selectedRows = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .ToArray();

        if (columns.Length == 0 || selectedRows.Length == 0)
        {
            return;
        }

        var text = string.Join(
            "\r\n",
            selectedRows.Select(row => string.Join(
                "\t",
                columns.Select(column => EscapeClipboardField(
                    Convert.ToString(row.Cells[column.Index].Value) ?? string.Empty)))));
        Clipboard.SetText(text);
        SetStatus($"Copied {selectedRows.Length} row(s)");
    }

    private static string EscapeClipboardField(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny(['\t', '"', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
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

    private void CopySelectedRowsAsCsv()
    {
        var columns = grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(column => column.Visible)
            .OrderBy(column => column.DisplayIndex)
            .ToArray();
        var selectedRows = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .ToArray();

        if (columns.Length == 0 || selectedRows.Length == 0)
        {
            return;
        }

        var headers = columns.Select(column => column.HeaderText).ToArray();
        var rows = selectedRows
            .Select(row => columns
                .Select(column => Convert.ToString(row.Cells[column.Index].Value) ?? string.Empty)
                .ToArray())
            .ToList();
        Clipboard.SetText(new CsvDocument(headers, rows).ToCsvText());
        SetStatus($"Copied {selectedRows.Length} row(s) as CSV");
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
    }

    private void MarkDirty()
    {
        if (isLoading)
        {
            return;
        }

        SetDirty(true);
        if (currentFilePath is not null)
        {
            SetStatus("Modified");
        }
    }

    private void SetDirty(bool dirty)
    {
        isDirty = dirty;
        saveButton.Enabled = dirty || IsClipboardView;
        reloadButton.Enabled = currentFilePath is not null;

        if (currentFilePath is null)
        {
            Text = "CSVHelper - (clipboard)";
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
        if (!isDirty && !IsClipboardView)
        {
            return;
        }

        if (isDirty)
        {
            var result = MessageBox.Show(
                this,
                currentFilePath is not null ? "Save changes before closing?" : "Save clipboard content before closing?",
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
