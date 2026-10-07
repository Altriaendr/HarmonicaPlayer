using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HarmonicaPlayer;

// Windows-only regression test using actual WPF windows. Only dry/log playback;
// no note input. Uses a fresh temporary settings file for each run.
public static class WindowsSmokeTests
{
    [STAThread]
    public static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0;
        app.Startup += async (_, _) =>
        {
            try
            {
                foreach (string test in new[] { "idle", "pending-save", "countdown", "playback", "error-recovery" })
                {
                    string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HarmonicaPlayerSmoke-" + Guid.NewGuid().ToString("N") + ".json");
                    PlayerWindow? window = null;
                    try
                    {
                        window = new PlayerWindow(path, new TestScoreDialogs(), administrator: false);
                        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.Closed += (_, _) => closed.TrySetResult();
                        window.Show();
                        await WaitUntil(() => window.IsLoaded, "window loaded");
                        var defaultMode = Field<CheckBox>(window, "dry");
                        if (defaultMode.IsChecked != false || !Field<Button>(window, "start").Content.ToString()!.StartsWith("开始演奏"))
                            throw new Exception("Fresh startup must default to game performance.");
                        if (!Field<TextBlock>(window, "alert").Text.StartsWith("权限提醒"))
                            throw new Exception("Non-admin warning must be first.");
                        var nativeController = Field<HotkeyController>(window, "hotkeys");
                        if (nativeController.StopReady && !Field<Button>(window, "start").IsEnabled)
                            throw new Exception("Non-admin warning blocked a valid score.");
                        // Every scenario explicitly opts into dry mode before invoking Begin.
                        defaultMode.IsChecked = true;
                        if (test == "error-recovery")
                        {
                            var editor = Descendants(window).OfType<TextBox>().Single(t => t.AcceptsReturn && !t.IsReadOnly);
                            var startButton = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("开始") == true);
                            var locate = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString() == "定位曲谱错误");
                            editor.Text = "1\n8";
                            await WaitUntil(() => !startButton.IsEnabled && locate.IsEnabled, "invalid score displayed");
                            if (startButton.IsEnabled || !locate.IsEnabled) throw new Exception("Invalid score did not disable start/enable location.");
                            locate.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            if (editor.SelectionStart != 2 || editor.SelectionLength != 1) throw new Exception("Error location is incorrect.");
                            var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                            await (Task)typeof(PlayerWindow).GetMethod("Begin", bindingFlags)!.Invoke(window, new object[] { (uint)0 })!;
                            editor.Text = "1 2";
                            await WaitUntil(() => startButton.IsEnabled && !locate.IsEnabled, "score recovered");
                            if (!startButton.IsEnabled || locate.IsEnabled) throw new Exception("Corrected score did not restore controls.");
                            if (typeof(PlayerWindow).GetField("scoreIssue", bindingFlags)!.GetValue(window) != null ||
                                typeof(PlayerWindow).GetField("operationIssue", bindingFlags)!.GetValue(window) != null)
                                throw new Exception("Corrected score left a stale error.");
                            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                            var controller = (HotkeyController)typeof(PlayerWindow).GetField("hotkeys", flags)!.GetValue(window)!;
                            controller.Suspend();
                            var dryBox = Descendants(window).OfType<CheckBox>().Single(c => c.Content?.ToString()?.StartsWith("仅测试") == true);
                            dryBox.IsChecked = false;
                            if (startButton.IsEnabled) throw new Exception("Real playback allowed without stop hotkey.");
                            dryBox.IsChecked = true;
                            if (!startButton.IsEnabled) throw new Exception("Dry run unnecessarily blocked by stop hotkey.");
                        }
                        if (test == "pending-save")
                        {
                            // A valid numeric edit queues the real debounced settings saver.
                            var number = Descendants(window).OfType<TextBox>().First(t => t.Text == "120" && !t.IsReadOnly);
                            number.Text = "110";
                        }
                        if (test is "countdown" or "playback")
                        {
                            var dry = Descendants(window).OfType<CheckBox>().Single(c => c.Content?.ToString()?.StartsWith("仅测试") == true);
                            dry.IsChecked = true;
                            if (!Field<Button>(window, "start").Content.ToString()!.StartsWith("开始测试")) throw new Exception("Test label not updated.");
                            var start = Descendants(window).OfType<Button>().Single(b => b.Content?.ToString()?.StartsWith("开始") == true);
                            start.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            await WaitUntil(() => test == "countdown"
                                ? Field<TextBlock>(window, "status").Text.Contains("秒后开始")
                                : Field<TextBox>(window, "log").Text.Length > 0, test + " actually started");
                        }
                        var clock = Stopwatch.StartNew();
                        window.Close();
                        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        Console.WriteLine($"PASS {test}: close {clock.ElapsedMilliseconds}ms");
                    }
                    finally
                    {
                        if (window?.IsVisible == true) window.Close();
                        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    }
                }
                await MidiImportWindowTests();
                await SettingsGenerationTests();
                await PlaybackRestartTests();
                await DocumentWindowTests();
                await AudioWindowTests();
                await LibraryWindowTests();
                await ShellWindowTests();
            }
            catch (Exception e) { failures++; Console.Error.WriteLine(e); }
            finally { app.Shutdown(); }
        };
        app.Run();
        return failures == 0 ? 0 : 1;
    }
    private static T Field<T>(PlayerWindow window, string name) =>
        (T)typeof(PlayerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;

    // 诊断用：从控件向上打印可视父链的尺寸，便于定位“高度被压成 0”的位置。
    private static string Chain(DependencyObject node)
    {
        var parts = new List<string>();
        for (DependencyObject? current = node; current != null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
            parts.Add(current is FrameworkElement element
                ? $"{element.GetType().Name}({element.ActualWidth:0}x{element.ActualHeight:0})"
                : current.GetType().Name);
        return string.Join(" < ", parts);
    }
    private static Task Invoke(PlayerWindow window, string name, params object[] args) =>
        (Task)typeof(PlayerWindow).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, args)!;
    private static async Task WaitUntil(Func<bool> predicate, string name)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(8)) throw new Exception("Timed out: " + name);
            await Task.Delay(20);
        }
    }
    private static async Task CloseWindow(PlayerWindow window)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Close(); await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // 外壳验证：4:3 无边框浮窗、矢量图标、选项卡布局（曲谱库占满整列高度）与中英切换。
    private static async Task ShellWindowTests()
    {
        if (!UiIcon.Validate()) throw new Exception("Icon geometry is invalid");
        string path = Path.Combine(Path.GetTempPath(), "HarpKitShell-" + Guid.NewGuid() + ".json");
        var window = new PlayerWindow(path, new TestScoreDialogs(), administrator: false);
        try
        {
            window.Show();
            await WaitUntil(() => window.IsLoaded, "shell window loaded");
            if (window.WindowStyle != WindowStyle.None) throw new Exception("Floating window must be frameless");
            if (Math.Abs(window.Width * 3 - window.Height * 4) > 0.5) throw new Exception("Default window is not 4:3");
            if (window.Icon is null) throw new Exception("Window icon missing");
            var tabs = Descendants(window).OfType<TabControl>().Single();
            if (tabs.Items.Count != 3) throw new Exception("Shell must expose three tabs");
            if (((TabItem)tabs.Items[0]!).Header is not TextBlock firstHeader || firstHeader.Text != "曲谱库")
                throw new Exception("Score library must be the first tab");
            // 曲谱库列表占满整列高度：不再出现“显示不全还得手动滚动才能绑快捷键”。
            var list = Field<ListBox>(window, "libraryList");
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            list.UpdateLayout();
            if (list.ActualHeight < 200)
                throw new Exception("Library list does not fill the column: " + list.ActualHeight +
                    " | chain=" + Chain(list) + " | editor=" + Chain(Field<TextBox>(window, "score")));

            var language = Field<ComboBox>(window, "languageChoice");
            if (language.SelectedIndex != 0 || Field<Button>(window, "saveScore").Content.ToString() != "保存 TXT")
                throw new Exception("Default language must be Chinese");
            language.SelectedIndex = 1;
            await WaitUntil(() => Field<Button>(window, "saveScore").Content.ToString() == "Save TXT", "English applied");
            if (((TabItem)tabs.Items[0]!).Header is not TextBlock englishHeader || englishHeader.Text != "Score library")
                throw new Exception("Tab header did not switch to English");
            if (!Field<Button>(window, "start").Content.ToString()!.StartsWith("Start playing"))
                throw new Exception("Start button did not switch to English");
            await WaitUntil(() => SettingsStore.Load(path, out _).Language == "en", "language persisted");
            language.SelectedIndex = 0;
            await WaitUntil(() => Field<Button>(window, "saveScore").Content.ToString() == "保存 TXT", "Chinese restored");
            if (!Field<Button>(window, "start").Content.ToString()!.StartsWith("开始演奏"))
                throw new Exception("Start label was not restored to Chinese");
            Console.WriteLine("PASS shell UI: 4:3 frameless window, icon, tabs, full-height library, language switch, persistence");
        }
        finally
        {
            if (window.IsVisible) await CloseWindow(window);
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private static async Task PlaybackRestartTests()
    {
        string path = Path.Combine(Path.GetTempPath(), "HarmonicaRestart-" + Guid.NewGuid() + ".json");
        var window = new PlayerWindow(path, new TestScoreDialogs(), administrator: false);
        try
        {
            window.Show();
            await WaitUntil(() => window.IsLoaded, "restart window loaded");
            Field<TextBox>(window, "score").Text = "1:16 1:16 0";
            Field<CheckBox>(window, "dry").IsChecked = true; // Explicit opt-in: tests must never send input.
            void Stop() => Field<Button>(window, "stop").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            foreach (bool duringPlayback in new[] { false, true })
            {
                Task first = Invoke(window, "Begin", (uint)0);
                await WaitUntil(() => duringPlayback
                    ? Field<TextBox>(window, "log").Text.Length > 0
                    : Field<TextBlock>(window, "status").Text.Contains("秒后开始"), "first run reached phase");
                var active = Field<Task>(window, "running");
                var cancellation = Field<CancellationTokenSource>(window, "cancellation");
                CancellationToken oldToken = cancellation.Token;
                for (int i = 0; i < 3; i++) await Invoke(window, "Begin", (uint)0);
                if (!ReferenceEquals(active, Field<Task>(window, "running")) ||
                    !ReferenceEquals(cancellation, Field<CancellationTokenSource>(window, "cancellation")))
                    throw new Exception("Duplicate start replaced the active playback");
                Stop();
                // Cleanup has not yet resumed on the dispatcher: no new run may start here.
                await Invoke(window, "Begin", (uint)0);
                if (!ReferenceEquals(active, Field<Task>(window, "running")) || !oldToken.IsCancellationRequested)
                    throw new Exception("Start during cancellation created another playback");
                await first.WaitAsync(TimeSpan.FromSeconds(3));
                if (Field<Task?>(window, "running") != null || Field<CancellationTokenSource?>(window, "cancellation") != null ||
                    Field<bool>(window, "beginning") || Field<TextBox>(window, "score").IsReadOnly)
                    throw new Exception("Stop did not finish cleanup and restore editing");

                Task restarted = Invoke(window, "Begin", (uint)0);
                await WaitUntil(() => Field<TextBlock>(window, "status").Text.Contains("秒后开始"), "restart countdown");
                if (ReferenceEquals(cancellation, Field<CancellationTokenSource>(window, "cancellation")) ||
                    Field<CancellationTokenSource>(window, "cancellation").IsCancellationRequested)
                    throw new Exception("Restart reused cancelled state");
                Stop(); await restarted.WaitAsync(TimeSpan.FromSeconds(3));
                await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                if (Field<TextBox>(window, "log").Text.Length != 0 || Field<TextBlock>(window, "status").Text != "已停止。")
                    throw new Exception("Old playback progress overwrote restarted state");
            }
            Console.WriteLine("PASS playback-restart: duplicate start, stop/restart during countdown and playback, stale progress");
        }
        finally
        {
            if (window.IsVisible) await CloseWindow(window);
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private static async Task MidiImportWindowTests()
    {
        string folder = Path.Combine(Path.GetTempPath(), "HarmonicaMidiWindow-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        string midiPath = Path.Combine(folder, "source.mid"), settingsPath = Path.Combine(folder, "settings.json");
        var timeline = ScoreTimeline.Create("0:0.01 1 2 0_", "120", "20");
        File.WriteAllBytes(midiPath, MidiExporter.Encode(timeline, "UI source"));
        byte[] original = File.ReadAllBytes(midiPath);
        var dialogs = new MidiWindowDialogs(midiPath);
        var window = new PlayerWindow(settingsPath, dialogs, administrator: false);
        try
        {
            window.Show(); await WaitUntil(() => window.IsLoaded, "MIDI window loaded");
            string previousBody = Field<TextBox>(window, "score").Text;
            dialogs.CancelConversion = true;
            await Invoke(window, "ImportMidiAsync");
            if (Field<TextBox>(window, "score").Text != previousBody) throw new Exception("Cancelled MIDI conversion changed editor");
            dialogs.CancelConversion = false;
            await Invoke(window, "ImportMidiAsync");
            if (Field<TextBox>(window, "score").Text == previousBody || !window.Title.Contains("*") ||
                Field<string?>(window, "documentPath") != null || Field<CheckBox>(window, "dry").IsChecked != false)
                throw new Exception("Imported MIDI did not create an unsaved TXT document with independent mode");
            if (!File.ReadAllBytes(midiPath).SequenceEqual(original)) throw new Exception("Source MIDI modified");
            string imported = Field<TextBox>(window, "score").Text;
            dialogs.UnsavedResult = MessageBoxResult.Cancel;
            await Invoke(window, "ImportMidiAsync");
            if (Field<TextBox>(window, "score").Text != imported) throw new Exception("Unsaved cancel lost MIDI document");
            var file = MidiImporter.Parse(original);
            var importDialog = new MidiImportDialog(file) { Owner = window };
            try
            {
                importDialog.Show();
                await WaitUntil(() => importDialog.IsLoaded, "MIDI conversion dialog loaded");
                var accept = Descendants(importDialog).OfType<Button>().Single(b => b.Content?.ToString() == "导入到编辑器");
                if (!accept.IsEnabled) throw new Exception("Compatible MIDI preview blocked");
                var fields = Descendants(importDialog).OfType<TextBox>().Where(b => !b.IsReadOnly).ToArray();
                fields.Single(b => b.Text == "0").Text = "127";
                if (accept.IsEnabled) throw new Exception("Out-of-range transpose allowed import");
            }
            finally { if (importDialog.IsVisible) importDialog.Close(); }
            Console.WriteLine("PASS MIDI-window: cancel, unsaved TXT, source preservation, mode, transpose block");
        }
        finally
        {
            dialogs.UnsavedResult = MessageBoxResult.No;
            if (window.IsVisible) await CloseWindow(window);
            Directory.Delete(folder, true);
        }
    }
    private sealed class MidiWindowDialogs(string path) : IScoreDialogs
    {
        public bool CancelConversion;
        public MessageBoxResult UnsavedResult = MessageBoxResult.No;
        public string? Open(Window owner) => null;
        public string? Save(Window owner, string suggestedName) => null;
        public string? OpenMidi(Window owner) => path;
        public MessageBoxResult Unsaved(Window owner) => UnsavedResult;
        public ScoreDocument? ConvertMidi(Window owner, MidiImportFile file) =>
            CancelConversion ? null : MidiImporter.Convert(file, file.Parts.Single(), 120, 10).Document;
        public void PlaybackIssue(Window owner, string message) => throw new Exception("Unexpected runtime stop: " + message);
    }
    private static async Task SettingsGenerationTests()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int writes = 0;
        var window = new PlayerWindow(path, new TestScoreDialogs(), value =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test write timeout");
            }
            SettingsStore.Save(path, value);
        });
        try
        {
            window.Show();
            var bpm = Field<TextBox>(window, "bpm");
            bpm.Text = "90";
            Task slow = Invoke(window, "SaveCurrentSettingsAsync", false);
            await WaitUntil(() => entered.IsSet, "slow settings write entered");
            bpm.Text = "";
            await Invoke(window, "SaveCurrentSettingsAsync", false);
            string error = Field<TextBlock>(window, "settingsStatus").Text;
            if (!error.Contains("尚未填写完整")) throw new Exception("Invalid edit not reported");
            release.Set(); await slow;
            if (Field<TextBlock>(window, "settingsStatus").Text != error) throw new Exception("Old save replaced newer invalid status");
            bpm.Text = "100"; Task a = Invoke(window, "SaveCurrentSettingsAsync", false);
            bpm.Text = "110"; Task b = Invoke(window, "SaveCurrentSettingsAsync", false);
            bpm.Text = "100"; Task final = Invoke(window, "SaveCurrentSettingsAsync", false);
            await Task.WhenAll(a, b, final);
            if (SettingsStore.Load(path, out _).Bpm != 100) throw new Exception("A-B-A final save incorrect");
            bpm.Text = "95";
            await CloseWindow(window);
            if (SettingsStore.Load(path, out _).Bpm != 95) throw new Exception("Close lost final valid edit");
            Console.WriteLine("PASS settings-generation: slow invalid edit, A-B-A, close final value");
        }
        finally
        {
            release.Set();
            if (window.IsVisible) await CloseWindow(window);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
    private static async Task DocumentWindowTests()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HarmonicaUI-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(directory);
        var dialogs = new TestScoreDialogs();
        var window = new PlayerWindow(System.IO.Path.Combine(directory, "settings.json"), dialogs);
        try
        {
            window.Show();
            var editor = Field<TextBox>(window, "score");
            editor.Text = "1 2 3";
            if (!window.Title.EndsWith(" *")) throw new Exception("Dirty title missing");
            dialogs.Answer = MessageBoxResult.Cancel;
            window.Close();
            await WaitUntil(() => !window.IsClosing, "cancel close");
            if (!window.IsVisible || editor.Text != "1 2 3") throw new Exception("Cancel lost document");
            dialogs.SavePath = System.IO.Path.Combine(directory, "saved.txt");
            await Invoke(window, "DocumentOperation", (Func<Task<bool>>)(async () =>
                await (Task<bool>)typeof(PlayerWindow).GetMethod("SaveScoreAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, new object[] { false })!));
            if (window.Title.EndsWith(" *") || !System.IO.File.Exists(dialogs.SavePath)) throw new Exception("Save did not mark clean");
            Field<TextBox>(window, "gap").Text = "21";
            if (!window.Title.EndsWith(" *")) throw new Exception("Gap edit must mark score unsaved");
            Field<TextBox>(window, "gap").Text = "20";
            if (window.Title.EndsWith(" *")) throw new Exception("Restoring gap must restore clean state");
            editor.Text = "1 2";
            dialogs.OpenPath = dialogs.SavePath;
            dialogs.Answer = MessageBoxResult.Yes;
            await Invoke(window, "Import");
            if (editor.Text != "1 2") throw new Exception("Reimport read stale pre-save file");
            string legacyPath = System.IO.Path.Combine(directory, "old bpm100.txt");
            await System.IO.File.WriteAllTextAsync(legacyPath, "5 6 7");
            dialogs.OpenPath = legacyPath;
            await Invoke(window, "Import");
            if (Field<TextBox>(window, "bpm").Text != "100" || editor.Text != "5 6 7") throw new Exception("Legacy import failed");
            editor.Text = "5 6";
            dialogs.Answer = MessageBoxResult.Cancel;
            await Invoke(window, "Import");
            if (editor.Text != "5 6") throw new Exception("Cancelled import lost edits");
            dialogs.SavePath = legacyPath;
            dialogs.Answer = MessageBoxResult.Yes;
            window.Close();
            await WaitUntil(() => !window.IsClosing, "failed save cancels close");
            if (!window.IsVisible || await System.IO.File.ReadAllTextAsync(legacyPath) != "5 6 7") throw new Exception("Legacy overwrite protection failed");
            string badPath = System.IO.Path.Combine(directory, "bad.txt");
            await System.IO.File.WriteAllTextAsync(badPath, "@format=unknown\n@bpm=100\n\n1");
            dialogs.OpenPath = badPath;
            await Invoke(window, "Import");
            if (editor.Text != "5 6") throw new Exception("Malformed import lost editor");
            dialogs.SavePath = System.IO.Path.Combine(directory, "converted.txt");
            await CloseWindow(window);
            var saved = await ScoreDocumentReader.ReadAsync(dialogs.SavePath, 120);
            if (saved.Document.Bpm != 100 || saved.Document.ScoreText != "5 6") throw new Exception("Close-save roundtrip failed");
            Console.WriteLine("PASS document UI: dirty, cancel, save, legacy protection, malformed import, close-save");
        }
        finally
        {
            dialogs.Answer = MessageBoxResult.No;
            if (window.IsVisible) await CloseWindow(window);
            foreach (string file in System.IO.Directory.GetFiles(directory)) System.IO.File.Delete(file);
            System.IO.Directory.Delete(directory);
        }
    }
    private static async Task AudioWindowTests()
    {
        string path = Path.Combine(Path.GetTempPath(), "HarmonicaAudioUI-" + Guid.NewGuid() + ".json");
        var fake = new FakeLocalAudio();
        var dialogs = new TestScoreDialogs { MidiPath = path + ".mid" };
        var window = new PlayerWindow(path, dialogs, audioPlayer: fake);
        try
        {
            window.Show(); await WaitUntil(() => window.IsLoaded, "audio window loaded");
            var editor = Field<TextBox>(window, "score");
            editor.Text = "1 【2】 0 3"; editor.Select(3, 0);
            await WaitUntil(() => Field<Button>(window, "listen").IsEnabled, "listen available");
            await Invoke(window, "ExportMidiAsync");
            if (!File.Exists(dialogs.MidiPath) || !window.Title.EndsWith(" *") || editor.IsReadOnly)
                throw new Exception("MIDI export lost dirty state or did not create file");
            dialogs.MidiPath = null;
            await Invoke(window, "ExportMidiAsync");
            if (!window.Title.EndsWith(" *") || editor.IsReadOnly)
                throw new Exception("Cancelled export changed editor");
            var dryMode = Field<CheckBox>(window, "dry");
            // Suspension bypasses the window's hotkey refresh. Exercise a real mode
            // transition even when startup already defaults to game performance.
            dryMode.IsChecked = true;
            Field<HotkeyController>(window, "hotkeys").Suspend();
            dryMode.IsChecked = false;
            if (Field<Button>(window, "start").IsEnabled)
                throw new Exception("Game performance allowed without stop hotkey");
            if (!Field<Button>(window, "listen").IsEnabled || !Field<Button>(window, "listenFromCursor").IsEnabled ||
                !Field<Button>(window, "exportMidi").IsEnabled)
                throw new Exception("Local audio or MIDI export depends on game hotkey readiness");
            var run = Invoke(window, "ListenAsync", true);
            await WaitUntil(() => fake.Active, "audio started");
            if (fake.StartIndex != 1 || !editor.IsReadOnly || Field<Button>(window,"exportMidi").IsEnabled ||
                Field<Button>(window,"start").IsEnabled || Field<TextBox>(window,"bpm").IsEnabled)
                throw new Exception("Audio cursor or mutual exclusion failed");
            fake.Report!(1000);
            await WaitUntil(() => editor.SelectionStart == 6, "progress highlights rest");
            Field<Slider>(window, "volumeSlider").Value = 35;
            if (fake.Volume != 35) throw new Exception("Volume not forwarded");
            Field<Button>(window,"stop").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await run.WaitAsync(TimeSpan.FromSeconds(3));
            if(editor.IsReadOnly || editor.SelectionStart != 3 || !Field<Button>(window,"listen").IsEnabled)
                throw new Exception("Stop did not restore editor/cursor");
            fake.Fail = true;
            await Invoke(window,"ListenAsync",false);
            if(editor.IsReadOnly || !Field<TextBlock>(window,"alert").Text.Contains("fake audio device"))
                throw new Exception("Audio error not visible or UI stuck");
            fake.Fail = false;
            run = Invoke(window,"ListenAsync",false);
            await WaitUntil(() => fake.Active,"audio retry");
            fake.Complete(); await run.WaitAsync(TimeSpan.FromSeconds(3));
            if(Field<ProgressBar>(window,"audioProgress").Value != 100 || editor.IsReadOnly)
                throw new Exception("Audio completion did not restore UI");
            // Replay then close while a stream is active.
            run = Invoke(window,"ListenAsync",false);
            await WaitUntil(() => fake.Active,"audio before close");
            await CloseWindow(window); await run;
            if(fake.Active) throw new Exception("Audio continued after close");
            var saved = SettingsStore.Load(path, out var warning);
            if(warning != null || saved.PreviewVolume != 35) throw new Exception("Preview volume not saved");
            Console.WriteLine("PASS v0.3.0 audio UI: cursor, progress, lock, volume, stop, failure/retry, complete, close");
        }
        finally
        {
            if(window.IsVisible) await CloseWindow(window);
            if(File.Exists(path)) File.Delete(path);
            if(File.Exists(path + ".mid")) File.Delete(path + ".mid");
        }
    }

    // 曲谱库卡片：添加、绑定、冲突拦截、快捷键加载、演奏中锁定、移除、持久化。
    private static async Task LibraryWindowTests()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HarmonicaLibraryUI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settingsPath = Path.Combine(directory, "settings.json");
        string firstScore = Path.Combine(directory, "小星星.txt");
        string secondScore = Path.Combine(directory, "欢乐颂.txt");
        await File.WriteAllTextAsync(firstScore, "1 2 3");
        await File.WriteAllTextAsync(secondScore, "4 5 6");
        var dialogs = new TestScoreDialogs { LibraryPaths = new[] { firstScore, secondScore } };
        var window = new PlayerWindow(settingsPath, dialogs);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Click(string button) => Field<Button>(window, button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        void SetHotkey(HotkeyBinding? hotkey) => typeof(PlayerWindow)
            .GetMethod("SetSelectedLibraryHotkey", flags)!.Invoke(window, new object?[] { hotkey });
        try
        {
            window.Show();
            await WaitUntil(() => window.IsLoaded, "library window loaded");
            AppSettings Settings() => Field<AppSettings>(window, "settings");
            if (Field<ListBox>(window, "libraryList").Items.Count != 1 || !Field<TextBlock>(window, "libraryStatus").Text.Contains("曲谱库为空"))
                throw new Exception("Empty library card did not show its placeholder.");

            Click("addLibrary");
            await WaitUntil(() => Settings().Library.Length == 2, "library entries added");
            if (Field<ListBox>(window, "libraryList").Items.Count != 2 ||
                Settings().Library[0].Path != firstScore || Settings().Library[1].Path != secondScore)
                throw new Exception("Added scores are missing or out of order.");

            Field<ListBox>(window, "libraryList").SelectedIndex = 0;
            SetHotkey(new HotkeyBinding(0x70));
            var controller = Field<HotkeyController>(window, "hotkeys");
            await WaitUntil(() => controller.LibraryReady(ScoreLibrary.HotkeyId(0)), "library hotkey registered");
            if (Settings().Library[0].Hotkey != new HotkeyBinding(0x70) ||
                !Field<TextBlock>(window, "status").Text.Contains("已绑定") ||
                !Field<TextBlock>(window, "hotkeyStatus").Text.Contains("曲谱库 1 个快捷键已就绪"))
                throw new Exception("Bound library hotkey was not saved, reported or summarised.");

            // 与开始键相同的按键必须在写入设置前被挡下，否则下次启动会注册失败。
            SetHotkey(Settings().Start);
            if (Settings().Library[0].Hotkey != new HotkeyBinding(0x70) || !Field<TextBlock>(window, "alert").Text.Contains("开始键"))
                throw new Exception("Conflicting library hotkey was not rejected.");

            await Invoke(window, "LibraryActivateAsync", 0, (uint)0, false);
            if (Field<TextBox>(window, "score").Text != "1 2 3" || Field<string?>(window, "documentPath") != firstScore)
                throw new Exception("Library hotkey did not load the score into the editor.");

            // “按快捷键立即演奏”开关：关掉只加载曲谱，打开则加载并演奏。
            Field<CheckBox>(window, "libraryPlays").IsChecked = false;
            if (Settings().LibraryHotkeyPlays || !Field<TextBlock>(window, "status").Text.Contains("只把曲谱加载到编辑器"))
                throw new Exception("Turning the plays switch off was not applied.");
            Field<CheckBox>(window, "libraryPlays").IsChecked = true;
            if (!Settings().LibraryHotkeyPlays || !Field<TextBlock>(window, "status").Text.Contains("加载并演奏"))
                throw new Exception("Turning the plays switch on was not applied.");

            // 演奏/起奏期间禁止改库，避免保存出“未注册生效”的绑定。
            var running = Field<CancellationTokenSource?>(window, "cancellation");
            using var blocked = new CancellationTokenSource();
            typeof(PlayerWindow).GetField("cancellation", flags)!.SetValue(window, blocked);
            try
            {
                Click("removeLibrary");
                if (Settings().Library.Length != 2 || !Field<TextBlock>(window, "status").Text.Contains("正在演奏"))
                    throw new Exception("Library edit during playback was not blocked.");
            }
            finally { typeof(PlayerWindow).GetField("cancellation", flags)!.SetValue(window, running); }

            Field<ListBox>(window, "libraryList").SelectedIndex = 1;
            Click("removeLibrary");
            await WaitUntil(() => Settings().Library.Length == 1, "library entry removed");
            if (!File.Exists(secondScore) || Field<TextBox>(window, "score").Text != "1 2 3")
                throw new Exception("Removing a library entry touched the file or the editor.");

            // 快捷键路径带 play 时应进入倒计时（用“仅测试”模式，避免真的发按键）。
            // “立即演奏”开关保持打开，最后一起检查它是否随窗口关闭写入设置文件。
            Field<CheckBox>(window, "dry").IsChecked = true;
            var playByHotkey = (Task)typeof(PlayerWindow)
                .GetMethod("LibraryActivateAsync", flags)!.Invoke(window, new object?[] { 0, (uint)0x70, true })!;
            await WaitUntil(() => Field<TextBlock>(window, "status").Text.Contains("秒后开始"), "library hotkey countdown");
            if (!Settings().LibraryHotkeyPlays)
                throw new Exception("Library hotkey countdown ran while the plays switch was off.");

            await CloseWindow(window);
            await playByHotkey.WaitAsync(TimeSpan.FromSeconds(5));
            var saved = SettingsStore.Load(settingsPath, out var warning);
            if (warning != null || saved.Library.Length != 1 || saved.Library[0].Path != firstScore ||
                saved.Library[0].Hotkey != new HotkeyBinding(0x70) || !saved.LibraryHotkeyPlays)
                throw new Exception("Library settings were not persisted.");
            Console.WriteLine("PASS library UI: card, add, bind, conflict, hotkey load, plays switch, playback lock, remove, persist");
        }
        finally
        {
            if (window.IsVisible) await CloseWindow(window);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency)
            {
                yield return dependency;
                foreach (var nested in Descendants(dependency)) yield return nested;
            }
    }
}

sealed class TestScoreDialogs : IScoreDialogs
{
    public string? OpenPath, SavePath, MidiPath;
    public string[] LibraryPaths = Array.Empty<string>();
    public MessageBoxResult Answer = MessageBoxResult.No;
    public string? Open(Window owner) => OpenPath;
    public string? Save(Window owner, string suggestedName) => SavePath;
    public string[] OpenLibrary(Window owner) => LibraryPaths;
    public MessageBoxResult Unsaved(Window owner) => Answer;
    public string? SaveMidi(Window owner, string suggestedName) => MidiPath;
    public void PlaybackIssue(Window owner, string message) => throw new Exception("Unexpected playback stop: " + message);
}

sealed class FakeLocalAudio : ILocalAudioPlayer
{
    public int Volume { get; set; }
    public bool Active, Fail;
    public int StartIndex;
    public Action<double>? Report;
    private TaskCompletionSource? completion;
    public void Complete() => completion!.TrySetResult();
    public async Task PlayAsync(ScoreTimeline timeline, int startIndex, Action<double> progress, CancellationToken token)
    {
        if(Fail) throw new InvalidOperationException("fake audio device unavailable");
        Active=true; StartIndex=startIndex; Report=progress;
        completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        try { await completion.Task.WaitAsync(token); }
        finally { Active=false; }
    }
}
