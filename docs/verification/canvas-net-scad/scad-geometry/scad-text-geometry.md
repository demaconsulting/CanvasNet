### ScadTextGeometry Unit Verification Design

<!-- cspell:ignore notdef Liberation kerning kerned ttb btt rtl ltr unioned subpath halign -->

This document describes the unit-level verification strategy for the `ScadTextGeometry` unit: the internal static
class that builds the `text` module (`ScadTextGeometry`, `ScadTextGeometry.Flattening`) and the font resolver
`ScadFontResolver`, in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, together with the public
`ScadFontResolutionOptions` that carries its font options.

#### Verification Approach

The unit is verified by evaluating small `CanvasNetScad` programs and calling `ScadTextGeometry.Build` on the first
top-level instance (the helper `Run` of `ScadTextGeometryTests` creates a real `ScadGeometryContext` with a real
`ScadReporter` over an `ScadEvaluationSink`), and by building complete scenes through
`ScadGeometryTestSupport.Build`. The tests assert on the real flat result (its regions, vertices, and bounds) and on
the warnings and unsupported-feature reports, and use no mocks.

Every expectation is an **analytic property computed in the test**, never a recorded result:

- **Font metrics.** The test loads the bundled Liberation Sans Regular through
  `SystemFontCatalog.LoadBundledFallback` and reads the ink bounds (`Path.GetBounds()`), the advance widths, and the
  kerning pairs of straight-outline capitals (`H`, `I`, `E`, `L`, `T`, `A`, `V`) through the public `TrueTypeFont`
  API. For such glyphs the flattened outline is exactly the font outline, so the bounds of the built text are exact.
- **Documented formulas.** The scale (`size` divided by the ink height of `H`), the pen movement (kerning times
  scale before a glyph, advance times scale times spacing after it), the reversal for `rtl`, the vertical stacking
  for `ttb` and `btt`, and the alignment anchors are recomputed in the test from the glyph metrics and compared with
  the bounds of the build to a tolerance of 1e-4 model units.
- **Contours.** `TryMakeRegions` is driven directly with synthetic contours (nested squares, overlapping squares, a
  hole that crosses its solid, a bow tie), and the real glyphs `O` and `Å` check the normalization of holes and the
  split of composite glyphs; an exhaustive test builds every printable character of Basic Latin and Latin-1 in all
  twelve bundled faces and asserts no report.
- **Font resolution.** `ScadFontResolver` is tested directly (`ParseSpec`, `Resolve` with synthetic options and
  temporary directories holding copies of bundled fonts, garbage files, and missing directories) and through the
  module. All explicit-font tests set `UseSystemFonts = false`, so they never depend on the host's fonts; the
  default-font tests run with the system fonts enabled and prove that they are not consulted.
- **Integration.** Builds of complete programs show that the flat result is consumed by `linear_extrude`,
  `rotate_extrude`, a 2D `difference`, `hull`, `color`, a 3D transform, and the slab of a top-level shape, that the
  meshes are closed, and that the volume of an extruded `H` is its outline area times the height. The subsystem and
  system tests add the pixel-level checks (see the `ScadGeometry` subsystem and `CanvasNetScad` system verification).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
- **Mocking**: None; the evaluator, the Render3D regions, and the core `Fonts` and `Geometry` subsystems are the real
  implementations
- **Files**: Temporary directories with copies of bundled fonts and with garbage files are created and deleted by
  five tests; every program is an in-memory string
- **Fonts**: Only the bundled Liberation fonts and fonts copied from them; no test depends on an installed font
- **Isolation**: Each test builds its own program, context, and resolver; no state is shared between tests

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong bound, a warning where silence is expected (or the reverse), a missing or extra report, a wrong
feature identifier, a region that is not valid, or a result that differs between two builds of the same text
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Arguments: Argument Binding

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_EmptyString_ReturnsEmptyWithoutWarnings`,
`ScadTextGeometryTests.ScadTextGeometry_Build_LanguageAndScript_AreAcceptedAndIgnored`,
`ScadTextGeometryTests.ScadTextGeometry_Build_MissingGlyph_UsesNotdefOrWarnsWhenNotdefIsEmpty`

Asserts that an empty string and a string of spaces yield nothing without a warning, that `language` and `script`
of several values (strings, a number, a vector) change nothing and give no warning, and that a number as the text
is converted like `str()` (the text `12` equals the text `"12"`).

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-FontResolution: Default And Explicit Fonts

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_DefaultFontUsesBundledLiberationSansWhenSystemFontsDisabled`,
`ScadTextGeometryTests.ScadTextGeometry_Build_FontSpecParsesFamilyAndBoldItalicStyle`,
`ScadTextGeometryTests.ScadTextGeometry_Build_MissingFontFallsBackToBundledWithWarning`,
`ScadTextGeometryTests.ScadTextGeometry_FontResolver_AdditionalDirectory_MatchesFamilyAndClosestStyle`,
`ScadTextGeometryTests.ScadTextGeometry_FontResolver_AdditionalDirectories_AreSearchedInOrder`,
`ScadTextGeometryTests.ScadTextGeometry_FontResolver_AdditionalDirectory_SkipsMissingAndUnparseableFiles`,
`ScadRenderOptionsTests.ScadRenderOptions_FontResolution_DefaultsAndCopiesDirectories`,
`ScadRenderSystemTests.ScadRenderSystem_Text_BundledFontMetricsAreStableWhenSystemFontsAreDisabled`

Asserts that text without a font has exactly the outline of the bundled sans font with the system fonts on and
off and with the family named explicitly; that the documented forms of the font argument parse to the expected
family and flags (case, spacing, other keys, empty); that the bold face is the bundled bold outline; that an
unknown family gives one warning at the instance naming the family and the sans outline, while a generic name is
silent and gives the bundled serif outline; that a directory holding a bold serif face is searched first and
matched case-insensitively with the closest style; that an additional directory is searched recursively (a face in a
subdirectory with an upper case extension is found), the exact family and style win, the first face in directory
order and then ordinal path order wins a tie, and the result is the same on every run; that a missing directory, a
garbage `.ttf`, an empty `.otf`, and a font with another extension are skipped without a failure and without being
offered (so the family of a damaged file is "not available" with a warning); that the directory list is copied and a
later change of the
caller's list has no effect; and, through the public options, that the directory reaches the geometry (the text
differs from the bundled one). The system test pins literal font-unit metrics of Liberation Sans Regular.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Size: Cap-Height Size

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_SizeUsesSelectedFontCapHeight`,
`ScadRenderSystemTests.ScadRenderSystem_Text_FlatDefaultFontHasAnalyticBoundsAndCoverage`

Asserts that an `H` is exactly `size` tall at sizes 0.5, 10, and 25 in the sans font and in the serif font (with the
width of its own font, so the font was really selected), and, at the system level, that the bounds of a rendered
`HI` and the covered pixel count (the area of the two outlines times the squared pixel density, within 3%) are
analytic and that the counter of the `H` is background.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Layout: Kerning, Spacing, And Directions

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_KerningAndSpacing_AffectMeasuredPlacement`,
`ScadTextGeometryTests.ScadTextGeometry_Build_RtlReversesGlyphOrderWithoutShaping`,
`ScadTextGeometryTests.ScadTextGeometry_Build_TtbAndBttStackUprightGlyphs`

Asserts, for a pair of capitals that the font kerns (found by the test, which fails when there is none), that the
bounds of the text equal the placement computed from the advance, the kerning, and the spacing for spacings 1, 2,
and 0 (with two regions in each case, the glyphs overlapping at spacing 0); that `rtl` of `HI` equals `ltr` of `IH`
vertex for vertex; and that `ttb` and `btt` stack by the scaled advance of the first glyph with the left ink at 0.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Alignment: Ink-Extent Anchors

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_HorizontalAlignment_AnchorsMeasuredInkBounds`,
`ScadTextGeometryTests.ScadTextGeometry_Build_VerticalAlignment_AnchorsMeasuredInkBounds`,
`ScadRenderSystemTests.ScadRenderSystem_Text_AlignmentAndDirectionsHaveAnalyticBounds`

Asserts that `left` (also the default), `center`, and `right` put the minimum, the middle, and the maximum X of the
ink at 0 (case-insensitively), and that `baseline` keeps the baseline, `top`, `center`, and `bottom` put the
maximum, the middle, and the minimum Y at 0 for a text with a descender; at the system level, that the bounds of the
built scene equal the independently laid out ink extents moved by the anchors for seven combinations of alignment,
direction, and spacing.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Glyphs: Missing Glyphs And Controls

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_MissingGlyph_UsesNotdefOrWarnsWhenNotdefIsEmpty`,
`ScadTextGeometryTests.ScadTextGeometry_Build_EmptyString_ReturnsEmptyWithoutWarnings`

Asserts that a character the bundled font does not map (a CJK ideograph) is drawn with the `.notdef` outline without
a warning, that line feeds and tabs vanish without a trace (the text `"H\nI\tH"` equals `"HIH"`), and that spaces
advance without drawing. The branch in which `.notdef` has no outline and the character is skipped with a warning
cannot be reached with the bundled font the test uses (Liberation Sans draws a `.notdef` box) and no test font
without a `.notdef` outline is available to the Scad tests; it is a short branch covered by review and not by a
test.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Contours: Holes, Composite Glyphs, And Rejections

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_GlyphContours_NormalizeHolesAndRejectOverlaps`,
`ScadTextGeometryTests.ScadTextGeometry_Build_AllBundledLatinGlyphs_BuildValidRegions`

Asserts that nested contours of equal winding make one region whose hole is clockwise and whose area is the outer
area minus the hole; that overlapping solids split into two regions; that a hole crossing its solid is rejected
as `scad-text-overlapping-contours` and a bow tie as `scad-text-invalid-outline`; that the real `O` is one
counter-clockwise outline with a clockwise hole whose areas add up to the region area; that the composite `Å` is
several regions without a report; and that all 190 printable characters of Basic Latin and Latin-1 in all twelve
bundled faces build with no warning and no unsupported feature.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Flat2D: Consumption As A 2D Shape

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_TransformsBooleansAndExtrusionsConsumeTheFlatRegion`,
`ScadGeometryTests.ScadGeometry_Build_TextRegion_ParticipatesIn2DBooleanAndLinearExtrusion`,
`ScadRenderSystemTests.ScadRenderSystem_Text_ExtrudedGlyphHasAnalyticHeightAndHoleCoverage`,
`ScadRenderSystemTests.ScadRenderSystem_Text_ExtrudedBooleanMatchesDistributed2DGeometry`

Asserts that text under a 3D rotation and translation after a linear extrusion has exactly the volume (area times
height) and the bounds that follow, that a 2D hull of text is one closed prism, that a 2D difference extrudes to a
boolean node, that a `rotate_extrude` of a glyph and a colored extrusion and a top-level text build closed meshes,
and that none of them reports anything. At the system level, that an extruded `H` has the exact volume of its
outline area times the height, that an extruded `O` shows its counter as background and its ring in the surface
color, and that the image of an extruded 2D difference is identical to that of the difference of the extrusions.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Invalid: Invalid Arguments

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_InvalidArguments_WarnAndReturnEmpty`

Asserts that thirteen invalid forms (no text, undefined text, a zero, negative, or non-numeric size, a negative
spacing, an unknown or non-string `halign`, an unknown `valign` or `direction`, a numeric font, a function as the
text, and a size so large that the coordinates leave the supported range) each give exactly one warning, an empty
result, and no unsupported feature, and that an unknown argument together with a child object gives two warnings
while the text is still built.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Budget: Triangle Budget

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_FlatShapeBudgetUsesExistingSlabAccounting`

Asserts that a top-level text charges exactly the triangles of its slabs (the charge equals the triangle count of the
meshes) and that a budget of 20 triangles is refused with the located triangle-limit error at the instance.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Cancellation: Cancellation

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_Cancellation_IsPolledBeforeFlattening`

Asserts that a build with an already canceled token throws `OperationCanceledException` and reports and warns
nothing.

##### CanvasNetScad-ScadGeometry-ScadTextGeometry-Determinism: Reproducible Output

**Tests**: `ScadTextGeometryTests.ScadTextGeometry_Build_RepeatedCallsAreDeterministic`,
`ScadRenderSystemTests.ScadRenderSystem_Text_RepeatedRendersAreDeterministic`

Asserts that two builds of a text with curves, a hole, and a kerned pair give the same vertices in every region, and
that the text fixture renders byte-identically on repeated renders and with one and four degrees of parallelism,
without a warning or an unsupported feature.
