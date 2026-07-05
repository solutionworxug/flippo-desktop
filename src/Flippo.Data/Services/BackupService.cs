using Flippo.Core.Backup;
using Flippo.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flippo.Data.Services;

public sealed record BackupImportResult(
    int SetsImported,
    int EntriesImported,
    int SessionsImported,
    int EntriesSkipped,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Export/Import des Backup-JSON. Import = Full-Wipe wie Android, aber abgesichert:
/// Safety-Export → eine Transaktion (Wipe → Sets mit ID-Mapping → Entries mit remapptem setId
/// [unbekanntes setId: überspringen statt FK-Crash] → SessionRecords unverändert).
/// </summary>
public sealed class BackupService
{
    private const int MaxSafetyExports = 10;

    private readonly IDbContextFactory<FlippoDbContext> _factory;
    private readonly string _backupsDirectory;

    public BackupService(IDbContextFactory<FlippoDbContext> factory, string? backupsDirectory = null)
    {
        _factory = factory;
        _backupsDirectory = backupsDirectory ?? AppPaths.BackupsDirectory;
    }

    // ---------------- Export ----------------

    public async Task ExportAsync(Stream target, SrsSettings? currentSettings, long nowMs, CancellationToken ct = default)
    {
        var content = await ReadContentAsync(currentSettings, ct);
        var dto = BackupMapper.ToDto(content, nowMs);
        await BackupSerializer.SerializeAsync(target, dto, ct);
    }

    // ---------------- Parse / Preview ----------------

    /// <summary>Parst ein Backup (für den Preview-Dialog). Wirft <see cref="BackupFormatException"/>.</summary>
    public async Task<BackupParseResult> ParseAsync(Stream source, CancellationToken ct = default)
    {
        var dto = await BackupSerializer.DeserializeAsync(source, ct);
        return BackupMapper.FromDto(dto);
    }

    // ---------------- Import (commit) ----------------

    public async Task<BackupImportResult> ImportAsync(
        BackupContent content,
        bool writeSafetyExport,
        long nowMs,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();

        if (writeSafetyExport)
            await WriteSafetyExportAsync(nowMs, ct);

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Full-Wipe (Kinder zuerst)
        await db.Entries.ExecuteDeleteAsync(ct);
        await db.Sets.ExecuteDeleteAsync(ct);
        await db.SessionRecords.ExecuteDeleteAsync(ct);

        // Sets mit ID-Mapping alt→neu
        var idMapping = new Dictionary<long, long>();
        foreach (var set in content.Sets)
        {
            var e = set.ToEntity();
            e.Id = 0;
            db.Sets.Add(e);
            await db.SaveChangesAsync(ct);   // neue Id materialisieren
            idMapping[set.Id] = e.Id;
        }

        // Entries mit remapptem setId — unbekanntes Set: überspringen (Android würde FK-crashen)
        int skipped = 0;
        foreach (var entry in content.Entries)
        {
            if (!idMapping.TryGetValue(entry.SetId, out var newSetId))
            {
                skipped++;
                continue;
            }
            var e = (entry with { Id = 0, SetId = newSetId }).ToEntity();
            db.Entries.Add(e);
        }
        await db.SaveChangesAsync(ct);

        // SessionRecords unverändert (wrongEntryIds wird NICHT remappt — spiegelt Android bewusst)
        foreach (var record in content.Sessions)
        {
            var e = (record with { Id = 0 }).ToEntity();
            db.SessionRecords.Add(e);
        }
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        if (skipped > 0)
            warnings.Add($"{skipped} Karte(n) mit unbekanntem Set übersprungen.");

        return new BackupImportResult(
            content.Sets.Count,
            content.Entries.Count - skipped,
            content.Sessions.Count,
            skipped,
            warnings);
    }

    // ---------------- intern ----------------

    private async Task<BackupContent> ReadContentAsync(SrsSettings? settings, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sets = await db.Sets.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);
        var entries = await db.Entries.AsNoTracking().OrderBy(e => e.Id).ToListAsync(ct);
        var sessions = await db.SessionRecords.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);

        return new BackupContent(
            sets.Select(s => s.ToDomain()).ToList(),
            entries.Select(e => e.ToDomain()).ToList(),
            sessions.Select(r => r.ToDomain()).ToList(),
            settings);
    }

    private async Task WriteSafetyExportAsync(long nowMs, CancellationToken ct)
    {
        Directory.CreateDirectory(_backupsDirectory);

        var content = await ReadContentAsync(null, ct);
        var dto = BackupMapper.ToDto(content, nowMs);
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(_backupsDirectory, $"pre-import-{timestamp}.json");

        await using (var fs = File.Create(path))
            await BackupSerializer.SerializeAsync(fs, dto, ct);

        PruneSafetyExports();
    }

    private void PruneSafetyExports()
    {
        var stale = Directory.GetFiles(_backupsDirectory, "pre-import-*.json")
            .OrderByDescending(f => f)   // Zeitstempel im Namen → lexikografisch = chronologisch
            .Skip(MaxSafetyExports)
            .ToList();
        foreach (var f in stale)
        {
            try { File.Delete(f); }
            catch (IOException) { /* best effort */ }
        }
    }
}
