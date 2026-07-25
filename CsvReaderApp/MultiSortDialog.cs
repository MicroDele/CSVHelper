namespace CsvReaderApp;

public sealed class MultiSortDialog : Form
{
    private const string AscGlyph = "↑";
    private const string DescGlyph = "↓";

    private readonly List<string> allColumns;
    private readonly List<SortKey> selected = new();

    private readonly Label leftLabel = new();
    private readonly Label rightLabel = new();
    private readonly ListBox leftList = new();
    private readonly DataGridView rightGrid = new();
    private readonly Button addButton = new();
    private readonly Button removeButton = new();
    private readonly Button upButton = new();
    private readonly Button downButton = new();
    private readonly Button clearButton = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    public MultiSortDialog(IReadOnlyList<string> columns, IReadOnlyList<SortKey> current)
    {
        allColumns = columns.ToList();
        selected.AddRange(current);

        Text = "Multi-Sort";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 420);
        UiTheme.ApplyForm(this);

        BuildUi();
        Populate();
    }

    // 确定 时由调用方读取；取消 则忽略。
    public IReadOnlyList<SortKey> Result => selected;

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 14, 18, 16),
            ColumnCount = 3,
            RowCount = 3,
            BackColor = UiTheme.PageBackground
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        leftLabel.Text = "Available";
        leftLabel.Dock = DockStyle.Fill;
        leftLabel.ForeColor = UiTheme.Text;
        leftLabel.TextAlign = ContentAlignment.MiddleLeft;

        rightLabel.Text = "Sort by (primary → secondary)";
        rightLabel.Dock = DockStyle.Fill;
        rightLabel.ForeColor = UiTheme.Text;
        rightLabel.TextAlign = ContentAlignment.MiddleLeft;

        leftList.Dock = DockStyle.Fill;
        leftList.SelectionMode = SelectionMode.One;
        leftList.IntegralHeight = false;
        leftList.BorderStyle = BorderStyle.FixedSingle;
        leftList.BackColor = UiTheme.Surface;
        leftList.ForeColor = UiTheme.Text;

        rightGrid.Dock = DockStyle.Fill;
        rightGrid.AllowUserToAddRows = false;
        rightGrid.AllowUserToDeleteRows = false;
        rightGrid.AllowUserToResizeRows = false;
        rightGrid.RowHeadersVisible = false;
        rightGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        rightGrid.MultiSelect = false;
        rightGrid.ReadOnly = true;
        rightGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        rightGrid.BackgroundColor = UiTheme.Surface;
        rightGrid.BorderStyle = BorderStyle.FixedSingle;
        rightGrid.ColumnHeadersHeight = 26;
        rightGrid.EnableHeadersVisualStyles = false;
        rightGrid.GridColor = UiTheme.Border;
        rightGrid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Surface;
        rightGrid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        rightGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Surface;
        rightGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
        rightGrid.DefaultCellStyle.BackColor = UiTheme.Surface;
        rightGrid.DefaultCellStyle.ForeColor = UiTheme.Text;
        rightGrid.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
        rightGrid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        rightGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Column",
            HeaderText = "Column",
            ReadOnly = true,
            FillWeight = 160
        });
        rightGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Order",
            HeaderText = "Order",
            FillWeight = 60
        });
        rightGrid.CellClick += RightGridOnCellClick;

        addButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(addButton, toolTip, UiIconKind.Add, "Add selected column");
        addButton.Click += (_, _) => AddSelected();

        removeButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(removeButton, toolTip, UiIconKind.Remove, "Remove selected sort column");
        removeButton.Click += (_, _) => RemoveSelected();

        upButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(upButton, toolTip, UiIconKind.Up, "Move sort column up");
        upButton.Click += (_, _) => MoveSelected(-1);

        downButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(downButton, toolTip, UiIconKind.Down, "Move sort column down");
        downButton.Click += (_, _) => MoveSelected(1);

        clearButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(clearButton, toolTip, UiIconKind.Clear, "Clear sort columns");
        clearButton.Click += (_, _) =>
        {
            selected.Clear();
            Populate();
        };

        cancelButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "Cancel");
        cancelButton.DialogResult = DialogResult.Cancel;

        okButton.Size = new Size(40, 34);
        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "Apply sort", primary: true);
        okButton.DialogResult = DialogResult.OK;

        AcceptButton = okButton;
        CancelButton = cancelButton;

        var middleButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 42, 12, 0),
            BackColor = UiTheme.PageBackground
        };
        middleButtons.Controls.AddRange(new Control[] { addButton, removeButton, upButton, downButton });

        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = UiTheme.PageBackground
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));

        var confirmButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = UiTheme.PageBackground
        };
        confirmButtons.Controls.AddRange(new Control[] { cancelButton, okButton });

        bottomBar.Controls.Add(clearButton, 0, 0);
        bottomBar.Controls.Add(confirmButtons, 2, 0);

        layout.Controls.Add(leftLabel, 0, 0);
        layout.Controls.Add(rightLabel, 2, 0);
        layout.Controls.Add(leftList, 0, 1);
        layout.Controls.Add(middleButtons, 1, 1);
        layout.Controls.Add(rightGrid, 2, 1);
        layout.Controls.Add(bottomBar, 0, 2);
        layout.SetColumnSpan(bottomBar, 3);

        Controls.Add(layout);
    }

    private void Populate()
    {
        rightGrid.Rows.Clear();
        foreach (var key in selected)
        {
            rightGrid.Rows.Add(key.ColumnName, key.Ascending ? AscGlyph : DescGlyph);
        }

        // 左栏 = 未参与排序的列，保持 CSV 原始顺序。
        var used = selected.Select(k => k.ColumnName).ToHashSet();
        leftList.BeginUpdate();
        leftList.Items.Clear();
        foreach (var name in allColumns)
        {
            if (!used.Contains(name))
            {
                leftList.Items.Add(name);
            }
        }
        leftList.EndUpdate();
    }

    private void AddSelected()
    {
        if (leftList.SelectedItem is not string name)
        {
            return;
        }

        selected.Add(new SortKey(name, Ascending: true));
        Populate();
        SelectRightRow(selected.Count - 1);
    }

    private void RemoveSelected()
    {
        var index = RightSelectedIndex();
        if (index < 0)
        {
            return;
        }

        var name = selected[index].ColumnName;
        selected.RemoveAt(index);
        Populate();

        var leftIndex = leftList.Items.IndexOf(name);
        if (leftIndex >= 0)
        {
            leftList.SelectedIndex = leftIndex;
        }
    }

    private void MoveSelected(int delta)
    {
        var index = RightSelectedIndex();
        var target = index + delta;
        if (index < 0 || target < 0 || target >= selected.Count)
        {
            return;
        }

        (selected[index], selected[target]) = (selected[target], selected[index]);
        Populate();
        SelectRightRow(target);
    }

    private void RightGridOnCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        // 点击 Order 列（方向按钮）切换该行的升序/降序。
        if (e.ColumnIndex != 1 || e.RowIndex < 0 || e.RowIndex >= selected.Count)
        {
            return;
        }

        var current = selected[e.RowIndex];
        selected[e.RowIndex] = current with { Ascending = !current.Ascending };
        rightGrid.Rows[e.RowIndex].Cells["Order"].Value =
            selected[e.RowIndex].Ascending ? AscGlyph : DescGlyph;
    }

    private int RightSelectedIndex()
    {
        return rightGrid.CurrentRow?.Index ?? -1;
    }

    private void SelectRightRow(int index)
    {
        if (index < 0 || index >= rightGrid.Rows.Count)
        {
            return;
        }

        rightGrid.ClearSelection();
        rightGrid.Rows[index].Selected = true;
        rightGrid.CurrentCell = rightGrid.Rows[index].Cells["Column"];
    }
}
