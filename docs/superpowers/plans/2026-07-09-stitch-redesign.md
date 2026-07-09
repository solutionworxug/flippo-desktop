# Stitch-Redesign (Dashboard + Statistik) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dashboard und Statistik nach dem Stitch-Design „FLIPPO Dashboard Modernized" umbauen: kühle Blau-Farbwelt, dauerhaft dunkle Gradient-Sidebar, Fortschritts-Linienchart (LiveCharts2), GitHub-Stil-Aktivitäts-Heatmap, vertikale mehrfarbige Leitner-Boxen.

**Architecture:** Die Farbwelt wird ausschließlich über die bestehenden Design-Tokens (`Tokens.axaml`) umgestellt — Token-*Keys* bleiben stabil, nur *Werte* ändern sich, dadurch rethemen sich alle übrigen Views automatisch. Die Sidebar bekommt theme-unabhängige Zusatz-Tokens (außerhalb der ThemeDictionaries). Neue Chart-Daten (kumulative Fortschrittskurve, 182-Tage-Aktivität) entstehen TDD-getrieben im `StatisticsCalculator`; die Views rendern sie via LiveCharts2 (Linienchart) bzw. ItemsControl (Heatmap, Leitner-Balken).

**Tech Stack:** Avalonia 12.0.5, FluentTheme, CommunityToolkit.Mvvm, LiveChartsCore.SkiaSharpView.Avalonia `2.1.0-dev-798` (einzige Serie mit Avalonia-12-Abhängigkeit; stabile 2.0.5 ist gegen Avalonia 11 gebaut), xUnit.

## Global Constraints

- Token-Keys in `Tokens.axaml` dürfen nicht umbenannt oder entfernt werden — nur Werte ändern und neue Keys additiv ergänzen (alle anderen Views hängen an den Keys).
- Schriften bleiben unverändert (Bricolage Grotesque Display, Source Serif 4, Inter UI) — das Redesign betrifft Farben und Layout, nicht die Typografie.
- Dark-Mode bleibt vollständig funktionsfähig: jede geänderte Light-Farbe braucht ein abgestimmtes Dark-Pendant. Sidebar- und Hero-Farben sind bewusst theme-FEST (identisch in Light und Dark).
- Alle neuen UI-Strings zweisprachig: `src/Flippo.App/Resources/Strings.resx` (Englisch, neutral) + `Strings.de.resx` (Deutsch). Zugriff via `{loc:T Key}` bzw. `L.T("Key")`.
- Kein Funktionsverlust: Empty-States, „Alles erledigt", Streak-Pille, Letzte Session, Wochentag-/Tageszeit-Charts, Modus-Statistik und Schwerste Karten bleiben erhalten (nur restyled/umsortiert).
- Verifikation UI-Tasks: `dotnet build` (Exit 0) + App-Start `dotnet run --project src/Flippo.App` mit Sichtprüfung Light UND Dark. Verifikation Core-Tasks: `dotnet test`.
- Commit nach jedem Task.

## Stitch-Farbreferenz (aus den Screens abgeleitet)

| Rolle | Light | Dark |
|---|---|---|
| Bg.App | `#FFF3F5FA` | `#FF0B1120` |
| Surface.Card | `#FFFFFFFF` | `#FF141C30` |
| Surface.Subtle | `#FFECF0F7` | `#FF1C2740` |
| Border.Subtle | `#FFE1E6F0` | `#FF2A3654` |
| Text.Primary | `#FF0F172A` | `#FFE8EDF7` |
| Text.Secondary | `#FF5B6577` | `#FF97A3BA` |
| Accent | `#FF2563EB` | `#FF7CA6FF` |
| Success | `#FF16A34A` | `#FF4ADE80` |
| Warning | `#FFEAB308` | `#FFFACC15` |
| Danger | `#FFEF4444` | `#FFF87171` |
| Tertiary (kühl statt warm) | `#FF0EA5E9` | `#FF38BDF8` |
| Sidebar-Gradient (fest) | `#FF2B4ACB → #FF101B4D` | identisch |
| Hero-Gradient (fest) | `#FF3B82F6 → #FF1D4ED8` | identisch |

---

### Task 1: Farb-Tokens auf Stitch-Palette umstellen

**Files:**
- Modify: `src/Flippo.App/Theme/Tokens.axaml:44-106`

**Interfaces:**
- Produces: neue Ressourcen-Keys `Brush.Sidebar.Bg`, `Brush.Sidebar.Text`, `Brush.Sidebar.TextMuted`, `Brush.Sidebar.Hover`, `Brush.Sidebar.Active`, `Brush.Hero`, `Brush.OnHero`, `Brush.Hero.Button` (theme-fest); alle bestehenden `Color.*`/`Brush.*`-Keys behalten ihre Namen, nur Werte ändern sich.

- [ ] **Step 1: ThemeDictionaries ersetzen**

In `Tokens.axaml` den Block `<ResourceDictionary.ThemeDictionaries>` … `</ResourceDictionary.ThemeDictionaries>` (Zeilen 45–85) vollständig ersetzen durch:

```xml
    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
            <Color x:Key="Color.Bg.App">#FFF3F5FA</Color>
            <Color x:Key="Color.Surface.Card">#FFFFFFFF</Color>
            <Color x:Key="Color.Surface.Subtle">#FFECF0F7</Color>
            <Color x:Key="Color.Border.Subtle">#FFE1E6F0</Color>
            <Color x:Key="Color.Text.Primary">#FF0F172A</Color>
            <Color x:Key="Color.Text.Secondary">#FF5B6577</Color>
            <Color x:Key="Color.Accent">#FF2563EB</Color>
            <Color x:Key="Color.Accent.Soft">#1A2563EB</Color>
            <Color x:Key="Color.OnAccent">#FFFFFFFF</Color>
            <Color x:Key="Color.Success">#FF16A34A</Color>
            <Color x:Key="Color.Warning">#FFEAB308</Color>
            <Color x:Key="Color.Warning.Soft">#1AEAB308</Color>
            <Color x:Key="Color.Danger">#FFEF4444</Color>
            <Color x:Key="Color.Danger.Soft">#1AEF4444</Color>
            <Color x:Key="Color.Tertiary">#FF0EA5E9</Color>
            <Color x:Key="Color.Tertiary.Soft">#1A0EA5E9</Color>
            <!-- Fluent-Akzent (Buttons, Fokus) an den Stitch-Ton angleichen -->
            <Color x:Key="SystemAccentColor">#FF2563EB</Color>
        </ResourceDictionary>
        <ResourceDictionary x:Key="Dark">
            <Color x:Key="Color.Bg.App">#FF0B1120</Color>
            <Color x:Key="Color.Surface.Card">#FF141C30</Color>
            <Color x:Key="Color.Surface.Subtle">#FF1C2740</Color>
            <Color x:Key="Color.Border.Subtle">#FF2A3654</Color>
            <Color x:Key="Color.Text.Primary">#FFE8EDF7</Color>
            <Color x:Key="Color.Text.Secondary">#FF97A3BA</Color>
            <Color x:Key="Color.Accent">#FF7CA6FF</Color>
            <Color x:Key="Color.Accent.Soft">#337CA6FF</Color>
            <Color x:Key="Color.OnAccent">#FF0B1120</Color>
            <Color x:Key="Color.Success">#FF4ADE80</Color>
            <Color x:Key="Color.Warning">#FFFACC15</Color>
            <Color x:Key="Color.Warning.Soft">#33FACC15</Color>
            <Color x:Key="Color.Danger">#FFF87171</Color>
            <Color x:Key="Color.Danger.Soft">#33F87171</Color>
            <Color x:Key="Color.Tertiary">#FF38BDF8</Color>
            <Color x:Key="Color.Tertiary.Soft">#3338BDF8</Color>
            <Color x:Key="SystemAccentColor">#FF7CA6FF</Color>
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
```

- [ ] **Step 2: Theme-feste Sidebar-/Hero-Ressourcen ergänzen**

Direkt nach dem `Shadow.CardHover`-Eintrag (vor `</ResourceDictionary>`) einfügen:

```xml
    <!-- ── Theme-FEST: Sidebar (immer dunkel, Stitch-Gradient) + Hero-Karte ── -->
    <SolidColorBrush x:Key="Brush.Sidebar.Text" Color="#FFFFFFFF" />
    <SolidColorBrush x:Key="Brush.Sidebar.TextMuted" Color="#B8FFFFFF" />
    <SolidColorBrush x:Key="Brush.Sidebar.Hover" Color="#1FFFFFFF" />
    <SolidColorBrush x:Key="Brush.Sidebar.Active" Color="#33FFFFFF" />
    <LinearGradientBrush x:Key="Brush.Sidebar.Bg" StartPoint="0%,0%" EndPoint="0%,100%">
        <GradientStop Offset="0" Color="#FF2B4ACB" />
        <GradientStop Offset="1" Color="#FF101B4D" />
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="Brush.Hero" StartPoint="0%,0%" EndPoint="100%,100%">
        <GradientStop Offset="0" Color="#FF3B82F6" />
        <GradientStop Offset="1" Color="#FF1D4ED8" />
    </LinearGradientBrush>
    <SolidColorBrush x:Key="Brush.OnHero" Color="#FFFFFFFF" />
    <SolidColorBrush x:Key="Brush.Hero.Button" Color="#FF1D4ED8" />
```

Außerdem den Kopf-Kommentar der Datei (Zeilen 3–8) anpassen: „Warm-editorial" durch „Stitch-Blau: kühle Blau-Palette (Accent #2563EB), theme-feste dunkle Sidebar + Hero-Gradient" ersetzen.

- [ ] **Step 3: Build prüfen**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Sichtprüfung Light + Dark**

Run: `dotnet run --project src/Flippo.App`
Expected: App startet; Hintergrund hellgrau-blau, Akzente blau. In Einstellungen auf Dark umschalten: dunkelblaue Flächen, lesbare Texte, keine „weißen Löcher". (Sidebar ist nach diesem Task noch hell — das kommt in Task 2.)

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/Theme/Tokens.axaml
git commit -m "Stitch-Redesign T1: Tokens auf kühle Blau-Palette + Sidebar-/Hero-Ressourcen"
```

---

### Task 2: Sidebar dauerhaft dunkel (Gradient, weiße Nav, farbige Icons)

**Files:**
- Modify: `src/Flippo.App/Theme/Styles.axaml:99-122` (Button.nav-Styles)
- Modify: `src/Flippo.App/Views/MainWindow.axaml:86-158` (Sidebar-Border)

**Interfaces:**
- Consumes: `Brush.Sidebar.*` aus Task 1.
- Produces: keine neuen Schnittstellen; `Button.nav`-Klassenverhalten ist danach auf dunklen Grund ausgelegt (wird nur in der Sidebar verwendet).

- [ ] **Step 1: Nav-Styles auf dunklen Grund umstellen**

In `Styles.axaml` die vier `Button.nav`-Style-Blöcke (Zeilen 99–122) ersetzen durch:

```xml
    <!-- Sidebar-Navigationseintrag: flach, linksbündig, auf dunklem Sidebar-Grund -->
    <Style Selector="Button.nav">
        <Setter Property="HorizontalAlignment" Value="Stretch" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="Padding" Value="10,9" />
        <Setter Property="CornerRadius" Value="{DynamicResource Radius.Control}" />
        <Setter Property="FontSize" Value="{DynamicResource Font.Body}" />
        <Setter Property="Foreground" Value="{DynamicResource Brush.Sidebar.TextMuted}" />
    </Style>
    <Style Selector="Button.nav /template/ ContentPresenter">
        <Setter Property="Background" Value="Transparent" />
    </Style>
    <Style Selector="Button.nav:pointerover /template/ ContentPresenter">
        <Setter Property="Background" Value="{DynamicResource Brush.Sidebar.Hover}" />
    </Style>
    <Style Selector="Button.nav:pointerover">
        <Setter Property="Foreground" Value="{DynamicResource Brush.Sidebar.Text}" />
    </Style>

    <!-- Sidebar-Navigationseintrag: aktiver Zustand (weiße Soft-Fläche + weißer Text) -->
    <Style Selector="Button.nav.nav-active /template/ ContentPresenter">
        <Setter Property="Background" Value="{DynamicResource Brush.Sidebar.Active}" />
    </Style>
    <Style Selector="Button.nav.nav-active">
        <Setter Property="Foreground" Value="{DynamicResource Brush.Sidebar.Text}" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>
```

- [ ] **Step 2: Sidebar-Border in MainWindow umstellen**

In `MainWindow.axaml`:

a) Sidebar-Border (Zeile 88–89) ersetzen:

```xml
                <Border Grid.Column="0" Background="{DynamicResource Brush.Sidebar.Bg}" Padding="16">
```

(BorderBrush/BorderThickness entfallen — der Gradient trennt sich selbst vom Inhalt.)

b) App-Titel-Block (Zeilen 91–94) ersetzen:

```xml
                        <StackPanel DockPanel.Dock="Top" Margin="4,2,4,20" Spacing="2">
                            <TextBlock Text="FLIPPO" Classes="display" FontSize="26" Foreground="{DynamicResource Brush.Sidebar.Text}"/>
                            <TextBlock Classes="eyebrow" Text="{loc:T App_Subtitle}" Foreground="{DynamicResource Brush.Sidebar.TextMuted}"/>
                        </StackPanel>
```

c) Jedem der sechs Nav-`PathIcon`s eine feste Icon-Farbe geben (Stitch: farbige Icons auf dunklem Grund). Die sechs `PathIcon Classes="icon"`-Zeilen ersetzen:

```xml
<PathIcon Classes="icon" Data="{DynamicResource Icon.dashboard}" Foreground="{DynamicResource Brush.Sidebar.Text}"/>
<PathIcon Classes="icon" Data="{DynamicResource Icon.decks}" Foreground="#FFF87171"/>
<PathIcon Classes="icon" Data="{DynamicResource Icon.dictionary}" Foreground="#FF93C5FD"/>
<PathIcon Classes="icon" Data="{DynamicResource Icon.statistics}" Foreground="#FF4ADE80"/>
<PathIcon Classes="icon" Data="{DynamicResource Icon.history}" Foreground="#FF2DD4BF"/>
<PathIcon Classes="icon" Data="{DynamicResource Icon.settings}" Foreground="#FFFACC15"/>
```

d) Versions-Zeile (Zeile 144) ersetzen:

```xml
                        <TextBlock DockPanel.Dock="Bottom" Classes="caption" Text="{Binding AppVersion}" Foreground="{DynamicResource Brush.Sidebar.TextMuted}" HorizontalAlignment="Center"/>
```

e) Sidebar-CTA (Zeilen 146–154): Pill-Form wie im Stitch-Screen — beim `<Border Classes="sidebar-cta" ...>` zusätzlich `CornerRadius="{DynamicResource Radius.Pill}"` setzen und im inneren StackPanel `Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center"` verwenden:

```xml
                        <Border DockPanel.Dock="Bottom" Classes="sidebar-cta" CornerRadius="{DynamicResource Radius.Pill}" Margin="0,12,0,12">
                            <Button Background="Transparent" Padding="0" HorizontalAlignment="Stretch"
                                    Command="{Binding LearnAllDueCommand}">
                                <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
                                    <TextBlock Text="{loc:T Nav_LearnNow}" Foreground="{DynamicResource Brush.OnAccent}" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                    <PathIcon Data="{DynamicResource Icon.play}" Width="18" Height="18" Foreground="{DynamicResource Brush.OnAccent}"/>
                                </StackPanel>
                            </Button>
                        </Border>
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Sichtprüfung Light + Dark**

Run: `dotnet run --project src/Flippo.App`
Expected: Sidebar in beiden Themes dunkelblauer Gradient; Nav-Texte gedämpft weiß, aktiver Eintrag weiß auf heller Soft-Fläche; Icons farbig; „Jetzt lernen"-Pill unten; Version zentriert gedämpft. Back-Button („Zurück", ohne Icon) lesbar.

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/Theme/Styles.axaml src/Flippo.App/Views/MainWindow.axaml
git commit -m "Stitch-Redesign T2: Sidebar dauerhaft dunkel (Gradient, weiße Nav, farbige Icons)"
```

---

### Task 3: Dashboard-Layout nach Stitch (zentrierter Hero, Kennzahl-Chips mit Fortschrittsbalken)

**Files:**
- Modify: `src/Flippo.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/Flippo.App/Views/DashboardView.axaml`

**Interfaces:**
- Consumes: `Brush.Hero`, `Brush.OnHero`, `Brush.Hero.Button` (Task 1).
- Produces: `DashboardViewModel` bekommt drei neue ObservableProperties `double DueShare`, `double NewShare`, `double LeechShare` (0..1, Anteil am größten der drei Werte — für die ProgressBar-Chips).

- [ ] **Step 1: Share-Properties im ViewModel ergänzen**

In `DashboardViewModel.cs` nach Zeile 36 (`_leechCards`) einfügen:

```csharp
    // Anteile 0..1 für die Chip-Fortschrittsbalken (relativ zum größten der drei Werte)
    [ObservableProperty] private double _dueShare;
    [ObservableProperty] private double _newShare;
    [ObservableProperty] private double _leechShare;
```

Und in `LoadAsync()` direkt nach `LeechCards = stats.LeechCards;` einfügen:

```csharp
            int chipMax = Math.Max(1, Math.Max(DueToday, Math.Max(NewCards, LeechCards)));
            DueShare = (double)DueToday / chipMax;
            NewShare = (double)NewCards / chipMax;
            LeechShare = (double)LeechCards / chipMax;
```

- [ ] **Step 2: DashboardView nach Stitch umbauen**

`DashboardView.axaml` — den Inhalt des äußeren `<StackPanel Spacing="20" Margin="24">` (Zeilen 12–143) vollständig ersetzen (alle Bindings/Commands bleiben identisch, nur Layout/Optik ändern sich):

```xml
        <StackPanel Spacing="20" Margin="24" MaxWidth="760">

            <!-- Kopf: zentrierte Begrüßung, Streak-Pille rechts -->
            <Panel>
                <TextBlock Classes="display" Text="{Binding Greeting}" HorizontalAlignment="Center"/>
                <Border Classes="streak-pill" IsVisible="{Binding HasStreak}" HorizontalAlignment="Right" VerticalAlignment="Center">
                    <StackPanel Orientation="Horizontal" Spacing="6">
                        <PathIcon Classes="icon" Data="{DynamicResource Icon.streak}" Width="16" Height="16" Foreground="{DynamicResource Brush.Tertiary}"/>
                        <TextBlock Text="{Binding StreakText}" FontWeight="SemiBold" Foreground="{DynamicResource Brush.Tertiary}"/>
                    </StackPanel>
                </Border>
            </Panel>

            <!-- Empty-State: keine Karteien -->
            <Border Classes="app-card" IsVisible="{Binding !HasSets}">
                <StackPanel Spacing="10" HorizontalAlignment="Center" Margin="0,20">
                    <TextBlock Classes="section" Text="{loc:T Dash_EmptyTitle}" HorizontalAlignment="Center"/>
                    <TextBlock Classes="caption" Text="{loc:T Dash_EmptyHint}" HorizontalAlignment="Center"/>
                    <Button Content="{loc:T Dash_CreateSet}" Command="{Binding NewSetCommand}" Classes="accent" HorizontalAlignment="Center" Margin="0,4,0,0"/>
                </StackPanel>
            </Border>

            <!-- CTA / Alles-erledigt (nur mit Karteien) -->
            <Panel IsVisible="{Binding HasSets}">
                <!-- Jetzt lernen (Hero, zentriert, Stitch-Gradient) -->
                <Border Classes="hero-card" IsVisible="{Binding HasDue}" Background="{DynamicResource Brush.Hero}"
                        MaxWidth="420" HorizontalAlignment="Center" Padding="28,24">
                    <StackPanel Spacing="8" HorizontalAlignment="Center">
                        <TextBlock Classes="eyebrow" Text="{loc:T Dash_LearnNow}" Foreground="{DynamicResource Brush.OnHero}" Opacity="0.85" HorizontalAlignment="Center"/>
                        <TextBlock Classes="metric-lg" Text="{Binding DueTotal}" Foreground="{DynamicResource Brush.OnHero}" HorizontalAlignment="Center"/>
                        <TextBlock Classes="body" Text="{Binding DueCountText}" Foreground="{DynamicResource Brush.OnHero}" Opacity="0.9" HorizontalAlignment="Center"/>
                        <Button Command="{Binding LearnAllDueCommand}"
                                Background="{DynamicResource Brush.Hero.Button}" Foreground="{DynamicResource Brush.OnHero}"
                                CornerRadius="{DynamicResource Radius.Pill}"
                                HorizontalAlignment="Center" MinWidth="180" Padding="20,10" Margin="0,6,0,0">
                            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
                                <TextBlock Text="{loc:T Dash_LearnNow}" FontWeight="SemiBold" Foreground="{DynamicResource Brush.OnHero}"/>
                                <PathIcon Data="{DynamicResource Icon.play}" Width="16" Height="16" Foreground="{DynamicResource Brush.OnHero}"/>
                            </StackPanel>
                        </Button>
                    </StackPanel>
                </Border>
                <!-- Alles erledigt -->
                <Border Classes="hero-card" IsVisible="{Binding !HasDue}" MaxWidth="420" HorizontalAlignment="Center">
                    <StackPanel Spacing="6" HorizontalAlignment="Center">
                        <PathIcon Data="{DynamicResource Icon.check}" Width="28" Height="28" Foreground="{DynamicResource Brush.Success}" HorizontalAlignment="Center"/>
                        <TextBlock Classes="section" Text="{loc:T Dash_AllDone}" HorizontalAlignment="Center"/>
                        <TextBlock Classes="caption" Text="{loc:T Dash_AllDoneHint}" HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>
            </Panel>

            <!-- Kennzahl-Chips: Zahl + Label + proportionaler Farbbalken (Stitch) -->
            <Grid ColumnDefinitions="*,*,*" IsVisible="{Binding HasSets}">
                <Border Grid.Column="0" Classes="metric-tile" Margin="0,0,8,0">
                    <StackPanel Spacing="8">
                        <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                            <TextBlock Classes="metric" FontSize="{DynamicResource Font.Section}" Text="{Binding DueToday}" Foreground="{DynamicResource Brush.Warning}"/>
                            <TextBlock Classes="caption" Text="{loc:T Sets_Due}" VerticalAlignment="Center"/>
                        </StackPanel>
                        <ProgressBar Minimum="0" Maximum="1" Value="{Binding DueShare}" Height="6" CornerRadius="{DynamicResource Radius.Bar}"
                                     Foreground="{DynamicResource Brush.Warning}" Background="{DynamicResource Brush.Surface.Subtle}"/>
                    </StackPanel>
                </Border>
                <Border Grid.Column="1" Classes="metric-tile" Margin="0,0,8,0">
                    <StackPanel Spacing="8">
                        <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                            <TextBlock Classes="metric" FontSize="{DynamicResource Font.Section}" Text="{Binding NewCards}" Foreground="{DynamicResource Brush.Success}"/>
                            <TextBlock Classes="caption" Text="{loc:T Sets_New}" VerticalAlignment="Center"/>
                        </StackPanel>
                        <ProgressBar Minimum="0" Maximum="1" Value="{Binding NewShare}" Height="6" CornerRadius="{DynamicResource Radius.Bar}"
                                     Foreground="{DynamicResource Brush.Success}" Background="{DynamicResource Brush.Surface.Subtle}"/>
                    </StackPanel>
                </Border>
                <Border Grid.Column="2" Classes="metric-tile">
                    <StackPanel Spacing="8">
                        <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                            <TextBlock Classes="metric" FontSize="{DynamicResource Font.Section}" Text="{Binding LeechCards}" Foreground="{DynamicResource Brush.Danger}"/>
                            <TextBlock Classes="caption" Text="{loc:T Stats_Leeches}" VerticalAlignment="Center"/>
                        </StackPanel>
                        <ProgressBar Minimum="0" Maximum="1" Value="{Binding LeechShare}" Height="6" CornerRadius="{DynamicResource Radius.Bar}"
                                     Foreground="{DynamicResource Brush.Danger}" Background="{DynamicResource Brush.Surface.Subtle}"/>
                    </StackPanel>
                </Border>
            </Grid>

            <!-- Letzte Session -->
            <Border Classes="app-card" IsVisible="{Binding HasLastSession}">
                <StackPanel Spacing="6">
                    <TextBlock Classes="eyebrow" Text="{loc:T Dash_LastSession}"/>
                    <TextBlock Classes="body" Text="{Binding LastSessionText}"/>
                </StackPanel>
            </Border>

            <!-- Schnellzugriff (2×2 wie Stitch) -->
            <StackPanel Spacing="10" IsVisible="{Binding HasSets}">
                <TextBlock Classes="section" Text="{loc:T Dash_QuickActions}"/>
                <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto">
                    <Border Grid.Row="0" Grid.Column="0" Classes="quick-action" Margin="0,0,6,6">
                        <Button Command="{Binding GoSetsCommand}" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0">
                            <StackPanel Orientation="Horizontal" Spacing="10">
                                <PathIcon Classes="icon" Data="{DynamicResource Icon.decks}" Foreground="{DynamicResource Brush.Accent}"/>
                                <TextBlock Classes="body" Text="{loc:T Nav_Sets}"/>
                            </StackPanel>
                        </Button>
                    </Border>
                    <Border Grid.Row="0" Grid.Column="1" Classes="quick-action" Margin="6,0,0,6">
                        <Button Command="{Binding NewSetCommand}" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0">
                            <StackPanel Orientation="Horizontal" Spacing="10">
                                <PathIcon Classes="icon" Data="{DynamicResource Icon.folder-add}" Foreground="{DynamicResource Brush.Accent}"/>
                                <TextBlock Classes="body" Text="{loc:T Dash_CreateSet}"/>
                            </StackPanel>
                        </Button>
                    </Border>
                    <Border Grid.Row="1" Grid.Column="0" Classes="quick-action" Margin="0,0,6,0">
                        <Button Command="{Binding GoStatisticsCommand}" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0">
                            <StackPanel Orientation="Horizontal" Spacing="10">
                                <PathIcon Classes="icon" Data="{DynamicResource Icon.statistics}" Foreground="{DynamicResource Brush.Accent}"/>
                                <TextBlock Classes="body" Text="{loc:T Nav_Statistics}"/>
                            </StackPanel>
                        </Button>
                    </Border>
                    <Border Grid.Row="1" Grid.Column="1" Classes="quick-action" Margin="6,0,0,0">
                        <Button Command="{Binding LearnAllDueCommand}" HorizontalAlignment="Stretch" HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0">
                            <StackPanel Orientation="Horizontal" Spacing="10">
                                <PathIcon Classes="icon" Data="{DynamicResource Icon.play}" Foreground="{DynamicResource Brush.Accent}"/>
                                <TextBlock Classes="body" Text="{loc:T Dash_ActionLearn}"/>
                            </StackPanel>
                        </Button>
                    </Border>
                </Grid>
            </StackPanel>

        </StackPanel>
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Sichtprüfung**

Run: `dotnet run --project src/Flippo.App`
Expected: Begrüßung zentriert; blauer Gradient-Hero mittig mit großer Zahl und Pill-Button; drei Chips mit farbigen, proportionalen Balken (größter Wert = voller Balken); Schnellzugriffe 2×2 mit blauen Icons. Empty-State (Karteien löschen nicht nötig — Sichtprüfung optional per frischem Profil), „Alles erledigt"-Zustand und Streak-Pille unverändert funktional.

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/ViewModels/DashboardViewModel.cs src/Flippo.App/Views/DashboardView.axaml
git commit -m "Stitch-Redesign T3: Dashboard — zentrierter Gradient-Hero + Kennzahl-Chips mit Balken"
```

---

### Task 4: StatisticsCalculator — kumulative Fortschrittskurve + 182-Tage-Aktivität (TDD)

**Files:**
- Modify: `src/Flippo.Core/Statistics/StatisticsModels.cs`
- Modify: `src/Flippo.Core/Statistics/StatisticsCalculator.cs`
- Test: `tests/Flippo.Tests/Statistics/StatisticsCalculatorTests.cs`

**Interfaces:**
- Produces: `LearningStatistics.CumulativeLearned: IReadOnlyList<DayCount>` (je aktivem Lern-Tag die kumulierte Kartensumme, aufsteigend nach Datum, gesamte Historie) und `LearningStatistics.ActivityLast182Days: IReadOnlyList<DayCount>` (wie `ActivityLast30Days`, aber 182-Tage-Fenster). `DayCount(DateOnly Date, int Count)` bleibt unverändert.

- [ ] **Step 1: Failing Tests schreiben**

In `StatisticsCalculatorTests.cs` am Ende der Klasse ergänzen:

```csharp
    [Fact]
    public void Compute_CumulativeLearned_RunningTotalPerActiveDay()
    {
        long now = At(2024, 1, 15);
        var sessions = new[]
        {
            Sess(At(2024, 1, 10), correct: 3, wrong: 1),      // Tag 1: 4
            Sess(At(2024, 1, 10, 18), correct: 2, wrong: 0),  // Tag 1: +2 → 6
            Sess(At(2024, 1, 12), correct: 5, wrong: 0)       // Tag 2: kumuliert 11
        };

        var s = Compute([], sessions, now);

        Assert.Equal(2, s.CumulativeLearned.Count);
        Assert.Equal(new DayCount(new DateOnly(2024, 1, 10), 6), s.CumulativeLearned[0]);
        Assert.Equal(new DayCount(new DateOnly(2024, 1, 12), 11), s.CumulativeLearned[1]);
    }

    [Fact]
    public void Compute_CumulativeLearned_EmptyWithoutSessions()
    {
        var s = Compute([], [], At(2024, 1, 15));
        Assert.Empty(s.CumulativeLearned);
    }

    [Fact]
    public void Compute_ActivityLast182Days_WindowExcludesOlder()
    {
        long now = At(2024, 7, 1);
        var sessions = new[]
        {
            Sess(At(2024, 6, 30), correct: 2),   // im Fenster
            Sess(At(2023, 12, 1), correct: 9)    // älter als 182 Tage → ausgeschlossen
        };

        var s = Compute([], sessions, now);

        Assert.Single(s.ActivityLast182Days);
        Assert.Equal(new DayCount(new DateOnly(2024, 6, 30), 2), s.ActivityLast182Days[0]);
    }
```

- [ ] **Step 2: Tests laufen lassen — müssen fehlschlagen**

Run: `dotnet test --filter "FullyQualifiedName~StatisticsCalculatorTests"`
Expected: FAIL — `'LearningStatistics' does not contain a definition for 'CumulativeLearned'` (Compile-Fehler zählt als Fail).

- [ ] **Step 3: Modelle + Berechnung implementieren**

a) In `StatisticsModels.cs` im Block „Zeitreihen / Charts" (nach `ActivityLast30Days`) ergänzen:

```csharp
    /// <summary>Kumulierte Kartensumme (correct+wrong) je aktivem Lern-Tag, aufsteigend — Fortschrittskurve.</summary>
    public IReadOnlyList<DayCount> CumulativeLearned { get; init; } = [];
    /// <summary>Aktivität der letzten 182 Tage (26 Wochen) — Heatmap.</summary>
    public IReadOnlyList<DayCount> ActivityLast182Days { get; init; } = [];
```

b) In `StatisticsCalculator.cs` nach dem `activity30`-Block (Zeile 74) einfügen:

```csharp
        // Fortschrittskurve: kumulierte Karten je Lern-Tag (gesamte Historie)
        var cumulative = new List<DayCount>();
        int running = 0;
        foreach (var g in sessions.Where(s => s.StartedAt > 0)
                     .GroupBy(s => DayOf(s.StartedAt)).OrderBy(g => g.Key))
        {
            running += g.Sum(s => s.Total);
            cumulative.Add(new DayCount(g.Key, running));
        }

        // Heatmap: Aktivität der letzten 182 Tage (26 Wochen)
        var since182 = today.AddDays(-182);
        var activity182 = sessions
            .Where(s => s.StartedAt > 0 && DayOf(s.StartedAt) > since182)
            .GroupBy(s => DayOf(s.StartedAt))
            .Select(g => new DayCount(g.Key, g.Sum(s => s.Total)))
            .OrderBy(d => d.Date)
            .ToList();
```

c) Im Rückgabe-Objekt (`return new LearningStatistics { … }`) nach `ActivityLast30Days = activity30,` ergänzen:

```csharp
            CumulativeLearned = cumulative,
            ActivityLast182Days = activity182,
```

- [ ] **Step 4: Tests laufen lassen — müssen grün sein**

Run: `dotnet test --filter "FullyQualifiedName~StatisticsCalculatorTests"`
Expected: PASS, alle Tests grün (bestehende + 3 neue).

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.Core/Statistics/StatisticsModels.cs src/Flippo.Core/Statistics/StatisticsCalculator.cs tests/Flippo.Tests/Statistics/StatisticsCalculatorTests.cs
git commit -m "Stitch-Redesign T4: Calculator — CumulativeLearned + ActivityLast182Days (TDD)"
```

---

### Task 5: LiveCharts2 einbinden + Fortschritts-Linienchart

**Files:**
- Modify: `src/Flippo.App/Flippo.App.csproj`
- Modify: `src/Flippo.App/ViewModels/StatisticsViewModel.cs`
- Modify: `src/Flippo.App/Views/StatisticsView.axaml`
- Modify: `src/Flippo.App/Resources/Strings.resx`, `src/Flippo.App/Resources/Strings.de.resx`

**Interfaces:**
- Consumes: `LearningStatistics.CumulativeLearned` (Task 4).
- Produces: `StatisticsViewModel` bekommt `ISeries[] ProgressSeries`, `Axis[] ProgressXAxes`, `Axis[] ProgressYAxes`, `bool HasProgress`. Loc-Key `Stats_Progress`.

**Risiko-Hinweis:** `2.1.0-dev-798` ist ein Dev-Prerelease (einzige Serie gegen Avalonia 12; die stabile 2.0.5 referenziert Avalonia 11 und ist zur Laufzeit gegen Avalonia 12 nicht verlässlich). Nach Step 5 MUSS der Chart real gerendert geprüft werden. Falls das Paket zur Laufzeit crasht: Task abbrechen und an den Planer zurückmelden (Fallback wäre eine handgezeichnete Polyline — bewusste Entscheidung, nicht still umbauen).

- [ ] **Step 1: Paket referenzieren**

In `Flippo.App.csproj` in der ersten `<ItemGroup>` (nach `CommunityToolkit.Mvvm`) ergänzen:

```xml
    <PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="2.1.0-dev-798" />
```

Run: `dotnet restore`
Expected: Restore succeeded (Paket kommt von nuget.org).

- [ ] **Step 2: Loc-Strings ergänzen**

In `Strings.resx` (hinter `Stats_Hourly`, Zeile 217):

```xml
  <data name="Stats_Progress" xml:space="preserve"><value>Progress</value></data>
```

In `Strings.de.resx` (hinter `Stats_Hourly`, Zeile 217):

```xml
  <data name="Stats_Progress" xml:space="preserve"><value>Fortschritt</value></data>
```

- [ ] **Step 3: Serie + Achsen im ViewModel aufbauen**

In `StatisticsViewModel.cs`:

a) Usings ergänzen:

```csharp
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
```

b) Nach den bestehenden ObservableProperties (Zeile 42) ergänzen:

```csharp
    [ObservableProperty] private ISeries[] _progressSeries = [];
    [ObservableProperty] private Axis[] _progressXAxes = [];
    [ObservableProperty] private Axis[] _progressYAxes = [];
    [ObservableProperty] private bool _hasProgress;
```

c) In `BuildBars(...)` am Anfang (vor dem Karteikasten-Block) einfügen:

```csharp
        // Fortschrittskurve (LiveCharts2): kumulierte Karten über die Zeit
        HasProgress = s.CumulativeLearned.Count >= 2;
        var accent = new SKColor(0x25, 0x63, 0xEB);
        var axisText = new SolidColorPaint(new SKColor(0x5B, 0x65, 0x77));
        ProgressSeries =
        [
            new LineSeries<DateTimePoint>
            {
                Values = s.CumulativeLearned
                    .Select(d => new DateTimePoint(d.Date.ToDateTime(TimeOnly.MinValue), d.Count))
                    .ToArray(),
                GeometrySize = 7,
                LineSmoothness = 0.2,
                Stroke = new SolidColorPaint(accent, 2.5f),
                GeometryStroke = new SolidColorPaint(accent, 2f),
                GeometryFill = new SolidColorPaint(SKColors.White),
                Fill = new SolidColorPaint(accent.WithAlpha(0x22))
            }
        ];
        ProgressXAxes = [new DateTimeAxis(TimeSpan.FromDays(1), d => d.ToString("dd.MM")) { LabelsPaint = axisText }];
        ProgressYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisText }];
```

- [ ] **Step 4: Chart-Karte in der View einfügen**

In `StatisticsView.axaml`:

a) Namespace am `<UserControl>`-Element ergänzen:

```xml
             xmlns:lvc="using:LiveChartsCore.SkiaSharpView.Avalonia"
```

b) Direkt nach dem Block „Erfolgsquote + Sessions" (nach Zeile 85) einfügen:

```xml
                <!-- Fortschritt (kumulierte Karten, LiveCharts2) -->
                <Border Classes="app-card" IsVisible="{Binding HasProgress}">
                    <StackPanel Spacing="10">
                        <TextBlock Classes="section" Text="{loc:T Stats_Progress}"/>
                        <lvc:CartesianChart Height="240"
                                            Series="{Binding ProgressSeries}"
                                            XAxes="{Binding ProgressXAxes}"
                                            YAxes="{Binding ProgressYAxes}"/>
                    </StackPanel>
                </Border>
```

- [ ] **Step 5: Build + Laufzeit prüfen**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

Run: `dotnet run --project src/Flippo.App` → Statistik öffnen.
Expected: Blaues Linienchart mit Punkten und sanfter Flächenfüllung, Datums-X-Achse, Y ab 0. Kein Crash beim Navigieren, Fenster-Resize okay. Bei weniger als 2 Lern-Tagen ist die Karte ausgeblendet.

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.App/Flippo.App.csproj src/Flippo.App/ViewModels/StatisticsViewModel.cs src/Flippo.App/Views/StatisticsView.axaml src/Flippo.App/Resources/Strings.resx src/Flippo.App/Resources/Strings.de.resx
git commit -m "Stitch-Redesign T5: LiveCharts2 + Fortschritts-Linienchart in der Statistik"
```

---

### Task 6: Aktivitäts-Heatmap (GitHub-Stil) statt 30-Tage-Balken

**Files:**
- Modify: `src/Flippo.App/ViewModels/StatisticsViewModel.cs`
- Modify: `src/Flippo.App/Views/StatisticsView.axaml`
- Modify: `src/Flippo.App/Resources/Strings.resx`, `src/Flippo.App/Resources/Strings.de.resx`

**Interfaces:**
- Consumes: `LearningStatistics.ActivityLast182Days` (Task 4).
- Produces: Records `HeatCell(double Intensity, string? Tooltip)` und `HeatWeek(IReadOnlyList<HeatCell> Days)` in `StatisticsViewModel.cs` (Namespace `Flippo.App.ViewModels`); `ObservableCollection<HeatWeek> HeatWeeks` am ViewModel. Loc-Keys `Stats_Activity`, `Stats_LastMonths`. Die bisherigen `ActivityBars` + zugehörige XAML-Sektion entfallen (durch die Heatmap ersetzt — gleiche Information, längerer Zeitraum).

- [ ] **Step 1: Loc-Strings ergänzen**

`Strings.resx` (hinter `Stats_Progress`):

```xml
  <data name="Stats_Activity" xml:space="preserve"><value>Activity</value></data>
  <data name="Stats_LastMonths" xml:space="preserve"><value>Last 6 months</value></data>
```

`Strings.de.resx` (hinter `Stats_Progress`):

```xml
  <data name="Stats_Activity" xml:space="preserve"><value>Aktivität</value></data>
  <data name="Stats_LastMonths" xml:space="preserve"><value>Letzte 6 Monate</value></data>
```

Die Keys `Stats_Activity30` in beiden resx-Dateien entfernen (Zeile 215, keine weiteren Verwendungen nach diesem Task).

- [ ] **Step 2: Heatmap-Aufbau im ViewModel**

In `StatisticsViewModel.cs`:

a) Records neben `Bar` (Zeile 13) ergänzen:

```csharp
/// <summary>Heatmap-Zelle: Intensität 0..1 (0 = kein Lernen), Tooltip „dd.MM.yyyy: n".</summary>
public sealed record HeatCell(double Intensity, string? Tooltip);

/// <summary>Eine Heatmap-Spalte = Kalenderwoche (Mo–So, 7 Zellen).</summary>
public sealed record HeatWeek(IReadOnlyList<HeatCell> Days);
```

b) Collection neben `ActivityBars` ergänzen — und `ActivityBars` samt Befüll-Code entfernen (der `// 30-Tage-Aktivität …`-Block in `BuildBars`, die Property und die Konstante `MaxBarHeight` bleibt für `HourBars` erhalten):

```csharp
    public ObservableCollection<HeatWeek> HeatWeeks { get; } = new();
```

c) In `BuildBars(...)` (anstelle des entfernten 30-Tage-Blocks) einfügen:

```csharp
        // Aktivitäts-Heatmap: 26 Wochen-Spalten à 7 Tage (Mo–So), Intensität relativ zum Maximum
        HeatWeeks.Clear();
        var today = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(nowMs).ToLocalTime().DateTime);
        var heatByDay = s.ActivityLast182Days.ToDictionary(d => d.Date, d => d.Count);
        int maxHeat = Math.Max(1, heatByDay.Count == 0 ? 1 : heatByDay.Values.Max());
        var heatStart = today.AddDays(-181);
        heatStart = heatStart.AddDays(-(((int)heatStart.DayOfWeek + 6) % 7));   // auf Montag zurückrunden
        for (var weekStart = heatStart; weekStart <= today; weekStart = weekStart.AddDays(7))
        {
            var cells = new List<HeatCell>(7);
            for (int d = 0; d < 7; d++)
            {
                var date = weekStart.AddDays(d);
                if (date > today) { cells.Add(new HeatCell(0, null)); continue; }
                int count = heatByDay.TryGetValue(date, out var c) ? c : 0;
                double intensity = count == 0 ? 0 : 0.25 + 0.75 * count / maxHeat;
                cells.Add(new HeatCell(intensity, $"{date:dd.MM.yyyy}: {count}"));
            }
            HeatWeeks.Add(new HeatWeek(cells));
        }
```

Hinweis: die lokale Variable `today` existiert im alten 30-Tage-Block bereits — beim Entfernen des Blocks darauf achten, dass sie genau einmal deklariert bleibt.

- [ ] **Step 3: Heatmap-Karte in der View**

In `StatisticsView.axaml` die komplette Karte `<!-- 30-Tage-Aktivität (vertikale Balken) -->` (Zeilen 109–132) ersetzen durch:

```xml
                <!-- Aktivitäts-Heatmap (GitHub-Stil, 26 Wochen) -->
                <Border Classes="app-card">
                    <StackPanel Spacing="10">
                        <TextBlock Classes="section" Text="{loc:T Stats_Activity}"/>
                        <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                            <ItemsControl ItemsSource="{Binding HeatWeeks}">
                                <ItemsControl.ItemsPanel>
                                    <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                                </ItemsControl.ItemsPanel>
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate x:DataType="vm:HeatWeek">
                                        <ItemsControl ItemsSource="{Binding Days}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate x:DataType="vm:HeatCell">
                                                    <Grid Width="13" Height="13" Margin="1" ToolTip.Tip="{Binding Tooltip}">
                                                        <Border CornerRadius="3" Background="{DynamicResource Brush.Surface.Subtle}"/>
                                                        <Border CornerRadius="3" Background="{DynamicResource Brush.Accent}" Opacity="{Binding Intensity}"/>
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </ScrollViewer>
                        <TextBlock Classes="caption" Text="{loc:T Stats_LastMonths}"/>
                    </StackPanel>
                </Border>
```

- [ ] **Step 4: Build + Sichtprüfung**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

Run: `dotnet run --project src/Flippo.App` → Statistik.
Expected: Heatmap-Raster (26 Spalten × 7 Zeilen), Lern-Tage blau in Abstufungen, leere Tage dezent; Tooltips zeigen „dd.MM.yyyy: n"; in Dark-Mode lesbar. Kein `Stats_Activity30`-Key mehr sichtbar (kein roher Key-Text in der UI).

- [ ] **Step 5: Commit**

```bash
git add src/Flippo.App/ViewModels/StatisticsViewModel.cs src/Flippo.App/Views/StatisticsView.axaml src/Flippo.App/Resources/Strings.resx src/Flippo.App/Resources/Strings.de.resx
git commit -m "Stitch-Redesign T6: Aktivitäts-Heatmap (26 Wochen) ersetzt 30-Tage-Balken"
```

---

### Task 7: Leitner-Boxen vertikal + mehrfarbig

**Files:**
- Modify: `src/Flippo.App/ViewModels/StatisticsViewModel.cs`
- Modify: `src/Flippo.App/Views/StatisticsView.axaml`

**Interfaces:**
- Consumes: `LearningStatistics.CardsByBox` (bestehend).
- Produces: Record `BoxBar(string Label, int Value, double Height, IBrush Fill)` in `StatisticsViewModel.cs`; die bestehende `ObservableCollection<Bar> BoxBars` wird zu `ObservableCollection<BoxBar> BoxBars` (gleicher Name, neuer Elementtyp).

- [ ] **Step 1: BoxBar-Record + farbige Befüllung**

In `StatisticsViewModel.cs`:

a) Using ergänzen:

```csharp
using Avalonia.Media;
```

b) Record neben `Bar` ergänzen:

```csharp
/// <summary>Vertikaler Karteikasten-Balken: Label, Wert, Pixel-Höhe, feste Datenfarbe.</summary>
public sealed record BoxBar(string Label, int Value, double Height, IBrush Fill);
```

c) Collection-Typ ändern:

```csharp
    public ObservableCollection<BoxBar> BoxBars { get; } = new();
```

d) In `BuildBars(...)` den Karteikasten-Block ersetzen durch:

```csharp
        // Karteikasten (vertikal, feste Datenfarben je Fach — Stitch)
        BoxBars.Clear();
        string[] boxHex = ["#EF4444", "#F97316", "#FACC15", "#38BDF8", "#2563EB", "#16A34A"];
        int maxBox = Math.Max(1, s.CardsByBox.Count == 0 ? 1 : s.CardsByBox.Max(b => b.Count));
        foreach (var b in s.CardsByBox)
        {
            var fill = new SolidColorBrush(Color.Parse(boxHex[(b.Box - 1) % boxHex.Length]));
            BoxBars.Add(new BoxBar(string.Format(L.T("Stats_BoxLabel"), b.Box), b.Count,
                Math.Max(6, (double)b.Count / maxBox * MaxBarHeight), fill));
        }
```

(Die Konstante `MaxBarWidth` wird danach nur noch von `WeekdayBars` genutzt — bleibt bestehen.)

- [ ] **Step 2: Karteikasten-Karte vertikal rendern**

In `StatisticsView.axaml` die Karte `<!-- Karteikasten -->` (Zeilen 88–107) ersetzen durch:

```xml
                <!-- Karteikasten (vertikal, mehrfarbig) -->
                <Border Classes="app-card">
                    <StackPanel Spacing="10">
                        <TextBlock Classes="section" Text="{loc:T Stats_Boxes}"/>
                        <ItemsControl ItemsSource="{Binding BoxBars}" HorizontalAlignment="Center">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate><StackPanel Orientation="Horizontal" Spacing="18"/></ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:BoxBar">
                                    <StackPanel Width="40" Spacing="6">
                                        <TextBlock Classes="caption" Text="{Binding Value}" HorizontalAlignment="Center"/>
                                        <Grid Height="120" VerticalAlignment="Bottom">
                                            <Border VerticalAlignment="Bottom" Height="{Binding Height}" Width="32"
                                                    Background="{Binding Fill}" CornerRadius="{DynamicResource Radius.Bar}"
                                                    ToolTip.Tip="{Binding Value}"/>
                                        </Grid>
                                        <TextBlock Classes="caption" Text="{Binding Label}" HorizontalAlignment="Center"/>
                                    </StackPanel>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </Border>
```

- [ ] **Step 3: Build + Sichtprüfung**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

Run: `dotnet run --project src/Flippo.App` → Statistik.
Expected: Karteikasten-Fächer als vertikale Balken in Rot/Orange/Gelb/Hellblau/Blau/Grün, Wert über dem Balken, Fach-Label darunter; auch bei nur 1–2 Fächern zentriert und mit Mindesthöhe sichtbar.

- [ ] **Step 4: Commit**

```bash
git add src/Flippo.App/ViewModels/StatisticsViewModel.cs src/Flippo.App/Views/StatisticsView.axaml
git commit -m "Stitch-Redesign T7: Leitner-Boxen vertikal + mehrfarbig"
```

---

### Task 8: Statistik-Kopf nach Stitch (3 KPI-Karten) + Layout-Reihenfolge

**Files:**
- Modify: `src/Flippo.App/ViewModels/StatisticsViewModel.cs`
- Modify: `src/Flippo.App/Views/StatisticsView.axaml`
- Modify: `src/Flippo.App/Resources/Strings.resx`, `src/Flippo.App/Resources/Strings.de.resx`

**Interfaces:**
- Consumes: `SuccessRateText`, `LearningTimeText` (bestehend), `HasProgress`/Chart (Task 5), `HeatWeeks` (Task 6), `BoxBars` (Task 7).
- Produces: `string TotalLearnedText` am ViewModel. Loc-Keys `Stats_TotalLearned`, `Stats_LearningTime`.

- [ ] **Step 1: Loc-Strings ergänzen**

`Strings.resx`:

```xml
  <data name="Stats_TotalLearned" xml:space="preserve"><value>Total learned</value></data>
  <data name="Stats_LearningTime" xml:space="preserve"><value>Learning time</value></data>
```

`Strings.de.resx`:

```xml
  <data name="Stats_TotalLearned" xml:space="preserve"><value>Gesamt gelernt</value></data>
  <data name="Stats_LearningTime" xml:space="preserve"><value>Lernzeit</value></data>
```

- [ ] **Step 2: TotalLearnedText im ViewModel**

In `StatisticsViewModel.cs` bei den Text-Properties (Zeile 38–42) ergänzen:

```csharp
    [ObservableProperty] private string _totalLearnedText = "";
```

Und in `LoadAsync()` nach `SuccessRateText = …` einfügen:

```csharp
            TotalLearnedText = (s.TotalCorrect + s.TotalWrong).ToString("N0", CultureInfo.CurrentUICulture);
```

- [ ] **Step 3: Statistik-Layout umsortieren**

In `StatisticsView.axaml` innerhalb des `<StackPanel Spacing="16">` die Sektionen in diese Reihenfolge bringen (bestehende Blöcke verschieben, nicht neu erfinden):

1. **NEU — Stitch-KPI-Zeile (3 Karten):** an den Anfang einfügen:

```xml
                <!-- Stitch-KPIs: Gesamt gelernt / Genauigkeit / Lernzeit -->
                <UniformGrid Columns="3">
                    <Border Classes="metric-tile" Margin="0,0,8,0">
                        <StackPanel Spacing="6">
                            <TextBlock Classes="eyebrow" Text="{loc:T Stats_TotalLearned}"/>
                            <TextBlock Classes="metric" Text="{Binding TotalLearnedText}"/>
                            <Border Height="5" CornerRadius="{DynamicResource Radius.Bar}" Background="{DynamicResource Brush.Success}"/>
                        </StackPanel>
                    </Border>
                    <Border Classes="metric-tile" Margin="0,0,8,0">
                        <StackPanel Spacing="6">
                            <TextBlock Classes="eyebrow" Text="{loc:T Stats_SuccessRate}"/>
                            <TextBlock Classes="metric" Text="{Binding SuccessRateText}"/>
                            <Border Height="5" CornerRadius="{DynamicResource Radius.Bar}" Background="{DynamicResource Brush.Accent}"/>
                        </StackPanel>
                    </Border>
                    <Border Classes="metric-tile">
                        <StackPanel Spacing="6">
                            <TextBlock Classes="eyebrow" Text="{loc:T Stats_LearningTime}"/>
                            <TextBlock Classes="metric" Text="{Binding LearningTimeText}"/>
                            <Border Height="5" CornerRadius="{DynamicResource Radius.Bar}" Background="{DynamicResource Brush.Danger}"/>
                        </StackPanel>
                    </Border>
                </UniformGrid>
```

2. Fortschritts-Chart (Task 5) — direkt danach.
3. **Leitner-Boxen (Task 7) + Heatmap (Task 6) nebeneinander:** beide Karten in ein gemeinsames Grid setzen:

```xml
                <Grid ColumnDefinitions="Auto,*">
                    <!-- hier die Karteikasten-Karte aus Task 7, mit Margin="0,0,8,0" am Border -->
                    <!-- hier die Heatmap-Karte aus Task 6, Grid.Column="1" am Border -->
                </Grid>
```

(Die beiden Karten-XML-Blöcke unverändert hineinschieben; nur `Margin`/`Grid.Column` am jeweiligen äußeren `Border` ergänzen.)

4. Danach die bestehenden Detail-Sektionen in dieser Reihenfolge: 4er-KPI-Zeile (Total/Fällig/Neu/Leeches), Erfolgsquote+Sessions-Karten, Wochentag+Tageszeit, Modus-Statistik, Schwerste Karten.

- [ ] **Step 4: Build + Sichtprüfung Light + Dark**

Run: `dotnet build`
Expected: Build succeeded, 0 Errors.

Run: `dotnet run --project src/Flippo.App` → Statistik, Light und Dark.
Expected: Reihenfolge = Stitch oben (3 KPIs → Fortschritt → Leitner|Heatmap), Details unten; „Gesamt gelernt" mit Tausendertrennung; nichts abgeschnitten bei 1000×680 Fenstergröße (Leitner|Heatmap-Zeile darf bei schmalem Fenster horizontal scrollen — Heatmap hat ihren eigenen ScrollViewer).

- [ ] **Step 5: Alle Tests**

Run: `dotnet test`
Expected: PASS, alle Tests grün.

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.App/ViewModels/StatisticsViewModel.cs src/Flippo.App/Views/StatisticsView.axaml src/Flippo.App/Resources/Strings.resx src/Flippo.App/Resources/Strings.de.resx
git commit -m "Stitch-Redesign T8: Statistik-Kopf (3 KPIs) + Stitch-Layout-Reihenfolge"
```
