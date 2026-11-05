namespace ZBoard.Stats;

/// <summary>
/// Misst Druckdauer, Abstände und Sitzungskennzahlen. Kennt keine getippten Zeichen.
/// Uhren werden injiziert, damit die Logik ohne Android testbar ist.
/// </summary>
public sealed class KeystrokeStatsRecorder
{
    const int FlightResetMs = 5000;
    const int PauseThresholdMs = 2000;

    readonly IKeystrokeStatsSink _sink;
    readonly Func<long> _uptimeMs;
    readonly Func<long> _wallClockMs;

    Guid? _session;
    long _sessionStartWall;
    long? _downUptime;
    long? _lastUpUptime;
    long? _firstDownUptime;

    int _keystrokes;
    int _textKeystrokes;
    int _backspaces;
    int _pauses;
    int _suggestions;
    int _autocorrections;

    public KeystrokeStatsRecorder(IKeystrokeStatsSink sink, Func<long> uptimeMs, Func<long> wallClockMs)
    {
        _sink = sink;
        _uptimeMs = uptimeMs;
        _wallClockMs = wallClockMs;
    }

    public bool IsActive => _session != null;

    public void BeginSession()
    {
        if (IsActive) EndSession();
        _session = Guid.NewGuid();
        _sessionStartWall = _wallClockMs();
        _downUptime = null;
        _lastUpUptime = null;
        _firstDownUptime = null;
        _keystrokes = _textKeystrokes = _backspaces = _pauses = _suggestions = _autocorrections = 0;
    }

    public void KeyDown(long uptimeMs)
    {
        if (!IsActive) return;
        _downUptime = uptimeMs;
        _firstDownUptime ??= uptimeMs;
    }

    public void KeyUp(KeyCategory category, long uptimeMs)
    {
        if (_session is not { } session) return;

        long down = _downUptime ?? uptimeMs;
        int hold = (int)Math.Max(0, uptimeMs - down);

        int? flight = null;
        if (_lastUpUptime is { } lastUp)
        {
            long gap = down - lastUp;
            if (gap > PauseThresholdMs) _pauses++;
            if (gap <= FlightResetMs) flight = (int)gap;
        }

        _sink.Write(new KeystrokeEvent(ToWallClock(uptimeMs), category, hold, flight, session));

        _keystrokes++;
        if (category is KeyCategory.Letter or KeyCategory.Digit or KeyCategory.Punctuation or KeyCategory.Space)
        {
            _textKeystrokes++;
        }

        if (category == KeyCategory.Backspace) _backspaces++;
        if (category == KeyCategory.Suggestion) _suggestions++;

        _lastUpUptime = uptimeMs;
        _downUptime = null;
    }

    /// <summary>Vorschlag aus der Leiste angetippt.</summary>
    public void Suggestion(long downUptimeMs, long upUptimeMs)
    {
        KeyDown(downUptimeMs);
        KeyUp(KeyCategory.Suggestion, upUptimeMs);
    }

    public void CountAutocorrection()
    {
        if (IsActive) _autocorrections++;
    }

    public void EndSession()
    {
        if (_session is not { } session) return;

        long end = _wallClockMs();
        double activeMinutes = _firstDownUptime is { } first && _lastUpUptime is { } last && last > first
            ? (last - first) / 60000.0
            : 0;

        var summary = new SessionSummary(
            session,
            _sessionStartWall,
            end,
            _keystrokes,
            activeMinutes > 0 ? Math.Round(_textKeystrokes / activeMinutes, 1) : 0,
            _keystrokes > 0 ? Math.Round((double)_backspaces / _keystrokes, 3) : 0,
            _pauses,
            _suggestions,
            _autocorrections);

        if (_keystrokes > 0) _sink.Write(summary);
        _sink.Flush();
        _session = null;
    }

    long ToWallClock(long uptimeMs) => _wallClockMs() - (_uptimeMs() - uptimeMs);
}
