using System.IO;
using System.Text.Json;

namespace HarmonicaPlayer;

public sealed record AppSettings
{
    public HotkeyBinding Start { get; init; } = HotkeyBinding.DefaultStart;
    public HotkeyBinding Stop { get; init; } = HotkeyBinding.DefaultStop;
    public int Bpm { get; init; } = 120;
    public int Gap { get; init; } = 20;
    public int PreviewVolume { get; init; } = 60;
    // 曲谱库（可选）：旧版本设置文件没有该字段，读取后为空列表，行为与旧版一致。
    public ScoreLibraryEntry[] Library { get; init; } = Array.Empty<ScoreLibraryEntry>();
    // 曲谱库快捷键按下后是否立即演奏；关闭时只把该曲谱加载到编辑器。
    public bool LibraryHotkeyPlays { get; init; } = true;
    // 上次关闭时的窗口位置与大小（可选）；用于下次启动恢复布局，缺省表示“未记录/居中”。
    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    // 主题（可选）：system / light / dark；旧版本设置文件没有该字段，缺省表示跟随系统。
    // 这里只存字符串，非法值不会让整份设置失效（见 ThemePreference.Parse）。
    public string? Theme { get; init; }
    // 界面语言（可选）：zh / en；旧版本设置文件没有该字段，缺省为中文（保持既有文案与测试基线）。
    // 与主题一样，非法值只回退这一项，不影响快捷键、曲谱库等设置（见 LanguagePreference.Parse）。
    public string? Language { get; init; }

    // 记录自带的相等比较会把数组字段按引用比较：同一份设置经过一次“保存→读取”后，
    // 曲谱库就是新数组实例，会被误判成“有修改”。这里按内容比较，让脏检查与跳过写盘可靠。
    public bool Equals(AppSettings? other) =>
        other is not null &&
        Start == other.Start && Stop == other.Stop &&
        Bpm == other.Bpm && Gap == other.Gap && PreviewVolume == other.PreviewVolume &&
        LibraryHotkeyPlays == other.LibraryHotkeyPlays &&
        WindowWidth == other.WindowWidth && WindowHeight == other.WindowHeight &&
        WindowLeft == other.WindowLeft && WindowTop == other.WindowTop &&
        // 主题在比较前先归一化：null / "Dark " / "dark" 视为同一个值，否则切换主题不会触发保存。
        ThemePreference.Parse(Theme) == ThemePreference.Parse(other.Theme) &&
        // 语言同理：切换语言必须能触发一次保存，而写入后读回不应被误判成“有修改”。
        LanguagePreference.Parse(Language) == LanguagePreference.Parse(other.Language) &&
        SameLibrary(Library, other.Library);

    // 手写 Equals 后必须给出一致的哈希；这里只用标量字段与条数，不把数组元素算进去。
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Start); hash.Add(Stop); hash.Add(Bpm); hash.Add(Gap); hash.Add(PreviewVolume);
        hash.Add(LibraryHotkeyPlays); hash.Add(WindowWidth); hash.Add(WindowHeight);
        hash.Add(WindowLeft); hash.Add(WindowTop); hash.Add(Library?.Length ?? 0);
        hash.Add(ThemePreference.Parse(Theme));
        hash.Add(LanguagePreference.Parse(Language));
        return hash.ToHashCode();
    }

    private static bool SameLibrary(ScoreLibraryEntry[]? left, ScoreLibraryEntry[]? right)
        => ReferenceEquals(left, right) ||
           (left ?? Array.Empty<ScoreLibraryEntry>()).AsSpan().SequenceEqual(right ?? Array.Empty<ScoreLibraryEntry>());

    public void Validate()
    {
        if (Start is null || Stop is null) throw new FormatException(Loc.T("缺少快捷键设置。"));
        HotkeyBinding.ValidatePair(Start, Stop);
        if (Bpm is < 20 or > 300 || Gap is < 10 or > 5000 || PreviewVolume is < 0 or > 100)
            throw new FormatException(Loc.T("设置数值超出允许范围。"));
        // 曲谱库快捷键必须与开始/停止键互不重复，否则注册时会静默失效。
        ScoreLibrary.Validate(Library ?? Array.Empty<ScoreLibraryEntry>(), Start, Stop);
        // 窗口布局是可选记忆项：损坏或异常的数值一律视为不可用，绝不因此阻断启动。
        if (WindowWidth is { } width && (!double.IsFinite(width) || width is < 320 or > 10000))
            throw new FormatException(Loc.T("窗口宽度设置超出允许范围。"));
        if (WindowHeight is { } height && (!double.IsFinite(height) || height is < 320 or > 10000))
            throw new FormatException(Loc.T("窗口高度设置超出允许范围。"));
        if (WindowLeft is { } left && (!double.IsFinite(left) || left is < -100000 or > 100000))
            throw new FormatException(Loc.T("窗口横向位置设置超出允许范围。"));
        if (WindowTop is { } top && (!double.IsFinite(top) || top is < -100000 or > 100000))
            throw new FormatException(Loc.T("窗口纵向位置设置超出允许范围。"));
    }
}

// 主题模式：跟随系统（默认）/ 固定浅色 / 固定深色。
public enum ThemeMode { System, Light, Dark }

// 主题取值的单一来源：设置文件里存 system / light / dark。
// 无法识别的值（例如手工改成 "blue"）一律按“跟随系统”处理，而不是让整份设置回退到默认值。
public static class ThemePreference
{
    public static ThemeMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System
    };

    public static string Serialize(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => "system"
    };
}

public static class SettingsStore
{
    // 数据目录随产品更名改为 HarpKit；首次运行时会把旧目录的设置复制过来（见 MigrateLegacySettings）。
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarpKit", "settings.json");

    // 旧版（HarmonicaPlayer）数据目录：迁移时只读取复制，绝不删除或改写原文件。
    public static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarmonicaPlayer", "settings.json");

    // 产品更名后的一次性迁移：新目录没有设置文件、旧目录有，就复制一份，
    // 让快捷键、曲谱库、主题和窗口位置都不丢。失败时静默跳过，不影响启动。
    public static string? MigrateLegacySettings()
    {
        try
        {
            string target = DefaultPath;
            if (File.Exists(target) || !File.Exists(LegacyPath)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(LegacyPath, target, overwrite: false);
            return LegacyPath;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // 只读预读界面语言：在正式加载设置之前先取一次，避免窗口先按中文绘制再跳成英文。
    // 文件不存在、JSON 损坏或字段缺失都返回 null，由 LanguagePreference.Parse 归到中文。
    public static string? ReadLanguage(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("Language", out var language) &&
                   language.ValueKind == JsonValueKind.String
                ? language.GetString()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    // 只读预读：在正式加载设置之前先取一次主题偏好，避免窗口先按浅色绘制再跳成深色。
    // 文件不存在、JSON 损坏或字段缺失都返回 null，由 ThemePreference.Parse 归到“跟随系统”。
    public static string? ReadTheme(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("Theme", out var theme) &&
                   theme.ValueKind == JsonValueKind.String
                ? theme.GetString()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
    public static AppSettings Load(string path, out string? warning)
    {
        warning = null;
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                ?? throw new FormatException(Loc.T("设置文件为空。"));
            try { settings.Validate(); }
            catch (FormatException e) when (settings.Library is { Length: > 0 })
            {
                // 曲谱库损坏（多为手工编辑或跨版本残留）只清理曲谱库本身，
                // 不能让仍然可用的快捷键、速度等设置一起回退到默认值。
                var repaired = settings with
                {
                    Library = ScoreLibrary.Sanitize(settings.Library, settings.Start, settings.Stop, out var libraryNote)
                };
                try
                {
                    repaired.Validate();
                    warning = (libraryNote ?? Loc.T("曲谱库设置无效，已忽略曲谱库。")) + Loc.F("其他设置未受影响（{0}）。", e.Message);
                    return repaired;
                }
                catch (FormatException)
                {
                    warning = Loc.T("无法读取原设置，已使用默认值：") + e.Message;
                    return new();
                }
            }
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            warning = Loc.T("无法读取原设置，已使用默认值：") + e.Message;
            return new();
        }
    }
    public static void Save(string path, AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
