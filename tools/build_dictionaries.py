#!/usr/bin/env python3
"""
Erzeugt die zBoard-Wörterbücher Assets/dict/de.txt und Assets/dict/en.txt.

Quellen (per git holen, siehe README "Wörterbücher neu erzeugen"):
  --freq-de / --freq-en  FrequencyWords (OpenSubtitles 2018), CC BY-SA 4.0
                         https://github.com/hermitdave/FrequencyWords
  --nouns                german-nouns (Wiktionary), CC BY-SA 4.0
                         https://github.com/gambolputty/german-nouns
  --hunspell-de          igerman98 / de_DE Hunspell, GPLv2/GPLv3 (nur als Prüfliste, wird nicht ausgeliefert)
                         https://github.com/wooorm/dictionaries

  --bigrams-de / --bigrams-en  (optional) Google Books Ngram, häufigste Wortpaare 2010–2019, CC BY 3.0
                         https://github.com/orgtre/google-books-ngram-frequency (ngrams/2grams_*.csv)

Die Häufigkeitslisten sind komplett kleingeschrieben. Für Deutsch wird die Großschreibung so bestimmt:
  * Form kommt in Wiktionary als Substantiv vor UND nicht als kleingeschriebenes Wort in Hunspell
    -> nur großgeschrieben ("Haus"); zBoard schreibt "haus" dann automatisch groß.
  * Form kommt in beiden vor ("frage"/"Frage", "essen"/"Essen")
    -> beide Schreibweisen; zBoard lässt das Getippte stehen und bietet die andere an.
  * sonst kleingeschrieben.
Dazu kommen kurze Handlisten für Fälle, die die Regeln nicht erkennen (unregelmäßige Verbformen u. Ä.).

Format der Ausgabe: eine Schreibweise pro Zeile, "wort<TAB>häufigkeit".
"""
import argparse
import csv
import re

WORD = re.compile(r"^[^\W\d_][\w'\-]*$", re.UNICODE)

# Wörter, die die Regeln fälschlich als reine Substantive einstufen (Funktionswörter, unregelmäßige Verbformen)
DE_FORCE_LOWER = set("""
wenn hast nichts muss will etwas hab soll halt hatten würden letzten letzter letztes letzte schau tu werd
rum fürs wegen kamen weh scheiß hau dreh tritt steig fang is to on no my pa aye don van
ich sie ist du die es er ja mir wie aber für von nein habe gut als sein jetzt aus im weiß oder man werden mehr
bei bitte los danke tun einer werde viel weg würde am des weil heute wollen macht hallo seine zwei waren ab wohl
ob selbst gute lange ach denke drei warte bleiben unter rein ganze gegen niemand brauche halten all eins ganzen
suchen lang gewesen versuchen anders fünf hoch vier ersten kleinen allen falls stellen dies ehrlich alt frei
besten alten früh großen zehn sechs lieben leider tat suche groß versuche rufen heiraten super total halte laut
sitzen böse heißen hart tragen stolz schöne schätze anrufen hasse führen schauen perfekt acht rufe bitten hole
liegen gewinnen fort schönen klein süß tolle aller sucht heiße beweisen tief sowieso normal süße sauer näher
jung links reich schade bleibe toten manche gegenüber gehören stecken rechts entscheiden miteinander zweite
verstecken besuchen teilen feiern gelassen gefahren gebeten nahe beginnen neun melden übel leisten drehen
schließen mitten neuer hängen armen schätzen ehren reichen schreien spreche leer weißen weinen nutzen wünschen
zweiten gehalten prima stören küssen schwarzen halbe steigen junger geschossen schwarz fick rot nette langer
scheinen bösen schaffe lauter enden raten wachen verboten dunkel schick trage schicke trotz voraus streiten
schlage matt englisch roten zwölf schrieb abends heiligen leichter halben packen drücken schreibe fremden landen
extra perfekte trug verbunden prost wild träumen hauen dritten gucken anstatt langen rechten harte schwere feuern
blau gemein esse wechseln melde null schlafe starten zwingen tausend gründen frische verdächtigen menschliche
greifen per lecker irren fange real wachsen grün tollen fett achten kurze führe schenken albern starke freien
schneiden besondere rosa französisch kaufe reine linken rund leiten extrem kurzem negativ wenden plus lese laufe
daheim klingen hübscher angeboten ex nass menschlichen störe filmen besitzen süßes wilde heben tauschen senden
schalten spanisch live leihen scheiden positiv nähern
""".split())

# Mehrdeutig, aber das Substantiv dominiert so stark, dass nur die Großschreibung aufgenommen wird
DE_FORCE_NOUN = set("""
zeit angst freund ende ruhe tage bett schule teil menge plan licht erde nähe meister ärger gedanken partner
klappe rolle reise weile kraft ehre liste rücken wunder wette sonne richter soldaten schritt band feind wand
angriff ecke pause fahrt zustand geschichten reihe dienst knie grenze braut mühe hexe aussage regen fahrer
flucht song schüler präsidenten filme fälle schnauze decke gründe strafe agenten minister quelle schiffe hütte
stärke bürger klang punkte tränen schreie schnitt kaiser künstler anklage uniform zeuge verbrecher führer
verdacht probe ritter viertel miete wüste planet wissenschaftler morde dusche mitarbeiter ratte verdienst wunde
fische berge zentrale jäger indianer stahl aussagen söhne anzeige eile diamanten schütze kette terroristen
arbeiter widerstand haken berichte reporter hexen legende besucher schwäche flotte hinweise botschafter köpfe
knarre personal sinne ratten wunden beute orten kugeln dutzend lehrer patienten hure fresse arsch abend hause
""".split())

# Wörter, die als Substantiv UND klein häufig sind, von den Regeln aber nicht erkannt werden
DE_FORCE_BOTH = set("""
vertrauen verschwinden versprechen vermögen lauf ruf brauch mal morgen leben essen wissen
""".split())

# Anteil der Häufigkeit für die zweite Schreibweise bei mehrdeutigen Wörtern
SECOND_FORM_SHARE = 0.6

EN_CAPITALIZED = {"i": "I", "i'm": "I'm", "i'll": "I'll", "i've": "I've", "i'd": "I'd"}


def load_frequencies(path, min_freq):
    entries = []
    with open(path, encoding="utf-8") as src:
        for line in src:
            parts = line.split()
            if len(parts) < 2 or not parts[-1].isdigit():
                continue
            word, freq = parts[0], int(parts[-1])
            if freq < min_freq or not WORD.match(word) or len(word) > 40:
                continue
            entries.append((word, freq))
    entries.sort(key=lambda e: e[1], reverse=True)
    return entries


def is_nominalization(row):
    """Substantiviertes Adjektiv ("das Gute") oder substantivierter Infinitiv ("das Haben")."""
    lemma = row["lemma"]
    if "adjektivische Deklination" in row["pos"].split(","):
        return True
    return row["genus"] == "n" and re.search(r"(en|ern|eln)$", lemma) and row["nominativ plural"] in ("", lemma)


def noun_forms(path, skip_nominalizations=False):
    """Kleingeschriebene Form -> großgeschriebene Substantivform (alle Kasus, Singular und Plural)."""
    forms = {}
    with open(path, encoding="utf-8") as src:
        for row in csv.DictReader(src):
            if "Wortverbindung" in row["pos"].split(","):
                continue
            lemma = row["lemma"]
            if not lemma or not lemma[0].isupper() or " " in lemma:
                continue
            if skip_nominalizations and is_nominalization(row):
                continue
            values = [lemma] + [
                v for k, v in row.items()
                if k not in ("lemma", "pos") and v and k.split()[0] in ("nominativ", "genitiv", "dativ", "akkusativ")
            ]
            for v in values:
                if v[0].isupper() and " " not in v:
                    forms.setdefault(v.lower(), v)
    return forms


ADJ_SUFFIXES = ["e", "er", "es", "en", "em", "ere", "eren", "erer", "eres", "erem", "ste", "sten", "ster", "stes", "stem"]
VERB_SUFFIXES = ["e", "st", "t", "et", "est", "te", "test", "ten", "tet", "end", "ende", "enden", "ender", "endes"]


def lowercase_forms(path):
    """Alle Formen, die im Hunspell-Wörterbuch kleingeschrieben vorkommen (grob flektiert)."""
    out = set()
    with open(path, encoding="utf-8") as src:
        for line in src:
            parts = line.strip().split("/", 1)
            stem, flags = parts[0], (parts[1] if len(parts) > 1 else "")
            if not stem or not stem[0].islower() or not stem.isalpha():
                continue
            if "z" in flags or flags == "hke":  # Kompositions-Bestandteile, keine eigenständigen Wörter
                continue
            out.add(stem)
            if "A" in flags:  # Adjektiv
                out.update(stem + s for s in ADJ_SUFFIXES)
            if any(f in flags for f in "DIY") and stem.endswith("n"):  # Verb im Infinitiv
                if stem.endswith(("ern", "eln")):
                    root = stem[:-1]
                elif stem.endswith("en"):
                    root = stem[:-2]
                else:
                    root = stem[:-1]
                out.update(root + s for s in VERB_SUFFIXES)
                out.add("ge" + root + "t")
                out.add("ge" + root + "en")
    return out


def build_german(args):
    nouns = noun_forms(args.nouns)
    real_nouns = noun_forms(args.nouns, skip_nominalizations=True)
    lower = lowercase_forms(args.hunspell_de)
    lines = []
    count = 0
    for word, freq in load_frequencies(args.freq_de, args.min_freq):
        if len(word) < 2:
            continue
        noun = nouns.get(word)
        if noun is None or word in DE_FORCE_LOWER:
            lines.append((word, freq))
        elif word in DE_FORCE_NOUN:
            lines.append((noun, freq))
        elif word in DE_FORCE_BOTH or (word in lower and word in real_nouns):
            lines.append((word, freq))
            lines.append((noun, max(1, int(freq * SECOND_FORM_SHARE))))
        elif word in lower:
            lines.append((word, freq))  # nur Nominalisierung ("haben"/"das Haben"): klein lassen
        else:
            lines.append((noun, freq))
        count += 1
        if count >= args.limit_de:
            break
    return lines


def build_english(args):
    lines = []
    for word, freq in load_frequencies(args.freq_en, args.min_freq):
        if len(word) < 2 and word not in ("a", "i"):
            continue
        lines.append((EN_CAPITALIZED.get(word, word), freq))
        if len(lines) >= args.limit_en:
            break
    return lines


def build_bigrams(path):
    """CSV "ngram,freq" -> [(erstes Wort, zweites Wort, Häufigkeit)], nur echte Wortpaare."""
    out = []
    with open(path, encoding="utf-8") as src:
        next(src, None)  # Kopfzeile
        for line in src:
            ngram, _, freq = line.rstrip("\n").rpartition(",")
            parts = ngram.split(" ")
            if len(parts) != 2 or not freq.isdigit():
                continue
            if all(WORD.match(p) for p in parts):
                out.append((parts[0], parts[1], int(freq)))
    return out


def write_bigrams(path, rows, source_note):
    with open(path, "w", encoding="utf-8") as dst:
        dst.write(f"# {source_note}\n")
        for first, second, freq in rows:
            dst.write(f"{first}\t{second}\t{freq}\n")
    print(f"{path}: {len(rows)} Wortpaare")


TYPO_KEEP = {"beriet", "gnu"}


def drop_transposition_typos(lines, skip_capitalized):
    """
    Entfernt Tippfehler aus den Untertiteln ("nciht", "dsa", "waht"): seltene Wörter (< 100), die durch Tauschen
    zweier Nachbarbuchstaben zu einem mindestens 1000-mal häufigeren Wort werden.
    """
    freq, capitalized = {}, {}
    for word, f in lines:
        key = word.lower()
        freq[key] = max(freq.get(key, 0), f)
        capitalized[key] = capitalized.get(key, False) or word[0].isupper()

    typos = set()
    for key, f in freq.items():
        if f >= 100 or len(key) < 3 or key in TYPO_KEEP or (skip_capitalized and capitalized[key]):
            continue
        for i in range(len(key) - 1):
            if key[i] == key[i + 1]:
                continue
            swapped = key[:i] + key[i + 1] + key[i] + key[i + 2:]
            if freq.get(swapped, 0) >= 1000 * f:
                typos.add(key)
                break

    print(f"  {len(typos)} Tippfehler entfernt")
    return [(w, f) for w, f in lines if w.lower() not in typos]


def write(path, lines, source_note):
    with open(path, "w", encoding="utf-8") as dst:
        dst.write(f"# {source_note}\n")
        for word, freq in lines:
            dst.write(f"{word}\t{freq}\n")
    print(f"{path}: {len(lines)} Zeilen")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--freq-de", required=True)
    p.add_argument("--freq-en", required=True)
    p.add_argument("--nouns", required=True)
    p.add_argument("--hunspell-de", required=True)
    p.add_argument("--bigrams-de")
    p.add_argument("--bigrams-en")
    p.add_argument("--out-dir", default="Assets/dict")
    p.add_argument("--limit-de", type=int, default=120_000)
    p.add_argument("--limit-en", type=int, default=100_000)
    p.add_argument("--min-freq", type=int, default=3)
    args = p.parse_args()

    write(f"{args.out_dir}/de.txt", drop_transposition_typos(build_german(args), skip_capitalized=True),
          "Quellen: FrequencyWords (OpenSubtitles 2018, CC BY-SA 4.0), Großschreibung nach german-nouns/Wiktionary (CC BY-SA 4.0)")
    write(f"{args.out_dir}/en.txt", drop_transposition_typos(build_english(args), skip_capitalized=False),
          "Quelle: FrequencyWords (OpenSubtitles 2018, CC BY-SA 4.0)")
    note = "Quelle: Google Books Ngram Corpus 2010-2019 via orgtre/google-books-ngram-frequency (CC BY 3.0)"
    if args.bigrams_de:
        write_bigrams(f"{args.out_dir}/de.bigrams.txt", build_bigrams(args.bigrams_de), note)
    if args.bigrams_en:
        write_bigrams(f"{args.out_dir}/en.bigrams.txt", build_bigrams(args.bigrams_en), note)


if __name__ == "__main__":
    main()
