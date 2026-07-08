# C1 Slice 2 — Google-Drive-Backup-Ziel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nutzer verbindet in „Backup-Ziele" ein Google-Drive-Konto per Browser-OAuth (Loopback-PKCE, kein committetes Secret), sichert Backups in einen sichtbaren Ordner „FLIPPO" in „Meine Ablage", listet und stellt sie über denselben Preview/Confirm-Dialog wie lokaler Import wieder her. Refresh-Token liegt DPAPI-verschlüsselt im Token-Vault. Restore läuft weiter über den unveränderten `BackupService`.

**Architecture:** `Flippo.Cloud` nimmt in diesem Slice **NuGet** auf (`Google.Apis.Drive.v3`, `Google.Apis.Auth`, `System.Security.Cryptography.ProtectedData`) — `Flippo.Core` bleibt BCL-only. Google Drive ist nur ein weiteres `IBackupDestination`, daher bleiben `CloudBackupService` und der Restore-Pfad unverändert; `SetActionsService` bekommt in Task 6 nur die um `Offline`/`QuotaExceeded` erweiterte Fehler-Mapping-Switch (`DestErrorMessage`), damit Backup/Restore dieselben Fehlertexte zeigen wie der Connect/Reconnect-Flow im ViewModel. Neue Bausteine: `ITokenVault` + `WindowsDpapiTokenVault` (Security), `IInteractiveConnector` + `GoogleDriveConnector` (OAuth-Einstieg), `IDriveApi` + `DriveApi` (dünne Drive-Abstraktion, unit-testbar über einen Fake) + `GoogleDriveDestination`, `GoogleOAuth` (Loopback-PKCE, eigener `IDataStore` → `ITokenVault`). Die App registriert den neuen Connector in DI und erweitert die „Backup-Ziele"-UI um einen Provider-Chooser.

**Tech Stack:** C# / .NET 10, Avalonia 12, CommunityToolkit.Mvvm, xUnit. NuGet (nur `Flippo.Cloud`): `Google.Apis.Drive.v3` 1.75.0.4192, `Google.Apis.Auth` 1.75.0, `System.Security.Cryptography.ProtectedData` 10.0.9.

## Global Constraints

- `Flippo.Core` bleibt BCL-only. **NuGet ausschließlich in `Flippo.Cloud`** (drei Pakete, exakt gepinnt — s. Task 1).
- **Kein Client-Secret im öffentlichen Repo.** OAuth als **Loopback-PKCE** mit der öffentlichen Client-ID als Konstante. Ob `Google.Apis.Auth` ein (nicht-vertrauliches) Secret erzwingt, wird beim Bauen verifiziert; Fallback = gitignored Secret-Datei zur Build-Zeit (Task 5), **nie committen**.
- **Offline-first, opt-in:** OAuth-/Drive-Calls nur nutzergetriggert. Kein Startup-Check, kein Polling.
- **Backup-Format unverändert** — Restore läuft ausschließlich über den bestehenden `BackupService` (`ParseAsync`/`ImportAsync`).
- **Token-Vault: Windows-DPAPI only** (`ProtectedData`, Scope `CurrentUser`), Interface offen für später. DPAPI-Test ist Windows-guarded.
- Alle neuen UI-Strings DE **und** EN in `Strings.de.resx` + `Strings.resx`.
- Commit-Konvention: bestehende Trailer beibehalten (Implementierer hängt die Standard-Trailer des Repos an); niemals `.claude/` stagen; gezielt `git add` mit expliziten Pfaden.
- `TreatWarningsAsErrors` ist an (`Directory.Build.props`) — Build muss 0 Warnungen haben.
- **OAuth-Flow + echte Drive-API sind nicht unit-testbar** → Tasks 4/5 sind Build-/DI-only, Task 7 ist der einzige manuelle E2E.
- Erwarteter Endzustand: bestehende **201** Tests + neue Vault-/Drive-Tests alle grün.

---

## File Structure

**Neu — `Flippo.Cloud`:**
- `src/Flippo.Cloud/Abstractions/ITokenVault.cs`
- `src/Flippo.Cloud/Abstractions/IInteractiveConnector.cs`
- `src/Flippo.Cloud/Security/WindowsDpapiTokenVault.cs`
- `src/Flippo.Cloud/Destinations/IDriveApi.cs` (Abstraktion + DTO `DriveFile`)
- `src/Flippo.Cloud/Destinations/GoogleDriveDestination.cs`
- `src/Flippo.Cloud/Destinations/DriveApi.cs` (real, wrappt `DriveService`; build-only)
- `src/Flippo.Cloud/Destinations/GoogleDriveConnector.cs`
- `src/Flippo.Cloud/Auth/GoogleOAuth.cs`
- `src/Flippo.Cloud/Auth/VaultDataStore.cs`

**Geändert:**
- `src/Flippo.Cloud/Flippo.Cloud.csproj` — drei PackageReferences.
- `src/Flippo.Data/AppPaths.cs` — `TokensDirectory`.
- `src/Flippo.App/App.axaml.cs` — DI: `ITokenVault`, `GoogleDriveConnector` als zweiter `IDestinationConnector`.
- `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs` — „Ziel hinzufügen" + Provider-Chooser + Reconnect + interaktives Verbinden.
- `src/Flippo.App/Services/DestinationStore.cs` — `Remove` löscht auch Token aus dem Vault.
- `src/Flippo.App/Services/DialogService.cs` — `ShowProviderChooserAsync`.
- `src/Flippo.App/Views/BackupChooserWindow.axaml` — Lokalzeit statt UTC (aufgeschobener Slice-1-Nit).
- `src/Flippo.App/Views/SettingsView.axaml` — „Ziel hinzufügen" + `NotConnected`/`Offline`-Zustände je Karte.
- `src/Flippo.App/Views/ProviderChooserWindow.axaml(.cs)` — neuer kleiner Chooser-Dialog.
- `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx` — neue `Dest_*`-Keys.
- `.gitignore` — Fallback-Secret-Datei (Task 5).

**Tests:**
- `tests/Flippo.Tests/Cloud/WindowsDpapiTokenVaultTests.cs`
- `tests/Flippo.Tests/Cloud/GoogleDriveDestinationTests.cs` (gegen `FakeDriveApi`)

---

## Task 1: `Flippo.Cloud`-NuGet + `ITokenVault`/`IInteractiveConnector`-Abstraktion

**Files:**
- Modify: `src/Flippo.Cloud/Flippo.Cloud.csproj`
- Create: `src/Flippo.Cloud/Abstractions/ITokenVault.cs`
- Create: `src/Flippo.Cloud/Abstractions/IInteractiveConnector.cs`
- Modify: `src/Flippo.Data/AppPaths.cs`

**Interfaces:**
- Produces: `interface ITokenVault { void Store(string,string); string? Retrieve(string); void Delete(string); }`, `interface IInteractiveConnector : IDestinationConnector { Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct = default); }`, `AppPaths.TokensDirectory`.

- [ ] **Step 1: NuGet-Pakete in `Flippo.Cloud` aufnehmen**

Replace `src/Flippo.Cloud/Flippo.Cloud.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Flippo.Cloud</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Apis.Drive.v3" Version="1.75.0.4192" />
    <PackageReference Include="Google.Apis.Auth" Version="1.75.0" />
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.9" />
  </ItemGroup>
</Project>
```

(Versionen sind stabile Stände vom Juni 2026; `Google.Apis.Auth` ist eine transitive Abhängigkeit von `Google.Apis.Drive.v3`, wird aber explizit gepinnt, weil `GoogleOAuth` direkt darauf baut.)

- [ ] **Step 2: `ITokenVault` schreiben**

Create `src/Flippo.Cloud/Abstractions/ITokenVault.cs`:

```csharp
namespace Flippo.Cloud.Abstractions;

/// <summary>
/// Verschlüsselte Ablage sensibler Strings (v.a. OAuth-Refresh-Tokens), ein Blob je Schlüssel.
/// Windows-DPAPI-Impl in diesem Slice; Interface offen für spätere Plattformen (C1 Nicht-Ziel).
/// </summary>
public interface ITokenVault
{
    /// <summary>Speichert (oder ersetzt) das Geheimnis unter <paramref name="key"/>.</summary>
    void Store(string key, string secret);

    /// <summary>Liest das Geheimnis; <c>null</c>, wenn nicht vorhanden oder unlesbar.</summary>
    string? Retrieve(string key);

    /// <summary>Löscht das Geheimnis (idempotent — kein Fehler, wenn nicht vorhanden).</summary>
    void Delete(string key);
}
```

- [ ] **Step 3: `IInteractiveConnector` schreiben**

Create `src/Flippo.Cloud/Abstractions/IInteractiveConnector.cs`:

```csharp
namespace Flippo.Cloud.Abstractions;

/// <summary>
/// Erweitert <see cref="IDestinationConnector"/> um interaktives Verbinden (OAuth im System-Browser).
/// LocalFolder bleibt auf der Basis (Ordner-Pick ist App-UI); Cloud-Provider implementieren dies.
/// </summary>
public interface IInteractiveConnector : IDestinationConnector
{
    /// <summary>Startet den interaktiven Verbinde-Flow und liefert eine persistierbare Config.
    /// <c>null</c> = vom Nutzer abgebrochen.</summary>
    Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: `AppPaths.TokensDirectory` ergänzen**

In `src/Flippo.Data/AppPaths.cs` nach der `BackupsDirectory`-Zeile einfügen:

```csharp
    public static string TokensDirectory => Path.Combine(DataDirectory, "tokens");
```

Und in `EnsureDirectories()` (nach `Directory.CreateDirectory(BackupsDirectory);`) ergänzen:

```csharp
        Directory.CreateDirectory(TokensDirectory);
```

- [ ] **Step 5: Build verifizieren (NuGet-Restore + 0 Warnungen)**

Run: `dotnet build src/Flippo.Cloud/Flippo.Cloud.csproj -c Debug`
Expected: `Der Buildvorgang wurde erfolgreich ausgeführt.` — NuGet-Pakete werden restauriert, 0 Warnungen, 0 Fehler.

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.Cloud/Flippo.Cloud.csproj src/Flippo.Cloud/Abstractions/ITokenVault.cs src/Flippo.Cloud/Abstractions/IInteractiveConnector.cs src/Flippo.Data/AppPaths.cs
git commit -m "C1: Flippo.Cloud-NuGet (Drive/Auth/DPAPI) + ITokenVault/IInteractiveConnector"
```

---

## Task 2: `WindowsDpapiTokenVault` (Store/Retrieve/Delete, TDD)

**Files:**
- Create: `src/Flippo.Cloud/Security/WindowsDpapiTokenVault.cs`
- Test: `tests/Flippo.Tests/Cloud/WindowsDpapiTokenVaultTests.cs`

**Interfaces:**
- Consumes: `ITokenVault`, `AppPaths.TokensDirectory` (indirekt über injizierten Ordner).
- Produces: `WindowsDpapiTokenVault(string? directory = null)` — ein DPAPI-Blob je `key` als Datei `{directory}/{key}.bin`.

- [ ] **Step 1: Failing test schreiben**

Der Test ist Windows-only (DPAPI existiert nur dort). Wir prüfen zur Laufzeit mit `OperatingSystem.IsWindows()` und **skippen** via `Assert.Skip` (xUnit 2.9.3 unterstützt `Assert.Skip`), damit die Suite auf CI/non-Windows grün bleibt statt rot.

Create `tests/Flippo.Tests/Cloud/WindowsDpapiTokenVaultTests.cs`:

```csharp
using Flippo.Cloud.Security;

namespace Flippo.Tests.Cloud;

public class WindowsDpapiTokenVaultTests
{
    private static WindowsDpapiTokenVault NewVault(out string dir)
    {
        dir = Directory.CreateTempSubdirectory().FullName;
        return new WindowsDpapiTokenVault(dir);
    }

    [Fact]
    public void Store_Then_Retrieve_RoundTrips()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("DPAPI ist Windows-only."); return; }
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "refresh-token-secret-äöü");
            Assert.Equal("refresh-token-secret-äöü", vault.Retrieve(key));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Retrieve_UnknownKey_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("DPAPI ist Windows-only."); return; }
        var vault = NewVault(out var dir);
        try { Assert.Null(vault.Retrieve("does-not-exist")); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_RemovesSecret_RetrieveThenNull()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("DPAPI ist Windows-only."); return; }
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "x");
            vault.Delete(key);
            Assert.Null(vault.Retrieve(key));
            vault.Delete(key);   // idempotent — kein Wurf
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Store_Twice_Overwrites()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("DPAPI ist Windows-only."); return; }
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "first");
            vault.Store(key, "second");
            Assert.Equal("second", vault.Retrieve(key));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void StoredBlob_IsNotPlaintext()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("DPAPI ist Windows-only."); return; }
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "SUPER-SECRET-MARKER");
            var raw = File.ReadAllText(Path.Combine(dir, key + ".bin"));
            Assert.DoesNotContain("SUPER-SECRET-MARKER", raw);
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 2: Test läuft NICHT (fehlender Typ)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~WindowsDpapiTokenVault"`
Expected: Kompilierfehler `WindowsDpapiTokenVault` nicht gefunden.

- [ ] **Step 3: `WindowsDpapiTokenVault` implementieren**

Create `src/Flippo.Cloud/Security/WindowsDpapiTokenVault.cs`:

```csharp
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Data;

namespace Flippo.Cloud.Security;

/// <summary>
/// <see cref="ITokenVault"/> auf Basis von Windows-DPAPI (<see cref="ProtectedData"/>, Scope
/// <see cref="DataProtectionScope.CurrentUser"/>). Ein verschlüsselter Blob je Schlüssel als Datei
/// <c>{TokensDirectory}/{key}.bin</c> (Base64). Nur auf Windows lauffähig — die App läuft in
/// diesem Slice ausschließlich unter Windows (Velopack-Ziel).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiTokenVault : ITokenVault
{
    private readonly string _directory;

    /// <param name="directory">Ablage-Verzeichnis; Standard = <see cref="AppPaths.TokensDirectory"/>.</param>
    public WindowsDpapiTokenVault(string? directory = null)
        => _directory = directory ?? AppPaths.TokensDirectory;

    public void Store(string key, string secret)
    {
        Directory.CreateDirectory(_directory);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllText(PathFor(key), Convert.ToBase64String(cipher));
    }

    public string? Retrieve(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        try
        {
            var cipher = Convert.FromBase64String(File.ReadAllText(path));
            var plain = ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is IOException or FormatException or CryptographicException)
        {
            return null;   // beschädigt/unlesbar → wie „nicht vorhanden" behandeln (neu verbinden nötig)
        }
    }

    public void Delete(string key)
    {
        try { File.Delete(PathFor(key)); }
        catch (IOException) { /* idempotent — best effort */ }
    }

    private string PathFor(string key) => Path.Combine(_directory, key + ".bin");
}
```

- [ ] **Step 4: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~WindowsDpapiTokenVault"`
Expected: PASS — auf Windows 5 Tests bestanden; auf non-Windows 5 übersprungen (skipped), 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.Cloud/Security/WindowsDpapiTokenVault.cs tests/Flippo.Tests/Cloud/WindowsDpapiTokenVaultTests.cs
git commit -m "C1: WindowsDpapiTokenVault (DPAPI-verschlüsselte Token-Ablage) + TDD"
```

---

## Task 3: `IDriveApi`-Abstraktion + `GoogleDriveDestination` (TDD gegen Fake)

**Files:**
- Create: `src/Flippo.Cloud/Destinations/IDriveApi.cs`
- Create: `src/Flippo.Cloud/Destinations/GoogleDriveDestination.cs`
- Test: `tests/Flippo.Tests/Cloud/GoogleDriveDestinationTests.cs`

**Interfaces:**
- Consumes: `IBackupDestination`, `DestinationConfig`, `BackupFileInfo`, `DestinationException`, `DestinationState`.
- Produces: `interface IDriveApi` (5 Ops), `record DriveFile(...)`, `enum DriveErrorKind`, `class DriveApiException`, `GoogleDriveDestination(DestinationConfig config, IDriveApi api)`.

Die Abstraktion `IDriveApi` kapselt die ~5 Drive-Operationen als **rohe** Calls; sie kennt keine `DestinationException` (das ist Transport-neutrale Cloud-Abstraktion). Sie wirft eine eigene `DriveApiException(DriveErrorKind)`, die `GoogleDriveDestination` auf `DestinationState` mappt. So sind Ordner-Find/Create, Roundtrip und Fehler-Mapping ohne HTTP/OAuth testbar.

- [ ] **Step 1: Failing test schreiben (inkl. `FakeDriveApi`)**

Create `tests/Flippo.Tests/Cloud/GoogleDriveDestinationTests.cs`:

```csharp
using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class GoogleDriveDestinationTests
{
    // In-Memory-Fake des Drive-Backends: ein Ordner „FLIPPO" + flache Dateiliste darin.
    private sealed class FakeDriveApi : IDriveApi
    {
        private readonly Dictionary<string, (string Name, byte[] Data, DateTimeOffset Created)> _files = new();
        private string? _folderId;
        private int _seq;

        public DriveErrorKind? FailFindFolder;
        public DriveErrorKind? FailUpload;

        public Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct)
        {
            if (FailFindFolder is { } k) throw new DriveApiException(k, "find-folder failed");
            _folderId ??= "folder-" + name;
            return Task.FromResult(_folderId);
        }

        public Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct)
        {
            IReadOnlyList<DriveFile> list = _files
                .Where(kv => kv.Value.Name.Contains(nameContains, StringComparison.Ordinal))
                .Select(kv => new DriveFile(kv.Key, kv.Value.Name, kv.Value.Created, kv.Value.Data.Length))
                .ToList();
            return Task.FromResult(list);
        }

        public Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct)
        {
            if (FailUpload is { } k) throw new DriveApiException(k, "upload failed");
            using var mem = new MemoryStream();
            content.CopyTo(mem);
            var data = mem.ToArray();
            var id = "file-" + (++_seq);
            var created = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + _seq);
            _files[id] = (fileName, data, created);
            return Task.FromResult(new DriveFile(id, fileName, created, data.Length));
        }

        public Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
            => Task.FromResult<Stream>(new MemoryStream(_files[fileId].Data, writable: false));

        public Task DeleteAsync(string fileId, CancellationToken ct)
        {
            _files.Remove(fileId);
            return Task.CompletedTask;
        }
    }

    private static GoogleDriveDestination NewDest(IDriveApi api) =>
        new(new DestinationConfig(Guid.NewGuid(), BackupDestinationKind.GoogleDrive, "Google Drive (t@x)",
            new Dictionary<string, string>()), api);

    [Fact]
    public async Task Upload_List_Download_Delete_Roundtrip_ByteIdentical()
    {
        var dest = NewDest(new FakeDriveApi());
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");

        var info = await dest.UploadAsync("flippo-backup-20260707-120000.json", new MemoryStream(payload));

        var list = await dest.ListBackupsAsync();
        Assert.Single(list);
        Assert.Equal(info.RemoteId, list[0].RemoteId);
        Assert.Equal(payload.Length, list[0].SizeBytes);

        await using (var download = await dest.DownloadAsync(info.RemoteId))
        {
            using var mem = new MemoryStream();
            await download.CopyToAsync(mem);
            Assert.Equal(payload, mem.ToArray());
        }

        await dest.DeleteAsync(info.RemoteId);
        Assert.Empty(await dest.ListBackupsAsync());
    }

    [Fact]
    public async Task List_ReturnsNewestFirst_ByNameDescending()
    {
        var dest = NewDest(new FakeDriveApi());
        await dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1]));
        await dest.UploadAsync("flippo-backup-20260102-000000.json", new MemoryStream([2]));

        var result = await dest.ListBackupsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("flippo-backup-20260102-000000.json", result[0].FileName);
        Assert.Equal("flippo-backup-20260101-000000.json", result[1].FileName);
    }

    [Fact]
    public async Task Unauthorized_MapsTo_NotConnected()
    {
        var dest = NewDest(new FakeDriveApi { FailFindFolder = DriveErrorKind.Unauthorized });
        var ex = await Assert.ThrowsAsync<DestinationException>(() => dest.ListBackupsAsync());
        Assert.Equal(DestinationState.NotConnected, ex.State);
    }

    [Fact]
    public async Task Timeout_MapsTo_Offline()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.Timeout });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.Offline, ex.State);
    }

    [Fact]
    public async Task Quota_MapsTo_QuotaExceeded()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.QuotaExceeded });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.QuotaExceeded, ex.State);
    }

    [Fact]
    public async Task OtherError_MapsTo_TransportFailed()
    {
        var dest = NewDest(new FakeDriveApi { FailUpload = DriveErrorKind.Other });
        var ex = await Assert.ThrowsAsync<DestinationException>(
            () => dest.UploadAsync("flippo-backup-20260101-000000.json", new MemoryStream([1])));
        Assert.Equal(DestinationState.TransportFailed, ex.State);
    }
}
```

- [ ] **Step 2: Test läuft NICHT (fehlende Typen)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~GoogleDriveDestination"`
Expected: Kompilierfehler `IDriveApi`/`DriveFile`/`DriveErrorKind`/`DriveApiException`/`GoogleDriveDestination` nicht gefunden.

- [ ] **Step 3: `IDriveApi`-Abstraktion + DTO/Fehler schreiben**

Create `src/Flippo.Cloud/Destinations/IDriveApi.cs`:

```csharp
namespace Flippo.Cloud.Destinations;

/// <summary>Eine Datei im Drive-Ordner (roh, transport-neutral).</summary>
public sealed record DriveFile(string Id, string Name, DateTimeOffset CreatedAt, long SizeBytes);

/// <summary>Klassifiziert Drive-Transport-Fehler; wird von <c>GoogleDriveDestination</c> auf
/// <c>DestinationState</c> gemappt.</summary>
public enum DriveErrorKind { Unauthorized, Timeout, QuotaExceeded, Other }

/// <summary>Transport-Fehler der Drive-Ebene (kein UI-Zustand — das macht die Destination).</summary>
public sealed class DriveApiException : Exception
{
    public DriveErrorKind Kind { get; }
    public DriveApiException(DriveErrorKind kind, string message, Exception? inner = null)
        : base(message, inner) => Kind = kind;
}

/// <summary>
/// Dünne Abstraktion über die wenigen Drive-v3-Operationen, die dieses Slice braucht. Der reale
/// Wrapper (<c>DriveApi</c>) kapselt <c>DriveService</c>; Tests nutzen einen In-Memory-Fake, sodass
/// <c>GoogleDriveDestination</c> ohne echtes HTTP/OAuth getestet werden kann.
/// </summary>
public interface IDriveApi
{
    /// <summary>Sucht den Ordner (<c>name=... and mimeType=folder and trashed=false</c>) in „Meine
    /// Ablage" und legt ihn sonst an. Liefert die Ordner-Id.</summary>
    Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct);

    /// <summary>Listet Dateien im Ordner, deren Name <paramref name="nameContains"/> enthält.</summary>
    Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct);

    /// <summary>Lädt einen Stream als neue JSON-Datei in den Ordner hoch.</summary>
    Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct);

    /// <summary>Lädt den Dateiinhalt (<c>alt=media</c>) als Stream herunter.</summary>
    Task<Stream> DownloadAsync(string fileId, CancellationToken ct);

    /// <summary>Löscht die Datei.</summary>
    Task DeleteAsync(string fileId, CancellationToken ct);
}
```

- [ ] **Step 4: `GoogleDriveDestination` implementieren**

Create `src/Flippo.Cloud/Destinations/GoogleDriveDestination.cs`:

```csharp
using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Backup-Ziel auf Google Drive: alle Backups liegen flach im Ordner „FLIPPO" in „Meine Ablage".
/// <c>RemoteId</c> = Drive-File-Id (kein Pfad → keine Path-Traversal-Fläche). Der Transport läuft
/// über <see cref="IDriveApi"/>; Drive-Fehler werden auf <see cref="DestinationState"/> gemappt.
/// </summary>
public sealed class GoogleDriveDestination : IBackupDestination
{
    /// <summary>Fester Ordnername in „Meine Ablage" (kein Ordner-Picker in diesem Slice).</summary>
    public const string FolderName = "FLIPPO";
    private const string FilePrefix = "flippo-backup-";

    private readonly IDriveApi _api;
    private string? _folderId;

    public GoogleDriveDestination(DestinationConfig config, IDriveApi api)
    {
        DestinationId = config.Id;
        DisplayName = config.DisplayName;
        _api = api;
    }

    public Guid DestinationId { get; }
    public string DisplayName { get; }
    public BackupDestinationKind Kind => BackupDestinationKind.GoogleDrive;

    public async Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        return await MapAsync(async () =>
        {
            var folderId = await EnsureFolderAsync(ct);
            var files = await _api.ListFilesAsync(folderId, FilePrefix, ct);
            // Zeitstempel im Namen (lexikografisch = chronologisch), absteigend → neuestes zuerst
            // (gleiche Konvention wie LocalFolderDestination und CloudBackupService.PruneAsync).
            IReadOnlyList<BackupFileInfo> result = files
                .Select(f => new BackupFileInfo(f.Id, f.Name, f.CreatedAt, f.SizeBytes))
                .OrderByDescending(b => b.FileName, StringComparer.Ordinal)
                .ToList();
            return result;
        });
    }

    public async Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        return await MapAsync(async () =>
        {
            var folderId = await EnsureFolderAsync(ct);
            var f = await _api.UploadAsync(folderId, fileName, content, ct);
            return new BackupFileInfo(f.Id, f.Name, f.CreatedAt, f.SizeBytes);
        });
    }

    public async Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
        => await MapAsync(() => _api.DownloadAsync(remoteId, ct));

    public async Task DeleteAsync(string remoteId, CancellationToken ct = default)
        => await MapAsync(async () => { await _api.DeleteAsync(remoteId, ct); return true; });

    private async Task<string> EnsureFolderAsync(CancellationToken ct)
        => _folderId ??= await _api.FindOrCreateFolderAsync(FolderName, ct);

    /// <summary>Führt eine Drive-Operation aus und übersetzt <see cref="DriveApiException"/> in eine
    /// <see cref="DestinationException"/> mit UI-Zustand.</summary>
    private static async Task<T> MapAsync<T>(Func<Task<T>> op)
    {
        try
        {
            return await op();
        }
        catch (DriveApiException ex)
        {
            var state = ex.Kind switch
            {
                DriveErrorKind.Unauthorized => DestinationState.NotConnected,
                DriveErrorKind.Timeout => DestinationState.Offline,
                DriveErrorKind.QuotaExceeded => DestinationState.QuotaExceeded,
                _ => DestinationState.TransportFailed
            };
            throw new DestinationException(state, ex.Message, ex);
        }
    }
}
```

- [ ] **Step 5: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~GoogleDriveDestination"`
Expected: PASS (6 Tests).

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.Cloud/Destinations/IDriveApi.cs src/Flippo.Cloud/Destinations/GoogleDriveDestination.cs tests/Flippo.Tests/Cloud/GoogleDriveDestinationTests.cs
git commit -m "C1: IDriveApi-Abstraktion + GoogleDriveDestination (Roundtrip + Fehler-Mapping, TDD gegen Fake)"
```

---

## Task 4: Realer `DriveApi` (wrappt `DriveService`, build-only)

**Files:**
- Create: `src/Flippo.Cloud/Destinations/DriveApi.cs`

**Interfaces:**
- Consumes: `IDriveApi`, `Google.Apis.Drive.v3.DriveService`, `Google.Apis.Auth.OAuth2.UserCredential`.
- Produces: `DriveApi(UserCredential credential)` — realer Wrapper mit Timeout-/Fehler-Übersetzung nach `DriveApiException`.

> **Hinweis:** Nicht unit-testbar (braucht echtes HTTP/OAuth) → nur Build-Gate. Die Logik ist bewusst dünn: reine Drive-Calls + Übersetzung von `GoogleApiException`/Timeout in `DriveApiException`. Verifikation der echten Calls passiert im E2E (Task 7).

- [ ] **Step 1: `DriveApi` schreiben**

Create `src/Flippo.Cloud/Destinations/DriveApi.cs`:

```csharp
using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Realer <see cref="IDriveApi"/> über <see cref="DriveService"/>. Übersetzt Google-/Netzwerk-Fehler
/// in <see cref="DriveApiException"/> (die Destination mappt weiter auf UI-Zustände). Scope
/// <c>drive.file</c> genügt: FLIPPO sieht nur selbst erzeugte Dateien/Ordner.
/// </summary>
public sealed class DriveApi : IDriveApi, IDisposable
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string JsonMimeType = "application/json";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly DriveService _service;

    public DriveApi(UserCredential credential)
    {
        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FLIPPO"
        });
        _service.HttpClient.Timeout = Timeout;
    }

    public async Task<string> FindOrCreateFolderAsync(string name, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var list = _service.Files.List();
            list.Q = $"name = '{Escape(name)}' and mimeType = '{FolderMimeType}' and trashed = false";
            list.Fields = "files(id)";
            list.Spaces = "drive";
            var found = await list.ExecuteAsync(ct);
            if (found.Files is { Count: > 0 }) return found.Files[0].Id;

            var create = _service.Files.Create(new File { Name = name, MimeType = FolderMimeType });
            create.Fields = "id";
            var created = await create.ExecuteAsync(ct);
            return created.Id;
        });
    }

    public async Task<IReadOnlyList<DriveFile>> ListFilesAsync(string folderId, string nameContains, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var list = _service.Files.List();
            list.Q = $"'{Escape(folderId)}' in parents and name contains '{Escape(nameContains)}' and trashed = false";
            list.Fields = "files(id,name,createdTime,size)";
            list.Spaces = "drive";
            var res = await list.ExecuteAsync(ct);
            IReadOnlyList<DriveFile> mapped = (res.Files ?? new List<File>())
                .Select(ToDriveFile)
                .ToList();
            return mapped;
        });
    }

    public async Task<DriveFile> UploadAsync(string folderId, string fileName, Stream content, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var meta = new File { Name = fileName, Parents = new[] { folderId } };
            var request = _service.Files.Create(meta, content, JsonMimeType);
            request.Fields = "id,name,createdTime,size";
            var progress = await request.UploadAsync(ct);
            if (progress.Exception is not null) throw progress.Exception;
            return ToDriveFile(request.ResponseBody);
        });
    }

    public async Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
    {
        return await GuardAsync(async () =>
        {
            var mem = new MemoryStream();
            await _service.Files.Get(fileId).DownloadAsync(mem, ct);
            mem.Position = 0;
            return (Stream)mem;
        });
    }

    public async Task DeleteAsync(string fileId, CancellationToken ct)
        => await GuardAsync(async () => { await _service.Files.Delete(fileId).ExecuteAsync(ct); return true; });

    public void Dispose() => _service.Dispose();

    private static DriveFile ToDriveFile(File f) => new(
        f.Id,
        f.Name,
        f.CreatedTimeDateTimeOffset ?? DateTimeOffset.UnixEpoch,
        f.Size ?? 0);

    /// <summary>Google-Query-Escaping: einfache Anführungszeichen und Backslashes maskieren.</summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    /// <summary>Übersetzt Google-/Netzwerk-/Timeout-Fehler in eine klassifizierte
    /// <see cref="DriveApiException"/>.</summary>
    private static async Task<T> GuardAsync<T>(Func<Task<T>> op)
    {
        try
        {
            return await op();
        }
        catch (GoogleApiException ex)
        {
            var kind = ex.HttpStatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => DriveErrorKind.Unauthorized,
                HttpStatusCode.InsufficientStorage => DriveErrorKind.QuotaExceeded,
                (HttpStatusCode)429 => DriveErrorKind.QuotaExceeded,
                _ => DriveErrorKind.Other
            };
            throw new DriveApiException(kind, ex.Message, ex);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException or HttpRequestException)
        {
            throw new DriveApiException(DriveErrorKind.Timeout, ex.Message, ex);
        }
    }
}
```

> **Implementierer-Verify:** `Google.Apis.Auth` 1.75.0 exponiert `File.CreatedTimeDateTimeOffset` (neuere DTO-Property statt des veralteten `CreatedTime`-`DateTime?`). Falls die installierte Version diese Property nicht kennt, `f.CreatedTimeRaw` per `DateTimeOffset.Parse(..., CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)` parsen. Gleiches gilt für `429` als `HttpStatusCode`-Cast, falls das Enum den Namen `TooManyRequests` bereitstellt — dann den Namen verwenden.

- [ ] **Step 2: Build verifizieren (0 Warnungen)**

Run: `dotnet build src/Flippo.Cloud/Flippo.Cloud.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler. Falls DTO-Property-Namen abweichen → gemäß Implementierer-Verify oben korrigieren, bis grün.

- [ ] **Step 3: Commit**

```bash
git add src/Flippo.Cloud/Destinations/DriveApi.cs
git commit -m "C1: realer DriveApi (DriveService-Wrapper, drive.file, 15s-Timeout, Fehler-Mapping)"
```

---

## Task 5: `GoogleOAuth` (Loopback-PKCE) + `GoogleDriveConnector` (build + manual-verify, DI)

**Files:**
- Create: `src/Flippo.Cloud/Auth/VaultDataStore.cs`
- Create: `src/Flippo.Cloud/Auth/GoogleOAuth.cs`
- Create: `src/Flippo.Cloud/Destinations/GoogleDriveConnector.cs`
- Modify: `src/Flippo.App/App.axaml.cs` (DI)
- Modify: `.gitignore` (Fallback-Secret-Datei)

**Interfaces:**
- Consumes: `ITokenVault`, `IInteractiveConnector`, `DestinationConfig`, `IBackupDestination`, `GoogleAuthorizationCodeFlow`, `AuthorizationCodeInstalledApp`, `LocalServerCodeReceiver`, `UserCredential`, `DriveApi`.
- Produces: `VaultDataStore(ITokenVault, string keyPrefix)` (custom `IDataStore`), `GoogleOAuth` (Konstante `ClientId`, `AuthorizeAsync`/`LoadAsync`/`RevokeAsync`), `GoogleDriveConnector(ITokenVault) : IInteractiveConnector`.

> **Hinweis:** OAuth braucht echten Browser + Google-Login → **nicht unit-testbar** (Spec §Tests). Dieser Task ist Build- + DI-Gate; die Funktion wird in Task 7 (E2E) verifiziert.

- [ ] **Step 1: `VaultDataStore` (custom `IDataStore` → `ITokenVault`) schreiben**

Google speichert das Token über `IDataStore` (Default: Plaintext-`FileDataStore`). Wir leiten stattdessen auf den DPAPI-Vault um. Das gespeicherte Objekt ist `TokenResponse` (JSON-serialisierbar) — wir serialisieren es als String in den Vault.

Create `src/Flippo.Cloud/Auth/VaultDataStore.cs`:

```csharp
using System.Text.Json;
using Flippo.Cloud.Abstractions;
using Google.Apis.Util.Store;

namespace Flippo.Cloud.Auth;

/// <summary>
/// <see cref="IDataStore"/>-Adapter, der Googles Token-Ablage in den <see cref="ITokenVault"/>
/// (DPAPI) umleitet — das Refresh-Token landet verschlüsselt, nicht in Googles Plaintext-FileDataStore.
/// Ein Vault-Schlüssel je (Prefix, Google-Key): <c>{prefix}:{key}</c>.
/// </summary>
public sealed class VaultDataStore : IDataStore
{
    private readonly ITokenVault _vault;
    private readonly string _prefix;

    public VaultDataStore(ITokenVault vault, string keyPrefix)
    {
        _vault = vault;
        _prefix = keyPrefix;
    }

    public Task StoreAsync<T>(string key, T value)
    {
        _vault.Store(VaultKey(key), JsonSerializer.Serialize(value));
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var json = _vault.Retrieve(VaultKey(key));
        var value = json is null ? default! : JsonSerializer.Deserialize<T>(json)!;
        return Task.FromResult(value);
    }

    public Task DeleteAsync<T>(string key)
    {
        _vault.Delete(VaultKey(key));
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        // Nur der eigene Prefix-Schlüssel ist relevant; ClearAsync wird von unserem Flow nicht genutzt.
        _vault.Delete(VaultKey("user"));
        return Task.CompletedTask;
    }

    private string VaultKey(string key) => $"{_prefix}:{key}";
}
```

- [ ] **Step 2: `GoogleOAuth` (Loopback-PKCE) schreiben**

Create `src/Flippo.Cloud/Auth/GoogleOAuth.cs`:

```csharp
using Flippo.Cloud.Abstractions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;

namespace Flippo.Cloud.Auth;

/// <summary>
/// Google-OAuth per Loopback-PKCE für den Desktop-Client (public client → kein vertrauliches Secret
/// beim Token-Tausch). Scope <c>drive.file</c>, <c>access_type=offline</c> + <c>prompt=consent</c>
/// (erzwingt ein Refresh-Token). Token liegt via <see cref="VaultDataStore"/> DPAPI-verschlüsselt.
/// </summary>
public sealed class GoogleOAuth
{
    /// <summary>Öffentliche Desktop-Client-ID (kein Geheimnis — darf committet werden).</summary>
    public const string ClientId = "489948242017-o7jnrs9phnnqpqaa6cbjk15pufvm1kci.apps.googleusercontent.com";

    /// <summary>Bei Desktop-PKCE nicht vertraulich. Falls die Bibliothek ein nicht-leeres Secret
    /// erzwingt (s. Fallback im Plan-Task), wird dieser Wert zur Build-Zeit aus einer gitignored
    /// Datei injiziert und bleibt sonst leer.</summary>
    private const string ClientSecret = "";

    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

    private readonly ITokenVault _vault;

    public GoogleOAuth(ITokenVault vault) => _vault = vault;

    /// <summary>Startet den interaktiven Flow (System-Browser + Loopback-Listener) und liefert eine
    /// gültige <see cref="UserCredential"/> (auto-refresh). Der Vault-Prefix ist die Destination-Id.</summary>
    public Task<UserCredential> AuthorizeAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var flow = CreateFlow(vaultKeyPrefix);
        var app = new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver());
        return app.AuthorizeAsync("user", ct);
    }

    /// <summary>Rekonstruiert eine <see cref="UserCredential"/> aus dem gespeicherten Refresh-Token
    /// (kein Browser). <c>null</c>, wenn kein/ungültiges Token vorliegt → Zustand NotConnected.</summary>
    public async Task<UserCredential?> LoadAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var flow = CreateFlow(vaultKeyPrefix);
        var token = await flow.LoadTokenAsync("user", ct);
        if (token is null || string.IsNullOrEmpty(token.RefreshToken)) return null;
        return new UserCredential(flow, "user", token);
    }

    /// <summary>Widerruft (best effort) und löscht das Token aus dem Vault (beim Entfernen des Ziels).</summary>
    public async Task RevokeAsync(string vaultKeyPrefix, CancellationToken ct)
    {
        var cred = await LoadAsync(vaultKeyPrefix, ct);
        if (cred is not null)
        {
            try { await cred.RevokeTokenAsync(ct); } catch { /* offline/abgelaufen → egal */ }
        }
        await CreateFlow(vaultKeyPrefix).DataStore.DeleteAsync<TokenResponse>("user");
    }

    private GoogleAuthorizationCodeFlow CreateFlow(string vaultKeyPrefix) => new(
        new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = ClientId, ClientSecret = ClientSecret },
            Scopes = Scopes,
            DataStore = new VaultDataStore(_vault, vaultKeyPrefix),
            // Refresh-Token erzwingen (sonst kommt es nur beim allerersten Consent):
            Prompt = "consent"
        });
}
```

> **Implementierer-Verify (kritischer Punkt):**
> 1. Baut `GoogleAuthorizationCodeFlow` mit leerem `ClientSecret` (Desktop-PKCE)? `Google.Apis.Auth` 1.75.0 unterstützt PKCE für Installed-Apps; ein leeres Secret ist bei Desktop-Clients erlaubt. **Falls** der Token-Tausch ein nicht-leeres Secret verlangt (400 `invalid_request`), Fallback ausführen (Step 5).
> 2. `GoogleAuthorizationCodeFlow.Initializer` hat in 1.75.0 die Eigenschaft `Prompt` (String). Falls nicht vorhanden, stattdessen im `Initializer` `UserDefinedQueryParams` mit `("access_type","offline")` + `("prompt","consent")` setzen — beides ist dokumentiert. `access_type=offline` ist für Installed-Apps Default; `prompt=consent` ist der Refresh-Token-Erzwinger.

- [ ] **Step 3: `GoogleDriveConnector` schreiben**

Create `src/Flippo.Cloud/Destinations/GoogleDriveConnector.cs`:

```csharp
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace Flippo.Cloud.Destinations;

/// <summary>
/// Verbindet ein Google-Drive-Konto (interaktiv, Browser-OAuth) und baut daraus ein
/// <see cref="GoogleDriveDestination"/>. Der Vault-Schlüssel-Prefix ist die Destination-Id, sodass
/// jedes Ziel sein eigenes Refresh-Token hat.
/// </summary>
public sealed class GoogleDriveConnector : IInteractiveConnector
{
    private readonly ITokenVault _vault;
    private readonly GoogleOAuth _oauth;

    public GoogleDriveConnector(ITokenVault vault)
    {
        _vault = vault;
        _oauth = new GoogleOAuth(vault);
    }

    public BackupDestinationKind Kind => BackupDestinationKind.GoogleDrive;

    public IBackupDestination Create(DestinationConfig config)
    {
        var cred = _oauth.LoadAsync(config.Id.ToString("N"), CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new DestinationException(DestinationState.NotConnected,
                "Kein gültiges Google-Token — bitte neu verbinden.");
        return new GoogleDriveDestination(config, new DriveApi(cred));
    }

    public async Task<DestinationConfig?> ConnectInteractiveAsync(CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var prefix = id.ToString("N");

        UserCredential cred;
        try
        {
            cred = await _oauth.AuthorizeAsync(prefix, ct);
        }
        catch (OperationCanceledException)
        {
            _vault.Delete($"{prefix}:user");   // Teil-Token aufräumen
            return null;                         // Nutzer hat abgebrochen
        }

        var email = await FetchEmailAsync(cred, ct);
        return new DestinationConfig(id, BackupDestinationKind.GoogleDrive,
            $"Google Drive ({email})", new Dictionary<string, string>());
    }

    /// <summary>Konto-E-Mail über <c>about.get(fields=user)</c> — drive.file genügt dafür.</summary>
    private static async Task<string> FetchEmailAsync(UserCredential cred, CancellationToken ct)
    {
        using var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "FLIPPO"
        });
        var about = service.About.Get();
        about.Fields = "user(emailAddress)";
        try
        {
            var res = await about.ExecuteAsync(ct);
            return res.User?.EmailAddress ?? "unbekannt";
        }
        catch
        {
            return "unbekannt";   // Anzeigename-Nice-to-have; Verbindung steht trotzdem
        }
    }
}
```

> **Implementierer-Verify:** `DriveService.About.Get()` mit Feld `user(emailAddress)` funktioniert unter `drive.file`. Falls die installierte DTO-Property anders heißt (`res.User.EmailAddress`), entsprechend anpassen.

- [ ] **Step 4: DI-Registrierung**

In `src/Flippo.App/App.axaml.cs` in `ConfigureServices` **direkt nach** `services.AddSingleton<IDestinationConnector, LocalFolderConnector>();` ergänzen (Usings oben stehen bereits: `Flippo.Cloud.Abstractions`, `Flippo.Cloud.Destinations`; zusätzlich `using Flippo.Cloud.Security;`):

```csharp
        services.AddSingleton<ITokenVault>(_ => new WindowsDpapiTokenVault());
        services.AddSingleton<IDestinationConnector, GoogleDriveConnector>();
        services.AddSingleton<GoogleDriveConnector>();
```

(`GoogleDriveConnector` wird zusätzlich als konkreter Typ registriert, damit das ViewModel in Task 6 den interaktiven Connector direkt auflösen kann. `DestinationStore` bekommt automatisch beide `IDestinationConnector` über `IEnumerable<IDestinationConnector>`.)

- [ ] **Step 5: Fallback-Vorbereitung — `.gitignore` für ein evtl. Build-Zeit-Secret**

Damit ein etwaiges Secret **nie** eingecheckt wird, jetzt schon die gitignored Datei vorsehen. In `.gitignore` (Repo-Wurzel) ans Ende ergänzen:

```
# OAuth-Build-Secret (nur falls die Google-Lib bei Desktop-PKCE ein Secret erzwingt — nie committen)
src/Flippo.Cloud/Auth/google-client-secret.txt
```

**Fallback-Rezept (nur ausführen, wenn Step 6 unten mit `invalid_request`/Secret-Zwang fehlschlägt):**
1. In GCP für den vorhandenen Desktop-Client ein Secret erzeugen.
2. Secret in `src/Flippo.Cloud/Auth/google-client-secret.txt` legen (bereits gitignored).
3. In `GoogleOAuth`: `private const string ClientSecret = "";` ersetzen durch ein statisches Laden zur Laufzeit aus einer eingebetteten Ressource **oder** aus Umgebungsvariable, die ein Pre-Build-Step aus der Datei füllt. Konkret: die Datei als `<EmbeddedResource>` in `Flippo.Cloud.csproj` aufnehmen und in `GoogleOAuth` per `Assembly.GetManifestResourceStream` lesen; fehlt sie, bleibt `ClientSecret` leer.
4. **Datei niemals stagen** (`git status` muss sie als ignoriert zeigen).

- [ ] **Step 6: Build verifizieren (0 Warnungen)**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler. (Der echte OAuth-Flow wird erst in Task 7 ausgeführt — hier zählt nur, dass alles kompiliert und die DI-Registrierung baubar ist.)

- [ ] **Step 7: Commit**

```bash
git add src/Flippo.Cloud/Auth/VaultDataStore.cs src/Flippo.Cloud/Auth/GoogleOAuth.cs src/Flippo.Cloud/Destinations/GoogleDriveConnector.cs src/Flippo.App/App.axaml.cs .gitignore
git commit -m "C1: GoogleOAuth (Loopback-PKCE) + GoogleDriveConnector + DI (kein committetes Secret)"
```

---

## Task 6: Provider-Chooser-UI + Fehlerzustände + Lokalzeit-Fix + resx + DestinationStore-Cleanup

**Files:**
- Create: `src/Flippo.App/Views/ProviderChooserWindow.axaml`
- Create: `src/Flippo.App/Views/ProviderChooserWindow.axaml.cs`
- Modify: `src/Flippo.App/Services/DialogService.cs` (`ShowProviderChooserAsync`)
- Modify: `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs` (Ziel hinzufügen → Chooser; interaktives Verbinden; Reconnect)
- Modify: `src/Flippo.App/Services/SetActionsService.cs` (`DestErrorMessage` um `Offline`/`QuotaExceeded` erweitern)
- Modify: `src/Flippo.App/Services/DestinationStore.cs` (`Remove` löscht Token via Connector)
- Modify: `src/Flippo.App/Views/BackupChooserWindow.axaml` (Lokalzeit)
- Modify: `src/Flippo.App/Views/SettingsView.axaml` („Ziel hinzufügen" + Reconnect-CTA)
- Modify: `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx`

**Interfaces:**
- Consumes: `IInteractiveConnector`, `DestinationStore`, `GoogleDriveConnector`, `IDialogService`.
- Produces: `record ProviderChoice(BackupDestinationKind Kind)`; `IDialogService.ShowProviderChooserAsync() : Task<BackupDestinationKind?>`; erweitertes `BackupDestinationsViewModel` (Command `AddDestination` statt nur `AddFolder`; `Reconnect`); `DestinationStore.Remove` ruft `IInteractiveConnector.RevokeAsync`-Äquivalent nicht direkt — s. Design unten.

> **Design-Entscheidung (Token-Cleanup beim Entfernen):** `DestinationStore` kennt nur `IDestinationConnector`. Damit „Entfernen" auch das Token löscht, bekommt `DestinationStore.Remove` die zu entfernende **Config** (nicht nur die Id) und ruft, wenn der zuständige Connector ein `IInteractiveConnector` mit optionalem Disconnect ist, dessen Cleanup. Um das Interface schlank zu halten, ergänzen wir `IInteractiveConnector` **nicht** um eine Disconnect-Methode, sondern lassen `GoogleDriveConnector` `IDisposableDestination` … — **zu komplex.** Stattdessen: `DestinationStore` bekommt optional den `ITokenVault` injiziert und löscht beim Entfernen einer `GoogleDrive`-Config den Vault-Key `{id:N}:user` direkt. Das ist der einfachste korrekte Weg (der Vault-Schlüssel ist deterministisch aus der Id).

- [ ] **Step 1: `BackupChooserWindow` auf Lokalzeit umstellen (Slice-1-Nit)**

In `src/Flippo.App/Views/BackupChooserWindow.axaml` die Zeitanzeige von UTC auf Lokalzeit umstellen. `CreatedAt` ist ein `DateTimeOffset` (bei LocalFolder in UTC). Statt `StringFormat` auf dem UTC-Wert einen Converter für Lokalzeit nutzen — am einfachsten inline über `CreatedAt.LocalDateTime`. Da XAML keine Methodenaufrufe im Binding erlaubt, den `Run` ersetzen:

Alte Zeile:
```xml
                            <Run Text="{Binding CreatedAt, StringFormat={}{0:g}}"/>
```
Neue Zeile (bindet auf die `LocalDateTime`-Property von `DateTimeOffset`):
```xml
                            <Run Text="{Binding CreatedAt.LocalDateTime, StringFormat={}{0:g}}"/>
```

(`DateTimeOffset.LocalDateTime` rechnet in die lokale Zeitzone um; `{0:g}` = kurzes Datum + kurze Zeit in der aktuellen Kultur.)

- [ ] **Step 2: `ProviderChooserWindow` anlegen**

Create `src/Flippo.App/Views/ProviderChooserWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loc="using:Flippo.App.Localization"
        x:Class="Flippo.App.Views.ProviderChooserWindow"
        Title="{loc:T Dest_AddTitle}"
        Width="380" SizeToContent="Height"
        WindowStartupLocation="CenterOwner"
        CanResize="False">
    <StackPanel Margin="24" Spacing="12">
        <TextBlock Text="{loc:T Dest_ChooseProvider}" FontSize="16" FontWeight="SemiBold"/>
        <Button HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Padding="14,12"
                Click="OnFolder" Content="{loc:T Dest_ProviderFolder}"/>
        <Button HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Padding="14,12"
                Click="OnGoogleDrive" Content="{loc:T Dest_ProviderGoogleDrive}"/>
        <Button HorizontalAlignment="Right" Content="{loc:T SetEditor_Cancel}" Click="OnCancel" MinWidth="90"/>
    </StackPanel>
</Window>
```

Create `src/Flippo.App/Views/ProviderChooserWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.Cloud.Abstractions;

namespace Flippo.App.Views;

/// <summary>Kleiner Provider-Chooser für „Ziel hinzufügen". Rückgabe null = abgebrochen.</summary>
public partial class ProviderChooserWindow : Window
{
    public ProviderChooserWindow() => InitializeComponent();

    private void OnFolder(object? sender, RoutedEventArgs e) => Close(BackupDestinationKind.LocalFolder);
    private void OnGoogleDrive(object? sender, RoutedEventArgs e) => Close(BackupDestinationKind.GoogleDrive);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
```

- [ ] **Step 3: `DialogService.ShowProviderChooserAsync` ergänzen**

In `src/Flippo.App/Services/DialogService.cs` im Interface `IDialogService` ergänzen:

```csharp
    /// <summary>Provider-Auswahl für ein neues Backup-Ziel (C1 Slice 2). Rückgabe null = abgebrochen.</summary>
    Task<BackupDestinationKind?> ShowProviderChooserAsync();
```

In der Klasse `DialogService` ergänzen:

```csharp
    public async Task<BackupDestinationKind?> ShowProviderChooserAsync()
    {
        var owner = _owner();
        if (owner is null) return null;

        var window = new ProviderChooserWindow();
        return await window.ShowDialog<BackupDestinationKind?>(owner);
    }
```

- [ ] **Step 4: `DestinationStore` — Token-Cleanup beim Entfernen**

In `src/Flippo.App/Services/DestinationStore.cs`:
- Ctor um optionalen `ITokenVault? vault = null` erweitern und in ein Feld legen.
- `Remove` so ändern, dass es die Config nachschlägt und bei `GoogleDrive` den Vault-Key löscht.

Ctor (bestehende Signatur ergänzen):
```csharp
    private readonly ITokenVault? _vault;

    public DestinationStore(IEnumerable<IDestinationConnector> connectors, ITokenVault? vault = null, string? filePath = null)
    {
        _connectors = connectors.ToDictionary(c => c.Kind);
        _vault = vault;
        _filePath = filePath ?? AppPaths.DestinationsFile;
    }
```

`Remove` ersetzen:
```csharp
    public void Remove(Guid id)
    {
        var all = GetAll().ToList();
        var target = all.FirstOrDefault(c => c.Id == id);
        if (target is { Kind: BackupDestinationKind.GoogleDrive })
            _vault?.Delete($"{id:N}:user");   // Refresh-Token mitlöschen (Neu-Verbinden verlangt Login)
        Persist(all.Where(c => c.Id != id).ToList());
    }
```

> **Verify Test-Kompatibilität:** Der bestehende `DestinationStoreTests` ruft `new DestinationStore(connectors, file)` mit dem `filePath` als **zweitem** Positionsargument. Durch das neue optionale `vault` (an Position 2) würde dieser Aufruf brechen. **Deshalb** im Test-Helper `NewStore` in `DestinationStoreTests.cs` den benannten Parameter nutzen. In `tests/Flippo.Tests/Cloud/DestinationStoreTests.cs` die eine Helper-Zeile ändern:

Alt:
```csharp
    private static DestinationStore NewStore(string file) =>
        new(new IDestinationConnector[] { new LocalFolderConnector() }, file);
```
Neu:
```csharp
    private static DestinationStore NewStore(string file) =>
        new(new IDestinationConnector[] { new LocalFolderConnector() }, filePath: file);
```

- [ ] **Step 5: `BackupDestinationsViewModel` — „Ziel hinzufügen" + interaktives Verbinden + Reconnect**

Replace `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.App.ViewModels;

/// <summary>„Backup-Ziele"-Sektion der Einstellungen: Ordner- und Google-Drive-Ziele verwalten
/// (hinzufügen/entfernen, sichern/wiederherstellen, neu verbinden).</summary>
public sealed partial class BackupDestinationsViewModel : ViewModelBase
{
    private readonly DestinationStore _store;
    private readonly IFilePickerService _picker;
    private readonly IDialogService _dialogs;
    private readonly SetActionsService _actions;
    private readonly GoogleDriveConnector _gdrive;

    public ObservableCollection<DestinationConfig> Destinations { get; } = new();
    [ObservableProperty] private bool _hasDestinations;

    public BackupDestinationsViewModel(DestinationStore store, IFilePickerService picker,
        IDialogService dialogs, SetActionsService actions, GoogleDriveConnector gdrive)
    {
        _store = store;
        _picker = picker;
        _dialogs = dialogs;
        _actions = actions;
        _gdrive = gdrive;
        Reload();
    }

    private void Reload()
    {
        Destinations.Clear();
        foreach (var c in _store.GetAll()) Destinations.Add(c);
        HasDestinations = Destinations.Count > 0;
    }

    [RelayCommand]
    private async Task AddDestination()
    {
        var kind = await _dialogs.ShowProviderChooserAsync();
        switch (kind)
        {
            case BackupDestinationKind.LocalFolder:
                await AddFolderAsync();
                break;
            case BackupDestinationKind.GoogleDrive:
                await ConnectGoogleDriveAsync();
                break;
            // null = abgebrochen → nichts tun
        }
    }

    private async Task AddFolderAsync()
    {
        var path = await _picker.PickFolderAsync(L.T("Dest_PickFolderTitle"));
        if (string.IsNullOrWhiteSpace(path)) return;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) name = path;
        _store.Add(LocalFolderConnector.BuildConfig(path, name));
        Reload();
    }

    private async Task ConnectGoogleDriveAsync()
    {
        try
        {
            var config = await _gdrive.ConnectInteractiveAsync();
            if (config is null) return;   // Nutzer hat OAuth abgebrochen
            _store.Add(config);
            Reload();
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
        }
    }

    [RelayCommand]
    private async Task Reconnect(DestinationConfig? config)
    {
        if (config is null || config.Kind != BackupDestinationKind.GoogleDrive) return;
        // Neu verbinden = neues Ziel verbinden, altes ersetzen (gleicher Anzeigename-Stil).
        try
        {
            var fresh = await _gdrive.ConnectInteractiveAsync();
            if (fresh is null) return;
            _store.Remove(config.Id);
            _store.Add(fresh);
            Reload();
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
        }
    }

    [RelayCommand]
    private async Task Remove(DestinationConfig? config)
    {
        if (config is null) return;
        if (await _dialogs.ConfirmAsync(L.T("Dest_RemoveTitle"),
                string.Format(L.T("Dest_RemoveMsg"), config.DisplayName), L.T("Ctx_Delete")))
        {
            _store.Remove(config.Id);
            Reload();
        }
    }

    [RelayCommand]
    private Task Backup(DestinationConfig? config)
        => config is null ? Task.CompletedTask : _actions.ExportToDestinationAsync(config);

    [RelayCommand]
    private Task Restore(DestinationConfig? config)
        => config is null ? Task.CompletedTask : _actions.RestoreFromDestinationAsync(config);

    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        DestinationState.Offline => L.T("Dest_ErrOffline"),
        DestinationState.QuotaExceeded => L.T("Dest_ErrQuota"),
        _ => L.T("Dest_ErrTransport")
    };
}
```

> **Hinweis:** Das `AddFolderCommand` heißt jetzt `AddDestinationCommand`. Die View (Step 6) bindet den Button darauf um. Die Reconnect-CTA (Step 6) bindet `ReconnectCommand`.

- [ ] **Step 6: `SetActionsService.DestErrorMessage` um `Offline`/`QuotaExceeded` erweitern**

Die Karten-Buttons „Sichern"/„Wiederherstellen" laufen über `BackupDestinationsViewModel.Backup`/`.Restore` → `SetActionsService.ExportToDestinationAsync`/`RestoreFromDestinationAsync`, die `DestinationException` über die **eigene** (private, statische) `SetActionsService.DestErrorMessage` in einen Nutzertext übersetzen. Diese Methode kennt bisher nur zwei Fälle. Damit ein Offline-/Quota-Fehler beim Sichern/Wiederherstellen denselben Text zeigt wie beim Verbinden/Reconnect (Step 5), auf dieselbe 4-Zweig-Switch erweitern — dieselben resx-Keys, dieselbe Reihenfolge, keine gemeinsame Hilfsmethode (zwei kleine private Switches sind hier einfacher und risikoärmer als ein neuer Shared-Helper).

In `src/Flippo.App/Services/SetActionsService.cs`:

Alt:
```csharp
    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        _ => L.T("Dest_ErrTransport")
    };
```

Neu:
```csharp
    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        DestinationState.Offline => L.T("Dest_ErrOffline"),
        DestinationState.QuotaExceeded => L.T("Dest_ErrQuota"),
        _ => L.T("Dest_ErrTransport")
    };
```

**Verify:** `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug` — 0 Warnungen, 0 Fehler. Kein separater Unit-Test nötig (reine String-Mapping-Änderung, identisch zur bereits in Step 5 gebauten VM-Switch); Task 7, Step 5 (Offline-E2E über die Karte) deckt den Pfad ab.

- [ ] **Step 7: `SettingsView.axaml` — „Ziel hinzufügen" + Reconnect-CTA**

In `src/Flippo.App/Views/SettingsView.axaml` innerhalb der `BackupDestinationsViewModel`-Border:

(a) Den Add-Button umbenennen/umbinden:
```xml
                            <Button Grid.Column="1" Content="{loc:T Dest_AddDestination}" Command="{Binding AddDestinationCommand}"/>
```
(ersetzt die bisherige Zeile mit `Dest_AddFolder`/`AddFolderCommand`).

(b) Innerhalb des Karten-`DataTemplate` (x:DataType `cloud:DestinationConfig`) unter der `Kind`-Zeile eine dezente „Neu verbinden"-CTA nur für Google-Drive einfügen. Da das Template keinen State pro Ziel hat (Config trägt keinen Live-Zustand), zeigen wir die CTA **immer** bei `GoogleDrive` als leichtgewichtige Aktion. Neuer Block direkt nach dem `StackPanel Grid.Column="0"`-Ende bzw. innerhalb der Button-`StackPanel Grid.Column="1"` **vor** dem „✕"-Button:

```xml
                                                <Button Content="{loc:T Dest_Reconnect}"
                                                        ToolTip.Tip="{loc:T Dest_ReconnectHint}"
                                                        IsVisible="{Binding Kind, Converter={x:Static vm:KindConverters.IsGoogleDrive}}"
                                                        Command="{Binding $parent[ItemsControl].((vm:BackupDestinationsViewModel)DataContext).ReconnectCommand}"
                                                        CommandParameter="{Binding}"/>
```

Das braucht einen kleinen Konverter, da `Kind` ein Enum ist. Create-Ergänzung: eine statische Konverter-Klasse. Füge in `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs` **am Dateiende** (außerhalb der VM-Klasse, gleicher Namespace) hinzu:

```csharp
/// <summary>Kleine XAML-Konverter für die Ziel-Karten (Enum→bool).</summary>
public static class KindConverters
{
    public static readonly Avalonia.Data.Converters.IValueConverter IsGoogleDrive =
        new Avalonia.Data.Converters.FuncValueConverter<BackupDestinationKind, bool>(
            k => k == BackupDestinationKind.GoogleDrive);
}
```

(Die View kennt `xmlns:vm="using:Flippo.App.ViewModels"` bereits.)

- [ ] **Step 8: resx-Keys (EN + DE)**

In `src/Flippo.App/Resources/Strings.resx` vor `</root>` einfügen:

```xml
  <data name="Dest_AddDestination" xml:space="preserve"><value>Add destination…</value></data>
  <data name="Dest_AddTitle" xml:space="preserve"><value>Add backup destination</value></data>
  <data name="Dest_ChooseProvider" xml:space="preserve"><value>Where should backups go?</value></data>
  <data name="Dest_ProviderFolder" xml:space="preserve"><value>Folder (local or synced)</value></data>
  <data name="Dest_ProviderGoogleDrive" xml:space="preserve"><value>Google Drive</value></data>
  <data name="Dest_Reconnect" xml:space="preserve"><value>Reconnect</value></data>
  <data name="Dest_ReconnectHint" xml:space="preserve"><value>Sign in to Google again to refresh access.</value></data>
  <data name="Dest_ErrOffline" xml:space="preserve"><value>Destination not reachable — save locally instead?</value></data>
  <data name="Dest_ErrQuota" xml:space="preserve"><value>Not enough storage in the Google account.</value></data>
```

In `src/Flippo.App/Resources/Strings.de.resx` vor `</root>` einfügen:

```xml
  <data name="Dest_AddDestination" xml:space="preserve"><value>Ziel hinzufügen…</value></data>
  <data name="Dest_AddTitle" xml:space="preserve"><value>Backup-Ziel hinzufügen</value></data>
  <data name="Dest_ChooseProvider" xml:space="preserve"><value>Wohin sollen Backups?</value></data>
  <data name="Dest_ProviderFolder" xml:space="preserve"><value>Ordner (lokal oder synchronisiert)</value></data>
  <data name="Dest_ProviderGoogleDrive" xml:space="preserve"><value>Google Drive</value></data>
  <data name="Dest_Reconnect" xml:space="preserve"><value>Neu verbinden</value></data>
  <data name="Dest_ReconnectHint" xml:space="preserve"><value>Erneut bei Google anmelden, um den Zugriff zu erneuern.</value></data>
  <data name="Dest_ErrOffline" xml:space="preserve"><value>Ziel nicht erreichbar — stattdessen lokal speichern?</value></data>
  <data name="Dest_ErrQuota" xml:space="preserve"><value>Nicht genug Speicher im Google-Konto.</value></data>
```

- [ ] **Step 9: DI — `DestinationStore` mit `ITokenVault`**

Weil `DestinationStore` jetzt optional `ITokenVault` nimmt und DI den registrierten `ITokenVault` (Task 5) automatisch einspeist, ist keine Änderung nötig — der DI-Container wählt den Ctor mit den meisten auflösbaren Parametern. **Verify:** `services.AddSingleton<DestinationStore>();` bleibt; da `IEnumerable<IDestinationConnector>` und `ITokenVault` registriert sind und `filePath` optional ist, wird der 2-Arg-Ctor (`connectors`, `vault`) genutzt und `filePath` fällt auf `AppPaths.DestinationsFile`.

- [ ] **Step 10: Build + volle Testsuite grün**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler.

Run: `dotnet test`
Expected: PASS — 201 bestehende + WindowsDpapiTokenVault (5, auf non-Windows skipped) + GoogleDriveDestination (6) = alle grün, 0 Fehler.

- [ ] **Step 11: Commit**

```bash
git add src/Flippo.App/Views/ProviderChooserWindow.axaml src/Flippo.App/Views/ProviderChooserWindow.axaml.cs src/Flippo.App/Views/BackupChooserWindow.axaml src/Flippo.App/Views/SettingsView.axaml src/Flippo.App/Services/DialogService.cs src/Flippo.App/Services/DestinationStore.cs src/Flippo.App/Services/SetActionsService.cs src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs src/Flippo.App/Resources/Strings.resx src/Flippo.App/Resources/Strings.de.resx tests/Flippo.Tests/Cloud/DestinationStoreTests.cs
git commit -m "C1: Provider-Chooser-UI + Google-Drive-Verbinden/Reconnect + Lokalzeit-Fix + Token-Cleanup"
```

---

## Task 7: Manueller E2E-Verify (Plan-C1-Gate)

> Nicht automatisierbar (echter Browser + Google-Login + echtes Drive). Dies ist das Abnahme-Gate des Slices. Kein Code-Commit; Ergebnis wird protokolliert.

**Vorbedingung:** GCP-Desktop-Client „In Produktion", Drive-API an, Konto `solutionworxug@gmail.com` verfügbar (Memory `flippo-gdrive-oauth-setup`).

- [ ] **Step 1: App bauen und starten**

Run: `taskkill //F //IM Flippo.App.exe 2>/dev/null; dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler. Dann App starten.

- [ ] **Step 2: Google Drive verbinden**

Einstellungen → „Backup-Ziele" → „Ziel hinzufügen" → „Google Drive". Erwartung: System-Browser öffnet, Konto wählen, `drive.file` zustimmen. Nach Zustimmung erscheint die Karte „Google Drive ({email})".
- Verify (Beweis): Karte sichtbar; im `%APPDATA%\FLIPPO\tokens\`-Ordner liegt eine `{guid}:user.bin`-artige Datei (DPAPI-Blob, nicht Klartext).

- [ ] **Step 3: Sichern**

Karte → „Sichern". Erwartung: Erfolg-Meldung.
- Verify (Beweis): In Drive (Web-UI, „Meine Ablage/FLIPPO/") liegt `flippo-backup-*.json`.

- [ ] **Step 4: Wiederherstellen**

Karte → „Wiederherstellen". Erwartung: Backup-Liste (neuestes zuoberst, **Lokalzeit** angezeigt) → Auswahl → Preview/Confirm (identisch zum Datei-Import) → nach Bestätigung sind die Daten wieder da.
- Verify (Beweis): Import-Summary-Dialog zeigt importierte Sets/Einträge > 0.

- [ ] **Step 5: Offline-Verhalten**

WLAN aus → Karte → „Sichern". Erwartung: **nicht-blockierender** Hinweis „Ziel nicht erreichbar — stattdessen lokal speichern?" (`Dest_ErrOffline`), keine Exception, App bleibt bedienbar. WLAN wieder an.

- [ ] **Step 6: Entfernen → Token weg**

Karte → „✕" → bestätigen. Dann erneut „Ziel hinzufügen" → „Google Drive".
- Verify (Beweis): Der Vault-Blob der entfernten Id ist verschwunden; erneutes Verbinden verlangt echten Login (kein stilles Re-Auth).

- [ ] **Step 7: Ergebnis protokollieren**

Alle 6 Schritte mit Beweis abgehakt → C1-Slice-2-Gate bestanden. Falls Step 2 mit Secret-Zwang (`invalid_request`) scheitert → Fallback aus Task 5, Step 5 ausführen und E2E wiederholen.

---

## Self-Review

**Spec-Abdeckung (jede Spec-Sektion → Task):**
- §Constraints (NuGet nur in Cloud, kein Secret, offline-first, DPAPI-only) → Task 1 (NuGet), Task 5 (kein Secret/PKCE), Task 2 (DPAPI). ✓
- §Architektur & Projektstruktur (Abstractions/Destinations/Security/Auth) → Tasks 1–5 legen genau diese Ordner an. ✓
- §Kernabstraktion (`ITokenVault`, `IInteractiveConnector`) → Task 1. ✓
- §OAuth-Flow (Loopback-PKCE, `IDataStore`→`ITokenVault`, `drive.file`, `access_type=offline`+`prompt=consent`, NotConnected bei fehlendem Refresh) → Task 5 (`GoogleOAuth` + `VaultDataStore`). ✓
- §`GoogleDriveConnector` (`ConnectInteractiveAsync`, Konto-E-Mail via `about.get`, Guid, `Create` rekonstruiert via Vault, Disconnect löscht Token) → Task 5 (Connector) + Task 6 (Remove→Vault-Delete). ✓
- §`GoogleDriveDestination` (Ordner FLIPPO find-or-create, List `name contains 'flippo-backup-'`, Upload/Download/Delete, absteigend nach Name, Fehler-Mapping 401→NotConnected/Timeout→Offline/Quota→QuotaExceeded/sonst→TransportFailed, RemoteId=FileId) → Task 3 (Logik + Mapping gegen Fake) + Task 4 (reale Drive-Calls). ✓
- §UI (Ziel-hinzufügen-Chooser, Ordner/Drive, Karte mit Konto, Fehlerzustände NotConnected→Neu-verbinden, Offline→nicht-blockierend, Quota→Meldung, Backup-Chooser Lokalzeit) → Task 6 (Chooser, Reconnect-CTA, Lokalzeit-Fix, Offline/Quota-resx, `SetActionsService.DestErrorMessage`-Erweiterung für den Sichern/Wiederherstellen-Pfad). ✓
- §Tests (Destination gegen Fake: find-or-create/Roundtrip/Sort/Fehler-Mapping; Vault Store/Retrieve/Delete Windows-guarded; OAuth+echte API = manueller E2E; 201 bestehende grün) → Task 3, Task 2, Task 7. ✓
- §Verify (E2E: verbinden→sichern→wiederherstellen→offline→entfernen) → Task 7 (6 Schritte mit Beweisen). ✓
- §Bewusst NICHT im Slice (OneDrive, FlippoCloud, mac/Linux-Vaults, Ordner-Picker in Drive, Team-Drives, Scope-Registrierung) → in keinem Task; DPAPI-Vault ist explizit `[SupportedOSPlatform("windows")]`, fester Ordner „FLIPPO". ✓

**Platzhalter-Scan:** Keine „TBD"/„similar to Task N"/„add error handling"-Platzhalter. Zwei bewusst als **build-/manual-verify** markierte Tasks (4 = realer DriveApi, 5 = OAuth) enthalten **vollständigen** Code; ihre „nicht unit-testbar"-Natur ist von der Spec vorgegeben, nicht Code-Lücke. Zwei „Implementierer-Verify"-Boxen (DTO-Property-Namen `CreatedTimeDateTimeOffset`, `Prompt`/`ClientSecret`-leer) benennen die einzigen versionsabhängigen Google-API-Punkte mit konkretem Fallback — kein offener Platzhalter.

**Typ-Konsistenz:**
- `ITokenVault.Store(string,string)/Retrieve(string):string?/Delete(string)` — identisch in `WindowsDpapiTokenVault`, `VaultDataStore`, `DestinationStore.Remove`. ✓
- `IDriveApi`: `FindOrCreateFolderAsync(string,ct):Task<string>`, `ListFilesAsync(string,string,ct):Task<IReadOnlyList<DriveFile>>`, `UploadAsync(string,string,Stream,ct):Task<DriveFile>`, `DownloadAsync(string,ct):Task<Stream>`, `DeleteAsync(string,ct):Task` — identisch in `FakeDriveApi`, `DriveApi`, `GoogleDriveDestination`. ✓
- `DriveFile(string Id, string Name, DateTimeOffset CreatedAt, long SizeBytes)` → gemappt auf `BackupFileInfo(RemoteId=Id, FileName=Name, CreatedAt, SizeBytes)` — Feld-Reihenfolge/Typen konsistent mit dem in Slice 1 festgelegten `BackupFileInfo`. ✓
- `DriveErrorKind {Unauthorized,Timeout,QuotaExceeded,Other}` → `DestinationState {NotConnected,Offline,QuotaExceeded,TransportFailed}` — Mapping in `GoogleDriveDestination.MapAsync` deckt alle vier Fälle ab. ✓
- `DestErrorMessage(DestinationException):string` existiert **zweimal** (bewusst, kein Shared-Helper nötig) — `BackupDestinationsViewModel` (Connect/Reconnect, Task 6 Step 5) und `SetActionsService` (Sichern/Wiederherstellen, Task 6 Step 6) — beide auf dieselbe 4-Zweig-Switch (`NotConnected`/`Offline`/`QuotaExceeded`/sonst) mit denselben resx-Keys umgestellt. ✓
- `IInteractiveConnector.ConnectInteractiveAsync(ct):Task<DestinationConfig?>` — Signatur identisch in Interface (Task 1) und `GoogleDriveConnector` (Task 5); ViewModel konsumiert `?`-Rückgabe (null=abgebrochen). ✓
- `DestinationStore`-Ctor neu `(IEnumerable<IDestinationConnector>, ITokenVault? vault=null, string? filePath=null)` — Test-Helper auf `filePath:`-benannt umgestellt (Task 6, Step 4), DI nutzt `(connectors, vault)`-Pfad. ✓
- resx: Alle neuen Keys (`Dest_AddDestination`, `Dest_AddTitle`, `Dest_ChooseProvider`, `Dest_ProviderFolder`, `Dest_ProviderGoogleDrive`, `Dest_Reconnect`, `Dest_ReconnectHint`, `Dest_ErrOffline`, `Dest_ErrQuota`) in **beiden** resx-Dateien; `Dest_ErrNotConnected`/`Dest_ErrTransport`/`Dest_ErrorTitle` existieren bereits aus Slice 1. ✓
