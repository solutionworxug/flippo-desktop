namespace Flippo.Data.Entities;

/// <summary>Room-Spiegel von VocabularySetEntity.kt (Tabelle "vocabulary_sets").</summary>
public class VocabularySetEntity
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceLanguage { get; set; } = "";
    public string TargetLanguage { get; set; } = "";
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    /// <summary>Navigation (EF); FK Entry→Set mit Cascade.</summary>
    public List<VocabularyEntryEntity> Entries { get; set; } = new();
}
