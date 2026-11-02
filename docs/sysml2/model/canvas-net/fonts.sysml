package CanvasNet {
    part def Fonts {
        doc /* TrueType and CFF/OpenType font-loading subsystem exposing the TrueTypeFont unit (loads glyph-based (glyf) SFNT fonts and CFF/OpenType (OTTO-flavored) fonts, including standalone classic PostScript Type 1/Type 1C font-program loading, selecting an individual face out of a TrueType Collection (ttcf) container, maps Unicode codepoints to glyph indices, extracts glyph outlines as Geometry.Path geometry, and reports horizontal metrics and basic format-0 kerning)         and the SystemFontCatalog unit (directory-scan-only discovery of fonts installed on the host operating system, best-effort family-name/style matching, a process-wide cached font load, and a bundled Liberation Sans/Serif/Mono last-resort fallback font) built on the FontDirectoryScanner unit (deterministic, bounded, link-safe header-only directory scan), and the FontResolver unit (resolves a CSS font-family list to a loaded font - generic keywords and optional aliases to the bundled fonts, then additional directories, installed fonts and bundled family names - with the GenericFontFamilies classification and list-splitting policy). Fonts depends only on Geometry; callers combine its output with Drawing and Canvas if they want rendered pixels. */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/CanvasNetTests.cs, test/DemaConsulting.CanvasNet.Tests/TestSupport/SyntheticFontBuilder.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/TrueTypeFontRealFontIntegrationTests.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogRealDiscoveryIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts.yaml */

        part trueTypeFont : TrueTypeFont;
        part systemFontCatalog : SystemFontCatalog;
        part fontDirectoryScanner : FontDirectoryScanner;
        part genericFontFamilies : GenericFontFamilies;
        part fontResolver : FontResolver;
    }
}
