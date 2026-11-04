package CanvasNet {
    part def GlyphFallback {
        doc /* Public static unit: per-character glyph fallback to the bundled Noto fonts (Noto Sans, Noto Sans Math, Noto Sans Symbols 2, already embedded and also used for the PDF Symbol/ZapfDingbats substitute) for a character that a caller's chosen font has no glyph for. Resolve returns the primary font and glyph when it maps the code point (nothing is loaded), else the first chain font that maps it (when NeedsGlyph accepts the code point), else the primary font's missing glyph; FindMissing lists the distinct code points that stay missing so a caller can report them. Deterministic, machine independent (never an installed font), regular style only, lazily loaded, thread-safe. */
        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Fonts/GlyphFallback.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Fonts/GlyphFallbackTests.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts/glyph-fallback.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts/glyph-fallback.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts/glyph-fallback.yaml */
    }
}
