namespace CsvReaderApp;

/// <summary>
/// 剪贴板视图的表格编辑对话框：表头与数据两个编辑框合并于一处，
/// 顶部可从预设选择常用表头，或把当前输入保存为预设。
/// 应用时整体替换表格：表头决定列数，数据更宽时自动补 "Column N"（由 CsvDocument.CreateTable 处理）。
/// </summary>
internal sealed class EditTableDialog : Form
{
    private readonly Label presetLabel = new();
    private readonly ComboBox presetCombo = new();
    private readonly Button savePresetButton = new();
    private readonly Button deletePresetButton = new();
    private readonly Label headersLabel = new();
    private readonly TextBox headersBox = new();
    private readonly Label dataLabel = new();
    private readonly TextBox dataBox = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();
    private readonly HeaderPresetStore store = new();
    private IReadOnlyList<HeaderPreset> presets = [];

    /// <param name="currentHeaders">已有表头（逗号拼接文本），用于回填；无则传空。</param>
    /// <param name="currentData">已有数据行（CSV 文本），用于回填；无则传空。</param>
    public EditTableDialog(string currentHeaders = "", string currentData = "")
    {
        Text = "Edit Table";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 522);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();
        RefreshPresetCombo();
        headersBox.Text = currentHeaders;

        // 数据区不默认全选：末尾补一个换行（Apply 时会被 Trim 掉），
        // 光标落在最后一行数据之后的空行，方便直接追加。
        if (currentData.Length > 0)
        {
            currentData += "\r\n";
        }

        dataBox.Text = currentData;
        dataBox.SelectionStart = dataBox.Text.Length;
        dataBox.SelectionLength = 0;
        dataBox.ScrollToCaret();

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

    /// <summary>应用时的表头（空数组 = 无表头，列数由数据行决定）。</summary>
    public string[] Headers { get; private set; } = [];

    /// <summary>应用时的数据行。</summary>
    public List<string[]> Rows { get; private set; } = [];

    private void BuildUi()
    {
        presetLabel.Text = "Preset:";
        presetLabel.SetBounds(16, 16, 56, 20);
        presetLabel.ForeColor = UiTheme.Text;
        presetLabel.TextAlign = ContentAlignment.MiddleLeft;

        presetCombo.SetBounds(76, 13, 416, 24);
        presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        presetCombo.BackColor = UiTheme.Surface;
        presetCombo.ForeColor = UiTheme.Text;
        presetCombo.SelectedIndexChanged += (_, _) =>
        {
            var index = presetCombo.SelectedIndex;
            deletePresetButton.Enabled = index >= 0;
            if (index >= 0 && index < presets.Count)
            {
                headersBox.Text = string.Join(", ", presets[index].Headers);
            }
        };

        UiTheme.ApplyIconButton(savePresetButton, toolTip, UiIconKind.Save, "Save current headers as preset");
        savePresetButton.SetBounds(500, 10, 40, 30);
        savePresetButton.Click += (_, _) => SavePresetFromInput();

        UiTheme.ApplyIconButton(deletePresetButton, toolTip, UiIconKind.Trash, "Delete selected preset");
        deletePresetButton.SetBounds(544, 10, 40, 30);
        deletePresetButton.Click += (_, _) => DeleteSelectedPreset();

        headersLabel.Text = "Headers (comma-separated):";
        headersLabel.SetBounds(16, 52, 568, 20);
        headersLabel.ForeColor = UiTheme.Text;
        headersLabel.TextAlign = ContentAlignment.MiddleLeft;
        headersLabel.Font = new Font(headersLabel.Font, FontStyle.Bold);

        headersBox.SetBounds(16, 76, 568, 24);
        headersBox.BorderStyle = BorderStyle.FixedSingle;
        headersBox.BackColor = UiTheme.Surface;
        headersBox.ForeColor = UiTheme.Text;
        headersBox.Font = new Font("Consolas", 9.5F);

        dataLabel.Text = "Data (every line is a data row):";
        dataLabel.SetBounds(16, 108, 568, 20);
        dataLabel.ForeColor = UiTheme.Text;
        dataLabel.TextAlign = ContentAlignment.MiddleLeft;
        dataLabel.Font = new Font(dataLabel.Font, FontStyle.Bold);

        dataBox.SetBounds(16, 132, 568, 316);
        // Multiline = true 才会让 TextBox 接受上面的高度，否则只有单行。
        dataBox.Multiline = true;
        dataBox.BorderStyle = BorderStyle.FixedSingle;
        dataBox.BackColor = UiTheme.Surface;
        dataBox.ForeColor = UiTheme.Text;
        dataBox.Font = new Font("Consolas", 9.5F);
        dataBox.AcceptsReturn = true;
        dataBox.AcceptsTab = false;
        dataBox.WordWrap = false;
        dataBox.ScrollBars = ScrollBars.Both;

        UiTheme.ApplyIconButton(okButton, toolTip, UiIconKind.Ok, "Apply table", primary: true);
        okButton.SetBounds(500, 474, 40, 30);
        okButton.Click += (_, _) => Apply();

        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "Cancel");
        cancelButton.SetBounds(544, 474, 40, 30);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = okButton;
        Controls.Add(presetLabel);
        Controls.Add(presetCombo);
        Controls.Add(savePresetButton);
        Controls.Add(deletePresetButton);
        Controls.Add(headersLabel);
        Controls.Add(headersBox);
        Controls.Add(dataLabel);
        Controls.Add(dataBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    /// <summary>重新加载预设列表；传入名称时选中它，否则清空选择。</summary>
    private void RefreshPresetCombo(string? selectName = null)
    {
        presets = store.Load();
        presetCombo.Items.Clear();
        foreach (var preset in presets)
        {
            presetCombo.Items.Add(preset.Name);
        }

        presetCombo.SelectedIndex = selectName is null ? -1 : presetCombo.Items.IndexOf(selectName);
    }

    private void SavePresetFromInput()
    {
        var headers = ParseHeaders(headersBox.Text);
        if (headers.Length == 0)
        {
            MessageBox.Show(this, "Enter headers before saving a preset.", "Save preset",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 预填当前选中的预设名：不改名直接确认即覆盖
        var selectedName = presetCombo.SelectedIndex >= 0 ? presets[presetCombo.SelectedIndex].Name : "";
        using var dialog = new PresetNameDialog(selectedName);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var name = dialog.PresetName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (!store.SavePreset(name, headers))
        {
            MessageBox.Show(this, "Failed to save the preset.", "Save preset",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshPresetCombo(name);
    }

    private void DeleteSelectedPreset()
    {
        if (presetCombo.SelectedIndex < 0 || presetCombo.SelectedIndex >= presets.Count)
        {
            return;
        }

        var name = presets[presetCombo.SelectedIndex].Name;
        var choice = MessageBox.Show(this, $"Delete preset \"{name}\"?", "Delete preset",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (choice != DialogResult.Yes)
        {
            return;
        }

        if (!store.DeletePreset(name))
        {
            MessageBox.Show(this, "Failed to delete the preset.", "Delete preset",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshPresetCombo();
    }

    private void Apply()
    {
        var headers = ParseHeaders(headersBox.Text);
        var dataText = dataBox.Text.Trim();
        if (headers.Length == 0 && dataText.Length == 0)
        {
            return;
        }

        List<string[]> rows = [];
        if (dataText.Length > 0)
        {
            try
            {
                rows = CsvDocument.ParseRecords(dataText);
            }
            catch
            {
                // 解析失败 - 让用户修正
                return;
            }
        }

        Headers = headers;
        Rows = rows;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string[] ParseHeaders(string text) => text.Trim()
        .Split(',')
        .Select(h => h.Trim())
        .Where(h => h.Length > 0)
        .ToArray();
}
