namespace ZBoard.Stats;

/// <summary>Art des Anschlags. Welche Taste genau gedrückt wurde, wird bewusst NICHT gespeichert.</summary>
public enum KeyCategory
{
    Letter,
    Digit,
    Punctuation,
    Space,
    Backspace,
    Enter,
    Shift,
    Suggestion,

    /// <summary>Wischgeste: Wischeingabe eines Wortes oder Wort-Löschen per Wischen.</summary>
    Gesture,
    Other,
}

/// <summary>Ein Tastenanschlag als reine Metadaten.</summary>
/// <param name="TimestampMs">Zeitpunkt des Loslassens (Unix-ms, UTC).</param>
/// <param name="Category">Art der Taste.</param>
/// <param name="HoldMs">Druckdauer.</param>
/// <param name="FlightMs">Zeit vom Loslassen der vorherigen bis zum Drücken dieser Taste; null nach einer Pause über 5 s.</param>
/// <param name="SessionId">Eingabesitzung.</param>
public sealed record KeystrokeEvent(long TimestampMs, KeyCategory Category, int HoldMs, int? FlightMs, Guid SessionId);

/// <summary>Zusammenfassung einer Eingabesitzung (Tastatur geöffnet bis geschlossen).</summary>
public sealed record SessionSummary(
    Guid SessionId,
    long StartMs,
    long EndMs,
    int Keystrokes,
    double CharsPerMinute,
    double CorrectionRate,
    int PausesOver2s,
    int SuggestionsAccepted,
    int Autocorrections);

/// <summary>Ziel für Statistikdaten. zHeal Meridian kann eine eigene Implementierung einhängen.</summary>
public interface IKeystrokeStatsSink
{
    void Write(KeystrokeEvent keystroke);

    void Write(SessionSummary summary);

    void Flush();
}
