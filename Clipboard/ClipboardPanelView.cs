using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using ZBoard.Keyboard;

namespace ZBoard.Clipboard;

public interface IClipboardPanelListener
{
    void OnClipPaste(string text);

    void OnClipPin(string text, bool pinned);

    void OnClipRemove(string text);

    void OnClipClearUnpinned();

    void OnClipPanelClose();
}

/// <summary>Zwischenablage-Verlauf: Antippen fügt ein, 📌 heftet an, ✕ entfernt.</summary>
public sealed class ClipboardPanelView : LinearLayout
{
    readonly float _dp;
    readonly LinearLayout _list;
    readonly TextView _title;
    readonly Button _clear;
    readonly Button _close;
    KeyboardTheme _theme = KeyboardTheme.Light;

    public ClipboardPanelView(Context context) : base(context)
    {
        _dp = context.Resources!.DisplayMetrics!.Density;
        Orientation = Android.Widget.Orientation.Vertical;

        var header = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        header.SetPadding(Dp(12), Dp(4), Dp(4), Dp(4));

        _title = new TextView(context) { Text = "Zwischenablage", TextSize = 15 };
        _title.SetTypeface(null, TypefaceStyle.Bold);
        header.AddView(_title, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        _clear = FlatButton(context, "Leeren");
        _clear.Click += (_, _) => Listener?.OnClipClearUnpinned();
        header.AddView(_clear);

        _close = FlatButton(context, "ABC");
        _close.Click += (_, _) => Listener?.OnClipPanelClose();
        header.AddView(_close);

        AddView(header, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var scroll = new ScrollView(context);
        _list = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Vertical };
        scroll.AddView(_list);
        AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
    }

    public IClipboardPanelListener? Listener { get; set; }

    /// <summary>Höhe des Panels; die Tastatur soll beim Umschalten nicht springen.</summary>
    public int PanelHeight { get; set; }

    public void ApplyTheme(KeyboardTheme theme)
    {
        _theme = theme;
        SetBackgroundColor(theme.Background);
        _title.SetTextColor(theme.Text);
        _clear.SetTextColor(theme.Accent);
        _close.SetTextColor(theme.Accent);
    }

    public void ShowItems(IReadOnlyList<ClipItem> items)
    {
        _list.RemoveAllViews();
        if (items.Count == 0)
        {
            var empty = new TextView(Context)
            {
                Text = "Kopierter Text erscheint hier für eine Stunde. Angeheftetes bleibt.",
                TextSize = 14,
            };
            empty.SetTextColor(_theme.HintText);
            empty.SetPadding(Dp(16), Dp(24), Dp(16), Dp(24));
            _list.AddView(empty);
            return;
        }

        foreach (var item in items) _list.AddView(Row(item));
    }

    View Row(ClipItem item)
    {
        var row = new LinearLayout(Context) { Orientation = Android.Widget.Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(12), Dp(6), Dp(4), Dp(6));
        row.SetBackgroundColor(_theme.Key);

        var text = new TextView(Context) { Text = item.Text, TextSize = 15 };
        text.SetMaxLines(2);
        text.Ellipsize = TextUtils.TruncateAt.End;
        text.SetTextColor(_theme.Text);
        text.Click += (_, _) => Listener?.OnClipPaste(item.Text);
        row.AddView(text, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var pin = FlatButton(Context!, item.Pinned ? "📌" : "📍");
        pin.ContentDescription = item.Pinned ? "Lösen" : "Anheften";
        pin.Alpha = item.Pinned ? 1f : 0.45f;
        pin.Click += (_, _) => Listener?.OnClipPin(item.Text, !item.Pinned);
        row.AddView(pin);

        var remove = FlatButton(Context!, "✕");
        remove.ContentDescription = "Entfernen";
        remove.SetTextColor(_theme.HintText);
        remove.Click += (_, _) => Listener?.OnClipRemove(item.Text);
        row.AddView(remove);

        var wrapper = new FrameLayout(Context!);
        wrapper.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
        wrapper.AddView(row);
        return wrapper;
    }

    Button FlatButton(Context context, string label)
    {
        var button = new Button(context, null, Android.Resource.Attribute.BorderlessButtonStyle) { Text = label };
        button.SetAllCaps(false);
        button.SetMinWidth(Dp(44));
        button.SetMinimumWidth(Dp(44));
        return button;
    }

    int Dp(int value) => (int)(value * _dp);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int height = PanelHeight > 0 ? PanelHeight : (int)(4 * 54 * _dp + 12 * _dp);
        base.OnMeasure(widthMeasureSpec, MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly));
    }
}
