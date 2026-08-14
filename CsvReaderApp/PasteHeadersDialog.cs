namespace CsvReaderApp;

/// <summary>
/// 仅粘贴表头（列名）的小对话框，逗号分隔。
/// </summary>
internal sealed class PasteHeadersDialog : Form
{
    private readonly Label label = new();
    private readonly TextBox box = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    public PasteHeadersDialog()
    {
        Text = "Paste Headers";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 108);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();

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

    public string[] ParsedHeaders { get; private set; } = [];

    private void BuildUi()
    {
        label.Text = "Column names (comma-separated):";
        label.SetBounds(16, 14, 388, 20);
        label.ForeColor = UiTheme.Text;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = new Font(label.Font, FontStyle.Bold);

        box.SetBounds(16, 40, 388, 24);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = UiTheme.Surface;
        box.ForeColor = UiTheme.Text;
        box.Font = new Font("Consolas", 9.5F);

        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "Apply headers", primary: true);
        okButton.SetBounds(320, 74, 40, 30);
        okButton.Click += (_, _) => Apply();

        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "Cancel");
        cancelButton.SetBounds(364, 74, 40, 30);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = okButton;
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

        ParsedHeaders = raw.Split(',')
            .Select(h => h.Trim())
            .Where(h => h.Length > 0)
            .ToArray();

        if (ParsedHeaders.Length > 0)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
