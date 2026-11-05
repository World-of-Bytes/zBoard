using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using ZBoard.Ime;
using ZBoard.Keyboard;
using Color = Android.Graphics.Color;
using Orientation = Android.Widget.Orientation;

namespace ZBoard.Settings;

/// <summary>
/// Einstellungen im Aufbau von Gboard: Startseite mit großem Titel und Einträgen mit Symbol,
/// jeder Eintrag öffnet eine Unterseite. Farben wie die Tastatur (Material You ab Android 12).
/// </summary>
[Activity(
    Name = "de.zeriadtke.zboard.SettingsActivity",
    Label = "zBoard",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar")]
public sealed class SettingsActivity : Activity
{
    const int MicRequest = 42;

    ZBoardSettings _settings = null!;
    KeyboardTheme _colors = KeyboardTheme.Light;
    ScrollView _scroll = null!;
    LinearLayout _content = null!;
    float _dp;
    string _page = Main;
    Java.Lang.Object? _backCallback;

    const string Main = "main";

    record Page(string Id, int Icon, string Title, Func<string?> Subtitle, Action<SettingsActivity> Build);

    static readonly Page[] Pages =
    [
        new("languages", Resource.Drawable.ms_language, "Sprachen", () => null, a => a.BuildLanguages()),
        new("preferences", Resource.Drawable.ms_tune, "Einstellungen", () => null, a => a.BuildPreferences()),
        new("theme", Resource.Drawable.ms_palette, "Design", () => null, a => a.BuildTheme()),
        new("corrections", Resource.Drawable.ms_spellcheck, "Korrekturen & Vorschläge", () => null, a => a.BuildCorrections()),
        new("glide", Resource.Drawable.ms_gesture, "Glide Typing", () => null, a => a.BuildGlide()),
        new("voice", Resource.Drawable.ms_mic, "Spracheingabe", () => null, a => a.BuildVoice()),
        new("clipboard", Resource.Drawable.ms_content_paste, "Zwischenablage", () => null, a => a.BuildClipboard()),
        new("dictionary", Resource.Drawable.ms_menu_book, "Wörterbuch", () => null, a => a.BuildDictionary()),
        new("test", Resource.Drawable.ms_keyboard, "Tastatur ausprobieren", () => null, a => a.BuildTest()),
        new("about", Resource.Drawable.ms_info, "Über zBoard", () => null, a => a.BuildAbout()),
    ];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _settings = new ZBoardSettings(this);
        _colors = CurrentColors();
        // Schalter, Dialoge und Eingabefelder passend hell oder dunkel
        SetTheme(IsDark ? Android.Resource.Style.ThemeDeviceDefaultNoActionBar : Android.Resource.Style.ThemeDeviceDefaultLightNoActionBar);
        base.OnCreate(savedInstanceState);
        _dp = Resources!.DisplayMetrics!.Density;

        _content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _scroll = new ScrollView(this) { FillViewport = true };
        _scroll.SetBackgroundColor(_colors.Background);
        _scroll.AddView(_content);
        _scroll.SetOnApplyWindowInsetsListener(new SystemBarPadding());
        SetContentView(_scroll);
        ApplySystemBars();

        ShowPage(savedInstanceState?.GetString("page") ?? Main);
        if (Intent?.GetBooleanExtra(ZBoardImeService.RequestMicExtra, false) == true) RequestMicrophone();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        outState.PutString("page", _page);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_page == Main) ShowPage(Main); // Einrichtungsstatus aktualisieren
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus && _page == Main) ShowPage(Main); // nach dem Auswahldialog
    }

    // ---------- Navigation ----------

    void ShowPage(string id)
    {
        _page = id;
        _content.RemoveAllViews();
        if (id == Main)
        {
            BuildMain();
        }
        else
        {
            var page = Pages.First(p => p.Id == id);
            PageHeader(page.Title);
            page.Build(this);
        }

        _scroll.ScrollTo(0, 0);
        UpdateBackHandling();
    }

    void GoBack()
    {
        if (_page == Main) Finish();
        else ShowPage(Main);
    }

    // Ab Android 13 (bei targetSdk 36 Pflicht): Zurück-Geste über OnBackInvokedDispatcher.
    void UpdateBackHandling()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        var dispatcher = OnBackInvokedDispatcher;
        if (_page != Main && _backCallback == null)
        {
            var callback = new BackCallback(this);
            dispatcher.RegisterOnBackInvokedCallback(0, callback);
            _backCallback = callback;
        }
        else if (_page == Main && _backCallback is Android.Window.IOnBackInvokedCallback registered)
        {
            dispatcher.UnregisterOnBackInvokedCallback(registered);
            _backCallback = null;
        }
    }

#pragma warning disable CS0618, CS0672, CA1422 // bis Android 12 der klassische Weg
    public override void OnBackPressed()
    {
        if (_page != Main) ShowPage(Main);
        else base.OnBackPressed();
    }
#pragma warning restore CS0618, CS0672, CA1422

    sealed class BackCallback(SettingsActivity owner) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => owner.GoBack();
    }

    // ---------- Startseite ----------

    void BuildMain()
    {
        var title = Label("zBoard-Einstellungen", 32, _colors.Text);
        title.SetPadding(Dp(24), Dp(56), Dp(24), Dp(24));
        _content.AddView(title);

        BuildSetupCard();

        foreach (var page in Pages)
        {
            string? subtitle = page.Id == "languages" ? LanguageSummary() : page.Subtitle();
            Row(page.Icon, page.Title, subtitle, () => ShowPage(page.Id));
        }
    }

    string LanguageSummary()
    {
        var names = _settings.Languages.Select(code => Layouts.LanguageNames.GetValueOrDefault(code, code)).ToList();
        string text = string.Join(", ", names);
        return _settings.Languages.Contains("de") ? $"{text} (QWERTZ)" : text;
    }

    void BuildSetupCard()
    {
        var imm = InputMethods();
        bool enabled = imm.EnabledInputMethodList?.Any(i => i.PackageName == PackageName) == true;
        string? current = Android.Provider.Settings.Secure.GetString(ContentResolver, Android.Provider.Settings.Secure.DefaultInputMethod);
        bool selected = current?.StartsWith(PackageName + "/", StringComparison.Ordinal) == true;
        if (enabled && selected) return;

        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.SetPadding(Dp(20), Dp(16), Dp(20), Dp(12));
        card.Background = Rounded(_colors.Key, 24);
        var cardParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        cardParams.SetMargins(Dp(16), 0, Dp(16), Dp(16));

        card.AddView(Label("zBoard einrichten", 18, _colors.Text));
        card.AddView(Label(enabled
            ? "zBoard ist aktiviert, aber noch nicht als Tastatur ausgewählt."
            : "Aktiviere zBoard in den Systemeinstellungen und wähle es dann als Tastatur aus.", 14, _colors.HintText));

        var buttons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        buttons.SetGravity(GravityFlags.End);
        if (!enabled)
        {
            buttons.AddView(TextButton("Aktivieren",
                () => StartActivity(new Intent(Android.Provider.Settings.ActionInputMethodSettings))));
        }

        buttons.AddView(TextButton("Auswählen", () => InputMethods().ShowInputMethodPicker()));
        card.AddView(buttons);
        _content.AddView(card, cardParams);
    }

    // ---------- Unterseiten ----------

    void BuildLanguages()
    {
        Section("Aktive Sprachen");
        foreach (var (code, name) in Layouts.LanguageNames)
        {
            string layout = code switch { "de" => "QWERTZ", "fr" => "AZERTY", _ => "QWERTY" };
            Toggle(name, layout, _settings.Languages.Contains(code), (view, isChecked) =>
            {
                var list = _settings.Languages.ToList();
                if (isChecked && !list.Contains(code)) list.Add(code);
                if (!isChecked) list.Remove(code);

                if (list.Count == 0)
                {
                    Toast.MakeText(this, "Mindestens eine Sprache muss aktiv bleiben.", ToastLength.Short)!.Show();
                    view.Checked = true;
                    return;
                }

                _settings.Languages = list;
            });
        }

        Note("Zum Wechseln lange auf die Leertaste drücken und die Sprache wählen.");

        Section("Deutsch");
        Toggle("Eigene Tasten für ü, ö, ä", "Sonst wie bei Gboard per langem Druck auf u, o, a (ß auf s)",
            _settings.UmlautKeys, (_, v) => _settings.UmlautKeys = v);
    }

    void BuildPreferences()
    {
        Section("Tasten");
        Toggle("Zahlenreihe", "Ziffern 1–0 über den Buchstaben", _settings.NumberRow, (_, v) => _settings.NumberRow = v);
        Toggle("Sprachwechseltaste anzeigen", "Taste mit Globus neben der Leertaste",
            _settings.ShowLanguageKey, (_, v) => _settings.ShowLanguageKey = v);

        Section("Layout");
        Choice("Einhandmodus",
        [
            ("Aus", _settings.OneHanded == OneHandedMode.Off, () => _settings.OneHanded = OneHandedMode.Off),
            ("Rechts", _settings.OneHanded == OneHandedMode.Right, () => _settings.OneHanded = OneHandedMode.Right),
            ("Links", _settings.OneHanded == OneHandedMode.Left, () => _settings.OneHanded = OneHandedMode.Left),
        ]);

        var heightLabel = Label($"Tastaturhöhe: {_settings.HeightPercent} %", 17, _colors.Text);
        heightLabel.SetPadding(Dp(24), Dp(12), Dp(24), 0);
        _content.AddView(heightLabel);
        var height = new SeekBar(this) { Max = 50, Progress = _settings.HeightPercent - 80 };
        height.SetPadding(Dp(24), Dp(8), Dp(24), Dp(16));
        height.ProgressChanged += (_, e) =>
        {
            _settings.HeightPercent = 80 + e.Progress;
            heightLabel.Text = $"Tastaturhöhe: {_settings.HeightPercent} %";
        };
        _content.AddView(height);

        Section("Tastendruck");
        Toggle("Ton bei Tastendruck", null, _settings.KeySound, (_, v) => _settings.KeySound = v);
        Toggle("Haptisches Feedback bei Tastendruck", null, _settings.Haptics, (_, v) => _settings.Haptics = v);
        Toggle("Pop-up bei Tastendruck", "Vergrößerte Taste über dem Finger", _settings.KeyPreview, (_, v) => _settings.KeyPreview = v);
    }

    void BuildTheme()
    {
        List<(string, bool, Action)> options =
        [
            (OperatingSystem.IsAndroidVersionAtLeast(31) ? "Wie das System (Farben aus dem Hintergrundbild)" : "Wie das System",
                _settings.Theme is ThemeMode.System or ThemeMode.Dynamic, () => _settings.Theme = ThemeMode.System),
            ("Hell", _settings.Theme == ThemeMode.Light, () => _settings.Theme = ThemeMode.Light),
            ("Dunkel", _settings.Theme == ThemeMode.Dark, () => _settings.Theme = ThemeMode.Dark),
        ];
        Choice("Design", options);
        Note("Die Einstellungen übernehmen das Design beim nächsten Öffnen.");
    }

    void BuildCorrections()
    {
        Section("Vorschläge");
        Toggle("Vorschlagsleiste: nächstes Wort", "Vorschläge, bevor du zu tippen beginnst",
            _settings.NextWordPrediction, (_, v) => _settings.NextWordPrediction = v);

        Section("Korrekturen");
        Toggle("Autokorrektur", "Tippfehler beim Drücken der Leertaste korrigieren", _settings.AutoCorrect, (_, v) => _settings.AutoCorrect = v);
        Toggle("Automatische Großschreibung", "Am Satzanfang und bei Substantiven", _settings.AutoCaps, (_, v) => _settings.AutoCaps = v);
        Toggle("Punkt durch doppeltes Leerzeichen", null, _settings.DoubleSpacePeriod, (_, v) => _settings.DoubleSpacePeriod = v);
    }

    void BuildGlide()
    {
        Toggle("Glide Typing aktivieren", "Wörter eingeben, indem du von Buchstabe zu Buchstabe wischst",
            _settings.GlideTyping, (_, v) => _settings.GlideTyping = v);
        Note("Wischen über die Leertaste bewegt den Cursor. Wischen von der Löschtaste nach links markiert Wörter zum Löschen.");
    }

    void BuildVoice()
    {
        Toggle("Nur Offline-Erkennung", "Sprache verlässt das Gerät nicht (ab Android 12, je nach Hersteller)",
            _settings.VoiceOfflineOnly, (_, v) => _settings.VoiceOfflineOnly = v);
        Row(Resource.Drawable.ms_mic, "Mikrofon freigeben", "Einmalig nötig für die Spracheingabe", RequestMicrophone);
        Note("Das Mikrofon rechts in der Vorschlagsleiste startet die Spracherkennung des Systems. Antippen während der "
             + "Aufnahme beendet sie.");
    }

    void BuildClipboard()
    {
        Toggle("Zwischenablage-Verlauf", "Kopierte Texte eine Stunde lang anzeigen",
            _settings.ClipboardHistory, (_, v) => _settings.ClipboardHistory = v);
        Row(Resource.Drawable.ms_content_paste, "Angeheftete Einträge löschen", null, () =>
        {
            _settings.PinnedClips = string.Empty;
            Toast.MakeText(this, "Gilt ab dem nächsten Öffnen der Tastatur.", ToastLength.Short)!.Show();
        });
        Note("Nur Angeheftetes wird gespeichert. Als vertraulich markierte Inhalte, etwa aus Passwort-Managern, "
             + "übernimmt zBoard nie.");
    }

    void BuildDictionary()
    {
        Toggle("Aus meinen Eingaben lernen", "Häufig verwendete Wörter und Wortfolgen vorschlagen",
            _settings.LearnWords, (_, v) => _settings.LearnWords = v);
        Row(Resource.Drawable.ms_menu_book, "Gelernte Wörter löschen", null, () =>
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
        Note("Gelerntes bleibt auf dem Gerät und wird nie exportiert. In Passwort-, E-Mail- und Adressfeldern lernt "
             + "zBoard nichts.");
    }

    void BuildTest()
    {
        var input = new EditText(this)
        {
            Hint = "Hier tippen, um zBoard zu testen",
            InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences
                        | Android.Text.InputTypes.TextFlagMultiLine,
        };
        input.SetTextColor(_colors.Text);
        input.SetHintTextColor(_colors.HintText);
        input.SetMinLines(3);
        var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        lp.SetMargins(Dp(24), Dp(8), Dp(24), Dp(8));
        _content.AddView(input, lp);
        input.RequestFocus();
    }

    void BuildAbout()
    {
        Note("© 2026 World of Bytes, Patrick Zeriadtke – world-of-bytes.de\n"
             + "zBoard ist freie Software unter der BSD 2-Clause License.\n\n"
             + "Wortlisten (CC BY-SA 4.0): FrequencyWords von Hermit Dave auf Basis von OpenSubtitles 2018; "
             + "Großschreibung deutscher Substantive nach german-nouns von Gregor Weichbrodt auf Basis von Wiktionary. "
             + "Lizenz: creativecommons.org/licenses/by-sa/4.0\n\n"
             + "Wortpaare für die Vorhersage (CC BY 3.0): Google Books Ngram Corpus, aufbereitet von "
             + "orgtre/google-books-ngram-frequency. Lizenz: creativecommons.org/licenses/by/3.0\n\n"
             + "Emoji-Daten: unicode-emoji-json von Mu-An Chiou (MIT); Namen und Suchbegriffe: Unicode CLDR "
             + "(Unicode License v3), © Unicode, Inc.\n\n"
             + "Symbole: Material Symbols von Google (Apache License 2.0).");
    }

    // ---------- Mikrofon ----------

    void RequestMicrophone()
    {
        if (CheckSelfPermission(Android.Manifest.Permission.RecordAudio) == Android.Content.PM.Permission.Granted)
        {
            Toast.MakeText(this, "Das Mikrofon ist bereits freigegeben.", ToastLength.Short)!.Show();
            return;
        }

        RequestPermissions([Android.Manifest.Permission.RecordAudio], MicRequest);
    }

    public override void OnRequestPermissionsResult(
        int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != MicRequest) return;
        bool granted = grantResults.Length > 0 && grantResults[0] == Android.Content.PM.Permission.Granted;
        Toast.MakeText(this,
            granted ? "Mikrofon freigegeben. Tippe in der Tastatur erneut auf das Mikrofon." : "Ohne Mikrofon ist keine Spracheingabe möglich.",
            ToastLength.Long)!.Show();
        if (granted && Intent?.GetBooleanExtra(ZBoardImeService.RequestMicExtra, false) == true) Finish();
    }

    // ---------- Farben ----------

    KeyboardTheme CurrentColors()
    {
        bool systemDark = (Resources!.Configuration!.UiMode & UiMode.NightMask) == UiMode.NightYes;
        return _settings.Theme switch
        {
            ThemeMode.Light => KeyboardTheme.Light,
            ThemeMode.Dark => KeyboardTheme.Dark,
            _ when OperatingSystem.IsAndroidVersionAtLeast(31) => KeyboardTheme.FromSystem(this, systemDark),
            _ => systemDark ? KeyboardTheme.Dark : KeyboardTheme.Light,
        };
    }

    bool IsDark => (0.299 * _colors.Background.R + 0.587 * _colors.Background.G + 0.114 * _colors.Background.B) / 255 < 0.5;

    void ApplySystemBars()
    {
        var window = Window!;
        // Helle Statusleisten-Symbole auf dunklem Hintergrund und umgekehrt
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var mask = (int)WindowInsetsControllerAppearance.LightStatusBars | (int)WindowInsetsControllerAppearance.LightNavigationBars;
            window.InsetsController?.SetSystemBarsAppearance(IsDark ? 0 : mask, mask);
        }

#pragma warning disable CA1422, CS0618 // bis Android 14 wirksam, danach zeichnet die App hinter den Leisten
        window.SetStatusBarColor(_colors.Background);
        window.SetNavigationBarColor(_colors.Background);
#pragma warning restore CA1422, CS0618
    }

    /// <summary>Ab Android 15 liegt der Inhalt unter Status- und Navigationsleiste: Abstand halten.</summary>
    sealed class SystemBarPadding : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }

            return insets;
        }
    }

    // ---------- Bausteine ----------

    InputMethodManager InputMethods() =>
        (InputMethodManager)GetSystemService(Context.InputMethodService)!;

    int Dp(float value) => (int)(value * _dp);

    TextView Label(string text, float sp, Color color)
    {
        var view = new TextView(this) { Text = text, TextSize = sp };
        view.SetTextColor(color);
        return view;
    }

    Android.Graphics.Drawables.GradientDrawable Rounded(Color color, float radiusDp)
    {
        var shape = new Android.Graphics.Drawables.GradientDrawable();
        shape.SetColor(color);
        shape.SetCornerRadius(radiusDp * _dp);
        return shape;
    }

    /// <summary>Unterseite: Zurück-Pfeil und großer Titel.</summary>
    void PageHeader(string title)
    {
        var back = new ImageView(this) { ContentDescription = "Zurück" };
        back.SetImageResource(Resource.Drawable.ms_arrow_back);
        back.SetColorFilter(_colors.Text);
        back.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        back.Click += (_, _) => ShowPage(Main);
        var lp = new LinearLayout.LayoutParams(Dp(48), Dp(48));
        lp.SetMargins(Dp(8), Dp(16), 0, 0);
        _content.AddView(back, lp);

        var heading = Label(title, 32, _colors.Text);
        heading.SetPadding(Dp(24), Dp(40), Dp(24), Dp(16));
        _content.AddView(heading);
    }

    void Section(string title)
    {
        var view = Label(title, 14, _colors.Accent);
        view.SetPadding(Dp(24), Dp(20), Dp(24), Dp(4));
        _content.AddView(view);
    }

    void Note(string text)
    {
        var view = Label(text, 14, _colors.HintText);
        view.SetPadding(Dp(24), Dp(12), Dp(24), Dp(12));
        _content.AddView(view);
    }

    /// <summary>Eintrag wie in den Gboard-Einstellungen: Symbol, Titel, optional Untertitel.</summary>
    void Row(int icon, string title, string? subtitle, Action onClick)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(24), Dp(18), Dp(24), Dp(18));
        row.Foreground = Ripple();
        row.Click += (_, _) => onClick();

        var image = new ImageView(this);
        image.SetImageResource(icon);
        image.SetColorFilter(_colors.Text);
        var iconParams = new LinearLayout.LayoutParams(Dp(26), Dp(26)) { MarginEnd = Dp(28) };
        row.AddView(image, iconParams);

        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        texts.AddView(Label(title, 20, _colors.Text));
        if (!string.IsNullOrEmpty(subtitle)) texts.AddView(Label(subtitle, 14, _colors.HintText));
        row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        _content.AddView(row);
    }

    /// <summary>Schalter rechts, Titel und Untertitel links.</summary>
    void Toggle(string title, string? subtitle, bool value, Action<Switch, bool> onChange)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(24), Dp(14), Dp(16), Dp(14));

        var texts = new LinearLayout(this) { Orientation = Orientation.Vertical };
        texts.AddView(Label(title, 18, _colors.Text));
        if (!string.IsNullOrEmpty(subtitle)) texts.AddView(Label(subtitle, 14, _colors.HintText));
        row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var toggle = new Switch(this) { Checked = value };
        toggle.CheckedChange += (_, e) => onChange(toggle, e.IsChecked);
        row.AddView(toggle);

        row.Clickable = true;
        row.Foreground = Ripple();
        row.Click += (_, _) => toggle.Toggle();
        _content.AddView(row);
    }

    /// <summary>Auswahl aus mehreren Möglichkeiten (Optionsfelder).</summary>
    void Choice(string title, List<(string Label, bool Selected, Action Select)> options)
    {
        Section(title);
        var group = new RadioGroup(this) { Orientation = Orientation.Vertical };
        group.SetPadding(Dp(16), 0, Dp(24), 0);
        foreach (var (label, selected, select) in options)
        {
            var radio = new RadioButton(this) { Text = label, Id = View.GenerateViewId(), TextSize = 18 };
            radio.SetTextColor(_colors.Text);
            radio.SetPadding(Dp(12), Dp(12), 0, Dp(12));
            group.AddView(radio);
            if (selected) radio.Checked = true;
            radio.CheckedChange += (_, e) =>
            {
                if (e.IsChecked) select();
            };
        }

        _content.AddView(group);
    }

    Button TextButton(string text, Action onClick)
    {
        var button = new Button(this) { Text = text };
        button.SetAllCaps(false);
        button.SetTextColor(_colors.Accent);
        button.Background = null;
        button.Click += (_, _) => onClick();
        return button;
    }

    Android.Graphics.Drawables.Drawable? Ripple()
    {
        var attrs = ObtainStyledAttributes([Android.Resource.Attribute.SelectableItemBackground]);
        var drawable = attrs.GetDrawable(0);
        attrs.Recycle();
        return drawable;
    }
}
