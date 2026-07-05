using Flippo.Core.Domain;

namespace Flippo.Core.Backup;

/// <summary>
/// Mapping Domain ↔ Backup-DTO. Enums werden als Kotlin-Namens-Strings serialisiert; beim Import
/// führt ein unbekannter Enum-Wert zu Default + Warnung (darf den Import NICHT crashen, Plan 4.3).
/// </summary>
public static class BackupMapper
{
    // ================= Domain → DTO (Export) =================

    public static BackupDataDto ToDto(BackupContent content, long createdAtMs) => new()
    {
        Version = 2,
        CreatedAt = createdAtMs,
        Sets = content.Sets.Select(ToDto).ToList(),
        Entries = content.Entries.Select(ToDto).ToList(),
        SessionRecords = content.Sessions.Select(ToDto).ToList(),
        SrsSettings = content.Settings is null ? null : ToDto(content.Settings)
    };

    private static BackupSetDto ToDto(VocabularySet s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Description = s.Description,
        SourceLanguage = s.SourceLanguage,
        TargetLanguage = s.TargetLanguage,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private static BackupEntryDto ToDto(VocabularyEntry e) => new()
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

    private static BackupSessionRecordDto ToDto(SessionRecord r) => new()
    {
        Id = r.Id,
        SetId = r.SetId,
        SetName = r.SetName,
        CorrectCount = r.CorrectCount,
        WrongCount = r.WrongCount,
        StartedAt = r.StartedAt,
        WrongEntryIds = r.WrongEntryIds,
        DurationMinutes = r.DurationMinutes,
        LearningMode = r.LearningMode
    };

    private static BackupSrsSettingsDto ToDto(SrsSettings s) => new()
    {
        Mode = SrsModeToJson(s.Mode),
        BoxIntervals = s.BoxIntervals.ToList(),
        StrictAccents = s.StrictAccents,
        TypoToleranceEnabled = s.TypoToleranceEnabled,
        LeechThreshold = s.LeechThreshold,
        LearningDirection = DirectionToJson(s.LearningDirection),
        MaxCardsPerSession = s.MaxCardsPerSession,
        MaxNewCardsPerDay = s.MaxNewCardsPerDay
    };

    // ================= DTO → Domain (Import) =================

    public static BackupParseResult FromDto(BackupDataDto dto)
    {
        var warnings = new List<string>();

        var sets = dto.Sets.Select(FromDto).ToList();
        var entries = dto.Entries.Select(FromDto).ToList();
        var sessions = dto.SessionRecords.Select(FromDto).ToList();
        SrsSettings? settings = dto.SrsSettings is null ? null : FromDto(dto.SrsSettings, warnings);

        if (dto.Version > 2)
            warnings.Add($"Backup-Version {dto.Version} ist neuer als unterstützt (2) — nur bekannte Felder wurden übernommen.");

        return new BackupParseResult(
            dto.Version,
            dto.CreatedAt,
            new BackupContent(sets, entries, sessions, settings),
            warnings);
    }

    private static VocabularySet FromDto(BackupSetDto d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Description = d.Description,
        SourceLanguage = d.SourceLanguage,
        TargetLanguage = d.TargetLanguage,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };

    private static VocabularyEntry FromDto(BackupEntryDto d) => new()
    {
        Id = d.Id,
        SetId = d.SetId,
        SourceText = d.SourceText,
        TargetText = d.TargetText,
        AcceptedAnswers = (d.AcceptedAnswers ?? new List<string>()).ToList(),
        ExampleSentence = d.ExampleSentence,
        Notes = d.Notes,
        PartOfSpeech = d.PartOfSpeech,
        Gender = d.Gender,
        PluralForm = d.PluralForm,
        VerbForms = d.VerbForms,
        Pronunciation = d.Pronunciation,
        Tags = (d.Tags ?? new List<string>()).ToList(),
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

    private static SessionRecord FromDto(BackupSessionRecordDto d) => new()
    {
        Id = d.Id,
        SetId = d.SetId,
        SetName = d.SetName,
        CorrectCount = d.CorrectCount,
        WrongCount = d.WrongCount,
        StartedAt = d.StartedAt,
        WrongEntryIds = d.WrongEntryIds,
        DurationMinutes = d.DurationMinutes,
        LearningMode = d.LearningMode
    };

    private static SrsSettings FromDto(BackupSrsSettingsDto d, List<string> warnings)
    {
        var mode = ParseSrsMode(d.Mode, out var modeKnown);
        if (!modeKnown)
            warnings.Add($"Unbekannter SRS-Modus '{d.Mode}' → Standard (Adaptiv).");

        var direction = ParseDirection(d.LearningDirection, out var dirKnown);
        if (!dirKnown)
            warnings.Add($"Unbekannte Lernrichtung '{d.LearningDirection}' → Standard (Quelle→Ziel).");

        return new SrsSettings
        {
            Mode = mode,
            BoxIntervals = (d.BoxIntervals ?? new List<int>()).ToList(),
            StrictAccents = d.StrictAccents,
            TypoToleranceEnabled = d.TypoToleranceEnabled,
            LeechThreshold = d.LeechThreshold,
            LearningDirection = direction,
            MaxCardsPerSession = d.MaxCardsPerSession,
            MaxNewCardsPerDay = d.MaxNewCardsPerDay
        };
    }

    // ================= Enum ↔ String =================

    public static string SrsModeToJson(SrsMode mode) => mode switch
    {
        SrsMode.FlashcardBox => "FLASHCARD_BOX",
        SrsMode.Adaptive => "ADAPTIVE",
        _ => "ADAPTIVE"
    };

    public static SrsMode ParseSrsMode(string value, out bool known)
    {
        switch (value)
        {
            case "FLASHCARD_BOX": known = true; return SrsMode.FlashcardBox;
            case "ADAPTIVE": known = true; return SrsMode.Adaptive;
            default: known = false; return SrsMode.Adaptive;
        }
    }

    public static string DirectionToJson(LearningDirection direction) => direction switch
    {
        LearningDirection.SourceToTarget => "SOURCE_TO_TARGET",
        LearningDirection.TargetToSource => "TARGET_TO_SOURCE",
        LearningDirection.Mixed => "MIXED",
        _ => "SOURCE_TO_TARGET"
    };

    public static LearningDirection ParseDirection(string value, out bool known)
    {
        switch (value)
        {
            case "SOURCE_TO_TARGET": known = true; return LearningDirection.SourceToTarget;
            case "TARGET_TO_SOURCE": known = true; return LearningDirection.TargetToSource;
            case "MIXED": known = true; return LearningDirection.Mixed;
            default: known = false; return LearningDirection.SourceToTarget;
        }
    }
}
