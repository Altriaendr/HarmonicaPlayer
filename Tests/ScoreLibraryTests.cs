using HarmonicaPlayer;
using System.Text.Json;

// 曲谱库规则：编号、校验、冲突、坏数据清理，以及设置文件往返。
// 只覆盖纯逻辑与设置读写；窗口内的行为在 WindowsSmoke 里验证。
static class ScoreLibraryTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("Score library: " + name); passed++; }
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); }
            catch (FormatException) { rejected = true; }
            Check(rejected, name);
        }
        static string Json(object value) => JsonSerializer.Serialize(value);

        string folder = Path.Combine(Path.GetTempPath(), "HarmonicaLibraryTests-" + Guid.NewGuid().ToString("N"));
        string first = Path.Combine(folder, "小星星.txt");
        string second = Path.Combine(folder, "欢乐颂.txt");
        string third = Path.Combine(folder, "送别.txt");
        string fourth = Path.Combine(folder, "生日歌.txt");
        var start = HotkeyBinding.DefaultStart;
        var stop = HotkeyBinding.DefaultStop;
        var f5 = new HotkeyBinding(0x74);
        var ctrlF9 = new HotkeyBinding(0x78, 2);

        // 编号：1、2 固定给开始/停止，曲谱库从 100 起，避免注册冲突。
        Check(ScoreLibrary.HotkeyId(0) == 100 && ScoreLibrary.HotkeyId(ScoreLibrary.MaxEntries - 1) == 159, "曲谱快捷键编号从100起");
        Check(ScoreLibrary.IsLibraryHotkeyId(100) && ScoreLibrary.IsLibraryHotkeyId(159), "曲谱快捷键编号范围可用");
        Check(!ScoreLibrary.IsLibraryHotkeyId(0) && !ScoreLibrary.IsLibraryHotkeyId(1) && !ScoreLibrary.IsLibraryHotkeyId(2) &&
            !ScoreLibrary.IsLibraryHotkeyId(99) && !ScoreLibrary.IsLibraryHotkeyId(160), "曲谱编号不与开始停止键冲突");
        Check(ScoreLibrary.IndexFromHotkeyId(103) == 3 && ScoreLibrary.IndexFromHotkeyId(ScoreLibrary.HotkeyId(7)) == 7, "编号反查列表下标");

        // 显示名：曲名优先，其次文件名；空条目也不能抛异常。
        Check(new ScoreLibraryEntry { Path = first }.DisplayName == "小星星", "无曲名时取文件名");
        Check(new ScoreLibraryEntry { Path = first, Title = "小星星变奏" }.DisplayName == "小星星变奏", "自定义曲名优先于文件名");
        Check(new ScoreLibraryEntry().DisplayName == "未命名曲谱" && new ScoreLibraryEntry { Title = " " }.DisplayName == "未命名曲谱", "空条目显示名安全兜底");
        Check(ScoreLibrary.IndexOfPath(new[] { new ScoreLibraryEntry { Path = first } }, first.ToUpperInvariant()) == 0 &&
            ScoreLibrary.IndexOfPath(new[] { new ScoreLibraryEntry { Path = first } }, second) == -1 &&
            ScoreLibrary.IndexOfPath(new[] { new ScoreLibraryEntry { Path = first } }, " ") == -1, "按路径查重忽略大小写与空值");

        var one = new ScoreLibraryEntry { Path = first, Title = "小星星", Hotkey = f5 };
        ScoreLibrary.Validate(new[] { one }, start, stop);
        Check(true, "合法曲谱库通过校验");
        Reject(() => ScoreLibrary.Validate(new[] { one, one with { Path = second } }, start, stop), "拒绝两首曲谱共用同一个快捷键");
        Reject(() => ScoreLibrary.Validate(new[] { one with { Hotkey = stop } }, start, stop), "拒绝曲谱快捷键与停止键相同");
        Reject(() => ScoreLibrary.Validate(new[] { one with { Hotkey = start } }, start, stop), "拒绝曲谱快捷键与开始键相同");
        Reject(() => ScoreLibrary.Validate(new[] { one with { Hotkey = new HotkeyBinding(0x5A) } }, start, stop), "拒绝无效曲谱快捷键");
        Reject(() => ScoreLibrary.Validate(new[] { new ScoreLibraryEntry { Path = "song.txt" } }, start, stop), "拒绝相对路径");
        Reject(() => ScoreLibrary.Validate(new[] { new ScoreLibraryEntry { Path = first, Title = new string('长', ScoreLibraryEntry.MaxTitleLength + 1) } }, start, stop), "拒绝超长曲名");
        Reject(() => ScoreLibrary.Validate(new[] { new ScoreLibraryEntry { Path = first + "\u0001" } }, start, stop), "拒绝含控制字符的路径");
        Reject(() => ScoreLibrary.Validate(new ScoreLibraryEntry[] { null! }, start, stop), "拒绝空条目而不是崩溃");
        ScoreLibrary.Validate(new[] { new ScoreLibraryEntry { Path = first, Title = null! } }, start, stop);
        Check(true, "null曲名按空名处理，不算错误");
        ScoreLibrary.Validate(Enumerable.Range(0, ScoreLibrary.MaxEntries).Select(_ => new ScoreLibraryEntry { Path = first }).ToArray(), start, stop);
        Check(true, "曲谱库上限刚好合法");
        Reject(() => ScoreLibrary.Validate(Enumerable.Range(0, ScoreLibrary.MaxEntries + 1).Select(_ => new ScoreLibraryEntry { Path = first }).ToArray(), start, stop), "拒绝超过上限的曲谱数量");

        // 冲突检查：绑定前就挡下重复与无效值，并给出可读原因。
        var entries = new[]
        {
            new ScoreLibraryEntry { Path = first, Title = "小星星", Hotkey = f5 },
            new ScoreLibraryEntry { Path = second, Title = "欢乐颂", Hotkey = ctrlF9 }
        };
        Check(ScoreLibrary.Conflict(entries, 2, new HotkeyBinding(0x79), start, stop) == null, "未占用的候选快捷键可用");
        Check(ScoreLibrary.Conflict(entries, 0, f5, start, stop) == null, "与自身相同的快捷键不算冲突");
        Check(ScoreLibrary.Conflict(entries, 1, f5, start, stop) is string self && self.Contains("小星星"), "重复快捷键提示占用的曲谱名");
        Check(ScoreLibrary.Conflict(entries, 0, start, start, stop) is string begin && begin.Contains("开始键"), "与开始键冲突时提示开始键");
        Check(ScoreLibrary.Conflict(entries, 0, stop, start, stop) is string end && end.Contains("停止键"), "与停止键冲突时提示停止键");
        Check(ScoreLibrary.Conflict(entries, 0, new HotkeyBinding(0x41), start, stop) is string invalid && invalid.Length > 0, "无效候选快捷键返回原因");
        Check(!ScoreLibrary.HotkeyTaken(entries, new HotkeyBinding(0x79), start, stop) &&
            ScoreLibrary.HotkeyTaken(entries, f5, start, stop) && ScoreLibrary.HotkeyTaken(entries, start, start, stop), "占用查询与冲突判断一致");

        // 坏数据清理：只丢无效条目、只清冲突快捷键，其余保持原顺序。
        var messy = new[]
        {
            new ScoreLibraryEntry { Path = first, Title = "小星星", Hotkey = f5 },
            new ScoreLibraryEntry { Path = "relative.txt", Hotkey = new HotkeyBinding(0x79) },
            new ScoreLibraryEntry { Path = second, Title = "欢乐颂", Hotkey = new HotkeyBinding(0x5A) },
            new ScoreLibraryEntry { Path = third, Title = "送别", Hotkey = stop },
            new ScoreLibraryEntry { Path = fourth, Title = "生日歌", Hotkey = f5 },
            new ScoreLibraryEntry { Path = first + "-2.txt", Title = "小步舞曲" },
            new ScoreLibraryEntry { Path = first, Title = new string('长', ScoreLibraryEntry.MaxTitleLength + 1) },
            null!
        };
        var cleaned = ScoreLibrary.Sanitize(messy, start, stop, out string? warning);
        Check(cleaned.Length == 5 && cleaned[0].Title == "小星星" && cleaned[1].Title == "欢乐颂" &&
            cleaned[2].Title == "送别" && cleaned[3].Title == "生日歌" && cleaned[4].Title == "小步舞曲", "清理后只保留可修复条目并保持顺序");
        Check(cleaned[0].Hotkey == f5 && cleaned[1].Hotkey == null && cleaned[2].Hotkey == null &&
            cleaned[3].Hotkey == null && cleaned[4].Hotkey == null, "无效或冲突的快捷键被清除");
        Check(warning != null && warning.Contains("忽略3条") && warning.Contains("清除3个"), "清理提示包含丢弃与清除数量");
        var clean = ScoreLibrary.Sanitize(cleaned, start, stop, out string? noWarning);
        Check(noWarning == null && clean.SequenceEqual(cleaned), "干净的曲谱库不再提示");
        Check(ScoreLibrary.Sanitize(null, start, stop, out string? nullWarning).Length == 0 && nullWarning == null, "空曲谱库安全返回");
        Check(ScoreLibrary.Sanitize(Array.Empty<ScoreLibraryEntry>(), start, stop, out _).Length == 0, "零条曲谱库安全返回");
        var overflow = ScoreLibrary.Sanitize(Enumerable.Range(0, ScoreLibrary.MaxEntries + 5)
            .Select(_ => new ScoreLibraryEntry { Path = first }).ToArray(), start, stop, out string? overflowWarning);
        Check(overflow.Length == ScoreLibrary.MaxEntries && overflowWarning != null && overflowWarning.Contains("忽略5条"), "超量条目被截断并提示");
        var nullTitle = ScoreLibrary.Sanitize(new[] { new ScoreLibraryEntry { Path = first, Title = null! } }, start, stop, out string? titleWarning);
        Check(nullTitle.Length == 1 && nullTitle[0].Title == "" && titleWarning == null, "null曲名被规范成空字符串");

        // 注册层：编号、注册顺序、单首失败不牵连其他曲谱。
        var backend = new RecordingHotkeys();
        var manager = new HotkeyController(backend);
        var library = new[] { (ScoreLibrary.HotkeyId(0), f5), (ScoreLibrary.HotkeyId(1), ctrlF9) };
        manager.Apply(start, stop, library);
        Check(manager.StartReady && manager.StopReady && manager.LibraryCount == 2 && manager.LibraryReady(100) && manager.LibraryReady(101), "曲谱快捷键随开始停止键一起注册");
        Check(backend.Attempts.SequenceEqual(new[] { 2, 100, 101, 1 }), "注册顺序为停止键→曲谱库→开始键");
        Check(manager.LibraryResult(102) == "未注册", "未绑定的曲谱不占用编号");
        backend.Occupied.Add(f5);
        manager.Apply(start, stop, library);
        Check(!manager.LibraryReady(100) && manager.LibraryResult(100) == "快捷键被其他程序占用" &&
            manager.LibraryReady(101) && manager.StartReady && manager.StopReady, "单首曲谱快捷键被占用不影响其他曲谱");
        backend.Occupied.Clear();
        manager.Suspend();
        Check(backend.Registered.Count == 0 && manager.LibraryCount == 0 && !manager.LibraryReady(100), "暂停时一并释放曲谱快捷键");
        manager.Apply(start, stop, library);
        Check(manager.LibraryReady(100) && manager.LibraryReady(101) && backend.Registered.Count == 4, "暂停后重新注册成功");
        Reject(() => manager.Apply(start, stop, new[] { (99, f5) }), "拒绝非法曲谱快捷键编号");
        Reject(() => manager.Apply(start, stop, new[] { (ScoreLibrary.HotkeyId(0), stop) }), "拒绝与停止键重复的曲谱快捷键");
        Reject(() => manager.Apply(start, stop, new[] { (ScoreLibrary.HotkeyId(0), new HotkeyBinding(0x5A)) }), "拒绝无效曲谱快捷键");
        Reject(() => manager.Apply(start, stop, new[] { (ScoreLibrary.HotkeyId(0), f5), (ScoreLibrary.HotkeyId(1), f5) }), "拒绝两首曲谱重复快捷键");
        Check(manager.LibraryReady(100) && manager.LibraryReady(101) && manager.StartReady, "非法配置不破坏已有注册");

        // 设置文件：曲谱库正常往返，坏数据只清理曲谱库本身。
        string settingsFolder = Path.Combine(Path.GetTempPath(), "HarmonicaLibrarySettings-" + Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(settingsFolder, "settings.json");
        Directory.CreateDirectory(settingsFolder);
        try
        {
            var custom = new AppSettings
            {
                Bpm = 95, Gap = 15, LibraryHotkeyPlays = false,
                Library = new[]
                {
                    new ScoreLibraryEntry { Path = first, Title = "小星星", Hotkey = f5 },
                    new ScoreLibraryEntry { Path = second, Hotkey = ctrlF9 }
                }
            };
            SettingsStore.Save(settingsPath, custom);
            var loaded = SettingsStore.Load(settingsPath, out string? note);
            Check(note == null && loaded.Bpm == custom.Bpm && loaded.Gap == custom.Gap && !loaded.LibraryHotkeyPlays &&
                loaded.Library.SequenceEqual(custom.Library), "曲谱库连同曲名、快捷键与演奏开关往返保存");
            File.WriteAllText(settingsPath, Json(new { Bpm = 95, Gap = 15, Library = new object?[] { null, new { Path = "relative.txt" } } }));
            var repaired = SettingsStore.Load(settingsPath, out note);
            Check(note != null && repaired.Bpm == 95 && repaired.Gap == 15 && repaired.Library.Length == 0, "空条目与相对路径只清理曲谱库，其他设置保留");
            Check(note is string repairNote && repairNote.Contains("曲谱库") && repairNote.Contains("其他设置未受影响"), "清理提示说明只影响曲谱库");
            File.WriteAllText(settingsPath, Json(new
            {
                Bpm = 95,
                Library = new object?[]
                {
                    new { Path = first, Title = (string?)null, Hotkey = new { Key = 0x77 } },
                    new { Path = second, Hotkey = new { Key = 0x74 } },
                    new { Path = third, Hotkey = new { Key = 0x74 } }
                }
            }));
            var fixedUp = SettingsStore.Load(settingsPath, out note);
            Check(note != null && fixedUp.Bpm == 95 && fixedUp.Library.Length == 3, "null曲名与冲突快捷键被清理，其余设置保留");
            Check(fixedUp.Library[0].Title == "" && fixedUp.Library[0].Hotkey == null &&
                fixedUp.Library[1].Hotkey == f5 && fixedUp.Library[2].Hotkey == null, "与停止键冲突和重复的快捷键被清除");
            FixedUpSaves(fixedUp, settingsFolder);
            Check(true, "清理后的设置可以再次保存");
            File.WriteAllText(settingsPath, Json(new
            {
                Bpm = 110,
                Library = new object?[] { new { Path = first, Title = "小星星", Hotkey = new { Key = 0x74 } } }
            }));
            var good = SettingsStore.Load(settingsPath, out note);
            Check(note == null && good.Bpm == 110 && good.Library.Length == 1 && good.Library[0].Hotkey == f5, "合法曲谱库不再触发清理提示");
            Reject(() => SettingsStore.Save(settingsPath, custom with
            {
                Library = new[] { new ScoreLibraryEntry { Path = first, Hotkey = HotkeyBinding.DefaultStart } }
            }), "保存时拒绝与开始键相同的曲谱快捷键");
            Reject(() => SettingsStore.Save(settingsPath, custom with
            {
                Library = Enumerable.Range(0, ScoreLibrary.MaxEntries + 1).Select(_ => new ScoreLibraryEntry { Path = first }).ToArray()
            }), "保存时拒绝超过上限的曲谱库");
            Check(SettingsStore.Load(settingsPath, out note).Bpm == 110, "被拒绝的写入不修改已有设置");
        }
        finally { if (Directory.Exists(settingsFolder)) Directory.Delete(settingsFolder, true); }

        Console.WriteLine($"PASS score library: {passed} tests (编号、校验、冲突、清理、注册顺序、设置往返)");
    }

    // 清理后的设置必须仍是合法输入，否则下一次保存会把用户的曲谱库整个丢掉。
    private static void FixedUpSaves(AppSettings settings, string folder)
    {
        string path = Path.Combine(folder, "repaired.json");
        SettingsStore.Save(path, settings);
        var reloaded = SettingsStore.Load(path, out string? warning);
        File.Delete(path);
        // 记录里的数组用引用比较，这里逐字段比较，避免误判。
        if (warning != null || reloaded.Bpm != settings.Bpm || reloaded.Gap != settings.Gap ||
            reloaded.LibraryHotkeyPlays != settings.LibraryHotkeyPlays || !reloaded.Library.SequenceEqual(settings.Library))
            throw new Exception("Score library: 清理后的设置无法再次保存读取");
    }
}

// 记录注册顺序，用于验证“停止键 → 曲谱库 → 开始键”的注册次序。
sealed class RecordingHotkeys : IHotkeyBackend
{
    public List<int> Attempts { get; } = new();
    public Dictionary<int, HotkeyBinding> Registered { get; } = new();
    public HashSet<HotkeyBinding> Occupied { get; } = new();
    public int Register(int id, HotkeyBinding binding)
    {
        Attempts.Add(id);
        if (Occupied.Contains(binding)) return 1409;
        Registered[id] = binding;
        return 0;
    }
    public void Unregister(int id) => Registered.Remove(id);
}
