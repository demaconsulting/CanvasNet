### TextRenderer Unit Verification Design

The `TextRenderer` unit is verified by
`test/DemaConsulting.CanvasNet.Tests/Rendering/TextRendererTests.cs`.

### Verification Approach

- **Measurement**: `TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth`,
  `TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize`,
  `TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning`,
  `TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics`, and
  `TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent` cover the
  advance + kerning summation and vertical metrics against a synthetic 2-glyph font (advance
  500, kerning -50, ascender 800, descender -200, unitsPerEm 1000), plus the absolute-value
  Descent contract against a font with a (non-conformant) POSITIVE raw descender.
- **Alignment**: `TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing`,
  `TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX`, and
  `TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX` cover the three enum values.
- **Transform composition**: `TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation`
  verifies text moves with the canvas transform, and
  `TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill` verifies
  text under a rotated canvas matches a manually pre-transformed glyph fill through the static
  `PathFiller`, catching transform-composition-order and y-flip/rotation sign errors that a
  translation-only test cannot.
- **Validation**: null-text, null-font, non-finite-size, and invalid-align tests cover the
  validation contract.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/text-renderer.yaml` links to one or
more of these tests.
