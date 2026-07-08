# FLIPPO Desktop — Retheme auf die Stitch-Designrichtung (Design)

**Datum:** 2026-07-08
**Kontext:** Das bestehende „ruhig-professionelle" Token-Design gefällt nicht. Ein per Google Stitch
generierter Entwurf (warm-editorial: Bricolage-Grotesque-Headlines, Indigo-Akzent, Bento-Karten,
große Radien, weiche Schatten) wurde als Richtung abgesegnet. Dieser Retheme setzt ihn in der
ganzen App um.

## Mit Mark entschieden

- **Richtung:** Stitch-Entwurf (warm-editorial, Indigo, Bento, distinktiv statt templatehaft).
- **Umfang:** die **ganze App** (alle Views + Dialoge), nicht nur Dashboard.
- **Icons:** **kuratiertes Material-Symbols-Vektor-Set** (~20 `PathIcon`-Geometrien), NICHT die ganze
  Icon-Font einbetten.
- **Serifen:** **sparsam** — Source Serif 4 nur an Lese-Stellen; Inter für UI/Daten/Listen.

## Ziel & Nicht-Ziele

**Ziel:** Das Look-and-Feel der ganzen Desktop-App auf die Stitch-Richtung heben — neue Palette,
Fonts, Icons, Karten-Sprache (große Radien + weiche Schatten), Bento-Dashboard, gestylte Sidebar.

**Nicht-Ziele / bewusst weglassen (passen nicht zu FLIPPO = offline, kostenlos, kein Konto):**
Stitchs Nutzerprofil „Taylor S. · Pro Learner", Benachrichtigungs-Glocke, Header-Suche, Stock-Hero-
Foto, „Business Spanish"-Platzhalter, „Daily Inspiration"-Zitat (optional, kann als dezente
Empty-State-Note bleiben). Keine neuen Features, keine neue Navigation, keine Logikänderung.

## Constraints

- **Reiner Präsentations-Umbau:** keine Änderung an ViewModels, Services, Domain, DB, Backup-Format,
  i18n-Strings. Alle **226 bestehenden Tests bleiben grün** (kein Logik-Test wird berührt).
- Zentral über `src/Flippo.App/Theme/Tokens.axaml` + `Styles.axaml` gesteuert (3-Ebenen-Token-System
  bleibt: Primitive → semantic Brush/Color per ThemeDictionary → Style-Klassen). `DynamicResource`
  überall (Cross-Dictionary-Timing), NICHT StaticResource.
- Fonts als `AvaloniaResource` eingebettet (OFL/Apache, alle bundlebar); ~1–2 MB Binary-Zuwachs ok.
- Light **und** Dark (Stitch-Export war nur Light → Dark-Palette wird abgeleitet).
- Keine neuen NuGet-Pakete außer ggf. dem vorhandenen `Avalonia.Fonts.Inter`.

## Teil 1 — Fonts (`src/Flippo.App/Assets/Fonts/`, eingebettet)

- **Bricolage Grotesque** (OFL) — Headlines/Display. `.ttf` von Google Fonts / github.com/google/fonts.
- **Inter** (bereits als Paket vorhanden) — UI, Daten, Listen, DataGrids. **App-Default-Font.**
- **Source Serif 4** (OFL) — nur editoriale Lese-Stellen (Dashboard-Prosa, Empty-States, evtl.
  Karten-Frage im Lernmodus).
- Einbindung: `<AvaloniaResource Include="Assets\Fonts\**" />`; `FontFamily`-Ressourcen in Tokens
  (`Font.Family.Display` = Bricolage, `Font.Family.Ui` = Inter, `Font.Family.Serif` = Source Serif 4)
  via `avares://Flippo.App/Assets/Fonts/#<Family Name>`. App-Default-Font (Inter) in `App.axaml`
  über `FluentTheme` bzw. eine `Window`/`Application`-`FontFamily`-Angabe.

## Teil 2 — Tokens (`Theme/Tokens.axaml` überarbeiten)

**Palette Light** (aus dem Stitch-Export; Hex mit Alpha-Präfix `#FF`):
- `Color.Accent` (Primär/Indigo) `#FF0040E0`; `Color.Accent.Soft` `#220040E0`.
- `Color.Bg.App` `#FFFAF9F6` (warm off-white); `Color.Surface.Card` `#FFFFFFFF`;
  `Color.Surface.Subtle` `#FFF4F3F1`.
- `Color.Border.Subtle` `#FFE3E2E0` (weicher als bisher; Ränder werden ohnehin durch Schatten
  entlastet).
- `Color.Text.Primary` `#FF1A1C1A`; `Color.Text.Secondary` `#FF434656`.
- `Color.Success` (Grün) `#FF006C4D`; `Color.Warning` (Bernstein) `#FFB07A1E`;
  `Color.Danger` `#FFBA1A1A`; **neu** `Color.Tertiary` (Rost) `#FF993100` als Extra-Akzent
  (z.B. Score/Statistik-Facetten). `SystemAccentColor` = Indigo.

**Palette Dark** (abgeleitet, gleiche Rollen): warmes Dunkel `Color.Bg.App` ~`#FF16150F`/neutral-dunkel,
Karten ~`#FF23221E`, Text hell, **Accent** heller Indigo `#FFB8C3FF`, Erfolg/Warnung/Danger/Tertiary
in helleren Varianten. (Genaue Dark-Werte im Plan festlegen, an die neue Light-Palette angepasst.)

**Radii** (größer): `Radius.Control` 12, `Radius.Card` 24, **neu** `Radius.Hero` 32, `Radius.Pill`
9999 (voll). **Bar** 6.

**Schatten** (neu): weicher Karten-Schatten als `BoxShadows`-Ressource `Shadow.Card`
(`0 4 20 0 #0D000000` ≈ rgba(0,0,0,0.05)); optional `Shadow.CardHover` etwas stärker. In Dark
dezenter/aus.

**Type-Skala** (an Stitch angeglichen, Desktop-tauglich): `Font.Display` 40–48 (Bricolage 800),
`Font.Title` 28–32 (Bricolage 700), `Font.Section` 20–24 (Bricolage 600), `Font.BodyLarge` 18–20,
`Font.Body` 15–16 (Inter), `Font.Caption` 12–13 (Inter). Konkrete Werte im Plan.

**Semantic Brushes**: bestehende `Brush.*` beibehalten (Namen stabil!) + `Brush.Tertiary`,
`Brush.Tertiary.Soft` ergänzen, damit Views nicht alle umbenannt werden müssen.

## Teil 3 — Icons (`Theme/Icons.axaml`, neu)

- Kuratiertes Set der tatsächlich genutzten Icons als `StreamGeometry`/`PathGeometry`-Ressourcen
  (SVG-Pfade aus Material Symbols Outlined, Apache-2.0). Mindestset (aus Stitch + heutigen Glyphen):
  `dashboard, style (decks), search, bar_chart, history, settings, play_arrow, add_circle,
  create_new_folder, library_books, analytics, menu_book, refresh, arrow_back, close, edit, delete,
  chevron_right, local_fire_department (streak), check, more_horiz, folder, cloud, translate`.
- Verwendung via `PathIcon`/`Path` (Fill = `DynamicResource Brush.*`), Größe über Style. Text-Glyphen
  (↻ ← ✕ → ⋯ ◀ ▶) in allen Views durch Icons ersetzen.
- Kein Icon-Font im Binary (Größe/Ligatur-Fummelei vermieden).

## Teil 4 — Styles (`Theme/Styles.axaml` überarbeiten)

- `Border.app-card`: Karten-Surface, `Radius.Card`, **`Shadow.Card`** statt (oder plus dezentem)
  Rand.
- `Button.nav`: Sidebar-Item = Icon + Label, Hover-Fläche, **Aktiv-Indikator** (linker 4px-Balken in
  Accent) über eine `:selected`/`nav-active`-Variante.
- Neue Klassen: `hero-card` (großer Radius, Hero-CTA), `metric-tile` (Kennzahl + Mini-Fortschrittsbalken),
  `streak-pill` (Pill mit Icon), `quick-action` (Karte mit Icon-Kreis + Label, Hover-Invert),
  `sidebar-cta` (die „Jetzt lernen"-Karte unten).
- Typo-Klassen auf neue Fonts/Größen: `page-title`/`display` → Bricolage; `section`/`headline` →
  Bricolage; `body`/`caption`/`metric-label` → Inter; **neu** `serif` → Source Serif 4 (nur wo
  bewusst gesetzt).
- `Button`-Grundstil (Accent/normal): größere Radien, gefülltes Accent-Primary, ruhige Sekundär-
  Buttons.

## Teil 5 — Views (Feinschliff, funktional unverändert)

Reihenfolge nach Sichtbarkeit. Jede View: Style-Klassen/Icons/Layout an die neuen Tokens ziehen,
Verhalten/Bindings unangetastet.

1. **MainWindow (Sidebar + Shell):** Bricolage-Wortmarke „FLIPPO" + Eyebrow, Nav mit Icons +
   Aktiv-Indikator, **„Jetzt lernen"-CTA-Karte unten** (ersetzt Profil/Pro — nur der CTA), Version im
   Footer. Content-Bereich Hintergrund `Brush.Bg.App`.
2. **Dashboard** (Leit-Screen): Begrüßung + `streak-pill`; **Hero-„Jetzt lernen"-Karte** (Fälligkeits-
   zahl + CTA; ohne Stock-Bild/Konto); Bento: 3 `metric-tile` (Heute fällig/Neu/Problemkarten mit
   Mini-Balken), „Letzte Session"-Karte, `quick-action`-2×2-Grid. Empty-/All-done-States neu gestylt.
3. **Karteien-Übersicht**, **Karteil-Detail** (DataGrid: Kopf/Zeilen/Selektion an neue Tokens,
   Radien am Container), **Lernsession** (Karten-Look neu; Frage optional in Serif), **Statistik**
   (Kacheln/Charts an neue Palette, Rost als Extra-Facette), **Nachschlagen** (Liste/Detail),
   **Verlauf**, **Einstellungen**.
4. **Dialoge:** SetEditor, CardEditor, SetChooser, BackupChooser, ProviderChooser, Dict-Editoren,
   Message/Confirm, ImportPreview, FileImport, ThemeSetPicker — Karten-/Button-/Font-Styles neu.

## Teil 6 — Dark Mode

Stitch-Export ist Light-only → passende Dark-Palette ableiten (warmes Dunkel, heller Indigo-Accent,
Schatten dezenter/aus, Ränder etwas präsenter). Über die bestehende `Dark`-`ResourceDictionary` in
`Tokens.axaml`. Dark-Feinschliff als eigener Durchgang nach den Views.

## Teil 7 — Verify

Look ist nicht unit-testbar. Gate je Baustein:
1. `dotnet build` **0 Warnungen** (`TreatWarningsAsErrors`) + volle `dotnet test` **226 grün**
   (unverändert — nur Präsentation).
2. **Manueller Visual-Check** pro View (Light **und** Dark): App starten, jede Seite/Dialog ansehen —
   Fonts geladen (Bricolage-Headlines, Inter-UI), Icons statt Glyphen, Karten mit Schatten/Radien,
   Dashboard-Bento, Sidebar-CTA, keine kaputten Bindings/Restglyphen.
3. Regressions-Blick: keine funktionale Änderung (Lernen/Import/Backup/Katalog laufen wie vorher).

## Ausführungsreihenfolge (Hinweis für den Plan)

Fundament zuerst (Fonts einbetten → Tokens-Palette/Radii/Schatten/Type → Icons.axaml → Kern-Styles
inkl. Sidebar), dann Dashboard (Leit-Screen, prüft das Fundament visuell), dann die übrigen Views
einzeln, dann Dialoge, dann Dark-Mode-Feinschliff, dann Gesamt-Visual-QA. Jeder Baustein: Build grün +
manueller Blick. Subagent-getrieben; UI-Bausteine sind build+manual, nicht unit-getestet.
