using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HarmonicaPlayer;

// 独立实现的 Windows 输入封装；不注入游戏进程，不使用驱动。
public sealed class NativeInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public Union Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct Union
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Keyboard
    {
        public ushort Vk, Scan;
        public uint Flags, Time;
        public UIntPtr Extra;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Mouse
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public UIntPtr Extra;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    private readonly HashSet<ushort> keys = new();
    private readonly HashSet<int> buttons = new();
    private static readonly uint[] DownFlags = { 0x0002, 0x0008, 0x0020 };
    private static readonly uint[] UpFlags = { 0x0004, 0x0010, 0x0040 };

    private static void Send(Input input)
    {
        if (SendInput(1, new[] { input }, Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "输入发送失败。请确认程序与游戏权限一致（需要时以管理员运行），切换英文输入法及无边框窗口后重试；仍失败则可能受到输入限制。");
    }
    private static void Key(ushort scan, bool down) => Send(new Input
    {
        Type = 1, Data = new Union { Keyboard = new Keyboard { Scan = scan, Flags = 0x0008u | (down ? 0u : 0x0002u) } }
    });
    private static void Button(int button, bool down) => Send(new Input
    {
        Type = 0, Data = new Union { Mouse = new Mouse { Flags = down ? DownFlags[button] : UpFlags[button] } }
    });
    public void Modifiers(ScoreNote note)
    {
        var plan = GameNoteInput.Create(note);
        // Octave and semitone modifiers are independent; no release between them.
        if (plan.Low) PressButton(0);
        if (plan.High) PressButton(1);
        if (plan.Sharp) PressButton(2);
    }
    private void PressButton(int button) { buttons.Add(button); Button(button, true); }
    public void NoteOn(ScoreNote note)
    {
        ushort scan = GameNoteInput.Create(note).Scan;
        keys.Add(scan);
        Key(scan, true);
    }
    // 仅释放程序尝试按下的键，不干扰其他手动按键；失败保留记录供再次尝试。
    public string? Release()
    {
        var errors = new List<string>();
        foreach (var key in keys.ToArray())
            try { Key(key, false); keys.Remove(key); } catch (Exception e) { errors.Add(e.Message); }
        foreach (var button in buttons.ToArray())
            try { Button(button, false); buttons.Remove(button); } catch (Exception e) { errors.Add(e.Message); }
        return errors.Count == 0 ? null : string.Join("; ", errors);
    }
}
