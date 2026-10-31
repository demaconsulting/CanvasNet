## Scenegraph Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scenegraph` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Scenegraph`), containing four units: `SceneModel` (the immutable scene records, the
bounds helper, and the scene budget), `ScenePainter` (the scene to `Surface` painter), `ArcGeometry` (the outline of an
arc mark, with the d3-path emulation that the trail painter shares), and `CssColor` (CSS color text to `Rgba32`).

### Verification Approach

The `Scenegraph` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`scenegraph/`) verify each unit in isolation. The subsystem-level scenarios verify the units working together: the
painter reads the immutable scene records, parses their CSS color strings with `CssColor` (so the scene carries the
colors exactly as the specification and the configuration spell them, which is what the tests of the earlier stages
assert), builds the outline of an arc item with `ArcGeometry`, and draws them to a real `Surface` with the core path
filler, path stroker, and text renderer.
`ScenePainterTests` builds scenes from `SceneModel` records with `CssColor` strings and asserts the pixels that their
geometry covers, and `ScenePainterPathTests` does the same for the paths with curved segments, the arcs, and the trails,
which is the subsystem's contract: the model is the assertion surface for the stages before it, and
the painter turns it into pixels deterministically.

The pixel expectations are analytic: a filled rectangle covers exactly its pixels in its exact color; a stroke is
centered on the edge; half-transparent paint over white gives 127 in the red and green channels (255 x (1 - 128 / 255));
a clip covers exactly its rectangle at integer coordinates and by fractional coverage otherwise; a symbol of a given
size covers the area that the size defines; an arc covers its wedge and leaves the hole of a ring empty, and the area of
its outline is the area of the sector formula; and a trail is as wide as twice the interpolated half width of its
points. No golden images and no output of another implementation are used. The
subsystem uses no mocking or stubbing; the core `Surface`, `Rgba32`, `PathFiller`, `PathStroker`, `TextRenderer`, and the
bundled fonts are real. The subsystem is internal, so its tests reach it through the test assembly's `InternalsVisibleTo`
access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts for the text scenarios
- **Isolation**: Each test builds its own scene and surface; no files are read

### Acceptance Criteria

The `Scenegraph` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the four unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Scene Records Painted With CSS Colors Give The Analytic Pixels**:
  `ScenePainterTests.ScenePainter_Paint_RectAndRuleHonorsFillStrokeOpacity`,
  `ScenePainterTests.ScenePainter_Paint_NoPaintDrawsNothingAndBackgroundFillsTheSurface` — rectangles and rules built
  from model records with CSS color strings (hexadecimal, names, `transparent`, `none`) cover exactly the pixels
  that their geometry covers, in the exact colors, with the stroke centered on the edge and the opacity folded into the
  alpha.
- **Gradient Fills Follow The Stops And The Geometry**:
  `ScenePainterTests.ScenePainter_Paint_GradientFillRunsAlongTheItemBox`,
  `ScenePainterTests.ScenePainter_Paint_GradientFollowsTheScaleAndTheEnclosingGroup`,
  `SceneModelTests.SceneModel_Gradient_DefaultsCopiesAndLimits` — a gradient built from model records fills the
  bounding box of its item from the first stop to the last, with the end colors continuing, and follows the scale and
  the translation of the enclosing groups.
- **Clip, Scale, And Placement Compose**:
  `ScenePainterTests.ScenePainter_Paint_ClipAndScaleMatchAnalyticPixels`,
  `ScenePainterTests.ScenePainter_Paint_FractionalAndNestedClipsUseCoverage` — group translations from the model
  accumulate with the uniform scale and the fit placement, and nested clips intersect.
- **Text From The Model Is Painted Where The Layout Says**:
  `ScenePainterTests.ScenePainter_Paint_TextUsesBaselineRotationAndScale`,
  `ScenePainterTests.ScenePainter_Paint_TextLimitTruncatesTheDrawnLine` — text items measured by the guides stage are
  painted at the anchor, on the baseline, rotated, and scaled, and a limit truncates the drawn line.
- **Curved Paths, Arcs, And Trails Are Painted From The Model**:
  `ScenePainterPathTests.ScenePainter_Paint_SegmentPathStrokesThroughItsCubicCurve`,
  `ScenePainterPathTests.ScenePainter_Paint_ArcFillsOnlyItsWedgeAndLeavesTheHoleEmpty`,
  `ScenePainterPathTests.ScenePainter_Paint_TrailWidthFollowsTheSizeOfEachPoint`,
  `ArcGeometryTests.ArcGeometry_Build_FlattenedAreaMatchesTheSectorFormula`,
  `SceneModelTests.SceneModel_PathSegments_AreCopiedAndTakePrecedenceOverPoints`,
  `SceneModelTests.SceneModel_ArcAndTrail_ExposeTheirGeometryAndCopyLists` — segment paths, arc items, and trail items
  built from model records are drawn along their curves, as the wedge and the hole of the `ArcGeometry` outline, and with
  a width that follows the sizes of the trail points; the outline of an arc has the area of the sector formula.
- **The Stroke Miter Limit Reaches The Stroker**:
  `SceneModelTests.SceneModel_StrokeMiterLimit_DefaultsToNullAndSurvivesWith`,
  `ScenePainterPathTests.ScenePainter_Paint_StrokeMiterLimitBevelsASharpJoin` — the limit that the model carries (none
  by default) is the one that decides between the miter and the bevel of a sharp join when painted, with the Vega
  default of 4 when the item has none.
- **Bounds Of Scene Items Are Exact**:
  `SceneModelTests.SceneBounds_UnionTranslateAndRotate_AreExact`,
  `TextMeasurerTests.TextMeasurer_GetBounds_FollowsVegaTextBounds` — the bounds that layout derives from the model
  records have no rounding noise for translation and quarter turns.
- **Scene Budget And Cancellation**:
  `ScenePainterTests.ScenePainter_Paint_InvalidArgumentsAndCancellation`,
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks` — the painter honors cancellation and
  rejects invalid arguments, and the scene item budget is enforced when the model is built.
- **Whole Chart Pixels**:
  `CanvasNetVegaLite_SystemIntegration_Render_EmptyTitleViewHasDocumentedSizeAndColors`,
  `CanvasNetVegaLite_SystemIntegration_Determinism_RepeatedAndConcurrentRendersAreByteIdentical` — the painted empty view
  has the documented colors at exact pixels, and painting is byte-identical across repeated and concurrent renders.
