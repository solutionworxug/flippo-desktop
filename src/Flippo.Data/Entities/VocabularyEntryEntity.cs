namespace Flippo.Data.Entities;

/// <summary>
/// Room-Spiegel von VocabularyEntryEntity.kt (Tabelle "vocabulary_entries").
/// <c>AcceptedAnswers</c>/<c>Tags</c> werden per ValueConverter als JSON-String-Spalte gespeichert.
/// <c>Difficulty</c>-Default 250 = Entity-Default (nicht der Domain-Sentinel 0).
/// </summary>
public class VocabularyEntryEntity
{
    public long Id { get; set; }
    public long SetId { get; set; }
    public string SourceText { get; set; } = "";
    public string TargetText { get; set; } = "";
    public List<string> AcceptedAnswers { get; set; } = new();
    public string ExampleSentence { get; set; } = "";
    public string Notes { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    public string Gender { get; set; } = "";
    public string PluralForm { get; set; } = "";
    public string VerbForms { get; set; } = "";
    public string Pronunciation { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Mnemonic { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string AudioPath { get; set; } = "";
    public int Difficulty { get; set; } = 250;
    public int BoxLevel { get; set; } = 1;
    public long NextReviewAt { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public long LastReviewedAt { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public bool IsLeech { get; set; }
    public int? LastIntervalDays { get; set; }

    public VocabularySetEntity? Set { get; set; }
}
