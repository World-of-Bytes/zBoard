# Schnittstelle für zHeal Meridian

zBoard stellt die Tipp-Metadaten über einen Android-`ContentProvider` bereit. Getippte Inhalte sind nie enthalten:
nur Zeitpunkte, Druckdauer, Abstände und die Art der Taste.

## Voraussetzungen

- In zBoard ist **Einstellungen → Statistik für zHeal Meridian → Tipp-Statistik erfassen** eingeschaltet
  (Standard: aus). Ist sie aus, liefert die Schnittstelle leere Ergebnisse.
- zHeal Meridian ist mit **demselben Schlüssel signiert** wie zBoard. Die Leseberechtigung hat die Schutzstufe
  `signature`; andere Apps erhalten keinen Zugriff.
- zHeal Meridian deklariert im Manifest:

```xml
<uses-permission android:name="de.zeriadtke.zboard.permission.READ_TYPING_STATS" />
<queries>
  <package android:name="de.zeriadtke.zboard" />
</queries>
```

## Abfragen

| URI | Inhalt |
| --- | --- |
| `content://de.zeriadtke.zboard.stats/keystrokes?since=<unix-ms>` | einzelne Anschläge ab dem Zeitpunkt, älteste zuerst |
| `content://de.zeriadtke.zboard.stats/sessions?since=<unix-ms>` | Sitzungen, die ab dem Zeitpunkt endeten |

Ohne `since` kommen alle gespeicherten Daten (Aufbewahrung 30 Tage).

### Spalten `keystrokes`

| Spalte | Typ | Bedeutung |
| --- | --- | --- |
| `timestamp_ms` | long | Loslassen der Taste, Unix-ms (UTC) |
| `category` | string | `Letter`, `Digit`, `Punctuation`, `Space`, `Backspace`, `Enter`, `Shift`, `Suggestion`, `Gesture`, `Other` |
| `hold_ms` | int | Druckdauer |
| `flight_ms` | int oder null | Loslassen der vorherigen bis Drücken dieser Taste; null nach mehr als 5 s Pause |
| `session_id` | string (GUID) | Eingabesitzung (Tastatur geöffnet bis geschlossen) |

### Spalten `sessions`

| Spalte | Typ | Bedeutung |
| --- | --- | --- |
| `session_id` | string (GUID) | Sitzung |
| `start_ms`, `end_ms` | long | Beginn und Ende, Unix-ms (UTC) |
| `keystrokes` | int | Anschläge insgesamt |
| `chars_per_minute` | double | Text-Anschläge je Minute aktiver Tippzeit |
| `correction_rate` | double | Anteil Löschen an allen Anschlägen (0–1) |
| `pauses_over_2s` | int | Pausen über 2 s |
| `suggestions_accepted` | int | angetippte Vorschläge |
| `autocorrections` | int | automatische Korrekturen |

## Beispiel (C#, .NET for Android)

```csharp
var uri = Android.Net.Uri.Parse("content://de.zeriadtke.zboard.stats/sessions?since=" + sinceMs);
using var cursor = ContentResolver!.Query(uri, null, null, null, null);
while (cursor != null && cursor.MoveToNext())
{
    var cpm = cursor.GetDouble(cursor.GetColumnIndexOrThrow("chars_per_minute"));
    var corrections = cursor.GetDouble(cursor.GetColumnIndexOrThrow("correction_rate"));
}
```

## Aktualität

zBoard schreibt Anschläge gepuffert (alle 64 Anschläge) und am Ende jeder Sitzung. Neue Daten sind also spätestens
nach dem Schließen der Tastatur abrufbar.

## Datenschutz

- Keine Inhalte, keine Tastennamen, keine App-Namen.
- Nichts in Passwortfeldern oder Feldern mit `IME_FLAG_NO_PERSONALIZED_LEARNING`.
- Die Daten bleiben auf dem Gerät; zBoard hat keine Internetberechtigung.
