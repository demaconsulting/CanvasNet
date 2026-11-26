<#
.SYNOPSIS
    Developer tool: regenerates the MathML reference layout goldens (test/DemaConsulting.CanvasNet.MathMl.Tests/
    Fixtures/reference/*.layout.json, or Fixtures/reference-firefox/*.layout.json with -Browser Firefox) from the
    reference implementation.

.DESCRIPTION
    Not part of the build, the tests, or CI. It runs render-mathml.mjs (MathML Core in headless Chrome, through the
    puppeteer of the repository's pinned mermaid-cli, with the bundled Noto Sans Math font) on every fixture
    Fixtures/reference/*.mml at a font size of 100 CSS pixels and writes the layout files that
    MathMlReferenceLayoutTests compares CanvasNet's layout with. Fixtures/reference/README.md records the Chrome
    version the goldens were made with and every known difference. A golden change in a pull request is reviewed like
    code: the diff shows which boxes moved.

    With -Browser Firefox it lays out the fixtures of Fixtures/reference-firefox instead, in headless Firefox: the
    reference of the MathML 3 table attributes (columnalign, rowalign, columnspacing, rowspacing, framespacing,
    columnlines, rowlines, frame) that CanvasNet supports and Chrome ignores. Fixtures/reference-firefox/README.md
    records the Firefox version and what MathMlReferenceLayoutTests compares.

    Needs Node.js 20 or later and "npm install" in the repository root (it installs @mermaid-js/mermaid-cli, which
    brings puppeteer with its headless Chrome). Firefox needs a one-time install into the puppeteer cache (outside the
    repository) of the Firefox version that the pinned puppeteer expects: npx puppeteer browsers install firefox

.PARAMETER Fixture
    The names of the fixtures to regenerate (without .mml; default: every fixture).

.PARAMETER PngDirectory
    A folder to also write the browser's picture of each fixture to (for a visual comparison; not committed).

.PARAMETER Browser
    The reference browser: Chrome (default; Fixtures/reference) or Firefox (Fixtures/reference-firefox).
#>
[CmdletBinding()]
param(
    [string[]]$Fixture,

    [string]$PngDirectory,

    [ValidateSet('Chrome', 'Firefox')]
    [string]$Browser = 'Chrome'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$subfolder = if ($Browser -eq 'Firefox') { 'reference-firefox' } else { 'reference' }
$folder = Join-Path $PSScriptRoot "../../test/DemaConsulting.CanvasNet.MathMl.Tests/Fixtures/$subfolder"
$files = @(Get-ChildItem -Path $folder -Filter '*.mml' | Where-Object { -not $Fixture -or $Fixture -contains $_.BaseName } | Sort-Object Name)
if ($files.Count -eq 0) {
    throw 'No fixture matches.'
}

$arguments = @((Join-Path $PSScriptRoot 'render-mathml.mjs'), '--browser', $Browser.ToLowerInvariant(), '--font-size', '100', '--layout-dir', $folder)
if ($PngDirectory) {
    $arguments += @('--png-dir', $PngDirectory)
}

& node @arguments @($files | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) {
    throw "render-mathml.mjs failed with exit code $LASTEXITCODE."
}
