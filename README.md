# zBoard

[![Build](https://github.com/World-of-Bytes/zBoard/actions/workflows/build.yml/badge.svg)](https://github.com/World-of-Bytes/zBoard/actions/workflows/build.yml)

zBoard ist eine freie Android-Tastatur, die sich in Bedienung und Funktionsumfang an Gboard orientiert:
Wortvorschläge, Autokorrektur, Wischeingabe und Nächstes-Wort-Vorhersage für Deutsch und Englisch.
Anders als Gboard arbeitet zBoard vollständig offline und ohne Internetberechtigung; was es aus deinen Eingaben
lernt, bleibt auf dem Gerät. Geschrieben ist zBoard in C# mit .NET 10 for Android.

Für das Projekt **zHeal Meridian** kann zBoard auf Wunsch Tipp-Metadaten erfassen (Zeitpunkte, Druckdauer,
Pausen, Tastenart), aber nie, welche Zeichen getippt wurden.

Stand: **Meilenstein M3 von 5** – in Entwicklung, noch nicht veröffentlicht.

## zBoard im Vergleich zu Gboard

| | Gboard | zBoard |
| --- | --- | --- |
| Lizenz | proprietär, Quellcode nicht öffentlich | BSD 2-Clause (Code), Wortlisten CC BY-SA 4.0 / CC BY 3.0 |
| Internetzugriff | ja (u. a. GIFs, Übersetzen, Synchronisation) | keine Internetberechtigung |
| Lernen aus Eingaben | ja, Modelle werden per Federated Learning mit Google-Servern verbessert | ja, ausschließlich auf dem Gerät, abschaltbar und löschbar |
| Sprachen | sehr viele Sprachen und Layouts | Deutsch (QWERTZ), Englisch (QWERTY) |
| Tippen, Umschalt, Symbole, langer Druck für Sonderzeichen | ✓ | ✓ |
| Wortvorschläge und Autokorrektur | ✓ | ✓ 120 000 deutsche, 100 000 englische Wörter; Tastennachbarn, Vertauschungen, Umlaute |
| Großschreibung deutscher Substantive | ✓ | ✓ |
| Nächstes-Wort-Vorhersage | ✓ | ✓ mitgelieferte Wortpaare plus eigene Wortfolgen |
| Wischeingabe (Glide Typing) | ✓ | ✓ |
| Cursor per Leertaste, Wort-Löschen per Wischen | ✓ | ✓ |
| Tastenvorschau, Vibration, Ton | ✓ | ✓ |
| Helles/dunkles Design | ✓ | ✓ |
| Emoji-Tastatur | ✓ | geplant (M4) |
| Zwischenablage-Verlauf | ✓ | geplant (M4) |
| GIFs und Sticker | ✓ | GIFs optional geplant (M4) |
| Spracheingabe | ✓ | geplant (M5, offline) |
| Einhandmodus, Größe anpassen | ✓ | geplant (M5) |
| Übersetzen, Google-Dienste | ✓ | nicht geplant |
| Tipp-Statistik für Forschung (nur Metadaten) | – | ✓ standardmäßig aus |

## Bauen und installieren

Voraussetzungen: .NET 10 SDK, Android-Workload, Android SDK (bringen Visual Studio 2026 oder Rider mit).

```bash
dotnet workload install android
dotnet build -t:Run -f net10.0-android          # baut und installiert auf dem verbundenen Gerät/Emulator
dotnet publish -c Release -f net10.0-android    # Release-APK (mit AOT für schnellen Start)
```

Danach die App **zBoard** öffnen:

1. „zBoard in den Systemeinstellungen aktivieren“ antippen und zBoard einschalten
2. „zBoard als Tastatur auswählen“ antippen
3. Im Testfeld unten tippen

## Tests

Die Logik ohne Android (Wörterbuch, Autokorrektur, Vorhersage, Lernen, Wischeingabe, Statistik) hat eigene Unit-Tests:

```bash
dotnet test tests/ZBoard.Core.Tests
```

## Aufbau

| Ordner | Inhalt |
| --- | --- |
| `Ime/` | `ZBoardImeService` (Herzstück), `EditorProfile` (Eigenschaften des Eingabefelds), `TextBoundaries` |
| `Keyboard/` | Layout-Modell, alle Layouts, `KeyboardView` (Zeichnen und Touch), Farben |
| `Suggestions/` | `WordDictionary` (sortiertes Array), `SuggestionEngine`, `GlideDecoder`, `BigramModel`, `UserHistory`, `KeyProximity`, `SuggestionBarView` |
| `Stats/` | `KeystrokeEvent`, `KeystrokeStatsRecorder`, `IKeystrokeStatsSink`, `JsonlStatsSink` |
| `Settings/` | `SettingsActivity` (Startbildschirm), `ZBoardSettings` |
| `Assets/dict/` | Wortlisten pro Sprache (`wort<TAB>häufigkeit`) |
| `tools/` | `build_dictionaries.py` erzeugt die Wortlisten |
| `tests/` | Unit-Tests der plattformunabhängigen Logik |

## Wörterbücher

Mitgeliefert: **Deutsch** mit 120 000 Wörtern, **Englisch** mit 100 000 Wörtern, jeweils nach Häufigkeit
aus Film- und Serienuntertiteln (OpenSubtitles 2018). Format: eine Schreibweise pro Zeile, `wort<TAB>häufigkeit`.
Typische Tippfehler aus den Untertiteln („nciht“, „waht“) filtert das Skript heraus.

Für die Vorhersage: je 5 000 häufigste Wortpaare (`de.bigrams.txt`, `en.bigrams.txt`) aus dem Google Books
Ngram Corpus, Format `erstes<TAB>zweites<TAB>häufigkeit`. Den Rest lernt zBoard aus deinen Eingaben
(`files/learning/<sprache>.tsv` im app-privaten Speicher).

Großschreibung im Deutschen:

- Reine Substantive stehen nur großgeschrieben in der Liste. zBoard schreibt „haus“ beim Leerzeichen
  automatisch als „Haus“ (Löschen direkt danach macht das rückgängig).
- Mehrdeutige Wörter („frage/Frage“, „essen/Essen“) stehen in beiden Schreibweisen. zBoard lässt das
  Getippte stehen und bietet die andere Form in der Vorschlagsleiste an.

### Wörterbücher neu erzeugen

```bash
git clone --depth 1 --filter=blob:none --sparse https://github.com/hermitdave/FrequencyWords
git -C FrequencyWords sparse-checkout set --no-cone /content/2018/de/de_full.txt /content/2018/en/en_full.txt
git clone --depth 1 https://github.com/gambolputty/german-nouns
git clone --depth 1 --filter=blob:none --sparse https://github.com/wooorm/dictionaries
git -C dictionaries sparse-checkout set --no-cone /dictionaries/de/index.dic

python3 tools/build_dictionaries.py \
  --freq-de FrequencyWords/content/2018/de/de_full.txt \
  --freq-en FrequencyWords/content/2018/en/en_full.txt \
  --nouns german-nouns/german_nouns/nouns.csv \
  --hunspell-de dictionaries/dictionaries/de/index.dic \
  --bigrams-de google-books-ngram-frequency/ngrams/2grams_german.csv \
  --bigrams-en google-books-ngram-frequency/ngrams/2grams_english.csv
```

Die Wortpaare kommen aus `git clone --depth 1 https://github.com/orgtre/google-books-ngram-frequency`.

Fehler in der Großschreibung lassen sich über die Handlisten oben im Skript korrigieren
(`DE_FORCE_LOWER`, `DE_FORCE_NOUN`, `DE_FORCE_BOTH`).

## Lizenz

Der Code steht unter der **BSD 2-Clause License** (`LICENSE`). Die Wortlisten in `Assets/dict/` stehen unter
**CC BY-SA 4.0**, die Wortpaare unter **CC BY 3.0**; Quellen und Pflichten in `THIRD_PARTY_NOTICES.md`. Die App nennt die Quellen unter „Über zBoard“.

## Wischeingabe

`GlideDecoder` vergleicht die gewischte Spur mit der Idealspur jedes Wortes, dessen erster und letzter Buchstabe
nahe am Spuranfang und -ende liegen: Beide Linien werden auf 40 gleichmäßig verteilte Punkte gebracht, die
mittlere Abweichung bestimmt zusammen mit der Worthäufigkeit, dem Satzkontext und gelernten Wörtern die Reihenfolge.

Die Parameter sind mit simulierten Wischspuren auf dem deutschen Wörterbuch abgestimmt
(je 100 Wörter, Treffer auf Platz 1 / unter den ersten 3):

| Wörter | saubere Spur | ungenaue Spur |
| --- | --- | --- |
| häufig (Top 400) | 99 % / 100 % | 84 % / 99 % |
| mittel (Rang 400–5 000) | 94 % / 100 % | 79 % / 95 % |
| selten (Rang 5 000–30 000) | 78 % / 98 % | 68 % / 92 % |

„Ungenau“ heißt: Anfang und Ende bis zu einer halben Taste daneben, Ecken mit Abweichung. Viele verbleibende
Verwechslungen sind echte Doppeldeutigkeiten („bist/biest“, „fast/fest“), die auch Gboard nur über die
Vorschlagsleiste auflöst. Echte Wischspuren auf dem Gerät werden die Werte noch verschieben.

## Statistik-Format (zHeal Meridian)

Dateien im app-privaten Speicher: `files/stats/keystrokes-JJJJ-MM-TT.jsonl`, Aufbewahrung 30 Tage.

```json
{"type":"key","t":1762294812345,"cat":"Letter","hold":84,"flight":132,"session":"…"}
{"type":"session","session":"…","start":…,"end":…,"keystrokes":214,"cpm":187.3,"correctionRate":0.061,"pausesOver2s":3,"suggestions":4,"autocorrections":2}
```

`cat` ist eine von: Letter, Digit, Punctuation, Space, Backspace, Enter, Shift, Suggestion, Other.
Nichts wird in Passwortfeldern oder Feldern mit `IME_FLAG_NO_PERSONALIZED_LEARNING` erfasst.

## Bewusste Abgrenzung zu Gboard

Funktionen und Bedienung orientieren sich an Gboard. Name, Logo, Icons, Farben, Code und Wörterbuchdaten
von Google werden nicht übernommen. zBoard hat keine Internet-Berechtigung (Release-Build).

## Nächste Schritte (M4)

- Auf einem echten Gerät bauen und messen: Ladezeit, Speicher, Tipp-Latenz, Wischerkennung mit echten Spuren
- Emoji-Tastatur mit Kategorien, Suche und „Zuletzt verwendet“
- Zwischenablage-Verlauf mit Anheften
- GIFs (Anbieter klären, optional)
