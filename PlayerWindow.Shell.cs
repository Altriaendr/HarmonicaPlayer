using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace HarmonicaPlayer;

// 浮窗外壳：无边框浮窗（自绘标题栏）+ 顶栏状态区 + 选项卡内容区 + 底部固定播放条。
// 只重新摆放既有控件、复用既有事件与业务方法；演奏、热键、宏执行与配置读写逻辑未改。
public sealed partial class PlayerWindow
{
    public const string AppName = "HarpKit";

    // 显示用版本号直接取程序集版本，避免标题栏与 csproj 里的版本号再次脱节。
    public static string AppVersion => typeof(PlayerWindow).Assembly.GetName().Version is { } version
        ? $"{version.Major}.{version.Minor}.{version.Build}" : "dev";

    private readonly ComboBox languageChoice = new() { Width = 104 };
    private readonly Button minimizeWindow = new() { Content = "‒" };
    private readonly Button maximizeWindow = new() { Content = "▢" };
    private readonly Button closeWindow = new() { Content = "✕" };
    private readonly List<Action> localizedText = new();
    private bool languageChoiceReady;
    private Border? shellFrame;

    // ---------- 本地化登记：切换语言时重新套用一次，控件实例与事件都不变 ----------
    private void Localize(Action apply)
    {
        apply();
        localizedText.Add(apply);
    }

    private TextBlock LSectionTitle(string chinese)
    {
        var block = SectionTitle("");
        Localize(() => block.Text = Loc.T(chinese));
        return block;
    }

    private TextBlock LHint(string chinese)
    {
        var block = Hint("");
        Localize(() => block.Text = Loc.T(chinese));
        return block;
    }

    private TextBlock LLabel(string chinese)
    {
        var block = FieldLabel("");
        Localize(() => block.Text = Loc.T(chinese));
        return block;
    }

    private TextBlock LText(string chinese, double fontSize, string color)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            Foreground = UiTheme.Brush(color),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Localize(() => block.Text = Loc.T(chinese));
        return block;
    }

    private Button LButton(Button button, string chinese)
    {
        Localize(() => button.Content = Loc.T(chinese));
        return button;
    }

    private void LToolTip(FrameworkElement element, string chinese) =>
        Localize(() => element.ToolTip = Loc.T(chinese));

    // 卡片 = 标题行 + 可伸展主体：主体自身用 Grid 组织，列表/控制台才能占满剩余高度。
    private Border CardPanel(string chineseTitle, UIElement body)
    {
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var title = LSectionTitle(chineseTitle);
        Grid.SetRow(title, 0); layout.Children.Add(title);
        Grid.SetRow(body, 1); layout.Children.Add(body);
        return new Border { Style = Ui("Card"), Child = layout };
    }

    // ---------- 外壳 ----------
    private void BuildShell()
    {
        Icon = UiIcon.Image();
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 标题栏
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 状态区
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });     // 内容
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 底部播放条
        var titleBar = BuildTitleBar();
        Grid.SetRow(titleBar, 0); root.Children.Add(titleBar);
        var statusStrip = BuildStatusStrip();
        Grid.SetRow(statusStrip, 1); root.Children.Add(statusStrip);
        var content = BuildContentArea();
        Grid.SetRow(content, 2); root.Children.Add(content);
        var footer = BuildFooter();
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        // 1px 描边 + 圆角：让无边框浮窗在任何主题下都有明确边界。
        shellFrame = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = UiTheme.Brush(UiTheme.CardBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = root
        };
        Content = shellFrame;
        // 默认曲名/就绪提示只按初始值翻译一次；之后不会覆盖用户输入或运行中的状态文字。
        if (songTitle.Text == "未命名曲谱") songTitle.Text = Loc.T("未命名曲谱");
        if (status.Text == "就绪：先导入谱面，快捷键状态见上方。") status.Text = Loc.T("就绪：先导入谱面，快捷键状态见上方。");
        documentInfo.Text = documentPath ?? Loc.T("尚未保存");
        RegisterStaticTexts();
    }

    // 固定文案（字段初始化过的控件）在这里统一登记，语言切换时整表重放。
    private void RegisterStaticTexts()
    {
        LButton(newScore, "新建");
        LButton(import, "导入 TXT");
        LButton(importMidi, "导入单旋律 MIDI");
        LButton(saveScore, "保存 TXT");
        LButton(saveAs, "另存为");
        LButton(refresh, "预览 / 检查");
        LButton(locateError, "定位曲谱错误");
        LButton(listen, "从头试听");
        LButton(listenFromCursor, "从光标试听");
        LButton(exportMidi, "导出 MIDI");
        LButton(configure, "自定义快捷键");
        LButton(retry, "重试注册");
        LButton(addLibrary, "添加曲谱…");
        LButton(bindLibrary, "绑定快捷键…");
        LButton(clearLibraryHotkey, "清除绑定");
        LButton(removeLibrary, "移除");
        LButton(moveLibraryUp, "上移");
        LButton(moveLibraryDown, "下移");
        LButton(loadLibrary, "加载到编辑器");
        LButton(playLibrary, "加载并演奏");
        Localize(() => dry.Content = Loc.T("仅测试（不会操作游戏）"));
        Localize(() => libraryPlays.Content = Loc.T("按快捷键立即演奏（不勾选则只加载曲谱）"));
        LButton(start, "开始演奏 ");
        LButton(stop, "停止 ");
    }

    // ---------- 标题栏 ----------
    private Border BuildTitleBar()
    {
        var bar = new Border { Style = Ui("TitleBar"), Padding = new Thickness(12, 6, 8, 6) };
        LToolTip(bar, "拖动标题栏或空白处可移动窗口；双击标题栏最大化或还原。");
        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { ToggleMaximize(); return; }
            if (WindowState == WindowState.Maximized) return;
            DragMove();
        };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(UiIcon.Logo(22));
        brand.Children.Add(new TextBlock
        {
            Text = AppName, FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Brush(UiTheme.PrimaryText),
            Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        });
        var subtitle = LText("TXT → 单音口琴演奏", 12, UiTheme.MutedText);
        subtitle.Margin = new Thickness(10, 0, 0, 0);
        brand.Children.Add(subtitle);
        Grid.SetColumn(brand, 0); layout.Children.Add(brand);

        modeStatus.VerticalAlignment = VerticalAlignment.Center;
        modeStatus.HorizontalAlignment = HorizontalAlignment.Right;
        modeStatus.Margin = new Thickness(12, 0, 12, 0);
        Grid.SetColumn(modeStatus, 1); layout.Children.Add(modeStatus);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        tools.Children.Add(LLabel("界面语言："));
        languageChoice.Style = Ui("Combo");
        languageChoice.ItemsSource = LanguageNames;
        languageChoice.SelectedIndex = Loc.IsEnglish ? 1 : 0;
        languageChoice.SelectionChanged += (_, _) => { if (languageChoiceReady) ApplyLanguageChoice(); };
        languageChoiceReady = true;
        LToolTip(languageChoice, "界面语言。切换后立即生效并记住；尚未翻译的条目会保持中文。");
        tools.Children.Add(languageChoice);
        tools.Children.Add(LLabel("外观主题："));
        themeChoice.Style = Ui("Combo");
        themeChoice.Width = 122;
        // 选项文案随语言重建；重建期间关掉就绪标志，避免触发一次多余的保存。
        Localize(() =>
        {
            bool ready = themeChoiceReady;
            themeChoiceReady = false;
            int index = Math.Max(0, themeChoice.SelectedIndex);
            themeChoice.ItemsSource = new[] { Loc.T("跟随系统"), Loc.T("浅色"), Loc.T("深色") };
            themeChoice.SelectedIndex = index;
            themeChoiceReady = ready;
        });
        themeChoice.SelectionChanged += (_, _) => { if (themeChoiceReady) ApplyThemeChoice(); };
        themeChoiceReady = true;
        LToolTip(themeChoice, "外观主题。选择后立即生效并记住；“跟随系统”会实时响应 Windows 个性化设置。");
        tools.Children.Add(themeChoice);

        var windowButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        foreach (var button in new[] { minimizeWindow, maximizeWindow, closeWindow })
        {
            button.Style = Ui("IconButton");
            windowButtons.Children.Add(button);
        }
        minimizeWindow.Click += (_, _) => SystemCommands.MinimizeWindow(this);
        maximizeWindow.Click += (_, _) => ToggleMaximize();
        closeWindow.Click += (_, _) => Close();
        LToolTip(minimizeWindow, "最小化");
        LToolTip(maximizeWindow, "最大化/还原");
        LToolTip(closeWindow, "关闭");
        tools.Children.Add(windowButtons);
        Grid.SetColumn(tools, 2); layout.Children.Add(tools);

        bar.Child = layout;
        return bar;
    }

    // ---------- 顶栏状态区：状态一句话 + 仅测试开关 + 报警与处理按钮 ----------
    private Border BuildStatusStrip()
    {
        var strip = new Border { Style = Ui("HeaderBar") };
        var body = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 0); top.Children.Add(status);
        dry.HorizontalAlignment = HorizontalAlignment.Right;
        dry.VerticalAlignment = VerticalAlignment.Center;
        dry.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(dry, 1); top.Children.Add(dry);
        body.Children.Add(top);

        var alertPanel = new StackPanel();
        alertPanel.Children.Add(alert);
        var alertActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var fixHotkeys = LButton(new Button(), "修改快捷键");
        var retryHotkeys = LButton(new Button(), "重试注册");
        var dismissIssue = LButton(new Button(), "清除上次操作提示");
        fixHotkeys.Click += (_, _) => ConfigureHotkeys();
        retryHotkeys.Click += (_, _) => ApplyHotkeys();
        dismissIssue.Click += (_, _) => { operationIssue = null; UpdateAlert(); };
        locateError.Click += (_, _) => LocateError();
        alertActions.Children.Add(locateError);
        alertActions.Children.Add(fixHotkeys);
        alertActions.Children.Add(retryHotkeys);
        alertActions.Children.Add(dismissIssue);
        alertPanel.Children.Add(alertActions);
        alertBox.Child = alertPanel;
        body.Children.Add(alertBox);
        strip.Child = body;
        return strip;
    }

    // ---------- 内容区：左=曲谱编辑，右=选项卡（曲谱库独占整列高度，不再被压缩） ----------
    private Grid BuildContentArea()
    {
        var content = new Grid { Margin = new Thickness(12, 12, 12, 10) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 曲名与文档信息
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 文件工具行
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });     // 编辑区
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                          // 速度与间隔
        Grid.SetColumn(left, 0); content.Children.Add(left);

        var head = new StackPanel();
        head.Children.Add(Row(LLabel("曲名（与文件名独立）："), songTitle));
        documentInfo.Margin = new Thickness(0, 2, 0, 0);
        documentWarning.Margin = new Thickness(0, 4, 0, 0);
        head.Children.Add(documentInfo);
        head.Children.Add(documentWarning);
        Grid.SetRow(head, 0); left.Children.Add(head);

        var fileControls = Row(newScore, import, saveScore, saveAs, importMidi, refresh);
        Grid.SetRow(fileControls, 1); left.Children.Add(fileControls);

        var editorBlock = new Grid();
        editorBlock.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        editorBlock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(score, 0); editorBlock.Children.Add(score);
        var syntaxHint = LHint("【高音】 （低音） 【【1】】最高do、【【#1】】升半音；5:1.25指定总拍数；#或＃升半音；0休止，-或—延长一拍，_半拍，__四分之一拍，.附点。例：1 2_ 3_ 5 — | 0 6. 5_ 1 |");
        Grid.SetRow(syntaxHint, 1); editorBlock.Children.Add(syntaxHint);
        Grid.SetRow(editorBlock, 2); left.Children.Add(editorBlock);

        var parameters = Row(LLabel("速度（BPM）："), bpm, LLabel("音符间隔／留白(ms)："), gap);
        Grid.SetRow(parameters, 3); left.Children.Add(parameters);

        var tabs = new TabControl { Style = Ui("Tab"), Margin = new Thickness(12, 0, 0, 0) };
        tabs.Items.Add(TabPage("曲谱库", BuildLibraryPanel()));
        tabs.Items.Add(TabPage("试听与预览", BuildListeningPanel()));
        tabs.Items.Add(TabPage("快捷键与日志", BuildHotkeyLogPanel()));
        tabs.SelectedIndex = 0;
        Grid.SetColumn(tabs, 1); content.Children.Add(tabs);
        return content;
    }

    private TabItem TabPage(string chinese, UIElement body)
    {
        // 空白样式：不让隐式 TextBlock 样式固定前景色，选中项才能整体变成主色。
        var header = new TextBlock { Style = new Style(typeof(TextBlock)), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Localize(() => header.Text = Loc.T(chinese));
        return new TabItem { Header = header, Content = body };
    }

    private UIElement BuildListeningPanel()
    {
        var stack = new StackPanel();
        stack.Children.Add(BuildListeningCard());
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hint = LHint("每个音的拍数与时间；不是图片识谱。");
        Grid.SetRow(hint, 0); body.Children.Add(hint);
        Grid.SetRow(preview, 1); body.Children.Add(preview);
        stack.Children.Add(CardPanel("解析预览", body));
        return stack;
    }

    private UIElement BuildHotkeyLogPanel()
    {
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hotkeys = BuildHotkeyCard();
        Grid.SetRow(hotkeys, 0); layout.Children.Add(hotkeys);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hint = LHint("游戏演奏模式会操作键鼠；仅测试不会操作游戏，也不会发声。开始后倒计时3秒，请切到游戏口琴界面；演奏期间除停止键外不要操作键鼠。");
        Grid.SetRow(hint, 0); body.Children.Add(hint);
        Grid.SetRow(log, 1); body.Children.Add(log);
        var logCard = CardPanel("演奏日志", body);
        Grid.SetRow(logCard, 1); layout.Children.Add(logCard);
        return layout;
    }

    // ---------- 底部固定播放条：曲谱滚动时按钮与进度始终可见 ----------
    private Border BuildFooter()
    {
        var footer = new Border { Style = Ui("FooterBar") };
        var layout = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        start.Margin = new Thickness(0);
        stop.Margin = new Thickness(8, 0, 0, 0);
        var transportButtons = new StackPanel { Orientation = Orientation.Horizontal };
        transportButtons.Children.Add(start); transportButtons.Children.Add(stop);
        var transport = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        transport.Children.Add(transportButtons);
        transport.Children.Add(startReason);
        Grid.SetColumn(transport, 0); layout.Children.Add(transport);
        playbackProgress.Margin = new Thickness(0);
        playbackPosition.Margin = new Thickness(0, 4, 0, 0);
        var progressStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        progressStack.Children.Add(playbackProgress);
        progressStack.Children.Add(playbackPosition);
        Grid.SetColumn(progressStack, 1); layout.Children.Add(progressStack);
        footer.Child = layout;
        return footer;
    }

    // ---------- 语言切换 ----------
    private static readonly string[] LanguageNames = { "中文", "English" };

    private void ApplyLanguageChoice()
    {
        if (!languageChoiceReady) return;
        SetLanguage(languageChoice.SelectedIndex == 1 ? UiLanguage.English : UiLanguage.Chinese);
    }

    // 立即生效并记住：只重放文案与表单显示，不重启窗口、不影响演奏与快捷键状态。
    private void SetLanguage(UiLanguage language)
    {
        bool changed = Loc.Current != language;
        Loc.SetLanguage(language);
        languageChoiceReady = false;
        languageChoice.SelectedIndex = language == UiLanguage.English ? 1 : 0;
        languageChoiceReady = true;
        settings = settings with { Language = LanguagePreference.Serialize(language) };
        ApplyLanguageTexts();
        if (changed && !uiBusy && cancellation == null && !beginning && !documentBusy && !closing)
            status.Text = Loc.T(language == UiLanguage.English ? "界面语言已切换为英文。" : "界面语言已切换为中文。");
        _ = SaveCurrentSettingsAsync(true);
    }

    // 设置读取完成后按最终值对齐一次（预读失败时会回退成中文）。
    private void SyncLanguageChoice()
    {
        languageChoiceReady = false;
        languageChoice.SelectedIndex = Loc.IsEnglish ? 1 : 0;
        languageChoiceReady = true;
    }

    // 语言变化后统一重放：静态文案 + 由代码拼出来的动态文案。
    private void ApplyLanguageTexts()
    {
        foreach (Action apply in localizedText) apply();
        UpdateDocumentTitle();
        UpdateMode();
        ApplyHotkeys();
        RefreshLibraryList();
        UpdateLibraryStatus();
        UpdateAlert();
    }

    // ---------- 无边框浮窗 ----------
    private void SetupFloatingWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });
        SourceInitialized += (_, _) => TryRoundWindowCorners();
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Win11 原生圆角（DWMWA_WINDOW_CORNER_PREFERENCE = 33，DWMWCP_ROUND = 2）；
    // 其他系统或旧 dwmapi 直接忽略，不影响窗口可用性。
    private void TryRoundWindowCorners()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            int round = 2;
            DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
