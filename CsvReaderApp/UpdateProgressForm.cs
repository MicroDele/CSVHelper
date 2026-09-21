namespace CsvReaderApp;

/// <summary>
/// 更新下载进度小窗：置顶、无关闭按钮，真实百分比进度。
/// 下载在后台进行，完成后由 ApplyUpdatesAndRestart 自动退出并启动新版。
/// </summary>
internal sealed class UpdateProgressForm : Form
{
    private readonly Label label = new();
    private readonly ProgressBar bar = new();
    private readonly string versionText;

    /// <param name="newVersion">正在下载的版本号（展示用）。</param>
    public UpdateProgressForm(string newVersion)
    {
        versionText = newVersion;
        Text = "CSVHelper 更新";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        TopMost = true;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 110);
        UiTheme.ApplyForm(this);

        label.Text = $"正在下载更新 {versionText}...";
        label.SetBounds(16, 20, 348, 20);
        label.ForeColor = UiTheme.Text;
        label.TextAlign = ContentAlignment.MiddleLeft;

        bar.SetBounds(16, 52, 348, 22);
        bar.Minimum = 0;
        bar.Maximum = 100;

        Controls.Add(label);
        Controls.Add(bar);
    }

    /// <summary>汇报下载进度（0-100）。线程安全：回调可能来自后台线程。</summary>
    public void Report(int progress)
    {
        var value = Math.Clamp(progress, 0, 100);
        if (!IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => Apply(value));
        }
        else
        {
            Apply(value);
        }
    }

    private void Apply(int value)
    {
        bar.Value = value;
        label.Text = $"正在下载更新 {versionText}... {value}%";
    }
}
