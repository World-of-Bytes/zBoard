#!/usr/bin/env python3
"""
Erzeugt das zBoard-Logo in den Farben von World of Bytes (world-of-bytes.de).

    python3 tools/build_logo.py

Ausgabe:
  docs/logo/zboard-logo.svg                      – Logo als SVG (README, Web)
  Resources/drawable/ic_launcher_background.xml  – adaptives App-Symbol, Hintergrund
  Resources/drawable/ic_launcher_foreground.xml  – adaptives App-Symbol, Vordergrund (Farbverlauf)
  Resources/drawable/ic_launcher_monochrome.xml  – einfarbige Ebene für Android-13-Designsymbole
  Resources/mipmap-anydpi-v26/ic_launcher(.xml|_round.xml)

Motiv: eine Tastenkappe, darauf ein „z“ aus Punkten (wie die Punkt-Weltkugel von World of Bytes),
mit dem Markenverlauf Limette → Grün → Cyan → Blau. Darunter vier kleine Tasten und eine Leertaste.
"""
import os

# Farben von world-of-bytes.de (CSS-Variablen)
BG = "#07090D"
SURFACE = "#0E131B"
LINE = "#1E2733"
GRADIENT = [(0.0, "#B1C740"), (0.35, "#6BA942"), (0.70, "#3FA9D9"), (1.0, "#387ABE")]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SIZE = 108  # Kantenlänge eines adaptiven Symbols in dp; sichtbare Sicherheitszone: Kreis mit 66 dp

# ---------- Geometrie ----------

CAP_X, CAP_Y, CAP_W, CAP_H, CAP_R = 33, 26, 42, 38, 8   # Tastenkappe
DEPTH = 3                                            # sichtbare Kante unter der Kappe

# „z“ als Punktraster (7 Spalten, 8 Zeilen)
GRID = [
    "#######",
    "#######",
    "....##.",
    "...##..",
    "..##...",
    ".##....",
    "#######",
    "#######",
]
PITCH = 3.7
DOT_R = 1.4


def z_dots():
    rows, cols = len(GRID), len(GRID[0])
    x0 = CAP_X + CAP_W / 2 - (cols - 1) * PITCH / 2
    y0 = CAP_Y + CAP_H / 2 - (rows - 1) * PITCH / 2
    return [(x0 + c * PITCH, y0 + r * PITCH) for r, line in enumerate(GRID) for c, ch in enumerate(line) if ch == "#"]


# kleine Tasten und Leertaste unter der Kappe
MINI_Y, MINI_H = CAP_Y + CAP_H + DEPTH + 4, 4.5
MINI = [(33, 9), (44, 9), (55, 9), (66, 9)]     # (x, Breite)
SPACE = (41, MINI_Y + MINI_H + 2.5, 26, 4)      # x, y, Breite, Höhe


def circle_path(cx, cy, r):
    return (f"M{cx - r:.2f},{cy:.2f}a{r},{r} 0 1,0 {2 * r},0a{r},{r} 0 1,0 {-2 * r},0z")


def rrect_path(x, y, w, h, r):
    return (f"M{x + r},{y}h{w - 2 * r}a{r},{r} 0 0,1 {r},{r}v{h - 2 * r}a{r},{r} 0 0,1 {-r},{r}"
            f"h{-(w - 2 * r)}a{r},{r} 0 0,1 {-r},{-r}v{-(h - 2 * r)}a{r},{r} 0 0,1 {r},{-r}z")


DOTS = "".join(circle_path(x, y, DOT_R) for x, y in z_dots())
CAP = rrect_path(CAP_X, CAP_Y, CAP_W, CAP_H, CAP_R)
CAP_EDGE = rrect_path(CAP_X, CAP_Y + DEPTH, CAP_W, CAP_H, CAP_R)
MINIS = "".join(rrect_path(x, MINI_Y, w, MINI_H, 1.6) for x, w in MINI)
SPACEBAR = rrect_path(*SPACE, 1.8)

# Verlauf diagonal über das Motiv (links unten → rechts oben, wie der Schriftzug auf der Website)
G_START, G_END = (CAP_X, SPACE[1] + SPACE[3]), (CAP_X + CAP_W, CAP_Y)


# ---------- SVG ----------

def svg(px=512):
    stops = "".join(f'<stop offset="{o}" stop-color="{c}"/>' for o, c in GRADIENT)
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{px}" height="{px}" viewBox="18 18 72 72">
  <title>zBoard</title>
  <defs>
    <linearGradient id="wob" gradientUnits="userSpaceOnUse" x1="{G_START[0]}" y1="{G_START[1]}" x2="{G_END[0]}" y2="{G_END[1]}">{stops}</linearGradient>
    <radialGradient id="glow" cx="78" cy="22" r="60" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#3FA9D9" stop-opacity="0.22"/><stop offset="1" stop-color="#3FA9D9" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect x="18" y="18" width="72" height="72" rx="16" fill="{BG}"/>
  <rect x="18" y="18" width="72" height="72" rx="16" fill="url(#glow)"/>
  <path d="{CAP_EDGE}" fill="{LINE}"/>
  <path d="{CAP}" fill="{SURFACE}" stroke="url(#wob)" stroke-width="1.6"/>
  <path d="{DOTS}" fill="url(#wob)"/>
  <path d="{MINIS}{SPACEBAR}" fill="url(#wob)" fill-opacity="0.55"/>
</svg>
'''


# ---------- Android VectorDrawables ----------

def gradient_xml(indent):
    pad = " " * indent
    items = "\n".join(f'{pad}    <item android:offset="{o}" android:color="{c}"/>' for o, c in GRADIENT)
    return (f'{pad}<aapt:attr name="android:{{attr}}">\n'
            f'{pad}  <gradient android:type="linear" android:startX="{G_START[0]}" android:startY="{G_START[1]}"'
            f' android:endX="{G_END[0]}" android:endY="{G_END[1]}">\n{items}\n{pad}  </gradient>\n{pad}</aapt:attr>')


def vector(body, aapt=False):
    ns = ' xmlns:aapt="http://schemas.android.com/aapt"' if aapt else ""
    return f'''<?xml version="1.0" encoding="utf-8"?>
<!-- Erzeugt von tools/build_logo.py – nicht von Hand ändern. -->
<vector xmlns:android="http://schemas.android.com/apk/res/android"{ns}
    android:width="{SIZE}dp" android:height="{SIZE}dp"
    android:viewportWidth="{SIZE}" android:viewportHeight="{SIZE}">
{body}
</vector>
'''


def foreground():
    g = gradient_xml(4)
    return vector(f'''  <path android:fillColor="{LINE}" android:pathData="{CAP_EDGE}"/>
  <path android:fillColor="{SURFACE}" android:strokeWidth="1.6" android:pathData="{CAP}">
{g.replace("{attr}", "strokeColor")}
  </path>
  <path android:pathData="{DOTS}">
{g.replace("{attr}", "fillColor")}
  </path>
  <path android:fillAlpha="0.55" android:pathData="{MINIS}{SPACEBAR}">
{g.replace("{attr}", "fillColor")}
  </path>''', aapt=True)


def background():
    return vector(f'''  <path android:fillColor="{BG}" android:pathData="M0,0h{SIZE}v{SIZE}h-{SIZE}z"/>
  <path android:pathData="M0,0h{SIZE}v{SIZE}h-{SIZE}z">
    <aapt:attr name="android:fillColor">
      <gradient android:type="radial" android:centerX="80" android:centerY="20" android:gradientRadius="70">
        <item android:offset="0" android:color="#383FA9D9"/>
        <item android:offset="1" android:color="#003FA9D9"/>
      </gradient>
    </aapt:attr>
  </path>''', aapt=True)


def monochrome():
    return vector(f'''  <path android:fillColor="#FFFFFFFF" android:fillAlpha="0.35" android:pathData="{CAP}"/>
  <path android:fillColor="#FFFFFFFF" android:pathData="{DOTS}"/>
  <path android:fillColor="#FFFFFFFF" android:fillAlpha="0.6" android:pathData="{MINIS}{SPACEBAR}"/>''')


ADAPTIVE = '''<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
  <background android:drawable="@drawable/ic_launcher_background"/>
  <foreground android:drawable="@drawable/ic_launcher_foreground"/>
  <monochrome android:drawable="@drawable/ic_launcher_monochrome"/>
</adaptive-icon>
'''


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print(rel)


if __name__ == "__main__":
    write("docs/logo/zboard-logo.svg", svg())
    write("Resources/drawable/ic_launcher_foreground.xml", foreground())
    write("Resources/drawable/ic_launcher_background.xml", background())
    write("Resources/drawable/ic_launcher_monochrome.xml", monochrome())
    write("Resources/mipmap-anydpi-v26/ic_launcher.xml", ADAPTIVE)
    write("Resources/mipmap-anydpi-v26/ic_launcher_round.xml", ADAPTIVE)
