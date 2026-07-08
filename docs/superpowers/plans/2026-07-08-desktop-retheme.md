# FLIPPO Desktop Retheme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die ganze FLIPPO-Desktop-App auf die Stitch-Designrichtung heben (warm-editorial: Bricolage-Grotesque-Headlines, Inter-UI, Source-Serif-4 an Lese-Stellen, Indigo + warm-neutral Palette, Bento-Karten, große Radien, weiche Schatten, kuratiertes Material-Vektor-Icon-Set).

**Architecture:** Reiner Präsentations-Umbau. Zentral über `Theme/Tokens.axaml` (Primitive + semantische Farben Light/Dark) und `Theme/Styles.axaml` (Style-Klassen) gesteuert; neue Datei `Theme/Icons.axaml` (Icon-Geometrien). Fonts als eingebettete `AvaloniaResource`. Die Fundament-Änderungen propagieren automatisch in alle Views (alles über `DynamicResource`); danach Feinschliff pro View/Dialog.

**Tech Stack:** Avalonia 12.0.5, .NET 10, FluentTheme, `Avalonia.Fonts.Inter` (vorhanden). Keine neuen NuGet-Pakete.

## Global Constraints

- **Reiner Präsentations-Umbau:** keine Änderung an Services/Domain/DB/Backup-Format/i18n-Strings. **Alle 226 bestehenden Tests bleiben grün.** Einzige gestattete VM-Berührung: eine read-only Präsentations-Property `ActiveNav` in `MainWindowViewModel` (Task 5), testneutral.
- **Nur `DynamicResource`** für Theme-Ressourcen (Cross-Dictionary-Timing), NIE `StaticResource`.
- **Semantische Ressourcen-Namen stabil halten:** bestehende `Brush.*`/`Color.*`/`Font.*`/`Radius.*`/`Inset.*`/`Space.*`-Keys behalten ihre Namen, damit Views nicht global umbenannt werden müssen. Werte ändern sich, Keys nicht. Neue Keys nur additiv.
- **Build-Gate je Task:** `dotnet build` **0 Warnungen** (Projekt hat `TreatWarningsAsErrors`) und volle Suite `dotnet test` **grün** vor jedem Commit.
- **Sichtbarkeits-Gate je UI-Task:** App starten (`dotnet run --project src/Flippo.App`), die betroffene(n) Seite(n) in **Light und Dark** ansehen — Fonts geladen, Icons statt Glyphen, Karten mit Radius/Schatten, keine kaputten Bindings/Restglyphen. UI ist nicht unit-testbar; der manuelle Blick ist Teil des Task-Abschlusses.
- **Weglassen (passt nicht zu FLIPPO = offline/kostenlos/kein Konto):** Nutzerprofil/„Pro", Benachrichtigungs-Glocke, Header-Suche, Stock-Hero-Foto, „Daily Inspiration"-Zitat-Feed (höchstens dezente Empty-State-Note).
- Fonts sind OFL (Bricolage Grotesque, Source Serif 4) bzw. Apache-2.0 (Material Symbols, nur SVG-Pfade extrahiert) — bundlebar. OFL-Lizenzdatei mit einbetten.
- Windows-Pfade: Forward-Slash in Befehlen.

---

### Task 1: Fonts einbetten + App-Default-Font

**Files:**
- Create: `src/Flippo.App/Assets/Fonts/BricolageGrotesque.ttf`
- Create: `src/Flippo.App/Assets/Fonts/SourceSerif4.ttf`
- Create: `src/Flippo.App/Assets/Fonts/OFL.txt` (Lizenz, gemeinsame OFL für beide — Kurzhinweis + Verweis auf Google-Fonts-Quelle)
- Modify: `src/Flippo.App/Theme/Tokens.axaml` (FontFamily-Ressourcen ergänzen)
- Modify: `src/Flippo.App/App.axaml` (App-Default-Font = Inter)

**Interfaces:**
- Produces: `FontFamily`-Ressourcen `Font.Family.Display` (Bricolage), `Font.Family.Ui` (Inter), `Font.Family.Serif` (Source Serif 4) — von Task 4 (Styles) konsumiert.

- [ ] **Step 1: Fonts herunterladen** (`Assets\**` ist bereits als `AvaloniaResource` im csproj — kein csproj-Edit nötig)

```bash
cd "D:/Claude/Obsidian/FLIPPO-Desktop"
mkdir -p src/Flippo.App/Assets/Fonts
curl -L -o src/Flippo.App/Assets/Fonts/BricolageGrotesque.ttf \
  "https://github.com/google/fonts/raw/main/ofl/bricolagegrotesque/BricolageGrotesque%5Bopsz,wdth,wght%5D.ttf"
curl -L -o src/Flippo.App/Assets/Fonts/SourceSerif4.ttf \
  "https://github.com/google/fonts/raw/main/ofl/sourceserif4/SourceSerif4%5Bopsz,wght%5D.ttf"
```

Beide sind Variable Fonts (Gewichts-Achse `wght`); Avalonia 12 rendert sie, Gewicht wird über `FontWeight` gesteuert. Prüfen, dass beide Dateien > 100 KB sind (kein HTML-Fehler-Body):

```bash
ls -l src/Flippo.App/Assets/Fonts/*.ttf
```

Expected: zwei `.ttf` je mehrere hundert KB. OFL.txt mit knappem Lizenztext + Quelle anlegen.

- [ ] **Step 2: FontFamily-Ressourcen in Tokens.axaml** (in den Primitive-Block, vor den ThemeDictionaries)

```xml
<!-- ── Primitive: Font-Familien ── -->
<FontFamily x:Key="Font.Family.Display">avares://Flippo.App/Assets/Fonts/BricolageGrotesque.ttf#Bricolage Grotesque</FontFamily>
<FontFamily x:Key="Font.Family.Serif">avares://Flippo.App/Assets/Fonts/SourceSerif4.ttf#Source Serif 4</FontFamily>
<FontFamily x:Key="Font.Family.Ui">avares://Avalonia.Fonts.Inter/Assets#Inter</FontFamily>
```

- [ ] **Step 3: App-Default-Font auf Inter** — in `App.axaml` am `<Application>`-Wurzelelement ergänzen:

```xml
FontFamily="{DynamicResource Font.Family.Ui}"
```

(Setzt Inter als App-weiten Default; Bricolage/Serif werden gezielt über Style-Klassen gesetzt.)

- [ ] **Step 4: Build**

Run: `dotnet build src/Flippo.App -c Debug`
Expected: 0 Warnungen, Build erfolgreich.

- [ ] **Step 5: Sichtprüfung Fonts** — App starten, kurz ansehen: UI-Text wirkt als Inter (nicht System-Serif/Segoe-Fallback). (Bricolage/Serif werden erst in Task 4/5 sichtbar angewandt; hier nur: Inter lädt, kein Fallback-Bruch.)

Run: `dotnet run --project src/Flippo.App`

Falls der `#Family-Name`-Teil nicht matcht (Text fällt auf Systemfont zurück), den exakten internen Family-Namen prüfen und URI korrigieren, bevor weiter.

- [ ] **Step 6: Commit**

```bash
git add src/Flippo.App/Assets/Fonts src/Flippo.App/Theme/Tokens.axaml src/Flippo.App/App.axaml
git commit -m "Retheme T1: Fonts einbetten (Bricolage/Source Serif 4) + Inter als App-Default"
```

---

### Task 2: Tokens — Palette, Radien, Schatten, Type-Skala

**Files:**
- Modify: `src/Flippo.App/Theme/Tokens.axaml`

**Interfaces:**
- Produces: geänderte Werte für bestehende Keys (`Color.*`, `Radius.*`, `Font.*`) + neue Keys `Radius.Hero`, `Radius.Pill`, `Color.Tertiary`(+`.Soft`), `Brush.Tertiary`(+`.Soft`), `Shadow.Card`, `Shadow.CardHover`. Von allen späteren Tasks konsumiert.

- [ ] **Step 1: Light-Palette ersetzen** — im `<ResourceDictionary x:Key="Light">` die Farbwerte auf die Stitch-Palette setzen:

```xml
<Color x:Key="Color.Bg.App">#FFFAF9F6</Color>
<Color x:Key="Color.Surface.Card">#FFFFFFFF</Color>
<Color x:Key="Color.Surface.Subtle">#FFF4F3F1</Color>
<Color x:Key="Color.Border.Subtle">#FFE7E5E1</Color>
<Color x:Key="Color.Text.Primary">#FF1A1C1A</Color>
<Color x:Key="Color.Text.Secondary">#FF56585C</Color>
<Color x:Key="Color.Accent">#FF0040E0</Color>
<Color x:Key="Color.Accent.Soft">#1A0040E0</Color>
<Color x:Key="Color.Success">#FF006C4D</Color>
<Color x:Key="Color.Warning">#FFB07A1E</Color>
<Color x:Key="Color.Danger">#FFBA1A1A</Color>
<Color x:Key="Color.Tertiary">#FF993100</Color>
<Color x:Key="Color.Tertiary.Soft">#1A993100</Color>
<Color x:Key="SystemAccentColor">#FF0040E0</Color>
```

- [ ] **Step 2: Dark-Palette ersetzen** — im `<ResourceDictionary x:Key="Dark">` (warmes Dunkel, heller Indigo):

```xml
<Color x:Key="Color.Bg.App">#FF16150F</Color>
<Color x:Key="Color.Surface.Card">#FF211F18</Color>
<Color x:Key="Color.Surface.Subtle">#FF2B2920</Color>
<Color x:Key="Color.Border.Subtle">#FF3A382E</Color>
<Color x:Key="Color.Text.Primary">#FFF3F1EA</Color>
<Color x:Key="Color.Text.Secondary">#FFB0ADA2</Color>
<Color x:Key="Color.Accent">#FFAAB8FF</Color>
<Color x:Key="Color.Accent.Soft">#33AAB8FF</Color>
<Color x:Key="Color.Success">#FF6ED9AC</Color>
<Color x:Key="Color.Warning">#FFE0B056</Color>
<Color x:Key="Color.Danger">#FFF2B8B5</Color>
<Color x:Key="Color.Tertiary">#FFFFB59A</Color>
<Color x:Key="Color.Tertiary.Soft">#33FFB59A</Color>
<Color x:Key="SystemAccentColor">#FFAAB8FF</Color>
```

- [ ] **Step 3: Radien vergrößern + neue Radius-Keys** (Primitive-Block):

```xml
<CornerRadius x:Key="Radius.Control">12</CornerRadius>
<CornerRadius x:Key="Radius.Card">24</CornerRadius>
<CornerRadius x:Key="Radius.Hero">32</CornerRadius>
<CornerRadius x:Key="Radius.Pill">9999</CornerRadius>
<CornerRadius x:Key="Radius.Bar">6</CornerRadius>
```

- [ ] **Step 4: Type-Skala anheben** (Primitive-Block):

```xml
<x:Double x:Key="Font.Caption">12</x:Double>
<x:Double x:Key="Font.Body">14</x:Double>
<x:Double x:Key="Font.BodyLarge">17</x:Double>
<x:Double x:Key="Font.Section">21</x:Double>
<x:Double x:Key="Font.Title">30</x:Double>
<x:Double x:Key="Font.Metric">30</x:Double>
<x:Double x:Key="Font.Display">44</x:Double>
```

- [ ] **Step 5: Schatten- + Tertiary-Brushes** — nach den bestehenden `Brush.*`-Definitionen ergänzen:

```xml
<SolidColorBrush x:Key="Brush.Tertiary" Color="{DynamicResource Color.Tertiary}" />
<SolidColorBrush x:Key="Brush.Tertiary.Soft" Color="{DynamicResource Color.Tertiary.Soft}" />
<BoxShadows x:Key="Shadow.Card">0 4 20 0 #14000000</BoxShadows>
<BoxShadows x:Key="Shadow.CardHover">0 10 30 0 #1F000000</BoxShadows>
```

(`BoxShadows` als Ressource ist in Avalonia gültig und per `DynamicResource` an `Border.BoxShadow` bindbar.)

- [ ] **Step 6: Build + Sichtprüfung**

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnungen.
Run: `dotnet run --project src/Flippo.App` → App startet, warmer Off-White-Hintergrund, Indigo-Akzent an Buttons/Fokus sichtbar. (Karten haben noch alte Ränder — Schatten kommt in Task 4.)

- [ ] **Step 7: Test + Commit**

```bash
dotnet test
git add src/Flippo.App/Theme/Tokens.axaml
git commit -m "Retheme T2: Tokens — Indigo/warm-neutral Palette, große Radien, weiche Schatten, Type-Skala"
```

---

### Task 3: Icons.axaml — kuratiertes Material-Vektor-Set

**Files:**
- Create: `src/Flippo.App/Theme/Icons.axaml`
- Modify: `src/Flippo.App/App.axaml` (Icons.axaml in MergedDictionaries aufnehmen)

**Interfaces:**
- Produces: `StreamGeometry`-Ressourcen mit Key-Schema `Icon.<name>` für: `dashboard, decks, dictionary, statistics, history, settings, back, refresh, play, add, folder-add, book, analytics, search, close, edit, delete, chevron, streak, check, more, cloud, translate, download, upload`. Von Task 5–12 (Views/Dialoge) konsumiert.

- [ ] **Step 1: Icons.axaml anlegen** — `StreamGeometry`-Ressourcen aus Material-Symbols-Outlined-SVG-Pfaden (24px-Viewbox, `Fill`-Pfad-Daten). Beispielgerüst (Pfad-Daten pro Icon aus dem Material-Symbols-Repo `google/material-design-icons` bzw. fonts.google.com/icons übernehmen, Outlined-Style):

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Material Symbols Outlined (Apache-2.0), 24px-Viewbox. Nutzung via PathIcon Data={DynamicResource Icon.X}. -->
    <StreamGeometry x:Key="Icon.dashboard">M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z</StreamGeometry>
    <StreamGeometry x:Key="Icon.settings">M19.14 12.94c...</StreamGeometry>
    <!-- ... restliche Icons analog ... -->
</ResourceDictionary>
```

Vollständiges Icon-Set (Keys + Material-Name zum Nachschlagen des Pfads):
`Icon.dashboard`(dashboard), `Icon.decks`(style), `Icon.dictionary`(menu_book), `Icon.statistics`(bar_chart), `Icon.history`(history), `Icon.settings`(settings), `Icon.back`(arrow_back), `Icon.refresh`(refresh), `Icon.play`(play_arrow), `Icon.add`(add), `Icon.folder-add`(create_new_folder), `Icon.book`(library_books), `Icon.analytics`(analytics), `Icon.search`(search), `Icon.close`(close), `Icon.edit`(edit), `Icon.delete`(delete), `Icon.chevron`(chevron_right), `Icon.streak`(local_fire_department), `Icon.check`(check), `Icon.more`(more_horiz), `Icon.cloud`(cloud), `Icon.translate`(translate), `Icon.download`(download), `Icon.upload`(upload).

Die exakten Pfad-Daten pro Icon von fonts.google.com/icons (Outlined, Weight 400) bzw. aus `github.com/google/material-design-icons/…/24px.svg` (das `<path d="…">`-Attribut) übernehmen. Bei Bedarf per `curl` die SVG holen und das `d`-Attribut extrahieren.

- [ ] **Step 2: Icons.axaml einbinden** — in `App.axaml` in `ResourceDictionary.MergedDictionaries` neben Tokens.axaml:

```xml
<ResourceInclude Source="avares://Flippo.App/Theme/Icons.axaml"/>
```

- [ ] **Step 3: Smoke-Test** — vorübergehend ein `<PathIcon Data="{DynamicResource Icon.dashboard}" Width="24" Height="24"/>` in die Sidebar setzen, App starten, prüfen dass das Icon rendert (nicht leer/kaputt), dann wieder entfernen (die echte Einbindung folgt in Task 5).

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnungen. `dotnet run` → Icon sichtbar.

- [ ] **Step 4: Commit**

```bash
git add src/Flippo.App/Theme/Icons.axaml src/Flippo.App/App.axaml
git commit -m "Retheme T3: Icons.axaml — kuratiertes Material-Symbols-Vektor-Set"
```

---

### Task 4: Styles.axaml — Karten-Schatten, Typo auf neue Fonts, neue Klassen

**Files:**
- Modify: `src/Flippo.App/Theme/Styles.axaml`

**Interfaces:**
- Consumes: `Font.Family.*` (T1), `Radius.*`/`Shadow.*`/`Brush.*` (T2), `Icon.*` (T3).
- Produces: aktualisierte Klassen `app-card`, `page-title`, `section`, `metric`, `metric-lg`, `nav` + neue Klassen `display`, `serif`, `hero-card`, `metric-tile`, `streak-pill`, `quick-action`, `sidebar-cta`, `nav-active`, `icon`. Von Task 5–12 konsumiert.

- [ ] **Step 1: `app-card` auf weichen Schatten** — Rand entfernen (oder auf 0), `BoxShadow` setzen:

```xml
<Style Selector="Border.app-card">
    <Setter Property="Background" Value="{DynamicResource Brush.Surface.Card}" />
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Card}" />
    <Setter Property="Padding" Value="{DynamicResource Inset.Card}" />
    <Setter Property="BoxShadow" Value="{DynamicResource Shadow.Card}" />
</Style>
```

- [ ] **Step 2: Display-/Titel-/Sektion-Typo auf Bricolage** — `page-title`, `section` und neue `display`-Klasse bekommen `FontFamily="{DynamicResource Font.Family.Display}"`; `metric`/`metric-lg` ebenfalls (Kennzahlen wirken als Display). Beispiel:

```xml
<Style Selector="TextBlock.display">
    <Setter Property="FontFamily" Value="{DynamicResource Font.Family.Display}" />
    <Setter Property="FontSize" Value="{DynamicResource Font.Display}" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Text.Primary}" />
</Style>
```

(In `page-title`, `section`, `metric`, `metric-lg` jeweils `<Setter Property="FontFamily" Value="{DynamicResource Font.Family.Display}" />` ergänzen.)

- [ ] **Step 3: `serif`-Klasse (Lese-Stellen)**:

```xml
<Style Selector="TextBlock.serif">
    <Setter Property="FontFamily" Value="{DynamicResource Font.Family.Serif}" />
    <Setter Property="FontSize" Value="{DynamicResource Font.BodyLarge}" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Text.Primary}" />
    <Setter Property="LineHeight" Value="26" />
</Style>
```

- [ ] **Step 4: Icon-Hilfsklasse** — für `PathIcon` in Nav/Buttons:

```xml
<Style Selector="PathIcon.icon">
    <Setter Property="Width" Value="20" />
    <Setter Property="Height" Value="20" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Text.Secondary}" />
</Style>
```

- [ ] **Step 5: Nav-Aktiv-Variante** — `nav` behält Basis; `nav-active` bekommt Accent-Soft-Fläche + Accent-Text; der linke Indikator-Balken wird in Task 5 als separates `Border` pro Nav-Zeile gebaut (an `nav-active` gekoppelt). Ergänzen:

```xml
<Style Selector="Button.nav.nav-active /template/ ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource Brush.Accent.Soft}" />
</Style>
<Style Selector="Button.nav.nav-active">
    <Setter Property="Foreground" Value="{DynamicResource Brush.Accent}" />
    <Setter Property="FontWeight" Value="SemiBold" />
</Style>
```

- [ ] **Step 6: Bento-/CTA-Klassen** — `hero-card` (Radius.Hero, Accent-Hintergrund optional), `metric-tile` (wie app-card, kompakter), `streak-pill` (Radius.Pill, Tertiary/Accent-Soft-Fläche), `quick-action` (app-card + Hover-Lift via `:pointerover` → `Shadow.CardHover`), `sidebar-cta` (Accent-Fläche, Radius.Card, weißer Text). Konkret:

```xml
<Style Selector="Border.hero-card">
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Hero}" />
    <Setter Property="Padding" Value="28" />
    <Setter Property="Background" Value="{DynamicResource Brush.Surface.Card}" />
    <Setter Property="BoxShadow" Value="{DynamicResource Shadow.Card}" />
</Style>
<Style Selector="Border.metric-tile">
    <Setter Property="Background" Value="{DynamicResource Brush.Surface.Card}" />
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Card}" />
    <Setter Property="Padding" Value="18" />
    <Setter Property="BoxShadow" Value="{DynamicResource Shadow.Card}" />
</Style>
<Style Selector="Border.streak-pill">
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Pill}" />
    <Setter Property="Background" Value="{DynamicResource Brush.Tertiary.Soft}" />
    <Setter Property="Padding" Value="12,6" />
</Style>
<Style Selector="Border.quick-action">
    <Setter Property="Background" Value="{DynamicResource Brush.Surface.Card}" />
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Card}" />
    <Setter Property="Padding" Value="16" />
    <Setter Property="BoxShadow" Value="{DynamicResource Shadow.Card}" />
    <Setter Property="Transitions">
        <Transitions><BoxShadowsTransition Property="BoxShadow" Duration="0:0:0.15"/></Transitions>
    </Setter>
</Style>
<Style Selector="Border.quick-action:pointerover">
    <Setter Property="BoxShadow" Value="{DynamicResource Shadow.CardHover}" />
</Style>
<Style Selector="Border.sidebar-cta">
    <Setter Property="Background" Value="{DynamicResource Brush.Accent}" />
    <Setter Property="CornerRadius" Value="{DynamicResource Radius.Card}" />
    <Setter Property="Padding" Value="16" />
</Style>
```

- [ ] **Step 7: Build + Sichtprüfung** — App starten: Karten haben jetzt runde Ecken + weichen Schatten (kein harter Rand), Überschriften in Bricolage. Light + Dark ansehen.

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnungen. `dotnet run`.

- [ ] **Step 8: Test + Commit**

```bash
dotnet test
git add src/Flippo.App/Theme/Styles.axaml
git commit -m "Retheme T4: Styles — Karten-Schatten, Bricolage-Typo, Bento-/CTA-/Nav-Klassen"
```

---

### Task 5: MainWindow — Sidebar mit Icons, Aktiv-Indikator, „Jetzt lernen"-CTA

**Files:**
- Modify: `src/Flippo.App/Views/MainWindow.axaml`
- Modify: `src/Flippo.App/ViewModels/MainWindowViewModel.cs` (read-only `ActiveNav`-Property + Setzen in Show*-Commands)

**Interfaces:**
- Consumes: `Icon.*` (T3), `nav-active`/`sidebar-cta`/`icon` (T4), `Font.Family.Display` (T1).
- Produces: `ActiveNav`-Property (string, z.B. `"Dashboard"`), die die Sidebar-Buttons via `Classes.nav-active` togglen.

- [ ] **Step 1: `ActiveNav`-Property im VM** — read-only observable Property; in jedem `Show…Command` den passenden Wert setzen (`Dashboard`/`Sets`/`Dictionary`/`Statistics`/`History`/`Settings`). Reines Präsentations-Flag, keine Logikänderung. Beispiel (CommunityToolkit):

```csharp
[ObservableProperty]
private string _activeNav = "Dashboard";
```

In `ShowDashboard()` → `ActiveNav = "Dashboard";` usw. (analog für die anderen Show-Methoden).

- [ ] **Step 2: Sidebar-Kopf** — Wortmarke in Bricolage + Eyebrow (bestehende `App_Subtitle`), Zeile 98–100 ersetzen:

```xml
<StackPanel DockPanel.Dock="Top" Margin="4,2,4,20" Spacing="2">
    <TextBlock Text="FLIPPO" Classes="display" FontSize="26"/>
    <TextBlock Classes="eyebrow" Text="{loc:T App_Subtitle}"/>
</StackPanel>
```

- [ ] **Step 3: Nav-Einträge mit Icon + Aktiv-Indikator** — jeden `Button.nav` auf Icon+Label umstellen und `Classes` an `ActiveNav` koppeln. Muster pro Eintrag (Beispiel Dashboard):

```xml
<Button DockPanel.Dock="Top" Classes="nav" Command="{Binding ShowDashboardCommand}" Margin="0,0,0,4"
        Classes.nav-active="{Binding ActiveNav, Converter={x:Static loc:EqualsConverter.Instance}, ConverterParameter=Dashboard}">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <PathIcon Classes="icon" Data="{DynamicResource Icon.dashboard}"/>
        <TextBlock Text="{loc:T Nav_Dashboard}" VerticalAlignment="Center"/>
    </StackPanel>
</Button>
```

Icon-Zuordnung: Dashboard→`Icon.dashboard`, Sets→`Icon.decks`, Dictionary→`Icon.dictionary`, Statistics→`Icon.statistics`, Settings→`Icon.settings`, Back→`Icon.back` (Back-Button behält `IsVisible`-Binding). History fehlt aktuell in der Sidebar — optional als weiteren Nav-Eintrag mit `Icon.history` ergänzen (History ist bisher nur im Menü).

Falls `EqualsConverter` noch nicht existiert: als einfachen `IValueConverter` in `src/Flippo.App/Localization/` (oder `Converters/`) anlegen — `value?.ToString() == parameter?.ToString()`. (Alternativ, um jeglichen Converter zu vermeiden: pro Nav-Button ein `Style Selector` mit `DataTrigger` — aber der Converter ist knapper.) Dieser Converter ist Präsentations-Infrastruktur, kein Domänen-Code.

- [ ] **Step 4: „Jetzt lernen"-CTA unten** — vor dem Versions-`TextBlock` (Zeile 118) eine `sidebar-cta`-Karte einsetzen, die `LearnAllDueCommand` (Flashcard) auslöst:

```xml
<Border DockPanel.Dock="Bottom" Classes="sidebar-cta" Margin="0,12,0,12">
    <Button Background="Transparent" Padding="0" HorizontalAlignment="Stretch"
            Command="{Binding LearnAllDueCommand}" CommandParameter="Flashcard">
        <StackPanel Spacing="6">
            <PathIcon Data="{DynamicResource Icon.play}" Width="22" Height="22" Foreground="White"/>
            <TextBlock Text="{loc:T Nav_LearnNow}" Foreground="White" FontWeight="SemiBold"/>
        </StackPanel>
    </Button>
</Border>
```

`Nav_LearnNow` als neuen i18n-Key in allen Sprachdateien ergänzen (z.B. „Jetzt lernen" / „Study now"). **Kein** bestehender String wird geändert — nur additiv.

- [ ] **Step 5: Build + Sichtprüfung** — App starten: Sidebar zeigt Bricolage-Wortmarke, Nav mit Icons, aktive Seite hervorgehoben (beim Navigieren wandert die Hervorhebung mit), CTA-Karte unten. Durch alle Seiten klicken → Aktiv-Zustand korrekt. Light + Dark.

Run: `dotnet build src/Flippo.App -c Debug` → 0 Warnungen. `dotnet run`.

- [ ] **Step 6: Test + Commit**

```bash
dotnet test
git add src/Flippo.App/Views/MainWindow.axaml src/Flippo.App/ViewModels/MainWindowViewModel.cs src/Flippo.App/Localization src/Flippo.App/Converters 2>/dev/null
git commit -m "Retheme T5: Sidebar — Bricolage-Wortmarke, Icon-Nav mit Aktiv-Indikator, Jetzt-lernen-CTA"
```

---

### Task 6: DashboardView — Hero-CTA + Bento

**Files:**
- Modify: `src/Flippo.App/Views/DashboardView.axaml`

**Interfaces:**
- Consumes: `hero-card`/`metric-tile`/`streak-pill`/`quick-action` (T4), `Icon.*` (T3), `display`/`serif` (T4).

**Transformation (Verhalten/Bindings unverändert — nur Struktur/Klassen/Icons):**

- [ ] **Step 1** Aktuelle DashboardView lesen; bestehende Bindings (Fälligkeitszahlen, Begrüßung, Commands) notieren — sie bleiben 1:1.
- [ ] **Step 2** Kopf: Begrüßung als `display`/`page-title` (Bricolage) + `streak-pill` rechts (falls eine Streak-/Serien-Kennzahl vorhanden ist; sonst Pill weglassen — **keine** erfundene Kennzahl).
- [ ] **Step 3** Hero-„Jetzt lernen"-Karte (`hero-card`): große Fälligkeitszahl (`metric-lg`/`display`) + Primär-Button mit `Icon.play` → bestehendes Lern-Command. **Kein** Stock-Bild, **kein** Konto/Pro.
- [ ] **Step 4** Bento-Grid (`UniformGrid`/`Grid`): 3 `metric-tile` (fällig/neu/Problemkarten — nur Kennzahlen, die es im VM schon gibt) mit optionalem Mini-Fortschrittsbalken (`Border` mit `Radius.Bar`), „Letzte Session"-Karte (falls VM-Daten existieren), `quick-action`-Grid (2×2) für vorhandene Aktionen (z.B. Import, Neues Set, Statistik, Nachschlagen) je mit Icon.
- [ ] **Step 5** Empty-/All-done-State neu stylen; optional dezente `serif`-Note als Lese-Element (statt „Daily Inspiration"-Feed).
- [ ] **Step 6** Build (0 Warnungen) + Sichtprüfung Light/Dark: Bento sitzt, Hero-CTA startet Lernen, keine Restglyphen/kaputten Bindings.
- [ ] **Step 7** `dotnet test` + Commit `Retheme T6: Dashboard — Hero-CTA + Bento-Kennzahlen`.

---

### Task 7: SetsOverviewView + SetDetailView (DataGrid)

**Files:**
- Modify: `src/Flippo.App/Views/SetsOverviewView.axaml`
- Modify: `src/Flippo.App/Views/SetDetailView.axaml`

- [ ] **Step 1** Beide Views lesen; Bindings/Commands bleiben.
- [ ] **Step 2** SetsOverview: Karten/Listeneinträge auf `app-card`/neue Radien; Aktions-Buttons (Lernen/Bearbeiten) mit Icons (`Icon.play`/`Icon.edit`); Titel als Bricolage. Text-Glyphen durch `PathIcon` ersetzen.
- [ ] **Step 3** SetDetail: Kopfbereich als Bricolage-Titel; DataGrid an neue Tokens — Container in `app-card` mit `Radius.Card` + `ClipToBounds`, Spaltenkopf/Zeilenhöhe/Selektionsfarbe über `Brush.*`. Zeilen-Aktions-Glyphen (bearbeiten/löschen `Icon.edit`/`Icon.delete`) als `PathIcon`.
- [ ] **Step 4** Build (0 Warnungen) + Sichtprüfung Light/Dark (DataGrid lesbar, Selektion sichtbar, Icons statt Glyphen).
- [ ] **Step 5** `dotnet test` + Commit `Retheme T7: Karteien-Übersicht + Detail (DataGrid) auf neue Tokens`.

---

### Task 8: LearnSessionView + SessionSummaryView

**Files:**
- Modify: `src/Flippo.App/Views/LearnSessionView.axaml`
- Modify: `src/Flippo.App/Views/SessionSummaryView.axaml`

- [ ] **Step 1** Beide lesen; Lern-Logik/Bindings unverändert.
- [ ] **Step 2** LearnSession: Karteikarte als große `Border` mit `Radius.Hero` + `Shadow.Card`; **Vokabel-Frage in `serif`** (editoriale Lese-Stelle); Bewertungs-/Weiter-Buttons mit Icons; Fortschritt/Zähler als `caption`/`metric`. Glyphen (◀ ▶ ✓ ✗ ↻) → `PathIcon` (`Icon.back`/`Icon.chevron`/`Icon.check`/`Icon.close`/`Icon.refresh`).
- [ ] **Step 3** SessionSummary: Ergebnis-Kennzahlen als `metric` (Bricolage) in `metric-tile`; „nochmal/fertig"-Buttons mit Icons.
- [ ] **Step 4** Build (0 Warnungen) + Sichtprüfung: eine echte Lernsession durchklicken (Flashcard), Frage in Serif, Karten-Look sitzt, Zusammenfassung korrekt. Light + Dark.
- [ ] **Step 5** `dotnet test` + Commit `Retheme T8: Lernsession + Zusammenfassung — Karten-Look, Serif-Frage`.

---

### Task 9: StatisticsView

**Files:**
- Modify: `src/Flippo.App/Views/StatisticsView.axaml`

- [ ] **Step 1** Lesen; Bindings bleiben.
- [ ] **Step 2** Kacheln auf `metric-tile`/`app-card`; Kennzahlen Bricolage; Balken/Charts auf `Brush.Accent`/`Brush.Success`/`Brush.Tertiary` (Rost als Extra-Facette) + `Radius.Bar`. Glyphen → Icons.
- [ ] **Step 3** Build (0 Warnungen) + Sichtprüfung Light/Dark (Diagrammfarben lesbar, kein Kontrastbruch in Dark).
- [ ] **Step 4** `dotnet test` + Commit `Retheme T9: Statistik auf neue Palette/Kacheln`.

---

### Task 10: DictionaryListView + UserDictionaryDetailView

**Files:**
- Modify: `src/Flippo.App/Views/DictionaryListView.axaml`
- Modify: `src/Flippo.App/Views/UserDictionaryDetailView.axaml`

- [ ] **Step 1** Beide lesen; Bindings bleiben.
- [ ] **Step 2** Listen/Detail auf `app-card`/neue Radien; Wörterbuch-Einträge in Inter, Beispiel-/Lesetext optional `serif`; Aktions-Glyphen → `PathIcon` (`Icon.add`/`Icon.edit`/`Icon.delete`/`Icon.translate`).
- [ ] **Step 3** Build (0 Warnungen) + Sichtprüfung Light/Dark.
- [ ] **Step 4** `dotnet test` + Commit `Retheme T10: Nachschlagen (Liste + Detail) auf neue Tokens`.

---

### Task 11: HistoryView + SettingsView

**Files:**
- Modify: `src/Flippo.App/Views/HistoryView.axaml`
- Modify: `src/Flippo.App/Views/SettingsView.axaml`

- [ ] **Step 1** Beide lesen; Bindings bleiben.
- [ ] **Step 2** History: Zeitleiste/Einträge auf `app-card`; Glyphen → Icons (`Icon.history`/`Icon.check`). Settings: Abschnitte als `section` (Bricolage) in `app-card`; Steuerelemente (Theme-Toggle, UI-Scale, Backup-Ziele) an neue Radien/Farben; Backup-/Cloud-Aktionen mit `Icon.cloud`/`Icon.download`/`Icon.upload`.
- [ ] **Step 3** Build (0 Warnungen) + Sichtprüfung Light/Dark; Theme-Umschalter live testen (Light↔Dark während App läuft — beide korrekt).
- [ ] **Step 4** `dotnet test` + Commit `Retheme T11: Verlauf + Einstellungen auf neue Tokens`.

---

### Task 12: Dialoge

**Files:**
- Modify: `src/Flippo.App/Views/SetEditorWindow.axaml`, `DictEntryEditorWindow.axaml`, `DictionaryEditorWindow.axaml`, `SetChooserWindow.axaml`, `BackupChooserWindow.axaml`, `ProviderChooserWindow.axaml`, `ThemeSetPickerWindow.axaml`, `ImportPreviewWindow.axaml`, `FileImportWindow.axaml`, `MessageWindow.axaml`, `ConfirmWindow.axaml`

- [ ] **Step 1** Alle Dialog-Fenster lesen; Bindings/Commands bleiben.
- [ ] **Step 2** Einheitlich: Fenster-Hintergrund `Brush.Bg.App`; Inhalts-Container `app-card`; Titel als `section`/`page-title` (Bricolage); Primär-Button Accent-gefüllt, Sekundär ruhig; Radien via `Radius.Control`; etwaige Glyphen → `PathIcon` (`Icon.close`/`Icon.check`). `FileImportWindow` behält seine Monospace-Vorschau (Consolas) — nur Rahmen/Buttons angleichen.
- [ ] **Step 3** Build (0 Warnungen) + Sichtprüfung: jeden Dialog einmal öffnen (Neues Set, Karte bearbeiten, Backup-Ziel wählen, Themenset-Picker, Import-Vorschau, Bestätigen/Meldung) — Light + Dark.
- [ ] **Step 4** `dotnet test` + Commit `Retheme T12: Dialoge auf neue Karten-/Button-/Font-Styles`.

---

### Task 13: Dark-Mode-Feinschliff + Gesamt-Visual-QA

**Files:**
- Modify: `src/Flippo.App/Theme/Tokens.axaml` (nur falls Dark-Feinschliff nötig)
- ggf. einzelne Views (Korrekturen)

- [ ] **Step 1** App in **Dark** starten, systematisch jede Seite + jeden Dialog durchgehen: Kontrast Text/Fläche, Schatten dezent genug, Accent lesbar, Charts/Balken unterscheidbar, keine „weißen Blitzer" (hartkodierte helle Flächen). Gefundene hartkodierte Farben in Views auf `Brush.*` umstellen.
- [ ] **Step 2** App in **Light** starten, gleicher Durchgang.
- [ ] **Step 3** Regressions-Blick: eine Lernsession, ein Import, ein Backup-Export, Katalog-Öffnen — funktional wie vorher (keine Änderung erwartet).
- [ ] **Step 4** `dotnet build` (0 Warnungen) + volle `dotnet test` **grün**.
- [ ] **Step 5** Commit `Retheme T13: Dark-Mode-Feinschliff + Visual-QA` (nur falls Änderungen; sonst überspringen).

---

## Self-Review (Autor)

- **Spec-Abdeckung:** Fonts (T1), Tokens/Palette/Radii/Schatten/Type (T2), Icons (T3), Styles (T4), Sidebar (T5), Dashboard (T6), Sets+Detail (T7), Learn+Summary (T8), Statistics (T9), Dictionaries (T10), History+Settings (T11), Dialoge (T12), Dark+QA (T13) — alle Spec-Abschnitte abgedeckt.
- **Weglassungen** (Profil/Pro/Glocke/Header-Suche/Stock-Bild/Zitat-Feed) in Global Constraints + T5/T6 verankert.
- **VM-Ausnahme** (`ActiveNav`) explizit als einzige gestattete VM-Berührung markiert, testneutral.
- **Namensstabilität:** bestehende Ressourcen-Keys behalten Namen; neue nur additiv (`Radius.Hero/Pill`, `Color/Brush.Tertiary*`, `Shadow.Card*`, `Font.Family.*`, `Icon.*`, i18n `Nav_LearnNow`).
- **Kein Placeholder-Code** in Fundament-Tasks; per-View-Tasks sind Transformations-Kontrakte gegen vorhandene, beim Ausführen gelesene Dateien (Retheme-typisch).
