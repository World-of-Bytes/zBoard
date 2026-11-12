# Vorschlagsmessung

Misst, wie gut Wortvorschläge und Autokorrektur sind, indem es einen Testtext mit simulierten Tippfehlern „tippt“.
Benutzt dieselben Quelltexte wie die App (`Suggestions/`, `Keyboard/Layouts.cs`) und die Wortlisten aus `Assets/dict/`.

```bash
dotnet run -c Release --project tools/SuggestionEval -- --lang de
dotnet run -c Release --project tools/SuggestionEval -- --lang en --verbose        # mit Fehlerbeispielen
dotnet run -c Release --project tools/SuggestionEval -- --lang de --text mein.txt  # eigener Text, ein Satz pro Zeile
```

Weitere Optionen: `--runs`, `--sigma`, `--seed`, `--no-context` (siehe `--help`). Gleiche Optionen ergeben immer
dieselben Zahlen.

## Was gemessen wird

**Vorschläge (fehlerfrei getippt):** Für jedes Wort wird geprüft, ob es vor dem ersten Buchstaben vorhergesagt wird
und ab welchem Buchstaben es in der Leiste steht. „Gesparte Tastendrücke“ rechnet mit einem Nutzer, der einen
Vorschlag antippt, sobald er sichtbar ist.

**Autokorrektur (ungenau getippt):** Jeder Tastendruck landet normalverteilt um die Tastenmitte (`--sigma`, in
Tastenbreiten); die nächstgelegene Taste zählt. Dazu kommen ausgelassene, doppelte und vertauschte Buchstaben und
Umlaute, die als Grundbuchstabe getippt werden („fur“). Nach jedem Wort folgt die Leertaste; verglichen wird, was
dann im Text stünde.

Wie am Gerät tippt der simulierte Nutzer mitten im Satz alles klein und überlässt die Großschreibung der Tastatur.
Gelerntes gibt es nicht (frisch installierte Tastatur).

## Ausgangswerte (Version 0.11.1, mitgelieferte Testsätze)

| | Deutsch | Englisch |
| --- | --- | --- |
| Nächstes Wort vorhergesagt, unter den ersten 3 | 15,6 % | 18,2 % |
| Wort nach der Hälfte der Buchstaben in der Leiste | 61,1 % | 71,0 % |
| Gesparte Tastendrücke | 42,1 % | 39,3 % |
| Tippfehler richtig korrigiert | 66,3 % | 60,1 % |
| Richtig getippte Wörter, die verändert wurden | 0,0 % | 1,4 % |
| Falsche Wörter im Text nach der Korrektur | 8,8 % | 8,5 % |

## Grenzen

- Die Testsätze in `samples/` sind wenige (rund 1 000 Wörter je Sprache) und eigens geschrieben. Unterschiede unter
  etwa einem Prozentpunkt sind Zufall; mit `--runs 20` oder einem größeren Text wird es genauer.
- Die Tippfehler sind simuliert. Echte Finger treffen systematisch daneben (z. B. zu tief), das bildet die
  Normalverteilung nicht ab.
- Wischeingabe, mehrsprachige Eingabe und Gelerntes werden nicht gemessen.
