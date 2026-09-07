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
        // 安装后注册 .csv 文件关联，卸载前清理。
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => RegisterCsvFileAssociation())
            .OnBeforeUninstallFastCallback(_ => UnregisterCsvFileAssociation())
            .Run();

        if (args.Length >= 2 && args[0] == "--inspect")
        {
            var text = File.ReadAllText(args[1], new System.Text.UTF8Encoding(false, true));
            var document = CsvDocument.Parse(text);
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

            var result = MessageBox.Show(
                owner,
                $"发现新版本 {update.TargetFullRelease.Version}，是否立即更新？",
                "CSVHelper 更新",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result != DialogResult.Yes)
            {
                return;
            }

            await mgr.DownloadUpdatesAsync(update);
            mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
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
