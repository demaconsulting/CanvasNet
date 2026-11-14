<#
.SYNOPSIS
    Regenerates the committed README images from their sources, or checks that they are still current.

.DESCRIPTION
    The images in this folder are pictures the canvasnet tool draws from architecture.svg, the sources in
    docs/images/showcase, the
    examples of the user guide (docs/user_guide/examples), and one fixture in the test folder. This script is the one
    place that knows how.

    Without -Check it draws every image into a temporary folder and copies the result over the committed file.
    Commit the changed images with the change that made them differ.

    With -Check it draws every image into a temporary folder and compares each with the committed file using
    "canvasnet compare ... --threshold rmse=1 --threshold diff-ratio=0.001 --json". It changes nothing in the
    repository, and exits 1 when a committed image is missing, has another size, or differs by more than the
    tolerance. The tolerance is not zero on purpose: a drawing is not bit-for-bit the same on every operating
    system (a last-bit difference in floating point moves a few anti-aliased pixels, and a font may differ),
    but any change of structure, color, or layout is far above it. rmse is the root-mean-square difference of
    the color levels (0-255); diff-ratio is the share of pixels that differ by more than 8 levels in any color.
    The CI job that builds the documents runs this check on the operating system that made the images.

    The tool is "dotnet canvasnet" from the tool manifest (restored or installed), or the command in
    -ToolCommand / the CANVASNET_TOOL environment variable, for example to use a local build:

        pwsh docs/images/render-images.ps1 -Check -ToolCommand "dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll"

    Run it from anywhere; paths are taken from the repository the script is in.

.PARAMETER Check
    Compare the freshly drawn images with the committed ones instead of replacing them.

.PARAMETER ToolCommand
    The command to run instead of "dotnet canvasnet": a program and leading arguments, separated by spaces, with
    double quotes around a part that contains spaces. Default: the CANVASNET_TOOL environment variable when set.

.PARAMETER Threshold
    The tolerances of -Check, as "metric=value" texts for "canvasnet compare --threshold". Default: rmse=1 and
    diff-ratio=0.001.
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$ToolCommand,
    [string[]]$Threshold = @('rmse=1', 'diff-ratio=0.001')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

# --- The tool -------------------------------------------------------------------------------------------------
if (-not $ToolCommand) {
    $ToolCommand = $env:CANVASNET_TOOL
}
if (-not $ToolCommand) {
    $ToolCommand = 'dotnet canvasnet'
}
# A relative path to a tool file is taken from the caller's folder, before this script moves to the repository.
$tokens = @([regex]::Matches($ToolCommand, '"[^"]*"|\S+') | ForEach-Object {
        $token = $_.Value.Trim('"')
        if (Test-Path -LiteralPath $token -PathType Leaf) { (Resolve-Path -LiteralPath $token).Path } else { $token }
    })
if ($tokens.Count -eq 0) {
    throw 'The tool command is empty.'
}
$program = $tokens[0]
$leading = @($tokens | Select-Object -Skip 1)

Set-Location $repo

# Runs the tool, passing every argument as one argument (no shell parsing), and returns its standard output.
# Standard error is shown. A failing exit code is returned in $script:ToolExit for the caller to judge.
function Invoke-Tool {
    $output = & $program @leading @args
    $script:ToolExit = $LASTEXITCODE
    $output
}

# Runs the tool and stops the script when it fails.
function Invoke-ToolOrFail {
    Invoke-Tool @args | Out-Null
    if ($script:ToolExit -ne 0) {
        throw "canvasnet $($args -join ' ') failed with exit code $($script:ToolExit)."
    }
}

# --- The images -----------------------------------------------------------------------------------------------
# Each image: the committed path (from the repository root) and a script block that draws it into the folder it is
# given and returns the path of the result.

# docs/images/showcase.png: a montage of eight panels in a 4x2 grid, each shown at 320x240 and drawn from 800x600.
# The panels are padded with white to 4:3 so every cell has the same shape. --labels prints the input file name, so
# the panels are copied to extensionless files named after their labels before the montage is built.
function New-Showcase {
    param([string]$Folder)
    $sources = 'docs/images/showcase'
    $visio = 'test/DemaConsulting.CanvasNet.Vsdx.Tests/VsdxFixtures/davehoward-test4-connectors.vsdx'
    $labeled = Join-Path $Folder 'labeled'
    New-Item -ItemType Directory -Force -Path $labeled | Out-Null

    Invoke-ToolOrFail render "$sources/dashboard.svg" --size 800x600 -o "$Folder/1-svg.png"
    Invoke-ToolOrFail render "$sources/hull.scad" --azimuth 25 --elevation 32 --default-color '#4a90d9' `
        --size 800x600 -o "$Folder/2-scad.png"
    Invoke-ToolOrFail render "$sources/sales.vl.json" --strict --scale 2.4 --pad '0,15,0,15' --pad-color white `
        -o "$Folder/3-vl.png"
    Invoke-ToolOrFail render "$sources/mesh-shading.pdf" --dpi 144 --resize 600x600 --pad '100,0,100,0' `
        --pad-color white -o "$Folder/4-pdf.png"
    Invoke-ToolOrFail render "$sources/slide.pptx" --page 1 --dpi 80 --resize 800x600 -o "$Folder/5-pptx.png"
    Invoke-ToolOrFail render $visio --page 1 --dpi 288 --trim --pad 40 --pad '59,0,60,0' --pad-color white `
        --resize 800x600 -o "$Folder/6-vsdx.png"
    Invoke-ToolOrFail render "$sources/flowchart.mmd" --strict --width 800 --pad '0,40,0,41' --pad-color white `
        -o "$Folder/7-mmd.png"
    Invoke-ToolOrFail render "$sources/composition.svg" --strict --size 800x600 -o "$Folder/8-composition.png"

    $labels = [ordered]@{
        '1-svg.png' = 'SVG'; '2-scad.png' = 'OpenSCAD 3D'; '3-vl.png' = 'Vega-Lite'; '7-mmd.png' = 'Mermaid'
        '4-pdf.png' = 'PDF'; '5-pptx.png' = 'PowerPoint'; '6-vsdx.png' = 'Visio'; '8-composition.png' = 'Composition'
    }
    foreach ($file in $labels.Keys) {
        Copy-Item (Join-Path $Folder $file) (Join-Path $labeled $labels[$file])
    }

    # The label is the file name without its folder, so the panels are named in a folder of their own and the tool
    # runs from the repository, where a tool manifest is found.
    $panels = $labels.Values | ForEach-Object { Join-Path $labeled $_ }
    Invoke-ToolOrFail montage @panels --columns 4 --cell 320x240 --spacing 12 --padding 12 `
        --background '#e5e7eb' --labels -o "$Folder/showcase.png"
    Join-Path $Folder 'showcase.png'
}

# docs/images/composition.png: one SVG page (showcase/composition.svg) whose pictures are other files: a Mermaid
# flowchart whose nodes show an OpenSCAD model and a Vega-Lite chart, one OpenSCAD model twice (chosen by the def.
# fragment of each link), and a Mermaid sequence diagram with the theme chosen by its link. It is drawn at 1.5 times
# its size so that its text stays sharp in the README.
function New-Composition {
    param([string]$Folder)
    Invoke-ToolOrFail render 'docs/images/showcase/composition.svg' --strict --width 1440 -o "$Folder/composition.png"
    Join-Path $Folder 'composition.png'
}

# docs/images/montage-explode.png: one assembly (showcase/explode.scad) three times, at explode levels 0, 0.5 and 1,
# chosen by the fragment of each input path. The camera is fixed with -D '$vpd' and -D '$vpt' so that every cell has
# the same scale; without them each cell would be fitted to its own geometry and the plate would shrink.
function New-MontageExplode {
    param([string]$Folder)
    $model = 'docs/images/showcase/explode.scad'
    Invoke-ToolOrFail montage "$model#def.explode=0" "$model#def.explode=0.5" "$model#def.explode=1" `
        -D '$vpd=360' -D '$vpt=[0,0,50]' --columns 3 --cell 280x320 --spacing 12 --padding 12 `
        --background '#e5e7eb' --labels -o "$Folder/montage-explode.png"
    Join-Path $Folder 'montage-explode.png'
}

# docs/images/vegalite-gallery.png: six charts of the Vega-Lite gallery (docs/user_guide/examples/gallery, original
# specifications), each drawn in strict mode at twice its size and shown at 400x300 in the montage. --labels prints
# the input file name, so the charts are copied to extensionless files named after their labels first.
function New-VegaLiteGallery {
    param([string]$Folder)
    $sources = 'docs/user_guide/examples/gallery'
    $labeled = Join-Path $Folder 'labeled'
    New-Item -ItemType Directory -Force -Path $labeled | Out-Null

    $charts = [ordered]@{
        '02-line-series' = 'Lines'; '04-bar-stacked' = 'Stacked bars'; '06-heatmap' = 'Binned heat map'
        '11-donut' = 'Donut'; '13-density' = 'Density'; '18-dark-theme' = 'Theme'
    }
    foreach ($name in $charts.Keys) {
        Invoke-ToolOrFail render "$sources/$name.vl.json" --strict --scale 2 --pad 12 --pad-color '#ffffff' `
            -o "$Folder/$name.png"
        Copy-Item (Join-Path $Folder "$name.png") (Join-Path $labeled $charts[$name])
    }

    $panels = $charts.Values | ForEach-Object { Join-Path $labeled $_ }
    Invoke-ToolOrFail montage @panels --columns 3 --cell 400x300 --spacing 12 --padding 12 `
        --background '#e5e7eb' --labels -o "$Folder/vegalite-gallery.png"
    Join-Path $Folder 'vegalite-gallery.png'
}

# docs/images/mermaid-gallery.png: six Mermaid diagrams of the user guide examples (docs/user_guide/examples, original
# diagrams), each drawn in strict mode at twice its size and shown at 400x300 in the montage, like the Vega-Lite
# gallery. --labels prints the input file name, so the diagrams are copied to extensionless files named after their
# labels first.
function New-MermaidGallery {
    param([string]$Folder)
    $sources = 'docs/user_guide/examples'
    $labeled = Join-Path $Folder 'labeled'
    New-Item -ItemType Directory -Force -Path $labeled | Out-Null

    $diagrams = [ordered]@{
        'mermaid-sequence-arrows' = 'Sequence'; 'mermaid-class-relations' = 'Class'; 'mermaid-state-notes' = 'State'
        'mermaid-gantt' = 'Gantt'; 'mermaid-mindmap' = 'Mindmap'; 'mermaid-gitgraph' = 'Git graph'
    }
    foreach ($name in $diagrams.Keys) {
        Invoke-ToolOrFail render "$sources/$name.mmd" --strict --scale 2 --pad 12 --pad-color '#ffffff' `
            -o "$Folder/$name.png"
        Copy-Item (Join-Path $Folder "$name.png") (Join-Path $labeled $diagrams[$name])
    }

    $panels = $diagrams.Values | ForEach-Object { Join-Path $labeled $_ }
    Invoke-ToolOrFail montage @panels --columns 3 --cell 400x300 --spacing 12 --padding 12 `
        --background '#e5e7eb' --labels -o "$Folder/mermaid-gallery.png"
    Join-Path $Folder 'mermaid-gallery.png'
}

# docs/images/architecture.png: the package architecture diagram (architecture.svg, original), drawn at 1800 pixels
# wide on a transparent background so that its rounded card reads well on light and dark pages.
function New-Architecture {
    param([string]$Folder)
    Invoke-ToolOrFail render 'docs/images/architecture.svg' --strict --background transparent --width 1800 `
        -o "$Folder/architecture.png"
    Join-Path $Folder 'architecture.png'
}

$images = @(
    @{ Path = 'docs/images/architecture.png'; Build = ${function:New-Architecture} }
    @{ Path = 'docs/images/showcase.png'; Build = ${function:New-Showcase} }
    @{ Path = 'docs/images/composition.png'; Build = ${function:New-Composition} }
    @{ Path = 'docs/images/montage-explode.png'; Build = ${function:New-MontageExplode} }
    @{ Path = 'docs/images/vegalite-gallery.png'; Build = ${function:New-VegaLiteGallery} }
    @{ Path = 'docs/images/mermaid-gallery.png'; Build = ${function:New-MermaidGallery} }
)

# --- Run --------------------------------------------------------------------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('canvasnet-images-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $work | Out-Null
$failed = $false

try {
    foreach ($image in $images) {
        $committed = Join-Path $repo $image.Path
        $folder = Join-Path $work ([System.IO.Path]::GetFileNameWithoutExtension($image.Path))
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        $fresh = & $image.Build $folder | Select-Object -Last 1

        if (-not $Check) {
            Copy-Item -LiteralPath $fresh -Destination $committed -Force
            Write-Host "Regenerated $($image.Path)"
            continue
        }

        if (-not (Test-Path -LiteralPath $committed)) {
            Write-Host "FAIL  $($image.Path): the committed image is missing (run docs/images/render-images.ps1)"
            $failed = $true
            continue
        }

        $thresholds = @($Threshold | ForEach-Object { '--threshold'; $_ })
        $sideBySide = Join-Path $folder 'compare.png'
        $json = Invoke-Tool compare $committed $fresh @thresholds --side-by-side $sideBySide --json 2>$null
        $exit = $script:ToolExit
        $result = $null
        try { $result = ($json | Out-String) | ConvertFrom-Json } catch { }

        if ($exit -eq 0) {
            $metrics = $result.metrics
            Write-Host ("ok    {0}: rmse {1:N3}, differing pixels {2:P3} (limits: {3})" -f `
                    $image.Path, $metrics.rmse, $metrics.diffPixelRatio, ($Threshold -join ', '))
        }
        else {
            $failed = $true
            $reason = if ($result -and $result.error) { $result.error.message } else { "the tool exited with code $exit" }
            Write-Host "FAIL  $($image.Path): $reason"
            if ($result -and $result.PSObject.Properties['metrics']) {
                Write-Host ("      rmse {0:N3}, differing pixels {1:P3}, ssim {2:N4}" -f `
                        $result.metrics.rmse, $result.metrics.diffPixelRatio, $result.metrics.ssim)
            }
            Write-Host "      If the change is intended, run: pwsh docs/images/render-images.ps1 and commit the image."
            Write-Host "      The fresh image and a comparison are kept in $folder"
        }
    }
}
finally {
    if (-not $failed) {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failed) {
    exit 1
}
if ($Check) {
    Write-Host 'The committed images are current.'
}
exit 0
