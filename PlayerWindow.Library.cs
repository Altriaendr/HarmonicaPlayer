using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HarmonicaPlayer;

// 曲谱库：把常弹的 TXT 曲谱列成一张表，每首可以单独绑定一个全局快捷键。
// 只组织“文件 + 快捷键”，加载与演奏复用既有 ScoreDocumentReader / ApplyDocument / Begin。
public sealed partial class PlayerWindow
{
    private readonly ListBox libraryList = new() { MinHeight = 140 };
    private readonly TextBlock libraryStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button addLibrary = new() { Content = "添加曲谱…" };
    private readonly Button removeLibrary = new() { Content = "移除" };
    private readonly Button bindLibrary = new() { Content = "绑定快捷键…" };
    private readonly Button clearLibraryHotkey = new() { Content = "清除绑定" };
    private readonly Button moveLibraryUp = new() { Content = "上移" };
    private readonly Button moveLibraryDown = new() { Content = "下移" };
    private readonly Button loadLibrary = new() { Content = "加载到编辑器" };
    private readonly Button playLibrary = new() { Content = "加载并演奏" };
    private readonly CheckBox libraryPlays = new() { Content = "按快捷键立即演奏（不勾选则只加载曲谱）" };

    // 曲谱库选项卡：列表占满整列剩余高度，一屏能看全并直接绑快捷键，不必再手动滚动找曲目。
    private Border BuildLibraryPanel()
    {
        libraryList.Background = UiTheme.Brush(UiTheme.Panel);
        libraryList.BorderBrush = UiTheme.Brush(UiTheme.ControlBorder);
        libraryList.BorderThickness = new Thickness(1);
        libraryList.Padding = new Thickness(4);
        ScrollViewer.SetVerticalScrollBarVisibility(libraryList, ScrollBarVisibility.Auto);
        libraryList.SelectionChanged += (_, _) => { ApplyLibrarySelection(); UpdateLibraryStatus(); };
        addLibrary.Click += async (_, _) => await AddLibraryEntriesAsync();
        removeLibrary.Click += (_, _) => RemoveSelectedLibraryEntry();
        bindLibrary.Click += (_, _) => BindSelectedLibraryHotkey();
        clearLibraryHotkey.Click += (_, _) => SetSelectedLibraryHotkey(null);
        moveLibraryUp.Click += (_, _) => MoveSelectedLibraryEntry(-1);
        moveLibraryDown.Click += (_, _) => MoveSelectedLibraryEntry(1);
        loadLibrary.Click += async (_, _) => await LibraryButtonAsync(SelectedLibraryIndex, play: false);
        playLibrary.Click += async (_, _) => await LibraryButtonAsync(SelectedLibraryIndex, play: true);
        libraryPlays.Checked += (_, _) => SetLibraryPlays(true);
        libraryPlays.Unchecked += (_, _) => SetLibraryPlays(false);
        addLibrary.Style = Ui("BtnGhost");
        loadLibrary.Style = Ui("BtnSecondary");
        playLibrary.Style = Ui("BtnPrimary");
        addLibrary.Padding = loadLibrary.Padding = playLibrary.Padding = new Thickness(14, 8, 14, 8);
        foreach (var button in new[] { removeLibrary, bindLibrary, clearLibraryHotkey, moveLibraryUp, moveLibraryDown })
            button.Style = Ui("BtnSmall");

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 说明
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // 列表（占满）
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 曲谱操作
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 绑定与排序
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 快捷键行为
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 状态
        var hint = LHint("添加常弹的 TXT 曲谱，再为每首绑定一个全局快捷键：在游戏里按一下就会加载并演奏。曲谱快捷键与开始/停止键共用同一套注册，冲突时会明确提示。");
        Grid.SetRow(hint, 0); body.Children.Add(hint);
        Grid.SetRow(libraryList, 1); body.Children.Add(libraryList);
        var fileRow = Row(addLibrary, loadLibrary, playLibrary);
        Grid.SetRow(fileRow, 2); body.Children.Add(fileRow);
        var bindRow = Row(bindLibrary, clearLibraryHotkey, removeLibrary, moveLibraryUp, moveLibraryDown);
        Grid.SetRow(bindRow, 3); body.Children.Add(bindRow);
        var playsRow = Row(libraryPlays);
        Grid.SetRow(playsRow, 4); body.Children.Add(playsRow);
        Grid.SetRow(libraryStatus, 5); body.Children.Add(libraryStatus);
        return CardPanel("曲谱库：每首曲子可以绑一个自己的快捷键", body);
    }
    // 设置读取完成后调用：同步复选框并显示列表。
    private void InitializeLibrary()
    {
        libraryPlays.IsChecked = settings.LibraryHotkeyPlays;
        RefreshLibraryList();
    }

    private int SelectedLibraryIndex => libraryList.SelectedIndex;

    // 设置变化后重建列表：曲名、快捷键、状态，并标出当前编辑器里加载的曲谱。
    private void RefreshLibraryList()
    {
        int selected = libraryList.SelectedIndex;
        libraryList.Items.Clear();
        var entries = settings.Library;
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            bool current = ScoreLibrary.IndexOfPath(entries, documentPath) == i;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var (state, color) = LibraryState(i, entry);
            var title = new TextBlock
            {
                Text = entry.DisplayName + (current ? Loc.T("（当前）") : ""),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = UiTheme.Brush(current ? UiTheme.Accent : UiTheme.PrimaryText)
            };
            var hotkey = new TextBlock
            {
                Text = entry.Hotkey?.Label ?? Loc.T("未绑定"),
                Margin = new Thickness(12, 0, 12, 0),
                FontFamily = UiTheme.MonoFont,
                Foreground = UiTheme.Brush(entry.Hotkey == null ? UiTheme.MutedText : UiTheme.PrimaryText)
            };
            var stateText = new TextBlock { Text = state, Foreground = UiTheme.Brush(color) };
            Grid.SetColumn(title, 0); Grid.SetColumn(hotkey, 1); Grid.SetColumn(stateText, 2);
            row.Children.Add(title); row.Children.Add(hotkey); row.Children.Add(stateText);
            libraryList.Items.Add(new ListBoxItem { Content = row, ToolTip = entry.Path, Style = Ui("LibraryItem") });
        }
        if (entries.Length == 0)
            libraryList.Items.Add(new ListBoxItem
            {
                IsEnabled = false,
                Style = Ui("LibraryItem"),
                Content = new TextBlock
                {
                    Text = Loc.T("还没有曲谱：点“添加曲谱…”把常弹的 TXT 加进来，再为它绑定快捷键。"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = UiTheme.Brush(UiTheme.MutedText)
                }
            });
        libraryList.SelectedIndex = entries.Length == 0 ? -1 : Math.Min(selected < 0 ? 0 : selected, entries.Length - 1);
        UpdateLibraryStatus();
    }
    // 每行的状态：文件是否存在、快捷键是否真的注册成功。
    private (string Text, string Color) LibraryState(int index, ScoreLibraryEntry entry)
    {
        if (!File.Exists(entry.Path)) return (Loc.T("文件不存在"), UiTheme.Danger);
        if (entry.Hotkey == null) return (Loc.T("未绑定快捷键"), UiTheme.MutedText);
        int id = ScoreLibrary.HotkeyId(index);
        string result = hotkeys?.LibraryResult(id) ?? Loc.T("待注册");
        return (result, hotkeys?.LibraryReady(id) == true ? UiTheme.Success : UiTheme.Warning);
    }

    private void UpdateLibraryStatus()
    {
        var entries = settings.Library;
        int bound = entries.Count(entry => entry.Hotkey != null);
        int missing = entries.Count(entry => !File.Exists(entry.Path));
        var text = entries.Length == 0
            ? Loc.T("曲谱库为空：添加曲谱后可以为每首绑定一个全局快捷键。")
            : Loc.F("共{0}首，已绑定{1}个曲谱快捷键", entries.Length, bound) +
              (missing != 0 ? Loc.F("，{0}首文件不存在（请重新添加或移除）", missing) : "") +
              Loc.T("。快捷键被其他程序占用时只影响这一首，其他曲谱不受影响。");
        int index = SelectedLibraryIndex;
        if (index >= 0 && index < entries.Length) text += Loc.T("\n当前选中：") + entries[index].Path;
        if (libraryStatus.Text != text) libraryStatus.Text = text;
    }

    private void ApplyLibrarySelection() => ApplyLibrarySelection(uiBusy);

    private void ApplyLibraryBusy(bool busy) => ApplyLibrarySelection(busy);

    private void ApplyLibrarySelection(bool busy)
    {
        int index = SelectedLibraryIndex;
        bool has = index >= 0 && index < settings.Library.Length;
        addLibrary.IsEnabled = !busy;
        libraryList.IsEnabled = libraryPlays.IsEnabled = !busy;
        removeLibrary.IsEnabled = bindLibrary.IsEnabled = clearLibraryHotkey.IsEnabled = !busy && has;
        loadLibrary.IsEnabled = playLibrary.IsEnabled = !busy && has;
        moveLibraryUp.IsEnabled = !busy && has && index > 0;
        moveLibraryDown.IsEnabled = !busy && has && index < settings.Library.Length - 1;
    }

    // 默认建议值：优先未被占用的 F1～F11（F12 与 Win 组合被系统保留，演奏键由规则拒绝）。
    private HotkeyBinding SuggestLibraryHotkey()
    {
        for (uint key = 0x70; key <= 0x7A; key++)
        {
            var candidate = new HotkeyBinding(key);
            if (!ScoreLibrary.HotkeyTaken(settings.Library, candidate, settings.Start, settings.Stop)) return candidate;
        }
        return HotkeyBinding.DefaultStart;
    }
    private void SetLibraryPlays(bool plays)
    {
        if (settings.LibraryHotkeyPlays == plays) return;
        settings = settings with { LibraryHotkeyPlays = plays };
        QueueSave();
        status.Text = plays
            ? Loc.T("曲谱库快捷键：按下后加载并演奏（倒计时3秒，随时可用停止键取消）。")
            : Loc.T("曲谱库快捷键：只把曲谱加载到编辑器，不自动演奏。");
    }

    // 演奏/起奏期间禁止改动曲谱库：此时不能重新注册全局快捷键，改动只会保存成“未生效”。
    private bool LibraryEditingBlocked()
    {
        if (cancellation == null && !beginning && !closing) return false;
        status.Text = Loc.T("正在演奏，曲谱库暂不可修改；请先按停止键，再添加、移除或改绑定。");
        return true;
    }

    // 一次可以选择多个 TXT；已在列表里的路径不会重复添加。
    private async Task AddLibraryEntriesAsync()
    {
        if (uiBusy || documentBusy || LibraryEditingBlocked()) return;
        string[] paths;
        try { paths = scoreDialogs.OpenLibrary(this); }
        catch (Exception e) { ReportIssue(Loc.T("无法打开文件选择框：") + e.Message); return; }
        if (paths.Length == 0) return;
        var entries = settings.Library.ToList();
        int added = 0, skipped = 0;
        foreach (string path in paths)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception) { skipped++; continue; }
            if (entries.Count >= ScoreLibrary.MaxEntries || ScoreLibrary.IndexOfPath(entries, full) >= 0) { skipped++; continue; }
            entries.Add(new ScoreLibraryEntry { Path = full });
            added++;
        }
        if (added == 0)
        {
            status.Text = Loc.F("没有新增曲谱：所选文件已在列表中，或曲谱库已达上限（{0}首）。", ScoreLibrary.MaxEntries);
            return;
        }
        settings = settings with { Library = entries.ToArray() };
        ApplyLibraryChange(entries.Count - 1);
        status.Text = Loc.F("已添加{0}首曲谱", added) + (skipped != 0 ? Loc.F("，跳过{1}个（重复或超过上限）", skipped) : "") +
            Loc.T("。选中一行后点“绑定快捷键…”设置按键。");
    }

    // 只从曲谱库移除；当前编辑器内容与磁盘文件都不改动。
    private void RemoveSelectedLibraryEntry()
    {
        if (uiBusy || documentBusy || LibraryEditingBlocked()) return;
        int index = SelectedLibraryIndex;
        if (index < 0 || index >= settings.Library.Length) return;
        var entry = settings.Library[index];
        var entries = settings.Library.Where((_, i) => i != index).ToArray();
        settings = settings with { Library = entries };
        ApplyLibraryChange(entries.Length == 0 ? -1 : Math.Min(index, entries.Length - 1));
        status.Text = Loc.F("已从曲谱库移除“{0}”（曲谱文件与编辑器内容未改动）。", entry.DisplayName);
    }

    private void MoveSelectedLibraryEntry(int delta)
    {
        if (uiBusy || documentBusy || LibraryEditingBlocked()) return;
        int index = SelectedLibraryIndex, target = index + delta;
        var entries = settings.Library;
        if (index < 0 || index >= entries.Length || target < 0 || target >= entries.Length) return;
        var list = entries.ToList();
        (list[index], list[target]) = (list[target], list[index]);
        settings = settings with { Library = list.ToArray() };
        ApplyLibraryChange(target);
    }
    private void BindSelectedLibraryHotkey()
    {
        if (uiBusy || documentBusy || LibraryEditingBlocked()) return;
        int index = SelectedLibraryIndex;
        if (index < 0 || index >= settings.Library.Length) return;
        var entry = settings.Library[index];
        var dialog = new LibraryHotkeyDialog(entry.DisplayName, entry.Hotkey ?? SuggestLibraryHotkey(),
            candidate => ScoreLibrary.Conflict(settings.Library, index, candidate, settings.Start, settings.Stop))
        { Owner = this };
        if (dialog.ShowDialog() != true) return;
        SetSelectedLibraryHotkey(dialog.Hotkey);
    }

    // hotkey 为 null 表示清除绑定；冲突在写入设置之前就拦下，避免保存出无法注册的配置。
    private void SetSelectedLibraryHotkey(HotkeyBinding? hotkey)
    {
        if (uiBusy || documentBusy || LibraryEditingBlocked()) return;
        int index = SelectedLibraryIndex;
        if (index < 0 || index >= settings.Library.Length) return;
        if (hotkey != null && ScoreLibrary.Conflict(settings.Library, index, hotkey, settings.Start, settings.Stop) is string reason)
        {
            ReportIssue(reason);
            return;
        }
        var entry = settings.Library[index] with { Hotkey = hotkey };
        var entries = settings.Library.ToArray();
        entries[index] = entry;
        settings = settings with { Library = entries };
        ApplyLibraryChange(index);
        status.Text = hotkey == null
            ? Loc.F("已清除“{0}”的曲谱快捷键。", entry.DisplayName)
            : Loc.F("“{0}”已绑定 {1}：", entry.DisplayName, hotkey.Label) + (hotkeys?.LibraryResult(ScoreLibrary.HotkeyId(index)) ?? Loc.T("待注册"));
    }

    // 曲谱库变化后的统一收尾：刷新列表 → 重新注册全部快捷键 → 保存（保存有 generation 防护，旧写入不会覆盖新值）。
    private void ApplyLibraryChange(int select)
    {
        RefreshLibraryList();
        libraryList.SelectedIndex = select;
        ApplyHotkeys();
        _ = SaveCurrentSettingsAsync(true);
    }
    // 按钮路径：用户就在窗口里，允许“保存/放弃未保存修改”的确认流程。
    private async Task LibraryButtonAsync(int index, bool play)
    {
        if (uiBusy || cancellation != null || beginning || editingHotkeys || closing || documentBusy) return;
        bool ready = false;
        documentBusy = true; SetBusy(true);
        try { ready = await PrepareLibraryEntryAsync(index, interactive: true); }
        catch (Exception e) { ReportIssue(Loc.T("加载曲谱库曲谱失败：") + e.Message); }
        finally { documentBusy = false; SetBusy(false); }
        if (ready && play) await Begin();
    }

    // 快捷键路径：演奏中不应弹出对话框；有未保存修改时明确提示并放弃本次切换。
    private async Task LibraryActivateAsync(int index, uint triggerKey, bool play)
    {
        if (cancellation != null || beginning || editingHotkeys || closing || documentBusy || uiBusy)
        {
            status.Text = Loc.T("正在演奏或读写曲谱，本次曲谱库快捷键未生效。");
            return;
        }
        bool ready;
        try { ready = await PrepareLibraryEntryAsync(index, interactive: false); }
        catch (Exception e) { ReportIssue(Loc.T("加载曲谱库曲谱失败：") + e.Message); return; }
        if (ready && play) await Begin(triggerKey);
    }

    // 把曲谱读进编辑器；返回 false 表示文件缺失、用户取消或谱面有错，不应继续演奏。
    private async Task<bool> PrepareLibraryEntryAsync(int index, bool interactive)
    {
        if (index < 0 || index >= settings.Library.Length) return false;
        var entry = settings.Library[index];
        if (!File.Exists(entry.Path))
        {
            ReportIssue(Loc.F("曲谱库“{0}”的文件不存在：{1}\n请重新添加这首曲谱，或从曲谱库移除。", entry.DisplayName, entry.Path));
            return false;
        }
        // 已经加载同一文件且没有未保存修改：直接演奏，不重复读盘。
        if (string.Equals(documentPath, entry.Path, StringComparison.OrdinalIgnoreCase) && !DocumentDirty) return true;
        if (!interactive && DocumentDirty)
        {
            ReportIssue(Loc.F("当前曲谱有未保存的修改，已取消曲谱库切换（“{0}”）。请先在播放器中保存或新建，再使用曲谱库快捷键。", entry.DisplayName));
            return false;
        }
        int fallback = int.TryParse(bpm.Text, out int value) && value is >= 20 and <= 300 ? value : settings.Bpm;
        var result = await ScoreDocumentReader.ReadAsync(entry.Path, fallback);
        if (!await ConfirmUnsavedAsync()) return false;
        // 保存当前文档可能已更新所选文件，因此重新读取一次。
        result = await ScoreDocumentReader.ReadAsync(entry.Path, fallback);
        ApplyDocument(result.Document);
        documentWarning.Text = string.Join("\n", result.Warnings);
        status.Text = scoreIssue == null
            ? Loc.F("已从曲谱库加载“{0}”。", result.Document.Title)
            : Loc.T("已加载曲谱，请先修正顶部提示的错误再演奏。");
        return scoreIssue == null;
    }

    // 已绑定快捷键的曲谱：编号与列表顺序一致，某一首注册失败只影响这一首。
    private (int Id, HotkeyBinding Binding)[] LibraryHotkeyBindings()
    {
        var bindings = new List<(int Id, HotkeyBinding Binding)>();
        for (int i = 0; i < settings.Library.Length; i++)
            if (settings.Library[i].Hotkey is HotkeyBinding hotkey) bindings.Add((ScoreLibrary.HotkeyId(i), hotkey));
        return bindings.ToArray();
    }

    // 顶部快捷键状态栏里的曲谱库摘要；未生效的点名到具体曲谱。
    private string LibraryHotkeySummary()
    {
        int bound = settings.Library.Count(entry => entry.Hotkey != null);
        if (bound == 0) return Loc.T("曲谱库尚未绑定快捷键。");
        var failed = new List<string>();
        for (int i = 0; i < settings.Library.Length; i++)
        {
            var entry = settings.Library[i];
            if (entry.Hotkey is HotkeyBinding hotkey && hotkeys?.LibraryReady(ScoreLibrary.HotkeyId(i)) != true)
                failed.Add(Loc.F("“{0}”{1}", entry.DisplayName, hotkey.Label));
        }
        return failed.Count == 0
            ? Loc.F("曲谱库 {0} 个快捷键已就绪。", bound)
            : Loc.F("曲谱库快捷键未生效：{0}。", string.Join(Loc.T("、"), failed));
    }
}

// 曲谱库单键绑定对话框：复用开始/停止键的录入控件，只绑定一个键。
// 与 HotkeyDialog 一致：打开期间由调用方暂停全局快捷键，关闭后再统一注册。
public sealed class LibraryHotkeyDialog : Window
{
    // Hotkey 为 null 表示清除绑定。
    public HotkeyBinding? Hotkey { get; private set; }
    public LibraryHotkeyDialog(string scoreName, HotkeyBinding suggested, Func<HotkeyBinding, string?> conflict)
    {
        Title = Loc.T("为曲谱绑定快捷键");
        Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        UiTheme.Apply(this);
        var panel = new StackPanel { Margin = new Thickness(18) };
        Content = panel;
        panel.Children.Add(new TextBlock
        {
            Text = Loc.F("曲谱：{0}\n绑定后无论在哪个窗口，按这个键都会加载并演奏这首曲子。它与开始/停止键共用同一套注册，被其他程序占用时会明确提示。", scoreName),
            TextWrapping = TextWrapping.Wrap
        });
        var editor = new HotkeyDialog.BindingEditor(Loc.T("曲谱快捷键"), suggested);
        panel.Children.Add(editor);
        panel.Children.Add(new TextBlock
        {
            Text = Loc.T("停止键始终优先保留；演奏键 Z/X/C/V/B/N/M/逗号与 F12、Win 组合不能作为曲谱快捷键。"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6),
            Foreground = UiTheme.Brush(UiTheme.MutedText)
        });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.Brush(UiTheme.Danger) };
        panel.Children.Add(error);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var clear = new Button { Content = Loc.T("清除绑定"), Style = UiTheme.Style(this, "BtnGhost") };
        var cancel = new Button { Content = Loc.T("取消"), Style = UiTheme.Style(this, "BtnGhost"), IsCancel = true };
        var confirm = new Button { Content = Loc.T("确定"), Style = UiTheme.Style(this, "BtnPrimary"), IsDefault = true };
        clear.Click += (_, _) => { Hotkey = null; DialogResult = true; };
        confirm.Click += (_, _) =>
        {
            var value = editor.Value;
            if (conflict(value) is string reason) { error.Text = reason; return; }
            Hotkey = value;
            DialogResult = true;
        };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(clear); buttons.Children.Add(cancel); buttons.Children.Add(confirm);
        panel.Children.Add(buttons);
    }
}

