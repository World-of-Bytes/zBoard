#if DEBUG
using Android.App;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace ZBoard.Testing;

/// <summary>
/// Nur in Debug-Builds: ein einzelnes Eingabefeld, in das der Emulator-Test (tools/emulator_test.py) tippt.
/// </summary>
[Activity(
    Name = "de.zeriadtke.zboard.TypingTestActivity",
    Label = "zBoard Test",
    Exported = true,
    WindowSoftInputMode = SoftInput.StateAlwaysVisible | SoftInput.AdjustResize)]
public sealed class TypingTestActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var field = new EditText(this)
        {
            InputType = InputTypes.ClassText | InputTypes.TextFlagCapSentences,
            ContentDescription = "zboard-testfeld",
            Hint = "Testfeld",
        };
        SetContentView(field);
        field.RequestFocus();
    }
}
#endif
