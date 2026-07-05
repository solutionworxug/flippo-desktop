namespace Flippo.Data.Entities;

/// <summary>Room-Spiegel von UserDictionaryEntity.kt (Tabelle "user_dictionaries").</summary>
public class UserDictionaryEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string SourceLanguage { get; set; } = "";
    public string TargetLanguage { get; set; } = "";
    public long CreatedAt { get; set; }

    /// <summary>Navigation (EF); FK Entry→Dictionary mit Cascade.</summary>
    public List<UserDictionaryEntryEntity> Entries { get; set; } = new();
}
