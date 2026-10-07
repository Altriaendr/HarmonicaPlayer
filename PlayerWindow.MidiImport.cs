using System.Windows;
using System.Windows.Controls;

namespace HarmonicaPlayer;

public sealed partial class PlayerWindow
{
    private readonly Button importMidi = new() { Content = "导入单旋律 MIDI", Margin = new Thickness(5), Padding = new Thickness(10, 6, 10, 6) };
    private Task ImportMidiAsync() => DocumentOperation(async () =>
    {
        string? path = scoreDialogs.OpenMidi(this);
        if (path == null) return false;
        // MIDI decoding is bounded but may still be expensive; keep the WPF dispatcher free.
        var file = await Task.Run(() => MidiImporter.ReadAsync(path));
        if (scoreDialogs.ConvertMidi(this, file) is not ScoreDocument document) return false;
        if (!await ConfirmUnsavedAsync()) return false;
        ApplyDocument(document);
        // This is a new, unsaved TXT document, not the source .mid file.
        cleanDocument = ("", "", "", ""); UpdateDocumentTitle();
        documentWarning.Text = $"由 MIDI“{System.IO.Path.GetFileName(path)}”转换，原 MIDI 未修改。请先试听检查，再保存 TXT 或游戏演奏。";
        status.Text = "单旋律 MIDI 已导入为可编辑谱面，尚未保存 TXT。";
        return true;
    });
}

public sealed class MidiImportDialog : Window
{
    public ScoreDocument? Document { get; private set; }
    private readonly MidiImportFile file;
    private readonly ComboBox part = new() { MinWidth = 380, DisplayMemberPath = nameof(MidiImportPart.Label) };
    private readonly TextBox tempo = new() { Width = 65 };
    private readonly TextBox gap = new() { Width = 65, Text = "10" };
    private readonly TextBox transpose = new() { Width = 65, Text = "0" };
    private readonly TextBlock summary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock recommendation = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox result = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220 };
    private readonly Button accept = new() { Content = Loc.T("导入到编辑器"), Padding = new Thickness(12, 6, 12, 6), IsDefault = true };
    private MidiTransposeSuggestion? suggestion;
    private MidiConversion? conversion;

    public MidiImportDialog(MidiImportFile file)
    {
        this.file = file;
        Title = Loc.T("导入单旋律 MIDI"); Width = 720; Height = 640; MinWidth = 560; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // 与主窗口共用同一套主题：深色下对话框不能还是白底。
        UiTheme.Apply(this);
        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = Loc.T("取消"), Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6), IsCancel = true };
        buttons.Children.Add(accept); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var panel = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(new TextBlock { Text = Loc.T("选择单旋律轨道／通道。严格单音，不丢音、不截短、不自动移调。"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(part); panel.Children.Add(summary);
        var row = new WrapPanel { Margin = new Thickness(0, 12, 0, 12) };
        void Field(string label, TextBox box) { row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) }); row.Children.Add(box); }
        Field(Loc.T("固定 BPM"), tempo); Field(Loc.T("gap（ms）"), gap); Field(Loc.T("整体移调（半音）"), transpose);
        panel.Children.Add(row);
        panel.Children.Add(new TextBlock { Text = Loc.T("按原速度事件计算实际时间，转换为固定 BPM 下的自定义时值。gap 是目标谱面的留白，不从 MIDI 猜测；无法保留原起音、音尾与总时长时阻止导入。"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(recommendation);
        var apply = new Button { Content = Loc.T("使用建议移调"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(apply); panel.Children.Add(result); Content = root;
        tempo.Text = file.SuggestedBpm.ToString();
        part.ItemsSource = file.Parts;
        part.SelectionChanged += (_, _) =>
        {
            suggestion = part.SelectedItem is MidiImportPart selected ? MidiImporter.SuggestTranspose(selected) : null;
            Refresh();
        };
        foreach (var box in new[] { tempo, gap, transpose }) box.TextChanged += (_, _) => Refresh();
        apply.Click += (_, _) => { if (suggestion != null) transpose.Text = suggestion.Semitones.ToString(); };
        accept.Click += (_, _) =>
        {
            Refresh();
            if (conversion?.Success == true) { Document = conversion.Document; DialogResult = true; }
        };
        part.SelectedIndex = 0;
    }
    private void Refresh()
    {
        accept.IsEnabled = false; conversion = null; Document = null;
        if (part.SelectedItem is not MidiImportPart selected) return;
        int low = selected.Notes.Min(n => n.Pitch), high = selected.Notes.Max(n => n.Pitch);
        summary.Text = Loc.F("{0}\n音域：{1}～{2}；", selected.Label, HarmonicaPitchMap.Name(low), HarmonicaPitchMap.Name(high)) +
            Loc.T("速度：") + (file.Tempos.Count > 1
                ? Loc.F("{0} 段速度", file.Tempos.Count)
                : Loc.F("{0} BPM", (60000000.0 / file.Tempos[0].Microseconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + "。";
        if (suggestion != null)
            recommendation.Text = Loc.F("建议整体移调 {0} 半音：原始音域覆盖率 {1}/{2}（{3}%）→ 建议移调后 {4}/{5}（{6}%）。仅供选择，未自动应用。",
                suggestion.Semitones.ToString("+0;-0;0"), suggestion.OriginalPlayable, suggestion.Total,
                (100.0 * suggestion.OriginalPlayable / suggestion.Total).ToString("0.##"), suggestion.SuggestedPlayable, suggestion.Total,
                (100.0 * suggestion.SuggestedPlayable / suggestion.Total).ToString("0.##"));
        try
        {
            int targetTempo = PlaybackValidation.ParseBpm(tempo.Text), targetGap = PlaybackValidation.ParseGap(gap.Text);
            if (!int.TryParse(transpose.Text, out int shift)) throw new FormatException(Loc.T("整体移调必须是整数半音。"));
            conversion = MidiImporter.Convert(file, selected, targetTempo, targetGap, shift);
            int playable = selected.Notes.Count(n => HarmonicaPitchMap.Contains(n.Pitch + shift));
            summary.Text += Loc.F("\n当前移调 {0} 半音：{1}/{2} 音在支持范围内。",
                shift.ToString("+0;-0;0"), playable, selected.Notes.Count);
            if (conversion.Success)
            {
                string body = conversion.Document!.ScoreText;
                result.Text = Loc.T("检查通过。音高、实际起音、发声结束和所选轨道总时长保留；未量化、未调整重叠。\n\n") +
                    body[..Math.Min(body.Length, 30000)] + (body.Length > 30000 ? Loc.T("\n（预览截断，导入时保留完整谱面）") : "");
                accept.IsEnabled = true;
            }
            else
                result.Text = Loc.F("无法导入：共 {0} 项问题，未修改原 MIDI。\n", conversion.Problems.Count) +
                    string.Join("\n\n", conversion.Problems.Take(30)) + (conversion.Problems.Count > 30 ? Loc.T("\n仅显示前 30 项，请先在原 MIDI 修正。") : "");
        }
        catch (FormatException e) { result.Text = e.Message; }
    }
}

