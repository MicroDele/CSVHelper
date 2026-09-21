namespace CsvReaderApp;

/// <summary>
/// 保存预设时输入名称的小对话框。可预填已有名称（保存即覆盖同名预设）。
/// </summary>
internal sealed class PresetNameDialog : Form
{
    private readonly Label label = new();
    private readonly TextBox box = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    /// <param name="initialName">预填名称（覆盖已有预设时传入其名）；无则传空。</param>
    public PresetNameDialog(string initialName = "")
    {
        Text = "Save Preset";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 108);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();
        box.Text = initialName;

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

    /// <summary>用户输入的预设名（未 trim，由调用方决定处理方式）。</summary>
    public string PresetName => box.Text;

    private void BuildUi()
    {
        label.Text = "Preset name:";
        label.SetBounds(16, 14, 388, 20);
        label.ForeColor = UiTheme.Text;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = new Font(label.Font, FontStyle.Bold);

        box.SetBounds(16, 40, 388, 24);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = UiTheme.Surface;
        box.ForeColor = UiTheme.Text;
        box.Font = new Font("Consolas", 9.5F);

        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "Save preset", primary: true);
        okButton.SetBounds(320, 74, 40, 30);
        okButton.Click += (_, _) =>
        {
            if (box.Text.Trim().Length > 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

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
}
