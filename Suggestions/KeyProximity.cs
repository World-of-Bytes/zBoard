using ZBoard.Keyboard;

namespace ZBoard.Suggestions;

/// <summary>
/// Ersetzungskosten für die Autokorrektur: Nachbartasten und Umlaut-Varianten sind "billiger" als beliebige Buchstaben,
/// weil sie typische Tippfehler sind ("fur" → "für", "tesr" → "test").
/// </summary>
public sealed class KeyProximity
{
    const float NeighborCost = 0.5f;
    const float VariantCost = 0.3f;
    const float DefaultCost = 1f;

    static readonly (char A, char B)[] Variants =
    {
        ('a', 'ä'), ('o', 'ö'), ('u', 'ü'), ('s', 'ß'),
        ('e', 'é'), ('e', 'è'), ('a', 'à'), ('c', 'ç'), ('n', 'ñ'),
    };

    readonly Dictionary<char, (int Row, float X)> _positions = new();

    public static KeyProximity FromLayout(KeyboardLayout layout)
    {
        var proximity = new KeyProximity();
        for (int r = 0; r < layout.Rows.Count; r++)
        {
            var row = layout.Rows[r];
            float x = row.Indent;
            foreach (var key in row.Keys)
            {
                float w = key.Width > 0 ? key.Width : 1f;
                if (key.IsCharacter && key.Label.Length == 1 && char.IsLetter(key.Label[0]))
                {
                    proximity._positions[key.Label[0]] = (r, x + w / 2);
                }

                x += w;
            }
        }

        return proximity;
    }

    public float SubstitutionCost(char a, char b)
    {
        if (a == b) return 0f;
        foreach (var (va, vb) in Variants)
        {
            if ((a == va && b == vb) || (a == vb && b == va)) return VariantCost;
        }

        if (_positions.TryGetValue(a, out var pa) && _positions.TryGetValue(b, out var pb))
        {
            int rowDistance = Math.Abs(pa.Row - pb.Row);
            float dx = Math.Abs(pa.X - pb.X);
            if (rowDistance == 0 && dx <= 1.01f) return NeighborCost;
            if (rowDistance == 1 && dx <= 1.01f) return NeighborCost;
        }

        return DefaultCost;
    }
}
