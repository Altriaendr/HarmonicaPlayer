using System.Windows;
using Microsoft.Win32;

namespace HarmonicaPlayer;

// Injectable only for UI tests; production uses standard Windows dialogs.
public interface IScoreDialogs
{
    string? Open(Window owner);
    // 曲谱库支持一次添加多首；默认实现供测试替身直接复用。
    string[] OpenLibrary(Window owner) => new ScoreDialogs().OpenLibrary(owner);
    string? OpenMidi(Window owner) => new ScoreDialogs().OpenMidi(owner);
    ScoreDocument? ConvertMidi(Window owner, MidiImportFile file)
    {
        var dialog = new MidiImportDialog(file) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Document : null;
    }
    void PlaybackIssue(Window owner, string message) => MessageBox.Show(owner, message, Loc.T("演奏已停止"), MessageBoxButton.OK, MessageBoxImage.Warning);
    string? Save(Window owner, string suggestedName);
    MessageBoxResult Unsaved(Window owner);
    string? SaveMidi(Window owner, string suggestedName) => new ScoreDialogs().SaveMidi(owner, suggestedName);
}

public sealed class ScoreDialogs : IScoreDialogs
{
    public string? Open(Window owner)
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("TXT 简谱|*.txt") };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    // 曲谱库：可多选，一次性把常弹的谱子加进列表。
    public string[] OpenLibrary(Window owner)
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("TXT 简谱|*.txt"), Multiselect = true, CheckFileExists = true };
        return dialog.ShowDialog(owner) == true ? dialog.FileNames : Array.Empty<string>();
    }
    public string? OpenMidi(Window owner)
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("标准 MIDI 文件|*.mid;*.midi"), CheckFileExists = true };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    public string? Save(Window owner, string suggestedName)
    {
        var dialog = new SaveFileDialog { Filter = Loc.T("TXT 简谱|*.txt"), DefaultExt = ".txt", AddExtension = true,
            OverwritePrompt = true, FileName = suggestedName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    public string? SaveMidi(Window owner, string suggestedName)
    {
        var dialog = new SaveFileDialog { Filter = Loc.T("标准 MIDI 文件|*.mid"), DefaultExt = ".mid", AddExtension = true,
            OverwritePrompt = true, FileName = suggestedName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    public MessageBoxResult Unsaved(Window owner) => MessageBox.Show(owner,
        Loc.T("曲谱、曲名、BPM或音符间隔已修改，是否保存？\n“否”放弃本次修改，“取消”返回编辑。"),
        Loc.T("曲谱尚未保存"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
}
