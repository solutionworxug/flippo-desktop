using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flippo.App.Localization;
using Flippo.App.Services;
using Flippo.Cloud.Catalog;
using Flippo.Core.Content;
using Flippo.Data.Services;

namespace Flippo.App.ViewModels;

/// <summary>
/// Ein Themenset in der Picker-Liste. <see cref="IsImported"/> steuert den Button-Zustand; <see cref="IsOnline"/>
/// unterscheidet gebündelte (sofort importierbar) von Online-Packs (erst herunterladen). Bei Online-Packs trägt
/// <see cref="Pack"/> die Katalog-Metadaten (inkl. sha256/url), <see cref="Entry"/> ist dann <c>null</c>.
/// </summary>
public sealed partial class ThemeSetItem : ObservableObject
{
    public ThemeSetManifestEntry? Entry { get; }
    public CatalogPack? Pack { get; }
    public string Title { get; }
    public string CountText { get; }
    public bool IsOnline { get; }
    /// <summary>Für Online-Zeilen: „Herunterladen · 4,7 KB"; für bundled leer.</summary>
    public string DownloadText { get; }
    [ObservableProperty] private bool _isImported;

    /// <summary>Gebündeltes Set.</summary>
    public ThemeSetItem(ThemeSetManifestEntry entry, string title, string countText)
    {
        Entry = entry;
        Title = title;
        CountText = countText;
        IsOnline = false;
        DownloadText = "";
    }

    /// <summary>Online-Pack aus dem Katalog.</summary>
    public ThemeSetItem(CatalogPack pack, string title, string countText, string downloadText)
    {
        Pack = pack;
        Title = title;
        CountText = countText;
        IsOnline = true;
        DownloadText = downloadText;
    }
}

/// <summary>
/// Themenset-Picker (P12 + C2-Katalog): Sprachfilter + Liste, Inline-Import. Gebündelte Sets laden sofort;
/// der Online-Katalog wird beim Öffnen asynchron nachgeladen und in dieselbe Liste gemergt (Bundle gewinnt
/// bei id-Kollision). Online-Packs zeigen eine Download-Kennzeichnung + Größe; Klick lädt sha256-verifiziert
/// und importiert über <see cref="ThemeSetImporter.ImportFileAsync"/>. Katalog nicht erreichbar → stiller
/// Hinweis, Picker voll nutzbar.
/// </summary>
public sealed partial class ThemeSetPickerViewModel : ViewModelBase
{
    private readonly ThemeSetImporter _importer;
    private readonly IThemeSetSource _bundledSource;
    private readonly CatalogClient _catalog;
    private readonly InstalledPacksRegistry _installed;
    private readonly IDialogService _dialogs;
    private readonly string _targetLanguage;

    private List<ThemeSetManifestEntry> _bundled = new();
    private List<CatalogPack> _onlinePacks = new();
    private HashSet<string> _bundledIds = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<string> Languages { get; } = new();
    public ObservableCollection<ThemeSetItem> Items { get; } = new();

    [ObservableProperty] private string _selectedLanguage = "";
    [ObservableProperty] private bool _isEmpty;
    /// <summary>True → Caption „Katalog nicht erreichbar" wird sichtbar (Picker bleibt nutzbar).</summary>
    [ObservableProperty] private bool _catalogUnreachable;

    /// <summary>True, sobald mindestens ein Set importiert wurde → Aufrufer lädt die Karteien neu.</summary>
    public bool AnyImported { get; private set; }

    public ThemeSetPickerViewModel(ThemeSetImporter importer, IThemeSetSource bundledSource,
        CatalogClient catalog, InstalledPacksRegistry installed, IDialogService dialogs, string targetLanguage)
    {
        _importer = importer;
        _bundledSource = bundledSource;
        _catalog = catalog;
        _installed = installed;
        _dialogs = dialogs;
        _targetLanguage = targetLanguage;
    }

    /// <summary>Lädt sofort die gebündelten Sets (unverändert). Der Katalog kommt in <see cref="LoadCatalogAsync"/>.</summary>
    public async Task LoadAsync()
    {
        _bundled = (await _importer.GetAvailableAsync(_targetLanguage)).ToList();

        var manifest = await _bundledSource.LoadManifestAsync();
        _bundledIds = manifest is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : manifest.ThemeSets.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        RebuildLanguages();
        SelectedLanguage = Languages[0];   // "Alle Sprachen"
        Apply();
    }

    /// <summary>
    /// Holt den Online-Katalog (nutzergetriggert beim Öffnen; kein Startup-Fetch). Nicht erreichbar → Caption.
    /// Online-Packs, deren id gebündelt ist, werden verworfen (Bundle gewinnt).
    /// </summary>
    public async Task LoadCatalogAsync(CancellationToken ct = default)
    {
        CatalogIndex? index;
        try { index = await _catalog.GetIndexAsync(ct); }
        catch { index = null; }

        if (index is null)
        {
            CatalogUnreachable = true;
            return;
        }

        _onlinePacks = index.Packs
            .Where(p => !_bundledIds.Contains(p.Id))
            .Where(p => string.Equals(p.TargetLanguage, _targetLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        RebuildLanguages();
        Apply();
    }

    partial void OnSelectedLanguageChanged(string value) => Apply();

    private void RebuildLanguages()
    {
        var current = SelectedLanguage;
        Languages.Clear();
        Languages.Add(L.T("ThemeSet_AllLanguages"));
        var langs = _bundled.Select(e => e.Language)
            .Concat(_onlinePacks.Select(p => p.SourceLanguage))
            .Distinct()
            .OrderBy(x => x);
        foreach (var lang in langs) Languages.Add(lang);
        if (!string.IsNullOrEmpty(current) && Languages.Contains(current)) SelectedLanguage = current;
    }

    private void Apply()
    {
        Items.Clear();
        bool all = string.IsNullOrEmpty(SelectedLanguage) || SelectedLanguage == L.T("ThemeSet_AllLanguages");

        var bundledItems = (all ? _bundled : _bundled.Where(e => e.Language == SelectedLanguage))
            .OrderBy(e => e.Title)
            .Select(e => new ThemeSetItem(e, BundledTitle(e), string.Format(L.T("ThemeSet_CardCount"), e.EntryCount)));
        foreach (var item in bundledItems) Items.Add(item);

        var onlineItems = (all ? _onlinePacks : _onlinePacks.Where(p => p.SourceLanguage == SelectedLanguage))
            .OrderBy(p => p.Title)
            .Select(CreateOnlineItem);
        foreach (var item in onlineItems) Items.Add(item);

        IsEmpty = Items.Count == 0;
    }

    private ThemeSetItem CreateOnlineItem(CatalogPack pack)
    {
        var title = $"{CatalogTitle(pack)} ({pack.SourceLanguage})";
        var count = string.Format(L.T("ThemeSet_CardCount"), pack.EntryCount);
        var download = string.Format(L.T("Catalog_DownloadTag"), FormatSize(pack.SizeBytes));
        var item = new ThemeSetItem(pack, title, count, download);
        // Bereits importiert? Registry (per id) oder Titel-Dedupe (gleicher Anzeigetitel schon als Kartei).
        if (_installed.IsInstalled(pack.Id)) item.IsImported = true;
        return item;
    }

    [RelayCommand]
    private async Task Import(ThemeSetItem? item)
    {
        if (item is null || item.IsImported) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!item.IsOnline)
        {
            var result = await _importer.ImportAsync(item.Entry!, item.Title, now);
            item.IsImported = true;   // auch bei „bereits vorhanden" (null) als importiert markieren
            if (result is not null) AnyImported = true;
            return;
        }

        // Online: Download → sha256 (im Client) → ImportFileAsync → Registry.
        var pack = item.Pack!;
        try
        {
            var file = await _catalog.DownloadPackAsync(pack, CancellationToken.None);
            var result = await _importer.ImportFileAsync(file, item.Title, now);
            _installed.MarkInstalled(pack.Id, pack.PackVersion);
            item.IsImported = true;
            if (result is not null) AnyImported = true;
        }
        catch (CatalogChecksumException)
        {
            await _dialogs.ShowMessageAsync(L.T("Catalog_ErrorTitle"), L.T("Catalog_ShaMismatch"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            await _dialogs.ShowMessageAsync(L.T("Catalog_ErrorTitle"), L.T("Catalog_DownloadError"));
        }
    }

    /// <summary>Lokalisierter Themen-Titel eines gebündelten Sets (Fallback: Manifest-Titel).</summary>
    private static string BundledTitle(ThemeSetManifestEntry e)
    {
        var key = "ThemeSetTitle_" + e.Topic.Replace('-', '_');
        var loc = L.T(key);
        var title = loc == key ? e.Title : loc;
        return $"{title} ({e.Language})";
    }

    /// <summary>Lokalisierter Titel eines Online-Packs (Topic aus der id, wie bei bundled).</summary>
    private static string CatalogTitle(CatalogPack p)
    {
        int dash = p.Id.IndexOf('-');
        var topic = dash >= 0 && dash < p.Id.Length - 1 ? p.Id[(dash + 1)..] : p.Id;
        var key = "ThemeSetTitle_" + topic.Replace('-', '_');
        var loc = L.T(key);
        return loc == key ? p.Title : loc;
    }

    /// <summary>Menschliche Größenangabe (KB) für die Download-Kennzeichnung.</summary>
    private static string FormatSize(long bytes)
    {
        double kb = bytes / 1024.0;
        return kb < 1024 ? $"{kb:0.#} KB" : $"{kb / 1024:0.#} MB";
    }
}
