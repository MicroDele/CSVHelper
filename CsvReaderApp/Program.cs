using System.Runtime.InteropServices;
using Microsoft.Win32;
using Velopack;

namespace CsvReaderApp;

static class Program
{
    // 更新源：GitHub Releases 的 latest 下载地址（Velopack 会自动读取 releases.win.json 清单）
    private const string UpdateUrl = "https://github.com/MicroDele/CSVHelper/releases/latest/download";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Velopack 更新钩子：处理安装/更新时由更新器传入的参数，普通启动无副作用。
        // 安装后注册 .csv 文件关联，卸载前清理并询问是否删除用户数据。
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => RegisterCsvFileAssociation())
            .OnBeforeUninstallFastCallback(_ =>
            {
                UnregisterCsvFileAssociation();
                PromptDeleteUserData();
            })
            .Run();

        if (args.Length >= 2 && args[0] == "--inspect")
        {
            var document = CsvDocument.LoadAsync(args[1]).GetAwaiter().GetResult();
            var first = document.Rows.Count > 0 && document.Rows[0].Length > 0 ? document.Rows[0][0] : string.Empty;
            var output = $"rows={document.Rows.Count} columns={document.Headers.Length} first={first}";
            if (args.Length >= 3)
            {
                File.WriteAllText(args[2], output);
            }
            else
            {
                Console.WriteLine(output);
            }
            return;
        }

        ApplicationConfiguration.Initialize();

        var form = new MainForm(args.FirstOrDefault());
        form.Shown += async (_, _) => await CheckForUpdatesAsync(form);
        Application.Run(form);
    }

    private static async Task CheckForUpdatesAsync(Form owner)
    {
        try
        {
            var mgr = new UpdateManager(UpdateUrl);
            var update = await mgr.CheckForUpdatesAsync();
            if (update is null)
            {
                return;
            }

            var newVersion = update.TargetFullRelease.Version.ToString();
            var settings = AppSettings.Load();
            // 用户忽略了该版本：不再提示，直到更高版本发布
            if (settings.IgnoredUpdateVersion == newVersion)
            {
                return;
            }

            using var prompt = new UpdatePromptDialog(newVersion, update.TargetFullRelease.NotesMarkdown);
            if (prompt.ShowDialog(owner) != DialogResult.OK)
            {
                if (prompt.IgnoreVersion)
                {
                    settings = settings with { IgnoredUpdateVersion = newVersion };
                    settings.Save();
                }

                return;
            }

            // 立即更新：隐藏主窗 + 置顶进度窗；完成后自动退出并启动新版。
            // 下载失败则恢复主窗，用户现场不丢。
            owner.Hide();
            using var progressForm = new UpdateProgressForm(newVersion);
            progressForm.Show();
            try
            {
                await mgr.DownloadUpdatesAsync(update, progressForm.Report);
                mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
            }
            catch
            {
                progressForm.Close();
                owner.Show();
                MessageBox.Show(owner, "更新下载失败，请稍后重启应用重试。", "CSVHelper 更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch
        {
            // 更新检查或下载失败时静默忽略，不影响正常使用
        }
    }

    /// <summary>
    /// 安装后注册 .csv 文件关联（写入当前用户 HKCU，无需管理员权限）。
    /// 关联目标使用 current 目录（Velopack 的稳定 junction，始终指向最新版本），更新后依然有效。
    /// </summary>
    private static void RegisterCsvFileAssociation()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            // exePath 形如 %LOCALAPPDATA%\CSVHelper\packages\CSVHelper-1.0.0-full\CSVHelper.exe
            // 其上一级目录即 %LOCALAPPDATA%\CSVHelper，其下的 current 是稳定的 junction。
            var rootDir = Path.GetDirectoryName(Path.GetDirectoryName(exePath));
            var stableExe = Path.Combine(rootDir!, "current", "CSVHelper.exe");

            using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.csv");
            extKey.SetValue("", "CSVHelper.csv");

            using var progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CSVHelper.csv");
            progId.SetValue("", "CSV 表格文件");
            using (var iconKey = progId.CreateSubKey("DefaultIcon"))
            {
                iconKey.SetValue("", $"\"{stableExe}\",0");
            }
            using (var cmdKey = progId.CreateSubKey(@"shell\open\command"))
            {
                cmdKey.SetValue("", $"\"{stableExe}\" \"%1\"");
            }
        }
        catch
        {
            // 注册失败不影响程序正常运行
        }
    }

    /// <summary>
    /// 卸载前询问是否删除用户数据（%APPDATA%\CSVHelper：预设等）。
    /// 默认"否"（回车直接保留）；静默卸载（非交互会话）时跳过询问直接保留。
    /// </summary>
    private static void PromptDeleteUserData()
    {
        try
        {
            if (!Environment.UserInteractive)
            {
                return;
            }

            // Win32 MessageBox + MB_TOPMOST/MB_SETFOREGROUND：
            // 卸载进度窗口不归本进程所有，无法作为 owner，
            // .NET MessageBoxOptions 又没有置顶选项——不用 TOPMOST 会被卸载界面遮挡。
            const uint MB_YESNO = 0x00000004;
            const uint MB_ICONQUESTION = 0x00000020;
            const uint MB_DEFBUTTON2 = 0x00000100;
            const uint MB_SETFOREGROUND = 0x00010000;
            const uint MB_TOPMOST = 0x00040000;
            const int IDYES = 6;

            var choice = NativeMessageBox(IntPtr.Zero, "是否同时删除用户数据？", "CSVHelper 卸载",
                MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 | MB_SETFOREGROUND | MB_TOPMOST);
            if (choice != IDYES)
            {
                return;
            }

            if (Directory.Exists(HeaderPresetStore.UserDataDirectory))
            {
                Directory.Delete(HeaderPresetStore.UserDataDirectory, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响卸载
        }
    }

    // 命名避开 WinForms 的 MessageBox 类：同类内标识符会遮蔽它
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int NativeMessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// 卸载前清理 .csv 文件关联。
    /// </summary>
    private static void UnregisterCsvFileAssociation()
    {
        try
        {
            using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.csv", writable: true);
            if (extKey?.GetValue("") as string == "CSVHelper.csv")
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.csv", throwOnMissingSubKey: false);
            }

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CSVHelper.csv", throwOnMissingSubKey: false);
        }
        catch
        {
            // 清理失败不影响卸载
        }
    }
}
