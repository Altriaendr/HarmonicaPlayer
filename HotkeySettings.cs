namespace HarmonicaPlayer;

// Values deliberately match Win32 MOD_ALT / MOD_CONTROL / MOD_SHIFT.
public record HotkeyBinding(uint Key, uint Modifiers = 0)
{
    public static HotkeyBinding DefaultStart => new(0x75);
    public static HotkeyBinding DefaultStop => new(0x77);
    public string Label => ((Modifiers & 2) != 0 ? "Ctrl + " : "") +
        ((Modifiers & 1) != 0 ? "Alt + " : "") +
        ((Modifiers & 4) != 0 ? "Shift + " : "") + KeyName(Key);
    public static string KeyName(uint key) => key switch
    {
        >= 0x70 and <= 0x7A => $"F{key - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        0x1B => "Esc", 0x20 => "Space", 0x24 => "Home", 0x23 => "End",
        0x21 => "PageUp", 0x22 => "PageDown", 0x2D => "Insert", 0x2E => "Delete",
        _ => $"VK {key:X2}"
    };
    public string? Error()
    {
        if ((Modifiers & ~7u) != 0) return Loc.T("仅支持 Ctrl、Alt、Shift，不支持 Windows 键。");
        if (Key == 0x7B) return Loc.T("F12为系统调试保留键，请选择其他键。");
        if (Key is 0x5A or 0x58 or 0x43 or 0x56 or 0x42 or 0x4E or 0x4D or 0xBC)
            return Loc.T("Z/X/C/V/B/N/M/逗号用于演奏，请选择其他快捷键（包括组合键）。");
        bool function = Key is >= 0x70 and <= 0x7A;
        bool text = Key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A;
        bool navigation = Key is 0x1B or 0x20 or 0x24 or 0x23 or 0x21 or 0x22 or 0x2D or 0x2E;
        if (!function && !text && !navigation) return Loc.T("请选择 F1～F11、字母、数字或导航键。");
        if ((text || Key == 0x20) && Modifiers == 0)
            return Loc.T("字母、数字和空格须搭配 Ctrl、Alt 或 Shift，避免影响日常输入。");
        if (Key == 0x2E && (Modifiers & 3) == 3) return Loc.T("Ctrl+Alt+Delete为系统保留组合。");
        if (Key == 0x73 && (Modifiers & 1) != 0) return Loc.T("Alt+F4用于关闭窗口，请选择其他组合。");
        if (Key == 0x1B && (Modifiers & 3) != 0) return Loc.T("带 Ctrl 或 Alt 的 Esc 为系统快捷键，请选择其他组合。");
        if (Key == 0x20 && (Modifiers & 1) != 0) return Loc.T("Alt+Space用于系统菜单，请选择其他组合。");
        return null;
    }
    public static void ValidatePair(HotkeyBinding start, HotkeyBinding stop)
    {
        if (start.Error() is string a) throw new FormatException(Loc.T("开始键：") + a);
        if (stop.Error() is string b) throw new FormatException(Loc.T("停止键：") + b);
        if (start == stop) throw new FormatException(Loc.T("开始键和停止键不能相同。"));
    }
}

public interface IHotkeyBackend
{
    int Register(int id, HotkeyBinding binding);
    void Unregister(int id);
}

// Called on the window thread; track each registration independently.
public sealed class HotkeyController(IHotkeyBackend backend)
{
    // 曲谱库快捷键：编号 → 注册错误码（0 表示可用）。开始/停止仍固定为 1、2。
    private readonly Dictionary<int, int> libraryErrors = new();
    public bool StartReady { get; private set; }
    public bool StopReady { get; private set; }
    public int StartError { get; private set; }
    public int StopError { get; private set; }
    public int LibraryCount => libraryErrors.Count;
    public bool LibraryReady(int id) => libraryErrors.TryGetValue(id, out int error) && error == 0;
    public string LibraryResult(int id) => !libraryErrors.TryGetValue(id, out int error) ? Loc.T("未注册") :
        error == 0 ? Loc.T("快捷键可用") : error == 1409 ? Loc.T("快捷键被其他程序占用") : Loc.F("注册失败（错误码{0}）", error);
    public void Apply(HotkeyBinding start, HotkeyBinding stop) => Apply(start, stop, Array.Empty<(int, HotkeyBinding)>());
    // 注册顺序：停止键 → 曲谱库 → 开始键。停止键必须先占用成功，避免新增功能削弱紧急停止。
    public void Apply(HotkeyBinding start, HotkeyBinding stop, IReadOnlyList<(int Id, HotkeyBinding Binding)> library)
    {
        HotkeyBinding.ValidatePair(start, stop); // Never discard valid registrations for invalid input.
        // 重复注册会静默失效或覆盖，注册前必须拒绝；此检查在 Suspend 之前，失败不会破坏当前注册。
        var planned = new HashSet<HotkeyBinding> { start, stop };
        foreach (var (id, binding) in library)
        {
            if (!ScoreLibrary.IsLibraryHotkeyId(id)) throw new FormatException(Loc.T("曲谱库快捷键编号无效。"));
            if (binding.Error() is string error) throw new FormatException(Loc.F("曲谱库快捷键 {0} 无效：{1}", binding.Label, error));
            if (!planned.Add(binding)) throw new FormatException(Loc.F("快捷键 {0} 重复，无法同时注册。", binding.Label));
        }
        Suspend();
        StopError = backend.Register(2, stop); StopReady = StopError == 0;
        foreach (var (id, binding) in library)
        {
            int error = backend.Register(id, binding);
            libraryErrors[id] = error;
        }
        StartError = backend.Register(1, start); StartReady = StartError == 0;
    }
    public void Suspend()
    {
        // 曲谱库快捷键一并释放，否则下次 Apply 会因“编号已注册”而失败。
        foreach (int id in libraryErrors.Keys.ToArray()) backend.Unregister(id);
        libraryErrors.Clear();
        if (StartReady) backend.Unregister(1);
        if (StopReady) backend.Unregister(2);
        StartReady = StopReady = false;
    }
    public string Describe(HotkeyBinding start, HotkeyBinding stop) =>
        Loc.F("开始 {0}：{1}；停止 {2}：{3}。", start.Label, Result(StartReady, StartError), stop.Label, Result(StopReady, StopError)) +
        Loc.T(!StopReady ? "停止键不可用，禁止真实演奏；请修改快捷键或解除占用后点“重试注册”。" :
         !StartReady ? "可点击“开始”按钮演奏；也可修改开始键或重试注册。" : "快捷键已就绪。");
    private static string Result(bool ok, int error) => ok ? Loc.T("可用") :
        error == 1409 ? Loc.T("被占用") : Loc.F("注册失败（错误码{0}，可能被其他程序占用）", error);
}
