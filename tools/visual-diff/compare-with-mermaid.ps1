<#
.SYNOPSIS
    Developer tool: draws a Mermaid diagram with CanvasNet and with the reference implementation (the repository's
    pinned mermaid-cli and mermaid in headless Chrome), and reports how much the two pictures differ.

.DESCRIPTION
    Not part of the build, the tests, or CI. It exists to find the places where CanvasNet's diagram differs from the
    diagram that Mermaid itself draws, while a feature is being implemented.

    1. Draws the diagram with "canvasnet render <diagram.mmd> --scale N -o ours.png".
    2. Draws it with render-mermaid.mjs (mermaid-cli's renderMermaid through puppeteer, with liberation.css so that
       the browser measures text with the Liberation Sans faces CanvasNet bundles), giving ref.png and the layout
       read back from the SVG, ref.layout.json (the format of the layout goldens).
    3. Runs "canvasnet compare ours.png ref.png --resize-to a --side-by-side side-by-side.png --json" and prints the
       sizes, the RMSE, SSIM and the share of differing pixels.

    The pictures, the layout and the side-by-side comparison (CanvasNet, Mermaid, the differences) are kept in the
    output folder. There are no thresholds: the anti-aliasing and the text rendering differ from a browser's by
    design, so the numbers are a guide to where to look (the side-by-side picture), not a pass or fail. The layout
    itself is checked by MermaidReferenceLayoutTests against the goldens (update-mermaid-goldens.ps1).

    Needs Node.js 20 or later and "npm install" in the repository root (it installs @mermaid-js/mermaid-cli, mermaid and
    puppeteer with its headless Chrome).

    The tool is "dotnet canvasnet" from the tool manifest, or the command in -ToolCommand / the CANVASNET_TOOL
    environment variable, for example a local build:

        pwsh tools/visual-diff/compare-with-mermaid.ps1 `
            -Diagram test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/flowchart/25-subgraph-basic.mmd -Scale 2 `
            -ToolCommand "dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll"

.PARAMETER Diagram
    The Mermaid diagram (a .mmd file).

.PARAMETER OutDir
    Where the pictures are written (default: a new folder under the temporary folder, which is printed).

.PARAMETER Scale
    The scale of both pictures (default 1; 2 shows more detail).

.PARAMETER ToolCommand
    The command to run instead of "dotnet canvasnet": a program and leading arguments separated by spaces, with
    double quotes around a part that contains spaces. Default: the CANVASNET_TOOL environment variable when set.

.PARAMETER RenderCommand
    The command that draws CanvasNet's picture, called as "<command> render <diagram> --scale N -o <png>"
    (default: the tool), for example another build of the library.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Diagram,
    [string]$OutDir,
    [double]$Scale = 1,
    [string]$ToolCommand,
    [string]$RenderCommand
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $here '../..')).Path
$diagramFile = (Resolve-Path -LiteralPath $Diagram).Path
$name = [System.IO.Path]::GetFileNameWithoutExtension($diagramFile)
if (-not $OutDir) {
    $OutDir = Join-Path ([System.IO.Path]::GetTempPath()) ("canvasnet-mermaid-diff-$name-" + [guid]::NewGuid().ToString('N').Substring(0, 6))
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path

# --- The commands -----------------------------------------------------------------------------------------------
function Split-Command([string]$command) {
    @([regex]::Matches($command, '"[^"]*"|\S+') | ForEach-Object {
            $token = $_.Value.Trim('"')
            if (Test-Path -LiteralPath $token -PathType Leaf) { (Resolve-Path -LiteralPath $token).Path } else { $token }
        })
}

function Invoke-External([string[]]$command) {
    $program = $command[0]
    $leading = @($command | Select-Object -Skip 1)
    $output = & $program @leading @args
    $script:ExitCode = $LASTEXITCODE
    $output
}

if (-not $ToolCommand) { $ToolCommand = $env:CANVASNET_TOOL }
if (-not $ToolCommand) { $ToolCommand = 'dotnet canvasnet' }
if (-not $RenderCommand) { $RenderCommand = $ToolCommand }
$tool = Split-Command $ToolCommand
$renderer = Split-Command $RenderCommand

# --- Node and the pinned mermaid-cli ----------------------------------------------------------------------------
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is needed (node was not found on the path). Install Node.js 20 or later and try again.'
}
if (-not (Test-Path (Join-Path $repoRoot 'node_modules/@mermaid-js/mermaid-cli/package.json'))) {
    throw 'The pinned mermaid-cli is not installed: run "npm install" in the repository root first.'
}

# --- The two pictures -------------------------------------------------------------------------------------------
$ours = Join-Path $OutDir 'ours.png'
$ref = Join-Path $OutDir 'ref.png'
$layout = Join-Path $OutDir 'ref.layout.json'
$compare = Join-Path $OutDir 'side-by-side.png'
$scaleText = $Scale.ToString([System.Globalization.CultureInfo]::InvariantCulture)

Invoke-External $renderer render $diagramFile --scale $scaleText -o $ours | Out-Null
if ($script:ExitCode -ne 0) { throw "CanvasNet could not draw $diagramFile (exit code $($script:ExitCode))." }

node (Join-Path $here 'render-mermaid.mjs') --scale $scaleText --png-dir $OutDir --layout-dir $OutDir $diagramFile | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Mermaid could not draw the diagram (see the message above).' }
Move-Item -Force -LiteralPath (Join-Path $OutDir "$name.png") -Destination $ref
Move-Item -Force -LiteralPath (Join-Path $OutDir "$name.layout.json") -Destination $layout

# --- The comparison ---------------------------------------------------------------------------------------------
$json = Invoke-External $tool compare $ours $ref --resize-to a --side-by-side $compare --json 2>$null
$result = ($json | Out-String) | ConvertFrom-Json
if (-not $result.ok) {
    throw "canvasnet compare failed: $($result.error.message)"
}

$a = $result.a
$b = $result.b
$m = $result.metrics
Write-Host ''
Write-Host "Diagram       : $diagramFile"
Write-Host "CanvasNet     : $ours  ($($a.loadedWidth) x $($a.loadedHeight))"
$resized = if ($a.loadedWidth -ne $b.loadedWidth -or $a.loadedHeight -ne $b.loadedHeight) { '; brought to the size of the first picture' } else { '' }
Write-Host "Mermaid       : $ref  ($($b.loadedWidth) x $($b.loadedHeight)$resized)"
Write-Host "Layout        : $layout"
Write-Host "Side by side  : $compare  (CanvasNet, Mermaid, the differences)"
Write-Host ('RMSE {0:N3}   SSIM {1:N4}   MAE {2:N3}   differing pixels {3:P2}' -f $m.rmse, $m.ssim, $m.mae, $m.diffPixelRatio)
