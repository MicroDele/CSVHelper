using System.Text.Json;

namespace CsvReaderApp;

/// <summary>
/// 应用级用户设置（%APPDATA%\CSVHelper\settings.json）。
/// 与预设同目录，卸载询问的删除范围内。损坏/缺失时回退默认值。
/// </summary>
internal sealed record AppSettings(string? IgnoredUpdateVersion = null)
{
    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CSVHelper", "settings.json");

    public static AppSettings Load() => Load(DefaultPath);

    /// <param name="path">设置文件路径（测试注入用）。</param>
    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new();
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<AppSettings>(stream) ?? new();
        }
        catch
        {
            // 文件损坏、占用等：当作没有设置
            return new();
        }
    }

    public void Save() => Save(DefaultPath);

    /// <param name="path">设置文件路径（测试注入用）。</param>
    public void Save(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch
        {
            // 保存失败静默：设置非关键数据
        }
    }
}
