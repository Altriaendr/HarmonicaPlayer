using HarmonicaPlayer;

static class PlaybackGuardTests
{
    public static async Task CheckFeedback(string directory)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "*.txt", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p) != "readme.txt"))
        {
            var document = (await ScoreDocumentReader.ReadAsync(path, 120)).Document;
            var notes = document.Validate(document.Gap.ToString());
            var plan = GamePlaybackTiming.Create(notes, 60000.0 / document.Bpm, document.Gap);
            foreach (var step in plan.Select((value, index) => (value, index)))
                PlaybackTimingGuard.Check(step.value, step.value.StartMs + 6, document.ScoreText, step.index);
            var timeline = ScoreTimeline.Create(document.ScoreText, document.Bpm.ToString(), document.Gap.ToString());
            if (Math.Abs(plan[^1].EndMs - timeline.DurationMs) > .000001) throw new Exception("Feedback timeline changed");
            Console.WriteLine($"PASS feedback: {path} · {notes.Count} 音/休止 · {timeline.DurationMs / 1000:0.######} 秒 · 注入 6ms 迟到，无输入、无真实等待");
        }
    }
    public static void Run()
    {
        int passed = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("Playback guard: " + name); passed++; }
        var body = "0:0.999999566667 4:0.499999783333 4:0.999999566667 【2】:0.999999566667 【2】:3.489581821181 |\r\n0:0.010416662153 0:2.749999241667";
        var notes = ScoreParser.Parse(body);
        var plan = GamePlaybackTiming.Create(notes, 60000.0 / 163, 10);
        var rest = plan[5];
        Check(Math.Abs(rest.DurationMs - 3.834354) < .000001, "real reported tiny rest remains intact");
        Check(PlaybackTimingGuard.Tolerance(rest) == 50, "rest does not get sub-ms stop threshold");
        PlaybackTimingGuard.Check(rest, rest.StartMs + 2, body, 5);
        Check(true, "2ms jitter does not falsely stop 3.8ms rest");
        Check(Math.Abs(plan[^1].EndMs - notes.Sum(n => n.Beats * 60000.0 / 163)) < .000001, "no duration/tempo change");
        var note = new GamePlaybackStep(new ScoreNote(1, 0, false, 0), 0, 80, 80, 12, 70);
        Check(PlaybackTimingGuard.Tolerance(note) == 20, "sounding note keeps existing serious-delay protection");
        PlaybackTimingGuard.Check(note, 20, "1", 0); Check(true, "boundary allowed");
        try { PlaybackTimingGuard.Check(note, 21, "1", 0); throw new Exception("Expected stop"); }
        catch (PlaybackTimingException e)
        {
            Check(e.Position == 0 && e.Message.Contains("实际迟到 21") && e.Message.Contains("允许值 20"), "measured delay and threshold");
            Check(e.Message.Contains("第 1 行、第 1 列") && e.Message.Contains("曲内 0 秒"), "position and relative playback time");
        }
        try { PlaybackTimingGuard.Check(rest, rest.StartMs + 51, body, 5); throw new Exception("Expected serious rest stop"); }
        catch (PlaybackTimingException e)
        {
            Check(e.Position == notes[5].Position && e.Message.Contains("第 2 行、第 1 列"), "CRLF and rest error location");
            Check(e.Message.Contains("保留总时值") && e.Message.Contains("不能直接删除"), "specific non-destructive rest suggestions");
        }
        var barrage = ScoreParser.Parse("0:0.041666697917");
        var barrageStep = GamePlaybackTiming.Create(barrage, 60000.0 / 105, 10).Single();
        PlaybackTimingGuard.Check(barrageStep, 6, "0:0.041666697917", 0);
        Check(Math.Abs(barrageStep.DurationMs - 23.809542) < .000001, "Barricades 6ms jitter no false stop");
        var repeated = GamePlaybackTiming.Create(ScoreParser.Parse("5 5"), 500, 10);
        Check(repeated.Count == 2, "guard does not merge repeated sounding notes");
        PlaybackTimingGuard.Check(note, 32, "1", 0, 12);
        Check(true, "key-down jitter measured relative to its own deadline, not preparation start");
        try { PlaybackTimingGuard.Check(note, 33, "1", 0, 12); throw new Exception("Expected key-down stop"); }
        catch (PlaybackTimingException e)
        {
            Check(e.Message.Contains("音符按键起音") && e.Message.Contains("实际迟到 21"), "late native key-down gets accurate phase diagnosis");
            Check(e.Message.Contains("曲内 0.012 秒"), "key-down location includes 12ms preparation");
        }
        Console.WriteLine($"PASS playback guard: {passed} checks");
    }
}

