namespace ZBoard.Suggestions;

/// <summary>
/// Ein Vorschlag der Pinyin-Eingabe: die Schriftzeichen und wie viele der getippten Buchstaben sie ersetzen.
/// Bleiben Buchstaben übrig ("nihaoma" → "你好" ersetzt fünf), tippt man mit dem Rest weiter.
/// </summary>
public readonly record struct PinyinCandidate(string Text, int Consumed);

/// <summary>
/// Chinesisch tippen wie bei Gboard: Pinyin ohne Tonzeichen auf QWERTY (ü als v), die Schriftzeichen wählt man aus
/// den Vorschlägen. Die Tabelle (Assets/dict/zh.pinyin.txt) ordnet jeder Pinyin-Schreibweise Wörter in vereinfachten
/// Schriftzeichen mit ihrer Häufigkeit zu. Plattformunabhängig.
/// </summary>
public sealed class PinyinEngine
{
    /// <summary>So viele Wörter derselben kürzeren Schreibweise kommen höchstens in die Liste (außer einzelnen Silben).</summary>
    const int PerPrefix = 6;

    /// <summary>So viele Wörter, die mit dem Getippten erst beginnen ("zhon" → 中国), kommen höchstens in die Liste.</summary>
    const int MaxCompletions = 8;

    readonly string[] _keys;  // Pinyin, ordinal sortiert; bei gleicher Schreibweise das häufigste Wort zuerst
    readonly string[] _words;
    readonly int[] _freq;
    readonly int _longestKey;

    PinyinEngine(string[] keys, string[] words, int[] freq)
    {
        _keys = keys;
        _words = words;
        _freq = freq;
        _longestKey = keys.Length == 0 ? 0 : keys.Max(k => k.Length);
    }

    public int Count => _keys.Length;

    /// <summary>Lädt die Tabelle: "pinyin[Tab]wort[Tab]häufigkeit" je Zeile, Zeilen mit # werden ignoriert.</summary>
    public static PinyinEngine Load(Stream stream)
    {
        var entries = new List<(string Key, string Word, int Freq)>(70_000);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0) continue;
            entries.Add((parts[0], parts[1], parts.Length > 2 && int.TryParse(parts[2], out int f) ? f : 1));
        }

        return FromEntries(entries);
    }

    public static PinyinEngine FromEntries(IEnumerable<(string Key, string Word, int Freq)> entries)
    {
        var sorted = entries
            .OrderBy(e => e.Key, StringComparer.Ordinal).ThenByDescending(e => e.Freq).ThenBy(e => e.Word, StringComparer.Ordinal)
            .ToArray();
        return new PinyinEngine([.. sorted.Select(e => e.Key)], [.. sorted.Select(e => e.Word)], [.. sorted.Select(e => e.Freq)]);
    }

    /// <summary>
    /// Ist die Eingabe Pinyin, also nur Buchstaben a–z? Anderes (Ziffern, Bindestrich) bleibt, wie es getippt wurde.
    /// </summary>
    public static bool IsPinyin(string typed) =>
        typed.Length > 0 && typed.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));

    /// <summary>
    /// Vorschläge für die getippten Buchstaben, bester zuerst: Wörter, die genau so geschrieben werden; sonst der
    /// ganze Satz aus den längsten passenden Wörtern; dann Wörter, die so beginnen; dann Wörter für den Anfang der
    /// Eingabe, längere zuerst. Leer, wenn die Eingabe kein Pinyin ist oder nichts passt.
    /// </summary>
    public IReadOnlyList<PinyinCandidate> Candidates(string typed, int max = 30)
    {
        if (!IsPinyin(typed) || max <= 0) return [];
        string letters = typed.ToLowerInvariant();
        var result = new List<PinyinCandidate>(max);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string text, int consumed)
        {
            if (result.Count < max && seen.Add(text)) result.Add(new PinyinCandidate(text, consumed));
        }

        var (start, end) = Range(letters);
        if (start == end)
        {
            // Kein Wort wird genau so geschrieben: der ganze Satz zuerst ("nihaoma" → 你好吗).
            var (sentence, consumed) = ConvertPrefix(letters);
            if (consumed == letters.Length) Add(sentence, consumed);
        }

        for (int i = start; i < end; i++) Add(_words[i], letters.Length);

        foreach (int i in Completions(letters, end)) Add(_words[i], letters.Length);

        for (int length = Math.Min(letters.Length - 1, _longestKey); length >= 1 && result.Count < max; length--)
        {
            var (from, to) = Range(letters[..length]);
            if (from == to) continue;

            // Einzelne Silben vollständig (davon gibt es viele gleichlautende Zeichen), längere Wörter nur die häufigsten.
            int limit = length <= 6 && _words[from].Length == 1 ? int.MaxValue : PerPrefix;
            for (int i = from; i < to && i - from < limit; i++) Add(_words[i], length);
        }

        return result;
    }

    /// <summary>
    /// Wandelt die ganze Eingabe um, jeweils mit dem häufigsten Wort der längsten passenden Schreibweise
    /// ("womenshipengyou" → 我们是朋友). Was sich nicht umwandeln lässt, bleibt als Buchstaben stehen.
    /// </summary>
    public string Convert(string typed)
    {
        if (!IsPinyin(typed)) return typed;
        var (text, consumed) = ConvertPrefix(typed.ToLowerInvariant());
        return consumed == 0 ? typed : text + typed[consumed..];
    }

    (string Text, int Consumed) ConvertPrefix(string letters)
    {
        var text = new System.Text.StringBuilder();
        int pos = 0;
        while (pos < letters.Length)
        {
            int matched = 0;
            for (int length = Math.Min(letters.Length - pos, _longestKey); length >= 1; length--)
            {
                var (from, to) = Range(letters.Substring(pos, length));
                if (from == to) continue;
                text.Append(_words[from]);
                matched = length;
                break;
            }

            if (matched == 0) break;
            pos += matched;
        }

        return (text.ToString(), pos);
    }

    /// <summary>Die häufigsten Wörter, deren Schreibweise mit <paramref name="letters"/> beginnt, aber länger ist.</summary>
    IEnumerable<int> Completions(string letters, int from)
    {
        var top = new List<int>(MaxCompletions + 1);
        for (int i = from; i < _keys.Length && _keys[i].StartsWith(letters, StringComparison.Ordinal); i++)
        {
            int at = top.FindIndex(j => _freq[j] < _freq[i]);
            if (at < 0 && top.Count >= MaxCompletions) continue;
            top.Insert(at < 0 ? top.Count : at, i);
            if (top.Count > MaxCompletions) top.RemoveAt(top.Count - 1);
        }

        return top;
    }

    /// <summary>Bereich der Einträge mit genau dieser Schreibweise: [Start, Ende).</summary>
    (int Start, int End) Range(string key)
    {
        int lo = 0, hi = _keys.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (string.CompareOrdinal(_keys[mid], key) < 0) lo = mid + 1;
            else hi = mid;
        }

        int end = lo;
        while (end < _keys.Length && _keys[end] == key) end++;
        return (lo, end);
    }
}
