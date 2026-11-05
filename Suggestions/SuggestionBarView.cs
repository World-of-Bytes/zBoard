using Android.Content;
using Android.Graphics;
using Android.Views;
using ZBoard.Keyboard;

namespace ZBoard.Suggestions;

/// <param name="Display">Angezeigter Text (z. B. mit Anführungszeichen).</param>
/// <param name="Word">Wort, das beim Antippen eingefügt wird.</param>
/// <param name="Emphasized">Fett darstellen (wird bei Leertaste eingefügt).</param>
/// <param name="IsLiteral">Der Nutzer wählt sein getipptes Wort statt der Korrektur.</param>
public sealed record SuggestionSlot(string Display, string Word, bool Emphasized, bool IsLiteral);

/// <summary>Leiste mit drei Vorschlägen über der Tastatur; der mittlere ist der wichtigste.</summary>
public sealed class SuggestionBarView : View
{
    const int SlotCount = 3;

    readonly float _dp;
    readonly Paint _text = new(PaintFlags.AntiAlias);
    readonly Paint _line = new(PaintFlags.AntiAlias);
    readonly Paint _fill = new(PaintFlags.AntiAlias);
    readonly SuggestionSlot?[] _slots = new SuggestionSlot?[SlotCount];

    int _pressed = -1;
    long _downTime;

    public SuggestionBarView(Context context) : base(context)
    {
        _dp = context.Resources!.DisplayMetrics!.Density;
        _text.TextAlign = Paint.Align.Center;
        _line.StrokeWidth = 1 * _dp;
    }

    /// <summary>Slot wurde angetippt (Slot, Touch-Down-Zeit, Touch-Up-Zeit in Uptime-ms).</summary>
    public event Action<SuggestionSlot, long, long>? SuggestionPicked;

    public KeyboardTheme Theme
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    } = KeyboardTheme.Light;

    /// <summary>Setzt die Slots links, Mitte, rechts. Fehlende Einträge bleiben leer.</summary>
    public void SetSlots(SuggestionSlot? left, SuggestionSlot? middle, SuggestionSlot? right)
    {
        _slots[0] = left;
        _slots[1] = middle;
        _slots[2] = right;
        _pressed = -1;
        Invalidate();
    }

    public void Clear() => SetSlots(null, null, null);

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.DrawColor(Theme.Background);

        float slotWidth = Width / (float)SlotCount;
        bool any = _slots.Any(s => s != null);

        for (int i = 0; i < SlotCount; i++)
        {
            float left = i * slotWidth;

            if (i == _pressed && _slots[i] != null)
            {
                _fill.Color = Theme.KeyPressed;
                canvas.DrawRect(left, 0, left + slotWidth, Height, _fill);
            }

            if (any && i > 0)
            {
                _line.Color = Theme.HintText;
                canvas.DrawLine(left, Height * 0.3f, left, Height * 0.7f, _line);
            }

            var slot = _slots[i];
            if (slot == null) continue;

            _text.SetTypeface(slot.Emphasized ? Typeface.DefaultBold : Typeface.Default);
            _text.Color = Theme.Text;
            _text.TextSize = 17 * _dp;
            float available = slotWidth - 16 * _dp;
            float measured = _text.MeasureText(slot.Display);
            if (measured > available) _text.TextSize *= available / measured;

            float baseline = Height / 2f - (_text.Descent() + _text.Ascent()) / 2;
            canvas.DrawText(slot.Display, left + slotWidth / 2, baseline, _text);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return false;
        int index = Math.Clamp((int)(e.GetX() / (Width / (float)SlotCount)), 0, SlotCount - 1);

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _pressed = _slots[index] != null ? index : -1;
                _downTime = e.EventTime;
                Invalidate();
                break;

            case MotionEventActions.Move:
                if (_pressed >= 0 && index != _pressed)
                {
                    _pressed = -1;
                    Invalidate();
                }

                break;

            case MotionEventActions.Up:
                if (_pressed >= 0 && index == _pressed && _slots[index] is { } slot)
                {
                    _pressed = -1;
                    Invalidate();
                    SuggestionPicked?.Invoke(slot, _downTime, e.EventTime);
                }
                else
                {
                    _pressed = -1;
                    Invalidate();
                }

                break;

            case MotionEventActions.Cancel:
                _pressed = -1;
                Invalidate();
                break;
        }

        return true;
    }
}
