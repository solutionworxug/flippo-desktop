namespace Flippo.Data.Entities;

/// <summary>
/// Room-Spiegel von SessionRecordEntity.kt (Tabelle "session_records").
/// Room deklariert KEINEN FK auf das Set (setId nullable, "null = alle Listen") — SessionRecords
/// bestehen unabhängig weiter und werden beim Restore nicht kaskadierend gelöscht.
/// Feldname <c>LearnMode</c> = Room-Name (Domain/Backup-JSON: <c>learningMode</c>).
/// </summary>
public class SessionRecordEntity
{
    public long Id { get; set; }
    public long? SetId { get; set; }
    public string SetName { get; set; } = "";
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public long StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string WrongEntryIds { get; set; } = "";
    public string LearnMode { get; set; } = "FREE_TEXT";
}
