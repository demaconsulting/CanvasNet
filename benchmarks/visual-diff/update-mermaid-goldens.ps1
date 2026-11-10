<#
.SYNOPSIS
    Developer tool: regenerates the Mermaid layout goldens (test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/
    reference/*.layout.json and reference/{sequence,class,state,er,pie,gantt}/*.layout.json) from the reference implementation.

.DESCRIPTION
    Not part of the build, the tests, or CI. It runs render-mermaid.mjs (the repository's pinned mermaid-cli and
    mermaid in headless Chrome, with liberation.css) on the fixtures of each diagram type and writes the layout files
    that the reference layout tests compare CanvasNet's layout with:

    - flowchart: Fixtures/flowchart/*.mmd to Fixtures/reference/*.layout.json (MermaidReferenceLayoutTests);
    - sequence: Fixtures/sequence/*.mmd to Fixtures/reference/sequence/*.layout.json;
    - class: Fixtures/class/*.mmd to Fixtures/reference/class/*.layout.json;
    - state: Fixtures/state/*.mmd to Fixtures/reference/state/*.layout.json;
    - er: Fixtures/er/*.mmd to Fixtures/reference/er/*.layout.json;
    - pie: Fixtures/pie/*.mmd to Fixtures/reference/pie/*.layout.json (MermaidChartReferenceLayoutTests);
    - gantt: Fixtures/gantt/*.mmd to Fixtures/reference/gantt/*.layout.json (MermaidChartReferenceLayoutTests; drawn in
      the UTC time zone with the clock fixed at 2024-06-12T10:30:00Z).

    The fixtures in $Excluded get no golden; Fixtures/reference/README.md gives the reason of each. A golden change
    in a pull request is reviewed like code: the diff shows which coordinates moved.

    Needs Node.js 20 or later and "npm ci" in the repository root (it installs @mermaid-js/mermaid-cli, mermaid and
    puppeteer with its headless Chrome); it refuses any mermaid but the pinned 11.17.2.

.PARAMETER Diagram
    The diagram types to regenerate (flowchart, sequence, class, state, er, pie, gantt; default: all).

.PARAMETER Fixture
    The names of the fixtures to regenerate (without .mmd; default: every fixture that is not excluded).
#>
[CmdletBinding()]
param(
    [ValidateSet('flowchart', 'sequence', 'class', 'state', 'er', 'pie', 'gantt')]
    [string[]]$Diagram = @('flowchart', 'sequence', 'class', 'state', 'er', 'pie', 'gantt'),

    [string[]]$Fixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The fixture and golden folders of each diagram type (relative to the test fixtures)
$Folders = @{
    flowchart = @{ Source = 'flowchart'; Golden = 'reference' }
    sequence  = @{ Source = 'sequence'; Golden = 'reference/sequence' }
    class     = @{ Source = 'class'; Golden = 'reference/class' }
    state     = @{ Source = 'state'; Golden = 'reference/state' }
    er        = @{ Source = 'er'; Golden = 'reference/er' }
    pie       = @{ Source = 'pie'; Golden = 'reference/pie' }
    gantt     = @{ Source = 'gantt'; Golden = 'reference/gantt' }
}

# Fixtures whose reference depends on the machine (see Fixtures/reference/README.md)
$Excluded = @{
    flowchart = @('38-unicode')
    sequence  = @()
    class     = @()
    state     = @()
    er        = @()
    pie       = @()
    gantt     = @()
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fixtures = Join-Path $repoRoot 'test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is needed (node was not found on the path).'
}
if (-not (Test-Path (Join-Path $repoRoot 'node_modules/@mermaid-js/mermaid-cli/package.json'))) {
    throw 'The pinned mermaid-cli is not installed: run "npm ci" in the repository root first.'
}

# The goldens are those of the pinned mermaid only
$PinnedMermaid = '11.17.2'
$mermaidVersion = (Get-Content -Raw (Join-Path $repoRoot 'node_modules/mermaid/package.json') | ConvertFrom-Json).version
if ($mermaidVersion -ne $PinnedMermaid) {
    throw "The installed mermaid is $mermaidVersion, not the pinned ${PinnedMermaid}: run ""npm ci"" in the repository root."
}

$total = 0
foreach ($type in $Diagram) {
    $sourceDir = Join-Path $fixtures $Folders[$type].Source
    $goldenDir = Join-Path $fixtures $Folders[$type].Golden
    $files = @(Get-ChildItem -LiteralPath $sourceDir -Filter '*.mmd' | Sort-Object Name | Where-Object {
            $name = $_.BaseName
            ($Excluded[$type] -notcontains $name) -and (-not $Fixture -or $Fixture -contains $name)
        } | ForEach-Object { $_.FullName })
    if ($files.Count -eq 0) {
        continue
    }

    node (Join-Path $PSScriptRoot 'render-mermaid.mjs') --layout-dir $goldenDir @files
    if ($LASTEXITCODE -ne 0) {
        throw 'render-mermaid.mjs failed (see the message above).'
    }

    Write-Host "Wrote $($files.Count) $type golden(s) to $goldenDir"
    $total += $files.Count
}

if ($total -eq 0) {
    throw 'No fixture to regenerate.'
}
