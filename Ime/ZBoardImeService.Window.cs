using Android.Views;
using Android.Widget;

namespace ZBoard.Ime;

/// <summary>
/// Platz für Popups über der Tastatur, wie bei Gboard und der Android-Open-Source-Tastatur (AOSP LatinIME):
/// Das Tastaturfenster ist so hoch wie der Bildschirm, der Eingabebereich sitzt unten, und die Eltern-Views
/// schneiden nicht ab. So darf der Zusatzzeichen-Popup mit zwei Zeilen über die App reichen, statt nach unten
/// auf die Tasten zu rutschen. Android hält weiterhin nur den Eingabebereich für sichtbar und berührbar
/// (Standard von OnComputeInsets): Die App darüber bleibt bedienbar und wird nicht verkleinert.
/// </summary>
public partial class ZBoardImeService
{
    public override void SetInputView(View? view)
    {
        base.SetInputView(view);
        UpdatePopupSpace();
    }

    public override void UpdateFullscreenMode()
    {
        base.UpdateFullscreenMode();
        UpdatePopupSpace();
    }

    /// <summary>Fenster auf volle Höhe, Eingabebereich unten (wie AOSP updateSoftInputWindowLayoutParameters).</summary>
    void UpdatePopupSpace()
    {
        var window = Window?.Window;
        if (window == null) return;

        var attributes = window.Attributes;
        if (attributes != null && attributes.Height != ViewGroup.LayoutParams.MatchParent)
        {
            attributes.Height = ViewGroup.LayoutParams.MatchParent;
            window.Attributes = attributes;
        }

        // Im Vollbildmodus (Querformat mit Textfeld der Tastatur) füllt der Eingabebereich das Fenster wie gewohnt
        int height = IsFullscreenMode ? ViewGroup.LayoutParams.MatchParent : ViewGroup.LayoutParams.WrapContent;
        if (window.FindViewById(Android.Resource.Id.InputArea) is { } inputArea)
        {
            switch (inputArea.LayoutParameters)
            {
                case LinearLayout.LayoutParams lp:
                    lp.Height = height;
                    lp.Gravity = GravityFlags.Bottom;
                    inputArea.LayoutParameters = lp;
                    break;
                case FrameLayout.LayoutParams lp:
                    lp.Height = height;
                    lp.Gravity = GravityFlags.Bottom;
                    inputArea.LayoutParameters = lp;
                    break;
                case { } lp:
                    lp.Height = height;
                    inputArea.LayoutParameters = lp;
                    break;
            }
        }

        AllowDrawingAboveKeyboard();
        ApplyNavigationBarStyle();
    }

    /// <summary>
    /// Navigationsleiste unter der Tastatur wie bei Gboard in der Hintergrundfarbe der Tastatur, Symbole passend
    /// dunkel oder hell. Sonst malt Android die Leiste in eigener Farbe (oft weiß mit hellen Symbolen).
    /// </summary>
    void ApplyNavigationBarStyle()
    {
        var window = Window?.Window;
        if (window == null) return;
        var bg = _theme.Background;
        bool light = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255 >= 0.5;

        window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
        if (OperatingSystem.IsAndroidVersionAtLeast(29)) window.NavigationBarContrastEnforced = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            int mask = (int)WindowInsetsControllerAppearance.LightNavigationBars;
            window.InsetsController?.SetSystemBarsAppearance(light ? mask : 0, mask);
        }
        else if (window.DecorView is { } decor)
        {
#pragma warning disable CA1422, CS0618 // vor Android 11 nur über die alten Flags
            var flags = (int)decor.SystemUiVisibility;
            int lightNav = (int)SystemUiFlags.LightNavigationBar;
            decor.SystemUiVisibility = (StatusBarVisibility)(light ? flags | lightNav : flags & ~lightNav);
#pragma warning restore CA1422, CS0618
        }

#pragma warning disable CA1422, CS0618 // bis Android 14 wirksam, danach liegt die Tastatur selbst unter der Leiste
        window.SetNavigationBarColor(bg);
#pragma warning restore CA1422, CS0618
    }

    /// <summary>
    /// Alle Eltern bis zum Fenster schneiden nicht ab: Die Tastatur darf darüber zeichnen
    /// (KeyboardView.OverflowTop rechnet den Platz bis zum obersten solchen Elternteil).
    /// </summary>
    void AllowDrawingAboveKeyboard()
    {
        var parent = _inputRoot?.Parent;
        while (parent is ViewGroup group)
        {
            group.SetClipChildren(false);
            group.SetClipToPadding(false);
            parent = group.Parent;
        }
    }
}
