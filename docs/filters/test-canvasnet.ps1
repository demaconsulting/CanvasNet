<#
.SYNOPSIS
    A cheap standalone test of the canvasnet.lua Pandoc filter.

.DESCRIPTION
    Builds a small document tree in a temporary folder, runs "dotnet pandoc" with docs/filters/canvasnet.lua
    over it, and checks what the filter promises: pictures are drawn once and found again (cache), a change to
    the source, an included file, a chart's data file, or an attribute draws a new picture, other pictures are
    left alone without starting the tool, wrong attributes and a missing file are clear errors, a path that
    leaves the document's folder renders (the filter has no containment rule), a dependency outside the folder is
    read under the tool's default file-access policy and refused under CANVASNET_FILE_ACCESS=roots (CANVASNET_ROOTS is a
    PATH-like list, both are hashed, a denied picture stops the build, and a bad value is an error), an absolute or
    file: data url and an absolute SVG link are hashed, and a model the tool cannot draw stops the build with the tool's message.
    Mermaid diagrams are drawn from code blocks and from .mmd/.mermaid pictures (the pictures of their nodes are
    hashed), sequence, class, state, ER, pie, gantt, mindmap, packet, and git graph diagrams are drawn by the filter itself, a diagram
    type the tool does not draw is left as written (shown as code) with a warning, and the defaults-file filters run
    before a later command-line filter, which sees only the blocks left as written. An SVG picture that links no file only the tool draws is left alone without
    starting the tool; one that links a diagram (directly or through a nested SVG file) points at a flattened copy whose
    diagram is a PNG picture, cached like the other pictures (the same drawing in another folder gets its own copy),
    and WeasyPrint ("dotnet weasyprint") is run over both to check that the flattened diagram reaches the PDF. The scan
    of an SVG drawing ignores image tags in comments, CDATA sections and processing instructions, reads past a ">" in a
    quoted attribute, takes a prefixed href only in the XLink namespace (under any prefix bound to it), and starts the tool for a link to a missing diagram (directly or in a nested SVG file), whose
    warning is shown on every build until creating the file flattens the drawing.

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

# Runs Invoke-Filter with environment variables set for the run (the child process inherits them), then removes them.
function Invoke-FilterWith([hashtable]$Environment, [string]$Name, [string]$Markdown) {
    try {
        foreach ($key in $Environment.Keys) {
            Set-Item -Path "env:$key" -Value $Environment[$key]
        }
        Invoke-Filter $Name $Markdown
    }
    finally {
        foreach ($key in $Environment.Keys) {
            Remove-Item -Path "env:$key" -ErrorAction SilentlyContinue
        }
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
    New-Item -ItemType Directory -Force -Path $guide, (Join-Path $guide 'generated'), (Join-Path $tree 'docs/filters'), (Join-Path $tree '.config') | Out-Null
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

    # Charts whose data url is written the way the tool's DataFileResolver reads it: a backslash is a slash,
    # and a percent sign is just a character (no decoding).
    New-Item -ItemType Directory -Force -Path (Join-Path $guide 'data') | Out-Null
    Set-Content (Join-Path $guide 'data/b.csv') -Value "k,v`na,1`nb,2"
    Set-Content (Join-Path $guide 'pA.csv') -Value "k,v`na,1`nb,2"
    Set-Content (Join-Path $guide 'p%41.csv') -Value "k,v`na,1`nb,2"
    $urlSpec = '{"$schema":"https://vega.github.io/schema/vega-lite/v5.json","width":120,"height":80,' +
        '"data":{"url":"URL"},"mark":"bar","encoding":{"x":{"field":"k","type":"nominal"},' +
        '"y":{"field":"v","type":"quantitative"}}}'
    # In JSON, two backslashes are one: the url text is data\b.csv.
    Set-Content (Join-Path $guide 'bs.vl.json') -Value $urlSpec.Replace('URL', 'data\\b.csv')
    Set-Content (Join-Path $guide 'pct.vl.json') -Value $urlSpec.Replace('URL', 'p%41.csv')
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

    # --- param-NAME sets a parameter of a chart ------------------------------------------------------------
    Set-Content (Join-Path $guide 'p.vl.json') -Value (
        '{"params":[{"name":"cutoff","value":1}],"width":{"step":20},"height":80,"data":{"url":"t.csv"},' +
        '"transform":[{"filter":"datum.v >= cutoff"}],"mark":"bar","encoding":{"x":{"field":"k","type":"nominal"},' +
        '"y":{"field":"v","type":"quantitative"}}}')
    $paramBefore = @(Get-Pictures).Count
    $withParam = Invoke-Filter 'param' '![c](p.vl.json){param-cutoff=4 width=50%}'
    $paramAfter = @(Get-Pictures).Count
    Write-Result ($withParam.ExitCode -eq 0 -and $paramAfter -eq $paramBefore + 1 -and $withParam.Html -notmatch 'param-cutoff') `
        'param-NAME draws a chart with the parameter and is consumed, not kept' "before $paramBefore, after $paramAfter`n$($withParam.Log)"
    Invoke-Filter 'param' '![c](p.vl.json){param-cutoff=2}' | Out-Null
    Write-Result (@(Get-Pictures).Count -eq $paramAfter + 1) 'another value of param-NAME draws the chart again'
    $jsonParam = Invoke-Filter 'param-json' '![c](p.vl.json){param-cutoff=3 param-other=x}'
    Write-Result ($jsonParam.ExitCode -ne 0 -and $jsonParam.Log -match 'declares no parameter named') `
        'a parameter the chart does not declare stops the build with the tool message' $jsonParam.Log
    $modelParam = Invoke-Filter 'param-model' '![m](t.scad){param-cutoff=3}'
    Write-Result ($modelParam.ExitCode -ne 0 -and $modelParam.Log -match 'param-NAME applies to Vega-Lite charts only') `
        'param-NAME on a model is an error' $modelParam.Log

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

    # A data url is read as the tool reads it, so the filter hashes the file the tool will draw from.
    Invoke-Filter 'bs' '![c](bs.vl.json)' | Out-Null
    $bsBefore = @(Get-Pictures).Count
    Set-Content (Join-Path $guide 'data/b.csv') -Value "k,v`na,1`nb,9"
    Invoke-Filter 'bs' '![c](bs.vl.json)' | Out-Null
    $bsAfter = @(Get-Pictures).Count
    Write-Result ($bsAfter -eq $bsBefore + 1) 'a data file written with backslashes is hashed' "before $bsBefore, after $bsAfter"

    Invoke-Filter 'pct' '![c](pct.vl.json)' | Out-Null
    $pctBefore = @(Get-Pictures).Count
    Set-Content (Join-Path $guide 'pA.csv') -Value "k,v`na,1`nb,9"
    Invoke-Filter 'pct' '![c](pct.vl.json)' | Out-Null
    $pctOther = @(Get-Pictures).Count
    Write-Result ($pctOther -eq $pctBefore) 'a percent sign in a data url is not decoded: the other file is not hashed' `
        "before $pctBefore, after $pctOther"
    Set-Content (Join-Path $guide 'p%41.csv') -Value "k,v`na,1`nb,9"
    Invoke-Filter 'pct' '![c](pct.vl.json)' | Out-Null
    $pctAfter = @(Get-Pictures).Count
    Write-Result ($pctAfter -eq $pctBefore + 1) 'a data file with a percent sign in its name is hashed' "before $pctBefore, after $pctAfter"

    # The tool ignores a leading byte order mark, so the data file of such a chart is hashed too.
    Set-Content (Join-Path $guide 'bom.csv') -Value "k,v`na,1`nb,2"
    [System.IO.File]::WriteAllText((Join-Path $guide 'bom.vl.json'), $urlSpec.Replace('URL', 'bom.csv'),
        [System.Text.UTF8Encoding]::new($true))
    Invoke-Filter 'bom' '![c](bom.vl.json)' | Out-Null
    $bomBefore = @(Get-Pictures).Count
    Set-Content (Join-Path $guide 'bom.csv') -Value "k,v`na,1`nb,9"
    $bom = Invoke-Filter 'bom' '![c](bom.vl.json)'
    $bomAfter = @(Get-Pictures).Count
    Write-Result ($bom.ExitCode -eq 0 -and $bomAfter -eq $bomBefore + 1) `
        'a chart with a byte order mark has its data file hashed' "before $bomBefore, after $bomAfter`n$($bom.Log)"

    # The pictures of an image mark are inputs of the chart: a link of the "url" channel (a value, or a field
    # of the inline rows) and the "url" of the mark are hashed, so a changed picture draws the chart again.
    Copy-Item (Join-Path $repo 'Icon.png') (Join-Path $guide 'pic.png')
    Set-Content (Join-Path $guide 'pm.scad') -Value "include <pi.scad>`npi();"
    Set-Content (Join-Path $guide 'pi.scad') -Value 'module pi() { cube([10, 10, 10]); }'
    $imageSpec = '{"width":120,"height":80,"data":{"values":[{"k":1,"v":1,"u":"URL"}]},"mark":MARK,' +
        '"encoding":{"x":{"field":"k","type":"quantitative"},"y":{"field":"v","type":"quantitative"}ENC}}'
    Set-Content (Join-Path $guide 'img-value.vl.json') -Value $imageSpec.Replace('MARK', '"image"').Replace(
        'ENC', ',"url":{"value":"pic.png"}').Replace('URL', '')
    Set-Content (Join-Path $guide 'img-field.vl.json') -Value $imageSpec.Replace('MARK', '"image"').Replace(
        'ENC', ',"url":{"field":"u","type":"nominal"}').Replace('URL', 'pic.png')
    Set-Content (Join-Path $guide 'img-mark.vl.json') -Value $imageSpec.Replace('MARK', '{"type":"image","url":"pm.scad"}').Replace(
        'ENC', '').Replace('URL', '')
    foreach ($chart in 'img-value', 'img-field') {
        $result = Invoke-Filter $chart "![c]($chart.vl.json)"
        $before = @(Get-Pictures).Count
        Copy-Item (Join-Path $repo 'docs/images/showcase.png') (Join-Path $guide 'pic.png') -Force
        $result = Invoke-Filter $chart "![c]($chart.vl.json)"
        $after = @(Get-Pictures).Count
        Write-Result ($result.ExitCode -eq 0 -and $after -eq $before + 1) "a changed picture of the $chart chart draws it again" `
            "before $before, after $after`n$($result.Log)"
        Copy-Item (Join-Path $repo 'Icon.png') (Join-Path $guide 'pic.png') -Force
    }
    Invoke-Filter 'img-mark' '![c](img-mark.vl.json)' | Out-Null
    $markBefore = @(Get-Pictures).Count
    Set-Content (Join-Path $guide 'pi.scad') -Value 'module pi() { cube([10, 10, 14]); }'
    $mark = Invoke-Filter 'img-mark' '![c](img-mark.vl.json)'
    $markAfter = @(Get-Pictures).Count
    Write-Result ($mark.ExitCode -eq 0 -and $markAfter -eq $markBefore + 1) `
        'a changed file included by the model of an image mark draws the chart again' "before $markBefore, after $markAfter`n$($mark.Log)"

    # A fragment of an image link sets variables of the model it names (#def.NAME=VALUE): the file is what is hashed,
    # and the fragment is part of the specification, so a changed fragment draws the chart again.
    Set-Content (Join-Path $guide 'img-frag.vl.json') -Value $imageSpec.Replace('MARK', '{"type":"image","url":"pm.scad#def.h=10"}').Replace(
        'ENC', '').Replace('URL', '')
    Invoke-Filter 'img-frag' '![c](img-frag.vl.json)' | Out-Null
    $fragBefore = @(Get-Pictures).Count
    Set-Content (Join-Path $guide 'pi.scad') -Value 'module pi() { cube([10, 10, 20]); }'
    $fragFile = Invoke-Filter 'img-frag' '![c](img-frag.vl.json)'
    $fragFileAfter = @(Get-Pictures).Count
    Write-Result ($fragFile.ExitCode -eq 0 -and $fragFileAfter -eq $fragBefore + 1) `
        'a changed model named with a #def. fragment draws the chart again' "before $fragBefore, after $fragFileAfter`n$($fragFile.Log)"
    Set-Content (Join-Path $guide 'img-frag.vl.json') -Value $imageSpec.Replace('MARK', '{"type":"image","url":"pm.scad#def.h=12"}').Replace(
        'ENC', '').Replace('URL', '')
    Invoke-Filter 'img-frag' '![c](img-frag.vl.json)' | Out-Null
    Write-Result (@(Get-Pictures).Count -eq $fragFileAfter + 1) 'a changed #def. fragment draws the chart again'

    # --- Mermaid diagrams: code blocks, .mmd pictures and the types the tool does not draw --------------------
    # A mermaid code block becomes the picture (a figure with a caption attribute); its attributes are read or kept.
    $blocks = @'
```{.mermaid #order caption="Order flow" theme=forest width=80%}
flowchart LR
    A[Order] --> B[Ship]
```

```mermaid
flowchart TD
    X --> Y
```
'@
    $blockBefore = @(Get-Pictures).Count
    $block = Invoke-Filter 'mermaid-block' $blocks
    $blockImages = @([regex]::Matches($block.Html, 'src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } |
            Where-Object { $_ -match '^\.\./\.\./generated/canvasnet/mermaid-[^/]+\.png$' })
    Write-Result ($block.ExitCode -eq 0 -and $blockImages.Count -eq 2 -and @(Get-Pictures).Count -eq $blockBefore + 2) `
        'mermaid code blocks are drawn and replaced by their pictures' "$($block.Log)`n$($blockImages -join "`n")"
    Write-Result ($block.Html -match '<figure[^>]*id="order"' -and $block.Html -match '<figcaption[^>]*>Order flow</figcaption>' -and
        $block.Html -match 'width:80(\.0)?%' -and $block.Html -notmatch '<code|theme=|caption=') `
        'a caption makes a figure; theme is consumed and width stays on the picture' $block.Html
    Write-Result (@(Get-ChildItem -LiteralPath $cache -Filter '*.mmd').Count -ge 2) 'the text of a block is written to the cache folder'
    $blockStamp = Get-Stamp
    $blockAgain = Invoke-Filter 'mermaid-block' $blocks
    Write-Result ($blockAgain.ExitCode -eq 0 -and (Get-Stamp) -eq $blockStamp) 'a second run draws no code block again' $blockAgain.Log
    Invoke-Filter 'mermaid-block' ($blocks -replace 'theme=forest', 'theme=dark') | Out-Null
    Write-Result (@(Get-Pictures).Count -eq $blockBefore + 3) 'a changed block attribute draws the block again'

    # A .mmd picture is drawn, and the local pictures of its nodes are hashed: a changed one draws it again.
    Set-Content (Join-Path $guide 'flow.mmd') -Value "flowchart LR`n    A[Start] --> p@{ img: `"pic.png#x`", w: 40, h: 40, constraint: `"on`" }"
    Copy-Item (Join-Path $guide 'flow.mmd') (Join-Path $guide 'same.mermaid')
    $mmdBefore = @(Get-Pictures).Count
    $mmd = Invoke-Filter 'mermaid-file' "![f](flow.mmd){theme=dark}`n`n![g](same.mermaid)"
    Write-Result ($mmd.ExitCode -eq 0 -and $mmd.Html -match 'generated/canvasnet/flow-' -and
        $mmd.Html -match 'generated/canvasnet/same-' -and @(Get-Pictures).Count -eq $mmdBefore + 2) `
        '.mmd and .mermaid pictures are drawn' $mmd.Log
    Copy-Item (Join-Path $repo 'docs/images/showcase.png') (Join-Path $guide 'pic.png') -Force
    $mmdChanged = Invoke-Filter 'mermaid-file' "![f](flow.mmd){theme=dark}`n`n![g](same.mermaid)"
    Write-Result ($mmdChanged.ExitCode -eq 0 -and @(Get-Pictures).Count -eq $mmdBefore + 4) `
        'a changed picture of a node draws the diagram again' $mmdChanged.Log
    Copy-Item (Join-Path $repo 'Icon.png') (Join-Path $guide 'pic.png') -Force

    # A sequence diagram is drawn by the filter itself, as a code block and as a .mmd picture: it is not left as
    # written, and no marker is left in the cache.
    Set-Content (Join-Path $guide 'talk.mmd') -Value "sequenceDiagram`n    Alice->>Bob: Hello`n    Bob-->>Alice: Hi"
    $sequences = @'
```mermaid
sequenceDiagram
    participant Client
    participant Server
    Client->>+Server: Request
    Server-->>-Client: Response
```

![t](talk.mmd)
'@
    $sequencesBefore = @(Get-Pictures).Count
    $sequenceMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $sequence = Invoke-Filter 'mermaid-sequence' $sequences
    Write-Result ($sequence.ExitCode -eq 0 -and $sequence.Html -notmatch '<code' -and
        $sequence.Html -match 'generated/canvasnet/mermaid-' -and $sequence.Html -match 'generated/canvasnet/talk-' -and
        @(Get-Pictures).Count -eq $sequencesBefore + 2 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $sequenceMarkersBefore -and
        $sequence.Log -notmatch 'left as written') `
        'a sequence block and a sequence .mmd picture are drawn by the filter itself' "$($sequence.Log)`n$($sequence.Html)"

    # A class diagram is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $classes = @'
```mermaid
classDiagram
    class Animal {
        +String name
        +speak() String
    }
    Animal <|-- Dog
```
'@
    $classesBefore = @(Get-Pictures).Count
    $classMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $class = Invoke-Filter 'mermaid-class' $classes
    Write-Result ($class.ExitCode -eq 0 -and $class.Html -notmatch '<code' -and
        $class.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $classesBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $classMarkersBefore -and
        $class.Log -notmatch 'left as written') `
        'a class block is drawn' "$($class.Log)`n$($class.Html)"

    # A state diagram is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $states = @'
```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Busy : job
    Busy --> Idle : done
    Busy --> [*]
```
'@
    $statesBefore = @(Get-Pictures).Count
    $stateMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $state = Invoke-Filter 'mermaid-state' $states
    Write-Result ($state.ExitCode -eq 0 -and $state.Html -notmatch '<code' -and
        $state.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $statesBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $stateMarkersBefore -and
        $state.Log -notmatch 'left as written') `
        'a state block is drawn' "$($state.Log)`n$($state.Html)"

    # An ER diagram is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $entities = @'
```mermaid
erDiagram
    CUSTOMER ||--o{ ORDER : places
    ORDER {
        int id PK
        date placed
    }
```
'@
    $entitiesBefore = @(Get-Pictures).Count
    $erMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $er = Invoke-Filter 'mermaid-er' $entities
    Write-Result ($er.ExitCode -eq 0 -and $er.Html -notmatch '<code' -and
        $er.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $entitiesBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $erMarkersBefore -and
        $er.Log -notmatch 'left as written') `
        'an ER block is drawn' "$($er.Log)`n$($er.Html)"

    # A pie chart is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $slices = @'
```mermaid
pie title Pets
    "Dogs" : 386
    "Cats" : 85
```
'@
    $piesBefore = @(Get-Pictures).Count
    $pieMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $pie = Invoke-Filter 'mermaid-pie' $slices
    Write-Result ($pie.ExitCode -eq 0 -and $pie.Html -notmatch '<code' -and
        $pie.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $piesBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $pieMarkersBefore -and
        $pie.Log -notmatch 'left as written') `
        'a pie block is drawn' "$($pie.Log)`n$($pie.Html)"

    # A gantt chart is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $tasks = @'
```mermaid
gantt
    dateFormat YYYY-MM-DD
    section Build
    Design    :des, 2024-06-03, 5d
    Implement :after des, 8d
```
'@
    $ganttsBefore = @(Get-Pictures).Count
    $ganttMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $gantt = Invoke-Filter 'mermaid-gantt' $tasks
    Write-Result ($gantt.ExitCode -eq 0 -and $gantt.Html -notmatch '<code' -and
        $gantt.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $ganttsBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $ganttMarkersBefore -and
        $gantt.Log -notmatch 'left as written') `
        'a gantt block is drawn' "$($gantt.Log)`n$($gantt.Html)"

    # A mindmap is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $branches = @'
```mermaid
mindmap
  root((Garden))
    Soil
    Plants
      beans[Beans]
```
'@
    $mindmapsBefore = @(Get-Pictures).Count
    $mindmapMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $mindmap = Invoke-Filter 'mermaid-mindmap' $branches
    Write-Result ($mindmap.ExitCode -eq 0 -and $mindmap.Html -notmatch '<code' -and
        $mindmap.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $mindmapsBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $mindmapMarkersBefore -and
        $mindmap.Log -notmatch 'left as written') `
        'a mindmap block is drawn' "$($mindmap.Log)`n$($mindmap.Html)"

    # A packet diagram is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $fields = @'
```mermaid
packet
    0-15: "Source"
    +16: "Target"
    +8: "Flags"
```
'@
    $packetsBefore = @(Get-Pictures).Count
    $packetMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $packet = Invoke-Filter 'mermaid-packet' $fields
    Write-Result ($packet.ExitCode -eq 0 -and $packet.Html -notmatch '<code' -and
        $packet.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $packetsBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $packetMarkersBefore -and
        $packet.Log -notmatch 'left as written') `
        'a packet block is drawn' "$($packet.Log)`n$($packet.Html)"

    # A git graph is drawn by the filter itself too: the block becomes a picture, and no marker is left.
    $history = @'
```mermaid
gitGraph
    commit id: "init"
    branch develop
    commit id: "api"
    checkout main
    merge develop tag: "v1"
```
'@
    $graphsBefore = @(Get-Pictures).Count
    $graphMarkersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $gitGraph = Invoke-Filter 'mermaid-gitgraph' $history
    Write-Result ($gitGraph.ExitCode -eq 0 -and $gitGraph.Html -notmatch '<code' -and
        $gitGraph.Html -match 'generated/canvasnet/mermaid-' -and @(Get-Pictures).Count -eq $graphsBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $graphMarkersBefore -and
        $gitGraph.Log -notmatch 'left as written') `
        'a gitGraph block is drawn' "$($gitGraph.Log)`n$($gitGraph.Html)"

    # A diagram type the tool does not draw is left as written (the document shows its code), with a warning that
    # names it, and a marker in the cache remembers it: the next run warns from the marker and does not draw it.
    Set-Content (Join-Path $guide 'zoo.mmd') -Value "journey`n    title Ducks"
    $journeys = @'
```mermaid
journey
    title Pets
```

![s](zoo.mmd)
'@
    $journeysBefore = @(Get-Pictures).Count
    $unsupported = Invoke-Filter 'mermaid-unsupported' $journeys
    Write-Result ($unsupported.ExitCode -eq 0 -and $unsupported.Html -match '<code class="[^"]*mermaid"' -and
        $unsupported.Html -match 'Pets' -and
        $unsupported.Html -match 'src="zoo\.mmd"' -and @(Get-Pictures).Count -eq $journeysBefore) `
        'an unsupported diagram type is left as written, as a code block and as a picture' "$($unsupported.Log)`n$($unsupported.Html)"
    Write-Result (([regex]::Matches($unsupported.Log, 'it is left as written and not drawn')).Count -eq 2 -and
        $unsupported.Log -match '\[WARNING\]' -and $unsupported.Log -match 'are not drawn by this version' -and
        $unsupported.Log.Contains('canvasnet.lua: mermaid block (journey): ') -and $unsupported.Log.Contains('canvasnet.lua: zoo.mmd: ')) `
        'an unsupported diagram type is a warning that names it, with the tool message' $unsupported.Log
    $markers = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported')
    Write-Result ($markers.Count -eq 2) 'an unsupported diagram leaves a marker in the cache' "$($markers.Count) markers"
    foreach ($marker in $markers) {
        Set-Content -LiteralPath $marker.FullName -Value 'remembered-unsupported' -NoNewline
    }
    $remembered = Invoke-Filter 'mermaid-unsupported' $journeys
    Write-Result ($remembered.ExitCode -eq 0 -and ([regex]::Matches($remembered.Log, 'remembered-unsupported')).Count -eq 2) `
        'a later run takes an unsupported diagram from its marker without drawing it' $remembered.Log

    # A flowchart that embeds a diagram of a type the tool does not draw is still drawn: the report about the
    # embedded picture (which names it in its origin) is an ordinary warning, not the whole picture unsupported.
    Set-Content (Join-Path $guide 'embeds-journey.mmd') -Value "flowchart LR`n    A --> B@{ img: `"zoo.mmd`", h: 60, constraint: `"on`" }"
    $embedBefore = @(Get-Pictures).Count
    $markersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count
    $embedded = Invoke-Filter 'mermaid-embeds-unsupported' '![e](embeds-journey.mmd)'
    Write-Result ($embedded.ExitCode -eq 0 -and $embedded.Html -match 'generated/canvasnet/embeds-journey-' -and
        @(Get-Pictures).Count -eq $embedBefore + 1 -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.unsupported').Count -eq $markersBefore -and
        $embedded.Log -match 'are not drawn by this version' -and $embedded.Log -notmatch 'left as written') `
        'a flowchart embedding an unsupported diagram is drawn, with the embedded report as a warning' "$($embedded.Log)`n$($embedded.Html)"

    # Pandoc runs the filters in command-line order, a defaults file's filters where its --defaults option is: with
    # "--defaults ... --lua-filter later.lua", canvasnet.lua sees the mermaid blocks first, and the later filter sees
    # only the blocks left as written. A small Lua filter that marks every code block it sees is the later filter here.
    Set-Content (Join-Path $root 'defaults.yaml') -Value "filters:`n  - docs/filters/canvasnet.lua"
    Set-Content (Join-Path $root 'observe.lua') -Value (
        'function CodeBlock(b) return pandoc.Para{ pandoc.Str("OBSERVED-" .. (b.text:match("^(%S+)") or "")) } end')
    Set-Content (Join-Path $guide 'order.md') -Value $blocks.Replace('X --> Y', 'X --> Y --> Z') -NoNewline
    Add-Content (Join-Path $guide 'order.md') -Value ("`n`n" + $journeys.Replace('![s](zoo.mmd)', ''))
    Push-Location $tree
    try {
        $orderLog = & dotnet pandoc --defaults (Join-Path $root 'defaults.yaml') --lua-filter (Join-Path $root 'observe.lua') `
            --from markdown --to html5 --resource-path docs/guide --output docs/guide/generated/order.html docs/guide/order.md 2>&1 |
            Out-String
        $orderCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    $orderHtml = Get-Content -LiteralPath (Join-Path $guide 'generated/order.html') -Raw -ErrorAction SilentlyContinue
    Write-Result ($orderCode -eq 0 -and $orderHtml -match 'OBSERVED-journey' -and $orderHtml -notmatch 'OBSERVED-flowchart' -and
        ([regex]::Matches($orderHtml, 'generated/canvasnet/mermaid-')).Count -eq 2) `
        'defaults-file filters run before a command-line filter, which sees only the unsupported blocks' "$orderLog`n$orderHtml"

    # --- SVG pictures: plain ones are left alone, composite ones are flattened ------------------------------
    $svgDir = Join-Path $guide 'svg'
    New-Item -ItemType Directory -Force -Path $svgDir | Out-Null
    Copy-Item (Join-Path $guide 'x.png') (Join-Path $svgDir 'logo.png')
    Set-Content (Join-Path $svgDir 'flow.mmd') -Value "flowchart LR`n    A[Start] --> B[Done]`n    style A fill:#000000,color:#ffffff`n    style B fill:#000000,color:#ffffff"
    Set-Content (Join-Path $svgDir 'plain.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><rect width="200" height="100" fill="#cccccc"/>' +
        '<image href="logo.png" width="50" height="50"/></svg>')
    $composite = '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="400" height="200" viewBox="0 0 400 200">' +
        "`n  <!-- a frame drawn as vectors -->`n" +
        '  <rect x="2" y="2" width="396" height="196" fill="none" stroke="#000000" stroke-width="4"/>' + "`n" +
        '  <text x="10" y="190" font-size="14">Composite</text>' + "`n" +
        '  <image href="flow.mmd" x="10" y="10" width="280" height="160"/>' + "`n" +
        '  <image xlink:href="logo.png" x="300" y="10" width="80" height="80"/>' + "`n</svg>"
    Set-Content (Join-Path $svgDir 'comp.svg') -Value $composite
    Set-Content (Join-Path $svgDir 'outer.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="400" height="200"><image href="comp.svg" width="400" height="200"/></svg>')
    Set-Content (Join-Path $svgDir 'hidden.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><defs><image id="h" href="flow.mmd" width="50" height="50"/></defs></svg>')
    function Get-Copies {
        if (Test-Path -LiteralPath $cache) {
            @(Get-ChildItem -LiteralPath $cache -Filter '*.svg' | Where-Object { $_.Name -notmatch '\.partial\.svg$' })
        }
        else {
            @()
        }
    }

    $plainSvg = Invoke-Filter 'svg-plain' '![p](svg/plain.svg){width=50%}'
    Write-Result ($plainSvg.ExitCode -eq 0 -and $plainSvg.Html -match 'src="svg/plain\.svg"' -and @(Get-Copies).Count -eq 0) `
        'a plain SVG picture is left alone' "$($plainSvg.Log)`n$($plainSvg.Html)"

    $compDoc = '![c](svg/comp.svg){width=80%}'
    $comp = Invoke-Filter 'svg-comp' $compDoc
    $compSrc = [regex]::Match($comp.Html, 'src="(\.\./\.\./generated/canvasnet/comp-[0-9a-f]+\.svg)"').Groups[1].Value
    Write-Result ($comp.ExitCode -eq 0 -and $compSrc) 'a composite SVG picture points at a flattened copy' "$($comp.Log)`n$($comp.Html)"
    $copyPath = if ($compSrc) { Join-Path (Split-Path $comp.HtmlPath) $compSrc } else { '' }
    $copy = if ($copyPath -and (Test-Path -LiteralPath $copyPath)) { Get-Content -LiteralPath $copyPath -Raw } else { '' }
    Write-Result ($copy -match 'href="data:image/png;base64,' -and $copy -notmatch 'href="flow\.mmd"' -and
        $copy -match '<!-- a frame drawn as vectors -->' -and $copy -match '<text x="10" y="190"') `
        'the copy has the diagram as a PNG and keeps everything else' $copy
    $logoLink = [regex]::Match($copy, 'xlink:href="([^"]+logo\.png)"').Groups[1].Value
    Write-Result ($logoLink -and (Test-Path -LiteralPath (Join-Path (Split-Path $copyPath) $logoLink))) `
        'the raster link of the copy is rebased so it finds its file from the cache folder' $logoLink

    $svgStamp = (Get-Copies | Sort-Object Name | ForEach-Object { "$($_.Name):$($_.LastWriteTimeUtc.Ticks)" }) -join '|'
    $compAgain = Invoke-Filter 'svg-comp' $compDoc
    $svgStampAgain = (Get-Copies | Sort-Object Name | ForEach-Object { "$($_.Name):$($_.LastWriteTimeUtc.Ticks)" }) -join '|'
    Write-Result ($compAgain.ExitCode -eq 0 -and $svgStampAgain -eq $svgStamp) 'a second run flattens nothing again' $compAgain.Log
    $copies = @(Get-Copies).Count
    $scaled = Invoke-Filter 'svg-scale' '![c](svg/comp.svg){scale=2}'
    Write-Result ($scaled.ExitCode -eq 0 -and @(Get-Copies).Count -eq $copies + 1 -and $scaled.Html -notmatch 'scale=') `
        'scale draws a new copy and is consumed, not kept' "$($scaled.Log)`n$($scaled.Html)"
    Add-Content (Join-Path $svgDir 'flow.mmd') -Value '    B --> C[Again]'
    $changed = Invoke-Filter 'svg-comp' $compDoc
    Write-Result ($changed.ExitCode -eq 0 -and @(Get-Copies).Count -eq $copies + 2) `
        'a changed linked diagram flattens the drawing again' $changed.Log

    # The same drawing in another folder gets its own copy: the links the tool rebases lead to that folder
    $movedDir = Join-Path $guide 'svg-moved'
    New-Item -ItemType Directory -Force -Path $movedDir | Out-Null
    foreach ($file in 'comp.svg', 'flow.mmd', 'logo.png') {
        Copy-Item (Join-Path $svgDir $file) (Join-Path $movedDir $file)
    }
    $moved = Invoke-Filter 'svg-moved' '![c](svg-moved/comp.svg){width=80%}'
    $movedSrc = [regex]::Match($moved.Html, 'src="(\.\./\.\./generated/canvasnet/comp-[0-9a-f]+\.svg)"').Groups[1].Value
    $movedPath = if ($movedSrc) { Join-Path (Split-Path $moved.HtmlPath) $movedSrc } else { '' }
    $movedCopy = if ($movedPath -and (Test-Path -LiteralPath $movedPath)) { Get-Content -LiteralPath $movedPath -Raw } else { '' }
    $movedLogo = [regex]::Match($movedCopy, 'xlink:href="([^"]+logo\.png)"').Groups[1].Value
    Write-Result ($moved.ExitCode -eq 0 -and @(Get-Copies).Count -eq $copies + 3 -and $movedLogo -match 'svg-moved/logo\.png$' -and
        (Test-Path -LiteralPath (Join-Path (Split-Path $movedPath) $movedLogo))) `
        'the same drawing in another folder gets its own copy, whose links find the files of that folder' "$($moved.Log)`n$movedLogo"
    $nested = Invoke-Filter 'svg-nested' '![o](svg/outer.svg)'
    Write-Result ($nested.ExitCode -eq 0 -and $nested.Html -match 'generated/canvasnet/outer-[0-9a-f]+\.svg') `
        'an SVG picture that links a composite SVG file is flattened' "$($nested.Log)`n$($nested.Html)"
    $hidden = Invoke-Filter 'svg-hidden' '![h](svg/hidden.svg)'
    Write-Result ($hidden.ExitCode -eq 0 -and $hidden.Html -match 'src="svg/hidden\.svg"' -and
        $hidden.Log -match 'is not shown by the drawing' -and @(Get-ChildItem -LiteralPath $cache -Filter '*.plain').Count -eq 1) `
        'a drawing whose diagram is never shown is left alone with a warning and a marker' "$($hidden.Log)`n$($hidden.Html)"

    # The pre-scan reads the markup as the tool does: a ">" in a quoted attribute does not end the tag
    Set-Content (Join-Path $svgDir 'quoted.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">' +
        '<image aria-label="a > b" data-note=''x > y'' href="flow.mmd" width="100" height="50"/></svg>')
    $quoted = Invoke-Filter 'svg-quoted' '![q](svg/quoted.svg)'
    Write-Result ($quoted.ExitCode -eq 0 -and $quoted.Html -match 'generated/canvasnet/quoted-[0-9a-f]+\.svg') `
        'a link after a quoted ">" in an image tag is found and flattened' "$($quoted.Log)`n$($quoted.Html)"

    # A link to a diagram that does not exist (directly, or in a nested SVG file) starts the tool, which reports it; the
    # warning is shown again from the marker, and creating the file flattens the drawing
    Set-Content (Join-Path $svgDir 'gone.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><image href="gone.mmd" width="100" height="50"/></svg>')
    Set-Content (Join-Path $svgDir 'gone-outer.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><image href="gone-inner.svg" width="200" height="100"/></svg>')
    Set-Content (Join-Path $svgDir 'gone-inner.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><image href="gone-too.mmd" width="100" height="50"/></svg>')
    $gone = Invoke-Filter 'svg-gone' '![g](svg/gone.svg)'
    Write-Result ($gone.ExitCode -eq 0 -and $gone.Html -match 'src="svg/gone\.svg"' -and $gone.Log -match 'canvasnet\.lua: svg/gone\.svg: .*gone\.mmd') `
        'a link to a missing diagram starts the tool, which reports it as a warning' "$($gone.Log)`n$($gone.Html)"
    $goneAgain = Invoke-Filter 'svg-gone' '![g](svg/gone.svg)'
    Write-Result ($goneAgain.ExitCode -eq 0 -and $goneAgain.Log -match 'canvasnet\.lua: svg/gone\.svg: .*gone\.mmd') `
        'the warning about a missing diagram is shown again from the marker' $goneAgain.Log
    $goneNested = Invoke-Filter 'svg-gone-nested' '![g](svg/gone-outer.svg)'
    Write-Result ($goneNested.ExitCode -eq 0 -and $goneNested.Log -match 'gone-too\.mmd') `
        'a missing diagram linked by a nested SVG file is reported' "$($goneNested.Log)`n$($goneNested.Html)"
    Copy-Item (Join-Path $svgDir 'flow.mmd') (Join-Path $svgDir 'gone.mmd')
    $goneMade = Invoke-Filter 'svg-gone' '![g](svg/gone.svg)'
    Write-Result ($goneMade.ExitCode -eq 0 -and $goneMade.Html -match 'generated/canvasnet/gone-[0-9a-f]+\.svg') `
        'creating the missing diagram flattens the drawing (the cache noted it as missing)' "$($goneMade.Log)`n$($goneMade.Html)"

    # Image tags in a comment or a CDATA section are not markup: this drawing is plain (checked below, without the tool)
    Set-Content (Join-Path $svgDir 'commented.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">' +
        '<!-- <image href="flow.mmd" width="50" height="50"/> -->' +
        '<style><![CDATA[ /* <image href="flow.mmd"/> */ rect { fill: #cccccc; } ]]></style>' +
        '<?note <image href="flow.mmd"/> ?><rect width="200" height="100"/><image href="logo.png" width="50" height="50"/></svg>')
    # A prefixed href outside the XLink namespace is not a link (checked below, without the tool), but any prefix bound to
    # the XLink namespace is
    Set-Content (Join-Path $svgDir 'foreign-href.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" xmlns:foo="urn:example:foo" width="200" height="100">' +
        '<image foo:href="part.scad" width="50" height="50"/>' +
        '<g xmlns:xlink="urn:example:not-xlink"><image xlink:href="flow.mmd" width="50" height="50"/></g></svg>')
    Set-Content (Join-Path $svgDir 'other-prefix.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" xmlns:l="http://www.w3.org/1999/xlink" width="200" height="100">' +
        '<image l:href="flow.mmd" width="100" height="50"/></svg>')
    # A link is read as the tool reads it: cut at "?" or "#", then percent-decoded, so flow%2Emmd is the diagram
    Set-Content (Join-Path $svgDir 'encoded.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><image href="flow%2Emmd" width="100" height="50"/></svg>')
    $encoded = Invoke-Filter 'svg-encoded' '![e](svg/encoded.svg)'
    $encodedSrc = [regex]::Match($encoded.Html, 'src="(\.\./\.\./generated/canvasnet/encoded-[0-9a-f]+\.svg)"').Groups[1].Value
    $encodedCopy = if ($encodedSrc) { Get-Content -LiteralPath (Join-Path (Split-Path $encoded.HtmlPath) $encodedSrc) -Raw } else { '' }
    Write-Result ($encoded.ExitCode -eq 0 -and $encodedCopy -match 'href="data:image/png;base64,' -and $encodedCopy -notmatch 'flow%2Emmd') `
        'a percent-encoded link to a diagram (flow%2Emmd) is flattened' "$($encoded.Log)`n$($encoded.Html)"
    $otherPrefix = Invoke-Filter 'svg-other-prefix' '![o](svg/other-prefix.svg)'
    Write-Result ($otherPrefix.ExitCode -eq 0 -and $otherPrefix.Html -match 'generated/canvasnet/other-prefix-[0-9a-f]+\.svg') `
        'an href under another prefix bound to the XLink namespace is found and flattened' "$($otherPrefix.Log)`n$($otherPrefix.Html)"

    # WeasyPrint, which draws the HTML into the PDF, shows the flattened copy: its page is darker than the page of a
    # control document that shows the original drawing (whose diagram WeasyPrint cannot draw), and the control page
    # still shows the frame and the text that both have.
    $controlHtml = Join-Path $guide 'generated/svg-control.html'
    Set-Content -LiteralPath $controlHtml -Value '<p><img src="../svg/comp.svg" style="width: 80%" alt="c"></p>'
    $toolParts = if ($env:CANVASNET_TOOL) {
        @([regex]::Matches($env:CANVASNET_TOOL, '"[^"]*"|\S+') | ForEach-Object { $_.Value.Trim('"') })
    }
    else {
        @('dotnet', 'canvasnet')
    }
    $means = @{}
    Push-Location $tree
    try {
        foreach ($html in @{ flat = $comp.HtmlPath; control = $controlHtml }.GetEnumerator()) {
            $pdf = Join-Path $root "$($html.Key).pdf"
            $png = Join-Path $root "$($html.Key).png"
            & dotnet weasyprint $html.Value $pdf 2>&1 | Out-Null
            & $toolParts[0] @($toolParts | Select-Object -Skip 1) render $pdf --dpi 48 -o $png 2>&1 | Out-Null
            $colors = & $toolParts[0] @($toolParts | Select-Object -Skip 1) inspect colors $png --json 2>$null | Out-String
            $means[$html.Key] = if ($colors) { ($colors | ConvertFrom-Json).luma.mean } else { $null }
        }
    }
    finally {
        Pop-Location
    }
    Write-Result ($null -ne $means.flat -and $null -ne $means.control -and $means.flat -lt $means.control - 1 -and $means.control -lt 255) `
        'WeasyPrint draws the flattened diagram in the PDF, and the frame of the original' "flat $($means.flat), control $($means.control)"

    # --- Pictures the filter ignores never start the tool -----------------------------------------------
    $saved = $env:CANVASNET_TOOL
    $env:CANVASNET_TOOL = 'no-such-program-canvasnet'
    try {
        $plain = Invoke-Filter 'plain' "![plain](x.png)`n`n![web](https://example.com/a.scad)`n`n![drawing](svg/plain.svg)"
        Write-Result ($plain.ExitCode -eq 0) 'PNG, plain SVG, and remote pictures never start the tool' $plain.Log
        $commented = Invoke-Filter 'svg-commented' '![c](svg/commented.svg)'
        Write-Result ($commented.ExitCode -eq 0 -and $commented.Html -match 'src="svg/commented\.svg"') `
            'an SVG whose diagram links are only in a comment, CDATA, or a processing instruction never starts the tool' `
            "$($commented.Log)`n$($commented.Html)"
        $foreign = Invoke-Filter 'svg-foreign-href' '![f](svg/foreign-href.svg)'
        Write-Result ($foreign.ExitCode -eq 0 -and $foreign.Html -match 'src="svg/foreign-href\.svg"') `
            'an SVG whose only prefixed hrefs are outside the XLink namespace never starts the tool' `
            "$($foreign.Log)`n$($foreign.Html)"
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
        'a missing file'             = @('![m](nothing-here.scad)', "cannot find 'nothing-here.scad' in the document's resource path")
        'theme on a model'           = @('![m](t.scad){theme=dark}', 'theme applies to Mermaid diagrams only')
        'a bad theme'                = @('![m](flow.mmd){theme=sepia}', 'write default, base, dark, forest or neutral')
        'a bad code block attribute' = @("``````{.mermaid page=2}`nflowchart LR`n    A --> B`n``````", "'mermaid block (flowchart LR)' has page=2")
    }
    foreach ($case in $errors.Keys) {
        $run = Invoke-Filter ('err-' + ($case -replace '\W', '-')) $errors[$case][0]
        Write-Result ($run.ExitCode -ne 0 -and $run.Log -match [regex]::Escape($errors[$case][1])) `
            "rejects $case" $run.Log
    }

    # --- Paths are resolved like Pandoc does; the filter has no containment rule --------------------------
    # An image src may leave the document's folder with "../" (the tree is docs/guide, so three levels up is
    # the temporary root), and an absolute src is used as it is.
    $upward = Invoke-Filter 'up-image' '![m](../../../outside.pdf){dpi=48}'
    Write-Result ($upward.ExitCode -eq 0 -and $upward.Html -match 'generated/canvasnet/outside-') `
        'a ../ image src outside the document folder renders' $upward.Log
    $abs = ((Join-Path $root 'outside.pdf') -replace '\\', '/')
    $absolute = Invoke-Filter 'abs-image' "![m](<$abs>){dpi=48}"
    Write-Result ($absolute.ExitCode -eq 0 -and $absolute.Html -match 'generated/canvasnet/outside-') `
        'an absolute image src renders' $absolute.Log

    # An include or data url that leaves its folder is followed and hashed; the tool's file-access policy
    # decides whether it may read it, and its default (like Pandoc) reads it, so the picture is drawn.
    $sub = Join-Path $guide 'esc'
    New-Item -ItemType Directory -Force -Path $sub | Out-Null
    Set-Content (Join-Path $guide 'outer.scad') -Value 'module outer() { cube([3, 3, 3]); }'
    Set-Content (Join-Path $guide 'outer.csv') -Value "k,v`na,1`nb,2"
    Set-Content (Join-Path $sub 'm.scad') -Value "include <../outer.scad>`ncube(5);"
    Set-Content (Join-Path $sub 'c.vl.json') -Value $urlSpec.Replace('URL', '../outer.csv')
    foreach ($dep in 'm.scad', 'c.vl.json') {
        $run = Invoke-Filter ('esc-dep-' + ($dep -replace '\W', '-')) "![x](esc/$dep)"
        Write-Result ($run.ExitCode -eq 0 -and $run.Html -match 'generated/canvasnet/') `
            "a dependency outside the folder is read by default ($dep)" $run.Log
    }

    # --- Opt-in file access: CANVASNET_FILE_ACCESS and CANVASNET_ROOTS ------------------------------------
    # Under a restrictive mode the same dependencies are denied by the tool, and the build stops.
    foreach ($dep in 'm.scad', 'c.vl.json') {
        $run = Invoke-FilterWith @{ CANVASNET_FILE_ACCESS = 'roots'; CANVASNET_ROOTS = $sub } ('esc-roots-' + ($dep -replace '\W', '-')) "![x](esc/$dep)"
        Write-Result ($run.ExitCode -ne 0 -and $run.Log -match 'denied') `
            "CANVASNET_FILE_ACCESS=roots refuses a dependency outside the roots ($dep)" $run.Log
    }

    # CANVASNET_ROOTS is a list separated like PATH; the include is in the second folder
    $rootList = $sub + [System.IO.Path]::PathSeparator + $guide
    $listed = Invoke-FilterWith @{ CANVASNET_ROOTS = $rootList } 'esc-list' '![x](esc/m.scad)'
    Write-Result ($listed.ExitCode -eq 0 -and $listed.Html -match 'generated/canvasnet/') `
        'CANVASNET_ROOTS takes folders separated like PATH' $listed.Log

    # The variables are part of every picture's name: the same picture under roots is drawn again
    Invoke-Filter 'access-hash' '![m](t.scad)' | Out-Null
    $hashBefore = @(Get-Pictures).Count
    $hashed = Invoke-FilterWith @{ CANVASNET_FILE_ACCESS = 'roots' } 'access-hash' '![m](t.scad)'
    $hashAfter = @(Get-Pictures).Count
    Write-Result ($hashed.ExitCode -eq 0 -and $hashAfter -eq $hashBefore + 1) `
        'the file-access variables are part of the hash' "before $hashBefore, after $hashAfter`n$($hashed.Log)"

    # A picture the mode denies is only a warning of the tool, but it stops a restrictive build
    $deny = Join-Path $guide 'deny'
    New-Item -ItemType Directory -Force -Path $deny | Out-Null
    Copy-Item (Join-Path $guide 'x.png') (Join-Path $guide 'outside-pic.png')
    Set-Content (Join-Path $guide 'outside-flow.mmd') -Value "flowchart LR`n    A[Out] --> B[Side]"
    Set-Content (Join-Path $deny 'd.mmd') -Value "flowchart LR`n    A[Start] --> p@{ img: `"../outside-pic.png`", w: 40, h: 40, constraint: `"on`" }"
    Set-Content (Join-Path $deny 'f.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><image href="../outside-flow.mmd" width="50" height="50"/></svg>')
    $deniedRender = Invoke-FilterWith @{ CANVASNET_ROOTS = $deny } 'deny-render' '![d](deny/d.mmd)'
    Write-Result ($deniedRender.ExitCode -ne 0 -and $deniedRender.Log -match 'denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS') `
        'a picture the mode denies stops the build (render)' $deniedRender.Log
    $markersBefore = @(Get-ChildItem -LiteralPath $cache -Filter '*.plain').Count
    $deniedFlatten = Invoke-FilterWith @{ CANVASNET_ROOTS = $deny } 'deny-flatten' '![f](deny/f.svg)'
    Write-Result ($deniedFlatten.ExitCode -ne 0 -and $deniedFlatten.Log -match 'denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS' -and
        @(Get-ChildItem -LiteralPath $cache -Filter '*.plain').Count -eq $markersBefore -and
        @(Get-ChildItem -LiteralPath $cache -Filter 'f-*.svg').Count -eq 0) `
        'a picture the mode denies stops the build and leaves no marker (flatten)' $deniedFlatten.Log

    # A mistake in the variables stops the build before the tool runs
    $unknownMode = Invoke-FilterWith @{ CANVASNET_FILE_ACCESS = 'everything' } 'access-unknown' '![m](t.scad)'
    Write-Result ($unknownMode.ExitCode -ne 0 -and $unknownMode.Log -match "CANVASNET_FILE_ACCESS is 'everything'") `
        'an unknown CANVASNET_FILE_ACCESS stops the build' $unknownMode.Log
    $conflict = Invoke-FilterWith @{ CANVASNET_FILE_ACCESS = 'any'; CANVASNET_ROOTS = $guide } 'access-conflict' '![m](t.scad)'
    Write-Result ($conflict.ExitCode -ne 0 -and $conflict.Log -match 'CANVASNET_ROOTS cannot be used with CANVASNET_FILE_ACCESS=any') `
        'CANVASNET_ROOTS with CANVASNET_FILE_ACCESS=any stops the build' $conflict.Log

    # An absolute data url and a file: URI without a host are local files the tool reads, so they are hashed
    $absCsv = (Join-Path $root 'abs.csv') -replace '\\', '/'
    $fileUri = 'file://' + $(if ($absCsv.StartsWith('/')) { $absCsv } else { '/' + $absCsv })
    Set-Content (Join-Path $guide 'abs.vl.json') -Value $urlSpec.Replace('URL', $absCsv)
    Set-Content (Join-Path $guide 'uri.vl.json') -Value $urlSpec.Replace('URL', $fileUri)
    foreach ($case in @(@('abs', 'an absolute data url is hashed'), @('uri', 'a file: data url is hashed'))) {
        Set-Content (Join-Path $root 'abs.csv') -Value "k,v`na,1`nb,2"
        $absFirst = Invoke-Filter "data-$($case[0])" "![c]($($case[0]).vl.json)"
        $absBefore = @(Get-Pictures).Count
        Set-Content (Join-Path $root 'abs.csv') -Value "k,v`na,1`nb,$(7 + $case[0].Length)"
        $absRun = Invoke-Filter "data-$($case[0])" "![c]($($case[0]).vl.json)"
        $absAfter = @(Get-Pictures).Count
        Write-Result ($absFirst.ExitCode -eq 0 -and $absRun.ExitCode -eq 0 -and $absAfter -eq $absBefore + 1) $case[1] `
            "before $absBefore, after $absAfter`n$($absFirst.Log)`n$($absRun.Log)"
    }

    # An SVG drawing that links a diagram by an absolute path is composite, so it is flattened
    $absFlow = (Join-Path $svgDir 'flow.mmd') -replace '\\', '/'
    Set-Content (Join-Path $svgDir 'abs-link.svg') -Value (
        '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><image href="' + $absFlow + '" width="50" height="50"/></svg>')
    $absLink = Invoke-Filter 'svg-abs-link' '![a](svg/abs-link.svg)'
    Write-Result ($absLink.ExitCode -eq 0 -and $absLink.Html -match 'generated/canvasnet/abs-link-[0-9a-f]+\.svg') `
        'an SVG that links a diagram by an absolute path is flattened' "$($absLink.Log)`n$($absLink.Html)"

    # An include cycle ends in the filter, and the tool then reports the cycle.
    Set-Content (Join-Path $guide 'cyc-a.scad') -Value "include <cyc-b.scad>`ncube(3);"
    Set-Content (Join-Path $guide 'cyc-b.scad') -Value "include <cyc-a.scad>`ncube(2);"
    $cycle = Invoke-Filter 'cycle' '![m](cyc-a.scad)'
    Write-Result ($cycle.ExitCode -ne 0 -and $cycle.Log -match 'Include cycle' -and $cycle.Log -notmatch 'levels deep|limit is 512') `
        'an include cycle ends in the filter and fails with the tool''s message' $cycle.Log
    # --- Dependency limit ---------------------------------------------------------------------------------
    # A chart that names more data files than the filter hashes (512) must fail, not draw from a partial hash.
    $many = Join-Path $guide 'many'
    New-Item -ItemType Directory -Force -Path $many | Out-Null
    function New-ManyChart([int]$Count) {
        $layers = foreach ($n in 1..$Count) {
            Set-Content (Join-Path $many "d$n.csv") -Value "k,v`na,$n"
            '{"data":{"url":"d' + $n + '.csv"},"mark":"bar"}'
        }
        Set-Content (Join-Path $many "chart$Count.vl.json") -Value (
            '{"$schema":"https://vega.github.io/schema/vega-lite/v5.json","width":120,"height":80,"layer":[' +
            ($layers -join ',') + ']}')
    }
    New-ManyChart 513
    $over = Invoke-Filter 'many-over' '![c](many/chart513.vl.json)'
    Write-Result ($over.ExitCode -ne 0 -and $over.Log -match 'chart513\.vl\.json' -and
        $over.Log -match 'names 513 data files; the limit is 512') `
        'a chart with more than 512 data files fails naming the source and the limit' $over.Log
    New-ManyChart 512
    $at = Invoke-Filter 'many-at' '![c](many/chart512.vl.json)'
    # The filter must hash all 512; the tool then applies its own, lower data-file limit (MaxDataFiles), which is fine here.
    Write-Result (($at.ExitCode -eq 0 -or $at.Log -match 'MaxDataFiles') -and $at.Log -notmatch 'the limit is 512') `
        'a chart with exactly 512 data files is not rejected by the limit' $at.Log

    # The same limit for a model: the model plus 512 included files is 513 hashed files.
    foreach ($n in 1..512) {
        Set-Content (Join-Path $many "i$n.scad") -Value "module m$n() { cube($n); }"
    }
    Set-Content (Join-Path $many 'big.scad') -Value ((1..512 | ForEach-Object { "include <i$_.scad>" }) -join "`n")
    $bigModel = Invoke-Filter 'many-scad' '![m](many/big.scad)'
    Write-Result ($bigModel.ExitCode -ne 0 -and $bigModel.Log -match 'includes are more than 512 files') `
        'a model that includes too many files fails with the limit' $bigModel.Log
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
# Expected tool failures above leave a nonzero $LASTEXITCODE that would otherwise become this script's exit code.
exit 0
