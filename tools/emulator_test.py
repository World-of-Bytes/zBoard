#!/usr/bin/env python3
"""
Emulator-Test für zBoard (läuft in GitHub Actions, geht aber auch lokal mit laufendem Emulator).

    python3 tools/emulator_test.py <pfad-zur-debug.apk> <ausgabeordner>

Installiert die Debug-APK, macht zBoard zur aktiven Tastatur, öffnet das Testfeld (TypingTestActivity) und
tippt echte Fingertipps und eine Wischgeste. Geprüft wird der Text im Eingabefeld. Die Tastenpositionen schreibt
die Debug-Version von zBoard ins Log (Tag "zBoardTest"). Screenshots, Log und eine Zusammenfassung landen im
Ausgabeordner; in GitHub Actions erscheint die Zusammenfassung zusätzlich auf der Seite des Laufs.
"""
import os
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

PACKAGE = "de.zeriadtke.zboard"
IME = f"{PACKAGE}/.ZBoardImeService"
TEST_ACTIVITY = f"{PACKAGE}/{PACKAGE}.TypingTestActivity"


def adb(*args, check=True, timeout=120):
    result = subprocess.run(["adb", *args], capture_output=True, text=True, timeout=timeout)
    if check and result.returncode != 0:
        raise RuntimeError(f"adb {' '.join(args)} fehlgeschlagen: {result.stderr.strip()}")
    return result.stdout


def shell(command, check=True):
    return adb("shell", command, check=check)


class Keyboard:
    """Tastenpositionen aus dem Log der Debug-Version."""

    def __init__(self):
        self.layouts = {}

    def refresh(self):
        log = adb("logcat", "-d", "-s", "zBoardTest:I", check=False)
        for line in log.splitlines():
            match = re.search(r"KEYS (\S+) (.*)$", line)
            if not match:
                continue
            keys = {}
            for item in match.group(2).split():
                label, _, pos = item.rpartition("@")
                x, y = pos.split(",")
                keys[label] = (int(x), int(y))
            self.layouts[match.group(1)] = keys  # spätere Zeilen überschreiben frühere

    def wait_for(self, layout, timeout=150):
        """Wartet, bis das Layout gemeldet ist und seine Position 2 s lang unverändert bleibt."""
        end = time.time() + timeout
        last, stable_since = None, None
        while time.time() < end:
            self.refresh()
            current = self.layouts.get(layout)
            if current is not None:
                if current != last:
                    last, stable_since = current, time.time()
                elif time.time() - stable_since >= 2:
                    return current
            time.sleep(0.5)
        print("--- Diagnose ---")
        print(shell("dumpsys input_method | grep -E 'mInputShown|mCurMethodId|mCurId|mShowRequested|mVisibleBound|isShown'", check=False))
        print(shell("dumpsys activity activities | grep -E 'mResumedActivity|topResumedActivity'", check=False))
        print("Absturz-Log:\n" + adb("logcat", "-d", "-b", "crash", check=False)[-4000:])
        log = adb("logcat", "-d", check=False).splitlines()
        noise = r"InputMethodEntryManager|GoogleInputMethodService|LatinCommon|SystemSubtypes|DictionarySuperpacks|ApkAssets"
        relevant = [l for l in log
                    if re.search(r"zboard|ZBoard|monodroid|AndroidRuntime|mono|DOTNET|InputMethodManagerService|ImeTracker|FATAL", l)
                    and not re.search(noise, l)]
        print("Log (Auszug):\n" + "\n".join(relevant[-120:]))
        raise RuntimeError(f"Tastatur mit Layout '{layout}' erscheint nicht (keine Tastenpositionen im Log)")


def wait_for_ime(timeout=60):
    """Android registriert eine frisch installierte Tastatur mit Verzögerung."""
    end = time.time() + timeout
    while time.time() < end:
        if IME in shell("ime list -a -s", check=False):
            return
        time.sleep(2)
    # Diagnose fürs Log
    print("Installierte Tastaturen:\n" + shell("ime list -a -s", check=False))
    print("Paket:\n" + shell(f"pm list packages {PACKAGE}", check=False))
    print("Dienste im Paket:\n" + shell(f"dumpsys package {PACKAGE} | grep -i -A4 'Service'", check=False)[:3000])
    raise RuntimeError(f"{IME} taucht nicht in der Liste der Tastaturen auf")


KEYBOARD = Keyboard()


def tap(pos):
    shell(f"input tap {pos[0]} {pos[1]}")
    time.sleep(0.25)


def type_text(keys, text):
    keys = KEYBOARD.wait_for("de")
    for ch in text:
        if ch == " ":
            tap(keys["Space"])
        else:
            tap(keys[ch.lower()])


def glide(keys, word, steps=4):
    """Wischgeste über die Buchstaben als eine Folge von Touch-Ereignissen."""
    keys = KEYBOARD.wait_for("de")
    points = [keys[c] for c in word]
    commands = [f"input motionevent DOWN {points[0][0]} {points[0][1]}"]
    for (x1, y1), (x2, y2) in zip(points, points[1:]):
        for i in range(1, steps + 1):
            x = x1 + (x2 - x1) * i // steps
            y = y1 + (y2 - y1) * i // steps
            commands.append(f"input motionevent MOVE {x} {y}")
    commands.append(f"input motionevent UP {points[-1][0]} {points[-1][1]}")
    shell("; ".join(commands))
    time.sleep(0.8)


def field_text():
    shell("uiautomator dump /sdcard/zboard-ui.xml")
    xml = adb("exec-out", "cat", "/sdcard/zboard-ui.xml")
    for node in ET.fromstring(xml).iter("node"):
        if node.get("class") == "android.widget.EditText":
            return node.get("text", "")
    raise RuntimeError("Eingabefeld nicht gefunden")


def screenshot(out_dir, name):
    path = os.path.join(out_dir, f"{name}.png")
    with open(path, "wb") as f:
        f.write(subprocess.run(["adb", "exec-out", "screencap", "-p"], capture_output=True, timeout=60).stdout)
    return path


def crashes():
    log = adb("logcat", "-d", "-b", "crash", check=False)
    return [line for line in log.splitlines() if PACKAGE in line or "FATAL" in line]


def main():
    apk, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    results = []

    def check(name, condition, detail=""):
        results.append((name, bool(condition), detail))
        print(f"{'OK  ' if condition else 'FEHL'} {name} {detail}")

    try:
        print(adb("install", "-r", apk, timeout=300).strip())
        wait_for_ime()
        shell(f"ime enable {IME}")
        shell(f"ime set {IME}")
        check("zBoard als Tastatur aktiv", IME.split("/")[0] in shell("settings get secure default_input_method"))

        adb("logcat", "-c", check=False)
        shell(f"am start -W -n {TEST_ACTIVITY}")
        kb = KEYBOARD
        try:
            keys = kb.wait_for("de", timeout=60)
        except RuntimeError:
            # Android hält manchmal noch die vorherige Tastatur gebunden: zBoard erneut setzen, Testfeld neu öffnen
            print("Tastatur nicht erschienen, zweiter Versuch")
            shell(f"ime set {IME}")
            shell(f"am start -W -S -n {TEST_ACTIVITY}")
            keys = kb.wait_for("de", timeout=120)
        time.sleep(4)  # Wörterbuch lädt im Hintergrund
        screenshot(out_dir, "1-tastatur")
        check("Tastatur erscheint", True, f"({len(keys)} Tasten)")
        check("Keine Sprachtaste (Wechsel per langem Druck auf die Leertaste)", "Language" not in keys)
        check("Deutsch wie Gboard: keine eigenen Umlaut-Tasten", not any(k in keys for k in ("ü", "ö", "ä")))

        bounds = re.findall(r"BOUNDS de bottom=(\d+) limit=(\d+) nav=(\d+)", adb("logcat", "-d", "-s", "zBoardTest:I", check=False))
        if bounds:
            bottom, limit, nav = map(int, bounds[-1])
            check("Tastatur über der Navigationsleiste", bottom <= limit,
                  f"(Unterkante {bottom}, erlaubt bis {limit}, Leiste {nav} px)")
        else:
            check("Tastatur über der Navigationsleiste", False, "(keine Lageangabe im Log)")

        type_text(keys, "hallo ")
        text = field_text()
        check("Tippen + Großschreibung am Satzanfang", text == "Hallo ", f"→ „{text}“")

        type_text(keys, "dsa ")
        text = field_text()
        check("Autokorrektur (dsa → das)", text.endswith("das "), f"→ „{text}“")

        type_text(keys, "haus ")
        text = field_text()
        check("Substantiv großschreiben (haus → Haus)", text.endswith("Haus "), f"→ „{text}“")

        glide(keys, "gut")
        tap(keys["Space"])
        text = field_text()
        check("Wischeingabe (g-u-t)", text.endswith("gut "), f"→ „{text}“")
        screenshot(out_dir, "2-getippt")

        tap(kb.wait_for("de")["Delete"])
        time.sleep(0.5)
        text = field_text()
        check("Löschen", text.endswith("gut"), f"→ „{text}“")

        tap(kb.wait_for("de")["ToSymbols"])
        symbols = kb.wait_for("symbols1", timeout=15)
        screenshot(out_dir, "3-symbole")
        check("Symbolebene", "@" in symbols)
        tap(symbols["ToAlpha"])
        time.sleep(0.5)

        tap(kb.wait_for("de")["Emoji"])
        time.sleep(2)
        screenshot(out_dir, "4-emoji")
        shell("input keyevent KEYCODE_BACK", check=False)
        time.sleep(1)

        found = crashes()
        check("Keine Abstürze", not found, "; ".join(found[:3]))
    except Exception as e:  # noqa: BLE001 – jeder Fehler ist ein Testfehler
        check("Testablauf", False, str(e))
        try:
            screenshot(out_dir, "fehler")
        except Exception:  # noqa: BLE001
            pass
    finally:
        with open(os.path.join(out_dir, "logcat.txt"), "w", encoding="utf-8") as f:
            f.write(adb("logcat", "-d", check=False))
        write_summary(out_dir, results)

    sys.exit(0 if results and all(ok for _, ok, _ in results) else 1)


def write_summary(out_dir, results):
    lines = ["## zBoard im Emulator", "", "| Prüfung | Ergebnis | Detail |", "| --- | --- | --- |"]
    for name, ok, detail in results:
        lines.append(f"| {name} | {'✅' if ok else '❌'} | {detail.replace('|', '/')} |")
    lines += ["", "Screenshots und Log: Artefakt **emulator-test**."]
    summary = "\n".join(lines) + "\n"
    with open(os.path.join(out_dir, "zusammenfassung.md"), "w", encoding="utf-8") as f:
        f.write(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
            f.write(summary)


if __name__ == "__main__":
    main()
