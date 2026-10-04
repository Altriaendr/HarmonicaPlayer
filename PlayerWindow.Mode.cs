using System.Security.Principal;
using System.Windows.Controls;
using System.Windows.Media;

namespace HarmonicaPlayer;

public sealed partial class PlayerWindow
{
    private readonly bool administrator;
    private readonly TextBlock modeStatus = new() { TextWrapping = System.Windows.TextWrapping.Wrap };
    private static bool DetectAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    private string? PermissionHint => !administrator && dry.IsChecked != true && !listening
        ? "权限提醒：当前未以管理员身份运行；若游戏权限更高，可能无法操作游戏。可继续演奏，必要时以管理员身份运行播放器。"
        : null;
    private string? TestModeHint => dry.IsChecked == true
        ? "当前为仅测试：不会操作游戏，也不会发声；需要声音请使用本地试听。"
        : null;
    private void UpdateMode()
    {
        start.Content = (dry.IsChecked == true ? "开始测试 " : "开始演奏 ") + settings.Start.Label;
        modeStatus.Text = $"权限：{(administrator ? "已以管理员身份运行" : "未以管理员身份运行")}　|　当前模式：{(dry.IsChecked == true ? "仅测试" : "游戏演奏")}";
        modeStatus.Foreground = administrator || dry.IsChecked == true ? Brushes.DarkSlateGray : Brushes.DarkGoldenrod;
    }
}

