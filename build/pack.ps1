<#
.SYNOPSIS
    FLIPPO Desktop – Velopack-Paketierung (P8).

.DESCRIPTION
    Baut ein self-contained Publish (kein Single-File, kein Trimming, kein ReadyToRun –
    EF Core ist nicht trim-safe) und schnürt daraus mit Velopack (vpk) ein Installer- +
    Portable-Paket samt Update-Feed-Dateien (RELEASES).

    Voraussetzung: Velopack-CLI installiert ->  dotnet tool install -g vpk

.PARAMETER Version
    SemVer der Release-Version (z.B. 0.1.0). MUSS über Releases monoton steigen.

.PARAMETER Runtime
    .NET RID des Zielsystems (Default win-x64).

.EXAMPLE
    pwsh build/pack.ps1 -Version 0.1.0
#>
param(
    [string]$Version = "0.1.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root       = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $root "src/Flippo.App/Flippo.App.csproj"
$publishDir = Join-Path $root "publish"
$releaseDir = Join-Path $root "releases"

Write-Host "==> FLIPPO Desktop packen  (Version $Version, Runtime $Runtime)" -ForegroundColor Cyan

# 1. Sauberes self-contained Publish (Flags gemäß Plan Abschnitt 9)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project `
    -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen (Exit $LASTEXITCODE)" }

# 2. Velopack-Paket schnüren (Installer + Portable + RELEASES-Feed)
vpk pack `
    --packId FLIPPO `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe Flippo.App.exe `
    --packTitle "FLIPPO Desktop" `
    --packAuthors "Solutionworx UG" `
    --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack fehlgeschlagen (Exit $LASTEXITCODE)" }

Write-Host "==> Fertig. Artefakte in: $releaseDir" -ForegroundColor Green
Get-ChildItem $releaseDir | Select-Object Name, Length | Format-Table -AutoSize
