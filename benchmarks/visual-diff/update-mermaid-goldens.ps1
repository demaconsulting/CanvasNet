<#
.SYNOPSIS
    Developer tool: regenerates the Mermaid layout goldens (test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/
    reference/*.layout.json) from the reference implementation.

.DESCRIPTION
    Not part of the build, the tests, or CI. It runs render-mermaid.mjs (the repository's pinned mermaid-cli and
    mermaid in headless Chrome, with liberation.css) on every flowchart fixture of
    test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/flowchart and writes the layout files that
    MermaidReferenceLayoutTests compares CanvasNet's layout with.

    The fixtures in $Excluded get no golden; Fixtures/reference/README.md gives the reason of each. A golden change
    in a pull request is reviewed like code: the diff shows which coordinates moved.

    Needs Node.js 20 or later and "npm ci" in the repository root (it installs @mermaid-js/mermaid-cli, mermaid and
    puppeteer with its headless Chrome).

.PARAMETER Fixture
    The names of the fixtures to regenerate (without .mmd; default: every fixture that is not excluded).
#>
[CmdletBinding()]
param(
    [string[]]$Fixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Fixtures whose reference depends on the machine (see Fixtures/reference/README.md)
$Excluded = @('38-unicode')

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fixtures = Join-Path $repoRoot 'test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures'
$sourceDir = Join-Path $fixtures 'flowchart'
$goldenDir = Join-Path $fixtures 'reference'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is needed (node was not found on the path).'
}
if (-not (Test-Path (Join-Path $repoRoot 'node_modules/@mermaid-js/mermaid-cli/package.json'))) {
    throw 'The pinned mermaid-cli is not installed: run "npm ci" in the repository root first.'
}

$files = @(Get-ChildItem -LiteralPath $sourceDir -Filter '*.mmd' | Sort-Object Name | Where-Object {
        $name = $_.BaseName
        ($Excluded -notcontains $name) -and (-not $Fixture -or $Fixture -contains $name)
    } | ForEach-Object { $_.FullName })
if ($files.Count -eq 0) {
    throw 'No fixture to regenerate.'
}

node (Join-Path $PSScriptRoot 'render-mermaid.mjs') --layout-dir $goldenDir @files
if ($LASTEXITCODE -ne 0) {
    throw 'render-mermaid.mjs failed (see the message above).'
}

Write-Host "Wrote $($files.Count) golden(s) to $goldenDir"
