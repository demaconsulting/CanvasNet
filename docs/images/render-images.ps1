<#
.SYNOPSIS
    Regenerates the committed README images from their sources, or checks that they are still current.

.DESCRIPTION
    The images in this folder are pictures the canvasnet tool draws from the sources in docs/images/showcase
    (and one fixture in the test folder). This script is the one place that knows how.

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

# docs/images/showcase.png: a montage of six panels, each shown at 400x300 and drawn from 800x600. The panels
# are padded with white to 4:3 so every cell has the same shape. --labels prints the input file name, so the
# panels are copied to extensionless files named after their labels before the montage is built.
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

    $labels = [ordered]@{
        '1-svg.png' = 'SVG'; '2-scad.png' = 'OpenSCAD 3D'; '3-vl.png' = 'Vega-Lite'
        '4-pdf.png' = 'PDF'; '5-pptx.png' = 'PowerPoint'; '6-vsdx.png' = 'Visio'
    }
    foreach ($file in $labels.Keys) {
        Copy-Item (Join-Path $Folder $file) (Join-Path $labeled $labels[$file])
    }

    # The label is the file name without its folder, so the panels are named in a folder of their own and the tool
    # runs from the repository, where a tool manifest is found.
    $panels = $labels.Values | ForEach-Object { Join-Path $labeled $_ }
    Invoke-ToolOrFail montage @panels --columns 3 --cell 400x300 --spacing 12 --padding 12 `
        --background '#e5e7eb' --labels -o "$Folder/showcase.png"
    Join-Path $Folder 'showcase.png'
}

$images = @(
    @{ Path = 'docs/images/showcase.png'; Build = ${function:New-Showcase} }
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
