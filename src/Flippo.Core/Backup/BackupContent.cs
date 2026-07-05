using Flippo.Core.Domain;

namespace Flippo.Core.Backup;

/// <summary>Domain-Inhalt eines Backups (transport-/serialisierungsfrei).</summary>
public sealed record BackupContent(
    IReadOnlyList<VocabularySet> Sets,
    IReadOnlyList<VocabularyEntry> Entries,
    IReadOnlyList<SessionRecord> Sessions,
    SrsSettings? Settings);

/// <summary>Ergebnis des Parsens inkl. tolerant gesammelter Warnungen (unbekannte Enums, neue Version).</summary>
public sealed record BackupParseResult(
    int Version,
    long CreatedAt,
    BackupContent Content,
    IReadOnlyList<string> Warnings);
