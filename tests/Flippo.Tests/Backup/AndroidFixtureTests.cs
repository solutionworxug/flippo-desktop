using Flippo.Core.Backup;
using Flippo.Data.Services;
using Flippo.Tests.Data;

namespace Flippo.Tests.Backup;

/// <summary>
/// Verifikation gegen ein ECHTES Android-Backup unter <c>Fixtures/android-backup-v2.json</c>.
/// Die Datei enthält persönliche Vokabeldaten und ist bewusst NICHT eingecheckt (.gitignore) —
/// bei fehlender Datei (frischer Clone / CI) werden diese Tests übersprungen.
/// </summary>
public class AndroidFixtureTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "android-backup-v2.json");

    [Fact]
    public void RealAndroidBackup_ParsesWithoutError()
    {
        if (!File.Exists(FixturePath)) return;   // optionaler Integrationstest — ohne Fixture (CI/Clone) übersprungen

        var json = File.ReadAllText(FixturePath);
        var parsed = BackupMapper.FromDto(BackupSerializer.Deserialize(json));

        Assert.True(parsed.Content.Sets.Count > 0, "Erwartet mindestens 1 Set");
        Assert.True(parsed.Content.Entries.Count > 0, "Erwartet mindestens 1 Karte");
        Assert.Empty(parsed.Warnings);   // keine unbekannten Enums im echten Export
    }

    [Fact]
    public void RealAndroidBackup_ImportExportImport_SemanticallyStable()
    {
        if (!File.Exists(FixturePath)) return;   // optionaler Integrationstest — ohne Fixture (CI/Clone) übersprungen

        var json = File.ReadAllText(FixturePath);

        var parsed1 = BackupMapper.FromDto(BackupSerializer.Deserialize(json));
        var reJson = BackupSerializer.Serialize(BackupMapper.ToDto(parsed1.Content, parsed1.CreatedAt));
        var parsed2 = BackupMapper.FromDto(BackupSerializer.Deserialize(reJson));

        Assert.Equal(parsed1.Content.Sets.Count, parsed2.Content.Sets.Count);
        Assert.Equal(parsed1.Content.Entries.Count, parsed2.Content.Entries.Count);
        Assert.Equal(parsed1.Content.Sessions.Count, parsed2.Content.Sessions.Count);
    }

    [Fact]
    public async Task RealAndroidBackup_ImportsIntoDatabase_WithCorrectCounts()
    {
        if (!File.Exists(FixturePath)) return;

        using var db = new SqliteTestDatabase();
        var backupDir = Path.Combine(Path.GetTempPath(), $"flippo-fixture-{Guid.NewGuid():N}");
        var backup = new BackupService(db.Factory, backupDir);
        var store = new VocabularyStore(db.Factory);

        BackupParseResult parsed;
        await using (var fs = File.OpenRead(FixturePath))
            parsed = await backup.ParseAsync(fs);

        var result = await backup.ImportAsync(parsed.Content, writeSafetyExport: false, nowMs: 0);

        // Alle Karten haben ein gültiges Set → nichts übersprungen
        Assert.Equal(parsed.Content.Sets.Count, result.SetsImported);
        Assert.Equal(parsed.Content.Entries.Count, result.EntriesImported);
        Assert.Equal(0, result.EntriesSkipped);

        // Daten tatsächlich in der DB (End-to-End des P4-Daten-Pfades)
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sets = await store.GetSetsWithCountsAsync(now);
        Assert.Equal(parsed.Content.Sets.Count, sets.Count);
        Assert.Equal(parsed.Content.Entries.Count, sets.Sum(s => s.TotalCards));
    }
}
