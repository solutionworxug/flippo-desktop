using Flippo.Core.Domain;
using Flippo.Data.Entities;

namespace Flippo.Data.Services;

/// <summary>Handgeschriebenes Mapping Entity ↔ Core-Domain (Plan 4.1).</summary>
public static class EntityMapping
{
    public static VocabularyEntry ToDomain(this VocabularyEntryEntity e) => new()
    {
        Id = e.Id,
        SetId = e.SetId,
        SourceText = e.SourceText,
        TargetText = e.TargetText,
        AcceptedAnswers = e.AcceptedAnswers.ToList(),
        ExampleSentence = e.ExampleSentence,
        Notes = e.Notes,
        PartOfSpeech = e.PartOfSpeech,
        Gender = e.Gender,
        PluralForm = e.PluralForm,
        VerbForms = e.VerbForms,
        Pronunciation = e.Pronunciation,
        Tags = e.Tags.ToList(),
        Mnemonic = e.Mnemonic,
        ImagePath = e.ImagePath,
        AudioPath = e.AudioPath,
        Difficulty = e.Difficulty,
        BoxLevel = e.BoxLevel,
        NextReviewAt = e.NextReviewAt,
        CorrectCount = e.CorrectCount,
        WrongCount = e.WrongCount,
        LastReviewedAt = e.LastReviewedAt,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        IsArchived = e.IsArchived,
        IsLeech = e.IsLeech,
        LastIntervalDays = e.LastIntervalDays
    };

    public static VocabularyEntryEntity ToEntity(this VocabularyEntry d) => new()
    {
        Id = d.Id,
        SetId = d.SetId,
        SourceText = d.SourceText,
        TargetText = d.TargetText,
        AcceptedAnswers = d.AcceptedAnswers.ToList(),
        ExampleSentence = d.ExampleSentence,
        Notes = d.Notes,
        PartOfSpeech = d.PartOfSpeech,
        Gender = d.Gender,
        PluralForm = d.PluralForm,
        VerbForms = d.VerbForms,
        Pronunciation = d.Pronunciation,
        Tags = d.Tags.ToList(),
        Mnemonic = d.Mnemonic,
        ImagePath = d.ImagePath,
        AudioPath = d.AudioPath,
        Difficulty = d.Difficulty,
        BoxLevel = d.BoxLevel,
        NextReviewAt = d.NextReviewAt,
        CorrectCount = d.CorrectCount,
        WrongCount = d.WrongCount,
        LastReviewedAt = d.LastReviewedAt,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        IsArchived = d.IsArchived,
        IsLeech = d.IsLeech,
        LastIntervalDays = d.LastIntervalDays
    };

    public static VocabularySet ToDomain(this VocabularySetEntity e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        SourceLanguage = e.SourceLanguage,
        TargetLanguage = e.TargetLanguage,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    public static VocabularySetEntity ToEntity(this VocabularySet d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Description = d.Description,
        SourceLanguage = d.SourceLanguage,
        TargetLanguage = d.TargetLanguage,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };

    public static SessionRecord ToDomain(this SessionRecordEntity e) => new()
    {
        Id = e.Id,
        SetId = e.SetId,
        SetName = e.SetName,
        CorrectCount = e.CorrectCount,
        WrongCount = e.WrongCount,
        StartedAt = e.StartedAt,
        WrongEntryIds = e.WrongEntryIds,
        DurationMinutes = e.DurationMinutes,
        LearningMode = e.LearnMode   // Room-Feld learnMode → Domain learningMode
    };

    public static SessionRecordEntity ToEntity(this SessionRecord d) => new()
    {
        Id = d.Id,
        SetId = d.SetId,
        SetName = d.SetName,
        CorrectCount = d.CorrectCount,
        WrongCount = d.WrongCount,
        StartedAt = d.StartedAt,
        WrongEntryIds = d.WrongEntryIds,
        DurationMinutes = d.DurationMinutes,
        LearnMode = d.LearningMode
    };

    // ── Nachschlagewerk (P13) ──

    public static UserDictionary ToDomain(this UserDictionaryEntity e, int entryCount = 0) => new()
    {
        Id = e.Id,
        Name = e.Name,
        SourceLanguage = e.SourceLanguage,
        TargetLanguage = e.TargetLanguage,
        EntryCount = entryCount,
        CreatedAt = e.CreatedAt
    };

    public static UserDictionaryEntity ToEntity(this UserDictionary d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        SourceLanguage = d.SourceLanguage,
        TargetLanguage = d.TargetLanguage,
        CreatedAt = d.CreatedAt
    };

    public static UserDictionaryEntry ToDomain(this UserDictionaryEntryEntity e) => new()
    {
        Id = e.Id,
        DictionaryId = e.DictionaryId,
        SourceWord = e.SourceWord,
        TargetWord = e.TargetWord,
        PartOfSpeech = e.PartOfSpeech,
        Gender = e.Gender,
        ExampleSentence = e.ExampleSentence,
        ExampleTranslation = e.ExampleTranslation,
        Level = e.Level,
        AcceptedAnswers = e.AcceptedAnswers.ToList()
    };

    public static UserDictionaryEntryEntity ToEntity(this UserDictionaryEntry d) => new()
    {
        Id = d.Id,
        DictionaryId = d.DictionaryId,
        SourceWord = d.SourceWord,
        TargetWord = d.TargetWord,
        PartOfSpeech = d.PartOfSpeech,
        Gender = d.Gender,
        ExampleSentence = d.ExampleSentence,
        ExampleTranslation = d.ExampleTranslation,
        Level = d.Level,
        AcceptedAnswers = d.AcceptedAnswers.ToList()
    };
}
