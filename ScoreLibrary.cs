namespace HarmonicaPlayer;

// 曲谱库：把常用 TXT 曲谱列成一张表，每首可以单独绑一个全局快捷键。
// 只描述“哪个文件、显示什么名字、按哪个键”，不参与解析、演奏、试听或文件格式。
public sealed record ScoreLibraryEntry
{
    public const int MaxPathLength = 400;
    public const int MaxTitleLength = 200;
    public string Path { get; init; } = "";
    public string Title { get; init; } = "";
    public HotkeyBinding? Hotkey { get; init; }

    // 显示名：优先用户自定义曲名，否则取文件名；只用于列表和提示，不回写 TXT。
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title)) return Title;
            if (string.IsNullOrWhiteSpace(Path)) return Loc.T("未命名曲谱");
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(Path);
                return string.IsNullOrWhiteSpace(name) ? Loc.T("未命名曲谱") : name;
            }
            catch (ArgumentException) { return Loc.T("未命名曲谱"); }
        }
    }

    public void Validate()
    {
        ValidatePathTitle(Path, Title, DisplayName);
        if (Hotkey is HotkeyBinding hotkey && hotkey.Error() is string error)
            throw new FormatException(Loc.F("曲谱“{0}”的快捷键无效：{1}", DisplayName, error));
    }

    // 路径与曲名的检查独立出来：修复坏数据时可以只清除快捷键而保留条目。
    // 参数允许为 null：手工编辑设置文件时 null 会绕过非空类型，这里统一按“空值”处理而非抛出空引用异常。
    public static void ValidatePathTitle(string? path, string? title, string displayName)
    {
        path ??= ""; title ??= "";
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength || path.Any(char.IsControl))
            throw new FormatException(Loc.T("曲谱库条目缺少有效的文件路径。"));
        // 相对路径会随启动目录变化，直接拒绝，避免“同一首曲子时有时无”。
        if (!System.IO.Path.IsPathFullyQualified(path))
            throw new FormatException(Loc.F("曲谱库必须使用完整路径：{0}", path));
        if (title.Length > MaxTitleLength || title.Any(char.IsControl))
            throw new FormatException(Loc.F("曲谱“{0}”的曲名不能超过{1}个字符，也不能包含控制字符。", displayName, MaxTitleLength));
    }
}

// 曲谱库规则与全局快捷键编号。与界面、Windows API 无关，便于核心测试直接覆盖。
public static class ScoreLibrary
{
    public const int MaxEntries = 60;
    // 全局快捷键编号：1、2 固定给开始/停止，曲谱库从 100 起，避免与既有注册冲突。
    public const int HotkeyIdBase = 100;
    public static int HotkeyId(int index) => HotkeyIdBase + index;
    public static bool IsLibraryHotkeyId(int id) => id >= HotkeyIdBase && id < HotkeyIdBase + MaxEntries;
    public static int IndexFromHotkeyId(int id) => id - HotkeyIdBase;

    public static void Validate(IReadOnlyList<ScoreLibraryEntry> entries, HotkeyBinding start, HotkeyBinding stop)
    {
        if (entries.Count > MaxEntries) throw new FormatException(Loc.F("曲谱库最多保存{0}条曲谱。", MaxEntries));
        var used = new HashSet<HotkeyBinding>();
        foreach (var entry in entries)
        {
            // 设置文件里可以出现 null 数组元素，必须当作坏条目而不是让启动崩溃。
            if (entry is null) throw new FormatException(Loc.T("曲谱库包含空条目。"));
            entry.Validate();
            if (entry.Hotkey is not HotkeyBinding hotkey) continue;
            if (hotkey == start || hotkey == stop)
                throw new FormatException(Loc.F("曲谱“{0}”的快捷键 {1} 与开始/停止键相同，请重新绑定。", entry.DisplayName, hotkey.Label));
            if (!used.Add(hotkey))
                throw new FormatException(Loc.F("快捷键 {0} 被多首曲谱使用，请只保留一个绑定。", hotkey.Label));
        }
    }

    // 检查某个条目的候选快捷键是否可用；返回 null 表示可用，否则是给用户看的原因。
    public static string? Conflict(IReadOnlyList<ScoreLibraryEntry> entries, int index, HotkeyBinding candidate,
        HotkeyBinding start, HotkeyBinding stop)
    {
        if (candidate.Error() is string error) return error;
        if (candidate == start) return Loc.F("与开始键 {0} 相同，请选择其他组合。", start.Label);
        if (candidate == stop) return Loc.F("与停止键 {0} 相同，请选择其他组合。", stop.Label);
        for (int i = 0; i < entries.Count; i++)
            if (i != index && entries[i].Hotkey == candidate)
                return Loc.F("与曲谱“{0}”的快捷键重复。", entries[i].DisplayName);
        return null;
    }

    // 手工改坏或跨版本残留的数据：丢弃无效条目、清除冲突快捷键，但不牵连快捷键/速度等其他设置。
    public static ScoreLibraryEntry[] Sanitize(ScoreLibraryEntry[]? entries, HotkeyBinding start, HotkeyBinding stop,
        out string? warning)
    {
        warning = null;
        if (entries == null || entries.Length == 0) return Array.Empty<ScoreLibraryEntry>();
        var kept = new List<ScoreLibraryEntry>();
        var used = new HashSet<HotkeyBinding>();
        int dropped = 0, cleared = 0;
        foreach (var entry in entries)
        {
            if (kept.Count >= MaxEntries) { dropped++; continue; }
            if (entry is null) { dropped++; continue; }
            try { ScoreLibraryEntry.ValidatePathTitle(entry.Path, entry.Title, entry.DisplayName); }
            catch (FormatException) { dropped++; continue; }
            var hotkey = entry.Hotkey;
            if (hotkey is not null && (hotkey.Error() != null || hotkey == start || hotkey == stop || !used.Add(hotkey)))
            {
                hotkey = null; cleared++;
            }
            // 顺带把 JSON 里的 null 曲名规范成空字符串，避免清理后仍写回 null。
            kept.Add(entry with { Title = entry.Title ?? "", Hotkey = hotkey });
        }
        if (dropped != 0 || cleared != 0)
        {
            var parts = new List<string>();
            if (dropped != 0) parts.Add(Loc.F("忽略{0}条无效条目", dropped));
            if (cleared != 0) parts.Add(Loc.F("清除{0}个无效或冲突的快捷键", cleared));
            warning = Loc.F("曲谱库数据有问题，已自动清理（{0}）。", string.Join(Loc.T("，"), parts));
        }
        return kept.ToArray();
    }

    public static int IndexOfPath(IReadOnlyList<ScoreLibraryEntry> entries, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return -1;
        for (int i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].Path, path, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // 已经被列表占用的快捷键（用于默认建议值）。
    public static bool HotkeyTaken(IReadOnlyList<ScoreLibraryEntry> entries, HotkeyBinding candidate,
        HotkeyBinding start, HotkeyBinding stop)
        => Conflict(entries, -1, candidate, start, stop) != null;
}
