namespace HarmonicaPlayer;

public sealed class PlaybackTimingException(int position, string message) : Exception(message)
{
    public int Position { get; } = position;
}

internal static class PlaybackTimingGuard
{
    // A rest sends no input. Its tolerance must not shrink to sub-millisecond
    // values; keep absolute deadlines and check sounding notes independently.
    internal static double Tolerance(GamePlaybackStep step) =>
        step.Note.Degree == 0 ? 50 : Math.Min(50, step.DurationMs / 4);
    internal static void Check(GamePlaybackStep step, double elapsedMs, string body, int index, double? deadlineMs = null)
    {
        double deadline = deadlineMs ?? step.StartMs;
        double delay = Math.Max(0, elapsedMs - deadline), tolerance = Tolerance(step);
        if (delay <= tolerance) return;
        int line = 1, column = 1;
        int position = Math.Clamp(step.Note.Position, 0, body.Length);
        for (int i = 0; i < position; i++)
        {
            if (body[i] == '\n' || (body[i] == '\r' && (i + 1 >= body.Length || body[i + 1] != '\n')))
            { line++; column = 1; }
            else if (body[i] != '\r') column++;
        }
        string token = body[position..Math.Min(body.Length, position + 32)].Split('\r', '\n', '|')[0].Trim();
        string kind = step.Note.Degree == 0 ? "休止" : deadlineMs != null ? "音符按键起音" : "音符";
        throw new PlaybackTimingException(position,
            $"调度超时：第 {index + 1} 个音/休止（{kind}），第 {line} 行、第 {column} 列，曲内 {deadline / 1000:0.###} 秒。\n" +
            $"片段：{token}\n时长 {step.DurationMs:0.###}ms；实际迟到 {delay:0.###}ms，超过允许值 {tolerance:0.###}ms。\n" +
            (step.Note.Degree == 0
                ? "建议：相邻休止可合并并保留总时值；孤立休止不能直接删除。此处已使用独立的 50ms 休止容差，仍检测到实际调度超时。"
                : "建议：若总在此处停止，核对该片段及短音时值，可临时降低 BPM 对照；若位置随机，检查系统短时卡顿或后台任务。") +
            "\n这不是设备性能不足的判定；已停止并尝试释放输入。点击“定位曲谱错误”查看位置。");
    }
}

