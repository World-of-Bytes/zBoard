using System.Text;

namespace ZBoard.Tools.SuggestionEval;

/// <summary>Ein Wort des Testtexts mit dem, was die Tastatur an dieser Stelle über den Kontext wüsste.</summary>
/// <param name="Target">Das Wort, wie es im Text steht (so soll es am Ende dastehen).</param>
/// <param name="Previous">Vorheriges Wort, wenn dazwischen nur Leerraum steht (wie <c>PreviousWord()</c> im Tastaturdienst).</param>
/// <param name="SentenceStart">Am Satzanfang schaltet die Tastatur von selbst auf Großschreibung.</param>
public sealed record TestWord(string Target, string? Previous, bool SentenceStart)
{
    /// <summary>
    /// Was ein Nutzer fehlerfrei tippen würde: mitten im Satz alles klein (die Tastatur soll Substantive selbst
    /// großschreiben), am Satzanfang groß (automatische Umschalttaste).
    /// </summary>
    public string Intended => SentenceStart
        ? char.ToUpperInvariant(Target[0]) + Target[1..].ToLowerInvariant()
        : Target.ToLowerInvariant();
}

public static class Corpus
{
    /// <summary>Liest einen Text (ein Satz oder eine Nachricht pro Zeile, # leitet Kommentare ein) als Folge von Testwörtern.</summary>
    public static List<TestWord> Load(string path)
    {
        List<TestWord> words = [];
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            words.AddRange(Tokenize(line));
        }

        return words;
    }

    public static IEnumerable<TestWord> Tokenize(string line)
    {
        string? previous = null;   // null nach Satzzeichen und am Zeilenanfang
        bool sentenceStart = true;
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (char.IsLetterOrDigit(c))
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || IsInnerMark(line, i))) i++;
                string word = line[start..i];

                // Zahlen und Wörter mit Ziffern zählen als Kontext, werden aber nicht bewertet.
                if (word.All(ch => char.IsLetter(ch) || ch is '\'' or '-'))
                {
                    yield return new TestWord(word, previous, sentenceStart);
                }

                previous = word;
                sentenceStart = false;
                continue;
            }

            if (!char.IsWhiteSpace(c))
            {
                previous = null;
                if (c is '.' or '!' or '?') sentenceStart = true;
            }

            i++;
        }
    }

    /// <summary>Apostroph oder Bindestrich zwischen zwei Buchstaben gehört zum Wort ("don't", "E-Mail").</summary>
    static bool IsInnerMark(string line, int i) =>
        line[i] is '\'' or '’' or '-'
        && i > 0 && char.IsLetter(line[i - 1])
        && i + 1 < line.Length && char.IsLetter(line[i + 1]);
}
