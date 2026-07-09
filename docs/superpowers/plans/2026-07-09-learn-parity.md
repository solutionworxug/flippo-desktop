# Lern-Parität zu Android (Modus-Dialog + Bewertungs-Knöpfe) — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zwei Lern-Abweichungen vom Android-Original beheben: Bewertungs-Knöpfe „voll wie Android" (Falsch/Schwer/Gut/Leicht, 2×2, farbig, Wort-Hinweise) und einen Lernmodus-Dialog vor jedem Session-Start (SplitButton-Modus-Menüs raus).

**Architecture:** Punkt 1 ist reine Präsentation (XAML + resx), `LearnSessionViewModel` unangetastet. Punkt 2 führt ein modales `ModeChooserWindow` (nach `ProviderChooserWindow`-Muster) + einen zentralen `LearnLauncher`-Service ein, der „Modus abfragen → navigieren" bündelt; alle 5 Lern-Einstiegspunkte rufen ihn auf, die 3 duplizierten `ParseMode`-Helfer entfallen.

**Tech Stack:** Avalonia 12 / .NET 10 / C# / CommunityToolkit.Mvvm / MS.Extensions.DI, resx-Localization via `L.T(...)`.

## Global Constraints

- **Punkt 1 = reine Präsentation:** nur `LearnSessionView.axaml` + resx; kein VM-, kein Core-Eingriff. Die `PreviewAgain/Hard/Good/Easy`-Properties bleiben (nur nicht mehr gebunden).
- **Tests dürfen nicht sinken:** aktuell 224 grün; nach jedem Task `dotnet test` = 224 (dieser Slice fügt keine automatisierten Tests hinzu — kein Unit-Test-Seam, s.u.).
- **Build 0 Warnings:** `dotnet build src/Flippo.App -c Debug` (TreatWarningsAsErrors).
- **Neue resx-Keys immer in BEIDE Dateien:** `Strings.de.resx` (Deutsch) **und** `Strings.resx` (Englisch, neutral). `LocalizationTests` prüft nur Stichproben — fehlende Sprach-Parität würde erst zur Laufzeit auffallen.
- **Farb-Mapping der 4 Knöpfe:** Falsch→`Brush.Danger`, Schwer→`Brush.Warning`, Gut→`Brush.Accent`, Leicht→`Brush.Success`, Text jeweils `Brush.OnAccent`.
- **History-Wiederholungen** (`RepeatWrong`/`RelearnSet`) behalten ihren ursprünglichen Modus — **kein** Dialog.
- Alle Commits direkt auf `master`.

**Testbarkeit (bewusste Entscheidung):** `NavigationService` ist `sealed` + DI-gekoppelt; diese Navigations-VMs sind im Repo nicht unit-getestet (nur `CardEditorViewModel`). Der Modus-Dialog ist reine UI. Es gibt keinen sauberen Unit-Test-Seam ohne neue Test-Infrastruktur (Scope-Creep). Verifikation daher: Build + 224 grün + manueller Smoke-Test (Marks Gate), analog zum Retheme.

## File Structure

**Neu:**
- `src/Flippo.App/Views/ModeChooserWindow.axaml` (+ `.axaml.cs`) — modaler Lernmodus-Dialog
- `src/Flippo.App/Services/LearnLauncher.cs` — zentraler Lern-Starter (Modus fragen → navigieren)

**Geändert:** `LearnSessionView.axaml`, `Strings.de.resx`, `Strings.resx`, `DialogService.cs`, `App.axaml.cs`, `DashboardViewModel.cs`, `SetsOverviewViewModel.cs`, `SetDetailViewModel.cs`, `MainWindowViewModel.cs`, `SetsOverviewView.axaml`, `SetDetailView.axaml`, optional `Theme/Icons.axaml`.

---

### Task 1: Bewertungs-Knöpfe „voll wie Android" (Präsentation)

**Files:**
- Modify: `src/Flippo.App/Views/LearnSessionView.axaml:161-186`
- Modify: `src/Flippo.App/Resources/Strings.de.resx` (Zeilen 30–33 Werte; neue Keys)
- Modify: `src/Flippo.App/Resources/Strings.resx` (spiegelbildlich, Englisch)

**Interfaces:**
- Consumes: bestehende Commands `GradeWrong/Hard/Good/Easy` + Property `IsAdaptive` (unverändert), Brushes `Brush.Danger/Warning/Accent/Success/OnAccent`.
- Produces: nichts für Folgetasks.

- [ ] **Step 1: resx-Werte ändern (beide Dateien)**

`Strings.de.resx` / `Strings.resx`:

| Key | DE neu | EN neu |
|---|---|---|
| `Learn_Again` | `Falsch` | `Wrong` |
| `Learn_Hard` | `Schwer` | `Hard` |
| `Learn_Good` | `Gut` | `Good` |
| `Learn_Easy` | `Leicht` | `Easy` |

- [ ] **Step 2: Neue Hint-Keys ergänzen (beide Dateien)**

| Key | DE | EN |
|---|---|---|
| `Learn_AgainHint` | `Sofort wieder` | `Right away` |
| `Learn_HardHint` | `Bald wieder` | `Soon again` |
| `Learn_GoodHint` | `Normal` | `Normal` |
| `Learn_EasyHint` | `Längeres Intervall` | `Longer interval` |

- [ ] **Step 3: XAML — adaptiven Block (Z. 161–186) durch 2×2-Raster ersetzen**

Den `<StackPanel IsVisible="{Binding IsAdaptive}" Orientation="Horizontal" …>`-Block ersetzen. Der nicht-adaptive Block (`!IsAdaptive`, Z. 145–160, Falsch/Richtig) bleibt **unverändert**. Neuer Block:

```xml
<Grid IsVisible="{Binding IsAdaptive}" ColumnDefinitions="*,*" RowDefinitions="Auto,Auto"
      HorizontalAlignment="Center" MaxWidth="440">
    <Button Grid.Row="0" Grid.Column="0" Command="{Binding GradeWrongCommand}" Margin="0,0,6,6"
            Background="{DynamicResource Brush.Danger}" HorizontalAlignment="Stretch" Padding="18,10">
        <StackPanel HorizontalAlignment="Center" Spacing="2">
            <TextBlock Text="{loc:T Learn_Again}" FontSize="15" FontWeight="SemiBold"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
            <TextBlock Text="{loc:T Learn_AgainHint}" FontSize="12" Opacity="0.85"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
        </StackPanel>
    </Button>
    <Button Grid.Row="0" Grid.Column="1" Command="{Binding GradeHardCommand}" Margin="6,0,0,6"
            Background="{DynamicResource Brush.Warning}" HorizontalAlignment="Stretch" Padding="18,10">
        <StackPanel HorizontalAlignment="Center" Spacing="2">
            <TextBlock Text="{loc:T Learn_Hard}" FontSize="15" FontWeight="SemiBold"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
            <TextBlock Text="{loc:T Learn_HardHint}" FontSize="12" Opacity="0.85"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
        </StackPanel>
    </Button>
    <Button Grid.Row="1" Grid.Column="0" Command="{Binding GradeGoodCommand}" Margin="0,6,6,0"
            Background="{DynamicResource Brush.Accent}" HorizontalAlignment="Stretch" Padding="18,10">
        <StackPanel HorizontalAlignment="Center" Spacing="2">
            <TextBlock Text="{loc:T Learn_Good}" FontSize="15" FontWeight="SemiBold"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
            <TextBlock Text="{loc:T Learn_GoodHint}" FontSize="12" Opacity="0.85"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
        </StackPanel>
    </Button>
    <Button Grid.Row="1" Grid.Column="1" Command="{Binding GradeEasyCommand}" Margin="6,6,0,0"
            Background="{DynamicResource Brush.Success}" HorizontalAlignment="Stretch" Padding="18,10">
        <StackPanel HorizontalAlignment="Center" Spacing="2">
            <TextBlock Text="{loc:T Learn_Easy}" FontSize="15" FontWeight="SemiBold"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
            <TextBlock Text="{loc:T Learn_EasyHint}" FontSize="12" Opacity="0.85"
                       Foreground="{DynamicResource Brush.OnAccent}" HorizontalAlignment="Center"/>
        </StackPanel>
    </Button>
</Grid>
```

Keine `PreviewAgain/Hard/Good/Easy`-Bindings, keine Nummern. Die `KeyBinding`s D1–D4/NumPad1–4 (Z. 16–23) bleiben und bewerten weiterhin.

- [ ] **Step 4: Build + Tests**

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnings; `dotnet test` → 224 grün.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "Lern-Parität: Bewertungs-Knöpfe wie Android (Falsch/Schwer/Gut/Leicht, 2×2)"
```

---

### Task 2: Modus-Dialog + LearnLauncher (Infrastruktur)

**Files:**
- Create: `src/Flippo.App/Views/ModeChooserWindow.axaml` (+ `.axaml.cs`)
- Create: `src/Flippo.App/Services/LearnLauncher.cs`
- Modify: `src/Flippo.App/Services/DialogService.cs` (Interface + Impl)
- Modify: `src/Flippo.App/App.axaml.cs` (DI-Registrierung)
- Modify: `src/Flippo.App/Resources/Strings.de.resx` + `Strings.resx` (Dialog-Keys)
- Optional Modify: `src/Flippo.App/Theme/Icons.axaml` (`Icon.checklist`)

**Interfaces:**
- Consumes: `LearningMode` (`Flippo.Core.Domain`, Werte `Flashcard/FreeText/MultipleChoice`), `SessionFilter`, `NavigationService.NavigateTo<LearnSessionViewModel>(configure)`, `LearnSessionViewModel.Initialize(long? setId, string setName, SessionFilter filter, LearningMode mode = Flashcard, int boxLevel = 0)`, `IDialogService._owner()`-Muster.
- Produces: `LearnLauncher.StartAsync(long? setId, string setName, SessionFilter filter)` und `IDialogService.ShowModeChooserAsync() : Task<LearningMode?>` — von Task 3 konsumiert.

- [ ] **Step 1: resx — Dialog-Keys (beide Dateien)**

Titel für die 3 Modi via bestehende Keys `Sets_ModeFlashcard`/`Sets_ModeFreeText`/`Sets_ModeMultipleChoice` (bereits „Karteikarten/Freitext/Multiple Choice"); Cancel via bestehendes `SetEditor_Cancel`. Neu:

| Key | DE | EN |
|---|---|---|
| `Learn_ModeTitle` | `Lernmodus` | `Learning mode` |
| `Learn_ModeSubtitle` | `Wie möchtest du lernen?` | `How would you like to learn?` |
| `Learn_ModeFlashcardSub` | `Vorderseite ansehen, umdrehen, selbst bewerten — schnell und entspannt.` | `Look at the front, flip, rate yourself — quick and relaxed.` |
| `Learn_ModeFreeTextSub` | `Antwort selbst eintippen — anspruchsvoller, perfektioniert die Schreibweise.` | `Type the answer yourself — more challenging, perfects your spelling.` |
| `Learn_ModeMcSub` | `Aus 4 Antworten wählen — guter Einstieg.` | `Pick from 4 options — a good entry point.` |

- [ ] **Step 2 (optional): `Icon.checklist` ergänzen**

Falls ein passendes MC-Icon gewünscht: `Icon.checklist` (Material Symbols Outlined „checklist", viewBox `0 -960 960 960`) in `Icons.axaml` als `StreamGeometry` ergänzen — Pfad 1:1 von Google-Upstream, byte-verifiziert wie die bestehenden Keys. Fallback ohne neues Icon: in der View `Icon.check` für MC verwenden.

- [ ] **Step 3: `ModeChooserWindow.axaml` (nach `ProviderChooserWindow`-Muster)**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loc="using:Flippo.App.Localization"
        x:Class="Flippo.App.Views.ModeChooserWindow"
        Title="{loc:T Learn_ModeTitle}"
        Width="420" SizeToContent="Height"
        Background="{DynamicResource Brush.Bg.App}"
        WindowStartupLocation="CenterOwner" CanResize="False">
    <Window.KeyBindings>
        <KeyBinding Gesture="Escape" Command="{x:Static ... }"/> <!-- oder Cancel im Code-behind via OnKeyDown; einfachste Variante: KeyBinding weglassen, Window schließt per Cancel-Button -->
    </Window.KeyBindings>
    <Border Classes="app-card" Margin="16">
        <StackPanel Spacing="12">
            <StackPanel Spacing="2">
                <TextBlock Classes="section" Text="{loc:T Learn_ModeTitle}"/>
                <TextBlock Classes="caption" Text="{loc:T Learn_ModeSubtitle}"/>
            </StackPanel>

            <Button HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Padding="14,12"
                    CornerRadius="{DynamicResource Radius.Control}" Click="OnFlashcard">
                <StackPanel Orientation="Horizontal" Spacing="12">
                    <PathIcon Classes="icon" Data="{DynamicResource Icon.decks}" VerticalAlignment="Center"/>
                    <StackPanel Spacing="1">
                        <TextBlock Classes="body" Text="{loc:T Sets_ModeFlashcard}"/>
                        <TextBlock Classes="caption" Text="{loc:T Learn_ModeFlashcardSub}" TextWrapping="Wrap" MaxWidth="320"/>
                    </StackPanel>
                </StackPanel>
            </Button>

            <Button HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Padding="14,12"
                    CornerRadius="{DynamicResource Radius.Control}" Click="OnFreeText">
                <StackPanel Orientation="Horizontal" Spacing="12">
                    <PathIcon Classes="icon" Data="{DynamicResource Icon.edit}" VerticalAlignment="Center"/>
                    <StackPanel Spacing="1">
                        <TextBlock Classes="body" Text="{loc:T Sets_ModeFreeText}"/>
                        <TextBlock Classes="caption" Text="{loc:T Learn_ModeFreeTextSub}" TextWrapping="Wrap" MaxWidth="320"/>
                    </StackPanel>
                </StackPanel>
            </Button>

            <Button HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Padding="14,12"
                    CornerRadius="{DynamicResource Radius.Control}" Click="OnMultipleChoice">
                <StackPanel Orientation="Horizontal" Spacing="12">
                    <PathIcon Classes="icon" Data="{DynamicResource Icon.checklist}" VerticalAlignment="Center"/>
                    <StackPanel Spacing="1">
                        <TextBlock Classes="body" Text="{loc:T Sets_ModeMultipleChoice}"/>
                        <TextBlock Classes="caption" Text="{loc:T Learn_ModeMcSub}" TextWrapping="Wrap" MaxWidth="320"/>
                    </StackPanel>
                </StackPanel>
            </Button>

            <Button HorizontalAlignment="Right" Content="{loc:T SetEditor_Cancel}" Click="OnCancel"
                    MinWidth="90" CornerRadius="{DynamicResource Radius.Control}"/>
        </StackPanel>
    </Border>
</Window>
```

Hinweis: Escape → Cancel am einfachsten im Code-behind über `KeyDown` (siehe Step 4), das obige `Window.KeyBindings`-Fragment entsprechend weglassen. Falls `Icon.checklist` nicht ergänzt wurde: `Icon.check` verwenden.

- [ ] **Step 4: `ModeChooserWindow.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flippo.Core.Domain;

namespace Flippo.App.Views;

/// <summary>Lernmodus-Auswahl vor dem Session-Start (Android-Parität). Rückgabe null = abgebrochen.</summary>
public partial class ModeChooserWindow : Window
{
    public ModeChooserWindow() => InitializeComponent();

    private void OnFlashcard(object? sender, RoutedEventArgs e) => Close(LearningMode.Flashcard);
    private void OnFreeText(object? sender, RoutedEventArgs e) => Close(LearningMode.FreeText);
    private void OnMultipleChoice(object? sender, RoutedEventArgs e) => Close(LearningMode.MultipleChoice);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
```

- [ ] **Step 5: `IDialogService` + `DialogService` erweitern**

In `IDialogService` (nahe der anderen Chooser-Signaturen):
```csharp
/// <summary>Lernmodus-Auswahl vor dem Session-Start (Android-Parität). Rückgabe null = abgebrochen.</summary>
Task<LearningMode?> ShowModeChooserAsync();
```
In `DialogService`:
```csharp
public async Task<LearningMode?> ShowModeChooserAsync()
{
    var owner = _owner();
    if (owner is null) return null;
    var window = new ModeChooserWindow();
    return await window.ShowDialog<LearningMode?>(owner);
}
```

- [ ] **Step 6: `LearnLauncher.cs`**

```csharp
using Flippo.App.ViewModels;
using Flippo.Core.Session;

namespace Flippo.App.Services;

/// <summary>
/// Zentraler Lern-Starter: fragt den Lernmodus per Dialog ab und startet danach die Session.
/// Genutzt von allen Lern-Einstiegspunkten, damit die „Modus wählen → navigieren"-Logik nicht dupliziert.
/// </summary>
public sealed class LearnLauncher
{
    private readonly IDialogService _dialogs;
    private readonly NavigationService _nav;

    public LearnLauncher(IDialogService dialogs, NavigationService nav)
    {
        _dialogs = dialogs;
        _nav = nav;
    }

    /// <summary>Fragt den Lernmodus ab und startet danach die Session. Abbruch (null) → nichts.</summary>
    public async Task StartAsync(long? setId, string setName, SessionFilter filter)
    {
        var mode = await _dialogs.ShowModeChooserAsync();
        if (mode is null) return;
        _nav.NavigateTo<LearnSessionViewModel>(vm => vm.Initialize(setId, setName, filter, mode.Value));
    }
}
```

- [ ] **Step 7: DI-Registrierung in `App.axaml.cs`**

`LearnLauncher` neben `SetActionsService` registrieren (gleiche Lebensdauer wie die anderen App-Services). Signatur-Abhängigkeiten (`IDialogService`, `NavigationService`) sind bereits registriert.

- [ ] **Step 8: Build + Tests**

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnings; `dotnet test` → 224 grün. (Launcher/Dialog noch ungenutzt, muss aber sauber kompilieren.)

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "Lern-Parität: Modus-Dialog (ModeChooserWindow) + LearnLauncher-Service"
```

---

### Task 3: Einstiegspunkte umstellen + SplitButton-Modus-Menüs raus (Verdrahtung)

**Files:**
- Modify: `src/Flippo.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/Flippo.App/ViewModels/SetsOverviewViewModel.cs`
- Modify: `src/Flippo.App/ViewModels/SetDetailViewModel.cs`
- Modify: `src/Flippo.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/Flippo.App/Views/SetsOverviewView.axaml:17-27`
- Modify: `src/Flippo.App/Views/SetDetailView.axaml:51-77`
- Modify: `src/Flippo.App/Resources/Strings.de.resx` + `Strings.resx` (2 Tooltips)

**Interfaces:**
- Consumes: `LearnLauncher.StartAsync(long? setId, string setName, SessionFilter filter)` (Task 2).
- Produces: nichts für Folgetasks (Endpunkt der Verdrahtung).

- [ ] **Step 1: `DashboardViewModel` — Launcher injizieren + `LearnAllDue` async**

Konstruktor um `LearnLauncher launcher` erweitern (Feld `_launcher`). Command ersetzen:
```csharp
[RelayCommand]
private Task LearnAllDue()
    => _launcher.StartAsync(null, L.T("SetsVm_AllDueName"), SessionFilter.Due);
```
(Hardcodiertes `LearningMode.Flashcard` entfällt.)

- [ ] **Step 2: `SetsOverviewViewModel` — Launcher, `ParseMode` löschen**

Konstruktor um `LearnLauncher launcher`. Ersetzen:
```csharp
[RelayCommand]
private Task LearnAllDue()
    => _launcher.StartAsync(null, L.T("SetsVm_AllDueName"), SessionFilter.Due);

[RelayCommand]
private Task LearnSet(VocabularySet? set)
    => set is null ? Task.CompletedTask : _launcher.StartAsync(set.Id, set.Title, SessionFilter.Due);
```
`private static LearningMode ParseMode(...)` **löschen**.

- [ ] **Step 3: `SetDetailViewModel` — Launcher, nur Filter, `ParseMode` löschen**

Konstruktor um `LearnLauncher launcher`. `Learn` auf Filter-only umstellen:
```csharp
/// <summary>Lernen-Split-Button: startet eine Session; Parameter = Filter (Due/All/New/Leech). Modus per Dialog.</summary>
[RelayCommand]
private Task Learn(string? filter)
{
    var sessionFilter = filter switch
    {
        "All" => SessionFilter.All,
        "New" => SessionFilter.New,
        "Leech" => SessionFilter.Leech,
        _ => SessionFilter.Due
    };
    return _launcher.StartAsync(_set.Id, _set.Title, sessionFilter);
}
```
`private static LearningMode ParseMode(string s)` **löschen**.

- [ ] **Step 4: `MainWindowViewModel` — Launcher, `ParseMode` löschen**

Konstruktor um `LearnLauncher launcher`. Ersetzen:
```csharp
[RelayCommand]
private Task LearnAllDue()
    => _launcher.StartAsync(null, L.T("SetsVm_AllDueName"), SessionFilter.Due);
```
`private static LearningMode ParseMode(...)` **löschen**. Prüfen, ob das NativeMenu (`MainWindow.axaml`) `LearnAllDueCommand` mit `CommandParameter` aufruft — falls ja, Parameter entfernen.

- [ ] **Step 5: `SetsOverviewView.axaml` — SplitButton → einfacher Button**

Z. 17–27 (`SplitButton` „Alle fälligen") ersetzen durch:
```xml
<Button Content="{loc:T Sets_LearnAllDue}" Command="{Binding LearnAllDueCommand}"
        Classes="accent" ToolTip.Tip="{loc:T Sets_LearnAllDueTooltip}"/>
```

- [ ] **Step 6: `SetDetailView.axaml` — SplitButton-Flyout auf 4 Filter reduzieren**

Z. 51–77: `SplitButton` behalten (`Command="{Binding LearnCommand}" CommandParameter="Due"`), Flyout auf flache Filter-Liste (Modus-Untermenüs raus):
```xml
<SplitButton Content="{loc:T Detail_Learn}" Command="{Binding LearnCommand}" CommandParameter="Due"
             ToolTip.Tip="{loc:T Detail_LearnTooltip}">
    <SplitButton.Flyout>
        <MenuFlyout>
            <MenuItem Header="{loc:T Detail_Due}"     Command="{Binding LearnCommand}" CommandParameter="Due"/>
            <MenuItem Header="{loc:T Detail_All}"     Command="{Binding LearnCommand}" CommandParameter="All"/>
            <MenuItem Header="{loc:T Detail_New}"     Command="{Binding LearnCommand}" CommandParameter="New"/>
            <MenuItem Header="{loc:T Detail_Leeches}" Command="{Binding LearnCommand}" CommandParameter="Leech"/>
        </MenuFlyout>
    </SplitButton.Flyout>
</SplitButton>
```

- [ ] **Step 7: Tooltips angleichen (beide resx)**

| Key | DE neu | EN neu |
|---|---|---|
| `Sets_LearnAllDueTooltip` | `Fällige lernen — Modus wird beim Start gewählt` | `Learn due cards — mode is chosen at start` |
| `Detail_LearnTooltip` | *(auf „Modus wird beim Start gewählt" anpassen, ohne „Dropdown/Modus-Menü")* | *(analog EN)* |

(Aktuelle EN-Werte in `Strings.resx` vor dem Ersetzen kurz prüfen und sinngemäß angleichen.)

- [ ] **Step 8: Build + Tests**

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnings; `dotnet test` → 224 grün.

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "Lern-Parität: Modus-Dialog an allen Lern-Starts, SplitButton-Modus-Menüs entfernt"
```

---

## Verifikation (Ende-zu-Ende, Marks Gate)

`dotnet run --project src/Flippo.App -c Debug`, in **Light und Dark**:

1. **Bewertungs-Knöpfe** (adaptive Karteikarte, umdrehen): Falsch/Schwer/Gut/Leicht im 2×2, farbig (rot/amber/indigo/grün), Wort-Hinweise, keine Nummern, keine Tages-Vorschau. Tasten 1–4 bewerten weiterhin.
2. **Modus-Dialog erscheint** bei: Dashboard „Jetzt lernen" (Hero + Kachel), Karteien „Alle fälligen lernen", Karteien Zeilen-Play, Set-Detail „Lernen" (+ Filter-Menü), Menü Lernen→„Alle fälligen".
3. **Jeder Modus** startet korrekt (Karteikarte/Freitext/Multiple Choice); **Abbrechen** startet keine Session.
4. **History** „Falsche wiederholen"/„Erneut lernen" startet **ohne** Dialog im ursprünglichen Modus.
5. Keine SplitButton-Modus-Menüs mehr; Tooltips ohne „Dropdown/Modus".

**Danach (separater Schritt, externe Wirkung — nur nach Marks OK):** `<Version>` 0.4.0 → 0.5.0, Velopack-Pack, GitHub-Release v0.5.0.
