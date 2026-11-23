# Neuronales Sprachmodell (KI-Vorhersage)

Ein kleines LSTM je Sprache sagt das nächste Wort aus dem **ganzen Satz** bis zur Schreibstelle vorher (bis zu 24
Wörter), nicht nur aus den beiden Wörtern davor wie das N-Gramm-Modell (`Assets/dict/<Sprache>.ngrams.bin`). Beide
werden gemischt (`SuggestionEngine.NeuralWeight`): bei der Nächstes-Wort-Vorhersage, bei der Reihenfolge der
Vorschläge, bei der Wahl der Autokorrektur und beim Wischen. Ob ein Wort aus dem Wörterbuch überhaupt korrigiert wird,
entscheidet weiterhin die Bewertung ohne Satzkontext – das Netz kann also keine richtig getippten Wörter „verschlimmbessern“.

Läuft vollständig auf dem Gerät mit [ONNX Runtime](https://onnxruntime.ai) (MIT). Fehlt für eine Sprache das Modell
in `Assets/lm/`, arbeitet zBoard wie bisher nur mit N-Grammen. Abschaltbar unter Einstellungen → Vorschläge →
„KI-Vorhersage“; dann wird das Netz gar nicht erst geladen.

| | Vorgabe |
| --- | --- |
| Vokabular | 16 000 häufigste Schreibweisen der Wortliste (`--vocab`) |
| Netz | Einbettung 128, LSTM 256, Ein- und Ausgabe teilen sich eine Matrix (`--embed`, `--hidden`, `--no-tie`) |
| Größe | 2,5 Mio. Gewichte, als int8 etwa **2,5 MB pro Sprache** (18 Sprachen ≈ 45 MB) |
| Rechenzeit | unter 1 ms pro Vorhersage; die letzten 8 Satzstellen werden zwischengespeichert |
| Arbeitsspeicher | etwa 10–15 MB pro aktiver Sprache |

## Dateien

| Datei | Inhalt |
| --- | --- |
| `Assets/lm/<Sprache>.lm.onnx` | Netz: Eingang `tokens` (int64, [1, Länge]), Ausgang `probs` (float, [1, Vokabular]) |
| `Assets/lm/<Sprache>.vocab.txt` | Vokabular, Zeile i = Eintrag i: `wort<TAB>anteil` (Anteil am Trainingstext); die ersten vier sind `<pad>`, `<unk>`, `<s>`, `,` |
| `Suggestions/NeuralModel.cs` | Vokabular, Verteilung, Laden im Hintergrund, Zwischenspeicher (plattformunabhängig, mit Unit-Tests) |
| `Suggestions/OnnxLanguageModel.cs` | Ausführen mit ONNX Runtime |

## Trainieren

Voraussetzungen: Python 3.10+, `pip install torch onnx onnxruntime numpy` (für Chinesisch zusätzlich `jieba`).
Mit einer NVIDIA-Grafikkarte nimmt PyTorch sie von selbst (`--device cuda`); dann dauert eine Sprache wenige Minuten,
auf der CPU etwa 1–3 Stunden.

```bash
git clone --depth 1 --filter=blob:none --sparse https://github.com/common-voice/common-voice
git -C common-voice sparse-checkout set --no-cone '/server/data/de/*'

python3 tools/LanguageModel/train_lm.py --lang de --common-voice common-voice/server/data/de
```

Alle 18 Sprachen nacheinander (lädt die Satzsammlung selbst, falls nötig):

```bash
tools/LanguageModel/train_all.sh                 # alle
tools/LanguageModel/train_all.sh de en fr        # nur diese
```

Das Skript zeigt alle 5 000 Schritte die Perplexität und wie oft das nächste Wort im Prüftext auf Platz 1 bzw. unter
den ersten 3 steht, prüft das fertige int8-Modell gegen PyTorch und schreibt beide Dateien nach `Assets/lm/`.

Wichtige Optionen: `--steps` (Vorgabe 30 000), `--batch`, `--vocab`, `--source Datei.txt:Gewicht` (eigene Sätze, ein
Satz pro Zeile, mehrfach möglich), `--max-lines` (zum schnellen Ausprobieren), `--seed`.

## Quellen

Wie beim N-Gramm-Modell die Satzsammlung von [Mozilla Common Voice](https://github.com/common-voice/common-voice)
(CC0), gleich gewichtet: eingesandte Alltagssätze 10, Europarl 1, Wikipedia 0,15.

Für einige Sprachen ist sie klein (Griechisch, Vietnamesisch, Rumänisch, Chinesisch, Russisch, Arabisch, Bulgarisch:
unter 5 MB). Das Skript warnt bei weniger als 1 Mio. Wörtern. Dann lohnt sich mehr Text mit `--source`, z. B. die
Satzlisten von [Tatoeba](https://tatoeba.org/de/downloads) (CC BY 2.0 FR) oder die
[Leipzig Corpora](https://wortschatz.uni-leipzig.de/de/download) (CC BY 4.0, Nachrichten/Web, je Sprache 1 Mio. Sätze).
Lizenz der Quelle dann in `THIRD_PARTY_NOTICES.md` nachtragen. Ohne genug Text lieber kein Modell für diese Sprache
ausliefern: Ein schwaches Netz bringt gegenüber den N-Grammen nichts.

## Wörter lesen wie beim Tippen

Training und Tastatur müssen dasselbe unter „Kontext“ verstehen. `train_lm.py` liest Text nach denselben Regeln wie
`WordContext.FromTextWithSentence` und `tools/NgramBuilder`: Wörter aus Buchstaben, Ziffern, Apostroph und
Bindestrich, Komma als eigenes Zeichen, nach `. ! ? …` beginnt ein Satz (`<s>`), jedes andere Satzzeichen unterbricht
den Kontext. Wörter stehen in der Schreibweise der Wortliste (am Satzanfang bei mehrdeutigen Wörtern die häufigere).
Nach Änderungen an der Wortliste daher neu trainieren.

Chinesisch trennt die Wörter mit jieba; beim Tippen bekommt das Netz bisher nur das zuletzt eingesetzte Wort
(`ChineseContext`).

## Messen und abstimmen

`tools/SuggestionEval` nimmt das Modell aus `Assets/lm/` automatisch mit; Vergleich ohne Netz:

```bash
dotnet run -c Release --project tools/SuggestionEval -- --lang de               # mit Netz
dotnet run -c Release --project tools/SuggestionEval -- --lang de --no-neural   # nur N-Gramme
```

`NeuralWeight` (Anteil des Netzes) und `MinNeuralFactor` (wie stark das Netz ein unpassendes Wort abwerten darf) in
`SuggestionEngine` damit abstimmen.
