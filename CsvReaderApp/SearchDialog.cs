namespace CsvReaderApp;

// 非模态搜索对话框（Notepad++ 风格）：纯 UI，本身不持有表格引用。
// 手动搜索：点「查找」按钮或回车才触发 SearchRequested（不实时、无防抖）。
// Prev/Next 触发 NavigateRequested(-1/+1) 在已查找的命中间导航。
// 关闭逻辑复用 Form 自带的 FormClosed 事件，宿主据此清除高亮。
public sealed class SearchDialog : Form
{
    private readonly Label findLabel = new();
    private readonly TextBox queryBox = new();
    private readonly Button findButton = new();
    private readonly CheckBox wholeCellCheckBox = new();
    private readonly Button prevButton = new();
    private readonly Button nextButton = new();
    private readonly Label matchCountLabel = new();
    private readonly ToolTip toolTip = new();

    public SearchDialog(string initialQuery, bool initialWholeCell)
    {
        Text = "Search";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 128);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();

        queryBox.Text = initialQuery ?? string.Empty;
        wholeCellCheckBox.Checked = initialWholeCell;

        // KeyPreview 全局捕获 Esc 关闭（右上角 X 之外的键盘关闭方式）。
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.SuppressKeyPress = true;
            }
        };
    }

    public string Query => queryBox.Text;
    public bool WholeCell => wholeCellCheckBox.Checked;

    public event EventHandler? SearchRequested;
    public event EventHandler<int>? NavigateRequested;

    // 由宿主在搜索/导航后回调，刷新计数与 Prev/Next 可用性。
    public void UpdateMatchCount(int current, int total)
    {
        prevButton.Enabled = total > 0;
        nextButton.Enabled = total > 0;
        matchCountLabel.Text = total == 0
            ? (string.IsNullOrEmpty(queryBox.Text) ? string.Empty : "No matches")
            : $"{current + 1} / {total}";
    }

    public void FocusQuery()
    {
        queryBox.Focus();
        queryBox.SelectAll();
    }

    // 宿主切换文件时清空查询。
    public void ClearInput()
    {
        queryBox.Text = string.Empty;
    }

    private void BuildUi()
    {
        findLabel.Text = "Find:";
        findLabel.SetBounds(16, 18, 40, 20);
        findLabel.ForeColor = UiTheme.Text;
        findLabel.TextAlign = ContentAlignment.MiddleLeft;

        queryBox.SetBounds(60, 14, 232, 24);
        queryBox.BorderStyle = BorderStyle.FixedSingle;
        queryBox.BackColor = UiTheme.Surface;
        queryBox.ForeColor = UiTheme.Text;
        queryBox.KeyDown += QueryBoxOnKeyDown;

        UiTheme.ApplyIconButton(findButton, toolTip, UiIconKind.Search, "Find (Enter)");
        findButton.SetBounds(298, 14, 46, 24);
        findButton.Click += (_, _) => SearchRequested?.Invoke(this, EventArgs.Empty);

        wholeCellCheckBox.Text = "Match whole cell";
        wholeCellCheckBox.SetBounds(60, 44, 200, 24);
        wholeCellCheckBox.ForeColor = UiTheme.Text;
        wholeCellCheckBox.BackColor = UiTheme.PageBackground;
        // 切换匹配模式后，若已有查询则立即重搜（无防抖）。
        wholeCellCheckBox.CheckedChanged += (_, _) =>
        {
            if (!string.IsNullOrEmpty(queryBox.Text))
            {
                SearchRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        UiTheme.ApplyIconButton(prevButton, toolTip, UiIconKind.Up, "Previous match (Shift+Enter)");
        prevButton.SetBounds(16, 80, 40, 30);
        prevButton.Enabled = false;
        prevButton.Click += (_, _) => NavigateRequested?.Invoke(this, -1);

        UiTheme.ApplyIconButton(nextButton, toolTip, UiIconKind.Down, "Next match");
        nextButton.SetBounds(62, 80, 40, 30);
        nextButton.Enabled = false;
        nextButton.Click += (_, _) => NavigateRequested?.Invoke(this, 1);

        matchCountLabel.SetBounds(110, 86, 230, 20);
        matchCountLabel.ForeColor = UiTheme.MutedText;
        matchCountLabel.TextAlign = ContentAlignment.MiddleLeft;

        Controls.Add(findLabel);
        Controls.Add(queryBox);
        Controls.Add(findButton);
        Controls.Add(wholeCellCheckBox);
        Controls.Add(prevButton);
        Controls.Add(nextButton);
        Controls.Add(matchCountLabel);
    }

    private void QueryBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            // Enter 查找；Shift+Enter 上一个（Prev/Next 在已查找的命中间导航）。
            if (e.Shift)
            {
                NavigateRequested?.Invoke(this, -1);
            }
            else
            {
                SearchRequested?.Invoke(this, EventArgs.Empty);
            }
            e.SuppressKeyPress = true;
        }
    }
}
