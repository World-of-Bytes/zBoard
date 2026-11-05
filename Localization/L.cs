namespace ZBoard.Localization;

/// <summary>
/// Sprache der Bedienoberfläche: Deutsch, wenn das System auf Deutsch steht, sonst Englisch.
/// Beide Texte stehen direkt an der Stelle, an der sie gebraucht werden: <c>L.T("Deutsch", "English")</c>.
/// </summary>
public static class L
{
    /// <summary>true, wenn die Systemsprache Deutsch ist.</summary>
    public static bool German => Java.Util.Locale.Default.Language == "de";

    public static string T(string german, string english) => German ? german : english;
}
