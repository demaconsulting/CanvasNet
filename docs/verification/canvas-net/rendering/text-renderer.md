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

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; synthetic fonts are constructed in memory via `TestSupport/
  SyntheticFontBuilder`
- **Isolation**: Each test constructs its own `Surface`/`Canvas`/synthetic-font fixture, with no
  shared state between tests

### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework.

### Test Scenarios

#### CanvasNet-Rendering-TextRenderer-Measure: MeasureText Returns Advance-Plus-Kerning Width and Vertical Metrics

**Tests**: `TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth`,
`TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize`,
`TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning`,
`TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics`,
`TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent`

Verifies width equals the sum of scaled advances plus scaled kerning against a synthetic 2-glyph
font (advance 500, kerning -50, ascender 800, descender -200, unitsPerEm 1000), that ascent/
descent are derived from the font's own metrics, and that descent is always the absolute value of
the raw descender even against a (non-conformant) font with a positive raw descender.

#### CanvasNet-Rendering-TextRenderer-Draw: DrawText Renders Each Glyph Outline at the Baseline Anchor

**Tests**: `TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing`,
`TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation`

Verifies `DrawText` renders a string on a `Canvas` at a given `(x, y)` baseline anchor without
throwing, and that the rendered glyphs shift with a translated canvas.

#### CanvasNet-Rendering-TextRenderer-Alignment: Left/Center/Right Alignment Position Glyphs Relative to the Anchor

**Tests**: `TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing`,
`TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX`,
`TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX`

Verifies all three `TextAlign` values: a `Left` run starts at the anchor `x`, a `Center` run is
centered on `x`, and a `Right` run ends at `x`.

#### CanvasNet-Rendering-TextRenderer-TransformComposition: DrawText Composes Canvas and Per-Glyph Pen Transforms

**Tests**: `TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation`,
`TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill`

Verifies text moves with the canvas transform under translation, and that text drawn under a
rotated canvas matches a manually pre-transformed glyph fill through the static `PathFiller`,
catching transform-composition-order and y-flip/rotation sign errors that a translation-only test
cannot.

#### CanvasNet-Rendering-TextRenderer-Validation: Invalid Arguments Are Rejected

**Tests**: `TextRenderer_MeasureText_NullText_ThrowsArgumentNullException`,
`TextRenderer_MeasureText_NullFont_ThrowsArgumentNullException`,
`TextRenderer_MeasureText_NonFiniteSize_ThrowsArgumentOutOfRangeException`,
`TextRenderer_DrawText_NullText_ThrowsArgumentNullException`,
`TextRenderer_DrawText_NullFont_ThrowsArgumentNullException`,
`TextRenderer_DrawText_InvalidAlign_ThrowsArgumentOutOfRangeException`

Verifies `null` text or `null` font arguments are rejected with `ArgumentNullException`, a
non-finite size argument is rejected with `ArgumentOutOfRangeException`, and an undefined
`TextAlign` value is rejected with `ArgumentOutOfRangeException`.

### Traceability

Every requirement in `docs/reqstream/canvas-net/rendering/text-renderer.yaml` links to one or
more of these tests.
