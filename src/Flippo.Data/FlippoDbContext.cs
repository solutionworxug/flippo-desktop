using System.Text.Json;
using Flippo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flippo.Data;

/// <summary>
/// EF-Core-Kontext. Schema spiegelt Room v9 in EF-Standardbenennung (die DB ist KEIN
/// Interop-Artefakt — Interop läuft ausschließlich über das Backup-JSON).
/// </summary>
public class FlippoDbContext : DbContext
{
    public FlippoDbContext(DbContextOptions<FlippoDbContext> options) : base(options) { }

    public DbSet<VocabularySetEntity> Sets => Set<VocabularySetEntity>();
    public DbSet<VocabularyEntryEntity> Entries => Set<VocabularyEntryEntity>();
    public DbSet<SessionRecordEntity> SessionRecords => Set<SessionRecordEntity>();

    private static readonly JsonSerializerOptions ListJsonOptions = new(JsonSerializerDefaults.General);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // List<string> ↔ JSON-String-Spalte (wie Android; nicht normalisiert, keine Queries darüber).
        var listConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, ListJsonOptions),
            v => DeserializeList(v));

        var listComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        modelBuilder.Entity<VocabularySetEntity>(e =>
        {
            e.ToTable("vocabulary_sets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<VocabularyEntryEntity>(e =>
        {
            e.ToTable("vocabulary_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();

            e.Property(x => x.AcceptedAnswers).HasConversion(listConverter, listComparer);
            e.Property(x => x.Tags).HasConversion(listConverter, listComparer);

            e.HasOne(x => x.Set)
                .WithMany(s => s.Entries)
                .HasForeignKey(x => x.SetId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.SetId);
            e.HasIndex(x => x.NextReviewAt);
            e.HasIndex(x => x.IsArchived);
            e.HasIndex(x => x.IsLeech);
        });

        modelBuilder.Entity<SessionRecordEntity>(e =>
        {
            e.ToTable("session_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            // Bewusst KEIN FK auf Set (spiegelt Room: setId nullable, kein Cascade).
        });
    }

    private static List<string> DeserializeList(string json)
        => string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json, ListJsonOptions) ?? new List<string>();
}
