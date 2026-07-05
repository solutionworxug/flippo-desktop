namespace Flippo.Core.Domain;

/// <summary>Port von UserDictionary.kt — ein Nachschlagewerk (gerätelokal, NICHT im Backup).</summary>
public sealed record UserDictionary
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string SourceLanguage { get; init; } = "";
    public string TargetLanguage { get; init; } = "";
    public int EntryCount { get; init; }
    public long CreatedAt { get; init; }
}

/// <summary>Port von UserDictionaryEntry.kt — ein Wörterbuch-Eintrag.</summary>
public sealed record UserDictionaryEntry
{
    public long Id { get; init; }
    public long DictionaryId { get; init; }
    public string SourceWord { get; init; } = "";
    public string TargetWord { get; init; } = "";
    public string PartOfSpeech { get; init; } = "";
    public string Gender { get; init; } = "";
    public string ExampleSentence { get; init; } = "";
    public string ExampleTranslation { get; init; } = "";
    public string Level { get; init; } = "";
    public IReadOnlyList<string> AcceptedAnswers { get; init; } = [];
}
