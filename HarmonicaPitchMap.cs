using System.Globalization;

namespace HarmonicaPlayer;

public static class HarmonicaPitchMap
{
    public const int Lowest = 48;
    public const int Highest = 85;
    private static readonly int[] Offsets = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly string[] Names = { "1", "#1", "2", "#2", "3", "4", "#4", "5", "#5", "6", "#6", "7" };
    public static int Pitch(ScoreNote note) => note.Degree == 0 ? -1 :
        60 + note.Octave * 12 + Offsets[note.Degree - 1] + (note.Sharp ? 1 : 0);
    public static bool Contains(int pitch) => pitch is >= Lowest and <= Highest;
    public static string Token(int pitch)
    {
        if (!Contains(pitch)) throw new FormatException($"MIDI 音高 {pitch} 超出支持范围 {Lowest}～{Highest}。");
        if (pitch >= 84) return pitch == 84 ? "【【1】】" : "【【#1】】";
        string token = Names[pitch % 12];
        return pitch < 60 ? $"（{token}）" : pitch < 72 ? token : $"【{token}】";
    }
    public static string Name(int pitch)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return names[pitch % 12] + (pitch / 12 - 1).ToString(CultureInfo.InvariantCulture) + $" ({pitch})";
    }
}

