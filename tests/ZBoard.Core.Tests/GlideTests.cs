using System.Numerics;
using ZBoard.Ime;
using ZBoard.Suggestions;
using Xunit;

namespace ZBoard.Core.Tests;

public class GlideTests
{
    const float RowHeight = 1.45f; // Tastenhöhe in Tastenbreiten (54 dp hoch, ~37 dp breit)

    /// <summary>Tastenmittelpunkte des deutschen Layouts in Tastenbreiten.</summary>
    static Dictionary<char, Vector2> QwertzCenters()
    {
        var keys = new Dictionary<char, Vector2>();
        (string Letters, float Offset)[] rows = [("qwertzuiopü", 0f), ("asdfghjklöä", 0f), ("yxcvbnm", 2f)];
        for (int r = 0; r < rows.Length; r++)
        {
            for (int i = 0; i < rows[r].Letters.Length; i++)
            {
                keys[rows[r].Letters[i]] = new Vector2(rows[r].Offset + i + 0.5f, (r + 0.5f) * RowHeight);
            }
        }

        return keys;
    }

    static List<Vector2> PathThrough(string letters, Dictionary<char, Vector2> keys, float wobble = 0f)
    {
        List<Vector2> path = [];
        var points = letters.Select(c => keys[c]).ToList();
        for (int i = 0; i < points.Count - 1; i++)
        {
            for (int s = 0; s < 8; s++)
            {
                var p = Vector2.Lerp(points[i], points[i + 1], s / 8f);
                path.Add(p + new Vector2(0, (s % 2 == 0 ? 1 : -1) * wobble));
            }
        }

        path.Add(points[^1]);
        return path;
    }

    static WordDictionary Dictionary(params (string Word, int Freq)[] words)
    {
        var dictionary = new WordDictionary();
        foreach (var (w, f) in words) dictionary.Add(w, f);
        dictionary.BuildTopLists();
        return dictionary;
    }

    [Fact]
    public void Decodes_WordAlongItsKeys()
    {
        var keys = QwertzCenters();
        var decoder = new GlideDecoder(Dictionary(("hallo", 100), ("halt", 50), ("hall", 20), ("Haus", 80)));

        var result = decoder.Decode(PathThrough("halo", keys, wobble: 0.15f), keys);

        Assert.Equal("hallo", result[0]);
    }

    [Fact]
    public void ReturnsDictionarySpelling_ForNouns()
    {
        var keys = QwertzCenters();
        var decoder = new GlideDecoder(Dictionary(("Haus", 80), ("hat", 90)));

        var result = decoder.Decode(PathThrough("haus", keys), keys);

        Assert.Equal("Haus", result[0]);
    }

    [Fact]
    public void TinyMovement_IsNoGlide()
    {
        var keys = QwertzCenters();
        var decoder = new GlideDecoder(Dictionary(("hallo", 100)));

        Assert.Empty(decoder.Decode([keys['h'], keys['h'] + new Vector2(0.2f, 0)], keys));
    }

    [Theory]
    [InlineData("Hallo Welt", 1, 4)]
    [InlineData("Hallo Welt ", 1, 5)]
    [InlineData("Hallo Welt", 2, 10)]
    [InlineData("Hallo, Welt", 2, 6)]
    [InlineData("Hallo", 3, 5)]
    public void CharsForWords_CountsWordsFromTheEnd(string text, int words, int expected)
    {
        Assert.Equal(expected, TextBoundaries.CharsForWords(text, words));
    }
}
