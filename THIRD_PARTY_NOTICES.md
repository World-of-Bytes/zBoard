# Fremde Inhalte in zBoard

Der Quellcode von zBoard steht unter der BSD 2-Clause License (siehe `LICENSE`).
Die mitgelieferten Wortlisten sind **Daten** mit eigener Lizenz:

## Assets/dict/de.txt und Assets/dict/en.txt

**Lizenz:** [Creative Commons Attribution-ShareAlike 4.0 (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/)

Erstellt mit `tools/build_dictionaries.py` aus:

| Quelle | Verwendung | Lizenz |
| --- | --- | --- |
| [FrequencyWords](https://github.com/hermitdave/FrequencyWords) von Hermit Dave, Daten aus [OpenSubtitles 2018](http://opus.nlpl.eu/OpenSubtitles2018.php) | Wörter und Häufigkeiten (Deutsch, Englisch) | CC BY-SA 4.0 (Inhalte) |
| [german-nouns](https://github.com/gambolputty/german-nouns) von Gregor Weichbrodt, Daten aus [Wiktionary](https://de.wiktionary.org) | Großschreibung deutscher Substantive | CC BY-SA 4.0 |

Änderungen gegenüber den Quellen: auf die häufigsten 120 000 (Deutsch) bzw. 100 000 (Englisch) Wörter gekürzt,
Wörter mit Ziffern und Sonderzeichen entfernt, deutsche Substantive großgeschrieben, bei mehrdeutigen Wörtern
beide Schreibweisen aufgenommen.

Wer diese Wortlisten verändert und weitergibt, muss sie ebenfalls unter CC BY-SA 4.0 weitergeben.
Für den Code von zBoard gilt diese Pflicht nicht.

## Assets/dict/de.bigrams.txt und Assets/dict/en.bigrams.txt

**Lizenz:** [Creative Commons Attribution 3.0 Unported (CC BY 3.0)](https://creativecommons.org/licenses/by/3.0/)

Die 5 000 häufigsten Wortpaare je Sprache aus dem [Google Books Ngram Corpus](https://books.google.com/ngrams)
(Bücher von 2010 bis 2019), aufbereitet von [orgtre/google-books-ngram-frequency](https://github.com/orgtre/google-books-ngram-frequency).
Änderung: Paare mit Satzzeichen oder Ziffern entfernt, in das Format `erstes<TAB>zweites<TAB>häufigkeit` gebracht.

## Assets/dict/fr.*, es.*, it.*

Französisch, Spanisch und Italienisch aus denselben Quellen wie oben: Wortlisten aus FrequencyWords (CC BY-SA 4.0),
Wortpaare aus dem Google Books Ngram Corpus via orgtre/google-books-ngram-frequency (CC BY 3.0).
Änderung: auf 80 000 Wörter gekürzt, Tippfehler gefiltert.

## Assets/emoji/

| Datei | Quelle | Lizenz |
| --- | --- | --- |
| `emoji.txt` | [unicode-emoji-json](https://github.com/muan/unicode-emoji-json) von Mu-An Chiou (Emoji, Kategorien, Hauttöne) | MIT |
| `keywords.*.txt` | [Unicode CLDR](https://github.com/unicode-org/cldr) annotations (Namen und Suchbegriffe) | [Unicode License v3](https://www.unicode.org/license.txt), © Unicode, Inc. |

## Resources/drawable/ms_*.xml

Symbole aus [Material Symbols](https://github.com/google/material-design-icons) von Google, © Google LLC,
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Änderung: von SVG in Android-VectorDrawables umgewandelt
(`tools/build_icons.py`).

## Nur beim Erzeugen benutzt, nicht enthalten

| Quelle | Verwendung | Lizenz |
| --- | --- | --- |
| [igerman98](https://www.j3e.de/ispell/igerman98/) (de_DE Hunspell, über [wooorm/dictionaries](https://github.com/wooorm/dictionaries)) | Prüfliste, welche Wörter auch kleingeschrieben vorkommen | GPLv2 / GPLv3 |

igerman98 wird nicht mit zBoard ausgeliefert; es dient beim Erzeugen nur als Ja/Nein-Prüfung für die Großschreibung.
