using System.Text;

namespace ZBoard.Clipboard;

public sealed record ClipItem(string Text, long TimestampMs, bool Pinned);

/// <summary>
/// Verlauf der Zwischenablage. Unangeheftete Einträge verfallen nach einer Stunde und werden nie gespeichert;
/// angeheftete bleiben (auch über Neustarts) bis zum Löschen. Plattformunabhängig.
/// </summary>
public sealed class ClipboardHistory
{
    public const long ExpiryMs = 60 * 60 * 1000;
    const int MaxRecent = 20;
    const int MaxLength = 5000;

    readonly Lock _lock = new();
    readonly List<ClipItem> _items = [];

    /// <summary>Angeheftete zuerst, sonst neueste zuerst.</summary>
    public IReadOnlyList<ClipItem> Items(long nowMs)
    {
        lock (_lock)
        {
            Expire(nowMs);
            return [.. _items.OrderByDescending(i => i.Pinned).ThenByDescending(i => i.TimestampMs)];
        }
    }

    /// <summary>Jüngster unangehefteter Eintrag, wenn er jünger als <paramref name="maxAgeMs"/> ist (für den Vorschlag).</summary>
    public ClipItem? Latest(long nowMs, long maxAgeMs)
    {
        lock (_lock)
        {
            Expire(nowMs);
            var latest = _items.MaxBy(i => i.TimestampMs);
            return latest != null && nowMs - latest.TimestampMs <= maxAgeMs ? latest : null;
        }
    }

    public void Add(string text, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxLength) text = text[..MaxLength];

        lock (_lock)
        {
            int existing = _items.FindIndex(i => i.Text == text);
            bool pinned = existing >= 0 && _items[existing].Pinned;
            if (existing >= 0) _items.RemoveAt(existing);
            _items.Add(new ClipItem(text, nowMs, pinned));

            var unpinned = _items.Where(i => !i.Pinned).OrderBy(i => i.TimestampMs).ToList();
            foreach (var old in unpinned.Take(Math.Max(0, unpinned.Count - MaxRecent))) _items.Remove(old);
        }
    }

    public void SetPinned(string text, bool pinned)
    {
        lock (_lock)
        {
            int i = _items.FindIndex(x => x.Text == text);
            if (i >= 0) _items[i] = _items[i] with { Pinned = pinned };
        }
    }

    public void Remove(string text)
    {
        lock (_lock) _items.RemoveAll(i => i.Text == text);
    }

    public void ClearUnpinned()
    {
        lock (_lock) _items.RemoveAll(i => !i.Pinned);
    }

    void Expire(long nowMs) => _items.RemoveAll(i => !i.Pinned && nowMs - i.TimestampMs > ExpiryMs);

    // ---------- Nur Angeheftetes wird gespeichert ----------

    public string SerializePinned()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            foreach (var item in _items.Where(i => i.Pinned))
            {
                sb.Append(item.TimestampMs).Append('\t').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Text))).Append('\n');
            }

            return sb.ToString();
        }
    }

    public static ClipboardHistory DeserializePinned(string? data)
    {
        var history = new ClipboardHistory();
        if (string.IsNullOrEmpty(data)) return history;
        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split('\t');
            if (p.Length != 2 || !long.TryParse(p[0], out var ts)) continue;
            try
            {
                history._items.Add(new ClipItem(Encoding.UTF8.GetString(Convert.FromBase64String(p[1])), ts, true));
            }
            catch (FormatException)
            {
                // beschädigter Eintrag: überspringen
            }
        }

        return history;
    }
}
