using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HarmonicaPlayer;

public static class Program
{
    [STAThread]
    public static void Main() => SingleInstance.Run();
}

public sealed partial class PlayerWindow : Window
{
    private readonly TextBox songTitle = new() { Text = "未命名曲谱" };
    private readonly TextBlock documentInfo = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock documentWarning = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button saveScore = new() { Content = "保存 TXT", Margin = new Thickness(5) };
    private readonly Button saveAs = new() { Content = "另存为", Margin = new Thickness(5) };
    private readonly Button newScore = new() { Content = "新建", Margin = new Thickness(5) };
    private readonly IScoreDialogs scoreDialogs;
    private string? documentPath;
    private bool legacyDocument, documentBusy;
    private string[] extraHeaders = Array.Empty<string>();
    private (string Title, string Bpm, string Body, string Gap) cleanDocument;
    private long saveGeneration;
    private bool DocumentDirty => cleanDocument != (songTitle.Text, bpm.Text, score.Text, gap.Text);
    private void UpdateDocumentTitle()
    {
        // 标题栏与任务栏标题：产品名与版本号取单一来源（AppName / 程序集版本），不再手写常量。
        Title = $"{AppName} {AppVersion} — {songTitle.Text}{(DocumentDirty ? " *" : "")}";
        documentInfo.Text = documentPath ?? Loc.T("尚未保存");
    }
    private void MarkDocumentClean() { cleanDocument = (songTitle.Text, bpm.Text, score.Text, gap.Text); UpdateDocumentTitle(); }
    private readonly TextBox score = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 230, Text = "1234567 【1234567】 （1234567） #1 #2 #4 #5 #6 111 000" };
    private readonly TextBox gap = new() { Text = "20", Width = 75 };
    private readonly CheckBox dry = new() { Content = "仅测试（不会操作游戏）", IsChecked = false, Margin = new Thickness(0, 10, 0, 10) };
    private readonly TextBlock status = new() { Text = "就绪：先导入谱面，快捷键状态见上方。", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox log = new() { IsReadOnly = true, Height = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button start = new() { Content = "开始 F6", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };
    private readonly Button import = new() { Content = "导入 TXT", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };
    private readonly TextBox bpm = new() { Text = "120", Width = 75 };
    private readonly TextBox preview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button refresh = new() { Content = "预览 / 检查", Margin = new Thickness(5), Padding = new Thickness(12, 6, 12, 6) };
    private readonly TextBlock alert = new() { TextWrapping = TextWrapping.Wrap, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed };
    private readonly Border alertBox = new() { Background = Brushes.MistyRose, Padding = new Thickness(12), Margin = new Thickness(0, 6, 0, 6), Visibility = Visibility.Collapsed };
    private string? scoreIssue, hotkeyIssue, operationIssue;
    private int? errorPosition, operationErrorPosition;
    private bool operationIsRuntime;
    private long playbackGeneration;
    private bool uiBusy;
    private readonly Button locateError = new() { Content = "定位曲谱错误", Margin = new Thickness(4) };
    private readonly TextBlock startReason = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed };
    private void UpdateAvailability()
    {
        bool blockedByHotkey = dry.IsChecked != true && hotkeys?.StopReady != true;
        start.IsEnabled = !uiBusy && scoreIssue == null && !blockedByHotkey;
        startReason.Text = scoreIssue != null ? Loc.T("无法开始：请先修正曲谱或速度、音符间隔。") :
            blockedByHotkey ? Loc.T("无法开始游戏演奏：请先修复停止快捷键。") : "";
        locateError.IsEnabled = !uiBusy && (errorPosition ?? operationErrorPosition) != null;
        listen.IsEnabled = listenFromCursor.IsEnabled = exportMidi.IsEnabled = !uiBusy && scoreIssue == null;
    }
    private void LocateError()
    {
        if (uiBusy || (errorPosition ?? operationErrorPosition) is not int position) return;
        position = Math.Clamp(position, 0, score.Text.Length);
        score.Focus(); score.Select(position, position < score.Text.Length ? 1 : 0);
        score.ScrollToLine(score.GetLineIndexFromCharacterIndex(position));
    }
    private void UpdateAlert()
    {
        if (operationIssue == null) { operationErrorPosition = null; operationIsRuntime = false; }
        UpdateMode();
        // Runtime failures stay first; preflight reminders follow the confirmed priority.
        var issues = new[] { operationIsRuntime ? operationIssue : null, PermissionHint, TestModeHint, scoreIssue,
            operationIsRuntime ? null : operationIssue, hotkeyIssue }.Where(x => !string.IsNullOrWhiteSpace(x));
        alert.Text = string.Join("\n\n", issues);
        alertBox.Visibility = alert.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateAvailability();
    }
    private void ReportIssue(string message, int? position = null, bool runtime = false)
    { operationIssue = message; operationErrorPosition = position; operationIsRuntime = runtime; UpdateAlert(); }

    private void ApplyThemeChoice()
    {
        var mode = themeChoice.SelectedIndex switch
        {
            1 => ThemeMode.Light,
            2 => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        UiTheme.SetMode(mode);
        settings = settings with { Theme = ThemePreference.Serialize(mode) };
        // 与“自定义快捷键”一致：编码框里是半成品数字时，只保存上一份有效的速度/间隔。
        _ = SaveCurrentSettingsAsync(true);
    }

    // 设置文件加载完成后按最终值对齐一次（预读可能因为文件损坏而回退成跟随系统）。
    private void SyncThemeChoice()
    {
        themeChoiceReady = false;
        themeChoice.SelectedIndex = (int)UiTheme.Mode;
        themeChoiceReady = true;
    }

    // 右栏卡片：复用各 partial 里既有的控件与事件，只改变摆放位置，不改变行为。
    private Border BuildListeningCard()
    {
        var body = new StackPanel();
        AddListeningControls(body);
        return CardPanel("本地试听（不发送游戏按键）", body);
    }

    private Border BuildHotkeyCard() =>
        CardPanel("全局快捷键与设置", Block(
            LHint("开始/停止键可自定义；修改期间会暂停全局快捷键，关闭此窗口后重新注册。"),
            Row(configure, retry),
            hotkeyStatus,
            settingsStatus));
    private CancellationTokenSource? cancellation;
    private Task? running;
    public bool IsClosing => closing;
    private SettingsWriter settingsWriter = null!;
    private readonly string settingsPath;
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly NativeInput output = new();
    private HwndSource? source;
    private IntPtr hwnd;
    private bool allowClose, closing, editingHotkeys, beginning;
    private HotkeyController? hotkeys;
    private AppSettings settings = new();
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock settingsStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock hotkeyStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private readonly Button configure = new() { Content = "自定义快捷键", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button retry = new() { Content = "重试注册", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private readonly Button stop = new() { Content = "停止 F8", Margin = new Thickness(5), Padding = new Thickness(15, 6, 15, 6) };
    private readonly ComboBox themeChoice = new() { Width = 132 };
    private bool themeChoiceReady;

    public PlayerWindow(string? settingsFile = null, IScoreDialogs? dialogs = null, Action<AppSettings>? writeSettings = null, ILocalAudioPlayer? audioPlayer = null, bool? administrator = null)
    {
        this.administrator = administrator ?? DetectAdministrator();
        scoreDialogs = dialogs ?? new ScoreDialogs();
        localAudio = audioPlayer ?? new LocalAudioPlayer();
        settingsPath = settingsFile ?? SettingsStore.DefaultPath;
        Title = AppName + " " + AppVersion;
        // 4:3 浮窗默认尺寸（1040×780）；仍可自由缩放，最小尺寸保证左右两栏都能完整放下。
        Width = 1040; Height = 780; MinWidth = 880; MinHeight = 660;
        // 主题与界面语言：先按设置文件里的偏好（只读预读，不改动原有加载顺序）选好调色板与文案再建资源，
        // 否则窗口会先按浅色/中文绘制再跳一次。
        UiTheme.SetMode(ThemePreference.Parse(SettingsStore.ReadTheme(settingsPath)));
        Loc.SetLanguage(LanguagePreference.Parse(SettingsStore.ReadLanguage(settingsPath)));
        // 外观与交互增强：统一主题样式、窗口内快捷键、拖放导入、曲谱字号缩放。
        BuildThemeResources();
        ApplyControlStyles();
        InstallInteraction();
        // 浮窗外壳：4:3 无边框浮窗（自绘标题栏）+ 顶栏状态区 + 选项卡内容区 + 底部播放条。
        // 只重新摆放既有控件；演奏、热键、宏执行与配置读写逻辑均未改动。
        SetupFloatingWindow();
        BuildShell();
        settings = SettingsStore.Load(settingsPath, out var loadWarning);
        settingsWriter = new SettingsWriter(writeSettings ?? (value => SettingsStore.Save(settingsPath, value)),
            writeSettings == null && loadWarning == null && File.Exists(settingsPath) ? settings : null);
        bpm.Text = settings.Bpm.ToString(); gap.Text = settings.Gap.ToString();
        volumeSlider.Value = settings.PreviewVolume; localAudio.Volume = settings.PreviewVolume;
        settingsStatus.Text = loadWarning ?? Loc.T("设置会自动保存；每次启动默认游戏演奏模式，“仅测试”不勾选。");
        // 设置已通过校验：按最终值再对齐一次主题与语言（预读失败时会回退成跟随系统/中文）。
        UiTheme.SetMode(ThemePreference.Parse(settings.Theme));
        Loc.SetLanguage(LanguagePreference.Parse(settings.Language));
        SyncThemeChoice();
        SyncLanguageChoice();
        ApplyLanguageTexts();
        InitializeLibrary();
        RestoreWindowPlacement();
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await SaveCurrentSettingsAsync(); };
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); Preview(); };
        configure.Click += (_, _) => ConfigureHotkeys();
        retry.Click += (_, _) => ApplyHotkeys();
        import.Click += async (_, _) => await Import(); start.Click += async (_, _) => await Begin();
        importMidi.Click += async (_, _) => await ImportMidiAsync();
        saveScore.Click += async (_, _) => await DocumentOperation(() => SaveScoreAsync(false));
        saveAs.Click += async (_, _) => await DocumentOperation(() => SaveScoreAsync(true));
        newScore.Click += async (_, _) => await DocumentOperation(async () =>
        {
            if (!await ConfirmUnsavedAsync()) return false;
            ApplyDocument(new ScoreDocument("未命名曲谱", 120, "")); return true;
        });
        stop.Click += (_, _) => Stop();
        refresh.Click += (_, _) => Preview();
        score.TextChanged += (_, _) => QueuePreview();
        bpm.TextChanged += (_, _) => QueuePreview();
        gap.TextChanged += (_, _) => QueuePreview();
        foreach (var box in new[] { bpm, gap })
            box.TextChanged += (_, _) => QueueSave();
        // 即时校验：速度/间隔非法时立刻标红并给出提示，不必等到点“开始”。
        foreach (var box in new[] { bpm, gap })
            box.TextChanged += (_, _) => MarkTimingFields();
        MarkTimingFields();
        foreach (var box in new[] { songTitle, bpm, score, gap })
            box.TextChanged += (_, _) => UpdateDocumentTitle();
        MarkDocumentClean();
        dry.Checked += (_, _) => UpdateAlert();
        dry.Unchecked += (_, _) => UpdateAlert();
        SetBusy(false); Preview();
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(hwnd); source.AddHook(Hook);
            hotkeys = new HotkeyController(new NativeHotkeys(hwnd));
            ApplyHotkeys();
        };
        Closing += (_, e) =>
        {
            if (allowClose) return;
            e.Cancel = true;
            if (closing) return;
            if (documentBusy) { ReportIssue(Loc.T("正在读写曲谱，请完成后再关闭。")); return; }
            closing = true;
            CaptureWindowPlacement();
            saveTimer.Stop(); previewTimer.Stop();
            cancellation?.Cancel(); // Cancel first; do not put disk I/O before it.
            SetBusy(true); stop.IsEnabled = false;
            status.Text = Loc.T("正在停止演奏并关闭…");
            // Always leave the initial Closing event before calling Close again,
            // even when there is no playback task or every await completes inline.
            Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                new Action(() => _ = CloseAfterStopAsync()));
        };
    }
    private async Task CloseAfterStopAsync()
    {
        try
        {
            Task? playback = running;
            if (playback != null)
            {
                if (await Task.WhenAny(playback, Task.Delay(1500)) != playback)
                    status.Text = Loc.T("正在等待演奏任务停止，窗口仍可响应…");
                try { await playback; }
                catch (Exception e) { status.Text = Loc.T("演奏结束异常：") + e.Message; }
            }
            // No background note sender remains before final cleanup begins.
            string? releaseError = await Task.Run(output.Release);
            if (releaseError != null)
            {
                // Do not silently exit with keys still held. A second close retries.
                status.Text = Loc.T("按键释放失败，请手动按下并松开相关键后再关闭：") + releaseError; ReportIssue(status.Text, runtime: true);
                return;
            }
            if (!await ConfirmUnsavedAsync()) { status.Text = Loc.T("已取消关闭。"); return; }
            Task save = SaveCurrentSettingsAsync(true);
            if (await Task.WhenAny(save, Task.Delay(1500)) != save)
                status.Text = Loc.T("正在保存最后的设置，请稍候（窗口仍可响应）…");
            await save;
            hotkeys?.Suspend(); source?.RemoveHook(Hook);
            allowClose = true; Close();
        }
        catch (Exception e) { status.Text = Loc.T("关闭未完成，请重试：") + e.Message; ReportIssue(status.Text); }
        finally
        {
            if (!allowClose)
            {
                closing = false; stop.IsEnabled = true; SetBusy(false);
                Preview(); QueueSave(); // A cancelled close must restore validation/debouncing.
            }
        }
    }
    private void QueuePreview()
    {
        if (closing) return;
        // A runtime character location belongs to the old score/settings snapshot.
        if (operationErrorPosition != null) { operationIssue = null; operationErrorPosition = null; UpdateAlert(); }
        previewTimer.Stop(); previewTimer.Start();
    }

    private IntPtr Hook(IntPtr h, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x0312)
        {
            // Discard queued messages from old registrations after editing.
            uint packed = unchecked((uint)l.ToInt64());
            uint key = packed >> 16, modifiers = packed & 0xFFFF;
            if (!editingHotkeys && w.ToInt32() == 1 && hotkeys?.StartReady == true &&
                key == settings.Start.Key && modifiers == settings.Start.Modifiers) _ = Begin(key);
            if (!editingHotkeys && w.ToInt32() == 2 && hotkeys?.StopReady == true &&
                key == settings.Stop.Key && modifiers == settings.Stop.Modifiers) Stop();
            // 曲谱库快捷键：编号与列表顺序一一对应；按键与当前设置不符时按旧注册丢弃。
            int id = w.ToInt32();
            if (!editingHotkeys && ScoreLibrary.IsLibraryHotkeyId(id) && hotkeys?.LibraryReady(id) == true)
            {
                int index = ScoreLibrary.IndexFromHotkeyId(id);
                if (index >= 0 && index < settings.Library.Length && settings.Library[index].Hotkey is HotkeyBinding binding &&
                    key == binding.Key && modifiers == binding.Modifiers)
                    _ = LibraryActivateAsync(index, key, play: settings.LibraryHotkeyPlays);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }
    private void QueueSave()
    {
        if (closing) return;
        ++saveGeneration; // Invalidate completion even during the debounce interval.
        saveTimer.Stop(); saveTimer.Start();
    }
    private async Task SaveCurrentSettingsAsync(bool preserveValidTiming = false)
    {
        long generation = ++saveGeneration;
        AppSettings next = settings with { PreviewVolume = (int)volumeSlider.Value };
        string? invalid = null;
        if (!int.TryParse(bpm.Text, out int tempo) || !int.TryParse(gap.Text, out int silence))
            invalid = Loc.T("部分数值尚未填写完整，保留上次有效设置。");
        else
        {
            var candidate = next with { Bpm = tempo, Gap = silence };
            try { candidate.Validate(); next = candidate; }
            catch (FormatException e) { invalid = e.Message; }
        }
        if (invalid != null && !preserveValidTiming)
        { settingsStatus.Text = invalid; return; }
        settings = next; // Update the in-memory snapshot before asynchronous disk work.
        string? error = await settingsWriter.SaveAsync(next);
        if (generation != saveGeneration) return; // Never replace a newer edit/error's status.
        settingsStatus.Text = error != null ? Loc.T("设置未保存：") + error :
            invalid ?? Loc.T("设置已保存（快捷键、速度、音符间隔和试听音量）。");
    }
    private void ApplyHotkeys()
    {
        if (hotkeys == null || cancellation != null || closing || beginning || editingHotkeys || documentBusy) return;
        hotkeys.Apply(settings.Start, settings.Stop, LibraryHotkeyBindings());
        UpdateMode(); stop.Content = Loc.T("停止 ") + settings.Stop.Label;
        hotkeyStatus.Text = hotkeys.Describe(settings.Start, settings.Stop) + LibraryHotkeySummary();
        hotkeyIssue = hotkeys.StartReady && hotkeys.StopReady ? null :
            hotkeyStatus.Text + Loc.T("\n常见原因：其他播放器、录屏或键盘工具占用了热键。点击“自定义快捷键”换一个组合，或关闭占用程序后点“重试注册”。管理员权限不能解除热键占用。");
        UpdateAlert();
        RefreshLibraryList(); // 状态列反映本次真实注册结果
    }
    private void ConfigureHotkeys()
    {
        if (hotkeys == null || cancellation != null || closing || beginning || editingHotkeys || documentBusy) return;
        editingHotkeys = true; hotkeys.Suspend();
        try
        {
            var dialog = new HotkeyDialog(settings.Start, settings.Stop) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                settings = settings with { Start = dialog.Start, Stop = dialog.Stop };
                _ = SaveCurrentSettingsAsync(true);
            }
        }
        finally { editingHotkeys = false; ApplyHotkeys(); }
    }
    private async Task WaitForRelease(uint triggerKey, CancellationToken token)
    {
        var timeout = Stopwatch.StartNew();
        while (NativeHotkeys.Held(triggerKey))
        {
            token.ThrowIfCancellationRequested();
            if (timeout.Elapsed.TotalSeconds > 15)
                throw new InvalidOperationException(Loc.T("等待松开快捷键超时，已取消演奏。"));
            status.Text = Loc.F("请松开启动键及 Ctrl / Alt / Shift / Win；{0} 可停止。", settings.Stop.Label);
            await Task.Delay(20, token);
        }
        token.ThrowIfCancellationRequested();
    }
    private async Task DocumentOperation(Func<Task<bool>> action)
    {
        if (cancellation != null || beginning || editingHotkeys || closing || documentBusy) return;
        documentBusy = true; SetBusy(true);
        try { await action(); }
        catch (Exception e) { ReportIssue(Loc.T("曲谱操作失败：") + e.Message); }
        finally { documentBusy = false; SetBusy(false); }
    }
    private Task Import() => DocumentOperation(async () =>
    {
        string? path = scoreDialogs.Open(this);
        if (path == null) return false;
        int fallback = int.TryParse(bpm.Text, out int value) && value is >= 20 and <= 300 ? value : settings.Bpm;
        var result = await ScoreDocumentReader.ReadAsync(path, fallback);
        if (!await ConfirmUnsavedAsync()) return false;
        // Saving the current document may have updated the very file selected for import.
        result = await ScoreDocumentReader.ReadAsync(path, fallback);
        ApplyDocument(result.Document);
        documentWarning.Text = string.Join("\n", result.Warnings);
        status.Text = scoreIssue == null ? Loc.T("已导入，检查通过；请确认速度（BPM）。") : Loc.T("已导入，请修正顶部提示的错误。");
        return true;
    });
    private void ApplyDocument(ScoreDocument document)
    {
        documentPath = document.SourcePath; legacyDocument = document.IsLegacy;
        extraHeaders = document.ExtraHeaders ?? Array.Empty<string>();
        songTitle.Text = document.Title; bpm.Text = document.Bpm.ToString(); score.Text = document.ScoreText;
        gap.Text = document.Gap.ToString();
        documentWarning.Text = ""; operationIssue = null; MarkDocumentClean(); Preview();
        RefreshLibraryList(); // 曲谱库中标出当前加载的曲谱
    }
    private async Task<bool> ConfirmUnsavedAsync()
    {
        if (!DocumentDirty) return true;
        return scoreDialogs.Unsaved(this) switch
        {
            MessageBoxResult.No => true,
            MessageBoxResult.Yes => await SaveScoreAsync(false),
            _ => false
        };
    }
    private async Task<bool> SaveScoreAsync(bool forceSaveAs)
    {
        try
        {
            var document = new ScoreDocument(songTitle.Text, PlaybackValidation.ParseBpm(bpm.Text), score.Text,
                documentPath, legacyDocument, extraHeaders, PlaybackValidation.ParseGap(gap.Text));
            document.Validate(gap.Text); // Validate before asking for a destination.
            string? path = documentPath;
            if (forceSaveAs || legacyDocument || path == null)
            {
                string name = string.Concat(songTitle.Text.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                path = scoreDialogs.Save(this, name + (legacyDocument ? "-新版" : "") + ".txt");
                if (path == null) return false;
            }
            await ScoreDocumentWriter.SaveAsync(path, document, gap.Text);
            documentPath = Path.GetFullPath(path); legacyDocument = false;
            documentWarning.Text = string.Join("\n", ScoreDocumentReader.Parse(
                ScoreDocumentWriter.Serialize(document, gap.Text), documentPath, document.Bpm).Warnings);
            MarkDocumentClean(); operationIssue = null; UpdateAlert();
            status.Text = Loc.T("曲谱已保存（新格式，含曲名、BPM和音符间隔）。");
            return true;
        }
        catch (Exception e) { ReportIssue(Loc.T("曲谱未保存：") + e.Message); return false; }
    }
    private void Stop()
    {
        if (closing) return; // Shutdown owns final input cleanup; do not race it.
        cancellation?.Cancel();
        if (cancellation != null) status.Text = listening ? Loc.T("正在停止试听…") : Loc.T("正在停止并释放输入…");
        else { var error = output.Release(); status.Text = error ?? Loc.T("已停止。"); if (error != null) ReportIssue(Loc.T("释放按键失败：") + error, runtime: true); }
    }
    private async Task Begin(uint triggerKey = 0)
    {
        if (cancellation != null || closing || beginning || editingHotkeys || documentBusy) return;
        beginning = true; ++playbackGeneration; SetBusy(true);
        playbackProgress.Value = 0;
        operationIssue = null; UpdateAlert();
        try
        {
            var notes = ScoreParser.Parse(score.Text);
            double ms = Timing();
            int silence = ValidateGap(notes, ms);
            scoreIssue = null; errorPosition = null; UpdateAlert();
            bool simulation = dry.IsChecked == true;
            if (!simulation && hotkeys?.StopReady != true)
                throw new InvalidOperationException(Loc.F("停止键 {0} 不可用，禁止真实演奏。请点击“自定义快捷键”或“重试注册”。", settings.Stop.Label));
            cancellation = new CancellationTokenSource();
            if (closing || cancellation.IsCancellationRequested) return;
            _ = SaveCurrentSettingsAsync();
            SetBusy(true); log.Clear();
            running = Run(notes, ms, silence, simulation, triggerKey, cancellation.Token);
            await running;
        }
        catch (FormatException) { Preview(); status.Text = Loc.T("无法开始，请查看顶部提示。"); }
        catch (Exception e)
        {
            status.Text = Loc.T("无法开始，请查看顶部提示。");
            if (dry.IsChecked != true && hotkeys?.StopReady != true) UpdateAlert();
            else ReportIssue(e.Message);
        }
        finally
        {
            cancellation?.Cancel(); // Invalidate queued progress from the completed run.
            cancellation?.Dispose(); cancellation = null; running = null; beginning = false; SetBusy(false);
        }
    }
    private int ValidateGap(List<ScoreNote> notes, double ms)
        => PlaybackValidation.ValidateGap(gap.Text, notes, ms);
    private double Timing()
    {
        return 60000.0 / PlaybackValidation.ParseBpm(bpm.Text);
    }
    private void Preview()
    {
        if (cancellation != null || closing) return;
        try
        {
            var notes = ScoreParser.Parse(score.Text);
            double ms = Timing();
            ValidateGap(notes, ms);
            double total = 0;
            var lines = new StringBuilder();
            foreach (var note in notes)
            {
                if (lines.Length < 30000)
                    lines.AppendLine(Loc.F("{0:0.###}s  {1}  {2:0.###}拍  ({3:0.##}ms)", total / 1000, note.Label, note.Beats, note.Beats * ms));
                total += note.Beats * ms;
            }
            scoreIssue = null; errorPosition = null; UpdateAlert();
            preview.Text = Loc.F("共{0}个音/休止，{1:0.###}拍，预计{2:0.###}秒（不含倒计时）\n",
                notes.Count, notes.Sum(n => n.Beats), total / 1000) + lines;
        }
        catch (Exception e) { errorPosition = (e as ScoreFormatException)?.Position; scoreIssue = Loc.T("谱面／设置错误：") + e.Message; preview.Text = scoreIssue; UpdateAlert(); }
    }
    private void SetBusy(bool busy)
    {
        busy |= closing || documentBusy;
        uiBusy = busy;
        import.IsEnabled = importMidi.IsEnabled = refresh.IsEnabled = gap.IsEnabled = dry.IsEnabled = !busy;
        bpm.IsEnabled = !busy;
        configure.IsEnabled = retry.IsEnabled = !busy;
        score.IsReadOnly = busy;
        songTitle.IsReadOnly = busy;
        saveScore.IsEnabled = saveAs.IsEnabled = newScore.IsEnabled = !busy;
        ApplyLibraryBusy(busy);
        UpdateAlert();
    }
    private async Task Run(List<ScoreNote> notes, double ms, int silence, bool simulation, uint triggerKey, CancellationToken token)
    {
        string result = simulation ? Loc.T("测试完成：未操作游戏，也未发声。需要游戏演奏请取消“仅测试”。") : Loc.T("演奏完成。");
        bool failed = false;
        PlaybackTimingException? timingFailure = null;
        string playbackBody = score.Text;
        long generation = playbackGeneration;
        try
        {
            await WaitForRelease(triggerKey, token);
            for (int n = 3; n > 0; n--)
            { status.Text = Loc.F("{0}{1}：{2}秒后开始，请切到目标窗口…",
                (!simulation && !administrator ? Loc.T("权限提醒：未以管理员运行，允许继续。\n") : ""),
                Loc.T(simulation ? "仅测试" : "游戏演奏"), n); await Task.Delay(1000, token); }
            // Check again after countdown: the user may have used Alt+Tab to switch windows.
            await WaitForRelease(triggerKey, token);
            var target = NativeInput.GetForegroundWindow();
            NativeInput.GetWindowThreadProcessId(target, out uint pid);
            if (!simulation && (target == IntPtr.Zero || pid == (uint)Environment.ProcessId))
                throw new InvalidOperationException(Loc.T("倒计时结束时仍在播放器窗口，已取消。请在3秒倒计时内切到游戏口琴界面，或在游戏内按开始快捷键。"));
            await Task.Run(() =>
            {
                var schedule = GamePlaybackTiming.Create(notes, ms, silence);
                var clock = Stopwatch.StartNew();
                void Check()
                {
                    token.ThrowIfCancellationRequested();
                    if (!simulation && NativeInput.GetForegroundWindow() != target)
                        throw new InvalidOperationException(Loc.T("目标窗口失去焦点，已停止。演奏期间请勿切换窗口；切回游戏后重新开始。"));
                }
                void Wait(double until)
                {
                    while (true)
                    {
                        Check(); double remaining = until - clock.Elapsed.TotalMilliseconds;
                        if (remaining <= 0) return;
                        if (remaining > 2) token.WaitHandle.WaitOne((int)Math.Min(5, Math.Max(1, remaining - 1)));
                        else Thread.SpinWait(100);
                    }
                }
                var initialError = simulation ? null : output.Release();
                if (initialError != null) throw new InvalidOperationException(initialError);
                for (int i = 0; i < schedule.Count; i++)
                {
                    var step = schedule[i];
                    var note = step.Note;
                    double noteMs = step.DurationMs;
                    double begin = step.StartMs;
                    Wait(begin); Check();
                    // Keep the original deadlines; tiny rests receive independent jitter tolerance.
                    PlaybackTimingGuard.Check(step, clock.Elapsed.TotalMilliseconds, playbackBody, i);
                    int index = i;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (closing || token.IsCancellationRequested) return;
                        // 进度条与进度文本必须在 UI 线程更新；旧任务不会覆盖新一次演奏的进度。
                        if (playbackGeneration == generation)
                        {
                            playbackProgress.Value = notes.Count == 0 ? 100 : index * 100.0 / notes.Count;
                            playbackPosition.Text = Loc.F("{0}/{1} 个音", index + 1, notes.Count);
                        }
                        status.Text = Loc.F("{0}/{1}：{2} / {3:0.###}拍", index + 1, notes.Count, note.Label, note.Beats);
                        if (log.LineCount > 150) log.Clear();
                        log.AppendText(Loc.F("{0:0}ms  {1}  {2:0.###}拍 ({3:0.##}ms)\n", begin, note.Label, note.Beats, noteMs)); log.ScrollToEnd();
                    }));
                    if (note.Degree != 0)
                    {
                        PlaybackTimingGuard.Check(step, clock.Elapsed.TotalMilliseconds, playbackBody, i);
                        if (!simulation) output.Modifiers(note);
                        Wait(step.KeyDownMs!.Value); Check();
                        PlaybackTimingGuard.Check(step, clock.Elapsed.TotalMilliseconds, playbackBody, i, step.KeyDownMs.Value);
                        if (!simulation) output.NoteOn(note);
                        Wait(step.KeyUpMs!.Value);
                        var error = simulation ? null : output.Release();
                        if (error != null) throw new InvalidOperationException(error);
                    }
                    Wait(step.EndMs);
                }
            }, token);
        }
        catch (OperationCanceledException) { result = Loc.T("已停止。"); }
        catch (Exception e) { failed = true; timingFailure = e as PlaybackTimingException; result = Loc.T("已停止：") + e.Message; }
        finally
        {
            // 工作任务已退出，再做最终释放，避免释放之后仍有旧任务按键。
            var error = simulation ? null : await Task.Run(output.Release);
            if (!closing)
            {
                playbackProgress.Value = !failed && error == null && !token.IsCancellationRequested ? 100 : 0;
                playbackPosition.Text = result;
                status.Text = error == null ? result : result + Loc.T(" 释放失败，请手动按下并松开相关键：") + error;
                if (failed || error != null) ReportIssue(status.Text, timingFailure?.Position, runtime: true);
                if (timingFailure != null)
                {
                    string message = status.Text; int position = timingFailure.Position;
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        // Show after cancellation cleanup; discard an old run's queued dialog.
                        if (!closing && cancellation == null && playbackGeneration == generation && operationErrorPosition == position && operationIssue == message)
                            scoreDialogs.PlaybackIssue(this, message);
                    }));
                }
            }
        }
    }
}
