using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.Core.Content;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>Ein Themenset in der Picker-Liste; <see cref="IsImported"/> steuert den Button-Zustand.</summary>
public sealed partial class ThemeSetItem : ObservableObject
{
    public ThemeSetManifestEntry Entry { get; }
    public string Title { get; }
    public string CountText { get; }
    [ObservableProperty] private bool _isImported;

    public ThemeSetItem(ThemeSetManifestEntry entry, string title, string countText)
    {
        Entry = entry;
        Title = title;
        CountText = countText;
    }
}

/// <summary>
/// Themenset-Picker (Port von ThemeSetPickerDialog): Sprachfilter + Liste, Inline-Import.
/// Zeigt alle Sets der Ziel-UI-Sprache (kein Drip, keine Wörterbuch-Kopplung).
/// </summary>
public sealed partial class ThemeSetPickerViewModel : ViewModelBase
{
    private readonly ThemeSetImporter _importer;
    private readonly string _targetLanguage;
    private List<ThemeSetManifestEntry> _all = new();

    public ObservableCollection<string> Languages { get; } = new();
    public ObservableCollection<ThemeSetItem> Items { get; } = new();

    [ObservableProperty] private string _selectedLanguage = "";
    [ObservableProperty] private bool _isEmpty;

    /// <summary>True, sobald mindestens ein Set importiert wurde → Aufrufer lädt die Karteien neu.</summary>
    public bool AnyImported { get; private set; }

    public ThemeSetPickerViewModel(ThemeSetImporter importer, string targetLanguage)
    {
        _importer = importer;
        _targetLanguage = targetLanguage;
    }

    public async Task LoadAsync()
    {
        _all = (await _importer.GetAvailableAsync(_targetLanguage)).ToList();

        Languages.Clear();
        Languages.Add(L.T("ThemeSet_AllLanguages"));
        foreach (var lang in _all.Select(e => e.Language).Distinct().OrderBy(x => x))
            Languages.Add(lang);

        SelectedLanguage = Languages[0];   // "Alle Sprachen"
        Apply();
    }

    partial void OnSelectedLanguageChanged(string value) => Apply();

    private void Apply()
    {
        Items.Clear();
        bool all = string.IsNullOrEmpty(SelectedLanguage) || SelectedLanguage == L.T("ThemeSet_AllLanguages");
        var filtered = all ? _all : _all.Where(e => e.Language == SelectedLanguage);
        foreach (var e in filtered.OrderBy(e => e.Title))
            Items.Add(new ThemeSetItem(e, DisplayTitle(e), string.Format(L.T("ThemeSet_CardCount"), e.EntryCount)));
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private async Task Import(ThemeSetItem? item)
    {
        if (item is null || item.IsImported) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await _importer.ImportAsync(item.Entry, item.Title, now);
        item.IsImported = true;            // auch bei "bereits vorhanden" (null) als importiert markieren
        if (result is not null) AnyImported = true;
    }

    /// <summary>Lokalisierter Themen-Titel (Fallback: Manifest-Titel) + Quellsprach-Kürzel zur Unterscheidung.</summary>
    private static string DisplayTitle(ThemeSetManifestEntry e)
    {
        var key = "ThemeSetTitle_" + e.Topic.Replace('-', '_');
        var loc = L.T(key);
        var title = loc == key ? e.Title : loc;
        return $"{title} ({e.Language})";
    }
}
