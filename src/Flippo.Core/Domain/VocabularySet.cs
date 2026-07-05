namespace Flippo.Core.Domain;

/// <summary>Port von VocabularySet.kt. TotalCards/DueCards/NewCards sind berechnet (nicht gespeichert).</summary>
public sealed record VocabularySet
{
    public long Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string SourceLanguage { get; init; } = "";
    public string TargetLanguage { get; init; } = "";
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; init; }
    public int TotalCards { get; init; }
    public int DueCards { get; init; }
    public int NewCards { get; init; }
}
