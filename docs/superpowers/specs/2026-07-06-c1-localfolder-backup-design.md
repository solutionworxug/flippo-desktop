# C1 — Slice 1: LocalFolder-Backup-Ziel (Design)

**Datum:** 2026-07-06
**Kontext:** Erster vertikaler Durchstich der Cloud-Schicht (`docs/plan.md` Abschnitt 6). Beweist die
Transport-Abstraktion + „Backup-Ziele"-UI end-to-end **ohne OAuth**; Google Drive und OneDrive
docken danach nur noch ihren Connector an.

## Ziel & Nicht-Ziele

**Ziel:** Nutzer kann in den Einstellungen ein oder mehrere **Backup-Ziele** vom Typ „Ordner"
anlegen, ein Backup dorthin **sichern** (mit Retention), die vorhandenen Backups **auflisten** und
eines **wiederherstellen** — über denselben Preview/Confirm-Dialog wie der lokale Datei-Import.

**Nicht-Ziele (spätere Slices):** OAuth, `ITokenVault`, Google-Drive-/OneDrive-Connector,
`FlippoCloud`/eigenes Backend (C3), Delta-Sync, Konflikt-Auflösung, Resumable Uploads.

## Constraints (aus Projekt-Kontext)

- **Offline-first, opt-in:** Cloud-/Ziel-Code läuft ausschließlich nutzergetriggert — kein
  Startup-Check, kein Background-Polling.
- **`Flippo.Core` bleibt BCL-only.** `Flippo.Cloud` ist ein **neues** Projekt; in diesem Slice
  ebenfalls BCL-only (NuGet Google/MSAL erst mit den Cloud-Slices).
- **Backup-Format unverändert** — Wiederherstellen nutzt den bestehenden, getesteten
  `BackupService` (Full-Wipe-Import wie Android). Kein Format-Drift, Interop-Gate unberührt.
- Wörterbücher bleiben (wie bisher) außerhalb des Backups.

## Architektur & Projektstruktur

Neues Projekt **`Flippo.Cloud`** (BCL-only in diesem Slice):

```
Flippo.Cloud/
  Abstractions/   IBackupDestination, IDestinationConnector, BackupFileInfo,
                  DestinationConfig, BackupDestinationKind, DestinationState, DestinationException
  Destinations/   LocalFolderDestination, LocalFolderConnector
```

- Die **App** referenziert `Flippo.Cloud`.
- Die **Orchestrierung** (DB ↔ Ziel) liegt in der **App-Schicht** (`CloudBackupService`), damit
  `Flippo.Cloud` unabhängig von `Flippo.Data` bleibt.
- `Flippo.Data.BackupService` ist bereits Stream-basiert (`ExportAsync(Stream)` /
  `ParseAsync(Stream)`) → andocken ist reine Mechanik, kein Umbau.

## Kernabstraktion (`Flippo.Cloud/Abstractions`)

```csharp
enum BackupDestinationKind { LocalFolder, GoogleDrive, OneDrive, FlippoCloud }
enum DestinationState { Ready, NotConnected, Offline, QuotaExceeded, TransportFailed }

record BackupFileInfo(string RemoteId, string FileName, DateTimeOffset CreatedAt, long SizeBytes);

record DestinationConfig(Guid Id, BackupDestinationKind Kind, string DisplayName,
                         IReadOnlyDictionary<string,string> Settings);   // LocalFolder: {"folderPath": ...}

interface IBackupDestination {
    Guid DestinationId { get; }
    string DisplayName { get; }
    BackupDestinationKind Kind { get; }
    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct);
    Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct);
    Task<Stream> DownloadAsync(string remoteId, CancellationToken ct);
    Task DeleteAsync(string remoteId, CancellationToken ct);
}

interface IDestinationConnector {   // pro Kind; Auth orthogonal zum Transport
    BackupDestinationKind Kind { get; }
    IBackupDestination Create(DestinationConfig config);
}
```

- **`LocalFolderDestination`**: File-IO über `Settings["folderPath"]`. `RemoteId` = Dateiname
  (backup-Dateien liegen flach im Ordner). List = `flippo-backup-*.json` enumerieren (sortiert
  absteigend nach Name/Zeit); Upload = Stream in Datei schreiben; Download = `File.OpenRead`;
  Delete = `File.Delete`. Fehler (Pfad weg/Rechte) → `DestinationException(TransportFailed |
  NotConnected)`.
- **`LocalFolderConnector`**: `Create(config)` → `LocalFolderDestination`. Das interaktive
  „Ordner wählen" macht die App (Folder-Picker) **vor** dem Anlegen der Config — LocalFolder hat
  kein OAuth, daher kein `ConnectInteractiveAsync` im Slice (das Interface wächst mit dem
  Google-Drive-Slice um die Auth-Methode).

## Config-Persistenz

- **`destinations.json`** im Config-Verzeichnis (`AppPaths`) — Liste von `DestinationConfig`, nur
  **unsensible** Metadaten (`Id, Kind, DisplayName, Settings{folderPath}`).
- **Kein `ITokenVault` in diesem Slice** (LocalFolder hat keine Secrets). Wird mit dem
  Google-Drive-Slice eingeführt (Windows DPAPI etc.).
- App-Service **`DestinationStore`**: Laden/Speichern + CRUD über `destinations.json`.

## Orchestrierung — `CloudBackupService` (App)

Verbindet `BackupService` (Data) mit `IBackupDestination`:

- **`BackupToDestinationAsync(dest, …)`**: `ExportAsync` → `MemoryStream` →
  `dest.UploadAsync("flippo-backup-<yyyyMMdd-HHmmss>.json", stream)` → **Retention: letzte 10
  behalten** (List → älteste über N via `dest.DeleteAsync` entfernen; Client-Policy oberhalb des
  Interfaces).
- **`ListBackupsAsync(dest)`** → `dest.ListBackupsAsync`.
- **`RestoreFromDestinationAsync(dest, remoteId)`**: `dest.DownloadAsync` → `ParseAsync` →
  **derselbe Preview/Confirm-Dialog** wie der lokale Import → `ImportAsync`. Kein neuer
  Import-Pfad.

## UI-Anbindung

- **Einstellungen → „Backup-Ziele"**: Karten-Liste (Name, Typ, Pfad) + „Ordner hinzufügen"
  (Folder-Picker → `DisplayName` = Ordnername, editierbar) + „Entfernen". Diese Fläche nutzen die
  Cloud-Provider später unverändert.
- **`IFilePickerService`** bekommt `PickFolderAsync(title)` (via
  `IStorageProvider.OpenFolderPickerAsync` → `TryGetLocalPath`).
- **Sichern/Wiederherstellen** werden ziel-bewusst: Auswahl **„Datei wählen…"** (= heutiges
  Verhalten, Default) **oder** ein konfiguriertes Ziel. Umsetzung baut auf dem vorhandenen
  `SetActionsService`-Export/Import + einem Ziel-Auswahlschritt (Dropdown/kleiner Chooser, Muster
  wie `SetChooser`).
- **Fehlerzustände** nicht-blockierend: Toast/Inline; bei `TransportFailed` Angebot „Stattdessen
  lokal speichern?".

## Fehlerbehandlung

`DestinationState`-Enum mit allen vier Plan-Zuständen; im Slice real bespielt: `TransportFailed`
(Pfad/Rechte), `NotConnected` (Ordner gelöscht). `Offline`/`QuotaExceeded` sind cloud-only →
vorhanden, aber erst mit den Cloud-Slices bespielt. `DestinationException` trägt den Zustand für
die UI-Abbildung.

## Tests

- **`LocalFolderDestination`-Roundtrip** gegen Temp-Ordner: Upload → List → Download
  **byte-identisch** → Delete (der C1-Verify aus dem Plan; Fake + LocalFolder).
- **`CloudBackupService`-Retention**: nach >N Uploads bleiben die letzten N.
- **Restore-Pfad**: nutzt den schon getesteten `BackupService` (Parse/Import) — keine neue
  Import-Logik zu testen.
- Bestehende 191 Tests bleiben grün.

## Verify (manuell)

Ordner-Ziel anlegen → „Sichern" → JSON liegt im Ordner → „Wiederherstellen" → Liste → Auswahl →
Preview/Confirm → Daten wieder da. 11. Backup → ältestes wird entfernt (Retention). Ordner löschen
→ `TransportFailed`-Toast, App bleibt bedienbar.
