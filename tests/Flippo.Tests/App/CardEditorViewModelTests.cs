using Flippo.App.ViewModels;
using Flippo.Core.Domain;

namespace Flippo.Tests.App;

/// <summary>Kritisch: der CardEditor darf beim Bearbeiten den SRS-Zustand NICHT zerstören.</summary>
public class CardEditorViewModelTests
{
    [Fact]
    public void Build_PreservesSrsState_AndParsesSemicolonLists()
    {
        var backing = new VocabularyEntry
        {
            Id = 7,
            SetId = 3,
            SourceText = "alt",
            TargetText = "alt",
            BoxLevel = 4,
            Difficulty = 275,
            CorrectCount = 9,
            WrongCount = 2,
            NextReviewAt = 12_345,
            IsLeech = true,
            LastIntervalDays = 30,
            CreatedAt = 100
        };

        var editor = new CardEditorViewModel(backing)
        {
            SourceText = "Haus",
            TargetText = "casa",
            AcceptedAnswers = "la casa; el hogar ;",   // trailing/leere Teile werden entfernt
            Tags = "wohnen;a1"
        };

        var result = editor.Build(nowMs: 999);

        // Inhalt aktualisiert
        Assert.Equal("Haus", result.SourceText);
        Assert.Equal(new[] { "la casa", "el hogar" }, result.AcceptedAnswers);
        Assert.Equal(new[] { "wohnen", "a1" }, result.Tags);

        // SRS-Zustand vollständig erhalten
        Assert.Equal(4, result.BoxLevel);
        Assert.Equal(275, result.Difficulty);
        Assert.Equal(9, result.CorrectCount);
        Assert.Equal(2, result.WrongCount);
        Assert.Equal(12_345, result.NextReviewAt);
        Assert.True(result.IsLeech);
        Assert.Equal(30, result.LastIntervalDays);
        Assert.Equal(7, result.Id);
        Assert.Equal(100, result.CreatedAt);   // createdAt bleibt
        Assert.Equal(999, result.UpdatedAt);   // updatedAt neu
    }

    [Fact]
    public void Build_NewEntry_SetsCreatedAt()
    {
        var editor = new CardEditorViewModel(new VocabularyEntry { SetId = 1 })
        {
            SourceText = "a",
            TargetText = "b"
        };

        var result = editor.Build(nowMs: 555);

        Assert.Equal(0, result.Id);
        Assert.Equal(555, result.CreatedAt);
        Assert.Equal(555, result.UpdatedAt);
    }

    [Fact]
    public void HasContent_RequiresSourceAndTarget()
    {
        var editor = new CardEditorViewModel(new VocabularyEntry());
        Assert.False(editor.HasContent);

        editor.SourceText = "a";
        Assert.False(editor.HasContent);

        editor.TargetText = "b";
        Assert.True(editor.HasContent);
    }
}
