# Bundled Fallback Fonts

<!-- cspell:ignore dogfoods dogfooding -->

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
