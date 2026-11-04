# Bundled Fallback Fonts

<!-- cspell:ignore dogfoods dogfooding -->
<!-- cspell:ignore Noto Zapf notofonts -->

The 12 `.ttf` files in this folder are the real, unmodified, production "Liberation Sans",
"Liberation Serif", and "Liberation Mono" TrueType font families (four styles each - Regular,
Bold, Italic, BoldItalic), sourced from the official
[liberation-fonts](https://github.com/liberationfonts/liberation-fonts) upstream project's
`2.1.5` release asset `liberation-fonts-ttf-2.1.5.tar.gz`
(<https://github.com/liberationfonts/liberation-fonts/releases/tag/2.1.5>). No file's bytes were
altered in any way after download - each is the exact binary shipped by that release.

All 12 files are licensed under the [SIL Open Font License, Version 1.1](https://openfontlicense.org/)

- see `OFL.txt` (the license's full text, as required for redistribution) alongside them in this
folder, copied verbatim from the same release archive's own `LICENSE` file. This same `OFL.txt`
text is also packed into the NuGet package's `licenses\LiberationFonts-OFL.txt` (see the
`DemaConsulting.CanvasNet.csproj` `Pack="true"` entry) so that anyone inspecting the `.nupkg`
directly, independent of the embedded-resource copy used at runtime, can find the license text
that governs these bundled fonts.

Unlike `test/DemaConsulting.CanvasNet.Tests/FontFixtures/` (which bundles font files purely as
*test* fixtures, never shipped to a consumer), these 12 files are the first **runtime** (non-test)
binary assets ever bundled by this repository: they are embedded as `<EmbeddedResource>` items in
`DemaConsulting.CanvasNet.csproj` and loaded at runtime via
`Assembly.GetManifestResourceStream` by `Fonts.SystemFontCatalog.LoadBundledFallback` - the
deterministic, always-available last-resort font used when a consumer (for example
`DemaConsulting.CanvasNet.Pdf`'s `PdfDocument`) needs to render text with a non-embedded,
non-Standard-14, or otherwise-unmatched font and no equivalent font is installed on the host
operating system.

The SIL OFL permits bundling and redistributing these fonts as part of this software; it may not
be sold on its own, and this repository does not do so. No modification is made to the fonts
themselves, so the "a modified version may not use the original font's Reserved Font Name"
restriction does not apply either.

`BundledLiberationFontsTests.cs` (in `test/DemaConsulting.CanvasNet.Tests/Fonts/`) dogfoods every
one of these 12 files through `Fonts.TrueTypeFont.Load`, verifying each is a genuine, parseable
TrueType font exposing the expected family name and style flags - this is both a one-time sanity
check performed before committing these files and a permanent, repeatable CI gate against any
future accidental corruption or replacement of these files.

## Noto Substitute Fonts (Symbol/ZapfDingbats Fallback)

<!-- cspell:ignore varLib instancer wdth wght -->

The 3 additional `.ttf` files in this folder -
`NotoSans-Regular.ttf`, `NotoSansMath-Regular.ttf`, and `NotoSansSymbols2-Regular.ttf` - are used
by `DemaConsulting.CanvasNet.Pdf`'s `PdfDocument` as the bundled substitute font(s) for a
`/BaseFont /Symbol` or `/BaseFont /ZapfDingbats` simple font with no embedded font program (see
`PdfDocument.FontFallback.cs`'s `ResolveSymbolicNotoFallback`) - unlike every other non-embedded
font, `Symbol`/`ZapfDingbats` are never matched against an unrelated system or Liberation font,
since a symbol/dingbat glyph set has no meaningful generic-family equivalent; instead, a real
font whose glyph repertoire actually covers (most of) the Symbol/ZapfDingbats Adobe/ISO 32000-1
Appendix D character sets is bundled and substituted directly.

Each file is sourced from the official [Noto Fonts](https://notofonts.github.io/) project, from
the following upstream repositories (all at the `2022` release generation referenced by the
`NotoFonts-OFL.txt` copyright header below):

- `NotoSans-Regular.ttf` - from
  [notofonts/latin-greek-cyrillic](https://github.com/notofonts/latin-greek-cyrillic)'s variable
  font `NotoSans[wdth,wght].ttf`, instanced to a single static Regular (weight 400, width 100%)
  instance using [fonttools](https://github.com/fonttools/fonttools)' `varLib.instancer`:

  ```sh
  python -m fontTools.varLib.instancer "NotoSans[wdth,wght].ttf" wght=400 wdth=100 -o NotoSans-Regular.ttf --update-name-table
  ```

  Used as the Symbol substitute's primary font: its Greek-letter and general-symbol glyphs (for
  example `alpha`/`Alpha`, punctuation-like symbol glyphs) cover the bulk of the Symbol encoding.
- `NotoSansMath-Regular.ttf` - from
  [notofonts/math](https://github.com/notofonts/math)'s released static `Regular` instance,
  unmodified. Used as the Symbol substitute's second-priority font, covering Symbol's
  mathematical-operator glyphs (for example summation, integral, and set-theory symbols) that
  `NotoSans-Regular.ttf` itself does not carry.
- `NotoSansSymbols2-Regular.ttf` - from
  [notofonts/symbols](https://github.com/notofonts/symbols)'s released static `Regular` instance
  (the "Symbols 2" family, covering a broader symbol repertoire than the base "Symbols" family),
  unmodified. Used as the Symbol substitute's third-priority (Private-Use-Area-adjacent/rare
  symbol) font, and as the sole substitute font for ZapfDingbats (covering its dingbat glyph
  repertoire, for example `a1` through the arrow/star/circled-digit dingbats).

All 3 files are licensed under the same [SIL Open Font License, Version 1.1](https://openfontlicense.org/)
as the 12 Liberation files above - see `NotoFonts-OFL.txt` alongside them in this folder (its
copyright header separately attributes all three source sub-projects). This same text is also
packed into the NuGet package's `licenses\NotoFonts-OFL.txt` (see the
`DemaConsulting.CanvasNet.csproj` `Pack="true"` entry), exactly mirroring the Liberation fonts'
own `licenses\LiberationFonts-OFL.txt` entry.

`PdfDocument`'s glyph-lookup priority order for a `Symbol` `/BaseFont` tries
`NotoSans-Regular.ttf` first, then `NotoSansMath-Regular.ttf`, then
`NotoSansSymbols2-Regular.ttf` (the first font whose glyph cmap covers a given codepoint wins);
`ZapfDingbats` uses only `NotoSansSymbols2-Regular.ttf`. This is a documented, accepted fidelity
limitation, not full glyph coverage: of Symbol's 163 distinct mapped codepoints, 161 are covered
by this 3-font union (only U+2329/U+232A are not); of ZapfDingbats' 202 distinct mapped
codepoints, 158 are covered (the circled-digit Dingbats U+2460-U+2469/U+2776-U+2793 and
U+271D/U+271E/U+271F/U+2721 are not).

`BundledNotoFontsTests.cs` (in `test/DemaConsulting.CanvasNet.Tests/Fonts/`) dogfoods all 3 of
these files through `Fonts.TrueTypeFont.Load`, exactly like `BundledLiberationFontsTests.cs` does
for the 12 Liberation files above.

## Glyph Fallback Chain (Characters The Liberation Fonts Lack)

The same 3 Noto files are also the one per-character **glyph fallback chain** of the public
`Fonts.GlyphFallback` unit, which `Rendering.TextRenderer` (and so Svg, Charts, Vega-Lite, and the
tool), OpenSCAD `text()`, and the Pdf, Pptx, and Vsdx font fallbacks use for a character that the chosen
font has no glyph for - for example U+221D (PROPORTIONAL TO) and U+226B (MUCH GREATER-THAN), which
Liberation Sans lacks and which were previously drawn as a missing-glyph box with no diagnostic. No new
font file, license text, or package entry was needed: the chain is exactly these 3 files, in this order -
`NotoSans-Regular.ttf`, `NotoSansMath-Regular.ttf`, `NotoSansSymbols2-Regular.ttf` - and only these
are consulted (an installed font never is, so the result is the same on every machine). The chain is
the regular style; a bold or italic request is not synthesized for a fallback glyph. The fonts are
loaded lazily, on the first character that the chosen font misses.

Coverage, measured over the files in this folder (code points mapped; a character that none of the
fonts maps stays a missing-glyph box and is reported where the host has a diagnostics channel):

Each entry reads: block (code points in it) - mapped by Liberation Sans / Noto Sans / Noto Sans Math / Noto Sans
Symbols 2 - mapped by the chain together with Liberation Sans.

- U+221D and U+226B: only Noto Sans Math - covered by the chain.
- Greek 0370-03FF (144): 127 / 121 / 59 / 0 - 135.
- Arrows 2190-21FF (112): 8 / 0 / 112 / 13 - 112.
- Mathematical operators 2200-22FF (256): 18 / 1 / 256 / 7 - 256.
- General punctuation 2000-206F (112): 57 / 111 / 42 / 12 - 111.
- Supplemental mathematical operators and arrows (27C0-27FF, 2900-2AFF): 0 / 0 / all / a few - all.
- Geometric shapes 25A0-25FF (96): 24 / 1 / 96 / 96 - 96.
- Box drawing 2500-257F (128): 40 / 0 / 10 / 0 - 44.
- Block elements 2580-259F (32): 8 / 0 / 8 / 0 - 8.
- CJK ideographs and private-use characters: 0 / 0 / 0 / 0 - 0 (still missing).

The numbers can be reproduced with `fontTools` (`TTFont(path)["cmap"].getBestCmap()`, intersected with
a block's range). Another font was considered for the box-drawing and block-element gaps: DejaVu Sans
2.37 (757 KB, Bitstream Vera license plus public-domain changes) would cover all of both blocks, but it
is weaker on supplemental mathematical symbols and arrows, adds 757 KB and a second license notice, and
the characters it would add are rarely needed in chart or model text, so it is **not** bundled; the
box-drawing characters beyond the 44 that Liberation and Noto together cover remain missing (adding it
later is a one-line change of the chain in `GlyphFallback.cs` plus a license entry). The Noto fonts have
1000 units per em and Liberation 2048, so every renderer scales a fallback glyph with the em size of the
font that supplies it, and none of the Noto fonts has a classic `kern` table, so no kerning is applied
across or within them.
