using Android.Graphics;

namespace ZBoard.Keyboard;

/// <summary>Farben der Tastatur. Eigene Palette, bewusst nicht die Gboard-Farben.</summary>
public sealed class KeyboardTheme
{
    public Color Background { get; init; }
    public Color Key { get; init; }
    public Color SpecialKey { get; init; }
    public Color KeyPressed { get; init; }
    public Color Accent { get; init; }
    public Color AccentText { get; init; }
    public Color Text { get; init; }
    public Color HintText { get; init; }
    public Color Popup { get; init; }

    public static readonly KeyboardTheme Light = new()
    {
        Background = Color.ParseColor("#E9ECEF"),
        Key = Color.ParseColor("#FFFFFF"),
        SpecialKey = Color.ParseColor("#CED4DA"),
        KeyPressed = Color.ParseColor("#B8C0C9"),
        Accent = Color.ParseColor("#2F6FDE"),
        AccentText = Color.ParseColor("#FFFFFF"),
        Text = Color.ParseColor("#1F2328"),
        HintText = Color.ParseColor("#6B7280"),
        Popup = Color.ParseColor("#FFFFFF"),
    };

    public static readonly KeyboardTheme Dark = new()
    {
        Background = Color.ParseColor("#1D2024"),
        Key = Color.ParseColor("#3A3F46"),
        SpecialKey = Color.ParseColor("#2A2E33"),
        KeyPressed = Color.ParseColor("#535A63"),
        Accent = Color.ParseColor("#5B8DEF"),
        AccentText = Color.ParseColor("#0E1218"),
        Text = Color.ParseColor("#E8EAED"),
        HintText = Color.ParseColor("#9AA0A6"),
        Popup = Color.ParseColor("#454B53"),
    };
}
