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
