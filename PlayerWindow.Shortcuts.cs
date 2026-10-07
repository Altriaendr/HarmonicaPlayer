using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HarmonicaPlayer;

// 操作逻辑增强：窗口内快捷键、文件拖放、数值字段即时校验、演奏进度、窗口位置记忆。
// 全部复用已有方法（Import / SaveScoreAsync / ListenAsync / Begin / Stop 等），不新增演奏路径。
public sealed partial class PlayerWindow
{
    private readonly ProgressBar playbackProgress = new() { Minimum = 0, Maximum = 100 };
    private readonly TextBlock playbackPosition = new() { Text = "尚未开始演奏。", TextWrapping = TextWrapping.Wrap };

    private void InstallInteraction()
    {
        InstallShortcuts();
        InstallFileDrop();
        InstallScoreFontZoom();
    }

    // ---------- 窗口内快捷键（不是系统热键，只在窗口获得焦点时生效） ----------
    private void InstallShortcuts() => PreviewKeyDown += OnPreviewKeyDown;

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        try { await HandleShortcutAsync(e); }
        catch (Exception error) { ReportIssue(Loc.T("快捷键操作失败：") + error.Message); }
    }

    private async Task HandleShortcutAsync(KeyEventArgs e)
    {
        if (e.Handled || closing || editingHotkeys) return;
        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool plain = Keyboard.Modifiers == ModifierKeys.None;
        switch (e.Key)
        {
            case Key.F5 when plain:
                e.Handled = true; Preview(); break;
            case Key.Escape when plain && cancellation != null:
                e.Handled = true; Stop(); break;
            case Key.O when control && !shift:
                e.Handled = true; await RunShortcut(Import); break;
            case Key.M when control && !shift:
                e.Handled = true; await RunShortcut(ImportMidiAsync); break;
            case Key.N when control && !shift:
                e.Handled = true; await RunShortcut(NewScoreAsync); break;
            case Key.S when control && !shift:
                e.Handled = true; await RunShortcut(() => SaveViaShortcut(false)); break;
            case Key.S when control && shift:
                e.Handled = true; await RunShortcut(() => SaveViaShortcut(true)); break;
            case Key.E when control && !shift:
                e.Handled = true; await RunShortcut(ExportMidiAsync); break;
            case Key.L when control && !shift:
                e.Handled = true; await RunShortcut(() => ListenAsync(false)); break;
            case Key.L when control && shift:
                e.Handled = true; await RunShortcut(() => ListenAsync(true)); break;
            case Key.K when control && !shift:
                e.Handled = true; ConfigureHotkeys(); break;
            case Key.Return or Key.Enter when control:
                e.Handled = true; await RunShortcut(() => Begin()); break;
            case Key.D0 or Key.NumPad0 when control:
                e.Handled = true; ResetScoreFont(); break;
        }
    }

    // 快捷键与按钮共用同一套入口，忙碌时先提示而不是静默忽略。
    private async Task RunShortcut(Func<Task> action)
    {
        if (uiBusy || documentBusy || cancellation != null || beginning)
        {
            status.Text = Loc.T("请稍候：当前正在演奏或读写曲谱，快捷键稍后可用。");
            return;
        }
        await action();
    }

    private Task SaveViaShortcut(bool forceSaveAs) => DocumentOperation(() => SaveScoreAsync(forceSaveAs));

    // Ctrl+N：与“新建”按钮完全一致，先询问是否保存，再清空为新文档。
    private Task NewScoreAsync() => DocumentOperation(async () =>
    {
        if (!await ConfirmUnsavedAsync()) return false;
        ApplyDocument(new ScoreDocument(Loc.T("未命名曲谱"), 120, "")); return true;
    });

    // ---------- 拖放导入：把 .txt / .mid 拖进窗口即可，等同于对应的导入按钮 ----------
    private void InstallFileDrop()
    {
        AllowDrop = true;
        DragOver += (_, e) =>
        {
            e.Effects = DropPath(e) == null ? DragDropEffects.None : DragDropEffects.Copy;
            e.Handled = true;
        };
        Drop += async (_, e) =>
        {
            e.Handled = true;
            if (DropPath(e) is not string path) return;
            if (uiBusy || documentBusy || cancellation != null || beginning)
            {
                status.Text = Loc.T("请稍候：当前正在演奏或读写曲谱，稍后再拖入文件。");
                return;
            }
            if (IsMidi(path)) await ImportMidiDropped(path);
            else await ImportScoreDropped(path);
            Activate();
        };
    }

    private static string? DropPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return null;
        return SupportedImport(files[0]) ? files[0] : null;
    }

    private static bool SupportedImport(string path) => IsMidi(path) ||
        string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase);

    private static bool IsMidi(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".midi", StringComparison.OrdinalIgnoreCase);
    }

    private Task ImportScoreDropped(string path) => DocumentOperation(() => ImportFromPathAsync(path));
    private Task ImportMidiDropped(string path) => DocumentOperation(() => ImportMidiFromPathAsync(path));

    // 导入 TXT：与“导入 TXT”按钮完全相同的流程，只是路径来自拖放。
    private async Task<bool> ImportFromPathAsync(string path)
    {
        int fallback = int.TryParse(bpm.Text, out int value) && value is >= 20 and <= 300 ? value : settings.Bpm;
        var result = await ScoreDocumentReader.ReadAsync(path, fallback);
        if (!await ConfirmUnsavedAsync()) return false;
        // Saving the current document may have updated the very file selected for import.
        result = await ScoreDocumentReader.ReadAsync(path, fallback);
        ApplyDocument(result.Document);
        documentWarning.Text = string.Join("\n", result.Warnings);
        status.Text = scoreIssue == null ? Loc.T("已导入，检查通过；请确认速度（BPM）。") : Loc.T("已导入，请修正顶部提示的错误。");
        return true;
    }

    // 导入单旋律 MIDI：与“导入单旋律 MIDI”按钮相同的流程，只是路径来自拖放。
    private async Task<bool> ImportMidiFromPathAsync(string path)
    {
        // MIDI decoding is bounded but may still be expensive; keep the WPF dispatcher free.
        var file = await Task.Run(() => MidiImporter.ReadAsync(path));
        if (scoreDialogs.ConvertMidi(this, file) is not ScoreDocument document) return false;
        if (!await ConfirmUnsavedAsync()) return false;
        ApplyDocument(document);
        // This is a new, unsaved TXT document, not the source .mid file.
        cleanDocument = ("", "", "", ""); UpdateDocumentTitle();
        documentWarning.Text = Loc.F("由 MIDI“{0}”转换，原 MIDI 未修改。请先试听检查，再保存 TXT 或游戏演奏。", Path.GetFileName(path));
        status.Text = Loc.T("单旋律 MIDI 已导入为可编辑谱面，尚未保存 TXT。");
        return true;
    }

    // ---------- 数值字段即时校验：不必等到点“开始”才看到非法速度/间隔 ----------
    private void MarkTimingFields()
    {
        if (closing) return;
        MarkField(bpm, int.TryParse(bpm.Text, out int bpmValue) && bpmValue is >= 20 and <= 300,
            Loc.T("速度必须是 20～300 之间的整数（BPM）。"));
        MarkField(gap, int.TryParse(gap.Text, out int gapValue) && gapValue is >= 10 and <= 5000,
            Loc.T("音符间隔／留白必须是 10～5000 之间的整数（毫秒）。"));
    }

    private static void MarkField(TextBox box, bool valid, string message)
    {
        if (valid)
        {
            box.ClearValue(Control.BorderBrushProperty);
            box.ClearValue(FrameworkElement.ToolTipProperty);
            return;
        }
        box.BorderBrush = UiTheme.Brush(UiTheme.Danger);
        box.ToolTip = message;
    }

    // ---------- 曲谱编辑区字号（Ctrl+滚轮 / Ctrl+0），只影响显示 ----------
    private const double ScoreFontDefault = 13;
    private double scoreFont = ScoreFontDefault;

    private void InstallScoreFontZoom()
    {
        score.PreviewMouseWheel += (_, e) =>
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
            e.Handled = true;
            SetScoreFont(scoreFont + (e.Delta > 0 ? 1 : -1));
        };
    }

    private void ResetScoreFont() => SetScoreFont(ScoreFontDefault);

    private void SetScoreFont(double size)
    {
        scoreFont = Math.Clamp(Math.Round(size, 1), 11, 26);
        score.FontSize = scoreFont;
        status.Text = Loc.F("谱面字号已调整为 {0:0.#}（Ctrl+滚轮缩放，Ctrl+0 复位）。", scoreFont);
    }

    // ---------- 窗口位置与大小记忆 ----------
    private void RestoreWindowPlacement()
    {
        if (settings.WindowWidth is not double width || settings.WindowHeight is not double height) return;
        double screenLeft = SystemParameters.VirtualScreenLeft, screenTop = SystemParameters.VirtualScreenTop;
        double screenWidth = SystemParameters.VirtualScreenWidth, screenHeight = SystemParameters.VirtualScreenHeight;
        double left = settings.WindowLeft ?? screenLeft + (screenWidth - width) / 2;
        double top = settings.WindowTop ?? screenTop + (screenHeight - height) / 2;
        // 至少保留 120px 可见，避免显示器被移除后窗口落在屏幕外。
        if (left + width < screenLeft + 120 || left > screenLeft + screenWidth - 120) return;
        if (top < screenTop - 8 || top > screenTop + screenHeight - 120) return;
        Width = Math.Clamp(width, MinWidth, 4000);
        Height = Math.Clamp(height, MinHeight, 4000);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left; Top = top;
    }

    private void CaptureWindowPlacement()
    {
        if (WindowState != WindowState.Normal) return;
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || !double.IsFinite(Left) || !double.IsFinite(Top)) return;
        settings = settings with { WindowWidth = Width, WindowHeight = Height, WindowLeft = Left, WindowTop = Top };
    }
}
