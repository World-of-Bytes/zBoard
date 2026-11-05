using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using ZBoard.Ime;
using ZBoard.Keyboard;
using ZBoard.Stats;

namespace ZBoard.Settings;

/// <summary>Startbildschirm: zBoard aktivieren, auswählen, einstellen, testen; Statistik exportieren oder löschen.</summary>
[Activity(
    Name = "de.zeriadtke.zboard.SettingsActivity",
    Label = "zBoard",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.DeviceDefault.Settings")]
public sealed class SettingsActivity : Activity
{
    const int ExportRequest = 41;

    ZBoardSettings _settings = null!;
    LinearLayout _content = null!;
    TextView _status = null!;
    TextView _statsInfo = null!;
    float _dp;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _settings = new ZBoardSettings(this);
        _dp = Resources!.DisplayMetrics!.Density;

        _content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int pad = Dp(16);
        _content.SetPadding(pad, pad, pad, pad);

        var scroll = new ScrollView(this);
        scroll.AddView(_content);
        SetContentView(scroll);

        BuildSetupSection();
        BuildTypingSection();
        BuildLanguageSection();
        BuildAppearanceSection();
        BuildStatsSection();
        BuildTestSection();
        BuildAboutSection();
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshStatus();
        RefreshStatsInfo();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) RefreshStatus(); // nach dem Auswahldialog aktualisieren
    }

    // ---------- Abschnitte ----------

    void BuildSetupSection()
    {
        Header("Einrichtung");
        _status = Text(string.Empty);
        AddButton("1. zBoard in den Systemeinstellungen aktivieren",
            () => StartActivity(new Intent(Android.Provider.Settings.ActionInputMethodSettings)));
        AddButton("2. zBoard als Tastatur auswählen",
            () => InputMethods().ShowInputMethodPicker());
    }

    void BuildTypingSection()
    {
        Header("Tippen");
        AddSwitch("Autokorrektur", _settings.AutoCorrect, v => _settings.AutoCorrect = v);
        AddSwitch("Automatische Großschreibung", _settings.AutoCaps, v => _settings.AutoCaps = v);
        AddSwitch("Doppelte Leertaste setzt Punkt", _settings.DoubleSpacePeriod, v => _settings.DoubleSpacePeriod = v);
        AddSwitch("Wischeingabe (Wörter über die Tasten wischen)", _settings.GlideTyping, v => _settings.GlideTyping = v);
        AddSwitch("Tastenvorschau beim Drücken", _settings.KeyPreview, v => _settings.KeyPreview = v);
        AddSwitch("Vibration bei Tastendruck", _settings.Haptics, v => _settings.Haptics = v);
        AddSwitch("Ton bei Tastendruck", _settings.KeySound, v => _settings.KeySound = v);
        AddSwitch("Nächstes Wort vorschlagen", _settings.NextWordPrediction, v => _settings.NextWordPrediction = v);

        Header("Lernen");
        Text("zBoard merkt sich Wörter und Wortfolgen, die du oft verwendest, und schlägt sie dann vor. "
             + "Das bleibt auf dem Gerät, wird nie exportiert und ist von der Statistik getrennt. "
             + "In Passwort-, E-Mail- und Adressfeldern lernt zBoard nichts.");
        AddSwitch("Aus meinen Eingaben lernen", _settings.LearnWords, v => _settings.LearnWords = v);
        AddButton("Gelernte Wörter löschen", () =>
        {
            new AlertDialog.Builder(this)
                .SetTitle("Gelerntes löschen?")!
                .SetMessage("Alle gelernten Wörter und Wortfolgen werden entfernt.")!
                .SetPositiveButton("Löschen", (_, _) =>
                {
                    ZBoardImeService.ClearLearned(this);
                    Toast.MakeText(this, "Gelernte Wörter gelöscht.", ToastLength.Short)!.Show();
                })!
                .SetNegativeButton("Abbrechen", (_, _) => { })!
                .Show();
        });
    }

    void BuildLanguageSection()
    {
        Header("Sprachen");
        Text("Mit der 🌐-Taste wechselst du zwischen den aktivierten Sprachen.");

        foreach (var (code, name) in Layouts.LanguageNames)
        {
            var box = new CheckBox(this)
            {
                Text = name,
                Checked = _settings.Languages.Contains(code),
            };
            box.CheckedChange += (_, e) =>
            {
                var list = _settings.Languages.ToList();
                if (e.IsChecked && !list.Contains(code)) list.Add(code);
                if (!e.IsChecked) list.Remove(code);

                if (list.Count == 0)
                {
                    Toast.MakeText(this, "Mindestens eine Sprache muss aktiv bleiben.", ToastLength.Short)!.Show();
                    box.Checked = true;
                    return;
                }

                _settings.Languages = list;
            };
            _content.AddView(box);
        }
    }

    void BuildAppearanceSection()
    {
        Header("Darstellung");
        var group = new RadioGroup(this) { Orientation = Orientation.Vertical };
        (ThemeMode Mode, string Label)[] options =
        [
            (ThemeMode.System, "Wie das System"),
            (ThemeMode.Light, "Hell"),
            (ThemeMode.Dark, "Dunkel"),
        ];

        foreach (var (mode, label) in options)
        {
            var radio = new RadioButton(this) { Text = label, Id = View.GenerateViewId() };
            group.AddView(radio);
            if (_settings.Theme == mode) radio.Checked = true;
            radio.CheckedChange += (_, e) =>
            {
                if (e.IsChecked) _settings.Theme = mode;
            };
        }

        _content.AddView(group);
    }

    void BuildStatsSection()
    {
        Header("Statistik für zHeal Meridian");
        Text("Erfasst werden nur Zeitpunkte, Druckdauer, Abstände zwischen Anschlägen und die Art der Taste "
             + "(Buchstabe, Leertaste, Löschen …). Welche Zeichen du tippst, wird nie gespeichert. "
             + "In Passwortfeldern wird nichts erfasst. Die Daten bleiben auf dem Gerät.");

        AddSwitch("Tipp-Statistik erfassen", _settings.StatsEnabled, v => _settings.StatsEnabled = v);
        _statsInfo = Text(string.Empty);
        AddButton("Statistik exportieren (JSON Lines)", ExportStats);
        AddButton("Alle Statistikdaten löschen", () =>
        {
            new AlertDialog.Builder(this)
                .SetTitle("Statistik löschen?")!
                .SetMessage("Alle gespeicherten Tipp-Daten werden unwiderruflich entfernt.")!
                .SetPositiveButton("Löschen", (_, _) =>
                {
                    Sink().DeleteAll();
                    RefreshStatsInfo();
                })!
                .SetNegativeButton("Abbrechen", (_, _) => { })!
                .Show();
        });
    }

    void BuildTestSection()
    {
        Header("Ausprobieren");
        var input = new EditText(this)
        {
            Hint = "Hier tippen, um zBoard zu testen",
            InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences
                        | Android.Text.InputTypes.TextFlagMultiLine,
        };
        input.SetMinLines(3);
        _content.AddView(input);
    }

    void BuildAboutSection()
    {
        Header("Über zBoard");
        Text("© 2026 World of Bytes, Patrick Zeriadtke – world-of-bytes.de\n"
             + "zBoard ist freie Software unter der BSD 2-Clause License.\n\n"
             + "Wortlisten (CC BY-SA 4.0): FrequencyWords von Hermit Dave auf Basis von OpenSubtitles 2018; "
             + "Großschreibung deutscher Substantive nach german-nouns von Gregor Weichbrodt auf Basis von Wiktionary. "
             + "Lizenz: creativecommons.org/licenses/by-sa/4.0\n\n"
             + "Wortpaare für die Vorhersage (CC BY 3.0): Google Books Ngram Corpus, aufbereitet von "
             + "orgtre/google-books-ngram-frequency. Lizenz: creativecommons.org/licenses/by/3.0");
    }

    // ---------- Status ----------

    void RefreshStatus()
    {
        if (_status == null) return;
        var imm = InputMethods();
        bool enabled = imm.EnabledInputMethodList?.Any(i => i.PackageName == PackageName) == true;
        string? current = Android.Provider.Settings.Secure.GetString(ContentResolver, Android.Provider.Settings.Secure.DefaultInputMethod);
        bool selected = current?.StartsWith(PackageName + "/", StringComparison.Ordinal) == true;

        _status.Text = (enabled, selected) switch
        {
            (true, true) => "✓ zBoard ist aktiv und ausgewählt.",
            (true, false) => "zBoard ist aktiviert, aber noch nicht ausgewählt (Schritt 2).",
            _ => "zBoard ist noch nicht aktiviert (Schritt 1).",
        };
    }

    void RefreshStatsInfo()
    {
        if (_statsInfo == null) return;
        int files = Sink().FileCount;
        _statsInfo.Text = $"Gespeichert: {files} Tag(e). Aufbewahrung: {_settings.StatsRetentionDays} Tage.";
    }

    // ---------- Export ----------

    void ExportStats()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        intent.PutExtra(Intent.ExtraTitle, $"zboard-statistik-{DateTime.Now:yyyy-MM-dd}.jsonl");
        StartActivityForResult(intent, ExportRequest);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != ExportRequest || resultCode != Result.Ok || data?.Data is not { } uri) return;

        try
        {
            using var stream = ContentResolver!.OpenOutputStream(uri)
                               ?? throw new IOException("Ziel konnte nicht geöffnet werden.");
            Sink().ExportTo(stream);
            Toast.MakeText(this, "Statistik exportiert.", ToastLength.Short)!.Show();
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, $"Export fehlgeschlagen: {ex.Message}", ToastLength.Long)!.Show();
        }
    }

    // ---------- UI-Helfer ----------

    JsonlStatsSink Sink() => new(ZBoardImeService.StatsDirectory(this));

    InputMethodManager InputMethods() =>
        (InputMethodManager)GetSystemService(Context.InputMethodService)!;

    int Dp(int value) => (int)(value * _dp);

    void Header(string title)
    {
        var view = new TextView(this) { Text = title, TextSize = 18 };
        view.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        view.SetPadding(0, Dp(20), 0, Dp(8));
        _content.AddView(view);
    }

    TextView Text(string text)
    {
        var view = new TextView(this) { Text = text, TextSize = 14 };
        view.SetPadding(0, Dp(4), 0, Dp(8));
        _content.AddView(view);
        return view;
    }

    void AddButton(string text, Action onClick)
    {
        var button = new Button(this) { Text = text };
        button.SetAllCaps(false);
        button.Click += (_, _) => onClick();
        _content.AddView(button);
    }

    void AddSwitch(string text, bool value, Action<bool> onChange)
    {
        var view = new Switch(this) { Text = text, Checked = value };
        view.SetPadding(0, Dp(8), 0, Dp(8));
        view.CheckedChange += (_, e) => onChange(e.IsChecked);
        _content.AddView(view);
    }
}
