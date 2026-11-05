using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace ZBoard.Keyboard;

/// <summary>Material-Symbole (Resources/drawable/ms_*.xml) eingefärbt auf eine Canvas zeichnen.</summary>
public sealed class Icons(Context context)
{
    readonly Dictionary<int, Drawable> _cache = [];

    /// <summary>Zeichnet das Symbol zentriert um (<paramref name="cx"/>, <paramref name="cy"/>) in der Kantenlänge <paramref name="size"/> px.</summary>
    public void Draw(Canvas canvas, int resId, float cx, float cy, float size, Color color)
    {
        if (!_cache.TryGetValue(resId, out var drawable))
        {
            drawable = context.GetDrawable(resId)!.Mutate()!;
            _cache[resId] = drawable;
        }

        int half = (int)(size / 2);
        drawable.SetBounds((int)cx - half, (int)cy - half, (int)cx + half, (int)cy + half);
        drawable.SetTint(color.ToArgb());
        drawable.Draw(canvas);
    }

    /// <summary>Symbol für die Enter-Taste je nach Feldaktion (Beschriftung aus EditorProfile.EnterLabel).</summary>
    public static int ForEnter(string label) => label switch
    {
        "🔍" => Resource.Drawable.ms_search,
        "➤" => Resource.Drawable.ms_send,
        "→" => Resource.Drawable.ms_arrow_forward,
        "✓" => Resource.Drawable.ms_check,
        "⇥" => Resource.Drawable.ms_keyboard_tab,
        "⇤" => Resource.Drawable.ms_keyboard_tab_rtl,
        _ => Resource.Drawable.ms_keyboard_return,
    };
}
