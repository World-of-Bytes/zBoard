#if DEBUG || ZBOARD_TESTHOOKS
using Android.App;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace ZBoard.Testing;

/// <summary>
/// Nur in Debug-Builds: ein einzelnes Eingabefeld, in das der Emulator-Test (tools/emulator_test.py) tippt.
/// Mit dem Extra <c>search=true</c> verhält sich das Feld wie eine Suchleiste mit eigener Vorschlagsliste
/// (NO_SUGGESTIONS + AUTO_COMPLETE, Enter = Suchen), wie z. B. in der Google-App. Weitere Extras stehen unten.
/// </summary>
[Activity(
    Name = "de.World_of_Bytes.zBoard.TypingTestActivity",
    Label = "zBoard Test",
    Exported = true,
    WindowSoftInputMode = SoftInput.StateAlwaysVisible | SoftInput.AdjustResize)]
public sealed class TypingTestActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        bool search = Intent?.GetBooleanExtra("search", false) == true;
        var field = new EditText(this)
        {
            InputType = search
                ? InputTypes.ClassText | InputTypes.TextFlagNoSuggestions | InputTypes.TextFlagAutoComplete
                : InputTypes.ClassText | InputTypes.TextFlagCapSentences,
            ImeOptions = search ? Android.Views.InputMethods.ImeAction.Search : Android.Views.InputMethods.ImeAction.Unspecified,
            ContentDescription = "zboard-testfeld",
            Hint = search ? "Suchen" : "Testfeld",
        };
        if (search) field.SetSingleLine(true);

        // Extra password=text|number: Passwortfeld (Text) bzw. PIN-Feld (nur Ziffern)
        switch (Intent?.GetStringExtra("password"))
        {
            case "text":
                field.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
                field.Hint = "Passwort";
                break;
            case "number":
                field.InputType = InputTypes.ClassNumber | InputTypes.NumberVariationPassword;
                field.Hint = "PIN";
                break;
        }
        // Extra personal="kürzel=Text;=Wort": persönliches Wörterbuch für den Test setzen (leer = leeren)
        if (Intent?.GetStringExtra("personal") is { } personal)
        {
            var dictionary = ZBoard.Suggestions.PersonalDictionary.Empty;
            foreach (var item in personal.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = item.IndexOf('=');
                var entry = ZBoard.Suggestions.PersonalDictionary.Normalize(eq < 0 ? item : item[(eq + 1)..], eq < 0 ? null : item[..eq]);
                if (entry != null) dictionary = dictionary.With(entry);
            }

            new ZBoard.Settings.ZBoardSettings(this).PersonalDictionary = dictionary.Serialize();
        }

        // Klartext fürs Log, weil Android Passwörter in der Oberfläche maskiert
        field.TextChanged += (_, _) => Android.Util.Log.Info("zBoardTest", "FIELD " + field.Text);
        SetContentView(field);
        field.RequestFocus();
    }
}
#endif
