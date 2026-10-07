using Microsoft.Win32;

namespace HarmonicaPlayer;

// 窗口可以在主题变化时刷新自己的自绘样式；由 UiTheme 在重刷时回调。
public interface IThemeAware
{
    void OnThemeChanged();
}

// 语义化调色板：界面所有颜色都只能来自这里的 token，控件代码里禁止出现颜色字面量。
// 浅色与深色使用完全相同的字段，因此切换主题只是换一个 UiPalette 实例。
public sealed class UiPalette
{
    public required bool IsDark { get; init; }
    public required string Page { get; init; }              // 窗口背景
    public required string Panel { get; init; }             // 面板 / 卡片 / 按钮底
    public required string PanelAlt { get; init; }          // 次级面板：控制台、禁用底、进度槽
    public required string Hover { get; init; }             // 悬停底色
    public required string Pressed { get; init; }           // 按下底色
    public required string Border { get; init; }            // 卡片边框 / 分隔线
    public required string ControlBorder { get; init; }     // 输入框、按钮边框
    public required string TextPrimary { get; init; }
    public required string TextSecondary { get; init; }
    public required string TextMuted { get; init; }         // 提示、说明文字
    public required string TextDisabled { get; init; }
    public required string Accent { get; init; }
    public required string AccentHover { get; init; }
    public required string AccentPressed { get; init; }
    public required string AccentSoft { get; init; }        // 次级按钮底 / 选中行底
    public required string AccentSoftHover { get; init; }
    public required string AccentSoftPressed { get; init; }
    public required string OnAccent { get; init; }          // 主色底上的文字
    public required string Danger { get; init; }
    public required string DangerHover { get; init; }
    public required string DangerPressed { get; init; }
    public required string DangerSoft { get; init; }
    public required string DangerTint { get; init; }
    public required string DangerBorder { get; init; }
    public required string Warning { get; init; }
    public required string WarningSoft { get; init; }       // 警告条底
    public required string Success { get; init; }
    public required string Focus { get; init; }             // 键盘焦点环
    public required string TrackFill { get; init; }         // 进度条 / 滑杆槽
    public required string TrackBorder { get; init; }
    public required string Selection { get; init; }         // 文本选中
    public required double ShadowOpacity { get; init; }     // 只给浮层使用：0.05 / 0.3
}

public static class UiPalettes
{
    // 浅色：背景 #F6F7F9、面板 #FFFFFF、边框 #E2E5E9、主文字 #111827、主色 #2563EB。
    public static readonly UiPalette Light = new()
    {
        IsDark = false,
        Page = "#FFF6F7F9",
        Panel = "#FFFFFFFF",
        PanelAlt = "#FFF1F3F5",
        Hover = "#FFEDF0F4",
        Pressed = "#FFE4E8EE",
        Border = "#FFE2E5E9",
        ControlBorder = "#FFE2E5E9",
        TextPrimary = "#FF111827",
        TextSecondary = "#FF6B7280",
        TextMuted = "#FF8A93A4",
        TextDisabled = "#FF9CA3AF",
        Accent = "#FF2563EB",
        AccentHover = "#FF1D4ED8",
        AccentPressed = "#FF1E40AF",
        AccentSoft = "#FFEFF4FE",
        AccentSoftHover = "#FFE3ECFD",
        AccentSoftPressed = "#FFD6E2FB",
        OnAccent = "#FFFFFFFF",
        Danger = "#FFDC2626",
        DangerHover = "#FFB91C1C",
        DangerPressed = "#FF991B1B",
        DangerSoft = "#FFFDF3F2",
        DangerTint = "#FFF9E4E2",
        DangerBorder = "#FFF2C7C3",
        Warning = "#FFD97706",
        WarningSoft = "#FFFFF7E6",
        Success = "#FF16A34A",
        Focus = "#FF93C5FD",
        TrackFill = "#FFE7EAEE",
        TrackBorder = "#FFE2E5E9",
        Selection = "#FFBFD3FA",
        ShadowOpacity = 0.05
    };

    // 深色：背景 #0F1115、面板 #171A21、边框 #2A2F3A、主文字 #E5E7EB、主色 #3B82F6。
    public static readonly UiPalette Dark = new()
    {
        IsDark = true,
        Page = "#FF0F1115",
        Panel = "#FF171A21",
        PanelAlt = "#FF1E222B",
        Hover = "#FF232833",
        Pressed = "#FF2B313D",
        Border = "#FF2A2F3A",
        ControlBorder = "#FF2A2F3A",
        TextPrimary = "#FFE5E7EB",
        TextSecondary = "#FF9CA3AF",
        TextMuted = "#FF9CA3AF",
        TextDisabled = "#FF6B7280",
        Accent = "#FF3B82F6",
        AccentHover = "#FF60A5FA",
        AccentPressed = "#FF2563EB",
        AccentSoft = "#FF1C2739",
        AccentSoftHover = "#FF23324B",
        AccentSoftPressed = "#FF2B3D5C",
        OnAccent = "#FFFFFFFF",
        Danger = "#FFEF4444",
        DangerHover = "#FFF87171",
        DangerPressed = "#FFDC2626",
        DangerSoft = "#FF2A1618",
        DangerTint = "#FF3A1D20",
        DangerBorder = "#FF5C2A2E",
        Warning = "#FFF59E0B",
        WarningSoft = "#FF2A2013",
        Success = "#FF22C55E",
        // 深色焦点环用亮一档的主色：提示词给的 #1D4ED8 在深色面板上几乎看不见。
        Focus = "#FF60A5FA",
        TrackFill = "#FF232833",
        TrackBorder = "#FF2A2F3A",
        Selection = "#FF2F4A7A",
        ShadowOpacity = 0.30
    };
}

// 读取并监听 Windows 的“应用主题”（设置 → 个性化 → 颜色 → 选择默认应用模式）。
// 只用框架自带的 Microsoft.Win32.Registry 与 SystemEvents，不引入任何第三方依赖。
internal static class SystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly object gate = new();
    private static bool dark = Read();
    private static bool watching;

    internal static bool IsDark
    {
        get { lock (gate) return dark; }
    }

    // 只订阅一次；回调在 SystemEvents 的线程上触发，切换界面线程由调用方负责。
    internal static void StartWatching(Action onChanged)
    {
        lock (gate)
        {
            if (watching) return;
            watching = true;
        }
        // 极少数受限环境下订阅会失败，外观问题绝不影响程序启动。
        try { SystemEvents.UserPreferenceChanged += (_, _) => Refresh(onChanged); }
        catch (Exception) { }
    }

    private static void Refresh(Action onChanged)
    {
        bool now = Read();
        lock (gate)
        {
            if (now == dark) return;
            dark = now;
        }
        onChanged();
    }

    // 注册表缺失或读取失败时按浅色处理（与 Windows 默认一致），不抛异常。
    private static bool Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception) { return false; }
    }
}
