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

/// <summary>Eintrag der Werkzeugleiste.</summary>
public sealed record ToolbarItem(string Id, string Icon);

/// <summary>
/// Leiste über der Tastatur. Zeigt je nach Lage drei Vorschläge, die Werkzeugleiste (mit dem zuletzt kopierten Text),
/// die Emoji-Suche oder eine Statuszeile (Spracheingabe).
/// </summary>
public sealed class SuggestionBarView : View
{
    public const string ClipChipId = "clip-chip";
    public const string SearchPillId = "emoji-search";
    public const string StatusId = "status";

    enum Mode
    {
        Suggestions,
        Toolbar,
        EmojiSearch,
        Status,
    }

    readonly float _dp;
    readonly Paint _text = new(PaintFlags.AntiAlias);
    readonly Paint _line = new(PaintFlags.AntiAlias);
    readonly Paint _fill = new(PaintFlags.AntiAlias);
    readonly SuggestionSlot?[] _slots = new SuggestionSlot?[3];
    readonly List<(RectF Bounds, string Id, string Label)> _targets = [];

    Mode _mode = Mode.Toolbar;
    string? _clipChip;
    string _searchQuery = string.Empty;
    IReadOnlyList<string> _searchResults = [];
    string _status = string.Empty;
    int _pressed = -1;
    long _downTime;

    public SuggestionBarView(Context context) : base(context)
    {
        _dp = context.Resources!.DisplayMetrics!.Density;
        _text.TextAlign = Paint.Align.Center;
        _line.StrokeWidth = 1 * _dp;
    }

    /// <summary>Vorschlag angetippt (Slot, Touch-Down-Zeit, Touch-Up-Zeit in Uptime-ms).</summary>
    public event Action<SuggestionSlot, long, long>? SuggestionPicked;

    /// <summary>Werkzeug, Kopier-Vorschlag, Suchfeld oder Statuszeile angetippt (siehe Konstanten).</summary>
    public event Action<string>? ToolbarPicked;

    /// <summary>Emoji aus der Suchzeile angetippt.</summary>
    public event Action<string>? EmojiPicked;

    public KeyboardTheme Theme
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    } = KeyboardTheme.Light;

    /// <summary>Werkzeuge, die bei leerer Leiste erscheinen.</summary>
    public IReadOnlyList<ToolbarItem> ToolbarItems { get; set; } = [];

    /// <summary>Setzt die Vorschläge links, Mitte, rechts. Sind alle leer, erscheint die Werkzeugleiste.</summary>
    public void SetSlots(SuggestionSlot? left, SuggestionSlot? middle, SuggestionSlot? right)
    {
        _slots[0] = left;
        _slots[1] = middle;
        _slots[2] = right;
        _mode = left == null && middle == null && right == null ? Mode.Toolbar : Mode.Suggestions;
        Reset();
    }

    /// <summary>Werkzeugleiste, optional mit dem zuletzt kopierten Text als Vorschlag.</summary>
    public void Clear(string? clipChip = null)
    {
        Array.Clear(_slots);
        _clipChip = clipChip;
        _mode = Mode.Toolbar;
        Reset();
    }

    public void SetEmojiSearch(string query, IReadOnlyList<string> results)
    {
        _searchQuery = query;
        _searchResults = results;
        _mode = Mode.EmojiSearch;
        Reset();
    }

    public void SetStatus(string status)
    {
        _status = status;
        _mode = Mode.Status;
        Reset();
    }

    void Reset()
    {
        _pressed = -1;
        _targets.Clear();
        Invalidate();
    }

    // ---------- Zeichnen ----------

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.DrawColor(Theme.Background);
        _targets.Clear();

        switch (_mode)
        {
            case Mode.Suggestions:
                LayoutSuggestions();
                break;
            case Mode.Toolbar:
                LayoutToolbar();
                break;
            case Mode.EmojiSearch:
                LayoutEmojiSearch();
                break;
            case Mode.Status:
                _targets.Add((new RectF(0, 0, Width, Height), StatusId, _status));
                break;
        }

        for (int i = 0; i < _targets.Count; i++) DrawTarget(canvas, i);
    }

    void LayoutSuggestions()
    {
        float w = Width / 3f;
        for (int i = 0; i < 3; i++)
        {
            if (_slots[i] is { } slot) _targets.Add((new RectF(i * w, 0, (i + 1) * w, Height), $"slot{i}", slot.Display));
        }
    }

    void LayoutToolbar()
    {
        float x = 0;
        if (_clipChip != null)
        {
            float chipWidth = Width * 0.45f;
            _targets.Add((new RectF(0, 0, chipWidth, Height), ClipChipId, "📋 " + Shorten(_clipChip)));
            x = chipWidth;
        }

        if (ToolbarItems.Count == 0) return;
        float w = (Width - x) / ToolbarItems.Count;
        foreach (var item in ToolbarItems)
        {
            _targets.Add((new RectF(x, 0, x + w, Height), item.Id, item.Icon));
            x += w;
        }
    }

    void LayoutEmojiSearch()
    {
        float pillWidth = Width * 0.35f;
        _targets.Add((new RectF(0, 0, pillWidth, Height), SearchPillId, "🔍 " + _searchQuery + "▏"));
        float cell = 44 * _dp;
        float x = pillWidth;
        foreach (var emoji in _searchResults)
        {
            if (x + cell > Width) break;
            _targets.Add((new RectF(x, 0, x + cell, Height), "emoji:" + emoji, emoji));
            x += cell;
        }
    }

    void DrawTarget(Canvas canvas, int index)
    {
        var (bounds, id, label) = _targets[index];

        if (index == _pressed)
        {
            _fill.Color = Theme.KeyPressed;
            canvas.DrawRect(bounds, _fill);
        }

        bool pill = id is ClipChipId or SearchPillId;
        if (pill)
        {
            _fill.Color = Theme.Key;
            var r = new RectF(bounds.Left + 6 * _dp, bounds.Top + 6 * _dp, bounds.Right - 6 * _dp, bounds.Bottom - 6 * _dp);
            canvas.DrawRoundRect(r, 14 * _dp, 14 * _dp, _fill);
        }

        if (_mode == Mode.Suggestions && index > 0)
        {
            _line.Color = Theme.HintText;
            canvas.DrawLine(bounds.Left, Height * 0.3f, bounds.Left, Height * 0.7f, _line);
        }

        bool emphasized = id.StartsWith("slot") && _slots[id[^1] - '0']?.Emphasized == true;
        _text.SetTypeface(emphasized ? Typeface.DefaultBold : Typeface.Default);
        _text.Color = _mode == Mode.Status ? Theme.Accent : Theme.Text;
        _text.TextSize = (id.StartsWith("emoji:") || _mode == Mode.Toolbar && !pill ? 20 : 16) * _dp;

        float available = bounds.Width() - 16 * _dp;
        float measured = _text.MeasureText(label);
        if (measured > available) _text.TextSize *= available / measured;

        float baseline = bounds.CenterY() - (_text.Descent() + _text.Ascent()) / 2;
        canvas.DrawText(label, bounds.CenterX(), baseline, _text);
    }

    static string Shorten(string text)
    {
        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= 24 ? oneLine : oneLine[..23] + "…";
    }

    // ---------- Berührung ----------

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return false;
        int index = _targets.FindIndex(t => t.Bounds.Contains(e.GetX(), e.GetY()));

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _pressed = index;
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
                int picked = _pressed;
                _pressed = -1;
                Invalidate();
                if (picked >= 0 && picked == index) Dispatch(_targets[picked].Id, e.EventTime);
                break;

            case MotionEventActions.Cancel:
                _pressed = -1;
                Invalidate();
                break;
        }

        return true;
    }

    void Dispatch(string id, long upTime)
    {
        if (id.StartsWith("slot") && _slots[id[^1] - '0'] is { } slot)
        {
            SuggestionPicked?.Invoke(slot, _downTime, upTime);
        }
        else if (id.StartsWith("emoji:"))
        {
            EmojiPicked?.Invoke(id["emoji:".Length..]);
        }
        else
        {
            ToolbarPicked?.Invoke(id);
        }
    }
}
