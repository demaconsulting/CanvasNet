<#
.SYNOPSIS
    A cheap standalone test of the canvasnet.lua Pandoc filter.

.DESCRIPTION
    Builds a small document tree in a temporary folder, runs "dotnet pandoc" with docs/filters/canvasnet.lua
    over it, and checks what the filter promises: pictures are drawn once and found again (cache), a change to
    the source, an included file, a chart's data file, or an attribute draws a new picture, other pictures are
    left alone without starting the tool, wrong attributes and a path that leaves the resource path are clear
    errors, and a model the tool cannot draw stops the build with the tool's message.

    The tool is the one the filter would use: "dotnet canvasnet" from the tool manifest (copied into the
    temporary folder, so the tool must be restored or installed first, as the build-docs CI job does), or the
    command in -ToolCommand / the CANVASNET_TOOL environment variable, for example to try a local build:

        pwsh docs/filters/test-canvasnet.ps1 -ToolCommand "dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll"

    Prints PASS or FAIL for each check and exits 1 if any failed. Run it from anywhere; it changes nothing in
    the repository. Pandoc comes from the repository's tool manifest ("dotnet tool restore").

.PARAMETER ToolCommand
    The command to use instead of "dotnet canvasnet"; sets CANVASNET_TOOL for the runs. Default: the
    CANVASNET_TOOL environment variable when set, else "dotnet canvasnet".

.PARAMETER Keep
    Keeps the temporary folder and prints its path (it is deleted otherwise).
#>
[CmdletBinding()]
param(
    [string]$ToolCommand,
    [switch]$Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("canvasnet-filter-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$tree = Join-Path $root 'tree'
$guide = Join-Path $tree 'docs/guide'
$cache = Join-Path $tree 'docs/generated/canvasnet'
$script:failures = 0

if ($ToolCommand) {
    # The runs happen in a temporary folder, so a relative path to the tool's file is made absolute first.
    $parts = [regex]::Matches($ToolCommand, '"[^"]*"|\S+') | ForEach-Object {
        $token = $_.Value.Trim('"')
        if (Test-Path -LiteralPath $token -PathType Leaf) {
            '"' + ((Resolve-Path -LiteralPath $token).Path -replace '\\', '/') + '"'
        }
        else {
            $_.Value
        }
    }
    $env:CANVASNET_TOOL = $parts -join ' '
}

function Write-Result([bool]$Passed, [string]$Name, [string]$Detail = '') {
    if ($Passed) {
        Write-Host "PASS  $Name"
    }
    else {
        $script:failures++
        Write-Host "FAIL  $Name"
        if ($Detail) {
            ($Detail -split "`n" | Select-Object -First 12 | ForEach-Object { "      $_" }) -join "`n" | Write-Host
        }
    }
}

# Runs Pandoc on a Markdown text in the temporary tree, writing docs/guide/generated/<name>.html.
function Invoke-Filter([string]$Name, [string]$Markdown, [string]$Format = 'html5') {
    Set-Content -LiteralPath (Join-Path $guide "$Name.md") -Value $Markdown -Encoding utf8
    $output = "docs/guide/generated/$Name.html"
    Push-Location $tree
    try {
        $log = & dotnet pandoc --from markdown --to $Format --resource-path docs/guide `
            --lua-filter docs/filters/canvasnet.lua --output $output "docs/guide/$Name.md" 2>&1 | Out-String
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    $htmlPath = Join-Path $tree $output
    [pscustomobject]@{
        ExitCode = $code
        Log      = $log
        HtmlPath = $htmlPath
        Html     = $(if (Test-Path -LiteralPath $htmlPath) { Get-Content -LiteralPath $htmlPath -Raw } else { '' })
    }
}

function Get-Pictures {
    if (Test-Path -LiteralPath $cache) {
        @(Get-ChildItem -LiteralPath $cache -Filter '*.png' | Where-Object { $_.Name -notmatch '\.partial\.png$' })
    }
    else {
        @()
    }
}

function Get-Stamp {
    (Get-Pictures | Sort-Object Name | ForEach-Object { "$($_.Name):$($_.LastWriteTimeUtc.Ticks)" }) -join '|'
}

try {
    New-Item -ItemType Directory -Force -Path $guide, (Join-Path $tree 'docs/filters'), (Join-Path $tree '.config') | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'canvasnet.lua') (Join-Path $tree 'docs/filters/canvasnet.lua')
    Copy-Item (Join-Path $repo '.config/dotnet-tools.json') (Join-Path $tree '.config/dotnet-tools.json')

    $showcase = Join-Path $repo 'docs/images/showcase'
    Copy-Item (Join-Path $showcase 'mesh-shading.pdf'), (Join-Path $showcase 'slide.pptx') $guide
    Copy-Item (Join-Path $repo 'docs/images/showcase.png') (Join-Path $guide 'x.png')
    Copy-Item (Join-Path $repo 'test/DemaConsulting.CanvasNet.Vsdx.Tests/VsdxFixtures/davehoward-test4-connectors.vsdx') $guide
    Copy-Item (Join-Path $showcase 'mesh-shading.pdf') (Join-Path $root 'outside.pdf')

    Set-Content (Join-Path $guide 'inc.scad') -Value 'module part() { cube([10, 10, 10]); }'
    Set-Content (Join-Path $guide 't.scad') -Value "include <inc.scad>`nh = 4;`npart();`ntranslate([12, 0, 0]) cube([4, 4, h]);"
    Set-Content (Join-Path $guide 'echo.scad') -Value 'echo("hello from the model"); cube(5);'
    Set-Content (Join-Path $guide 'bad.scad') -Value 'float x = ;'
    Set-Content (Join-Path $guide 't.csv') -Value "k,v`na,3`nb,5`nc,2"
    Set-Content (Join-Path $guide 't.vl.json') -Value (
        '{"$schema":"https://vega.github.io/schema/vega-lite/v5.json","width":120,"height":80,' +
        '"data":{"url":"t.csv"},"mark":"bar","encoding":{"x":{"field":"k","type":"nominal"},' +
        '"y":{"field":"v","type":"quantitative"}}}')

    # --- Every handled input is drawn; others are left alone ---------------------------------------------
    $all = @'
![model](t.scad){view=iso width=50% define-h=6}

![chart](t.vl.json){scale=2}

![pdf page](mesh-shading.pdf){page=1 dpi=48}

![slide](slide.pptx){page=1 dpi=40 background=transparent}

![visio](davehoward-test4-connectors.vsdx){page=1 dpi=48}

![plain](x.png)
'@
    $first = Invoke-Filter 'all' $all
    Write-Result ($first.ExitCode -eq 0) 'builds a document with every input type' $first.Log
    $sources = [regex]::Matches($first.Html, 'src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $drawn = @($sources | Where-Object { $_ -like '*generated/canvasnet/*.png' })
    Write-Result ($drawn.Count -eq 5) 'five pictures are drawn and referenced' ($sources -join "`n")
    $relative = $drawn.Count -gt 0 -and -not @($drawn | Where-Object { $_ -notmatch '^\.\./\.\./generated/canvasnet/[^/]+\.png$' })
    Write-Result $relative 'picture src is relative to the output folder' ($drawn -join "`n")
    $exist = $drawn.Count -gt 0 -and -not @($drawn | Where-Object {
            -not (Test-Path -LiteralPath (Join-Path (Split-Path $first.HtmlPath) $_)) })
    Write-Result $exist 'every referenced picture exists' ($drawn -join "`n")
    Write-Result ($sources -contains 'x.png') 'a PNG picture is left alone'
    Write-Result ($first.Html -notmatch 'define-h') 'define-NAME is consumed, not kept'

    # --- Cache --------------------------------------------------------------------------------------------
    $stamp = Get-Stamp
    $count = @(Get-Pictures).Count
    $again = Invoke-Filter 'all' $all
    Write-Result ($again.ExitCode -eq 0 -and (Get-Stamp) -eq $stamp) 'a second run draws nothing again' $again.Log
    Write-Result (@(Get-ChildItem -LiteralPath $cache -Filter '*.partial.png').Count -eq 0) 'no partial file is left behind'

    Set-Content (Join-Path $guide 'inc.scad') -Value 'module part() { cube([10, 10, 12]); }'
    Invoke-Filter 'all' $all | Out-Null
    $count2 = @(Get-Pictures).Count
    Write-Result ($count2 -eq $count + 1) 'a changed included file draws the model again' "before $count, after $count2"

    Set-Content (Join-Path $guide 't.csv') -Value "k,v`na,3`nb,5`nc,9"
    Invoke-Filter 'all' $all | Out-Null
    $count3 = @(Get-Pictures).Count
    Write-Result ($count3 -eq $count2 + 1) 'a changed chart data file draws the chart again' "before $count2, after $count3"

    Invoke-Filter 'all' ($all -replace 'view=iso', 'view=top') | Out-Null
    $count4 = @(Get-Pictures).Count
    Write-Result ($count4 -eq $count3 + 1) 'a changed attribute draws the picture again' "before $count3, after $count4"

    Invoke-Filter 'all' ($all -replace 'width=50%', 'width=70%') | Out-Null
    Write-Result (@(Get-Pictures).Count -eq $count4) 'an attribute that stays on the picture does not draw again'

    # --- Pictures the filter ignores never start the tool -----------------------------------------------
    $saved = $env:CANVASNET_TOOL
    $env:CANVASNET_TOOL = 'no-such-program-canvasnet'
    try {
        $plain = Invoke-Filter 'plain' "![plain](x.png)`n`n![web](https://example.com/a.scad)"
        Write-Result ($plain.ExitCode -eq 0) 'PNG and remote pictures never start the tool' $plain.Log
        $missing = Invoke-Filter 'missing-tool' '![model](bad.scad)'
        Write-Result ($missing.ExitCode -ne 0 -and $missing.Log -match 'cannot run the canvasnet tool') `
            'a tool that cannot start is a clear error' $missing.Log
    }
    finally {
        $env:CANVASNET_TOOL = $saved
    }

    # --- Diagnostics and failures -----------------------------------------------------------------------
    $echo = Invoke-Filter 'echo' '![model](echo.scad)'
    Write-Result ($echo.ExitCode -eq 0 -and $echo.Log -match 'canvasnet\.lua: echo\.scad: .*hello from the model') `
        'echo() output becomes a warning' $echo.Log
    $bad = Invoke-Filter 'bad' '![model](bad.scad)'
    Write-Result ($bad.ExitCode -ne 0 -and $bad.Log -match 'SCAD syntax error') `
        'a model that cannot be drawn stops the build with the tool message' $bad.Log

    # --- Attribute and path errors ------------------------------------------------------------------------
    $errors = [ordered]@{
        'size and scale together'    = @('![m](t.scad){size=800x600 scale=2}', 'both size and scale')
        'a bad size'                 = @('![m](t.scad){size=big}', 'WIDTHxHEIGHT')
        'view on a chart'            = @('![m](t.vl.json){view=top}', 'view applies to .scad models only')
        'page on a model'            = @('![m](t.scad){page=2}', 'page applies to')
        'dpi on a chart'             = @('![m](t.vl.json){dpi=96}', 'dpi applies to')
        'a bad page'                 = @('![m](slide.pptx){page=0}', 'counting from 1')
        'a fractional vsdx dpi'      = @('![m](davehoward-test4-connectors.vsdx){dpi=96.5}', 'a whole number')
        'a bad define name'          = @('![m](t.scad){define-1x=3}', 'variable name')
        'an out-of-range elevation'  = @('![m](t.scad){elevation=120}', 'elevation')
        'a missing file'             = @('![m](nothing-here.scad)', 'cannot find')
        'a path above the resource path' = @('![m](../../../outside.pdf)', "outside the document's resource path")
    }
    foreach ($case in $errors.Keys) {
        $run = Invoke-Filter ('err-' + ($case -replace '\W', '-')) $errors[$case][0]
        Write-Result ($run.ExitCode -ne 0 -and $run.Log -match [regex]::Escape($errors[$case][1])) `
            "rejects $case" $run.Log
    }
}
catch {
    $script:failures++
    Write-Host "FAIL  the test itself stopped: $_"
}
finally {
    if ($Keep) {
        Write-Host "Kept $root"
    }
    else {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) FAILED"
    exit 1
}
Write-Host 'All checks passed'
