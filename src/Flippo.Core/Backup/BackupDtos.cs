using System.Text.Json.Serialization;

namespace Flippo.Core.Backup;

// DTOs = wörtlicher JSON-Kontrakt (Gson-Serialisierung der Android-Domain-Klassen, version 2).
// [JsonPropertyName] auf JEDER Property → driftfest. Enums als rohe Strings (Mapping im BackupMapper).
// Nullables (int?, long?, Referenztypen) werden beim Export weggelassen (WhenWritingNull) — wie Gson.

public sealed class BackupDataDto
{
    [JsonPropertyName("version")] public int Version { get; set; } = 2;
    [JsonPropertyName("createdAt")] public long CreatedAt { get; set; }
    [JsonPropertyName("sets")] public List<BackupSetDto> Sets { get; set; } = new();
    [JsonPropertyName("entries")] public List<BackupEntryDto> Entries { get; set; } = new();
    [JsonPropertyName("sessionRecords")] public List<BackupSessionRecordDto> SessionRecords { get; set; } = new();
    [JsonPropertyName("srsSettings")] public BackupSrsSettingsDto? SrsSettings { get; set; }
}

public sealed class BackupSetDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("sourceLanguage")] public string SourceLanguage { get; set; } = "";
    [JsonPropertyName("targetLanguage")] public string TargetLanguage { get; set; } = "";
    [JsonPropertyName("createdAt")] public long CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; set; }
}

public sealed class BackupEntryDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("setId")] public long SetId { get; set; }
    [JsonPropertyName("sourceText")] public string SourceText { get; set; } = "";
    [JsonPropertyName("targetText")] public string TargetText { get; set; } = "";
    [JsonPropertyName("acceptedAnswers")] public List<string> AcceptedAnswers { get; set; } = new();
    [JsonPropertyName("exampleSentence")] public string ExampleSentence { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("partOfSpeech")] public string PartOfSpeech { get; set; } = "";
    [JsonPropertyName("gender")] public string Gender { get; set; } = "";
    [JsonPropertyName("pluralForm")] public string PluralForm { get; set; } = "";
    [JsonPropertyName("verbForms")] public string VerbForms { get; set; } = "";
    [JsonPropertyName("pronunciation")] public string Pronunciation { get; set; } = "";
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("mnemonic")] public string Mnemonic { get; set; } = "";
    [JsonPropertyName("imagePath")] public string ImagePath { get; set; } = "";
    [JsonPropertyName("audioPath")] public string AudioPath { get; set; } = "";
    [JsonPropertyName("difficulty")] public int Difficulty { get; set; }
    [JsonPropertyName("boxLevel")] public int BoxLevel { get; set; }
    [JsonPropertyName("nextReviewAt")] public long NextReviewAt { get; set; }
    [JsonPropertyName("correctCount")] public int CorrectCount { get; set; }
    [JsonPropertyName("wrongCount")] public int WrongCount { get; set; }
    [JsonPropertyName("lastReviewedAt")] public long LastReviewedAt { get; set; }
    [JsonPropertyName("createdAt")] public long CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; set; }
    [JsonPropertyName("isArchived")] public bool IsArchived { get; set; }
    [JsonPropertyName("isLeech")] public bool IsLeech { get; set; }
    [JsonPropertyName("lastIntervalDays")] public int? LastIntervalDays { get; set; }
}

public sealed class BackupSessionRecordDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("setId")] public long? SetId { get; set; }
    [JsonPropertyName("setName")] public string SetName { get; set; } = "";
    [JsonPropertyName("correctCount")] public int CorrectCount { get; set; }
    [JsonPropertyName("wrongCount")] public int WrongCount { get; set; }
    [JsonPropertyName("startedAt")] public long StartedAt { get; set; }
    [JsonPropertyName("wrongEntryIds")] public string WrongEntryIds { get; set; } = "";
    [JsonPropertyName("durationMinutes")] public int DurationMinutes { get; set; }
    [JsonPropertyName("learningMode")] public string LearningMode { get; set; } = "FREE_TEXT";
}

public sealed class BackupSrsSettingsDto
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "ADAPTIVE";
    [JsonPropertyName("boxIntervals")] public List<int> BoxIntervals { get; set; } = new();
    [JsonPropertyName("strictAccents")] public bool StrictAccents { get; set; }
    [JsonPropertyName("typoToleranceEnabled")] public bool TypoToleranceEnabled { get; set; }
    [JsonPropertyName("leechThreshold")] public int LeechThreshold { get; set; }
    [JsonPropertyName("learningDirection")] public string LearningDirection { get; set; } = "SOURCE_TO_TARGET";
    [JsonPropertyName("maxCardsPerSession")] public int MaxCardsPerSession { get; set; }
    [JsonPropertyName("maxNewCardsPerDay")] public int MaxNewCardsPerDay { get; set; }
}
