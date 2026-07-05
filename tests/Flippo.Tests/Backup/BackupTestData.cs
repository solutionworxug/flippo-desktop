using Flippo.Core.Backup;
using Flippo.Core.Domain;

namespace Flippo.Tests.Backup;

/// <summary>Reichhaltige Test-Inhalte mit ALLEN Feldern gesetzt (auch Nullables) für Kontrakt-/Roundtrip-Tests.</summary>
internal static class BackupTestData
{
    public static BackupContent Full() => new(
        Sets: new[]
        {
            new VocabularySet
            {
                Id = 5, Title = "Spanisch A1", Description = "Grundwortschatz",
                SourceLanguage = "de", TargetLanguage = "es", CreatedAt = 111, UpdatedAt = 222
            }
        },
        Entries: new[]
        {
            new VocabularyEntry
            {
                Id = 9, SetId = 5, SourceText = "Haus", TargetText = "casa",
                AcceptedAnswers = ["la casa", "el hogar"],
                ExampleSentence = "La casa es grande.", Notes = "feminin",
                PartOfSpeech = "Nomen", Gender = "f", PluralForm = "casas",
                VerbForms = "", Pronunciation = "ˈkasa", Tags = ["wohnen", "a1"],
                Mnemonic = "Casa klingt wie Kasse", ImagePath = "img/casa.png", AudioPath = "audio/casa.mp3",
                Difficulty = 260, BoxLevel = 3, NextReviewAt = 1_700_000_000_000,
                CorrectCount = 4, WrongCount = 1, LastReviewedAt = 1_699_000_000_000,
                CreatedAt = 111, UpdatedAt = 333, IsArchived = false, IsLeech = true,
                LastIntervalDays = 7
            }
        },
        Sessions: new[]
        {
            new SessionRecord
            {
                Id = 2, SetId = 5, SetName = "Spanisch A1", CorrectCount = 8, WrongCount = 2,
                StartedAt = 1_698_000_000_000, WrongEntryIds = "9,10", DurationMinutes = 4,
                LearningMode = "FLASHCARD"
            }
        },
        Settings: new SrsSettings
        {
            Mode = SrsMode.FlashcardBox,
            BoxIntervals = [0, 4, 7, 14, 30, 180],
            StrictAccents = true,
            TypoToleranceEnabled = false,
            LeechThreshold = 5,
            LearningDirection = LearningDirection.TargetToSource,
            MaxCardsPerSession = 40,
            MaxNewCardsPerDay = 10
        });
}
