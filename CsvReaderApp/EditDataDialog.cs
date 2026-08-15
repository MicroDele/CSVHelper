namespace CsvReaderApp;

/// <summary>
/// 编辑 CSV 数据的对话框，编辑框加高以展示多行数据，打开时回填已有行。
/// 内容全部视为数据行（不抽取表头）。
/// </summary>
internal sealed class EditDataDialog : Form
{
    private readonly Label label = new();
    private readonly TextBox box = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    /// <param name="currentData">已有数据行（CSV 文本），用于回填；无则传空。</param>
    public EditDataDialog(string currentData = "")
    {
        Text = "Edit Data";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 480);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();
        // 不默认全选：末尾补一个换行（Apply 时会被 Trim 掉），
        // 光标落在最后一行数据之后的空行，方便直接追加。
        if (currentData.Length > 0)
        {
            currentData += "\r\n";
        }

        box.Text = currentData;
        box.SelectionStart = box.Text.Length;
        box.SelectionLength = 0;
        box.ScrollToCaret();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.SuppressKeyPress = true;
            }
        };
    }

    /// <summary>解析后的数据行（每一行都是数据，不含表头）。</summary>
    public List<string[]> ParsedRows { get; private set; } = [];

    private void BuildUi()
    {
        label.Text = "CSV data (every line is a data row — headers are filled automatically):";
        label.SetBounds(16, 14, 568, 20);
        label.ForeColor = UiTheme.Text;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = new Font(label.Font, FontStyle.Bold);

        box.SetBounds(16, 40, 568, 360);
        // Multiline = true 才会让 TextBox 接受上面的高度，否则只有单行。
        box.Multiline = true;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = UiTheme.Surface;
        box.ForeColor = UiTheme.Text;
        box.Font = new Font("Consolas", 9.5F);
        box.AcceptsReturn = true;
        box.AcceptsTab = false;
        box.WordWrap = false;
        box.ScrollBars = ScrollBars.Both;

        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "Apply data", primary: true);
        okButton.SetBounds(500, 420, 40, 30);
        okButton.Click += (_, _) => Apply();

        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "Cancel");
        cancelButton.SetBounds(544, 420, 40, 30);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(label);
        Controls.Add(box);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void Apply()
    {
        var raw = box.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            var rows = CsvDocument.ParseRecords(raw);
            if (rows.Count > 0)
            {
                ParsedRows = rows;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch
        {
            // 解析失败 - 让用户修正
        }
    }
}
