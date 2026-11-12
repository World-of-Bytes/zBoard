using System.Numerics;
using System.Text;
using ZBoard.Keyboard;

namespace ZBoard.Tools.SuggestionEval;

/// <summary>
/// Simuliert ungenaues Tippen. Unabhängig vom Fehlermodell der Autokorrektur: Jeder Tastendruck landet normalverteilt
/// um die Tastenmitte, die nächstgelegene Buchstabentaste zählt. Dazu kommen seltene Fehler, die nichts mit der
/// Treffgenauigkeit zu tun haben (Buchstabe ausgelassen, doppelt, zwei vertauscht), und Umlaute/Akzente, die als
/// Grundbuchstabe getippt werden ("fur" statt "für").
/// </summary>
public sealed class TypingNoise
{
    /// <summary>Höhe einer Tastenreihe in Tastenbreiten (Hochformat, etwa 54 dp zu 36 dp).</summary>
    public const float RowHeight = 1.5f;

    readonly Dictionary<char, Vector2> _keys = [];
    readonly Random _random;

    /// <summary>Streuung der Fingerposition in Tastenbreiten (0,24 ≈ 4 % daneben getippte Buchstaben).</summary>
    public double Sigma { get; init; } = 0.24;

    public double OmitRate { get; init; } = 0.004;
    public double DoubleRate { get; init; } = 0.003;
    public double SwapRate { get; init; } = 0.004;

    /// <summary>Anteil der Umlaute und Akzentbuchstaben, die als Grundbuchstabe getippt werden.</summary>
    public double LazyAccentRate { get; init; } = 0.3;

    public TypingNoise(KeyboardLayout layout, int seed)
    {
        _random = new Random(seed);
        for (int r = 0; r < layout.Rows.Count; r++)
        {
            var row = layout.Rows[r];
            float x = row.Indent;
            foreach (var key in row.Keys)
            {
                float w = key.Width > 0 ? key.Width : 1f;
                if (key.IsCharacter && key.Label.Length == 1 && char.IsLetter(key.Label[0]))
                {
                    _keys[char.ToLowerInvariant(key.Label[0])] = new Vector2(x + w / 2, r * RowHeight);
                }

                x += w;
            }
        }
    }

    /// <summary>Tippt <paramref name="intended"/> mit Fehlern.</summary>
    public string Type(string intended)
    {
        var typed = new StringBuilder(intended.Length + 2);
        foreach (char original in intended)
        {
            if (_random.NextDouble() < OmitRate) continue;

            char c = original;
            char lower = char.ToLowerInvariant(c);
            if (!_keys.ContainsKey(lower) && BaseLetter(lower) is { } plain && _random.NextDouble() < LazyAccentRate)
            {
                c = char.IsUpper(c) ? char.ToUpperInvariant(plain) : plain;
                lower = plain;
            }

            if (_keys.TryGetValue(lower, out var center))
            {
                char hit = NearestKey(center + new Vector2((float)(Gaussian() * Sigma), (float)(Gaussian() * Sigma)));
                c = char.IsUpper(c) ? char.ToUpperInvariant(hit) : hit;
            }

            typed.Append(c);
            if (_random.NextDouble() < DoubleRate) typed.Append(char.ToLowerInvariant(c));
        }

        for (int i = 0; i + 1 < typed.Length; i++)
        {
            if (_random.NextDouble() >= SwapRate || !char.IsLower(typed[i]) || !char.IsLower(typed[i + 1])) continue;
            (typed[i], typed[i + 1]) = (typed[i + 1], typed[i]);
            i++;
        }

        return typed.ToString();
    }

    char NearestKey(Vector2 point)
    {
        char best = ' ';
        float bestDistance = float.MaxValue;
        foreach (var (key, center) in _keys)
        {
            float d = Vector2.DistanceSquared(point, center);
            if (d < bestDistance) (best, bestDistance) = (key, d);
        }

        return best;
    }

    /// <summary>Grundbuchstabe eines Umlauts oder Akzentbuchstabens ("ü" → "u", "é" → "e", "ß" → "s"), sonst null.</summary>
    static char? BaseLetter(char c)
    {
        const string Accented = "äàâáãåöòôóõüùûúéèêëíìîïñçß";
        const string Plain = "aaaaaaooooouuuueeeeiiiincs";
        int i = Accented.IndexOf(c);
        return i < 0 ? null : Plain[i];
    }

    double Gaussian()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
