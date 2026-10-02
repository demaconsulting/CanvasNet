package CanvasNet {
    part def Fonts {
        doc /* TrueType and CFF/OpenType font-loading subsystem exposing the TrueTypeFont unit (loads glyph-based (glyf) SFNT fonts and CFF/OpenType (OTTO-flavored) fonts, including standalone classic PostScript Type 1/Type 1C font-program loading, selecting an individual face out of a TrueType Collection (ttcf) container, maps Unicode codepoints to glyph indices, extracts glyph outlines as Geometry.Path geometry, and reports horizontal metrics and basic format-0 kerning) and the SystemFontCatalog unit (directory-scan-only discovery of fonts installed on the host operating system, best-effort family-name/style matching, and a bundled Liberation Sans/Serif/Mono last-resort fallback font). Fonts depends only on Geometry; callers combine its output with Drawing and Canvas if they want rendered pixels. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/CanvasNetTests.cs, test/DemaConsulting.CanvasNet.Tests/TestSupport/SyntheticFontBuilder.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/TrueTypeFontRealFontIntegrationTests.cs, test/DemaConsulting.CanvasNet.Tests/Fonts/SystemFontCatalogRealDiscoveryIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts.yaml */

        part trueTypeFont : TrueTypeFont;
        part systemFontCatalog : SystemFontCatalog;
    }
}
