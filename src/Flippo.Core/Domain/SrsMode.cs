namespace Flippo.Core.Domain;

/// <summary>
/// Port von SrsSettings.kt <c>enum class SrsMode</c>.
/// Backup-JSON transportiert die Kotlin-Namen (FLASHCARD_BOX/ADAPTIVE) — die Übersetzung
/// Enum ↔ String passiert im BackupMapper (P3), nicht über C#-Enum-Namen.
/// </summary>
public enum SrsMode
{
    FlashcardBox,
    Adaptive
}
