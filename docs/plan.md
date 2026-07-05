# FLIPPO Desktop — Implementierungsplan (Avalonia / C# / .NET)

> **Handoff-Dokument.** Dieser Plan wird von einem separaten Coding-Agent (Claude Opus 4.8, Effort high) Phase für Phase umgesetzt. Alle Entscheidungen sind final getroffen; jede Phase hat ein Verify-Kriterium. Der Executor braucht keine Rückfragen zu stellen — wo Mark manuell zuliefern muss, steht es explizit ("Mark-Aufgabe").
>
> **Ausführung (Handoff-Setup):** (1) Neues Verzeichnis `D:\claude\Obsidian\FLIPPO-Desktop` als leeres Git-Repo anlegen. (2) Diese Plan-Datei dorthin als `docs/plan.md` kopieren. (3) Neue Claude-Code-Session in diesem Verzeichnis mit Opus 4.8 (Effort high) starten, Android-Repo als zusätzliches Arbeitsverzeichnis: `claude --add-dir D:\claude\Obsidian\FLIPPO` (nötig für die Kotlin-Referenzdateien in P1/P3/P9). (4) `/superpowers:executing-plans` auf `docs/plan.md` — Phase für Phase mit Bestätigung pro Phase. (5) Für den Velopack-Update-Feed (P8) ein **public** GitHub-Repo verwenden (GithubSource lädt Release-Assets; privates Repo würde pro Client ein Token erfordern).

## Kontext

FLIPPO ist ein Android-Vokabeltrainer mit Spaced-Repetition, live im Play Store (v1.549, Production). Es entsteht eine **vollwertige zweite Plattform** als Desktop-App: Vokabeln am PC mit echter Tastatur anlegen/importieren, vollständige Lern-Sessions am Desktop, neue Zielgruppe, Produkt wertiger machen. Kern-Verkaufsargument bleibt: **offline-first, kein Konto-Zwang, kein Tracking, komplett kostenlos** (keine Paywall, keine Free-Limits — die gesamte Premium-/Billing-Maschinerie der Android-App entfällt am Desktop).

Interop mit Android läuft ausschließlich über das vorhandene **Backup-JSON-Format** (manueller Datei-Export/-Import als immer verfügbare Basis; Cloud-Ziele sind später nur bequeme zusätzliche Transportwege für dieselbe Datei). Kein Echtzeit-Sync.

**Vom Nutzer entschieden (05.07.2026):** MVP = Verwalten + Lernen · SessionRecords ab MVP, Statistik-Screen Post-MVP · EF Core · Cloud komplett Post-MVP (Backend hier nur konzipiert).

## Harte Constraints (nicht aufweichen)

1. **Offline-first:** Die App läuft zu 100 % ohne Konto und ohne Cloud. Cloud-Code wird ausschließlich nutzergetriggert ausgeführt — kein Startup-Check, kein Background-Polling.
2. **Kostenlos, kein Kaufzwang** — auch Cloud/Extensions.
3. **Interop-Parität:** Das Backup-JSON muss von der Android-App (Gson, kein Versions-Check, Full-Wipe-Restore) fehlerfrei gelesen werden können und umgekehrt.

---

## 1. Verbindliche Interop-Fakten (aus dem Android-Code verifiziert, 05.07.2026)

Diese Fakten sind **Implementierungsvorschriften**. Referenzdateien im Android-Repo `d:\Claude\Obsidian\FLIPPO`:
`domain/model/SrsEngine.kt`, `domain/model/FreeTextChecker.kt`, `data/model/*Entity.kt`, `data/backup/BackupManager.kt`, `domain/usecase/StartLearningSessionUseCase.kt`, Tests unter `app/src/test/java/com/asz/vokabeltrainer/domain/`.

### 1.1 SRS-Logik (SrsEngine.kt — pure Kotlin, 1:1 portieren)

- **Karteikasten:** 6 Fächer. Richtig → `min(boxLevel+1, 6)`; falsch → Fach 1 (**hartkodiert**, nicht konfigurierbar). Effektiver Nutzer-Default der Intervalle: **`[0, 4, 7, 14, 30, 180]` Tage** (DataStore-Fallback; das `SrsSettings`-Klassen-Default sagt abweichend 45 statt 180 — Desktop nutzt **180**).
- **Adaptiv (SM-2-ähnlich):** WRONG → repetitions=0, ease−0.2, Intervall 1 d · HARD → ease−0.15, Intervall×1.2, repetitions unverändert · GOOD → repetitions+1, Intervall×ease (rep0→1 d, rep1→6 d) · EASY → ease+0.15, repetitions+1, Intervall×ease×1.3. ease geklemmt auf **[1.3, 3.0]**.
- `difficulty`-Feld = ease×100 (int, Entity-Default 250); Werte **<100 werden als ease 2.5 interpretiert**.
- `lastIntervalDays` (nullable int) wird gelesen (mit Fallback-Rekonstruktion `boxLevel×ease` bei NULL) und geschrieben.
- **Leech:** `wrongCount ≥ leechThreshold` → `isLeech=true`. Reset bei Erfolgsserie (`LEECH_RESET_STREAK=3`): Karteikasten `newBoxLevel ≥ 4`, adaptiv `repetitions ≥ 3`.

### 1.2 Freitextprüfung (FreeTextChecker.kt — 1:1 portieren)

- Eingabe: trimmen + Whitespace kollabieren (Java-Regex `\s+` = **nur ASCII-Whitespace**, siehe JavaCompat unten).
- Prüfreihenfolge: exact match (case-insensitive) → bei `!strictAccents` Akzent-Normalisierung (**NFD + Combining-Diacritical-Marks-Block strippen**, NICHT Kategorie Mn) → bei `typoToleranceEnabled` Levenshtein mit maxDistance nach **Kandidaten**-Länge: **≤8→0, ≤12→1, sonst 2**.
- Kandidaten = `targetText` + `acceptedAnswers`.
- **Geschwister-Kollisionsschutz:** Entspricht die Eingabe exakt einer anderen Session-Karte (siblingAnswers = `targetText` + `acceptedAnswers` + **`sourceText`** aller anderen Karten), wird keine TYPO-Wertung vergeben.
- Bewertungs-Mapping im Lernmodus: `CheckResult.isCorrect()` = alles außer WRONG. CORRECT/ALMOST_CORRECT/TYPO → `ReviewResult.GOOD`; WRONG/Skip → `WRONG`. Kein HARD/EASY im Freitext.

### 1.3 Backup-JSON-Kontrakt (BackupManager.kt)

- Gson, camelCase (= Kotlin-Property-Namen 1:1), pretty-printed. `BackupData { version (=2), createdAt, sets[], entries[], sessionRecords[], srsSettings (nullable) }`.
- `SrsSettings { mode (FLASHCARD_BOX|ADAPTIVE), boxIntervals[], strictAccents, typoToleranceEnabled, leechThreshold, learningDirection (SOURCE_TO_TARGET|TARGET_TO_SOURCE|MIXED), maxCardsPerSession, maxNewCardsPerDay }`. Enums als Namens-Strings.
- `SessionRecord`-Feld heißt im JSON **`learningMode`** (Domain-Name; das Room-Entity-Feld heißt abweichend `learnMode` — für Interop zählt das Domain-Modell).
- **Gson schreibt keine Nulls** (`serializeNulls` aus): `lastIntervalDays: null`, `srsSettings: null`, `setId: null` fehlen im Android-Export. Umgekehrt deserialisiert Gson ohne Konstruktor: **ein vom Desktop weggelassenes Nicht-Null-Feld (z. B. `notes`) landet auf Android als `null` in einem non-null Kotlin-String → latenter Crash.** Deshalb: **Desktop-Export schreibt ausnahmslos alle nicht-nullbaren Felder** (Defaults `""`/`0`/`false`/`[]`), Nullables werden weggelassen.
- Android-Restore: **kein Versions-Check**, Full-Wipe (löscht alle Sets/Sessions), Set-ID-Remapping alt→neu. `wrongEntryIds` wird beim Restore **nicht** remappt (zeigt danach ins Leere) — Desktop spiegelt das (Formatparität, nicht heimlich fixen; Kommentar im Code).
- Android-Restore wendet `srsSettings` an (BackupUseCase) — Desktop ebenso, mit Opt-out-Checkbox im Import-Dialog.
- Der Executor verifiziert die exakten JSON-Feldnamen vor P3 gegen die Kotlin-**Domain**-Klassen (`domain/model/VocabularyEntry.kt` — 26 Properties, `VocabularySet.kt`, `SessionRecord.kt`, `SrsSettings.kt`).

### 1.4 Session-Zusammenstellung & Modi (StartLearningSessionUseCase.kt, LearnViewModel.kt)

- Kandidaten nach filterMode (DUE default | ALL | NEW | LEECH), archivierte Karten immer ausgeschlossen. Fällige zuerst, sortiert nach `nextReviewAt` aufsteigend; neue Karten (`correctCount==0 && wrongCount==0`) gemischt dahinter. `maxNewCardsPerDay` ist faktisch ein **Pro-Session-Limit ohne Tages-Tracking** — exakt so spiegeln. Danach `maxCardsPerSession`-Take.
- Richtungen: pro Karte einmalig beim Session-Start gewürfelt (`MIXED` → Random je Karte).
- **Android-Quirk:** Freitext prüft bei TARGET_TO_SOURCE trotzdem gegen `targetText`+`acceptedAnswers` (richtungsblind = Bug). **Desktop-Entscheidung: Freitext erzwingt SOURCE_TO_TARGET** — dokumentierte, bewusste Abweichung, kein Interop-Risiko.
- Multiple Choice: 3 Distraktoren aus dem Session-Pool (distinct, ohne aktuelle Karte), bei <3 Auffüllen aus der Gesamt-DB; Optionen = shuffle. Richtig → GOOD, falsch → WRONG.
- SessionRecord: nur bei Session-Ende, nur wenn `correct+wrong > 0`; `durationMinutes = max(1, elapsed/60000)`; `wrongEntryIds` = CSV der Karten-IDs. **Desktop-Zusatz:** auch bei Abbruch schreiben, wenn ≥1 Karte beantwortet (Obermenge, Format identisch).
- Undo des letzten Reviews via komplettem Karten-Snapshot (wie Android) → `Ctrl+Z`.

### 1.5 Tests als Korrektheits-Netz

**62 Tests portieren:** `SrsEngineTest` (17) + `SrsEngineLeechResetTest` (6) + `FreeTextCheckerTest` (39). Bei Abweichung zwischen JVM- und CLR-Verhalten gewinnt immer das Android-Verhalten.

### 1.6 Out of Scope am Desktop

UserDictionary/Nachschlagewerk (ist nicht im Backup-Format), Premium/Billing/Paywall, Push-Notifications, Onboarding-Tour, Firebase (kein Tracking am Desktop), `imagePath`/`audioPath` (Felder werden als Daten durchgereicht, aber kein Bild/Audio-Feature).

---

## 2. Architektur-Entscheidungen (final)

| Thema | Entscheidung | Kurzbegründung |
|---|---|---|
| Framework | **.NET 10 (LTS)** + **Avalonia 11.3.x** (neueste stabile beim Start pinnen) | .NET 8 EOL 11/2026, .NET 9 STS; 10 LTS trägt bis 11/2028 |
| Repo | **Eigenes Git-Repo `flippo-desktop`**, Vorschlag: `D:\claude\Obsidian\FLIPPO-Desktop` | Getrennte Toolchains/Releases; Interop ist nur eine JSON-Datei |
| DB | SQLite via **EF Core 10** (`Microsoft.EntityFrameworkCore.Sqlite`), klassische Migrations (eingecheckt), `Database.Migrate()` beim Start, WAL-Mode | Entschieden (Migrations für Schema-Evolution) |
| MVVM | **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`) | Standard, Source-Generator, kein ReactiveUI-Overhead |
| DI | **Microsoft.Extensions.DependencyInjection pur** (kein Generic Host) | Keine HostedServices nötig |
| JSON | **System.Text.Json** mit explizitem `[JsonPropertyName]` auf jeder DTO-Property | Kontrakt wörtlich im Code, drift-fest |
| Tests | **xUnit** (kein FluentAssertions — Lizenzwechsel) | Standard, `[Theory]/[InlineData]` |
| i18n | **resx** (`Strings.resx` EN neutral + `Strings.de.resx`), Wechsel wirkt nach Neustart | Android-`strings.xml` als Übersetzungsquelle |
| Distribution | **Velopack + GitHub Releases** als Update-Feed; self-contained, KEIN Single-File, **KEIN Trimming** | EF Core nicht trim-safe; Velopack braucht Ordner-Layout für Delta-Updates |
| Kein Repository-Pattern | `AddDbContextFactory` + 3 fachliche **Stores** | EF ist bereits Repo+UoW; zweite Abstraktion = Ballast |
| Cloud im MVP | **Nein.** MVP nutzt nur `IStorageProvider`-Datei-Dialoge. `BackupService` trennt aber von Anfang an Serialisierung ↔ Transport (Stream-basierte API), sodass die spätere `IBackupDestination`-Schicht (Abschnitt 6) reine Mechanik ist | Keine spekulativen Abstraktionen im MVP |

## 3. Solution-Struktur

```
flippo-desktop/
├─ FlippoDesktop.sln
├─ Directory.Build.props            (net10.0, Nullable enable, TreatWarningsAsErrors)
├─ src/
│  ├─ Flippo.Core/                  KEINE NuGet-Deps außer BCL
│  │  ├─ Domain/    VocabularyEntry.cs, VocabularySet.cs, SessionRecord.cs, SrsSettings.cs,
│  │  │             VocabularyEntryUpdate.cs, ReviewResult.cs, SrsMode.cs, LearningMode.cs, LearningDirection.cs
│  │  ├─ Srs/       SrsEngine.cs                (static; Signatur: Schedule(entry, result, settings, long nowMs))
│  │  ├─ Checking/  FreeTextChecker.cs          (static; CheckResult-Enum, CheckOutcome-Record)
│  │  ├─ Session/   SessionComposer.cs, SessionPlan.cs   (injizierbarer Random + nowMs)
│  │  ├─ Backup/    BackupDtos.cs, BackupSerializer.cs, BackupMapper.cs
│  │  └─ Util/      JavaCompat.cs               (RoundHalfUp, ASCII-Whitespace-Regex, Diakritika-Regex)
│  ├─ Flippo.Data/                  EF Core
│  │  ├─ FlippoDbContext.cs, DesignTimeDbContextFactory.cs, AppPaths.cs
│  │  ├─ Entities/  VocabularyEntryEntity.cs, VocabularySetEntity.cs, SessionRecordEntity.cs
│  │  ├─ Migrations/                (eingecheckt)
│  │  └─ Services/  VocabularyStore.cs, SessionStore.cs, BackupService.cs, SettingsService.cs
│  └─ Flippo.App/                   Avalonia
│     ├─ Program.cs, App.axaml(.cs), ViewLocator.cs
│     ├─ ViewModels/  MainWindow-, SetsOverview-, SetDetail-, CardEditor-, LearnSession-,
│     │               SessionSummary-, Settings-, ImportPreviewViewModel
│     ├─ Views/       (je VM eine View; Lernmodi als DataTemplates)
│     ├─ Services/    NavigationService.cs, DialogService.cs, LocalizationService.cs
│     ├─ Resources/   Strings.resx, Strings.de.resx
│     └─ Assets/Fonts/ Noto Sans + Noto Sans Arabic (eingebettet, via FontManagerOptions.FontFallbacks)
└─ tests/
   └─ Flippo.Tests/                 xUnit
      ├─ Srs/  Checking/  Session/  Data/
      ├─ Backup/   BackupContractTests.cs, BackupRoundtripTests.cs, AndroidFixtureTests.cs
      └─ Fixtures/ android-backup-v2.json      (Mark-Aufgabe: echten Android-Export liefern)
```

NuGet: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, **Avalonia.Controls.DataGrid**, Avalonia.Diagnostics (Debug), CommunityToolkit.Mvvm, MS.Extensions.DependencyInjection, MS.EntityFrameworkCore.Sqlite (+Design), xunit, Velopack. Post-MVP: ClosedXML (P9), Google.Apis.Drive.v3, MSAL (C1).

## 4. Schlüssel-Spezifikationen

### 4.1 EF Core / Daten-Layer

- Schema = Spiegel von Room v9, aber EF-Standardbenennung (DB-Datei ist KEIN Interop-Artefakt). Mutable Entity-POCOs getrennt vom Core-Domain-Modell; Mapping per Hand in den Stores.
- `AcceptedAnswers`/`Tags` (`List<string>`): **ValueConverter** auf JSON-String-Spalte (wie Android) + ValueComparer. Nicht normalisieren (keine Queries darüber).
- Alle Timestamps als `long` Unix-ms (Konvertierung nur für UI-Anzeige). Keys `long` autoincrement. FK Entry→Set mit `OnDelete(Cascade)`. Indizes: SetId, NextReviewAt, IsArchived, IsLeech.
- DB-Pfad via `AppPaths`: Win `%APPDATA%\FLIPPO\flippo.db` · macOS `~/Library/Application Support/FLIPPO/` (explizit bauen — `SpecialFolder.ApplicationData` liefert dort `~/.config`!) · Linux `$XDG_DATA_HOME/flippo/` Fallback `~/.local/share/flippo/`. Daneben `settings.json` + `backups/` (Safety-Exports).
- Store-Tests gegen echte SQLite-Dateien im Temp-Verzeichnis (kein InMemory-Provider — der lügt bei FK/Cascade).
- Settings (`settings.json`, atomar via Temp+Rename): srsMode, boxIntervals (Default `[0,4,7,14,30,180]`), strictAccents=false, typoTolerance=true, leechThreshold=4, learningDirection, maxCardsPerSession=50, maxNewCardsPerDay=0, uiTheme, fontSize, uiLanguage.

### 4.2 Domain-Port — JavaCompat-Fallstricke (bindend)

1. **`Math.round`:** Java rundet half-up, .NET banker's rounding. ALLE Rundungsstellen der Adaptiv-Logik (×1.2, ×ease, ×ease×1.3, ease×100, Fallback `boxLevel×ease`) nutzen `JavaCompat.RoundHalfUp(v) = (long)Math.Floor(v + 0.5)`.
2. **Whitespace-Kollaps:** Java `\s` = nur ASCII. Explizite Klasse `[ \t\n\f\r]+` als kompiliertes Regex (NICHT .NET-`\s` — matcht Unicode/NBSP).
3. **Diakritika:** `Normalize(FormD)` + Regex-Block `\p{IsCombiningDiacriticalMarks}+` (U+0300–U+036F). **NICHT** Kategorie `Mn` filtern (würde arabische Harakat/hebräische Nikud strippen → Abweichung von Android).
4. Case-insensitive: `StringComparison.OrdinalIgnoreCase` / `ToLowerInvariant()`. Formeln in identischer IEEE-754-Reihenfolge lassen (keine algebraischen "Vereinfachungen").
5. Test-Portierung mechanisch 1:1, Kotlin-Backtick-Namen → PascalCase (Original als Kommentar), gleiche Argumentreihenfolge bei `Assert.Equal(expected, actual)`. Zusätzlich eigene Tests für `RoundHalfUp` (0.5/1.5/2.5).

### 4.3 Backup-Interop

- DTOs mit `[JsonPropertyName]` je Property; **Enums als rohe Strings im DTO**, Mapping in Domain-Enums im `BackupMapper` mit tolerantem Fallback (unbekannter Wert → Default + Import-Warnung) — ein unbekannter Enum-String darf den Import nicht crashen.
- Export-Options: `WriteIndented=true`, `DefaultIgnoreCondition=WhenWritingNull`, `Encoder=UnsafeRelaxedJsonEscaping`. `version` fest 2. Alle Non-Nullables immer schreiben (Fakt 1.3).
- **Import = Full-Wipe wie Android, aber abgesichert:** (1) Preview-Dialog "X Sets, Y Karten, Z Sessions ersetzen ALLE lokalen Daten (A/B)" + Checkbox "SRS-Einstellungen übernehmen"; (2) davor automatischer Safety-Export nach `<datadir>/backups/pre-import-{timestamp}.json` (max. 10); (3) eine Transaktion: Wipe → Sets mit ID-Mapping → Entries mit remapptem setId (unbekanntes setId: überspringen + Warnung — bewusste sichere Abweichung, Android würde FK-crashen) → SessionRecords unverändert. Kein Versions-Blocker, Warnhinweis bei `version > 2`.
- Tests: `BackupContractTests` (exakte JSON-Key-Menge pro Objekt gegen hartkodierte Liste = Drift-Alarm), `BackupRoundtripTests` (+ Sparse-JSON-Test), `AndroidFixtureTests` (echte Android-Datei; parst, Zählungen > 0, Stichproben, Import→Export→Parse semantisch gleich). Release-Gate: Desktop-Export manuell in Android-App importieren.

### 4.4 UI / Screens (MVP)

Ein Fenster, VM-first-Navigation (`MainWindowViewModel` mit `CurrentPage` + Back-Stack, `INavigationService`, ViewLocator-Konvention). Sidebar: Karteien | Einstellungen.

- **SetsOverview:** Set-Liste mit Zählern gesamt/fällig/neu (eine aggregierte Query, kein N+1); Aktionen: Neues Set, Backup-Import/-Export, "Alle fälligen lernen".
- **SetDetail:** `Avalonia.Controls.DataGrid` (virtualisiert, reicht für zehntausende Zeilen). Spalten: Quelle, Ziel, Fach, fällig am, Leech, Tags; Suche/Filter; Lernen-Split-Button (Fällige/Alle/Neue/Leeches).
- **CardEditor:** **Andock-Panel rechts, kein Modal** — tastatur-first. Tab-Folge sourceText → targetText → acceptedAnswers (eine TextBox, Semikolon-getrennt) → exampleSentence → notes; Expander "Mehr" für die übrigen Felder. Shortcuts: `Ctrl+N` neu, `F2`/`Enter` editieren, **`Ctrl+Enter` Speichern & Nächste** (Schnellanlage-Loop, Fokus zurück auf sourceText), `Ctrl+S` Speichern & schließen, `Esc` verwerfen, `Entf` löschen (Confirm).
- **LearnSession** (Modi als DataTemplates): Flashcard (`Space` umdrehen; Box: `1`/`2` = Falsch/Richtig; Adaptiv: `1`–`4` = Nochmal/Schwer/Gut/Einfach, mit Intervall-Vorschau per SrsEngine-Dry-Run) · Freitext (immer sourceText als Frage; `Enter` prüfen/weiter, `F1` "Weiß nicht"; Feedback-Texte für ALMOST_CORRECT/TYPO/WRONG) · Multiple Choice (Tasten `1`–`4`, sofortiges Feedback, `Enter` weiter). Global: `Ctrl+Z` Undo, `Esc` beenden (Confirm). Danach SessionSummary (Quote, "Falsche wiederholen").
- **Settings:** SRS-Einstellungen, Theme (System/Hell/Dunkel via `RequestedThemeVariant`), fontSize (3 Stufen als Resource-Override), Sprache (DE/EN, wirkt nach Neustart).
- Datei-Dialoge ausschließlich `IStorageProvider` vom `TopLevel`; Export `SuggestedFileName = "flippo-backup-{yyyy-MM-dd}.json"`.

## 5. Roadmap MVP (jede Phase einzeln baubar)

| Phase | Inhalt | Verify |
|---|---|---|
| **P0 Gerüst** | Solution, 3 src- + 1 Test-Projekt, Directory.Build.props, leeres Avalonia-Fenster, DI-Bootstrap, ViewLocator | `dotnet build` fehlerfrei; `dotnet run --project src/Flippo.App` öffnet Fenster |
| **P1 Domain-Port** | Modelle, SrsEngine, FreeTextChecker, JavaCompat; 62 Tests + JavaCompat-Tests | `dotnet test` grün, ≥62 Tests in Srs/Checking |
| **P2 Daten-Layer** | DbContext, Entities, Converter, Initial-Migration, AppPaths, Stores, SettingsService | `dotnet ef migrations list` zeigt Initial; Store-Tests (CRUD, Cascade, List-Roundtrip) grün |
| **P3 Backup-Interop** | DTOs, Serializer, Mapper, BackupService (Import mit Wipe+Safety-Export, Export), Kontrakt-/Roundtrip-/Fixture-Tests. **Mark-Aufgabe: echtes Android-Backup als Fixture liefern** (bis dahin Fixture-Test mit `Skip`-Begründung) | `dotnet test` grün inkl. AndroidFixtureTests |
| **P4 Shell + Sets read-only** | Navigation, SetsOverview mit Zählern, SetDetail-DataGrid read-only, Import/Export-UI mit Preview-Dialog | Manuell: Android-Backup importieren → Daten sichtbar; Gegenprobe: Desktop-Export in Android-App importieren |
| **P5 Verwaltung** | Set-CRUD, CardEditor-Panel mit allen Shortcuts, Schnellanlage-Loop, Suche/Filter | Checkliste: 3 Karten nur per Tastatur anlegen (Ctrl+Enter-Loop), F2-Edit, Entf; Neustart: persistent |
| **P6 Lern-Session** | SessionComposer + Tests, 3 Modi, Tastatursteuerung, Undo, SessionRecord, Summary | SessionComposer-Tests grün; je Modus eine manuelle Session; Export enthält `sessionRecords` mit korrektem `learningMode` |
| **P7 Settings/Theme/i18n** | SettingsView, Dark Mode, fontSize, DE/EN-resx vollständig (Quelle: Android `values/strings.xml` + `values-de/`) | Theme live; Sprache nach Neustart; Box-Intervall-Änderung ändert Intervall-Vorschau |
| **P8 Distribution = Release v0.1.0** | Velopack-Integration, `build/pack.ps1`, GitHub Release, Update-Check beim Start (async, nicht blockierend) | `vpk pack` erzeugt Artefakte; Install auf sauberem Windows-Profil; v0.1.0→v0.1.1-Update via GitHub-Feed durchläuft |
| **P9 Datei-Import (Post-MVP)** | CSV/TSV (eigener Parser wie Android) + **XLSX via ClosedXML** (MIT; manuelles OpenXML wäre Wochen für nichts); ` / `-Alternativen → acceptedAnswers; Import-Preview mit Spalten-Mapping; Android-Import-Tests portieren (ImportEngineTest, XlsxRoundtripTest) | `dotnet test` grün inkl. portierter Import-Tests; Beispiel-XLSX importiert |

Danach: **P10 Statistik-Screen** (Daten liegen ab P6 bereit), dann Cloud-Phasen C1–C3 (Abschnitt 6/7).

## 6. Cloud-Schicht (Post-MVP, Opt-in) — Spezifikation

Neues Projekt `Flippo.Cloud` ab C1 (Abstractions/, Destinations/, Security/, Catalog/); die App referenziert nur die Abstraktionen, Registrierung via DI.

### 6.1 Transport-Abstraktion

```csharp
enum BackupDestinationKind { LocalFolder, GoogleDrive, OneDrive, FlippoCloud }
record BackupFileInfo(string RemoteId, string FileName, DateTimeOffset CreatedAt, long SizeBytes);

interface IBackupDestination {
    Guid DestinationId; string DisplayName; BackupDestinationKind Kind;
    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken);
    Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken);
    Task<Stream> DownloadAsync(string remoteId, CancellationToken);
    Task DeleteAsync(string remoteId, CancellationToken);
}
interface IDestinationConnector {   // pro Kind; Auth orthogonal zum Transport
    BackupDestinationKind Kind;
    Task<DestinationConfig> ConnectInteractiveAsync(CancellationToken);
    Task DisconnectAsync(DestinationConfig);
    IBackupDestination Create(DestinationConfig);
}
```

- Bewusst NICHT drin: Delta-Sync, Konflikt-Auflösung, Resumable Uploads (Dateien sind KB-groß). Retention ("letzte 10 behalten") ist Client-Policy oberhalb des Interfaces.
- Fehler → vier UI-Zustände: `NotConnected` (Re-Login-CTA), `Offline` (Toast, nicht blockierend, "Stattdessen lokal speichern?"), `QuotaExceeded`, `TransportFailed`. HTTP-Timeout 15 s.
- Config-Speicherung getrennt: `destinations.json` (nur unsensible Metadaten) + **`ITokenVault`** für Secrets: Windows DPAPI (`ProtectedData`, CurrentUser), macOS Keychain, Linux libsecret mit dokumentiertem `chmod 600`-Datei-Fallback. Kein Master-Passwort, keine eigene Krypto. OneDrive nutzt stattdessen den MSAL-Cache (`MsalCacheHelper` bringt alle drei OS-Backends mit).
- UI: Einstellungen → "Backup-Ziele" (Karten-Liste + "Ziel hinzufügen" → Provider-Wahl → Browser-OAuth → Karte mit Account-Hint). Backup-Screen: Ziel-Dropdown (Default "Datei wählen…"). Restore: Ziel → Liste → Download → derselbe Preview-/Confirm-Dialog wie lokaler Import.

### 6.2 Google Drive (C1)

- NuGet `Google.Apis.Drive.v3` + `Google.Apis.Auth`. Installed-App-Flow mit Loopback (`http://127.0.0.1:{port}`, PKCE) via `GoogleWebAuthorizationBroker` + eigenem `IDataStore` → `ITokenVault`.
- **Scope: `drive.file`** (non-sensitive/"Recommended" wie `drive.appdata` — aber Backups liegen sichtbar in "Meine Ablage/FLIPPO/", Nutzer kann die JSON heute schon manuell in die Android-App importieren). Ordner `FLIPPO` per files.list suchen/anlegen.
- Client-Secret bei Desktop-Apps gilt laut Google explizit als nicht geheim → darf ins Binary. Consent-Screen "External" auf **"In production"** stellen (im Testing-Status verfallen Refresh-Tokens nach 7 Tagen!); non-sensitive Scope = keine Google-Review, kein "unverified app"-Screen.
- **Mark-Aufgabe (C1):** Google-Cloud-Projekt + OAuth-Client "Desktop app" anlegen, Consent-Screen auf Production.

### 6.3 OneDrive (C1)

- NuGet `Microsoft.Identity.Client` + `.Extensions.Msal`. **Bewusst OHNE Microsoft.Graph-SDK** — 4 REST-Calls direkt via HttpClient: `GET /me/drive/special/approot/children`, `PUT /me/drive/special/approot:/{name}:/content`, `GET /items/{id}/content`, `DELETE /items/{id}`.
- Scope `Files.ReadWrite.AppFolder` (→ Ordner "Apps/FLIPPO", nutzerkonsentierbar ohne Admin). PublicClient, Authority `common`, Redirect `http://localhost`, System-Browser (`WithUseEmbeddedWebView(false)`); WAM-Broker als optionales Later. `AcquireTokenSilent` → bei `MsalUiRequiredException` Zustand `NotConnected` (kein Auto-Popup mitten in einer Operation).
- **Mark-Aufgabe (C1):** Entra-ID-App-Registrierung (kostenlos): "any org + personal accounts", Plattform Mobile/Desktop, Redirect `http://localhost`.

## 7. FLIPPO-Backend (Konzept — eigenständiger Folgeauftrag, hier NICHT bauen)

### 7.1 Content-Katalog: komplett ohne Server-Code (C2)

- **Statische Dateien, Git als CMS:** Repo `solutionworxug/flippo-content` auf GitHub Pages (Muster von `flippo-privacy` bekannt, 0 €, CDN). Basis-URL als App-Config → späterer Umzug (z. B. Cloudflare R2) transparent.
- **Index `/catalog/v1/index.json`:** `{ formatVersion, catalogVersion, generatedAt, packs[ { id, kind, title, sourceLanguage, targetLanguage, packVersion, entryCount, sizeBytes, sha256, url, tags[] } ] }`. Pack-Dateinamen **versioniert + immutabel** (`{id}-v{n}.json`); `sha256` pflicht, Client verweigert bei Mismatch. Konsum mit ETag/`If-None-Match`, lokale installed-packs-Registry (`packId → packVersion`), Download nur auf Klick.
- **Pack-Format = existierendes Themeset-Format** (`{id, sourceLanguage, targetLanguage, title, entries[{source,target,example,pos,notes,tags}]}` + optional `formatVersion`) — NICHT das Backup-Format (das transportiert SRS-Zustand + lokale IDs, für Content semantisch falsch). Android-Parser existiert (`ThemeSetRepository`); die 120+ Asset-Dateien der Android-App sind das Seed-Inventar (Konverter-Skript Manifest→Index inkl. sha256).
- **"Extension" =** deklaratives Content-Pack, identifiziert über `kind`; v1 nur `kind: "vocab-set"`, später z. B. `"bundle"`. **Code-Plugins explizit out of scope** (kein DLL-Loading, kein Scripting). Unbekannte `kind`-Werte werden ignoriert. Update-Semantik v1: neuere packVersion → Badge; Import erzeugt frisches Set (keine SRS-Zustands-Migration).

### 7.2 Backup-API (C3)

- **Auth: E-Mail-Magic-Link → JWT (90 Tage, Claims sub + scope "backup").** Gegen Alternativen: Recovery-Code geht beim Geräteverlust mit verloren (genau das Backup-Szenario); Passkeys = unverhältnismäßiger WebAuthn-Aufwand. Kein Refresh-Token — nach Ablauf neuer Magic-Link. Mail via Resend Free-Tier (3 000/Monat).
- **API:** `POST /v1/auth/magic-link` (immer 204, kein User-Enumeration-Leak) · `POST /v1/auth/token {email, code}` · `GET/PUT /v1/backups` · `GET/DELETE /v1/backups/{id}`. Blobs opak, Server parst nichts.
- **Quota/Schutz:** 5 Backups × 2 MB pro Nutzer (6. Upload → 409, Client bietet Löschen des ältesten an — kein Auto-Evict); Read-Cap 2 MB → 413; Magic-Link 3/h pro E-Mail, 10/h pro IP; API 60 req/h; Codes 6-stellig, 10 min, 5 Fehlversuche. Housekeeping: Accounts > 24 Monate inaktiv anmailen, dann löschen.
- **Hosting-Empfehlung: Cloudflare Workers + R2 + D1 — 0 €/Monat**, Null-Ops (Worker ~300 Zeilen TypeScript, schreibt der Coding-Agent). Dokumentierter Fallback: Hetzner-VPS + ASP.NET Core Minimal API + SQLite + Caddy, ~4–6 €/Monat, aber laufende Ops. (Azure: vertraut, aber Cold-Starts + Kostenkontroll-Overhead — nicht als Erstes.)
- **Mark-Aufgaben (C3):** Cloudflare-Account, Resend-Account, ggf. Domain.

## 8. Cloud-Roadmap (nach P10)

| Phase | Inhalt | Verify |
|---|---|---|
| **C1** | `Flippo.Cloud`-Projekt: Abstraktion + `LocalFolderDestination` + GDrive + OneDrive, `ITokenVault`, Connect-UI, Offline-Fehlerbild | Interface-Roundtrip-Test (Upload→List→Download byte-identisch) mit Fake + LocalFolder; gemockter Timeout → Zustand `Offline`, App-Smoke-Test grün trotz unerreichbarem Ziel; einmalig manuell: echter GDrive/OneDrive-E2E, Datei im Web-UI sichtbar |
| **C2** | Repo `flippo-content` + Konverter-Skript, Katalog-Screen im Desktop | Test gegen lokalen Fixture-HTTP-Server: Index → Pack → sha256 → Import erzeugt Set mit entryCount Karten; manipulierte Checksumme → verweigert; ETag-Header gesendet |
| **C3** | Cloudflare-Worker (Auth + Backup-API) + `FlippoCloudDestination` | Skript gegen Dev-Deployment: Magic-Link → Token → PUT → LIST → GET byte-identisch → 6. PUT 409 → 3-MB-PUT 413 |
| **C4 (optional)** | Katalog-Konsum in der Android-App (gleicher Index, Gson-Parser existiert) | Android-Test: Index-Fixture → Pack-Import via ThemeSetRepository-Pfad |

## 9. Distribution & Cross-Platform

- **Publish:** self-contained `win-x64` primär; `osx-arm64`, `linux-x64` Bonus. Kein Single-File, kein Trimming, ReadyToRun aus.
- **SmartScreen:** zunächst unsigniert ("Unbekannter Herausgeber" bis Reputation) + Hinweis im Release-Text; später günstigster Weg Azure Trusted Signing (~10 USD/Monat) — Erweiterungspunkt, nicht MVP. macOS: ad-hoc codesign + dokumentierter xattr-Workaround; echte Notarisierung (99 USD/Jahr) explizit Post-MVP. Linux: portable `.tar.gz`, AppImage via Velopack als zweites Artefakt sobald getestet.
- **Fallstricke:** Pfade nur via `AppPaths`/`Path.Combine` (Linux case-sensitiv); StorageProvider nur vom TopLevel, Abbruch-`null` behandeln; **Font-Fallback** = Hauptrisiko für Arabisch/Kyrillisch → Noto Sans (+ Arabic) einbetten und via `FontManagerOptions.FontFallbacks` registrieren, manueller Smoke-Test mit arabischen + kyrillischen Vokabeln in P4; `FlowDirection` bleibt LTR (arabischer Inhalt in Controls braucht kein globales RTL); Linux-IME (fcitx/ibus) als bekannte Einschränkung dokumentieren; HiDPI-Check 100/150/200 %.

## 10. Verifikation gesamt (Definition of Done je Release)

1. `dotnet build` + `dotnet test` grün (nach P6: ≥62 Domain- + Composer- + Backup-Tests).
2. **Interop-Gate vor jedem Release:** echtes Android-Backup importieren → am Desktop lernen → exportieren → in der Android-App importieren → Stichproben (SRS-Zustand, SessionRecords, srsSettings) korrekt.
3. P8-Gate: Velopack-Install + Update-Durchlauf auf sauberem Windows-Profil.
