namespace CsvReaderApp;

/// <summary>
/// 发现新版本时的提示对话框：立即更新 / 暂不更新，可选"忽略该版本"（默认不勾选）。
/// 勾选忽略并选择暂不更新后，该版本不再提示，直到更高版本发布。
/// </summary>
internal sealed class UpdatePromptDialog : Form
{
    private readonly Label label = new();
    private readonly CheckBox ignoreBox = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    /// <param name="newVersion">远端新版本号（如 "1.0.1"）。</param>
    public UpdatePromptDialog(string newVersion)
    {
        Text = "CSVHelper 更新";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 130);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi(newVersion);
    }

    /// <summary>用户勾选了"忽略该版本"（仅在暂不更新时有意义）。</summary>
    public bool IgnoreVersion => ignoreBox.Checked;

    private void BuildUi(string newVersion)
    {
        label.Text = $"发现新版本 {newVersion}，是否立即更新？";
        label.SetBounds(16, 18, 368, 20);
        label.ForeColor = UiTheme.Text;
        label.TextAlign = ContentAlignment.MiddleLeft;

        ignoreBox.Text = "忽略该版本";
        ignoreBox.SetBounds(16, 48, 200, 22);
        ignoreBox.ForeColor = UiTheme.Text;
        ignoreBox.FlatStyle = FlatStyle.Flat;
        ignoreBox.Checked = false;

        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "立即更新", primary: true);
        okButton.SetBounds(300, 86, 40, 30);
        okButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "暂不更新");
        cancelButton.SetBounds(344, 86, 40, 30);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.Add(label);
        Controls.Add(ignoreBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}
