namespace Flippo.Core.Import;

/// <summary>
/// Spalten-Zuordnung für den Datei-Import (Port von ColumnMapping.kt).
/// Indizes 0-basiert; <c>-1</c> = Spalte nicht importieren.
/// </summary>
public sealed record ColumnMapping
{
    public int SourceTextColumn { get; init; } = 0;
    public int TargetTextColumn { get; init; } = 1;
    public int ExampleSentenceColumn { get; init; } = -1;
    public int NotesColumn { get; init; } = -1;
    public int TagsColumn { get; init; } = -1;
    public int PartOfSpeechColumn { get; init; } = -1;

    /// <summary><c>" / "</c> im Zieltext → erste Alternative = targetText, Rest = acceptedAnswers.</summary>
    public bool SplitAlternatives { get; init; } = true;
}
