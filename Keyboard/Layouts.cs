namespace ZBoard.Keyboard;

using static KeyDef;

/// <summary>Alle Tastaturlayouts. Neue Sprachen bekommen hier eine eigene Buchstabenebene.</summary>
public static class Layouts
{
    public const string Symbols1 = "symbols1";
    public const string Symbols2 = "symbols2";

    /// <summary>Unterstützte Sprachen in Anzeigereihenfolge.</summary>
    public static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>
    {
        ["de"] = "Deutsch",
        ["en"] = "English",
        ["fr"] = "Français",
        ["es"] = "Español",
        ["it"] = "Italiano",
    };

    /// <summary>Gebietsschema für die Spracheingabe.</summary>
    public static string LocaleTag(string language) => language switch
    {
        "de" => "de-DE",
        "fr" => "fr-FR",
        "es" => "es-ES",
        "it" => "it-IT",
        _ => "en-US",
    };

    /// <summary>Buchstabenebene; mit <paramref name="numberRow"/> steht eine Ziffernreihe darüber.</summary>
    public static KeyboardLayout Alphabet(string language, bool showLanguageKey, bool numberRow = false)
    {
        var (rows, units) = language switch
        {
            "en" => (Qwerty(), 10f),
            "fr" => (Azerty(), 10f),
            "es" => (Spanish(), 10f),
            "it" => (Italian(), 10f),
            _ => (Qwertz(), 11f),
        };

        List<KeyRow> all = [];
        if (numberRow) all.Add(NumberRow(units));
        all.AddRange(rows);
        all.Add(BottomRow(KeyAction.ToSymbols, showLanguageKey));
        return new KeyboardLayout(language, true, all);
    }

    public static KeyboardLayout Symbols(int page, bool showLanguageKey) =>
        page == 2 ? SymbolsPage2(showLanguageKey) : SymbolsPage1(showLanguageKey);

    // ---------- Buchstabenreihen ----------

    static KeyRow[] Qwertz() =>
    [
        new KeyRow(
        [
            Char("q", "1"), Char("w", "2"), Char("e", "3", "é", "è", "ê", "ë", "ē"), Char("r", "4"), Char("t", "5"),
            Char("z", "6"), Char("u", "7", "ú", "ù", "û", "ū"), Char("i", "8", "í", "ì", "î", "ï"),
            Char("o", "9", "ó", "ò", "ô", "õ", "ø", "œ"), Char("p", "0"), Char("ü"),
        ]),
        new KeyRow(
        [
            Char("a", "à", "á", "â", "ã", "å", "æ", "ā"), Char("s", "ß", "ś", "š"), Char("d"), Char("f"), Char("g"),
            Char("h"), Char("j"), Char("k"), Char("l"), Char("ö"), Char("ä"),
        ]),
        new KeyRow(
        [
            Special(KeyAction.Shift, 2f), Char("y", "ý", "ÿ"), Char("x"), Char("c", "ç", "ć", "č"), Char("v"), Char("b"),
            Char("n", "ñ", "ń"), Char("m"), Special(KeyAction.Delete, 2f),
        ]),
    ];

    static KeyRow[] Qwerty() =>
    [
        new KeyRow(
        [
            Char("q", "1"), Char("w", "2"), Char("e", "3", "é", "è", "ê", "ë"), Char("r", "4"), Char("t", "5"),
            Char("y", "6"), Char("u", "7", "ú", "ü", "û"), Char("i", "8", "í", "ï", "î"), Char("o", "9", "ó", "ö", "ô", "ø"),
            Char("p", "0"),
        ]),
        new KeyRow(
        [
            Char("a", "á", "à", "â", "ä", "æ"), Char("s", "ß"), Char("d"), Char("f"), Char("g"), Char("h"), Char("j"),
            Char("k"), Char("l"),
        ], Indent: 0.5f),
        new KeyRow(
        [
            Special(KeyAction.Shift, 1.5f), Char("z"), Char("x"), Char("c", "ç"), Char("v"), Char("b"), Char("n", "ñ"),
            Char("m"), Special(KeyAction.Delete, 1.5f),
        ]),
    ];

    static KeyRow[] Azerty() =>
    [
        new KeyRow(
        [
            Char("a", "1", "à", "â", "æ", "á", "ä"), Char("z", "2"), Char("e", "3", "é", "è", "ê", "ë"), Char("r", "4"),
            Char("t", "5"), Char("y", "6", "ÿ"), Char("u", "7", "ù", "û", "ü"), Char("i", "8", "î", "ï"),
            Char("o", "9", "ô", "œ", "ö"), Char("p", "0"),
        ]),
        new KeyRow(
        [
            Char("q"), Char("s"), Char("d"), Char("f"), Char("g"), Char("h"), Char("j"), Char("k"), Char("l"), Char("m"),
        ]),
        new KeyRow(
        [
            Special(KeyAction.Shift, 1.5f), Char("w"), Char("x"), Char("c", "ç"), Char("v"), Char("b"), Char("n", "ñ"),
            Char("'", "’", "\""), Special(KeyAction.Delete, 1.5f),
        ]),
    ];

    static KeyRow[] Spanish() =>
    [
        new KeyRow(
        [
            Char("q", "1"), Char("w", "2"), Char("e", "3", "é", "è", "ë"), Char("r", "4"), Char("t", "5"),
            Char("y", "6"), Char("u", "7", "ú", "ü", "ù"), Char("i", "8", "í", "ï"), Char("o", "9", "ó", "ò", "º"),
            Char("p", "0"),
        ]),
        new KeyRow(
        [
            Char("a", "á", "à", "ª"), Char("s"), Char("d"), Char("f"), Char("g"), Char("h"), Char("j"), Char("k"),
            Char("l"), Char("ñ"),
        ]),
        new KeyRow(
        [
            Special(KeyAction.Shift, 1.5f), Char("z"), Char("x"), Char("c", "ç"), Char("v"), Char("b"), Char("n"),
            Char("m"), Special(KeyAction.Delete, 1.5f),
        ]),
    ];

    static KeyRow[] Italian() =>
    [
        new KeyRow(
        [
            Char("q", "1"), Char("w", "2"), Char("e", "3", "è", "é"), Char("r", "4"), Char("t", "5"),
            Char("y", "6"), Char("u", "7", "ù", "ú"), Char("i", "8", "ì", "í"), Char("o", "9", "ò", "ó"),
            Char("p", "0"),
        ]),
        new KeyRow(
        [
            Char("a", "à", "á"), Char("s"), Char("d"), Char("f"), Char("g"), Char("h"), Char("j"), Char("k"), Char("l"),
        ], Indent: 0.5f),
        new KeyRow(
        [
            Special(KeyAction.Shift, 1.5f), Char("z"), Char("x"), Char("c"), Char("v"), Char("b"), Char("n"),
            Char("m"), Special(KeyAction.Delete, 1.5f),
        ]),
    ];

    /// <summary>Ziffernreihe über den Buchstaben; bei 11 Einheiten (QWERTZ) mittig eingerückt.</summary>
    static KeyRow NumberRow(float units) => new(
    [
        Char("1", "¹", "½", "⅓", "¼"), Char("2", "²", "⅔"), Char("3", "³", "¾"), Char("4", "⁴"), Char("5"),
        Char("6"), Char("7"), Char("8"), Char("9"), Char("0", "ⁿ", "∅"),
    ], Indent: (units - 10f) / 2);

    // ---------- Symbole ----------

    static KeyboardLayout SymbolsPage1(bool showLanguageKey) => new(Symbols1, false,
    [
        new KeyRow(
        [
            Char("1", "¹", "½", "⅓", "¼"), Char("2", "²", "⅔"), Char("3", "³", "¾"), Char("4", "⁴"), Char("5"),
            Char("6"), Char("7"), Char("8"), Char("9"), Char("0", "ⁿ", "∅"),
        ]),
        new KeyRow(
        [
            Char("@"), Char("#"), Char("€", "$", "£", "¥", "¢"), Char("_"), Char("&"), Char("-", "–", "—", "·"),
            Char("+", "±"), Char("(", "<", "{", "["), Char(")", ">", "}", "]"), Char("/", "\\"),
        ]),
        new KeyRow(
        [
            Special(KeyAction.ToSymbols2, 1.5f), Char("*", "★", "†"), Char("\"", "„", "“", "”", "«", "»"),
            Char("'", "‚", "‘", "’", "‹", "›"), Char(":"), Char(";"), Char("!", "¡"), Char("?", "¿"),
            Special(KeyAction.Delete, 1.5f),
        ]),
        BottomRow(KeyAction.ToAlpha, showLanguageKey),
    ]);

    static KeyboardLayout SymbolsPage2(bool showLanguageKey) => new(Symbols2, false,
    [
        new KeyRow(
        [
            Char("~"), Char("`"), Char("|"), Char("•", "♣", "♠", "♪", "♥", "♦"), Char("√"), Char("π", "Π"),
            Char("÷"), Char("×"), Char("¶", "§"), Char("∆"),
        ]),
        new KeyRow(
        [
            Char("£"), Char("¥"), Char("$"), Char("¢"), Char("^", "↑", "↓", "←", "→"), Char("°", "′", "″"),
            Char("=", "≠", "≈", "∞"), Char("{"), Char("}"), Char("\\"),
        ]),
        new KeyRow(
        [
            Special(KeyAction.ToSymbols, 1.5f), Char("%", "‰"), Char("©"), Char("®"), Char("™"), Char("✓"),
            Char("["), Char("]"), Special(KeyAction.Delete, 1.5f),
        ]),
        BottomRow(KeyAction.ToAlpha, showLanguageKey),
    ]);

    /// <summary>Untere Reihe: Ebenen-Taste, Emoji, Komma, (Sprache), Leertaste, Punkt, Enter.</summary>
    static KeyRow BottomRow(KeyAction modeKey, bool showLanguageKey)
    {
        List<KeyDef> keys =
        [
            Special(modeKey, 1.5f),
            Special(KeyAction.Emoji, 1f),
            Char(","),
        ];
        if (showLanguageKey) keys.Add(Special(KeyAction.Language, 1f));

        keys.Add(Fill(KeyAction.Space));
        keys.Add(Char(".", "?", "!", ",", ";", ":", "…", "-", "'", "\""));
        keys.Add(Special(KeyAction.Enter, 1.5f));
        return new KeyRow(keys);
    }
}
