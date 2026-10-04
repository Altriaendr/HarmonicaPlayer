namespace HarmonicaPlayer;

// A pure input plan shared by the native sender and device-free regression tests.
// Octave and semitone modifiers are independent and remain held together.
internal readonly record struct GameNoteInput(ushort Scan, bool Low, bool High, bool Sharp)
{
    private static readonly ushort[] Scans = { 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32 };
    internal static GameNoteInput Create(ScoreNote note)
    {
        if (note.Octave == 2 && note.Degree is >= 2 and <= 7)
            throw new FormatException("不支持双层2～7（包括升半音），未发送输入。");
        if (note.Degree is < 1 or > 7 || note.Octave is < -1 or > 2)
            throw new FormatException("无效音符或音区，未发送输入。");
        ushort scan = note.Octave >= 1 && note.Degree == 1 ? (ushort)0x33 : Scans[note.Degree - 1];
        return new(scan, note.Octave == -1,
            note.Octave == 2 || (note.Octave == 1 && note.Degree != 1), note.Sharp);
    }
}
