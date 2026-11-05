using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech;
using Android.Widget;
using ZBoard.Keyboard;
using ZBoard.Settings;

namespace ZBoard.Ime;

/// <summary>
/// Spracheingabe über den SpeechRecognizer des Systems. Bevorzugt die Erkennung auf dem Gerät (ab Android 12);
/// mit „Nur offline“ (Standard) wird nichts an einen Server geschickt.
/// </summary>
public sealed partial class ZBoardImeService
{
    public const string RequestMicExtra = "zboard.request_mic";

    /// <summary>
    /// So lange warten wir nach dem Ende der Aufnahme auf das Endergebnis. Manche Geräte (Offline-Erkennung)
    /// liefern danach kein OnResults mehr; dann gilt das letzte Zwischenergebnis.
    /// </summary>
    const int VoiceResultTimeoutMs = 2500;

    SpeechRecognizer? _recognizer;
    bool _voiceActive;
    bool _voiceHasPartial;
    bool _voiceEnded;                     // Aufnahme beendet, warten auf das Ergebnis
    Java.Lang.Runnable? _voiceTimeout;
    Handler? _voiceHandler;
    string _voicePrefix = string.Empty;   // Leerzeichen vor dem gesprochenen Text, beim Start festgelegt

    void StartVoice()
    {
        if (_voiceActive)
        {
            // Antippen während „Wird erkannt …“: nicht länger warten, Zwischenergebnis übernehmen
            StopVoice(cancel: _voiceEnded);
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            // Ein Tastatur-Dienst kann nicht selbst nach Berechtigungen fragen: Einstellungen übernehmen das.
            var intent = new Intent(this, typeof(SettingsActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            intent.PutExtra(RequestMicExtra, true);
            StartActivity(intent);
            return;
        }

        _recognizer = CreateRecognizer();
        if (_recognizer == null) return;

        if (_composing.Length > 0) CommitComposing(autocorrect: true, separator: string.Empty);

        var recognize = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        recognize.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        recognize.PutExtra(RecognizerIntent.ExtraLanguage, Layouts.LocaleTag(_language));
        recognize.PutExtra(RecognizerIntent.ExtraPartialResults, true);
        recognize.PutExtra(RecognizerIntent.ExtraPreferOffline, true);

        _recognizer.SetRecognitionListener(new VoiceListener(this));
        _voiceActive = true;
        _voiceHasPartial = false;
        _voiceEnded = false;
        string before = TextBeforeCursor(1);
        _voicePrefix = before.Length > 0 && !char.IsWhiteSpace(before[0]) ? " " : string.Empty;
        _suggestionBar?.SetStatus("Ich höre zu … (antippen zum Beenden)");
        _recognizer.StartListening(recognize);
    }

    SpeechRecognizer? CreateRecognizer()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && SpeechRecognizer.IsOnDeviceRecognitionAvailable(this))
        {
            return SpeechRecognizer.CreateOnDeviceSpeechRecognizer(this);
        }

        if (_settings.VoiceOfflineOnly)
        {
            Toast.MakeText(this,
                "Dieses Gerät bietet keine Offline-Spracherkennung. In den zBoard-Einstellungen lässt sich die Online-Erkennung des Systems erlauben.",
                ToastLength.Long)!.Show();
            return null;
        }

        if (!SpeechRecognizer.IsRecognitionAvailable(this))
        {
            Toast.MakeText(this, "Auf diesem Gerät ist keine Spracherkennung installiert.", ToastLength.Long)!.Show();
            return null;
        }

        return SpeechRecognizer.CreateSpeechRecognizer(this);
    }

    void StopVoice(bool cancel)
    {
        if (_recognizer == null) return;
        if (cancel)
        {
            _recognizer.Cancel();
            EndVoice();
        }
        else
        {
            _recognizer.StopListening(); // Ergebnis kommt noch über OnResults
            OnVoiceRecordingEnded();
        }
    }

    void OnVoiceRecordingEnded()
    {
        if (!_voiceActive || _voiceEnded) return;
        _voiceEnded = true;
        _suggestionBar?.SetStatus("Wird erkannt …");
        _voiceHandler ??= new Handler(Looper.MainLooper!);
        _voiceTimeout ??= new Java.Lang.Runnable(() =>
        {
            if (!_voiceActive) return;
            _recognizer?.Cancel();
            EndVoice();
        });
        _voiceHandler.RemoveCallbacks(_voiceTimeout);
        _voiceHandler.PostDelayed(_voiceTimeout, VoiceResultTimeoutMs);
    }

    void EndVoice()
    {
        if (_voiceTimeout != null) _voiceHandler?.RemoveCallbacks(_voiceTimeout);
        var recognizer = _recognizer;
        _recognizer = null;
        _voiceActive = false;
        _voiceEnded = false;
        // Nicht innerhalb eines Rückrufs des Erkenners zerstören
        if (recognizer != null) (_voiceHandler ??= new Handler(Looper.MainLooper!)).Post(recognizer.Destroy);
        if (_voiceHasPartial)
        {
            Ic?.FinishComposingText();
            _voiceHasPartial = false;
        }

        UpdateSuggestions();
    }

    void OnVoicePartial(string text)
    {
        var ic = Ic;
        if (!_voiceActive || ic == null || string.IsNullOrWhiteSpace(text)) return;
        ic.SetComposingText(J(VoiceText(text)), 1);
        _voiceHasPartial = true;
    }

    void OnVoiceFinal(string? text)
    {
        if (!_voiceActive) return; // schon per Zeitlimit beendet
        var ic = Ic;
        if (ic != null && !string.IsNullOrWhiteSpace(text))
        {
            ic.CommitText(J(VoiceText(text)), 1); // ersetzt das Zwischenergebnis
            _voiceHasPartial = false;
            UpdateAutoShift();
        }

        EndVoice();
    }

    /// <summary>Leerzeichen davor, wenn nötig; Großbuchstabe am Satzanfang.</summary>
    string VoiceText(string text)
    {
        if (_shift != ShiftState.Off && text.Length > 0) text = char.ToUpper(text[0]) + text[1..];
        return _voicePrefix + text;
    }

    void OnVoiceError(SpeechRecognizerError error)
    {
        if (!_voiceActive) return;
        if (_voiceHasPartial)
        {
            // Erkannt wurde schon etwas: behalten statt Fehlermeldung
            EndVoice();
            return;
        }

        string message = error switch
        {
            SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout => "Nichts verstanden.",
            SpeechRecognizerError.Audio => "Mikrofon nicht verfügbar.",
            SpeechRecognizerError.InsufficientPermissions => "Keine Berechtigung für das Mikrofon.",
            SpeechRecognizerError.Network or SpeechRecognizerError.NetworkTimeout => "Spracherkennung braucht eine Verbindung.",
            _ => "Spracherkennung fehlgeschlagen.",
        };
        Toast.MakeText(this, message, ToastLength.Short)!.Show();
        EndVoice();
    }

    sealed class VoiceListener(ZBoardImeService owner) : Java.Lang.Object, IRecognitionListener
    {
        public void OnReadyForSpeech(Bundle? @params)
        {
        }

        public void OnBeginningOfSpeech()
        {
        }

        public void OnRmsChanged(float rmsdB)
        {
        }

        public void OnBufferReceived(byte[]? buffer)
        {
        }

        public void OnEndOfSpeech() => owner.OnVoiceRecordingEnded();

        public void OnError(SpeechRecognizerError error) => owner.OnVoiceError(error);

        public void OnPartialResults(Bundle? partialResults)
        {
            var text = partialResults?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault();
            if (text != null) owner.OnVoicePartial(text);
        }

        public void OnResults(Bundle? results) =>
            owner.OnVoiceFinal(results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault());

        public void OnEvent(int eventType, Bundle? @params)
        {
        }
    }
}
