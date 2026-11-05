namespace ZBoard.Keyboard;

/// <summary>Was eine Taste auslöst.</summary>
public enum KeyAction
{
    Character,
    Shift,
    Delete,
    Enter,
    Space,
    ToSymbols,
    ToSymbols2,
    ToAlpha,
    Language,
}

/// <summary>Eine Taste im Layout. Breite in Layout-Einheiten (Standardtaste = 1, 0 = füllt den Rest der Reihe).</summary>
/// <param name="Alternates">Zeichen, die per langem Druck erscheinen.</param>
public sealed record KeyDef(KeyAction Action, string Label, float Width, IReadOnlyList<string> Alternates)
{
    public bool IsCharacter => Action == KeyAction.Character;

    public static KeyDef Char(string label, params string[] alternates) =>
        new(KeyAction.Character, label, 1f, alternates);

    public static KeyDef Special(KeyAction action, float width) =>
        new(action, string.Empty, width, []);

    public static KeyDef Fill(KeyAction action) =>
        new(action, string.Empty, 0f, []);
}

/// <param name="Indent">Einrückung links und rechts in Einheiten (z. B. 0.5 für die ASDF-Reihe bei QWERTY).</param>
public sealed record KeyRow(IReadOnlyList<KeyDef> Keys, float Indent = 0f);

/// <param name="IsAlphabetic">Buchstabenlayout (Umschalt wirkt) oder Symbollayout.</param>
public sealed class KeyboardLayout(string id, bool isAlphabetic, IReadOnlyList<KeyRow> rows)
{
    public string Id { get; } = id;

    public bool IsAlphabetic { get; } = isAlphabetic;

    public IReadOnlyList<KeyRow> Rows { get; } = rows;

    /// <summary>Breite einer Reihe in Einheiten; bestimmt die Breite einer Standardtaste.</summary>
    public float Units { get; } = rows.Max(r =>
        2 * r.Indent
        + r.Keys.Where(k => k.Width > 0).Sum(k => k.Width)
        + r.Keys.Count(k => k.Width <= 0));
}
