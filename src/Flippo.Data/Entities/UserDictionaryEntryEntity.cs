namespace Flippo.Data.Entities;

/// <summary>Room-Spiegel von UserDictionaryEntryEntity.kt (Tabelle "user_dictionary_entries").</summary>
public class UserDictionaryEntryEntity
{
    public long Id { get; set; }
    public long DictionaryId { get; set; }
    public string SourceWord { get; set; } = "";
    public string TargetWord { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    public string Gender { get; set; } = "";
    public string ExampleSentence { get; set; } = "";
    public string ExampleTranslation { get; set; } = "";
    public string Level { get; set; } = "";

    /// <summary>Wie Android als JSON-String-Spalte (ValueConverter im DbContext).</summary>
    public List<string> AcceptedAnswers { get; set; } = new();

    public UserDictionaryEntity? Dictionary { get; set; }
}
