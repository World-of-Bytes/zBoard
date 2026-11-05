namespace ZBoard.Ime;

/// <summary>Wortgrenzen im Text vor dem Cursor. Plattformunabhängig.</summary>
public static class TextBoundaries
{
    /// <summary>
    /// Wie viele Zeichen am Ende von <paramref name="text"/> die letzten <paramref name="words"/> Wörter
    /// samt Leerraum einnehmen. Ein Satzzeichen zählt als eigenes Wort.
    /// </summary>
    public static int CharsForWords(string text, int words)
    {
        int i = text.Length;
        for (int w = 0; w < words && i > 0; w++)
        {
            while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;
            int wordEnd = i;
            while (i > 0 && IsWordChar(text[i - 1])) i--;
            if (i == wordEnd && i > 0) i--;
        }

        return text.Length - i;
    }

    static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '\'' or '-';
}
