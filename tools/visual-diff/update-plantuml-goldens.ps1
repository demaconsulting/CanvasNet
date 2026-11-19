<#
.SYNOPSIS
    Developer tool: regenerates the PlantUML reference goldens (test/DemaConsulting.CanvasNet.PlantUml.Tests/Fixtures/
    reference/{sequence,activity}/*.svg) from the reference implementation.

.DESCRIPTION
    Not part of the build, the tests, or CI. It renders the original fixtures of each diagram type
    (Fixtures/sequence/*.puml and Fixtures/activity/*.puml) with the MIT-licensed PlantUML jar, with Graphviz turned
    off (GRAPHVIZ_DOT points nowhere, so only the engines that do not need it run), and writes the SVG files that the
    reference layout tests (PlantUmlReferenceLayoutTests) compare CanvasNet's layout with. The goldens are PlantUML's
    output for inputs written for this repository; Fixtures/reference/README.md records the jar, Java, and font
    versions they were made with. A golden change in a pull request is reviewed like code: the diff shows which
    coordinates moved.

    Needs Java 17 or later and the plantuml-mit jar (https://github.com/plantuml/plantuml/releases); the goldens were
    made with plantuml-mit-1.2026.10.jar and Java 25 on Windows, whose SansSerif font is Arial.

.PARAMETER Java
    The java executable (default: java on the PATH).

.PARAMETER Jar
    The path of the plantuml-mit jar.

.PARAMETER Diagram
    The diagram types to regenerate (sequence, activity; default: both).

.PARAMETER Fixture
    The names of the fixtures to regenerate (without .puml; default: every fixture).
#>
[CmdletBinding()]
param(
    [string]$Java = 'java',

    [Parameter(Mandatory = $true)]
    [string]$Jar,

    [ValidateSet('sequence', 'activity')]
    [string[]]$Diagram = @('sequence', 'activity'),

    [string[]]$Fixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$fixtures = Join-Path $PSScriptRoot '../../test/DemaConsulting.CanvasNet.PlantUml.Tests/Fixtures'
if (-not (Test-Path $Jar)) {
    throw "The jar '$Jar' does not exist."
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("plantuml-goldens-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$previousDot = $env:GRAPHVIZ_DOT
try {
    # Only the engines that need no Graphviz may run: point Graphviz nowhere
    $env:GRAPHVIZ_DOT = Join-Path $work 'no-dot.exe'
    foreach ($type in $Diagram) {
        $source = Join-Path $fixtures $type
        $golden = Join-Path $fixtures "reference/$type"
        if (-not (Test-Path $source)) {
            Write-Host "No fixtures for $type"
            continue
        }

        New-Item -ItemType Directory -Force -Path $golden | Out-Null
        $files = Get-ChildItem -Path $source -Filter '*.puml' | Where-Object { -not $Fixture -or $Fixture -contains $_.BaseName }
        foreach ($file in $files) {
            Copy-Item $file.FullName (Join-Path $work $file.Name)
            & $Java '-Djava.awt.headless=true' -jar $Jar -tsvg (Join-Path $work $file.Name)
            if ($LASTEXITCODE -ne 0) {
                throw "PlantUML failed on $($file.Name)."
            }

            Copy-Item (Join-Path $work ($file.BaseName + '.svg')) (Join-Path $golden ($file.BaseName + '.svg')) -Force
            Write-Host "Wrote reference/$type/$($file.BaseName).svg"
        }
    }
}
finally {
    $env:GRAPHVIZ_DOT = $previousDot
    Remove-Item -Recurse -Force $work
}