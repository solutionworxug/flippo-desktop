using CommunityToolkit.Mvvm.ComponentModel;
using Flippo.App.Localization;
using Flippo.Core.Domain;

namespace Flippo.App.ViewModels;

/// <summary>Formular für „Eigenes Wörterbuch anlegen / bearbeiten" (Name + Sprachen).</summary>
public sealed partial class DictionaryEditorViewModel : ViewModelBase
{
    private readonly long _id;
    private readonly long _createdAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _name = "";

    [ObservableProperty] private string _sourceLanguage = "";
    [ObservableProperty] private string _targetLanguage = "";

    public string HeaderText { get; }
    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    public DictionaryEditorViewModel(UserDictionary? existing)
    {
        if (existing is null)
        {
            HeaderText = L.T("DictEditor_HeaderNew");
        }
        else
        {
            _id = existing.Id;
            _createdAt = existing.CreatedAt;
            Name = existing.Name;
            SourceLanguage = existing.SourceLanguage;
            TargetLanguage = existing.TargetLanguage;
            HeaderText = L.T("DictEditor_HeaderEdit");
        }
    }

    public UserDictionary Build(long nowMs) => new()
    {
        Id = _id,
        Name = Name.Trim(),
        SourceLanguage = SourceLanguage.Trim(),
        TargetLanguage = TargetLanguage.Trim(),
        CreatedAt = _id == 0 ? nowMs : _createdAt
    };
}
