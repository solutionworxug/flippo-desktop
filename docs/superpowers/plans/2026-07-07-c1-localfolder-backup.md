# C1 Slice 1 — LocalFolder-Backup-Ziel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nutzer kann in den Einstellungen Ordner als „Backup-Ziele" anlegen, ein Backup dorthin sichern (mit Retention), vorhandene Backups auflisten und eines über den bestehenden Preview/Confirm-Dialog wiederherstellen — komplett ohne OAuth.

**Architecture:** Neues BCL-only-Projekt `Flippo.Cloud` mit der Transport-Abstraktion `IBackupDestination` + `LocalFolderDestination`. Die App orchestriert (`CloudBackupService`) den bereits Stream-basierten `BackupService` (Data) gegen ein Ziel. Ziel-Metadaten in `destinations.json`. Restore nutzt den vorhandenen, getesteten Backup-Import-Pfad.

**Tech Stack:** C# / .NET 10, System.Text.Json (BCL), Avalonia 12 (`IStorageProvider` Folder-Picker), CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- `Flippo.Core` bleibt BCL-only; `Flippo.Cloud` in diesem Slice ebenfalls **BCL-only** (kein NuGet).
- **Offline-first, opt-in:** kein Startup-Check, kein Background-Polling — nur nutzergetriggert.
- **Backup-Format unverändert** — Restore läuft ausschließlich über den bestehenden `BackupService` (`ParseAsync`/`ImportAsync`). Interop-Gate (Desktop↔Android) unberührt.
- Wörterbücher bleiben außerhalb des Backups.
- Alle neuen UI-Strings DE **und** EN in `Strings.de.resx` + `Strings.resx`.
- Commit-Konvention: bestehende Trailer beibehalten; niemals `.claude/` stagen; gezielt `git add` mit expliziten Pfaden.
- `TreatWarningsAsErrors` ist an (Directory.Build.props) — Build muss 0 Warnungen haben.

---

## File Structure

**Neu — `Flippo.Cloud` (Projekt):**
- `src/Flippo.Cloud/Flippo.Cloud.csproj` — classlib, erbt `Directory.Build.props`.
- `src/Flippo.Cloud/Abstractions/BackupDestinationModels.cs` — Enums, Records, `DestinationException`.
- `src/Flippo.Cloud/Abstractions/IBackupDestination.cs`
- `src/Flippo.Cloud/Abstractions/IDestinationConnector.cs`
- `src/Flippo.Cloud/Destinations/LocalFolderDestination.cs`
- `src/Flippo.Cloud/Destinations/LocalFolderConnector.cs`

**Neu — App:**
- `src/Flippo.App/Services/DestinationStore.cs` — `destinations.json`-CRUD + `Resolve(config)`.
- `src/Flippo.App/Services/CloudBackupService.cs` — Orchestrierung + Retention.
- `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs` — „Backup-Ziele"-Sektion.
- `src/Flippo.App/Views/BackupChooserWindow.axaml(.cs)` — Auswahl eines vorhandenen Backups.

**Geändert:**
- `FlippoDesktop.slnx` — Projekt aufnehmen.
- `src/Flippo.App/Flippo.App.csproj` + `tests/Flippo.Tests/Flippo.Tests.csproj` — `Flippo.Cloud`-Referenz.
- `src/Flippo.Data/AppPaths.cs` — `DestinationsFile`.
- `src/Flippo.App/Services/FilePickerService.cs` — `PickFolderAsync`.
- `src/Flippo.App/Services/DialogService.cs` — `ShowBackupChooserAsync`.
- `src/Flippo.App/Services/SetActionsService.cs` — Ziel-Export/-Restore + `CompleteImportAsync`-Refactor.
- `src/Flippo.App/ViewModels/SettingsViewModel.cs` — `Destinations`-Property.
- `src/Flippo.App/Views/SettingsView.axaml` — „Backup-Ziele"-Sektion.
- `src/Flippo.App/App.axaml.cs` — DI-Registrierungen.
- `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx` — `Dest_*`-Keys.

**Tests:**
- `tests/Flippo.Tests/Cloud/LocalFolderDestinationTests.cs`
- `tests/Flippo.Tests/Cloud/DestinationStoreTests.cs`
- `tests/Flippo.Tests/Cloud/CloudBackupServiceTests.cs`

---

## Task 1: `Flippo.Cloud`-Projekt + Abstraktion

**Files:**
- Create: `src/Flippo.Cloud/Flippo.Cloud.csproj`
- Create: `src/Flippo.Cloud/Abstractions/BackupDestinationModels.cs`
- Create: `src/Flippo.Cloud/Abstractions/IBackupDestination.cs`
- Create: `src/Flippo.Cloud/Abstractions/IDestinationConnector.cs`
- Modify: `FlippoDesktop.slnx`
- Modify: `src/Flippo.App/Flippo.App.csproj`
- Modify: `tests/Flippo.Tests/Flippo.Tests.csproj`

**Interfaces:**
- Produces: `enum BackupDestinationKind`, `enum DestinationState`, `record BackupFileInfo(string RemoteId, string FileName, DateTimeOffset CreatedAt, long SizeBytes)`, `record DestinationConfig(Guid Id, BackupDestinationKind Kind, string DisplayName, IReadOnlyDictionary<string,string> Settings)`, `class DestinationException(DestinationState State, ...)`, `interface IBackupDestination`, `interface IDestinationConnector`.

- [ ] **Step 1: Projektdatei anlegen**

Create `src/Flippo.Cloud/Flippo.Cloud.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Flippo.Cloud</RootNamespace>
  </PropertyGroup>
</Project>
```

(TargetFramework/Nullable/ImplicitUsings kommen aus `Directory.Build.props`.)

- [ ] **Step 2: Abstraktions-Typen schreiben**

Create `src/Flippo.Cloud/Abstractions/BackupDestinationModels.cs`:

```csharp
namespace Flippo.Cloud.Abstractions;

/// <summary>Art eines Backup-Ziels. FlippoCloud ist reserviert (C3), hier nicht implementiert.</summary>
public enum BackupDestinationKind { LocalFolder, GoogleDrive, OneDrive, FlippoCloud }

/// <summary>UI-Zustand eines Ziels. Offline/QuotaExceeded sind cloud-only (spätere Slices).</summary>
public enum DestinationState { Ready, NotConnected, Offline, QuotaExceeded, TransportFailed }

/// <summary>Ein am Ziel liegendes Backup.</summary>
public sealed record BackupFileInfo(string RemoteId, string FileName, DateTimeOffset CreatedAt, long SizeBytes);

/// <summary>Persistierbare, unsensible Ziel-Konfiguration (LocalFolder: Settings["folderPath"]).</summary>
public sealed record DestinationConfig(
    Guid Id, BackupDestinationKind Kind, string DisplayName, IReadOnlyDictionary<string, string> Settings);

/// <summary>Transport-Fehler mit UI-abbildbarem Zustand.</summary>
public sealed class DestinationException : Exception
{
    public DestinationState State { get; }
    public DestinationException(DestinationState state, string message, Exception? inner = null)
        : base(message, inner) => State = state;
}
```

Create `src/Flippo.Cloud/Abstractions/IBackupDestination.cs`:

```csharp
namespace Flippo.Cloud.Abstractions;

/// <summary>Transport für Backup-Dateien (Stream rein/raus). Auth ist orthogonal (Connector).</summary>
public interface IBackupDestination
{
    Guid DestinationId { get; }
    string DisplayName { get; }
    BackupDestinationKind Kind { get; }

    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default);
    Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default);
    Task DeleteAsync(string remoteId, CancellationToken ct = default);
}
```

Create `src/Flippo.Cloud/Abstractions/IDestinationConnector.cs`:

```csharp
namespace Flippo.Cloud.Abstractions;

/// <summary>Baut aus einer Config ein Transport-Objekt. Pro Kind eine Implementierung.
/// (Interaktives Verbinden/OAuth wächst mit dem ersten Cloud-Provider-Slice hinzu.)</summary>
public interface IDestinationConnector
{
    BackupDestinationKind Kind { get; }
    IBackupDestination Create(DestinationConfig config);
}
```

- [ ] **Step 3: Projekt in die Solution + Referenzen aufnehmen**

In `FlippoDesktop.slnx` innerhalb `<Folder Name="/src/">` ergänzen:

```xml
    <Project Path="src/Flippo.Cloud/Flippo.Cloud.csproj" />
```

In `src/Flippo.App/Flippo.App.csproj` im `<ItemGroup>` mit den ProjectReferences ergänzen:

```xml
    <ProjectReference Include="..\Flippo.Cloud\Flippo.Cloud.csproj" />
```

In `tests/Flippo.Tests/Flippo.Tests.csproj` bei den ProjectReferences ergänzen:

```xml
    <ProjectReference Include="..\..\src\Flippo.Cloud\Flippo.Cloud.csproj" />
```

- [ ] **Step 4: Build verifizieren**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: `Der Buildvorgang wurde erfolgreich ausgeführt.` — 0 Warnungen, 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.Cloud/ FlippoDesktop.slnx src/Flippo.App/Flippo.App.csproj tests/Flippo.Tests/Flippo.Tests.csproj
git commit -m "C1: Flippo.Cloud-Projekt + Backup-Transport-Abstraktion"
```

---

## Task 2: `LocalFolderDestination` (Roundtrip, TDD)

**Files:**
- Create: `src/Flippo.Cloud/Destinations/LocalFolderDestination.cs`
- Create: `src/Flippo.Cloud/Destinations/LocalFolderConnector.cs` (nur `BuildConfig` + Ctor-Helfer; `Create` folgt in Task 3)
- Test: `tests/Flippo.Tests/Cloud/LocalFolderDestinationTests.cs`

**Interfaces:**
- Consumes: `IBackupDestination`, `DestinationConfig`, `BackupFileInfo`, `DestinationException`, `DestinationState`.
- Produces: `LocalFolderDestination(DestinationConfig config)`; `static DestinationConfig LocalFolderConnector.BuildConfig(string folderPath, string displayName)`; Konstante Dateipräfix `"flippo-backup-"`, Suchmuster `"flippo-backup-*.json"`.

- [ ] **Step 1: Failing test schreiben**

Create `tests/Flippo.Tests/Cloud/LocalFolderDestinationTests.cs`:

```csharp
using System.Text;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class LocalFolderDestinationTests
{
    [Fact]
    public async Task Upload_List_Download_Delete_Roundtrip_ByteIdentical()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "Test"));
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
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task List_MissingFolder_Throws_NotConnected()
    {
        var missing = Path.Combine(Path.GetTempPath(), "flippo-missing-" + Guid.NewGuid());
        var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(missing, "X"));
        var ex = await Assert.ThrowsAsync<DestinationException>(() => dest.ListBackupsAsync());
        Assert.Equal(DestinationState.NotConnected, ex.State);
    }

    [Fact]
    public async Task List_IgnoresForeignFiles()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "notes.txt"), "x");
            var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "T"));
            await dest.UploadAsync("flippo-backup-20260707-120000.json", new MemoryStream([1, 2, 3]));
            Assert.Single(await dest.ListBackupsAsync());
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 2: Test läuft NICHT (fehlende Typen)**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~LocalFolderDestination"`
Expected: Kompilierfehler `LocalFolderDestination`/`LocalFolderConnector` nicht gefunden.

- [ ] **Step 3: `LocalFolderConnector.BuildConfig` anlegen**

Create `src/Flippo.Cloud/Destinations/LocalFolderConnector.cs`:

```csharp
using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>Connector für lokale Ordner. Kein OAuth — die App wählt den Ordner vorab und ruft BuildConfig.</summary>
public sealed class LocalFolderConnector : IDestinationConnector
{
    public const string FolderPathKey = "folderPath";

    public BackupDestinationKind Kind => BackupDestinationKind.LocalFolder;

    public IBackupDestination Create(DestinationConfig config) => new LocalFolderDestination(config);

    public static DestinationConfig BuildConfig(string folderPath, string displayName) => new(
        Guid.NewGuid(), BackupDestinationKind.LocalFolder, displayName,
        new Dictionary<string, string> { [FolderPathKey] = folderPath });
}
```

- [ ] **Step 4: `LocalFolderDestination` implementieren**

Create `src/Flippo.Cloud/Destinations/LocalFolderDestination.cs`:

```csharp
using Flippo.Cloud.Abstractions;

namespace Flippo.Cloud.Destinations;

/// <summary>Backup-Ziel = ein Ordner im Dateisystem (RemoteId = Dateiname, flach im Ordner).</summary>
public sealed class LocalFolderDestination : IBackupDestination
{
    private const string Prefix = "flippo-backup-";
    private const string SearchPattern = "flippo-backup-*.json";

    private readonly string _folder;

    public LocalFolderDestination(DestinationConfig config)
    {
        DestinationId = config.Id;
        DisplayName = config.DisplayName;
        _folder = config.Settings.TryGetValue(LocalFolderConnector.FolderPathKey, out var p) ? p : "";
    }

    public Guid DestinationId { get; }
    public string DisplayName { get; }
    public BackupDestinationKind Kind => BackupDestinationKind.LocalFolder;

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        EnsureFolder();
        var list = new List<BackupFileInfo>();
        foreach (var path in Directory.EnumerateFiles(_folder, SearchPattern))
        {
            var fi = new FileInfo(path);
            list.Add(new BackupFileInfo(fi.Name, fi.Name, fi.LastWriteTimeUtc, fi.Length));
        }
        IReadOnlyList<BackupFileInfo> result = list;
        return Task.FromResult(result);
    }

    public async Task<BackupFileInfo> UploadAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        EnsureFolder();
        var path = Path.Combine(_folder, fileName);
        try
        {
            await using (var fs = File.Create(path))
                await content.CopyToAsync(fs, ct);
        }
        catch (IOException ex)
        {
            throw new DestinationException(DestinationState.TransportFailed, ex.Message, ex);
        }
        var fi = new FileInfo(path);
        return new BackupFileInfo(fi.Name, fi.Name, fi.LastWriteTimeUtc, fi.Length);
    }

    public Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
    {
        var path = Path.Combine(_folder, remoteId);
        if (!File.Exists(path))
            throw new DestinationException(DestinationState.TransportFailed, $"Backup '{remoteId}' nicht gefunden.");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string remoteId, CancellationToken ct = default)
    {
        var path = Path.Combine(_folder, remoteId);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException ex) { throw new DestinationException(DestinationState.TransportFailed, ex.Message, ex); }
        return Task.CompletedTask;
    }

    private void EnsureFolder()
    {
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
            throw new DestinationException(DestinationState.NotConnected, $"Ordner nicht erreichbar: '{_folder}'.");
    }
}
```

- [ ] **Step 5: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~LocalFolderDestination"`
Expected: PASS (3 Tests).

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.Cloud/Destinations/ tests/Flippo.Tests/Cloud/LocalFolderDestinationTests.cs
git commit -m "C1: LocalFolderDestination + Roundtrip-Test"
```

---

## Task 3: `DestinationStore` (Persistenz + Resolve, TDD)

**Files:**
- Create: `src/Flippo.App/Services/DestinationStore.cs`
- Modify: `src/Flippo.Data/AppPaths.cs`
- Test: `tests/Flippo.Tests/Cloud/DestinationStoreTests.cs`

**Interfaces:**
- Consumes: `DestinationConfig`, `IDestinationConnector`, `IBackupDestination`, `LocalFolderConnector`.
- Produces: `DestinationStore(IEnumerable<IDestinationConnector> connectors, string? filePath = null)` mit `IReadOnlyList<DestinationConfig> GetAll()`, `void Add(DestinationConfig)`, `void Remove(Guid id)`, `IBackupDestination Resolve(DestinationConfig)`; `AppPaths.DestinationsFile`.

- [ ] **Step 1: Failing test schreiben**

Create `tests/Flippo.Tests/Cloud/DestinationStoreTests.cs`:

```csharp
using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.Tests.Cloud;

public class DestinationStoreTests
{
    private static DestinationStore NewStore(string file) =>
        new(new IDestinationConnector[] { new LocalFolderConnector() }, file);

    [Fact]
    public void Add_Persists_ReloadFromDisk_AndResolves()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        store.Add(LocalFolderConnector.BuildConfig(Path.GetTempPath(), "Backups"));

        var reloaded = NewStore(file);   // frische Instanz liest von Platte
        var all = reloaded.GetAll();
        Assert.Single(all);
        Assert.Equal("Backups", all[0].DisplayName);

        var dest = reloaded.Resolve(all[0]);
        Assert.Equal(BackupDestinationKind.LocalFolder, dest.Kind);
        Assert.Equal("Backups", dest.DisplayName);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        var cfg = LocalFolderConnector.BuildConfig(Path.GetTempPath(), "X");
        store.Add(cfg);
        store.Remove(cfg.Id);
        Assert.Empty(NewStore(file).GetAll());
    }

    [Fact]
    public void GetAll_MissingFile_ReturnsEmpty()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "none.json");
        Assert.Empty(NewStore(file).GetAll());
    }

    [Fact]
    public void Resolve_UnknownKind_Throws()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "destinations.json");
        var store = NewStore(file);
        var cfg = new DestinationConfig(Guid.NewGuid(), BackupDestinationKind.GoogleDrive, "G",
            new Dictionary<string, string>());
        Assert.Throws<InvalidOperationException>(() => store.Resolve(cfg));
    }
}
```

- [ ] **Step 2: Test läuft NICHT**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~DestinationStore"`
Expected: Kompilierfehler `DestinationStore` nicht gefunden.

- [ ] **Step 3: `AppPaths.DestinationsFile` ergänzen**

In `src/Flippo.Data/AppPaths.cs` nach der `SettingsFile`-Zeile einfügen:

```csharp
    public static string DestinationsFile => Path.Combine(DataDirectory, "destinations.json");
```

- [ ] **Step 4: `DestinationStore` implementieren**

Create `src/Flippo.App/Services/DestinationStore.cs`:

```csharp
using System.Text.Json;
using Flippo.Cloud.Abstractions;
using Flippo.Data;

namespace Flippo.App.Services;

/// <summary>
/// Persistiert Backup-Ziel-Configs in destinations.json (nur unsensible Metadaten) und löst
/// eine Config über den passenden Connector in ein Transport-Objekt auf.
/// </summary>
public sealed class DestinationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<BackupDestinationKind, IDestinationConnector> _connectors;
    private readonly string _filePath;

    public DestinationStore(IEnumerable<IDestinationConnector> connectors, string? filePath = null)
    {
        _connectors = connectors.ToDictionary(c => c.Kind);
        _filePath = filePath ?? AppPaths.DestinationsFile;
    }

    public IReadOnlyList<DestinationConfig> GetAll()
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<DestinationConfig>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];   // beschädigt/unlesbar → als leer behandeln (nicht crashen)
        }
    }

    public void Add(DestinationConfig config)
    {
        var all = GetAll().ToList();
        all.Add(config);
        Persist(all);
    }

    public void Remove(Guid id)
    {
        var all = GetAll().Where(c => c.Id != id).ToList();
        Persist(all);
    }

    public IBackupDestination Resolve(DestinationConfig config)
    {
        if (!_connectors.TryGetValue(config.Kind, out var connector))
            throw new InvalidOperationException($"Kein Connector für {config.Kind} registriert.");
        return connector.Create(config);
    }

    private void Persist(IReadOnlyList<DestinationConfig> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(all, JsonOptions));
    }
}
```

- [ ] **Step 5: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~DestinationStore"`
Expected: PASS (4 Tests).

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.App/Services/DestinationStore.cs src/Flippo.Data/AppPaths.cs tests/Flippo.Tests/Cloud/DestinationStoreTests.cs
git commit -m "C1: DestinationStore (destinations.json CRUD + Resolve)"
```

---

## Task 4: `CloudBackupService` (Orchestrierung + Retention, TDD)

**Files:**
- Create: `src/Flippo.App/Services/CloudBackupService.cs`
- Test: `tests/Flippo.Tests/Cloud/CloudBackupServiceTests.cs`

**Interfaces:**
- Consumes: `BackupService` (Data, `ExportAsync(Stream, SrsSettings?, long, ct)` + `ParseAsync(Stream, ct)`), `IBackupDestination`, `BackupFileInfo`, `Flippo.Core.Backup.BackupParseResult`, `Flippo.Core.Domain.SrsSettings`.
- Produces: `CloudBackupService(BackupService backup)` mit `const int KeepBackups = 10`, `Task<BackupFileInfo> BackupToDestinationAsync(IBackupDestination dest, SrsSettings? srs, long nowMs, ct)`, `Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(IBackupDestination dest, ct)`, `Task<BackupParseResult> DownloadAndParseAsync(IBackupDestination dest, string remoteId, ct)`.

- [ ] **Step 1: Failing test schreiben**

Create `tests/Flippo.Tests/Cloud/CloudBackupServiceTests.cs`:

```csharp
using Flippo.App.Services;
using Flippo.Cloud.Destinations;
using Flippo.Core.Domain;
using Flippo.Data.Services;
using Flippo.Tests.Data;

namespace Flippo.Tests.Cloud;

public class CloudBackupServiceTests
{
    private static (CloudBackupService svc, LocalFolderDestination dest, string dir) Setup(SqliteTestDatabase db)
    {
        var backupsDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new CloudBackupService(new BackupService(db.Factory, backupsDir));
        var dir = Directory.CreateTempSubdirectory().FullName;
        var dest = new LocalFolderDestination(LocalFolderConnector.BuildConfig(dir, "T"));
        return (svc, dest, dir);
    }

    [Fact]
    public async Task Backup_Then_DownloadAndParse_RoundTrips()
    {
        using var db = new SqliteTestDatabase();
        var vocab = new VocabularyStore(db.Factory);
        await vocab.AddSetAsync(new VocabularySet { Title = "Reise", CreatedAt = 1, UpdatedAt = 1 });
        var (svc, dest, _) = Setup(db);

        await svc.BackupToDestinationAsync(dest, null, 1_000, CancellationToken.None);

        var list = await svc.ListBackupsAsync(dest);
        Assert.Single(list);
        var parsed = await svc.DownloadAndParseAsync(dest, list[0].RemoteId);
        Assert.Single(parsed.Content.Sets);
        Assert.Equal("Reise", parsed.Content.Sets[0].Title);
    }

    [Fact]
    public async Task Retention_KeepsLastTen()
    {
        using var db = new SqliteTestDatabase();
        var (svc, dest, _) = Setup(db);

        // 12 Backups, je 1 s auseinander → eindeutige Zeitstempel-Dateinamen
        for (int i = 0; i < 12; i++)
            await svc.BackupToDestinationAsync(dest, null, 1_000 + i * 1_000, CancellationToken.None);

        Assert.Equal(10, (await svc.ListBackupsAsync(dest)).Count);
    }
}
```

- [ ] **Step 2: Test läuft NICHT**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~CloudBackupService"`
Expected: Kompilierfehler `CloudBackupService` nicht gefunden.

- [ ] **Step 3: `CloudBackupService` implementieren**

Create `src/Flippo.App/Services/CloudBackupService.cs`:

```csharp
using Flippo.Cloud.Abstractions;
using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Flippo.Data.Services;

namespace Flippo.App.Services;

/// <summary>
/// Verbindet den (Stream-basierten) BackupService mit einem IBackupDestination: Sichern (mit
/// Retention), Auflisten, Download+Parse. UI-frei — Preview/Import bleibt im SetActionsService.
/// </summary>
public sealed class CloudBackupService
{
    public const int KeepBackups = 10;

    private readonly BackupService _backup;

    public CloudBackupService(BackupService backup) => _backup = backup;

    public async Task<BackupFileInfo> BackupToDestinationAsync(
        IBackupDestination dest, SrsSettings? srs, long nowMs, CancellationToken ct = default)
    {
        using var mem = new MemoryStream();
        await _backup.ExportAsync(mem, srs, nowMs, ct);
        mem.Position = 0;

        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime.ToString("yyyyMMdd-HHmmss");
        var info = await dest.UploadAsync($"flippo-backup-{timestamp}.json", mem, ct);

        await PruneAsync(dest, ct);
        return info;
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(IBackupDestination dest, CancellationToken ct = default)
        => dest.ListBackupsAsync(ct);

    public async Task<BackupParseResult> DownloadAndParseAsync(
        IBackupDestination dest, string remoteId, CancellationToken ct = default)
    {
        await using var stream = await dest.DownloadAsync(remoteId, ct);
        return await _backup.ParseAsync(stream, ct);
    }

    /// <summary>Behält die neuesten <see cref="KeepBackups"/>; Zeitstempel steckt im Dateinamen
    /// (lexikografisch = chronologisch), daher nach FileName absteigend sortieren.</summary>
    private async Task PruneAsync(IBackupDestination dest, CancellationToken ct)
    {
        var stale = (await dest.ListBackupsAsync(ct))
            .OrderByDescending(b => b.FileName, StringComparer.Ordinal)
            .Skip(KeepBackups)
            .ToList();
        foreach (var b in stale)
            await dest.DeleteAsync(b.RemoteId, ct);
    }
}
```

- [ ] **Step 4: Test läuft grün**

Run: `dotnet test tests/Flippo.Tests/Flippo.Tests.csproj --filter "FullyQualifiedName~CloudBackupService"`
Expected: PASS (2 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/Services/CloudBackupService.cs tests/Flippo.Tests/Cloud/CloudBackupServiceTests.cs
git commit -m "C1: CloudBackupService (Sichern/Auflisten/Parse + Retention)"
```

---

## Task 5: Folder-Picker + Backup-Auswahl-Dialog

**Files:**
- Modify: `src/Flippo.App/Services/FilePickerService.cs`
- Modify: `src/Flippo.App/Services/DialogService.cs`
- Create: `src/Flippo.App/Views/BackupChooserWindow.axaml`
- Create: `src/Flippo.App/Views/BackupChooserWindow.axaml.cs`

**Interfaces:**
- Produces: `IFilePickerService.PickFolderAsync(string title) : Task<string?>`; `IDialogService.ShowBackupChooserAsync(IReadOnlyList<BackupFileInfo>) : Task<BackupFileInfo?>`.

- [ ] **Step 1: `PickFolderAsync` zum Interface + Impl**

In `src/Flippo.App/Services/FilePickerService.cs` im Interface `IFilePickerService` ergänzen:

```csharp
    /// <summary>Ordner-Auswahl → lokaler Pfad (C1 Backup-Ziel). null = abgebrochen.</summary>
    Task<string?> PickFolderAsync(string title);
```

In der Klasse `FilePickerService` ergänzen:

```csharp
    public async Task<string?> PickFolderAsync(string title)
    {
        var owner = _owner();
        if (owner is null) return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
```

- [ ] **Step 2: `BackupChooserWindow` anlegen**

Create `src/Flippo.App/Views/BackupChooserWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loc="using:Flippo.App.Localization"
        xmlns:cloud="using:Flippo.Cloud.Abstractions"
        x:Class="Flippo.App.Views.BackupChooserWindow"
        Title="{loc:T Dest_RestoreTitle}"
        Width="440" Height="460"
        WindowStartupLocation="CenterOwner"
        CanResize="False">
    <Grid Margin="24" RowDefinitions="Auto,*,Auto">
        <TextBlock Grid.Row="0" Text="{loc:T Dest_ChooseBackup}" FontSize="18" FontWeight="SemiBold" Margin="0,0,0,12"/>
        <ListBox Grid.Row="1" x:Name="BackupList" DoubleTapped="OnChoose">
            <ListBox.ItemTemplate>
                <DataTemplate x:DataType="cloud:BackupFileInfo">
                    <StackPanel Spacing="2" Margin="4">
                        <TextBlock Text="{Binding FileName}" FontWeight="SemiBold"/>
                        <TextBlock FontSize="12" Opacity="0.6">
                            <Run Text="{Binding CreatedAt, StringFormat={}{0:g}}"/>
                        </TextBlock>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" Margin="0,12,0,0">
            <Button Content="{loc:T SetEditor_Cancel}" Click="OnCancel" MinWidth="90"/>
            <Button Content="{loc:T Dest_Restore}" Click="OnChoose" MinWidth="90" Classes="accent"/>
        </StackPanel>
    </Grid>
</Window>
```

Create `src/Flippo.App/Views/BackupChooserWindow.axaml.cs`:

```csharp
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.Cloud.Abstractions;

namespace Flippo.App.Views;

/// <summary>Auswahl eines vorhandenen Backups am Ziel. Rückgabe null = abgebrochen.</summary>
public partial class BackupChooserWindow : Window
{
    public BackupChooserWindow() => InitializeComponent();

    public BackupChooserWindow(IReadOnlyList<BackupFileInfo> backups) : this()
    {
        BackupList.ItemsSource = backups;
        if (backups.Count > 0) BackupList.SelectedIndex = 0;
    }

    private void OnChoose(object? sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is BackupFileInfo info) Close(info);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
```

- [ ] **Step 3: DialogService-Methode ergänzen**

In `src/Flippo.App/Services/DialogService.cs` im Interface `IDialogService` ergänzen (Datei oben braucht `using Flippo.Cloud.Abstractions;`):

```csharp
    /// <summary>Auswahl eines vorhandenen Backups am Ziel (C1). Rückgabe null = abgebrochen.</summary>
    Task<BackupFileInfo?> ShowBackupChooserAsync(IReadOnlyList<BackupFileInfo> backups);
```

In der Klasse `DialogService` ergänzen:

```csharp
    public async Task<BackupFileInfo?> ShowBackupChooserAsync(IReadOnlyList<BackupFileInfo> backups)
    {
        var owner = _owner();
        if (owner is null) return null;

        var window = new BackupChooserWindow(backups);
        return await window.ShowDialog<BackupFileInfo?>(owner);
    }
```

- [ ] **Step 4: Build verifizieren**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/Services/FilePickerService.cs src/Flippo.App/Services/DialogService.cs src/Flippo.App/Views/BackupChooserWindow.axaml src/Flippo.App/Views/BackupChooserWindow.axaml.cs
git commit -m "C1: Folder-Picker + Backup-Auswahl-Dialog"
```

---

## Task 6: „Backup-Ziele"-UI (Verwaltung)

**Files:**
- Create: `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs`
- Modify: `src/Flippo.App/Services/SetActionsService.cs` (Ziel-Export/-Restore + Refactor)
- Modify: `src/Flippo.App/ViewModels/SettingsViewModel.cs` (`Destinations`-Property)
- Modify: `src/Flippo.App/Views/SettingsView.axaml` (Sektion)
- Modify: `src/Flippo.App/App.axaml.cs` (DI)
- Modify: `src/Flippo.App/Resources/Strings.resx` + `Strings.de.resx`

**Interfaces:**
- Consumes: `DestinationStore`, `IFilePickerService`, `IDialogService`, `SetActionsService`, `DestinationConfig`, `LocalFolderConnector`.
- Produces: `BackupDestinationsViewModel` mit `ObservableCollection<DestinationConfig> Destinations`, `bool HasDestinations`, Commands `AddFolder`, `Remove(DestinationConfig?)`, `Backup(DestinationConfig?)`, `Restore(DestinationConfig?)`; `SetActionsService.ExportToDestinationAsync(DestinationConfig)` (Task 7 füllt Logik) — hier nur Verwaltung.

> **Hinweis Reihenfolge:** Dieser Task baut die Verwaltung (Hinzufügen/Entfernen/Liste). Die Backup-/Restore-Commands rufen `SetActionsService`-Methoden, die in **Task 7** implementiert werden. Damit Task 6 baubar und testbar bleibt, werden die zwei `SetActionsService`-Methoden hier als Stubs angelegt (Task 7 füllt sie).

- [ ] **Step 1: `SetActionsService`-Stubs für Ziel-Aktionen anlegen**

In `src/Flippo.App/Services/SetActionsService.cs` Konstruktor + Felder um zwei Abhängigkeiten erweitern und zwei Methoden-Stubs ergänzen. Zunächst die Felder + Ctor (`CloudBackupService` + `DestinationStore` ergänzen):

```csharp
    private readonly CloudBackupService _cloud;
    private readonly DestinationStore _destinations;
```

Ctor-Signatur um `CloudBackupService cloud, DestinationStore destinations` erweitern und zuweisen (`_cloud = cloud; _destinations = destinations;`).

Dann die zwei Methoden (Logik folgt in Task 7):

```csharp
    /// <summary>Sichert ein Backup zum gewählten Ziel (Task 7).</summary>
    public Task<bool> ExportToDestinationAsync(DestinationConfig config) => throw new NotImplementedException();

    /// <summary>Listet Backups des Ziels, lässt auswählen und stellt wieder her (Task 7).</summary>
    public Task<bool> RestoreFromDestinationAsync(DestinationConfig config) => throw new NotImplementedException();
```

Datei oben: `using Flippo.Cloud.Abstractions;`.

- [ ] **Step 2: `BackupDestinationsViewModel` schreiben**

Create `src/Flippo.App/ViewModels/BackupDestinationsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Abstractions;
using Flippo.Cloud.Destinations;

namespace Flippo.App.ViewModels;

/// <summary>„Backup-Ziele"-Sektion der Einstellungen: Ordner-Ziele verwalten + sichern/wiederherstellen.</summary>
public sealed partial class BackupDestinationsViewModel : ViewModelBase
{
    private readonly DestinationStore _store;
    private readonly IFilePickerService _picker;
    private readonly IDialogService _dialogs;
    private readonly SetActionsService _actions;

    public ObservableCollection<DestinationConfig> Destinations { get; } = new();
    [ObservableProperty] private bool _hasDestinations;

    public BackupDestinationsViewModel(DestinationStore store, IFilePickerService picker,
        IDialogService dialogs, SetActionsService actions)
    {
        _store = store;
        _picker = picker;
        _dialogs = dialogs;
        _actions = actions;
        Reload();
    }

    private void Reload()
    {
        Destinations.Clear();
        foreach (var c in _store.GetAll()) Destinations.Add(c);
        HasDestinations = Destinations.Count > 0;
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        var path = await _picker.PickFolderAsync(L.T("Dest_PickFolderTitle"));
        if (string.IsNullOrWhiteSpace(path)) return;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) name = path;
        _store.Add(LocalFolderConnector.BuildConfig(path, name));
        Reload();
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
}
```

- [ ] **Step 3: `SettingsViewModel` um `Destinations` erweitern**

In `src/Flippo.App/ViewModels/SettingsViewModel.cs`:
- Ctor-Signatur um `BackupDestinationsViewModel destinations` erweitern.
- Property ergänzen:

```csharp
    public BackupDestinationsViewModel Destinations { get; }
```

- Im Ctor zuweisen: `Destinations = destinations;`

- [ ] **Step 4: „Backup-Ziele"-Sektion in `SettingsView.axaml`**

In `src/Flippo.App/Views/SettingsView.axaml` eine Sektion einfügen (am Ende des Einstellungs-Stacks, vor dem Speichern-Bereich). Falls die View `xmlns:cloud` noch nicht kennt, oben ergänzen: `xmlns:cloud="using:Flippo.Cloud.Abstractions"`.

```xml
<Border Classes="app-card" DataContext="{Binding Destinations}"
        x:DataType="vm:BackupDestinationsViewModel" Margin="0,8,0,0">
    <StackPanel Spacing="10">
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Grid.Column="0" Classes="section" Text="{loc:T Dest_SectionTitle}"/>
            <Button Grid.Column="1" Content="{loc:T Dest_AddFolder}" Command="{Binding AddFolderCommand}"/>
        </Grid>
        <TextBlock Classes="caption" Text="{loc:T Dest_SectionHint}"/>

        <ItemsControl ItemsSource="{Binding Destinations}">
            <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="cloud:DestinationConfig">
                    <Border BorderBrush="{DynamicResource Brush.Border.Subtle}" BorderThickness="1"
                            CornerRadius="{DynamicResource Radius.Control}" Padding="12" Margin="0,6,0,0">
                        <Grid ColumnDefinitions="*,Auto">
                            <StackPanel Grid.Column="0" Spacing="2" VerticalAlignment="Center">
                                <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold"/>
                                <TextBlock Text="{Binding Kind}" FontSize="12" Opacity="0.6"/>
                            </StackPanel>
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6">
                                <Button Content="{loc:T Dest_Backup}"
                                        Command="{Binding $parent[ItemsControl].((vm:BackupDestinationsViewModel)DataContext).BackupCommand}"
                                        CommandParameter="{Binding}"/>
                                <Button Content="{loc:T Dest_Restore}"
                                        Command="{Binding $parent[ItemsControl].((vm:BackupDestinationsViewModel)DataContext).RestoreCommand}"
                                        CommandParameter="{Binding}"/>
                                <Button Content="✕" ToolTip.Tip="{loc:T Ctx_Delete}"
                                        Command="{Binding $parent[ItemsControl].((vm:BackupDestinationsViewModel)DataContext).RemoveCommand}"
                                        CommandParameter="{Binding}"/>
                            </StackPanel>
                        </Grid>
                    </Border>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <TextBlock IsVisible="{Binding !HasDestinations}" Classes="caption"
                   Text="{loc:T Dest_Empty}" Margin="0,4,0,0"/>
    </StackPanel>
</Border>
```

- [ ] **Step 5: DI-Registrierungen**

In `src/Flippo.App/App.axaml.cs` `ConfigureServices` ergänzen (Usings oben: `using Flippo.Cloud.Abstractions;`, `using Flippo.Cloud.Destinations;`):

```csharp
        services.AddSingleton<IDestinationConnector, LocalFolderConnector>();
        services.AddSingleton<DestinationStore>();
        services.AddSingleton<CloudBackupService>();
        services.AddTransient<BackupDestinationsViewModel>();
```

(Die `DestinationStore`-DI nutzt den parameterlosen Pfad `filePath = null` → `AppPaths.DestinationsFile`, und bekommt `IEnumerable<IDestinationConnector>` automatisch.)

- [ ] **Step 6: resx-Keys (EN + DE)**

In `src/Flippo.App/Resources/Strings.resx` vor `</root>` einfügen:

```xml
  <data name="Dest_SectionTitle" xml:space="preserve"><value>Backup destinations</value></data>
  <data name="Dest_SectionHint" xml:space="preserve"><value>Back up to a folder (e.g. a synced cloud drive). Cloud providers follow later.</value></data>
  <data name="Dest_AddFolder" xml:space="preserve"><value>Add folder…</value></data>
  <data name="Dest_Empty" xml:space="preserve"><value>No destinations yet.</value></data>
  <data name="Dest_Backup" xml:space="preserve"><value>Back up</value></data>
  <data name="Dest_Restore" xml:space="preserve"><value>Restore</value></data>
  <data name="Dest_PickFolderTitle" xml:space="preserve"><value>Choose backup folder</value></data>
  <data name="Dest_RemoveTitle" xml:space="preserve"><value>Remove destination</value></data>
  <data name="Dest_RemoveMsg" xml:space="preserve"><value>Remove the destination “{0}”? (Existing backup files are kept.)</value></data>
  <data name="Dest_RestoreTitle" xml:space="preserve"><value>Restore backup</value></data>
  <data name="Dest_ChooseBackup" xml:space="preserve"><value>Choose a backup to restore</value></data>
  <data name="Dest_NoBackups" xml:space="preserve"><value>No backups found at this destination.</value></data>
  <data name="Dest_BackupDoneTitle" xml:space="preserve"><value>Backup saved</value></data>
  <data name="Dest_BackupDoneMsg" xml:space="preserve"><value>“{0}” saved to “{1}”.</value></data>
  <data name="Dest_ErrorTitle" xml:space="preserve"><value>Backup destination error</value></data>
  <data name="Dest_ErrNotConnected" xml:space="preserve"><value>The folder is not reachable. Has it been moved or deleted?</value></data>
  <data name="Dest_ErrTransport" xml:space="preserve"><value>The backup could not be transferred. Please try again.</value></data>
```

In `src/Flippo.App/Resources/Strings.de.resx` vor `</root>` einfügen:

```xml
  <data name="Dest_SectionTitle" xml:space="preserve"><value>Backup-Ziele</value></data>
  <data name="Dest_SectionHint" xml:space="preserve"><value>In einen Ordner sichern (z.B. ein synchronisierter Cloud-Ordner). Cloud-Anbieter folgen später.</value></data>
  <data name="Dest_AddFolder" xml:space="preserve"><value>Ordner hinzufügen…</value></data>
  <data name="Dest_Empty" xml:space="preserve"><value>Noch keine Ziele.</value></data>
  <data name="Dest_Backup" xml:space="preserve"><value>Sichern</value></data>
  <data name="Dest_Restore" xml:space="preserve"><value>Wiederherstellen</value></data>
  <data name="Dest_PickFolderTitle" xml:space="preserve"><value>Backup-Ordner wählen</value></data>
  <data name="Dest_RemoveTitle" xml:space="preserve"><value>Ziel entfernen</value></data>
  <data name="Dest_RemoveMsg" xml:space="preserve"><value>Ziel „{0}“ entfernen? (Vorhandene Backup-Dateien bleiben erhalten.)</value></data>
  <data name="Dest_RestoreTitle" xml:space="preserve"><value>Backup wiederherstellen</value></data>
  <data name="Dest_ChooseBackup" xml:space="preserve"><value>Backup zum Wiederherstellen wählen</value></data>
  <data name="Dest_NoBackups" xml:space="preserve"><value>Keine Backups an diesem Ziel gefunden.</value></data>
  <data name="Dest_BackupDoneTitle" xml:space="preserve"><value>Backup gesichert</value></data>
  <data name="Dest_BackupDoneMsg" xml:space="preserve"><value>„{0}“ nach „{1}“ gesichert.</value></data>
  <data name="Dest_ErrorTitle" xml:space="preserve"><value>Fehler beim Backup-Ziel</value></data>
  <data name="Dest_ErrNotConnected" xml:space="preserve"><value>Der Ordner ist nicht erreichbar. Wurde er verschoben oder gelöscht?</value></data>
  <data name="Dest_ErrTransport" xml:space="preserve"><value>Das Backup konnte nicht übertragen werden. Bitte erneut versuchen.</value></data>
```

- [ ] **Step 7: Build verifizieren**

Run: `dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler.

- [ ] **Step 8: Commit**

```bash
git add src/Flippo.App/ src/Flippo.App/Resources/
git commit -m "C1: Backup-Ziele-Verwaltung in den Einstellungen (Ordner hinzufügen/entfernen)"
```

---

## Task 7: Sichern & Wiederherstellen gegen ein Ziel

**Files:**
- Modify: `src/Flippo.App/Services/SetActionsService.cs` (Stubs füllen + `CompleteImportAsync`-Refactor)

**Interfaces:**
- Consumes: `CloudBackupService`, `DestinationStore`, `IDialogService.ShowBackupChooserAsync`, `IDialogService.ShowImportPreviewAsync`, `BackupService.ImportAsync`, `DestinationException`, `SettingsService.ToSrsSettings`.
- Produces: gefüllte `ExportToDestinationAsync` + `RestoreFromDestinationAsync`; privates `CompleteImportAsync(BackupParseResult) : Task<bool>`.

- [ ] **Step 1: Gemeinsamen Import-Abschluss extrahieren**

In `src/Flippo.App/Services/SetActionsService.cs` den Preview→Import→Settings→Summary-Teil aus `ImportBackupAsync` in eine private Methode ziehen und `ImportBackupAsync` sie aufrufen lassen:

```csharp
    /// <summary>Gemeinsamer Abschluss für Datei- und Ziel-Restore: Preview → Import → Settings → Summary.</summary>
    private async Task<bool> CompleteImportAsync(BackupParseResult parsed)
    {
        var confirm = await _dialogs.ShowImportPreviewAsync(parsed);
        if (confirm is null) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _backup.ImportAsync(parsed.Content, writeSafetyExport: true, now);

        if (confirm.ApplySettings && parsed.Content.Settings is not null)
        {
            var updated = SettingsService.WithSrs(_settings.Load(), parsed.Content.Settings);
            _settings.Save(updated);
        }

        var message = string.Format(L.T("SetsVm_ImportSummary"), result.SetsImported, result.EntriesImported, result.SessionsImported);
        if (result.EntriesSkipped > 0)
            message += "\n" + string.Format(L.T("SetsVm_ImportSkipped"), result.EntriesSkipped);
        await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportDoneTitle"), message);
        return true;
    }
```

`ImportBackupAsync` unten ersetzen (ab `var confirm = ...`) durch:

```csharp
        return await CompleteImportAsync(parsed);
```

(Die `BackupFormatException`-Behandlung beim `ParseAsync` bleibt unverändert davor stehen.)

- [ ] **Step 2: `ExportToDestinationAsync` füllen**

Den Stub aus Task 6 ersetzen:

```csharp
    public async Task<bool> ExportToDestinationAsync(DestinationConfig config)
    {
        try
        {
            var dest = _destinations.Resolve(config);
            var srs = SettingsService.ToSrsSettings(_settings.Load());
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var info = await _cloud.BackupToDestinationAsync(dest, srs, now);
            await _dialogs.ShowMessageAsync(L.T("Dest_BackupDoneTitle"),
                string.Format(L.T("Dest_BackupDoneMsg"), info.FileName, config.DisplayName));
            return true;
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
            return false;
        }
    }
```

- [ ] **Step 3: `RestoreFromDestinationAsync` füllen**

Den Stub ersetzen:

```csharp
    public async Task<bool> RestoreFromDestinationAsync(DestinationConfig config)
    {
        BackupParseResult parsed;
        try
        {
            var dest = _destinations.Resolve(config);
            var backups = await _cloud.ListBackupsAsync(dest);
            if (backups.Count == 0)
            {
                await _dialogs.ShowMessageAsync(L.T("Dest_RestoreTitle"), L.T("Dest_NoBackups"));
                return false;
            }

            var chosen = await _dialogs.ShowBackupChooserAsync(backups);
            if (chosen is null) return false;

            parsed = await _cloud.DownloadAndParseAsync(dest, chosen.RemoteId);
        }
        catch (DestinationException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("Dest_ErrorTitle"), DestErrorMessage(ex));
            return false;
        }
        catch (BackupFormatException ex)
        {
            await _dialogs.ShowMessageAsync(L.T("SetsVm_ImportFailedTitle"), ex.Message);
            return false;
        }

        return await CompleteImportAsync(parsed);
    }

    private static string DestErrorMessage(DestinationException ex) => ex.State switch
    {
        DestinationState.NotConnected => L.T("Dest_ErrNotConnected"),
        _ => L.T("Dest_ErrTransport")
    };
```

- [ ] **Step 4: Volle Testsuite grün (keine Regression)**

Run: `dotnet test`
Expected: PASS — 191 bestehende + 9 neue Cloud-Tests = **200** erfolgreich, 0 Fehler.

- [ ] **Step 5: Build + manueller Smoke**

Run: `taskkill //F //IM Flippo.App.exe 2>/dev/null; dotnet build src/Flippo.App/Flippo.App.csproj -c Debug`
Expected: 0 Warnungen, 0 Fehler. Dann App starten und manuell prüfen:
1. Einstellungen → „Backup-Ziele" → „Ordner hinzufügen" → Ordner wählen → Karte erscheint.
2. „Sichern" → `flippo-backup-*.json` liegt im Ordner, Erfolg-Meldung.
3. „Wiederherstellen" → Liste → Auswahl → Preview/Confirm → Daten wieder da.
4. Ordner im Explorer löschen → „Sichern" → nicht-blockierende Fehlermeldung „Ordner nicht erreichbar".
5. 11× sichern → nur 10 Dateien bleiben (Retention).

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.App/Services/SetActionsService.cs
git commit -m "C1: Sichern/Wiederherstellen gegen Backup-Ziel (LocalFolder-Durchstich komplett)"
```

---

## Self-Review

**Spec-Abdeckung:**
- Architektur/Projektstruktur (Spec §Architektur) → Task 1. ✓
- Kernabstraktion (§Kernabstraktion) → Task 1 (Typen) + Task 2 (LocalFolder-Impl). ✓
- Config-Persistenz `destinations.json`, kein TokenVault (§Config-Persistenz) → Task 3. ✓
- Orchestrierung `CloudBackupService` + Retention (§Orchestrierung) → Task 4. ✓
- UI „Backup-Ziele" + Folder-Picker (§UI-Anbindung) → Task 5 (Picker/Dialog) + Task 6 (Sektion). ✓
- Sichern/Wiederherstellen ziel-bewusst, selber Preview/Confirm (§UI-Anbindung) → Task 7. ✓
- Fehlerzustände `TransportFailed`/`NotConnected` (§Fehlerbehandlung) → Task 2 (wirft) + Task 6/7 (Meldung). ✓
- Tests: Roundtrip / Retention / Restore-reuse (§Tests) → Task 2 / Task 4 / Task 7 (volle Suite). ✓
- **Bewusst nicht:** OAuth, TokenVault, GDrive/OneDrive, FlippoCloud — in keinem Task. ✓ (gewollt)

**Platzhalter-Scan:** Task 6 legt bewusst zwei `SetActionsService`-Stubs an, die Task 7 füllt — beide Schritte sind mit vollständigem Code spezifiziert, kein offener Platzhalter im Endzustand. ✓

**Typ-Konsistenz:** `BackupFileInfo(RemoteId, FileName, CreatedAt, SizeBytes)`, `DestinationConfig(Id, Kind, DisplayName, Settings)`, `LocalFolderConnector.BuildConfig(folderPath, displayName)`, `CloudBackupService.BackupToDestinationAsync(dest, srs, nowMs, ct)`, `DestinationStore.Resolve/GetAll/Add/Remove` — über Tasks 1–7 einheitlich verwendet. ✓
