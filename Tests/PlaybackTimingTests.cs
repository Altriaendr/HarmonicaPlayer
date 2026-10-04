using HarmonicaPlayer;

static class PlaybackTimingTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception("Playback timing: " + name);
            passed++;
        }
        static bool Near(double a, double b) => Math.Abs(a - b) < .00001;
        static IReadOnlyList<GamePlaybackStep> Plan(string body, int bpm, int gap)
        {
            var notes = ScoreParser.Parse(body);
            double beat = 60000.0 / bpm;
            PlaybackValidation.ValidateGap(gap.ToString(), notes, beat);
            return GamePlaybackTiming.Create(notes, beat, gap);
        }

        // Independently specified deadlines catch a misplaced gap/preparation delay.
        var example = Plan("0_ 1_ 1 — 【#1】:1.25 【【1】】__ 0_.", 120, 20);
        Check(example.Select(s => s.StartMs).SequenceEqual(new double[] { 0, 250, 500, 1500, 2125, 2250 }), "known beat starts");
        Check(example.Select(s => s.EndMs).SequenceEqual(new double[] { 250, 500, 1500, 2125, 2250, 2625 }), "known beat ends including trailing rest");
        Check(example.Select(s => s.KeyDownMs).SequenceEqual(new double?[] { null, 262, 512, 1512, 2137, null }), "12ms preparation only for sounding notes");
        Check(example.Select(s => s.KeyUpMs).SequenceEqual(new double?[] { null, 480, 1480, 2105, 2230, null }), "gap inside each sounding beat");
        Check(example[3].Note.Sharp && example[3].Note.Octave == 1 && example[4].Note.Octave == 2, "sharp and highest do reach the game plan");
        var repeated = Plan("5 5 5 —", 120, 20);
        Check(repeated.Count == 3 && repeated.Select(s => s.KeyDownMs).SequenceEqual(new double?[] { 12, 512, 1012 }) && repeated[^1].EndMs == 2000,
            "repeated notes stay separate; extension has one key-down");
        var shortest = Plan("1:0.144", 120, 20).Single();
        Check(Near(shortest.KeyUpMs!.Value - shortest.KeyDownMs!.Value, 40), "minimum allowed hold remains 40ms");
        bool rejected = false;
        try { Plan("1:0.143", 120, 20); } catch (ScoreFormatException) { rejected = true; }
        Check(rejected, "short note is rejected rather than lengthened");

        void Compare(string body, int bpm, int gap)
        {
            var game = Plan(body, bpm, gap);
            var timeline = ScoreTimeline.Create(body, bpm.ToString(), gap.ToString());
            Check(game.Count == timeline.Notes.Count, "same event count");
            Check(game.Zip(timeline.Notes).All(p => Near(p.First.StartMs, p.Second.StartMs) && Near(p.First.EndMs, p.Second.EndMs)), "same absolute beat boundaries");
            Check(Near(game[^1].EndMs, timeline.DurationMs), "same total duration");
            Check(game.Where(s => s.Note.Degree == 0).All(s => s.KeyDownMs == null && s.KeyUpMs == null), "rests emit no input events");
            Check(game.Zip(timeline.Notes).Where(p => p.First.Note.Degree != 0).All(p =>
                Near(p.First.KeyDownMs!.Value, p.Second.StartMs + 12) && Near(p.First.KeyUpMs!.Value, p.Second.SoundEndMs)), "only game preparation differs from audio");

            // Read the emitted MIDI independently; compare actual event ticks to game deadlines.
            var midi = Decode(MidiExporter.Encode(timeline, "Timing regression"));
            long Tick(double ms) => (long)Math.Round(ms * bpm / 60000.0 * 9600);
            var sounding = game.Where(s => s.Note.Degree != 0).ToArray();
            var on = midi.Events.Where(e => e.Status == 0x90).ToArray();
            var off = midi.Events.Where(e => e.Status == 0x80).ToArray();
            Check(on.Length == sounding.Length && off.Length == sounding.Length, "MIDI keeps repeated on/off events and excludes rests");
            Check(on.Zip(sounding).All(p => Math.Abs(p.First.Tick - Tick(p.Second.KeyDownMs!.Value - 12)) <= 1 && p.First.Pitch == ScoreTimeline.Pitch(p.Second.Note)) &&
                off.Zip(sounding).All(p => Math.Abs(p.First.Tick - Tick(p.Second.KeyUpMs!.Value)) <= 1 && p.First.Pitch == ScoreTimeline.Pitch(p.Second.Note)), "MIDI agrees within one tick");
            Check(Math.Abs(midi.EndTick - Tick(game[^1].EndMs)) <= 1, "MIDI end includes final rest/gap");
        }
        foreach (int bpm in new[] { 90, 120, 180 })
        foreach (int gap in new[] { 10, 20 })
            Compare("0_ （#1） 5 5 【3_.】 【4__】 【【1】】:1.25 【【＃1】】:1.25 5 — 0:0.333", bpm, gap);
        Compare(string.Join(' ', Enumerable.Repeat("1:0.333", 2000)) + " 0:1.25", 137, 20);
        Compare("0:0.01 0 —", 300, 5000);
        Console.WriteLine($"PASS playback timing: {passed} game/audio/MIDI checks");
    }

    private static (List<(long Tick, int Status, int Pitch)> Events, long EndTick) Decode(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        reader.BaseStream.Position = 22; // SMF header (14), track header (8).
        int Variable()
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = reader.ReadByte(); value = (value << 7) | (b & 127);
                if ((b & 128) == 0) return value;
            }
            throw new Exception("Invalid MIDI variable length value");
        }
        var events = new List<(long Tick, int Status, int Pitch)>();
        long tick = 0, end = -1;
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            tick += Variable(); int status = reader.ReadByte();
            if (status == 0xFF)
            {
                int type = reader.ReadByte(), length = Variable();
                if (type == 0x2F) end = tick;
                if (reader.ReadBytes(length).Length != length) throw new Exception("Truncated MIDI metadata");
            }
            else if (status == 0xC0) reader.ReadByte();
            else if (status is 0x90 or 0x80)
            {
                int pitch = reader.ReadByte(); reader.ReadByte(); events.Add((tick, status, pitch));
            }
            else throw new Exception("Unexpected MIDI event");
        }
        return (events, end);
    }
}
