using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.App.Localization;
using Flippo.Core.Domain;

namespace Flippo.App.ViewModels;

/// <summary>Formular für Neue Kartei / Kartei bearbeiten (Titel + Beschreibung + Sprachen).</summary>
public sealed partial class SetEditorViewModel : ViewModelBase
{
    private readonly long _id;
    private readonly long _createdAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _title = "";

    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _sourceLanguage = "";
    [ObservableProperty] private string _targetLanguage = "";

    public string HeaderText { get; }
    public bool CanSave => !string.IsNullOrWhiteSpace(Title);

    public SetEditorViewModel(VocabularySet? existing)
    {
        if (existing is null)
        {
            HeaderText = L.T("SetEditor_HeaderNew");
        }
        else
        {
            _id = existing.Id;
            _createdAt = existing.CreatedAt;
            Title = existing.Title;
            Description = existing.Description;
            SourceLanguage = existing.SourceLanguage;
            TargetLanguage = existing.TargetLanguage;
            HeaderText = L.T("SetEditor_HeaderEdit");
        }
    }

    public VocabularySet Build(long nowMs) => new()
    {
        Id = _id,
        Title = Title.Trim(),
        Description = Description.Trim(),
        SourceLanguage = SourceLanguage.Trim(),
        TargetLanguage = TargetLanguage.Trim(),
        CreatedAt = _id == 0 ? nowMs : _createdAt,
        UpdatedAt = nowMs
    };
}
