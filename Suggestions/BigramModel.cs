namespace ZBoard.Suggestions;

/// <summary>
/// Mitgelieferte Wortpaar-Häufigkeiten ("ich" → "habe", "bin", …) für die Nächstes-Wort-Vorhersage.
/// Plattformunabhängig.
/// </summary>
public sealed class BigramModel
{
    const int MaxFollowersPerWord = 12;

    readonly Dictionary<string, List<(string Next, int Freq)>> _followers = new(StringComparer.Ordinal);
    bool _sealed;

    public static readonly BigramModel Empty = new BigramModel().Seal();

    /// <summary>Lädt "erstes[Tab]zweites[Tab]häufigkeit"; Zeilen mit # werden ignoriert.</summary>
    public static BigramModel Load(Stream stream)
    {
        var model = new BigramModel();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length != 3 || !int.TryParse(parts[2], out var freq)) continue;
            model.Add(parts[0], parts[1], freq);
        }

        return model.Seal();
    }

    public void Add(string previous, string next, int freq)
    {
        if (_sealed) throw new InvalidOperationException("Modell ist bereits abgeschlossen.");
        var key = previous.ToLowerInvariant();
        if (!_followers.TryGetValue(key, out var list))
        {
            list = [];
            _followers[key] = list;
        }

        int existing = list.FindIndex(x => x.Next == next);
        if (existing < 0) list.Add((next, freq));
        else list[existing] = (next, list[existing].Freq + freq);
    }

    /// <summary>Sortiert und kürzt die Listen. Danach sind keine Änderungen mehr möglich.</summary>
    public BigramModel Seal()
    {
        foreach (var key in _followers.Keys.ToList())
        {
            _followers[key] = _followers[key]
                .OrderByDescending(x => x.Freq)
                .Take(MaxFollowersPerWord)
                .ToList();
        }

        _sealed = true;
        return this;
    }

    /// <summary>Häufigste Folgewörter von <paramref name="previous"/>, häufigstes zuerst.</summary>
    public IReadOnlyList<(string Next, int Freq)> Followers(string previous) =>
        _followers.TryGetValue(previous.ToLowerInvariant(), out var list)
            ? list
            : [];
}
