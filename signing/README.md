# Signaturschlüssel für Testversionen

`zboard-test.keystore` signiert alle Debug- und Testversionen (lokal und in GitHub Actions), damit sich neue
Testversionen über ältere installieren lassen. Android akzeptiert ein Update nur mit demselben Schlüssel.

| | |
| --- | --- |
| Alias | `zboard-test` |
| Passwort (Keystore und Schlüssel) | `zboard-test` |

Der Schlüssel ist absichtlich öffentlich und **nur für Testversionen** gedacht. Veröffentlichte Versionen
(Play Store, F-Droid, GitHub-Releases) bekommen einen eigenen, geheimen Schlüssel, der nicht ins Repo gehört.
