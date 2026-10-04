namespace HarmonicaPlayer;

internal sealed record GamePlaybackStep(ScoreNote Note, double StartMs, double DurationMs,
    double EndMs, double? KeyDownMs, double? KeyUpMs);

// The caller validates BPM/gap first. Keep the existing game's absolute deadlines
// and accumulation order; expose the actual plan for comparison with audio/MIDI.
internal static class GamePlaybackTiming
{
    public static IReadOnlyList<GamePlaybackStep> Create(IReadOnlyList<ScoreNote> notes, double beatMs, int gap)
    {
        var result = new List<GamePlaybackStep>(notes.Count);
        double nextStart = 0;
        foreach (var note in notes)
        {
            double duration = note.Beats * beatMs;
            double end = nextStart + duration;
            result.Add(new(note, nextStart, duration, end,
                note.Degree == 0 ? null : nextStart + PlaybackValidation.PreparationMs,
                note.Degree == 0 ? null : end - gap));
            nextStart = end;
        }
        return result;
    }
}
