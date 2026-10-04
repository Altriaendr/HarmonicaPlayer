using HarmonicaPlayer;

static class PitchInputTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("Pitch/input: " + name); passed++; }
        int[][] naturalPitches = { new[] {48,50,52,53,55,57,59}, new[] {60,62,64,65,67,69,71}, new[] {72,74,76,77,79,81,83} };
        ushort[][] expectedScans = { new ushort[] {0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32},
            new ushort[] {0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32}, new ushort[] {0x33,0x2D,0x2E,0x2F,0x30,0x31,0x32} };
        bool[][] expectedHigh = { new[] {false,false,false,false,false,false,false},
            new[] {false,false,false,false,false,false,false}, new[] {false,true,true,true,true,true,true} };
        for (int region = 0; region < 3; region++)
        for (int degree = 1; degree <= 7; degree++)
        foreach (bool sharp in new[] {false, true})
        {
            string body = (sharp ? "#" : "") + degree;
            body = region == 0 ? "（" + body + "）" : region == 2 ? "【" + body + "】" : body;
            var note = ScoreParser.Parse(body).Single();
            Check(note.Degree == degree && note.Octave == region - 1 && note.Sharp == sharp &&
                ScoreTimeline.Pitch(note) == naturalPitches[region][degree - 1] + (sharp ? 1 : 0), "all natural/sharp degrees preserve octave and pitch: " + body);
            Check(GameNoteInput.Create(note) == new GameNoteInput(expectedScans[region][degree - 1], region == 0,
                expectedHigh[region][degree - 1], sharp), "combined physical keys: " + body);
            if (sharp) Check(ScoreParser.Parse(body.Replace('#', '＃')).Single() == note, "fullwidth sharp alias preserves source index: " + body);
        }
        var high = ScoreParser.Parse("【＃1#2＃3#4＃5#6＃7】");
        Check(high.Count == 7 && high.All(n => n.Octave == 1 && n.Sharp), "mixed-width grouped sharps remain individual notes");
        var top = ScoreParser.Parse("【【#1】】:1.25").Single();
        Check(top.Octave == 2 && top.Sharp && top.Beats == 1.25 && ScoreTimeline.Pitch(top) == 85, "highest sharp with custom duration");
        Check(GameNoteInput.Create(top) == new GameNoteInput(0x33, false, true, true), "highest sharp holds right + middle + comma together");
        Check(GameNoteInput.Create(ScoreParser.Parse("【【1】】").Single()) == new GameNoteInput(0x33, false, true, false), "existing highest do mapping preserved");
        var wideTop = ScoreParser.Parse("1\r\n【【＃1】】_.")[1];
        Check(wideTop.Position == 6 && wideTop.Octave == 2 && wideTop.Sharp && wideTop.Beats == .75, "fullwidth highest sharp keeps exact source position");
        Check(HarmonicaPitchMap.Token(84) == "【【1】】" && HarmonicaPitchMap.Token(85) == "【【#1】】", "canonical top MIDI inverse mapping");
        Check(ScoreTimeline.Pitch(ScoreParser.Parse("【#3】").Single()) == ScoreTimeline.Pitch(ScoreParser.Parse("【4】").Single()) &&
            ScoreTimeline.Pitch(ScoreParser.Parse("【#7】").Single()) == 84, "sharp 3/7 are valid enharmonics, not rejected");
        foreach (string malformed in new[] {"#＃1","＃#1","＃0","【【#2】】","【【#1_】】","【【＃1_.】】","【【#11】】","【【＃1】","【【【#1】】】"})
        {
            bool rejected = false; try { ScoreParser.Parse(malformed); } catch (ScoreFormatException) { rejected = true; }
            Check(rejected, "invalid octave syntax rejected: " + malformed);
        }
        var cursor = ScoreTimeline.Create("1＃2 【【＃1】】", "120", "20");
        Check(cursor.IndexAtCursor("1＃2 【【＃1】】", 1) == 1 && cursor.IndexAtCursor("1＃2 【【＃1】】", 6) == 2,
            "cursor at fullwidth sharp selects following note");
        var restored = ScoreDocumentReader.Parse(ScoreDocumentWriter.Serialize(new ScoreDocument("sharp", 120, "【【＃1】】:1.25", Gap: 20), "20"), null, 120);
        Check(restored.Document.ScoreText == "【【＃1】】:1.25" && restored.Document.Validate("20").Single().Sharp, "TXT save/reload retains source spelling");
        for (int degree = 2; degree <= 7; degree++)
        foreach (string sharp in new[] {"", "#", "＃"})
        {
            string body = "1\r\n【【" + sharp + degree + "】】";
            try { ScoreParser.Parse(body); throw new Exception("Expected unsupported double-high rejection"); }
            catch (ScoreFormatException e)
            {
                Check(e.Message.Contains("不支持双层2～7") && e.Message.Contains("第2行") &&
                    e.Position == body.IndexOf((char)('0' + degree)), "explicit double-high rejection and exact digit location: " + body);
            }
        }
        for (int degree = 2; degree <= 7; degree++)
        foreach (bool sharp in new[] {false, true})
        {
            try { GameNoteInput.Create(new ScoreNote(degree, 2, sharp, 0)); throw new Exception("Expected input plan rejection"); }
            catch (FormatException e) { Check(e.Message.Contains("不支持双层2～7"), "invalid note model cannot generate a double-high input"); }
        }
        foreach (var invalid in new[] {new ScoreNote(0, 0, false, 0), new ScoreNote(8, 0, false, 0),
            new ScoreNote(1, -2, true, 0), new ScoreNote(1, 3, true, 0)})
        {
            bool rejected = false; try { GameNoteInput.Create(invalid); } catch (FormatException) { rejected = true; }
            Check(rejected, "invalid/rest model generates no input plan");
        }
        Console.WriteLine($"PASS pitch/input combinations: {passed} checks (no native input)");
    }
}
