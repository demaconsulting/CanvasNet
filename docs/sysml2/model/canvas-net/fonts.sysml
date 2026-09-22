package CanvasNet {
    part def Fonts {
        doc /* TrueType font-loading subsystem exposing a single public TrueTypeFont unit. It loads glyph-based SFNT fonts, maps Unicode codepoints to glyph indices, extracts glyph outlines as Geometry.Path geometry, and reports horizontal metrics and basic format-0 kerning. Fonts depends only on Geometry; callers combine its output with Drawing and Canvas if they want rendered pixels. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/CanvasNetTests.cs, test/DemaConsulting.CanvasNet.Tests/TestSupport/SyntheticFontBuilder.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts.yaml */

        part trueTypeFont : TrueTypeFont;
    }
}
