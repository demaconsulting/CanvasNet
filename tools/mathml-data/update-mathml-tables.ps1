<#
.SYNOPSIS
    Developer tool: regenerates the data tables of the MathML package (src/DemaConsulting.CanvasNet.MathMl) from the
    pinned W3C and Unicode sources.

.DESCRIPTION
    Not part of the build, the tests, or CI. It downloads (or reads from -SourceDirectory) the pinned sources and
    rewrites the generated C# data files, which are committed:

    - Syntax/CharacterReferences.Table.cs: the 2125 named character references of the W3C "XML Entity Definitions for
      Characters" HTML+MathML set (htmlmathml-f.ent). The double-escaped values (&#38;#38; and the like) are unescaped
      and every value is decoded to its one or two code points.
    - Layout/MathVariants.Data.cs: the italic mapping table of MathML Core (Appendix C.1), used by the automatic
      italic of a single-character mi, and the mathvariant tables (bold, double-struck, script, and every other MathML 3
      value) derived from UnicodeData.txt: each code point whose decomposition is "<font> X" is classified by the
      prefix of its name. A Mathematical Alphanumeric Symbol wins over a Letterlike Symbol with the same style and base,
      so the letterlike symbols only fill the holes of the Mathematical Alphanumeric Symbols block.

    The output is deterministic: run it twice and nothing changes. See src/DemaConsulting.CanvasNet.MathMl/third-party/
    README.md for the provenance and the licenses of the sources.

.PARAMETER SourceDirectory
    A folder that already holds htmlmathml-f.ent, UnicodeData.txt, and mathml-core.html (the dated MathML Core CR);
    when omitted, the files are downloaded from their pinned URLs into a temporary folder.
#>
[CmdletBinding()]
param(
    [string]$SourceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The pinned sources: the W3C entity file, the dated MathML Core Candidate Recommendation, and Unicode 16.0.0
$sources = [ordered]@{
    'htmlmathml-f.ent' = 'https://www.w3.org/2003/entities/2007/htmlmathml-f.ent'
    'mathml-core.html' = 'https://www.w3.org/TR/2025/CR-mathml-core-20250624/'
    'UnicodeData.txt'  = 'https://www.unicode.org/Public/16.0.0/ucd/UnicodeData.txt'
}

$package = Join-Path $PSScriptRoot '../../src/DemaConsulting.CanvasNet.MathMl'
$temporary = $null
if (-not $SourceDirectory) {
    $temporary = Join-Path ([System.IO.Path]::GetTempPath()) ('mathml-data-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary | Out-Null
    foreach ($name in $sources.Keys) {
        Invoke-WebRequest -Uri $sources[$name] -OutFile (Join-Path $temporary $name) -UseBasicParsing
    }

    $SourceDirectory = $temporary
}

# Formats a code point as a C# escape inside a string literal
function Format-Escape([int]$codePoint) {
    if ($codePoint -gt 0xFFFF) {
        return '\U{0:X8}' -f $codePoint
    }

    return '\u{0:X4}' -f $codePoint
}

# Writes a file with LF line endings and UTF-8 without a byte-order mark
function Write-Generated([string]$path, [System.Collections.Generic.List[string]]$lines) {
    $text = ($lines -join "`n") + "`n"
    [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Wrote $path"
}

try {
    # --- The named character references -----------------------------------------------------------------------
    $entities = [System.Collections.Generic.List[object]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $SourceDirectory 'htmlmathml-f.ent'))) {
        $match = [regex]::Match($line, '^<!ENTITY\s+([A-Za-z0-9]+)\s+"([^"]*)"')
        if (-not $match.Success) {
            continue
        }

        # Unescape the double-escaped values (&#38;#38; is the reference &#38;), then decode every reference; a literal
        # character (the space before the combining marks of DotDot, DownBreve, TripleDot, and tdot) is kept as written
        $value = $match.Groups[2].Value.Replace('&#38;', '&')
        $codePoints = [regex]::Matches($value, '&#(x[0-9A-Fa-f]+|[0-9]+);|[^&]') | ForEach-Object {
            $digits = $_.Groups[1].Value
            if (-not $_.Groups[1].Success) { [int][char]$_.Value }
            elseif ($digits.StartsWith('x')) { [Convert]::ToInt32($digits.Substring(1), 16) }
            else { [int]$digits }
        }
        $stripped = [regex]::Replace($value, '&#(x[0-9A-Fa-f]+|[0-9]+);|[^&]', '')
        if ($stripped.Length -ne 0 -or @($codePoints).Count -eq 0) {
            throw "The entity '$($match.Groups[1].Value)' has an unexpected value '$value'."
        }

        $entities.Add([pscustomobject]@{ Name = $match.Groups[1].Value; CodePoints = @($codePoints) })
    }

    # Ordinal order keeps the output stable whatever the culture of the machine
    $sorted = [System.Linq.Enumerable]::ToArray([System.Linq.Enumerable]::OrderBy([object[]]$entities, [Func[object, string]] { param($e) $e.Name }, [System.StringComparer]::Ordinal))

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('// Generated by tools/mathml-data/update-mathml-tables.ps1 from https://www.w3.org/2003/entities/2007/htmlmathml-f.ent; do not edit by hand.')
    $lines.Add('// Portions derived from the W3C XML Entity Definitions for Characters (W3C); see third-party/README.md')
    $lines.Add('')
    $lines.Add('namespace DemaConsulting.CanvasNet.MathMl.Syntax;')
    $lines.Add('')
    $lines.Add('/// <content>')
    $lines.Add('///     The generated table of the named character references.')
    $lines.Add('/// </content>')
    $lines.Add('internal static partial class CharacterReferences')
    $lines.Add('{')
    $lines.Add('    /// <summary>')
    $lines.Add("    ///     The $($sorted.Length) named character references of the W3C HTML+MathML set, in ordinal order of their names: each")
    $lines.Add('    ///     name (case-sensitive, without the ampersand and the semicolon) is followed by its value of one or two code points.')
    $lines.Add('    ///     A property rather than a field, so that the table of the other part of the class is built from it whatever the')
    $lines.Add('    ///     order of the static initializers of the two files.')
    $lines.Add('    /// </summary>')
    $lines.Add('    private static string[] NamedPairs =>')
    $lines.Add('    [')
    foreach ($entity in $sorted) {
        $escaped = ($entity.CodePoints | ForEach-Object { Format-Escape $_ }) -join ''
        $lines.Add("        `"$($entity.Name)`", `"$escaped`",")
    }

    $lines.Add('    ];')
    $lines.Add('}')
    Write-Generated (Join-Path $package 'Syntax/CharacterReferences.Table.cs') $lines

    # --- The italic table of MathML Core (Appendix C.1) ---------------------------------------------------------
    $core = [System.IO.File]::ReadAllText((Join-Path $SourceDirectory 'mathml-core.html'))
    $start = $core.IndexOf('id="c-1-italic-mappings"')
    if ($start -lt 0) {
        throw 'The MathML Core text has no section C.1 (italic mappings).'
    }

    $end = $core.IndexOf('</table>', $start)
    $codes = @([regex]::Matches($core.Substring($start, $end - $start), 'U\+([0-9A-F]{4,6})') | ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
    if ($codes.Count % 2 -ne 0) {
        throw 'The italic table of MathML Core has an odd number of code points.'
    }

    $italic = [System.Collections.Generic.List[int[]]]::new()
    for ($i = 0; $i -lt $codes.Count; $i += 2) {
        $italic.Add(@($codes[$i], $codes[$i + 1]))
    }

    # --- The mathvariant tables (UnicodeData.txt) ---------------------------------------------------------------
    # The name prefixes of each style, longest first so that MATHEMATICAL BOLD ITALIC is not read as MATHEMATICAL BOLD
    $prefixes = @(
        @('MATHEMATICAL SANS-SERIF BOLD ITALIC ', 'SansSerifBoldItalic'),
        @('MATHEMATICAL SANS-SERIF BOLD ', 'BoldSansSerif'),
        @('MATHEMATICAL SANS-SERIF ITALIC ', 'SansSerifItalic'),
        @('MATHEMATICAL SANS-SERIF ', 'SansSerif'),
        @('MATHEMATICAL BOLD ITALIC ', 'BoldItalic'),
        @('MATHEMATICAL BOLD SCRIPT ', 'BoldScript'),
        @('MATHEMATICAL BOLD FRAKTUR ', 'BoldFraktur'),
        @('MATHEMATICAL BOLD ', 'Bold'),
        @('MATHEMATICAL ITALIC ', 'Italic'),
        @('MATHEMATICAL SCRIPT ', 'Script'),
        @('MATHEMATICAL FRAKTUR ', 'Fraktur'),
        @('MATHEMATICAL DOUBLE-STRUCK ', 'DoubleStruck'),
        @('MATHEMATICAL MONOSPACE ', 'Monospace'),
        @('ARABIC MATHEMATICAL DOUBLE-STRUCK ', 'DoubleStruck'),
        @('ARABIC MATHEMATICAL INITIAL ', 'Initial'),
        @('ARABIC MATHEMATICAL TAILED ', 'Tailed'),
        @('ARABIC MATHEMATICAL LOOPED ', 'Looped'),
        @('ARABIC MATHEMATICAL STRETCHED ', 'Stretched'),
        @('PLANCK CONSTANT', 'Italic'),
        @('SCRIPT ', 'Script'),
        @('BLACK-LETTER ', 'Fraktur'),
        @('DOUBLE-STRUCK ITALIC ', $null),
        @('DOUBLE-STRUCK ', 'DoubleStruck')
    )

    $variants = @{}
    $letterlike = [System.Collections.Generic.List[object]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $SourceDirectory 'UnicodeData.txt'))) {
        $fields = $line.Split(';')
        if (-not $fields[5].StartsWith('<font> ')) {
            continue
        }

        $codePoint = [Convert]::ToInt32($fields[0], 16)
        $base = [Convert]::ToInt32($fields[5].Substring(7), 16)
        $style = $null
        foreach ($prefix in $prefixes) {
            if ($fields[1].StartsWith($prefix[0], [System.StringComparison]::Ordinal)) {
                $style = $prefix[1]
                break
            }
        }

        if ($null -eq $style) {
            continue
        }

        # A Mathematical Alphanumeric Symbol wins; a letterlike symbol only fills a hole, so it is applied afterwards
        if ($fields[1].StartsWith('MATHEMATICAL ', [System.StringComparison]::Ordinal) -or $fields[1].StartsWith('ARABIC MATHEMATICAL ', [System.StringComparison]::Ordinal)) {
            if (-not $variants.ContainsKey($style)) {
                $variants[$style] = [System.Collections.Generic.SortedDictionary[int, int]]::new()
            }

            $variants[$style][$base] = $codePoint
        }
        else {
            $letterlike.Add(@($style, $base, $codePoint))
        }
    }

    foreach ($entry in $letterlike) {
        if (-not $variants.ContainsKey($entry[0])) {
            $variants[$entry[0]] = [System.Collections.Generic.SortedDictionary[int, int]]::new()
        }

        if (-not $variants[$entry[0]].ContainsKey($entry[1])) {
            $variants[$entry[0]][$entry[1]] = $entry[2]
        }
    }

    # The tables are written in the order of the MathVariant enumeration
    $order = @('Bold', 'Italic', 'BoldItalic', 'DoubleStruck', 'BoldFraktur', 'Script', 'BoldScript', 'Fraktur', 'SansSerif',
        'BoldSansSerif', 'SansSerifItalic', 'SansSerifBoldItalic', 'Monospace', 'Initial', 'Tailed', 'Looped', 'Stretched')

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('// Generated by tools/mathml-data/update-mathml-tables.ps1 from https://www.w3.org/TR/2025/CR-mathml-core-20250624/ (C.1) and https://www.unicode.org/Public/16.0.0/ucd/UnicodeData.txt; do not edit by hand.')
    $lines.Add('// Portions derived from MathML Core (W3C) and the Unicode Character Database (Unicode); see third-party/README.md')
    $lines.Add('')
    $lines.Add('namespace DemaConsulting.CanvasNet.MathMl.Layout;')
    $lines.Add('')
    $lines.Add('/// <content>')
    $lines.Add('///     The generated tables of the automatic italic and of the mathvariant values.')
    $lines.Add('/// </content>')
    $lines.Add('internal static partial class MathVariants')
    $lines.Add('{')
    $lines.Add('    /// <summary>')
    $lines.Add("    ///     The $($italic.Count) pairs of the italic table of MathML Core (Appendix C.1): each base code point is followed by its")
    $lines.Add('    ///     mathematical italic code point. A property (read once), so that the order of the static initializers of the two')
    $lines.Add('    ///     files of the class does not matter.')
    $lines.Add('    /// </summary>')
    $lines.Add('    private static ReadOnlySpan<int> AutoItalicPairs =>')
    $lines.Add('    [')
    foreach ($pair in $italic) {
        $lines.Add(('        0x{0:X4}, 0x{1:X4},' -f $pair[0], $pair[1]))
    }

    $lines.Add('    ];')
    $lines.Add('')
    $lines.Add('    /// <summary>')
    $lines.Add('    ///     The mapping of every mathvariant value other than <c>normal</c>, from UnicodeData.txt: for each value, the pairs of')
    $lines.Add('    ///     a base code point followed by its styled code point, in ascending order of the base. A property (read once), so')
    $lines.Add('    ///     that the order of the static initializers of the two files of the class does not matter.')
    $lines.Add('    /// </summary>')
    $lines.Add('    private static (MathVariant Variant, int[] Pairs)[] VariantPairs =>')
    $lines.Add('    [')
    foreach ($style in $order) {
        $lines.Add("        (MathVariant.$style,")
        $lines.Add('        [')
        foreach ($pair in $variants[$style].GetEnumerator()) {
            $lines.Add(('            0x{0:X4}, 0x{1:X4},' -f $pair.Key, $pair.Value))
        }

        $lines.Add('        ]),')
    }

    $lines.Add('    ];')
    $lines.Add('}')
    Write-Generated (Join-Path $package 'Layout/MathVariants.Data.cs') $lines
}
finally {
    if ($temporary) {
        Remove-Item -Recurse -Force $temporary
    }
}
