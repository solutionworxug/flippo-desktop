# C1 — Slice 2: Google-Drive-Backup-Ziel (Design)

**Datum:** 2026-07-08
**Kontext:** Zweiter vertikaler Durchstich der Cloud-Schicht (`docs/plan.md` §6.2), aufbauend auf
Slice 1 (LocalFolder). Fügt **Google Drive** als Backup-Ziel hinzu: OAuth-Login (Loopback-PKCE),
verschlüsselte Token-Ablage, Upload/List/Download/Delete in einem Drive-Ordner „FLIPPO". Restore
läuft weiter über den bestehenden, unveränderten `BackupService`.

## Ziel & Nicht-Ziele

**Ziel:** Nutzer verbindet in „Backup-Ziele" ein Google-Drive-Konto (Browser-OAuth), sichert
Backups in einen sichtbaren Ordner „FLIPPO" in „Meine Ablage", listet und stellt sie wieder her —
über denselben Preview/Confirm-Dialog wie lokaler Import.

**Nicht-Ziele (spätere Slices):** OneDrive (Slice 3), FlippoCloud/Backend (C3), macOS/Linux
Token-Vaults, Delta-Sync, geteilte Ordner, Team-Drives.

## Vorbedingung (erledigt)

Google-Cloud-OAuth-Client eingerichtet (Konto `solutionworxug@gmail.com`, Projekt
`flippo-desktop-501808`, Drive-API an, Consent Extern/**In Produktion**). **Client-ID**
(nicht geheim, darf in den Code):
`489948242017-o7jnrs9phnnqpqaa6cbjk15pufvm1kci.apps.googleusercontent.com`.
Details: Memory `flippo-gdrive-oauth-setup`.

## Constraints

- **`Flippo.Core` bleibt BCL-only.** `Flippo.Cloud` nimmt in diesem Slice **NuGet** auf
  (`Google.Apis.Drive.v3`, `Google.Apis.Auth`, `System.Security.Cryptography.ProtectedData`) —
  das war der Zweck des eigenen Cloud-Projekts (Slice 1 war nur zufällig BCL-only).
- **Kein Client-Secret im öffentlichen Repo.** OAuth als **Loopback-PKCE**. Ob `Google.Apis.Auth`
  ein (nicht-vertrauliches) Secret erzwingt, wird beim Bauen verifiziert; falls ja → Build-Zeit-
  Injektion einer gitignored Datei, nie committen. Client-ID darf committet werden.
- **Offline-first, opt-in:** OAuth/Drive-Calls nur nutzergetriggert. Kein Startup-Check, kein
  Polling.
- **Backup-Format unverändert**, Restore über `BackupService.ParseAsync`/`ImportAsync`.
- **Token-Vault: Windows-DPAPI only** (mit Mark entschieden), Interface offen für später.

## Architektur & Projektstruktur

```
Flippo.Cloud/
  Abstractions/   (+ ITokenVault, IInteractiveConnector)
  Destinations/   (+ GoogleDriveDestination, GoogleDriveConnector)
  Security/       WindowsDpapiTokenVault
  Auth/           GoogleOAuth (Loopback-PKCE-Flow, IDataStore→ITokenVault)
```

Orchestrierung bleibt in der App (`CloudBackupService`, `SetActionsService`) — unverändert, weil
Google Drive nur ein weiteres `IBackupDestination` liefert.

## Kernabstraktion (Erweiterungen)

```csharp
interface ITokenVault {                       // Flippo.Cloud.Abstractions
    void Store(string key, string secret);
    string? Retrieve(string key);
    void Delete(string key);
}

interface IInteractiveConnector : IDestinationConnector {
    Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct);  // null = abgebrochen
}
```

- `IDestinationConnector` (Slice 1: `Kind` + `Create`) bleibt; OAuth-Provider implementieren
  zusätzlich `IInteractiveConnector`. LocalFolder bleibt auf der Basis (Ordner-Pick ist App-UI).
- **`WindowsDpapiTokenVault`** (Flippo.Cloud/Security): verschlüsselt via `ProtectedData.Protect`
  (Scope `CurrentUser`), speichert Blob je `key` als Datei unter `AppPaths` (`tokens/{key}.bin`).
  `key` = Destination-`Id`.

## OAuth-Flow (`Auth/GoogleOAuth`)

- **Loopback-PKCE:** `GoogleAuthorizationCodeFlow`/`AuthorizationCodeInstalledApp` mit lokalem
  `http://127.0.0.1:{freier Port}`-Listener, Scope `drive.file`, `access_type=offline` +
  `prompt=consent` (damit ein Refresh-Token kommt). Client-ID aus Konstante.
- Eigener **`IDataStore` → `ITokenVault`**: das Refresh-Token landet DPAPI-verschlüsselt im Vault,
  nicht in Googles Plaintext-`FileDataStore`.
- Ergebnis: gültige `UserCredential` (auto-refresh). Bei fehlendem/abgelaufenem Refresh →
  Zustand `NotConnected` (kein Auto-Popup mitten in einer Operation).

## `GoogleDriveConnector` (IInteractiveConnector)

- `ConnectInteractiveAsync`: startet den OAuth-Flow (System-Browser), holt Token + Konto-E-Mail
  (`about.get`/`userinfo`), erzeugt eine `Guid`, legt das Token im Vault unter dieser Guid ab und
  gibt `DestinationConfig(Guid, GoogleDrive, "Google Drive ({email})", {})` zurück.
- `Create(config)`: baut `GoogleDriveDestination`, das seine `UserCredential` über den Vault
  (Key = `config.Id`) rekonstruiert.
- `DisconnectAsync`-Äquivalent: beim Entfernen des Ziels (bestehender „Entfernen"-Pfad) zusätzlich
  `vault.Delete(id)`.

## `GoogleDriveDestination` (IBackupDestination)

- **Ordner „FLIPPO"** in „Meine Ablage": per `files.list` (`name='FLIPPO' and mimeType=folder and
  trashed=false`) suchen, sonst `files.create` anlegen; Id cachen.
- `ListBackupsAsync`: `files.list` im Ordner (`name contains 'flippo-backup-'`), gemappt auf
  `BackupFileInfo(RemoteId=fileId, FileName=name, CreatedAt=createdTime, SizeBytes=size)`,
  **absteigend nach Name** (wie LocalFolder-Fix).
- `UploadAsync`: `files.create` (multipart, Parent=Ordner, mimeType `application/json`).
- `DownloadAsync`: `files.get?alt=media` → Stream.
- `DeleteAsync`: `files.delete`.
- **Fehler-Mapping:** HTTP/Netzwerk-Timeout (15 s) → `DestinationException(Offline)`;
  401/403 → `NotConnected`; 507/Quota → `QuotaExceeded`; sonst `TransportFailed`.
  `RemoteId` ist eine Drive-File-Id (kein Pfad) → keine Path-Traversal-Fläche.

## UI

- „Ordner hinzufügen" wird zu **„Ziel hinzufügen"** → kleiner Provider-Chooser (Ordner /
  Google Drive). Ordner = bisheriger Pfad; Google Drive = `ConnectInteractiveAsync` (Browser öffnet,
  Nutzer stimmt zu), dann Karte „Google Drive ({email})".
- Karte zeigt Typ + Konto; Sichern/Wiederherstellen/Entfernen wie bei LocalFolder.
- **Fehlerzustände real:** `NotConnected` → Karte mit „Neu verbinden"-CTA; `Offline` →
  nicht-blockierender Hinweis „Ziel nicht erreichbar — stattdessen lokal speichern?";
  `QuotaExceeded` → Meldung.
- Backup-Chooser: **Lokalzeit statt UTC** anzeigen (aufgeschobener Slice-1-Nit, hier eingewoben).

## Tests

- **`GoogleDriveDestination` gegen einen Fake-Drive-Client** (Abstraktion über die wenigen
  Drive-Calls, keine echten HTTP-Calls): Ordner find-or-create, Upload→List→Download byte-identisch
  →Delete, List absteigend sortiert, Fehler-Mapping (401→NotConnected, Timeout→Offline).
- **`WindowsDpapiTokenVault`**: Store→Retrieve round-trip, Delete, Retrieve-nach-Delete = null.
  (Läuft nur auf Windows — Test entsprechend `[SupportedOSPlatform]`/Skip auf non-Windows.)
- OAuth-Flow + echte Drive-API sind **nicht** unit-testbar → **ein manueller E2E** (siehe Verify).
- Bestehende 201 Tests bleiben grün.

## Verify (manuell, E2E — Plan-C1-Gate)

Ziel „Google Drive" hinzufügen → Browser-OAuth (Konto wählen, `drive.file` zustimmen) → Karte
erscheint. „Sichern" → in Drive („Meine Ablage/FLIPPO/") liegt `flippo-backup-*.json` (im Web-UI
sichtbar). „Wiederherstellen" → Liste (neuestes zuoberst) → Auswahl → Preview/Confirm → Daten
wieder da. WLAN aus → „Sichern" → nicht-blockierender „nicht erreichbar"-Hinweis. Ziel entfernen →
Token aus Vault weg (erneutes Verbinden verlangt Login).

## Bewusst NICHT im Slice

OneDrive, FlippoCloud/Backend, mac/Linux-Vaults, Ordner-Auswahl in Drive (fix „FLIPPO"),
geteilte/Team-Drives, Scope-Registrierung am Consent-Screen (drive.file ist non-sensitive).
