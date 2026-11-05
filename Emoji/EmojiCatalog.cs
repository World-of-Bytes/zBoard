namespace ZBoard.Emoji;

/// <param name="Group">Kategorie 0–8 in der Reihenfolge von <see cref="EmojiCatalog.GroupIcons"/>.</param>
/// <param name="SkinTones">Hauttöne möglich (👍 → 👍🏽).</param>
public sealed record EmojiEntry(string Emoji, int Group, bool SkinTones);

/// <summary>
/// Alle Emoji in Unicode-Reihenfolge, nach Kategorien, mit Namen und Suchbegriffen in der Tastatursprache.
/// Plattformunabhängig; welche Emoji das Gerät darstellen kann, filtert die Ansicht.
/// </summary>
public sealed class EmojiCatalog
{
    /// <summary>Symbole für die Kategorien-Leiste (Index = Gruppe).</summary>
    public static readonly string[] GroupIcons = ["😀", "👋", "🐻", "🍔", "🚗", "⚽", "💡", "❤", "🏳"];

    static readonly string[] SkinToneModifiers = ["\U0001F3FB", "\U0001F3FC", "\U0001F3FD", "\U0001F3FE", "\U0001F3FF"];

    readonly List<EmojiEntry> _entries = [];
    readonly Dictionary<string, (string Name, string[] Keywords)> _annotations = new(StringComparer.Ordinal);

    public IReadOnlyList<EmojiEntry> Entries => _entries;

    /// <summary>Lädt emoji.txt ("gruppe[Tab]emoji[Tab]hauttöne") und optional keywords.&lt;sprache&gt;.txt.</summary>
    public static EmojiCatalog Load(Stream emoji, Stream? keywords)
    {
        var catalog = new EmojiCatalog();
        using (var reader = new StreamReader(emoji))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                if (p.Length == 3 && int.TryParse(p[0], out var group))
                {
                    catalog._entries.Add(new EmojiEntry(p[1], group, p[2] == "1"));
                }
            }
        }

        if (keywords != null)
        {
            using var reader = new StreamReader(keywords);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                if (p.Length == 3)
                {
                    catalog._annotations[p[0]] = (p[1], p[2].Split('|', StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        return catalog;
    }

    public IEnumerable<EmojiEntry> InGroup(int group) => _entries.Where(e => e.Group == group);

    public string? NameOf(string emoji) => _annotations.TryGetValue(emoji, out var a) ? a.Name : null;

    /// <summary>
    /// Emoji passend zur Suche: zuerst exakte Stichwörter, dann Stichwörter oder Namen, die mit der Suche beginnen,
    /// zuletzt Namen, die die Suche enthalten. Groß-/Kleinschreibung egal.
    /// </summary>
    public IReadOnlyList<string> Search(string query, int max = 24)
    {
        var q = query.Trim().ToLowerInvariant();
        if (q.Length == 0) return [];

        List<(int Rank, int Order, string Emoji)> hits = [];
        for (int i = 0; i < _entries.Count; i++)
        {
            var emoji = _entries[i].Emoji;
            if (!_annotations.TryGetValue(emoji, out var a)) continue;

            var name = a.Name.ToLowerInvariant();
            int rank = int.MaxValue;
            foreach (var keyword in a.Keywords)
            {
                var k = keyword.ToLowerInvariant();
                if (k == q) rank = Math.Min(rank, 0);
                else if (k.StartsWith(q, StringComparison.Ordinal)) rank = Math.Min(rank, 2);
            }

            if (name == q) rank = Math.Min(rank, 1);
            else if (name.Split(' ').Any(w => w.StartsWith(q, StringComparison.Ordinal))) rank = Math.Min(rank, 2);
            else if (name.Contains(q, StringComparison.Ordinal)) rank = Math.Min(rank, 3);

            if (rank != int.MaxValue) hits.Add((rank, i, emoji));
        }

        return [.. hits.OrderBy(h => h.Rank).ThenBy(h => h.Order).Select(h => h.Emoji).Take(max)];
    }

    /// <summary>Grundform plus die fünf Hauttöne (Modifikator hinter dem ersten Zeichen).</summary>
    public static IReadOnlyList<string> SkinToneVariants(string emoji)
    {
        int firstLength = char.IsSurrogatePair(emoji, 0) ? 2 : 1;
        string head = emoji[..firstLength];
        string tail = emoji[firstLength..];
        if (tail.StartsWith('️')) tail = tail[1..]; // Variantenselektor entfällt vor dem Modifikator

        List<string> variants = [emoji];
        variants.AddRange(SkinToneModifiers.Select(m => head + m + tail));
        return variants;
    }
}

/// <summary>Zuletzt verwendete Emoji, neueste zuerst.</summary>
public sealed class RecentEmoji(int capacity = 32)
{
    readonly List<string> _items = [];

    public IReadOnlyList<string> Items => _items;

    public void Use(string emoji)
    {
        _items.Remove(emoji);
        _items.Insert(0, emoji);
        if (_items.Count > capacity) _items.RemoveAt(_items.Count - 1);
    }

    public string Serialize() => string.Join('\n', _items);

    public static RecentEmoji Deserialize(string? data, int capacity = 32)
    {
        var recent = new RecentEmoji(capacity);
        if (string.IsNullOrEmpty(data)) return recent;
        foreach (var e in data.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(capacity)) recent._items.Add(e);
        return recent;
    }
}
