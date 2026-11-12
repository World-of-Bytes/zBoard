# Vorschlagsmessung

Misst, wie gut Wortvorschläge und Autokorrektur sind, indem es einen Testtext mit simulierten Tippfehlern „tippt“.
Benutzt dieselben Quelltexte wie die App (`Suggestions/`, `Keyboard/Layouts.cs`) und die Wortlisten aus `Assets/dict/`.

```bash
dotnet run -c Release --project tools/SuggestionEval -- --lang de
dotnet run -c Release --project tools/SuggestionEval -- --lang en --verbose        # mit Fehlerbeispielen
dotnet run -c Release --project tools/SuggestionEval -- --lang de --text mein.txt  # eigener Text, ein Satz pro Zeile
```

Weitere Optionen: `--runs`, `--sigma`, `--lazy`, `--seed`, `--model`, `--no-context` (siehe `--help`). Gleiche Optionen ergeben immer
dieselben Zahlen.

## Was gemessen wird

**Vorschläge (fehlerfrei getippt):** Für jedes Wort wird geprüft, ob es vor dem ersten Buchstaben vorhergesagt wird
und ab welchem Buchstaben es in der Leiste steht. Den Satzkontext liest die Messung wie der Tastaturdienst
(`WordContext.FromText`). „Gesparte Tastendrücke“ rechnet mit einem Nutzer, der einen
Vorschlag antippt, sobald er sichtbar ist.

**Autokorrektur (ungenau getippt):** Jeder Tastendruck landet normalverteilt um die Tastenmitte (`--sigma`, in
Tastenbreiten); die nächstgelegene Taste zählt. Dazu kommen ausgelassene, doppelte und vertauschte Buchstaben und
Umlaute, die als Grundbuchstabe getippt werden („fur“). Nach jedem Wort folgt die Leertaste; verglichen wird, was
dann im Text stünde.

Wie am Gerät tippt der simulierte Nutzer mitten im Satz alles klein und überlässt die Großschreibung der Tastatur.
Gelerntes gibt es nicht (frisch installierte Tastatur).

## Werte (mitgelieferte Testsätze)

| | Deutsch 0.11.1 | Deutsch jetzt | Englisch 0.11.1 | Englisch jetzt |
| --- | --- | --- | --- | --- |
| Nächstes Wort vorhergesagt, unter den ersten 3 | – | 31,0 % | – | 35,0 % |
| … nur nach einem Wort (ohne Satzanfang und Komma) | 15,6 % | 28,2 % | 18,2 % | 33,6 % |
| Wort nach der Hälfte der Buchstaben in der Leiste | 61,1 % | 75,4 % | 71,0 % | 82,8 % |
| Gesparte Tastendrücke | 42,1 % | 50,9 % | 39,3 % | 49,7 % |
| Tippfehler richtig korrigiert | 66,3 % | 76,9 % | 60,1 % | 72,7 % |
| Richtig getippte Wörter, die verändert wurden | 0,0 % | 0,0 % | 1,4 % | 0,0 % |
| Richtig getippte Wörter mit falscher Groß-/Kleinschreibung | 2,0 % | 1,2 % | 1,0 % | 1,0 % |
| Falsche Wörter im Text nach der Korrektur | 8,8 % | 5,8 % | 8,5 % | 5,3 % |

Seit 0.11.1 dazugekommen:

1. Englische Kurzformen in der Wortliste („don't“, „I'm“) und die Korrektur seltener Wörterbucheinträge („fur“ →
   „für“, „dont“ → „don't“, „yiu“ → „you“; `LazySpellingRatio` und `RareWordRatio` in `SuggestionEngine`).
2. Das Sprachmodell (`NgramModel`, erzeugt mit `tools/NgramBuilder`) statt der 5 000 Wortpaare: Vorhersage aus zwei
   Wörtern Kontext, auch am Satzanfang und nach Komma; Vorschläge und Korrekturen nach dem Satz geordnet
   (`MaxContextFactor`); Großschreibung mehrdeutiger Wörter aus dem Satz („eine frage“ → „Frage“, `NounContextRatio`).

## Grenzen

- Die Testsätze in `samples/` sind wenige (rund 1 000 Wörter je Sprache) und eigens geschrieben. Unterschiede unter
  etwa einem Prozentpunkt sind Zufall; mit `--runs 20` oder einem größeren Text wird es genauer.
- Die Tippfehler sind simuliert. Echte Finger treffen systematisch daneben (z. B. zu tief), das bildet die
  Normalverteilung nicht ab.
- Wischeingabe, mehrsprachige Eingabe und Gelerntes werden nicht gemessen.
