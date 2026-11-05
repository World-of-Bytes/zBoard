using System.Globalization;
using System.Text.Json;

namespace ZBoard.Stats;

/// <summary>Liest die JSON-Lines-Dateien von <see cref="JsonlStatsSink"/> wieder ein (für die Schnittstelle des Statistik-AddOns).</summary>
public static class StatsReader
{
    /// <summary>Anschläge ab <paramref name="sinceMs"/> (Unix-ms), älteste zuerst.</summary>
    public static List<KeystrokeEvent> ReadKeystrokes(string directory, long sinceMs)
    {
        List<KeystrokeEvent> result = [];
        foreach (var json in Lines(directory, sinceMs))
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.GetProperty("type").GetString() != "key") continue;

            long t = r.GetProperty("t").GetInt64();
            if (t < sinceMs) continue;
            if (!Enum.TryParse<KeyCategory>(r.GetProperty("cat").GetString(), out var category)) category = KeyCategory.Other;
            var flight = r.GetProperty("flight");
            result.Add(new KeystrokeEvent(
                t,
                category,
                r.GetProperty("hold").GetInt32(),
                flight.ValueKind == JsonValueKind.Null ? null : flight.GetInt32(),
                r.GetProperty("session").GetGuid()));
        }

        return result;
    }

    /// <summary>Sitzungen, die ab <paramref name="sinceMs"/> endeten, älteste zuerst.</summary>
    public static List<SessionSummary> ReadSessions(string directory, long sinceMs)
    {
        List<SessionSummary> result = [];
        foreach (var json in Lines(directory, sinceMs))
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.GetProperty("type").GetString() != "session") continue;

            long end = r.GetProperty("end").GetInt64();
            if (end < sinceMs) continue;
            result.Add(new SessionSummary(
                r.GetProperty("session").GetGuid(),
                r.GetProperty("start").GetInt64(),
                end,
                r.GetProperty("keystrokes").GetInt32(),
                r.GetProperty("cpm").GetDouble(),
                r.GetProperty("correctionRate").GetDouble(),
                r.GetProperty("pausesOver2s").GetInt32(),
                r.GetProperty("suggestions").GetInt32(),
                r.GetProperty("autocorrections").GetInt32()));
        }

        return result;
    }

    /// <summary>Zeilen aller Tagesdateien, die Daten ab dem Zeitpunkt enthalten können (ein Tag Puffer für Zeitzonen).</summary>
    static IEnumerable<string> Lines(string directory, long sinceMs)
    {
        if (!Directory.Exists(directory)) yield break;
        var firstDay = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0, sinceMs)).UtcDateTime.Date.AddDays(-1);

        var files = Directory.EnumerateFiles(directory, "keystrokes-*.jsonl")
            .Select(path => (Path: path, Name: System.IO.Path.GetFileNameWithoutExtension(path)["keystrokes-".Length..]))
            .Where(f => DateTime.TryParseExact(f.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                        && d >= firstDay)
            .OrderBy(f => f.Name, StringComparer.Ordinal);

        foreach (var (path, _) in files)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line)) yield return line;
            }
        }
    }
}
