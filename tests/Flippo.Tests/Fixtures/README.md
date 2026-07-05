# Test-Fixtures

## android-backup-v2.json (Mark-Aufgabe)

Hierher gehört ein **echter Export der Android-App** (Einstellungen → Backup exportieren),
umbenannt zu `android-backup-v2.json`.

Sobald die Datei liegt: in `../Backup/AndroidFixtureTests.cs` das `Skip` entfernen — dann
verifizieren die Tests, dass der Desktop-Parser die echte Android-Datei fehlerfrei liest und
ein Import→Export→Import semantisch stabil bleibt.

Die Datei wird per csproj-Glob (`Fixtures/**/*.json`) automatisch ins Test-Output kopiert.
