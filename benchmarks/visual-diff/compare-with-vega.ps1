<#
.SYNOPSIS
    Developer tool: draws a Vega-Lite specification with CanvasNet and with the reference implementation (Vega and
    Vega-Lite in Node), and reports how much the two pictures differ.

.DESCRIPTION
    Not part of the build, the tests, or CI. It exists to find the places where CanvasNet's chart differs from the
    chart that Vega-Lite itself draws, while a feature is being implemented.

    1. Draws the specification with "canvasnet render <spec> --scale N" (a PNG).
    2. Compiles it with vega-lite and draws it with vega's SVG renderer in Node (render-vega.mjs: headless, no
       browser, no native canvas module), giving an SVG.
    3. Runs "canvasnet compare <ours.png> <vega.svg> --resize-to a --side-by-side ..." - the SVG is drawn by the
       same tool at the size of our picture - and prints the RMSE, SSIM and the share of differing pixels.

    The pictures and the side-by-side comparison are kept in the output folder. There are no thresholds: fonts,
    text widths, and anti-aliasing differ from a browser's by design, so the RMSE is a guide to where to look (the
    side-by-side picture), not a pass or fail. A chart whose two sizes differ much is compared after the Vega
    picture is stretched to ours, so look at the sizes that are printed first.

    Needs Node.js 18 or later and npm. The packages vega and vega-lite are installed into a cache folder
    (-ModulesFolder; default: canvasnet-visual-diff-modules under the temporary folder) the first time, with
    "npm install vega@6 vega-lite@6.5.0 --no-save"; nothing is installed into the repository.

    The tool is "dotnet canvasnet" from the tool manifest, or the command in -ToolCommand / the CANVASNET_TOOL
    environment variable, for example a local build:

        pwsh benchmarks/visual-diff/compare-with-vega.ps1 -Spec docs/user_guide/examples/gallery/01-scatter.vl.json `
            -ToolCommand "dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll"

.PARAMETER Spec
    The Vega-Lite specification (a .vl.json file).

.PARAMETER OutDir
    Where the pictures are written (default: a new folder under the temporary folder, which is printed).

.PARAMETER Scale
    The scale of both pictures (default 1; 2 shows more detail).

.PARAMETER ModulesFolder
    The folder that holds (or will hold) node_modules/vega and node_modules/vega-lite.

.PARAMETER ToolCommand
    The command to run instead of "dotnet canvasnet": a program and leading arguments separated by spaces, with
    double quotes around a part that contains spaces. Default: the CANVASNET_TOOL environment variable when set.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Spec,
    [string]$OutDir,
    [double]$Scale = 1,
    [string]$ModulesFolder = (Join-Path ([System.IO.Path]::GetTempPath()) 'canvasnet-visual-diff-modules'),
    [string]$ToolCommand
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$specFile = (Resolve-Path -LiteralPath $Spec).Path
$name = [System.IO.Path]::GetFileName($specFile) -replace '\.vl\.json$|\.vl$|\.json$', ''
if (-not $OutDir) {
    $OutDir = Join-Path ([System.IO.Path]::GetTempPath()) ("canvasnet-visual-diff-$name-" + [guid]::NewGuid().ToString('N').Substring(0, 6))
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path

# --- The tool ---------------------------------------------------------------------------------------------------
if (-not $ToolCommand) { $ToolCommand = $env:CANVASNET_TOOL }
if (-not $ToolCommand) { $ToolCommand = 'dotnet canvasnet' }
$tokens = @([regex]::Matches($ToolCommand, '"[^"]*"|\S+') | ForEach-Object {
        $token = $_.Value.Trim('"')
        if (Test-Path -LiteralPath $token -PathType Leaf) { (Resolve-Path -LiteralPath $token).Path } else { $token }
    })
$program = $tokens[0]
$leading = @($tokens | Select-Object -Skip 1)

function Invoke-Tool {
    $output = & $program @leading @args
    $script:ToolExit = $LASTEXITCODE
    $output
}

# --- Node, vega, vega-lite --------------------------------------------------------------------------------------
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is needed (node was not found on the path). Install Node.js 18 or later and try again.'
}
if (-not (Test-Path (Join-Path $ModulesFolder 'node_modules/vega-lite/package.json')) -or
    -not (Test-Path (Join-Path $ModulesFolder 'node_modules/vega/package.json'))) {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw 'npm is needed to install vega and vega-lite (npm was not found on the path).'
    }
    Write-Host "Installing vega@6 and vega-lite@6.5.0 into $ModulesFolder (once) ..."
    New-Item -ItemType Directory -Force -Path $ModulesFolder | Out-Null
    npm install vega@6 vega-lite@6.5.0 --no-save --no-audit --no-fund --prefix $ModulesFolder | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
}

# --- The two pictures -------------------------------------------------------------------------------------------
$ours = Join-Path $OutDir "$name-canvasnet.png"
$svg = Join-Path $OutDir "$name-vega.svg"
$compare = Join-Path $OutDir "$name-compare.png"
$scaleText = $Scale.ToString([System.Globalization.CultureInfo]::InvariantCulture)

Invoke-Tool render $specFile --scale $scaleText -o $ours | Out-Null
if ($script:ToolExit -ne 0) { throw "canvasnet could not render $specFile (exit code $($script:ToolExit))." }

node (Join-Path $here 'render-vega.mjs') $specFile $svg $ModulesFolder
if ($LASTEXITCODE -ne 0) { throw 'Vega could not draw the specification (see the message above).' }

# --- The comparison ---------------------------------------------------------------------------------------------
$json = Invoke-Tool compare $ours $svg --resize-to a --side-by-side $compare --json 2>$null
$result = ($json | Out-String) | ConvertFrom-Json
if (-not $result.ok) {
    throw "canvasnet compare failed: $($result.error.message)"
}

$m = $result.metrics
Write-Host ''
Write-Host "Specification : $specFile"
Write-Host "CanvasNet     : $ours"
Write-Host "Vega SVG      : $svg  (drawn by canvasnet at the size of the first picture)"
Write-Host "Side by side  : $compare  (CanvasNet, Vega, the differences)"
Write-Host ('RMSE {0:N3}   SSIM {1:N4}   MAE {2:N3}   differing pixels {3:P2}' -f $m.rmse, $m.ssim, $m.mae, $m.diffPixelRatio)
