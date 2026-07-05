namespace Flippo.Core.Domain;

/// <summary>
/// Port von SessionRecord.kt. <c>LearningMode</c> ist bewusst ein String (nicht das Enum):
/// das Backup-JSON transportiert das Domain-Feld <c>learningMode</c> als String (Plan 1.3).
/// </summary>
public sealed record SessionRecord
{
    public long Id { get; init; }
    public long? SetId { get; init; }
    public string SetName { get; init; } = "";
    public int CorrectCount { get; init; }
    public int WrongCount { get; init; }
    public long StartedAt { get; init; }
    public string WrongEntryIds { get; init; } = "";   // komma-separierte Entry-IDs
    public int DurationMinutes { get; init; }
    public string LearningMode { get; init; } = "FREE_TEXT";

    public int Total => CorrectCount + WrongCount;
    public float SuccessRate => Total > 0 ? (float)CorrectCount / Total : 0f;
}
