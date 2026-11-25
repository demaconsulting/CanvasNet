<#
.SYNOPSIS
    Developer tool: regenerates the MathML reference layout goldens (test/DemaConsulting.CanvasNet.MathMl.Tests/
    Fixtures/reference/*.layout.json) from the reference implementation.

.DESCRIPTION
    Not part of the build, the tests, or CI. It runs render-mathml.mjs (MathML Core in headless Chrome, through the
    puppeteer of the repository's pinned mermaid-cli, with the bundled Noto Sans Math font) on every fixture
    Fixtures/reference/*.mml at a font size of 100 CSS pixels and writes the layout files that
    MathMlReferenceLayoutTests compares CanvasNet's layout with. Fixtures/reference/README.md records the Chrome
    version the goldens were made with and every known difference. A golden change in a pull request is reviewed like
    code: the diff shows which boxes moved.

    Needs Node.js 20 or later and "npm install" in the repository root (it installs @mermaid-js/mermaid-cli, which
    brings puppeteer with its headless Chrome).

.PARAMETER Fixture
    The names of the fixtures to regenerate (without .mml; default: every fixture).

.PARAMETER PngDirectory
    A folder to also write Chrome's picture of each fixture to (for a visual comparison; not committed).
#>
[CmdletBinding()]
param(
    [string[]]$Fixture,

    [string]$PngDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$folder = Join-Path $PSScriptRoot '../../test/DemaConsulting.CanvasNet.MathMl.Tests/Fixtures/reference'
$files = @(Get-ChildItem -Path $folder -Filter '*.mml' | Where-Object { -not $Fixture -or $Fixture -contains $_.BaseName } | Sort-Object Name)
if ($files.Count -eq 0) {
    throw 'No fixture matches.'
}

$arguments = @((Join-Path $PSScriptRoot 'render-mathml.mjs'), '--font-size', '100', '--layout-dir', $folder)
if ($PngDirectory) {
    $arguments += @('--png-dir', $PngDirectory)
}

& node @arguments @($files | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) {
    throw "render-mathml.mjs failed with exit code $LASTEXITCODE."
}
