using System.Text.Json;

namespace CsvReaderApp;

/// <summary>一条表头预设：名称 + 列名数组。</summary>
internal sealed record HeaderPreset(string Name, string[] Headers);

/// <summary>
/// 表头预设的存取（%APPDATA%\CSVHelper\header-presets.json）。
/// 读写失败一律静默降级（空列表 / 返回 false），预设问题不影响主流程。
/// 该目录同时是应用的用户数据目录，卸载时按用户选择整目录删除。
/// </summary>
internal sealed class HeaderPresetStore
{
    /// <summary>用户数据目录：预设等应用级数据的存放处，卸载询问的删除范围。</summary>
    public static string UserDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CSVHelper");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;

    /// <summary>默认存储：%APPDATA%\CSVHelper\header-presets.json。</summary>
    public HeaderPresetStore()
        : this(Path.Combine(UserDataDirectory, "header-presets.json"))
    {
    }

    /// <param name="filePath">存储文件路径（测试注入用）。</param>
    public HeaderPresetStore(string filePath)
    {
        this.filePath = filePath;
    }

    /// <summary>加载全部预设（按保存顺序）；文件不存在或损坏时返回空列表。</summary>
    public IReadOnlyList<HeaderPreset> Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<List<HeaderPreset>>(stream) ?? [];
        }
        catch
        {
            // 文件损坏、占用等：当作没有预设
            return [];
        }
    }

    /// <summary>保存预设；同名覆盖，否则追加到末尾。名称空白或写盘失败返回 false。</summary>
    public bool SavePreset(string name, string[] headers)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        try
        {
            var presets = Load().ToList();
            var replaced = false;
            for (var i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Name, trimmed, StringComparison.Ordinal))
                {
                    presets[i] = new HeaderPreset(trimmed, headers);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                presets.Add(new HeaderPreset(trimmed, headers));
            }

            Persist(presets);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>删除同名预设；不存在或写盘失败返回 false。</summary>
    public bool DeletePreset(string name)
    {
        try
        {
            var presets = Load().ToList();
            var removed = presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (removed == 0)
            {
                return false;
            }

            Persist(presets);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Persist(List<HeaderPreset> presets)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(presets, JsonOptions));
    }
}
