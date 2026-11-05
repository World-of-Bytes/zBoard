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
/// (NO_SUGGESTIONS + AUTO_COMPLETE, Enter = Suchen), wie z. B. in der Google-App.
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
        SetContentView(field);
        field.RequestFocus();
    }
}
#endif
