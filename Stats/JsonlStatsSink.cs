using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ZBoard.Stats;

/// <summary>
/// Schreibt Statistik als JSON Lines in den app-privaten Speicher, eine Datei pro Tag (keystrokes-JJJJ-MM-TT.jsonl).
/// Zeilen mit "type":"key" sind Anschläge, "type":"session" Sitzungszusammenfassungen.
/// </summary>
public sealed class JsonlStatsSink : IKeystrokeStatsSink
{
    const string Prefix = "keystrokes-";
    const string Extension = ".jsonl";
    const int FlushThreshold = 64;

    readonly string _directory;
    readonly List<string> _buffer = [];
    readonly Lock _lock = new();

    public JsonlStatsSink(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Write(KeystrokeEvent keystroke)
    {
        var json = ToJson(w =>
        {
            w.WriteString("type", "key");
            w.WriteNumber("t", keystroke.TimestampMs);
            w.WriteString("cat", keystroke.Category.ToString());
            w.WriteNumber("hold", keystroke.HoldMs);
            if (keystroke.FlightMs is { } flight) w.WriteNumber("flight", flight);
            else w.WriteNull("flight");
            w.WriteString("session", keystroke.SessionId);
        });
        Enqueue(json);
    }

    public void Write(SessionSummary summary)
    {
        var json = ToJson(w =>
        {
            w.WriteString("type", "session");
            w.WriteString("session", summary.SessionId);
            w.WriteNumber("start", summary.StartMs);
            w.WriteNumber("end", summary.EndMs);
            w.WriteNumber("keystrokes", summary.Keystrokes);
            w.WriteNumber("cpm", summary.CharsPerMinute);
            w.WriteNumber("correctionRate", summary.CorrectionRate);
            w.WriteNumber("pausesOver2s", summary.PausesOver2s);
            w.WriteNumber("suggestions", summary.SuggestionsAccepted);
            w.WriteNumber("autocorrections", summary.Autocorrections);
        });
        Enqueue(json);
    }

    public void Flush()
    {
        lock (_lock) FlushLocked();
    }

    /// <summary>Löscht Dateien, die älter als <paramref name="retentionDays"/> Tage sind.</summary>
    public void Purge(int retentionDays)
    {
        var cutoff = DateTime.Today.AddDays(-retentionDays);
        foreach (var (path, date) in Files())
        {
            if (date < cutoff) File.Delete(path);
        }
    }

    public void DeleteAll()
    {
        lock (_lock)
        {
            _buffer.Clear();
            foreach (var (path, _) in Files()) File.Delete(path);
        }
    }

    public int FileCount => Files().Count();

    /// <summary>Hängt alle Tagesdateien (älteste zuerst) in <paramref name="target"/> an.</summary>
    public void ExportTo(Stream target)
    {
        Flush();
        foreach (var (path, _) in Files().OrderBy(f => f.Date))
        {
            using var source = File.OpenRead(path);
            source.CopyTo(target);
        }
    }

    void Enqueue(string json)
    {
        lock (_lock)
        {
            _buffer.Add(json);
            if (_buffer.Count >= FlushThreshold) FlushLocked();
        }
    }

    void FlushLocked()
    {
        if (_buffer.Count == 0) return;
        var path = Path.Combine(_directory, $"{Prefix}{DateTime.Now:yyyy-MM-dd}{Extension}");
        File.AppendAllLines(path, _buffer);
        _buffer.Clear();
    }

    IEnumerable<(string Path, DateTime Date)> Files()
    {
        if (!Directory.Exists(_directory)) yield break;
        foreach (var path in Directory.EnumerateFiles(_directory, Prefix + "*" + Extension))
        {
            var name = Path.GetFileNameWithoutExtension(path)[Prefix.Length..];
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                yield return (path, date);
            }
        }
    }

    static string ToJson(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
