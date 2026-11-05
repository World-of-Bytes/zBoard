# Signaturschlüssel für Testversionen

`zboard-test.keystore` signiert alle Debug- und Testversionen (lokal und in GitHub Actions), damit sich neue
Testversionen über ältere installieren lassen. Android akzeptiert ein Update nur mit demselben Schlüssel.

| | |
| --- | --- |
| Alias | `zboard-test` |
| Passwort (Keystore und Schlüssel) | `zboard-test` |

Der Schlüssel ist absichtlich öffentlich und **nur für Testversionen** gedacht. Veröffentlichte Versionen
(Play Store, F-Droid, GitHub-Releases) bekommen einen eigenen, geheimen Schlüssel, der nicht ins Repo gehört.

## Upload-Schlüssel für Google Play einrichten (einmalig)

Google Play signiert die App selbst („Play App Signing“). Hochgeladen wird mit einem eigenen **Upload-Schlüssel**.
Geht er verloren, lässt er sich über die Play Console ersetzen; trotzdem sicher aufbewahren (Passwort-Manager).

1. Schlüssel erzeugen (Java muss installiert sein; Passwörter werden abgefragt):

   ```bash
   keytool -genkeypair -v -keystore zboard-upload.keystore -alias zboard-upload \
     -keyalg RSA -keysize 4096 -validity 10000 \
     -dname "CN=World of Bytes, O=World of Bytes, C=DE"
   ```

2. Keystore als Base64 kodieren:

   ```bash
   base64 -w0 zboard-upload.keystore > zboard-upload.keystore.b64      # Linux
   base64 -i zboard-upload.keystore -o zboard-upload.keystore.b64      # macOS
   ```

   Unter Windows (PowerShell): `[Convert]::ToBase64String([IO.File]::ReadAllBytes("zboard-upload.keystore")) > zboard-upload.keystore.b64`

3. Auf GitHub unter **Settings → Secrets and variables → Actions → New repository secret** anlegen:

   | Secret | Inhalt |
   | --- | --- |
   | `ZBOARD_UPLOAD_KEYSTORE` | Inhalt von `zboard-upload.keystore.b64` |
   | `ZBOARD_UPLOAD_STORE_PASSWORD` | Passwort des Keystores |
   | `ZBOARD_UPLOAD_KEY_ALIAS` | `zboard-upload` |
   | `ZBOARD_UPLOAD_KEY_PASSWORD` | Passwort des Schlüssels |

4. Auf GitHub ein Release veröffentlichen (Tag z. B. `v0.9`). Der Workflow baut dann `zboard-<version>.aab` für
   Google Play und `zboard-<version>.apk` und hängt beide an das Release. Die `.keystore`-Datei und die Base64-Datei
   gehören **nicht** ins Repo.
