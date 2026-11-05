using Android.Content;

namespace ZBoard.Settings;

public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Einstellungen in SharedPreferences. Werte werden bei jedem Zugriff frisch gelesen.</summary>
public sealed class ZBoardSettings
{
    const string PrefsName = "zboard";

    readonly ISharedPreferences _prefs;

    public ZBoardSettings(Context context)
    {
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

    /// <summary>Aktivierte Sprachen in Reihenfolge, z. B. ["de", "en"].</summary>
    public IReadOnlyList<string> Languages
    {
        get
        {
            var raw = _prefs.GetString("languages", "de,en") ?? "de";
            var list = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return list.Count > 0 ? list : new List<string> { "de" };
        }
        set => PutString("languages", string.Join(",", value));
    }

    public string CurrentLanguage
    {
        get
        {
            var current = _prefs.GetString("currentLanguage", null);
            return current != null && Languages.Contains(current) ? current : Languages[0];
        }
        set => PutString("currentLanguage", value);
    }

    public bool Haptics
    {
        get => _prefs.GetBoolean("haptics", true);
        set => PutBool("haptics", value);
    }

    public bool AutoCorrect
    {
        get => _prefs.GetBoolean("autocorrect", true);
        set => PutBool("autocorrect", value);
    }

    public bool AutoCaps
    {
        get => _prefs.GetBoolean("autocaps", true);
        set => PutBool("autocaps", value);
    }

    public bool DoubleSpacePeriod
    {
        get => _prefs.GetBoolean("doubleSpacePeriod", true);
        set => PutBool("doubleSpacePeriod", value);
    }

    /// <summary>Nächstes Wort nach dem Leerzeichen vorschlagen.</summary>
    public bool NextWordPrediction
    {
        get => _prefs.GetBoolean("nextWordPrediction", true);
        set => PutBool("nextWordPrediction", value);
    }

    /// <summary>Eigene Wörter und Wortfolgen lernen (nur auf dem Gerät).</summary>
    public bool LearnWords
    {
        get => _prefs.GetBoolean("learnWords", true);
        set => PutBool("learnWords", value);
    }

    public bool GlideTyping
    {
        get => _prefs.GetBoolean("glideTyping", true);
        set => PutBool("glideTyping", value);
    }

    public bool KeyPreview
    {
        get => _prefs.GetBoolean("keyPreview", true);
        set => PutBool("keyPreview", value);
    }

    public bool KeySound
    {
        get => _prefs.GetBoolean("keySound", false);
        set => PutBool("keySound", value);
    }

    public ThemeMode Theme
    {
        get => (ThemeMode)_prefs.GetInt("theme", (int)ThemeMode.System);
        set => PutInt("theme", (int)value);
    }

    /// <summary>Statistik für zHeal Meridian. Standard: aus.</summary>
    public bool StatsEnabled
    {
        get => _prefs.GetBoolean("statsEnabled", false);
        set => PutBool("statsEnabled", value);
    }

    public int StatsRetentionDays
    {
        get => _prefs.GetInt("statsRetentionDays", 30);
        set => PutInt("statsRetentionDays", value);
    }

    void PutBool(string key, bool value) => _prefs.Edit()!.PutBoolean(key, value)!.Apply();

    void PutInt(string key, int value) => _prefs.Edit()!.PutInt(key, value)!.Apply();

    void PutString(string key, string value) => _prefs.Edit()!.PutString(key, value)!.Apply();
}
