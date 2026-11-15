using ZBoard.Keyboard;
using ZBoard.Suggestions;

namespace ZBoard.Ime;

/// <summary>
/// Chinesisch wie bei Gboard: Man tippt Pinyin auf QWERTY, das Getippte steht unterstrichen im Text, und die
/// Vorschlagsleiste bietet die Schriftzeichen an. Die Leertaste nimmt den ersten Vorschlag, Enter die Buchstaben.
/// Die Auswahl selbst steckt in <see cref="PinyinEngine"/> (mit Unit-Tests); hier steht nur die Anbindung.
/// </summary>
public sealed partial class ZBoardImeService
{
    /// <summary>Chinesische Satzzeichen, die wie ".,!?" ein Wort abschließen.</summary>
    const string ChinesePunctuation = "，。？！；：、";

    /// <summary>Wort des Eintrags "⋯" in der Leiste: zeigt die nächsten Vorschläge.</summary>
    const string PinyinMore = "\u0001more";

    const int PinyinMaxCandidates = 40;

    static Task<PinyinEngine>? _pinyinTable;

    IReadOnlyList<PinyinCandidate> _pinyinCandidates = [];
    string _pinyinFor = string.Empty; // Eingabe, zu der die Vorschläge gehören
    int _pinyinFirst;                 // erster gezeigter Vorschlag (Blättern mit "⋯")
    string? _chineseLast;             // zuletzt eingesetztes Wort, für die Vorhersage (Chinesisch hat keine Leerzeichen)

    bool IsChinese => _language == Layouts.Chinese;

    PinyinEngine? Pinyin => IsChinese && _pinyinTable is { IsCompletedSuccessfully: true } t ? t.Result : null;

    /// <summary>Lädt die Pinyin-Tabelle beim ersten Wechsel zu Chinesisch.</summary>
    void EnsurePinyin()
    {
        if (!IsChinese || _pinyinTable is { IsFaulted: false }) return;
        var assets = Assets!;
        _pinyinTable = Task.Run(() =>
        {
            using var stream = assets.Open("dict/zh.pinyin.txt");
            return PinyinEngine.Load(stream);
        });
        _pinyinTable.ContinueWith(
            _ => new Android.OS.Handler(Android.OS.Looper.MainLooper!).Post(UpdateSuggestions),
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    void ResetPinyin()
    {
        _pinyinCandidates = [];
        _pinyinFor = string.Empty;
        _pinyinFirst = 0;
    }

    /// <summary>
    /// Zeigt die Schriftzeichen zum getippten Pinyin: bester Vorschlag in der Mitte, rechts "⋯" für weitere.
    /// Ist das Getippte kein Pinyin ("WLAN", "abc1"), steht es selbst in der Leiste.
    /// </summary>
    void ShowPinyinCandidates()
    {
        if (_suggestionBar == null) return;
        string typed = _composing.ToString();
        if (typed != _pinyinFor)
        {
            _pinyinCandidates = Pinyin?.Candidates(typed, PinyinMaxCandidates) ?? [];
            _pinyinFor = typed;
            _pinyinFirst = 0;
        }

        _currentSuggestions = SuggestionResult.Empty;
        var candidates = _pinyinCandidates;
        if (candidates.Count == 0)
        {
            _suggestionBar.SetSlots(null, new SuggestionSlot(typed, typed, false, true), null);
            return;
        }

        if (_pinyinFirst >= candidates.Count) _pinyinFirst = 0;
        int first = _pinyinFirst;
        SuggestionSlot? Slot(int offset) => first + offset < candidates.Count
            ? new SuggestionSlot(candidates[first + offset].Text, candidates[first + offset].Text, first + offset == 0, false)
            : null;

        // Drei Plätze: Gibt es mehr Vorschläge, blättert der rechte Platz weiter.
        var right = candidates.Count > 3 ? new SuggestionSlot("⋯", PinyinMore, false, false) : Slot(2);
        _suggestionBar.SetSlots(Slot(1), Slot(0), right);
    }

    /// <summary>Antippen eines Eintrags der Leiste im chinesischen Layout. false: kein chinesischer Eintrag.</summary>
    bool PickChinese(SuggestionSlot slot)
    {
        if (!IsChinese || slot.IsLiteral) return false;
        var ic = Ic;
        if (ic == null) return false;

        if (slot.Word == PinyinMore)
        {
            _pinyinFirst += 2;
            ShowPinyinCandidates();
            return true;
        }

        if (_composing.Length > 0)
        {
            if (_glideActive || _pinyinFor != _composing.ToString()) return false;
            foreach (var candidate in _pinyinCandidates)
            {
                if (candidate.Text != slot.Word) continue;
                ApplyPinyin(candidate);
                return true;
            }

            return false;
        }

        // Vorhersage des nächsten Worts: ohne Leerzeichen anhängen
        MarkEdit();
        string word = slot.Word.Replace(" ", string.Empty);
        string? previous = ChinesePrevious();
        ic.CommitText(J(word), 1);
        _lastCorrection = null;
        _autoSpaceInserted = false;
        Learn(previous, word);
        _chineseLast = word;
        UpdateSuggestions();
        return true;
    }

    /// <summary>Setzt die Schriftzeichen ein; übrige Buchstaben bleiben in Bearbeitung ("nihaoma" → 你好 + "ma").</summary>
    void ApplyPinyin(PinyinCandidate candidate)
    {
        var ic = Ic;
        if (ic == null) return;
        MarkEdit();

        string typed = _composing.ToString();
        string rest = candidate.Consumed < typed.Length ? typed[candidate.Consumed..] : string.Empty;
        string? previous = ChinesePrevious();

        ic.BeginBatchEdit();
        ic.CommitText(J(candidate.Text), 1);
        _composing.Clear().Append(rest);
        if (rest.Length > 0) ic.SetComposingText(J(rest), 1);
        ic.EndBatchEdit();

        _lastCorrection = null;
        _restoredWord = null;
        _lastSpaceCommit = null;
        _autoSpaceInserted = false;
        ResetPinyin();
        Learn(previous, candidate.Text);
        _chineseLast = candidate.Text;
        UpdateAutoShift();
        UpdateSuggestions();
    }

    /// <summary>
    /// Schließt die Eingabe im chinesischen Layout ab. Mit <paramref name="convert"/> (Leertaste, Satzzeichen) wird
    /// der erste Vorschlag eingesetzt, sonst (Enter, Cursor bewegt) bleiben die Buchstaben, wie sie getippt wurden.
    /// </summary>
    void CommitChinese(bool convert, string separator)
    {
        var ic = Ic;
        if (ic == null) return;

        string typed = _composing.ToString();
        IReadOnlyList<PinyinCandidate> candidates = convert && Pinyin is { } pinyin ? pinyin.Candidates(typed, 1) : [];
        if (candidates.Count > 0 && separator == " " && candidates[0].Consumed < typed.Length)
        {
            ApplyPinyin(candidates[0]); // Leertaste nimmt den ersten Vorschlag; mit dem Rest geht es weiter
            return;
        }

        string? previous = ChinesePrevious();
        if (candidates.Count > 0)
        {
            // Zwischen Schriftzeichen steht kein Leerzeichen.
            string rest = candidates[0].Consumed < typed.Length ? typed[candidates[0].Consumed..] : string.Empty;
            ic.CommitText(J(candidates[0].Text + rest + (separator == " " ? string.Empty : separator)), 1);
            Learn(previous, candidates[0].Text);
            _chineseLast = rest.Length == 0 && separator is "" or " " ? candidates[0].Text : null;
        }
        else
        {
            ic.CommitText(J(typed + separator), 1);
            _chineseLast = null;
        }

        _composing.Clear();
        _glideActive = false;
        _restoredWord = null;
        _lastSpaceCommit = null;
        _currentSuggestions = SuggestionResult.Empty;
        ResetPinyin();
    }

    /// <summary>Das zuletzt eingesetzte chinesische Wort, wenn es noch direkt vor dem Cursor (bzw. vor dem Getippten) steht.</summary>
    string? ChinesePrevious()
    {
        if (_chineseLast is not { } last) return null;
        string expected = last + _composing;
        return TextBeforeCursor(expected.Length) == expected ? last : null;
    }

    /// <summary>Kontext für die Vorhersage im chinesischen Layout: das zuletzt eingesetzte Wort.</summary>
    WordContext ChineseContext() => ChinesePrevious() is { } last ? new WordContext(last) : WordContext.None;
}
