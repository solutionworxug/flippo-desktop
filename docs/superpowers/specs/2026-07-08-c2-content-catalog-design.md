# C2 — Content-Katalog (Design)

**Datum:** 2026-07-08
**Kontext:** Cloud-Phase C2 (`docs/plan.md` §7.1): Themensets **online nachladen**, ohne App-Update
und ohne Server-Code — statische Dateien auf GitHub Pages, Git als CMS. Baut auf P12 (gebündelte
Themensets, `IThemeSetSource`, `ThemeSetImporter`) und dem `Flippo.Cloud`-Projekt (C1) auf.

## Mit Mark entschieden

- **Katalog-UX: im bestehenden Themenset-Picker** — geblendete UND Online-Packs in einer Liste
  (Online mit Download-Kennzeichnung), kein eigener Screen.
- **Keine Pack-Updates in diesem Schnitt** — Registry merkt nur „importiert"; neuere packVersions
  werden ignoriert (importierte Packs sind normale Karteien mit Lernfortschritt).

## Ziel & Nicht-Ziele

**Ziel:** Nutzer öffnet den „Themensets…"-Picker und sieht zusätzlich zu den gebündelten Sets die
Online-Packs aus dem Katalog (`flippo-content` auf GitHub Pages). Klick auf ein Online-Pack lädt es
(sha256-verifiziert) und importiert es über den bestehenden Themeset-Import-Pfad als normale Kartei.

**Nicht-Ziele:** Pack-Updates/Versions-Upgrade-UI, Kategorien/Suche im Picker, Katalog-CI
(Index wird manuell per Skript gebaut), Bilder/Audio in Packs, Ersatz der gebündelten Assets
(offline-first: Bundle bleibt).

## Constraints

- **Offline-first, opt-in:** kein Startup-Fetch, kein Polling — Katalog wird nur beim Öffnen des
  Pickers geladen. Katalog nicht erreichbar → Picker bleibt mit gebündelten Sets voll nutzbar
  (stiller Hinweis, nicht blockierend).
- **`Flippo.Core` bleibt BCL-only.** Katalog-Client liegt in `Flippo.Cloud/Catalog/` (HttpClient =
  BCL; keine neuen NuGet-Pakete nötig).
- **Pack-Format = existierendes Themeset-Format** (`ThemeSetFile`) — NICHT das Backup-Format.
  Import läuft durch den bestehenden `ThemeSetImporter`-Mapper (Slash-Split, Tags, PoS).
- **sha256 ist Pflicht:** Client verweigert Import bei Checksummen-Mismatch.
- Neue UI-Strings in `Strings.resx` (EN) **und** `Strings.de.resx` (DE).
- Repo-Anlage + GitHub Pages = externer Effekt → nur mit Marks OK im Ausführungsschritt.

## Teil 1 — Repo `solutionworxug/flippo-content` (neu, öffentlich, GitHub Pages)

```
catalog/v1/index.json                 ← Katalog-Index
catalog/v1/packs/{id}-v{n}.json       ← versionierte, IMMUTABLE Pack-Dateien (Themeset-Format)
tools/build-index.ps1                 ← Konverter-Skript (PowerShell)
README.md
```

- **Seed-Inventar:** die 240 gebündelten Themesets aus
  `src/Flippo.App/Assets/ThemeSets/` (beweist die Pipeline; künftige schlanke Clients können den
  Katalog statt Bundling nutzen) **+ 1 kleines neues Demo-Pack** (~15 Einträge, neu authored,
  nicht im Bundle) — das Vehikel für den Live-E2E („nur online"-Pack sichtbar → Download → Import).
- **`tools/build-index.ps1`:** liest die Pack-Dateien unter `catalog/v1/packs/`, berechnet je Datei
  sha256 + sizeBytes + entryCount (aus dem JSON), schreibt `catalog/v1/index.json`. Manuell
  ausgeführt bei Content-Änderungen (kein CI — YAGNI).
- **Pages-Muster wie `flippo-privacy`** (Branch-Serving, 0 €, CDN). Basis-URL:
  `https://solutionworxug.github.io/flippo-content`.

## Teil 2 — Index-Format (`catalog/v1/index.json`, Plan §7.1 wörtlich)

```json
{
  "formatVersion": 1,
  "catalogVersion": 1,
  "generatedAt": "2026-07-08T12:00:00Z",
  "packs": [{
    "id": "en-farben", "kind": "themeset", "title": "Farben",
    "sourceLanguage": "Englisch", "targetLanguage": "Deutsch",
    "packVersion": 1, "entryCount": 30, "sizeBytes": 4711,
    "sha256": "<hex>", "url": "packs/en-farben-v1.json", "tags": []
  }]
}
```

- `url` relativ zur Index-URL. Pack-Dateinamen versioniert + immutabel (`{id}-v{n}.json`).
- Unbekannte `kind`-Werte werden vom Client übersprungen (Vorwärtskompatibilität).

## Teil 3 — `Flippo.Cloud/Catalog/`

- **`CatalogModels.cs`:** DTOs `CatalogIndex { FormatVersion, CatalogVersion, GeneratedAt, Packs }`,
  `CatalogPack { Id, Kind, Title, SourceLanguage, TargetLanguage, PackVersion, EntryCount,
  SizeBytes, Sha256, Url, Tags }` (System.Text.Json, camelCase).
- **`CatalogClient.cs`:** Konstruktor nimmt Basis-URL + Cache-Dateipfad (testbar gegen
  Fixture-Server).
  - `GetIndexAsync(ct)`: GET `catalog/v1/index.json` mit **`If-None-Match`** (ETag aus Disk-Cache
    `catalog-cache.json`: `{ etag, indexJson }`); 200 → Cache aktualisieren; **304 → Index aus
    Cache**; Fehler/Timeout (15 s) → `null` (Aufrufer zeigt nur gebündelte).
  - `DownloadPackAsync(pack, ct)`: GET Pack-URL → Bytes → **sha256 gegen `pack.Sha256` prüfen**
    (Mismatch → Exception/`null` mit Fehlerkennung „verweigert") → Deserialisierung als
    `ThemeSetFile` (Format identisch) → zurück.
- Kein neues NuGet (HttpClient + SHA256 = BCL).

## Teil 4 — App-Schicht

- **`InstalledPacksRegistry`** (App-Service, Muster `DestinationStore`): `installed-packs.json` im
  Datenverzeichnis (`AppPaths.InstalledPacksFile`), Map `packId → packVersion`. Gerätelokal,
  **nicht im Backup**. `IsInstalled(id)`, `MarkInstalled(id, version)`.
- **`ThemeSetImporter`:** aus `ImportAsync` wird der Datei-Teil als
  `ImportFileAsync(ThemeSetFile file, string displayTitle, long nowMs)` extrahiert (bestehende
  Methode delegiert; Verhalten byte-gleich — Titel-Dedupe gegen bestehende Karteien inklusive).
- **`ThemeSetPickerViewModel`:** lädt wie bisher **sofort** die gebündelten Einträge; parallel
  asynchron den Katalog (`CatalogClient.GetIndexAsync`). Merge: Online-Packs, deren `id` gebündelt
  ist, werden **ausgeblendet** (Bundle gewinnt — offline, instant). Online-Einträge zeigen
  Download-Kennzeichnung + Größe; „Importiert"-Zustand aus Registry bzw. Titel-Dedupe. Klick auf
  Online-Pack: Download → sha256 → `ImportFileAsync` → Registry-Eintrag → Zeile wird „Importiert".
  Katalog nicht erreichbar → dezenter Hinweis (Caption), Picker voll nutzbar.
- **Sprachfilter** wie bisher (UI-Sprache → Zielsprache), gilt auch für Online-Packs.
- Basis-URL als Konstante mit Override-Möglichkeit über `AppSettings` (Feld `CatalogBaseUrl`,
  Default leer = eingebaute URL; kein UI dafür) — macht Fixture-Tests und späteren Umzug trivial.

## Teil 5 — Fehlerbilder

- Index-Timeout/Netzfehler → Online-Teil fehlt still (Caption „Katalog nicht erreichbar").
- Pack-Download-Fehler → Meldung (bestehender Dialog-Pfad), kein Import.
- **sha256-Mismatch → Import verweigert** + Meldung (deutlich, nicht still).
- Kaputtes `catalog-cache.json` → wie „kein Cache" behandeln (kein Crash).

## Teil 6 — Tests (Plan-C2-Verify)

Gegen **lokalen Fixture-HTTP-Server** (in-proc, z.B. `HttpListener` auf freiem Port, liefert
Fixture-Index + -Packs mit ETag-Unterstützung):
1. Index → Pack → sha256 ok → `ImportFileAsync` erzeugt Set mit `entryCount` Karten.
2. **Manipulierte Checksumme → Import verweigert.**
3. **ETag:** Erstabruf 200 + Cache; Zweitabruf sendet `If-None-Match`, Server antwortet 304,
   Client nutzt Cache-Index.
4. Registry: MarkInstalled/IsInstalled round-trip über Disk.
5. Bestehende 213 Tests bleiben grün.

**Live-E2E (Mark):** Picker öffnen → Demo-Pack (nur online) erscheint mit Download-Kennzeichnung →
Klick → Import → Kartei mit ~15 Karten da; zweites Öffnen zeigt „Importiert"; Offline (WLAN aus) →
Picker zeigt nur gebündelte + Hinweis.

## Ausführungsreihenfolge (Hinweis für den Plan)

Erst App-Seite gegen Fixtures (voll testbar, kein externer Effekt), dann Repo-Anlage +
Seed + Pages (externer Effekt, Marks OK), dann Live-E2E.
