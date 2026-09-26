## SvgCodec Unit Verification Design

<!-- cspell:ignore unstroked Letterboxing uncatchable Glyf Loca unparseable Cmap -->

This document describes the unit-level verification strategy for the `SvgCodec` class.

### Verification Approach

The `SvgCodec` unit is verified through unit tests that exercise `Load` and `GetInfo` in
isolation, using hand-authored SVG string/stream fixtures (`SvgCodecTests.cs`) for controlled,
targeted coverage of every in-scope construct, malformed-input rejection path, and tolerant
unsupported-construct case, plus a real-file fixture corpus (`SvgFixtureTests.cs`, backed by
`SvgFixtures/*.svg`) for broader, whole-document coverage including a real-font
(`FontFixtures/OpenSans-Regular.ttf`) text-rendering integration test and two real-world,
third-party, Wikimedia-Commons-sourced fixtures (`SvgFixtures/SvgGradient.svg` and
`SvgFixtures/InkscapeFilters.svg`; see `SvgFixtures/WikimediaCommons.LICENSE` for provenance). `Path.GetTempFileName()`
is used for the file-path overload tests. Because `SvgCodec`'s dependencies (`Canvas.Surface`,
`Geometry`, `Drawing`, and `Fonts`) are all sibling in-house units, not external services, no
mocking or stubbing is required. Tests supply an SVG string/stream (and, for text tests, a
synthetic or real `TrueTypeFont`) and assert on rasterized pixel colors/alpha at specific,
hand-computed positions, on `ImageInfo` field values, and on thrown exception types — never on
"no exception thrown" alone, so every test can actually fail if the implementation is wrong.

Unit tests reside in `SvgCodecTests.cs` and `SvgFixtureTests.cs` within the
`DemaConsulting.CanvasNet.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `SvgCodec`'s dependencies are all in-house units
  (`Canvas.Surface`, `Geometry`, `Drawing`, `Fonts`)
- **Isolation**: Each test method builds its own SVG string/stream (and, where needed, its own
  synthetic `TrueTypeFont` via `SyntheticFontBuilder`); file-path tests use a uniquely generated
  temporary file deleted in a `finally` block; no shared state between tests

### Unit-Level Test Scenarios

#### CanvasNet-Codecs-SvgCodec-LoadBasicShapes: Basic Shapes Rasterize Correctly

**Tests**: `SvgCodec_Load_Rect_RendersFilledRectangle`,
`SvgCodec_Load_RectWithRoundedCorners_CutsCornerButFillsCenter`,
`SvgCodec_Load_Circle_RendersFilledCircle`, `SvgCodec_Load_Ellipse_RendersElongatedFill`,
`SvgCodec_Load_Line_RendersStrokedLine`,
`SvgCodec_Load_Polyline_DoesNotCloseBetweenLastAndFirstPoint`,
`SvgCodec_Load_Polygon_RendersClosedFilledShape`,
`SvgCodec_Load_ShapesFixture_RendersExpectedShapeColors`,
`SvgCodec_Load_FromFilePath_ReturnsExpectedPixels`

Renders each basic shape and asserts real pixel colors/alpha at hand-computed positions inside
and outside the shape (for example, a rounded rect's cut corner remains unfilled while its
center is filled; a polyline's implicit-closing edge is absent while a polygon's is present).
`SvgCodec_Load_ShapesFixture_RendersExpectedShapeColors` exercises the same shapes from a real
file under `SvgFixtures/`; `SvgCodec_Load_FromFilePath_ReturnsExpectedPixels` exercises the
`Load(string, int, int, ...)` file-path overload.

#### CanvasNet-Codecs-SvgCodec-LoadPathCommands: Path "d" Mini-Language Commands Rasterize Correctly

**Tests**: `SvgCodec_Load_PathWithAbsoluteMoveLineClose_RendersFilledTriangle`,
`SvgCodec_Load_PathWithRelativeMoveLineClose_RendersSameShapeAsAbsolute`,
`SvgCodec_Load_PathWithHorizontalAndVerticalLines_RendersRectangle`,
`SvgCodec_Load_PathWithCubicBezier_RendersFilledCurvedShape`,
`SvgCodec_Load_PathWithSmoothCubicBezier_RendersFilledShape`,
`SvgCodec_Load_PathWithQuadraticBezier_RendersFilledShape`,
`SvgCodec_Load_PathWithSmoothQuadraticBezier_RendersFilledShape`,
`SvgCodec_Load_PathWithArc_RendersFilledHalfDisc`

Exercises `M`/`m`, `L`/`l`, `H`/`h`, `V`/`v`, `C`/`c`, `S`/`s`, `Q`/`q`, `T`/`t`, `A`/`a`, and
`Z`/`z`, in both absolute and relative forms, asserting the resulting filled region matches the
hand-derived geometry (including that the relative-command variant renders the identical shape
as its absolute-command equivalent, and that an elliptical arc command renders a recognizable
half-disc via `Geometry.SvgArcConverter`).

#### CanvasNet-Codecs-SvgCodec-GroupTransformInheritance: Group Attribute/Transform Inheritance

**Tests**: `SvgCodec_Load_GroupFillInheritance_AppliesToChildWithoutOwnFill`,
`SvgCodec_Load_ChildOwnFill_OverridesGroupFill`,
`SvgCodec_Load_NestedGroupOpacity_MultipliesIntoFillAlpha`,
`SvgCodec_Load_GroupsAndTransformsFixture_RendersAtTransformedPositions`

Asserts a child element with no own `fill` inherits its group's; a child with its own `fill`
overrides the group's; nested groups' `opacity` values multiply together into the resulting
alpha (checked with an `InRange` tolerance rather than an exact value, since alpha compositing
is a floating-point computation); and a real fixture file combining nested groups and transforms
renders shapes at their correctly composed positions.

#### CanvasNet-Codecs-SvgCodec-TransformFunctions: Transform Function Parsing and Composition Order

**Tests**: `SvgCodec_Load_TranslateTransform_MovesShapeByOffset`,
`SvgCodec_Load_ScaleTransform_EnlargesShapeAboutOrigin`,
`SvgCodec_Load_RotateTransform_RotatesShapeAboutOrigin`,
`SvgCodec_Load_MatrixTransform_AppliesRawComponents`,
`SvgCodec_Load_SkewXTransform_ShearsShapeAlongX`,
`SvgCodec_Load_CombinedTransformFunctions_AppliesRightmostFunctionFirst`

Each single-function test hand-derives the expected transformed pixel position (including the
SVG rotation formula `x' = x*cos(θ) - y*sin(θ), y' = x*sin(θ) + y*cos(θ)` and the skewX shear
`x' = x + y*tan(a)`) and asserts a shape rendered there. The combined-function test is a
regression test for a genuine composition-order bug found and fixed during this unit's
implementation: it asserts a `"translate(...) rotate(...)"` transform list applies `rotate`
first and `translate` second (the rightmost-listed function applies to a point first, per the
SVG specification), and would fail under the opposite (naive left-to-right) composition order.

#### CanvasNet-Codecs-SvgCodec-PresentationAttributes: Fill/Stroke Presentation Attributes

**Tests**: `SvgCodec_Load_FillRuleEvenOdd_PunchesHoleInOverlappingSubpaths`,
`SvgCodec_Load_FillRuleNonzeroDefault_FillsUnionOfOverlappingSubpaths`,
`SvgCodec_Load_FillNoneWithStroke_RendersOnlyOutline`,
`SvgCodec_Load_StrokeDasharray_RendersGapsAlongLine`,
`SvgCodec_Load_Opacity_MultipliesIntoFillAlpha`

Asserts `fill-rule="evenodd"` punches a hole where two overlapping same-direction subpaths'
windings cancel, while the default `nonzero` rule fills their union; `fill="none"` paired with a
`stroke` renders only the outline; `stroke-dasharray` produces at least one genuinely unstroked
gap along a line (not merely "some pixels are stroked"); and `opacity` multiplies into a solid
fill's alpha to a value distinguishably between fully transparent and fully opaque.

#### CanvasNet-Codecs-SvgCodec-Gradients: Gradient Fill/Stroke Resolution

**Tests**: `SvgCodec_Load_LinearGradientUserSpaceOnUse_VariesAlongUserSpaceAxis`,
`SvgCodec_Load_RadialGradient_VariesFromCenterToEdge`,
`SvgCodec_Load_GradientSpreadMethodRepeat_TilesPastBaseRange`,
`SvgCodec_Load_StrokeGradient_PaintsVaryingColorAlongStroke`,
`SvgCodec_Load_GradientFixture_RendersVaryingGradientColors`,
`SvgCodec_Load_SvgGradientFixture_RendersVaryingGradientAndPinkBackground`,
`SvgCodec_Load_RadialGradientRNegative_FallsBackToDefaultRadiusWithoutThrowing`,
`SvgCodec_Load_RadialGradientFrNegative_FallsBackToDefaultFocalRadiusWithoutThrowing`,
`SvgCodec_Load_RadialGradientRFrValid_RendersNormally`

Asserts a `userSpaceOnUse` linear gradient varies along the document's user-space axis; a radial
gradient is brighter at its center than near its edge; `spreadMethod="repeat"` tiles a gradient's
base range (asserting two positions at the same fractional offset in successive tiles render
nearly the same color, and that the tiled color is distinguishably different from what a
"pad"/clamped default spread would produce at the same position); and a `stroke="url(#id)"`
gradient reference paints the stroke itself with varying color, not just a fill.
`SvgCodec_Load_SvgGradientFixture_RendersVaryingGradientAndPinkBackground` corroborates this with
a real, unmodified, third-party Wikimedia Commons fixture (`SvgFixtures/SvgGradient.svg`) whose
`userSpaceOnUse` gradient bar and `stop-color` percentage offsets are unlike this unit's other
hand-authored gradient tests. `SvgCodec_Load_RadialGradientRNegative_FallsBackToDefaultRadiusWithoutThrowing`
and `SvgCodec_Load_RadialGradientFrNegative_FallsBackToDefaultFocalRadiusWithoutThrowing` are
regression tests proving a `radialGradient`'s `r`/`fr` attribute - below
`Drawing.RadialGradient`'s documented contract of "finite and greater than or equal to zero" when
negative - falls back to the same default used for an absent attribute rather than reaching that
constructor and throwing an uncaught `ArgumentOutOfRangeException`, matching this codec's existing
tolerant handling of every other gradient coordinate;
`SvgCodec_Load_RadialGradientRFrValid_RendersNormally` confirms a valid, non-negative `r`/`fr`
still renders the gradient normally after this validation was added.

#### CanvasNet-Codecs-SvgCodec-GradientHrefInheritance: Gradient Href Template Inheritance and Cycle Rejection

**Tests**: `SvgCodec_Load_GradientHrefInheritance_InheritsStopsFromTemplate`,
`SvgCodec_Load_GradientHrefChainOfTwoHops_ResolvesToEventualStops`,
`SvgCodec_Load_GradientHrefCycle_ThrowsInvalidDataException`

Asserts a gradient with no `stop` children of its own inherits its stops from the gradient it
references via `href`, that a two-hop `href` chain (through an intermediate stop-less link)
still resolves to the eventual stop-bearing template, and that a cyclical `href` chain (`a`
referencing `b` referencing back to `a`) is rejected with `InvalidDataException` rather than
looping indefinitely.

#### CanvasNet-Codecs-SvgCodec-UseElement: Use Element Resolution, Recursion Guard, and Dangling References

**Tests**: `SvgCodec_Load_UseElement_RendersCopyAtOffsetIndependentOfDocumentOrder`,
`SvgCodec_Load_UseElementDanglingReference_IsSilentNoOp`,
`SvgCodec_Load_UseElementReferencingGroup_RendersAllGroupChildren`,
`SvgCodec_Load_UseElementMutualRecursionCycle_ThrowsInvalidDataException`,
`SvgCodec_Load_UseReferenceFixture_RendersAtOffsetPositionOnly`

Asserts a `use` element referencing an element defined *after* it in document order still
resolves correctly (proving the id index is document-order independent) while the original
element also still renders in place; a `use` referencing a nonexistent id renders nothing and
does not throw; a `use` referencing a `g` group renders every one of the group's children; and a
mutually-recursive `use`/`use` reference chain that would otherwise recurse indefinitely is
rejected with `InvalidDataException` once the bounded recursion guard is exceeded.

#### CanvasNet-Codecs-SvgCodec-MarkerRendering: Marker Vertex Placement, Orientation, and Units

**Tests**: `SvgCodec_Load_MarkerEndOnLine_RendersArrowheadPastLineEnd`,
`SvgCodec_Load_MarkerStartMidEndOnPolyline_RendersDistinctMarkersAtEachVertex`,
`SvgCodec_Load_MarkerOrientAutoOnHorizontalLine_OrientsAlongPositiveX`,
`SvgCodec_Load_MarkerOrientAutoOnVerticalLine_OrientsAlongPositiveY`,
`SvgCodec_Load_MarkerOrientAutoOnDiagonalLine_OrientsAlong45Degrees`,
`SvgCodec_Load_MarkerStartOrientAuto_PointsIntoLine`,
`SvgCodec_Load_MarkerStartOrientAutoStartReverse_PointsAwayFromLine`,
`SvgCodec_Load_MarkerUnitsUserSpaceOnUseVsStrokeWidthDefault_ScalesDifferently`,
`SvgCodec_Load_MarkerWithViewBox_FitsContentToMarkerWidthHeight`,
`SvgCodec_Load_MarkerOnMultiSubpathPath_AppliesStartEndOnlyAtWholePathEnds`,
`SvgCodec_Load_MarkerOnRectCircleEllipse_NeverRendersMarker`,
`SvgCodec_Load_MarkerContent_DoesNotInheritReferencingShapeFillOrStroke`,
`SvgCodec_Load_ArrowMarkersFixture_RendersArrowheadPastLineEnd`

Asserts a `marker-end` reference renders its `marker` element's content past a `line`'s own end
point, sized in user-space units; asserts `marker-start`/`marker-mid`/`marker-end` each
independently resolve and render their own distinct marker at the correct vertex of a
multi-vertex `polyline`, with each marker's own reference point (`refX`/`refY`) landing exactly on
its vertex regardless of rotation; asserts `orient="auto"` orients a marker along a horizontal,
vertical, and 45-degree diagonal segment's own direction of travel (the diagonal case uses an
alpha threshold rather than exact full opacity, tolerating this rasterizer's edge anti-aliasing on
a thin, diagonally rotated shape); asserts a plain `orient="auto"` `marker-start` points into the
line's own body while `orient="auto-start-reverse"` reverses it by 180 degrees to point away from
the line instead; asserts `markerUnits="userSpaceOnUse"` keeps a marker's size independent of the
referencing shape's effective stroke width while the default `markerUnits="strokeWidth"` scales it
proportionally; asserts a marker's own `viewBox` is fitted ("meet" scale-down) into
`markerWidth`/`markerHeight` rather than rendering at its raw, unfitted size; asserts a
multi-subpath `path`'s `marker-start`/`marker-end` apply only to the very first/last vertex of the
whole path, not to each subpath's own start/end, with the interior subpath-boundary vertices
correctly classified as "mid" instead; asserts `rect`/`circle`/`ellipse` never render a marker
even when a `marker-end` attribute is present, since these shapes have no natural vertices to
orient one along; and asserts a marker's own content renders with a fresh presentation-attribute
cascade from the SVG/CSS initial defaults, not inheriting the referencing shape's own
`fill`/`stroke`. A real fixture file (`SvgFixtures/arrow-markers.svg`) exercises the common
`marker-end` arrowhead scenario end-to-end.

#### CanvasNet-Codecs-SvgCodec-MarkerReferenceCycle: Marker Reference Dangling and Cycle Rejection

**Tests**: `SvgCodec_Load_MarkerDanglingReference_IsSilentNoOp`,
`SvgCodec_Load_MarkerSelfReferenceCycle_ThrowsInvalidDataException`,
`SvgCodec_Load_MarkerTwoElementReferenceCycle_ThrowsInvalidDataException`

Asserts a `marker-end` reference to a nonexistent id is tolerated as a silent no-op (the
referencing shape still renders normally, and no marker content appears), matching this codec's
general dangling-reference convention; asserts a `marker` element whose own content directly
references itself (via `marker-start` on a child shape) is rejected with `InvalidDataException`
once the bounded `marker`-reference recursion depth guard is exceeded, rather than recursing
indefinitely; and asserts a two-marker reference chain (`m1` referencing `m2` referencing `m1`) is
likewise rejected, not only a direct self-reference. This mirrors
`CanvasNet-Codecs-SvgCodec-UseElement`'s identical `InvalidDataException` cycle-rejection
behavior for `use`, applied to `marker`'s own independent recursion-depth counter.

#### CanvasNet-Codecs-SvgCodec-ElementNestingDepthLimit: Element/Group Nesting Depth Limit

**Tests**: `SvgCodec_Load_DeeplyNestedGroups_ThrowsInvalidDataException`

Asserts a document consisting of many levels of plain nested `g` elements (no `use` involved)
that exceeds the codec's fixed maximum element-tree recursion depth is rejected with
`InvalidDataException`, rather than being allowed to recurse without bound and risk an
uncatchable `StackOverflowException`. This proves the recursion-depth guard applies to ordinary
group nesting, not only to `use` reference chains covered by
`CanvasNet-Codecs-SvgCodec-UseElement` above.

#### CanvasNet-Codecs-SvgCodec-TotalElementBudget: Total Rendered Element Budget

**Tests**: `SvgCodec_Load_UseFanOutExceedingTotalElementBudget_ThrowsInvalidDataException`

Asserts a document built from nested groups each containing several sibling `use` elements that
all reference the same further-nested group - so every individual reference chain stays well
within both the `use`-nesting and element-tree depth limits, but the total number of elements
resolved across the whole fan-out grows exponentially with nesting depth - is rejected with
`InvalidDataException` once the codec's fixed total-rendered-element budget is exceeded. This
proves the codec bounds total rendering work, not merely the depth of any single reference chain,
guarding against the same class of non-cyclic exponential amplification the Fonts subsystem's
`GlyfLocaReader` unit guards against with its total-resolved-component budget. The test's
fan-out/depth parameters are chosen so the budget check fires almost immediately, keeping the
test itself fast regardless of how the underlying bug would otherwise behave.

#### CanvasNet-Codecs-SvgCodec-TotalGeometryWorkBudget: Total Geometry-Parsing Work Budget

**Tests**: `SvgCodec_Load_PathDataExceedingGeometryWorkBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_PointListExceedingGeometryWorkBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_TextExceedingGeometryWorkBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_PointsListLargeExceedingBudget_ThrowsWithoutLargeAllocation`

Asserts a single `path` element whose `d` attribute contains just over the codec's fixed
combined geometry-parsing work budget worth of implicit-repeat `L` commands, a single `polyline`
whose `points` attribute contains just over that many coordinate pairs, and a single `text`
element whose content is just over that many characters, are each rejected with
`InvalidDataException`. This proves the codec bounds a single element's own parsing cost - a
dimension `CanvasNet-Codecs-SvgCodec-TotalElementBudget` above does not cover, since that budget
counts elements visited, not the size of any one element's content - guarding against the same
class of single-pathological-structure amplification the Fonts subsystem's `GlyfLocaReader` unit
guards against with its own total-resolved-point budget. Each fixture is generated
programmatically (via `string.Concat`/`Enumerable.Repeat`/`new string(...)`) rather than committed
as a literal giant string, and the budget is charged incrementally as each command/coordinate/
character is parsed, so every test throws quickly rather than only after its entire (otherwise
unbounded) content has already been scanned.
`SvgCodec_Load_PointsListLargeExceedingBudget_ThrowsWithoutLargeAllocation` further proves, by
measuring actual bytes allocated via `GC.GetAllocatedBytesForCurrentThread()` (never wall-clock
time, matching the `PngCodecTests`/`TiffCodecTests` allocation-bound precedent), that a `points`
attribute far larger than the budget is rejected without `ParsePointList` ever fully
materializing the whole resolved coordinate list into memory first - closing a regression where
the budget was previously charged only once, in one batch, after the entire attribute had already
been parsed into two full-sized lists.

#### CanvasNet-Codecs-SvgCodec-NumberListLengthBudget: Number-List Attribute Length Budget

**Tests**: `SvgCodec_Load_StrokeDasharrayExceedingNumberListLengthCap_ThrowsWithoutLargeAllocation`,
`SvgCodec_Load_TransformArgumentListExceedingLengthCap_ThrowsInvalidDataException`,
`SvgCodec_Load_ViewBoxNumberListExceedingLengthCap_ThrowsInvalidDataException`

Asserts `ParseNumberList` - the shared parser behind `viewBox`, every transform function's
argument list (`transform`/`gradientTransform`), and `stroke-dasharray` - rejects a single
attribute value containing more than its fixed maximum number of numbers with
`InvalidDataException`, independent of both `TotalElementBudget` (a single attribute, not
multiple elements) and `TotalGeometryWorkBudget` (this attribute family is not charged against
it, so an oversized `stroke-dasharray`/`transform`/`viewBox` would otherwise bypass every existing
budget entirely).
`SvgCodec_Load_StrokeDasharrayExceedingNumberListLengthCap_ThrowsWithoutLargeAllocation` proves,
via `GC.GetAllocatedBytesForCurrentThread()` (matching the `TotalGeometryWorkBudget` allocation-
bound precedent above), that an oversized `stroke-dasharray` is rejected before the cap's
incremental per-number charge lets the backing list grow far beyond the cap, not only after the
whole (otherwise unbounded) list has already been materialized.
`SvgCodec_Load_TransformArgumentListExceedingLengthCap_ThrowsInvalidDataException` and
`SvgCodec_Load_ViewBoxNumberListExceedingLengthCap_ThrowsInvalidDataException` prove the same cap
is reached identically via the two other call sites that share `ParseNumberList`.

#### CanvasNet-Codecs-SvgCodec-TotalDocumentElementBudget: Total Whole-Document Element Budget

**Tests**: `SvgCodec_Load_UnrenderedDefsElementCountExceedingDocumentElementBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_GradientStopCountExceedingDocumentElementBudget_ThrowsWithoutLargeAllocation`

Asserts that `Load`'s `BuildIdIndex` call - which walks every element in the whole parsed
document (`root.DescendantsAndSelf()`) to build the id index used to resolve `href`/`url(#id)`
references, independent of and before `RenderElement`'s own `TotalElementBudget` ever runs -
is itself bounded, reusing the same `MaxTotalRenderedElements`-style budget.
`SvgCodec_Load_UnrenderedDefsElementCountExceedingDocumentElementBudget_ThrowsInvalidDataException`
proves a `<defs>` subtree containing more than the budget's worth of never-rendered elements
(elements `RenderElement` would never visit at all, since nothing references or renders them) is
still rejected with `InvalidDataException`, closing the gap where `TotalElementBudget` alone would
never see them.
`SvgCodec_Load_GradientStopCountExceedingDocumentElementBudget_ThrowsWithoutLargeAllocation`
proves the same guard closes the related `ParseStops` gap: a `linearGradient`/`radialGradient` -
a non-rendering element `RenderElement` charges only once for itself and never recurses into - can
otherwise carry an unbounded number of `<stop>` children, each allocating a `GradientStop`; this
test measures actual bytes allocated (matching the `TotalGeometryWorkBudget`/
`NumberListLengthBudget` allocation-bound precedent above) to prove the oversized `<stop>` list is
rejected before `ParseStops` fully materializes it.

#### CanvasNet-Codecs-SvgCodec-TextRendering: Text Glyph Rendering and Kerning

**Tests**: `SvgCodec_Load_TextWithMatchingFont_RendersGlyphAtExpectedPosition`,
`SvgCodec_Load_TextWithKerningPair_AppliesKerningBetweenGlyphs`,
`SvgCodec_Load_TextFixtureWithRealFont_RendersVisibleGlyphInk`

Uses a synthetic test font (`BuildTestFont`, built via `SyntheticFontBuilder`) with a known,
predictable 50x50-unit square glyph shape, 100-unit advance width, and a single -10-unit kerning
pair, so glyph pixel positions can be hand-computed exactly. Asserts a single glyph renders at
its expected pixel square and nowhere else, and that a two-character run's second glyph is
shifted by the expected kerning amount (asserting a pixel position that is filled only under the
correctly-kerned layout, and would remain unfilled if kerning were ignored). Separately,
`SvgCodec_Load_TextFixtureWithRealFont_RendersVisibleGlyphInk` loads the real
`FontFixtures/OpenSans-Regular.ttf` font and asserts real, non-vacuous ink was painted in the
expected text region while far corners of the canvas remain transparent, mirroring
`TrueTypeFontRealFontIntegrationTests`'s real-font integration pattern.

#### CanvasNet-Codecs-SvgCodec-TextFontFallbackSkipsSilently: Missing/Unmatched Font Silently Skips Text

**Tests**: `SvgCodec_Load_TextWithoutFontsDictionary_SkipsSilentlyWithoutThrowing`,
`SvgCodec_Load_TextFontFamilyNoMatch_SkipsSilentlyWithoutThrowing`,
`SvgCodec_Load_TextFontFamilyCommaSeparatedList_MatchesFirstAvailableFont`

Asserts `Load` with no `fonts` dictionary at all renders no ink for a `text` element and does not
throw; a `fonts` dictionary supplied but containing no entry matching the requested
`font-family` likewise renders no ink and does not throw; and a comma-separated `font-family`
fallback list (`"Nonexistent, TestFont"`) matches the first family actually present in the
dictionary, proving the fallback list is walked rather than only the first entry being
considered.

#### CanvasNet-Codecs-SvgCodec-TextAnchor: Text-Anchor Alignment

**Tests**: `SvgCodec_Load_TextAnchorMiddle_CentersTextHorizontally`,
`SvgCodec_Load_TextAnchorEnd_RightAlignsText`

Asserts `text-anchor="middle"` centers the glyph run on the given `x` (checked against both the
correctly-centered position and the position a start-anchored run would have used instead), and
`text-anchor="end"` right-aligns the run so it ends at the given `x` (checked the same way).

#### CanvasNet-Codecs-SvgCodec-ViewBoxFitting: ViewBox "Meet, Centered" Fitting and Letterboxing

**Tests**: `SvgCodec_Load_WideViewBoxIntoSquareRaster_LetterboxesTopAndBottom`,
`SvgCodec_Load_TallViewBoxIntoSquareRaster_LetterboxesLeftAndRight`

Asserts a wide (landscape) viewBox fit into a square raster is centered with transparent
letterbox bars above and below its content, and a tall (portrait) viewBox fit into a square
raster is centered with transparent letterbox bars to the left and right, in each case also
asserting the content band itself is filled.

#### CanvasNet-Codecs-SvgCodec-GetInfo: GetInfo Reports Resolved Intrinsic Size

**Tests**: `SvgCodec_GetInfo_ViewBoxPresent_ReturnsViewBoxDimensions`,
`SvgCodec_GetInfo_FromFilePath_ReturnsExpectedInfo`

Asserts `GetInfo` reports a document's `viewBox` dimensions (even when conflicting `width`/
`height` attributes are also present, proving `viewBox` takes precedence), always reports
`Channels = 4` and `HasAlpha = true`, and that the `GetInfo(string)` file-path overload returns
the same information as the stream overload.

#### CanvasNet-Codecs-SvgCodec-GetInfoFallback: GetInfo Three-Tier Fallback Policy

**Tests**: `SvgCodec_GetInfo_NoViewBoxWidthHeightPresent_ReturnsWidthHeight`,
`SvgCodec_GetInfo_NoViewBoxNoWidthHeight_ReturnsCssDefault300x150`,
`SvgCodec_GetInfo_WidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing`

Asserts `GetInfo` falls back to a document's `width`/`height` attributes when no `viewBox` is
present, and to the CSS/UA default replaced-element intrinsic size (300x150) when neither a
`viewBox` nor `width`/`height` are present.
`SvgCodec_GetInfo_WidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing` is a regression
test for the width/height-cast-overflow finding: a `width`/`height` resolved dimension large
enough to overflow `Int32` on a naive cast is clamped to `int.MaxValue` rather than reaching
undefined `float`-to-`int` cast behavior (since `int.MaxValue` is not exactly representable as a
`float`). This is sourced from the `width`/`height` fallback tier (`ParseLength`, a tolerant
parser not gated by the coordinate-magnitude bound described below) rather than `viewBox`: a
`viewBox` width/height large enough to overflow `Int32` necessarily also exceeds the new
`MaxCoordinateMagnitude` bound, so it is now rejected earlier instead (see
`SvgCodec_GetInfo_ViewBoxWidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing`'s updated
description under the "Coordinate Magnitude Bound" unlinked scenario below).

#### CanvasNet-Codecs-SvgCodec-MalformedXmlRejected: Malformed XML/ViewBox/Transform Rejected

**Tests**: `SvgCodec_Load_MalformedXml_ThrowsInvalidDataException`,
`SvgCodec_Load_MalformedViewBoxWrongNumberCount_ThrowsInvalidDataException`,
`SvgCodec_Load_ViewBoxNonPositiveWidth_ThrowsInvalidDataException`,
`SvgCodec_Load_MalformedTransformUnrecognizedFunction_ThrowsInvalidDataException`,
`SvgCodec_Load_DocumentExceedingCharacterBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_DocumentWithinCharacterBudget_LoadsSuccessfully`,
`SvgCodec_Load_ViewBoxWidthExtremelySmallCausesNonFiniteFitScale_ThrowsInvalidDataException`

Asserts `Load` throws `InvalidDataException` for non-well-formed XML (an unclosed tag), a
`viewBox` with the wrong number of components, a `viewBox` with a non-positive width, and a
`transform` attribute naming an unrecognized function.
`SvgCodec_Load_DocumentExceedingCharacterBudget_ThrowsInvalidDataException` further asserts a
generated, well-formed document whose total character count exceeds the codec's fixed
`MaxCharactersInDocument` bound is rejected with `InvalidDataException` (surfaced through the
already-existing `XmlException` catch) rather than being fully materialized into an unbounded
in-memory DOM, and `SvgCodec_Load_DocumentWithinCharacterBudget_LoadsSuccessfully` proves a
document sized just under that same bound still loads successfully, so the new bound does not
false-positive-reject an ordinary document.
`SvgCodec_Load_ViewBoxWidthExtremelySmallCausesNonFiniteFitScale_ThrowsInvalidDataException` is a
regression test for the degenerate-fit-transform finding: a `viewBox` width/height that is
positive and finite (passing `ParseViewBox`'s existing checks) but small enough (a subnormal
float) that `ComputeFitTransform`'s own division overflows the resulting scale to a non-finite
value is now rejected with `InvalidDataException` immediately after the fit transform is
computed, rather than silently proceeding to render a blank, fully-transparent surface with no
error at all.
`SvgCodec_GetInfo_OversizedRootAttributeValueExceedingCharacterBudget_ThrowsInvalidDataException`
is a regression test for the unbounded `GetInfo` header-only parse finding: a single root-element
attribute value padded well past the codec's fixed `MaxCharactersInDocument` bound is now rejected
with `InvalidDataException`, since the reader used by `LoadRootElementAttributesOnly` is now
bounded by the same character cap `LoadRootElement` already enforces — even though it never reads
past the root start-tag, it still advances character-by-character through the tag's own attribute
values.

#### CanvasNet-Codecs-SvgCodec-XxeHardening: DOCTYPE-Bearing Documents Rejected via Both Load and GetInfo

**Tests**: `SvgCodec_Load_DocumentWithDoctypeDeclaration_ThrowsInvalidDataException`,
`SvgCodec_Load_DocumentWithExternalEntityDoctype_ThrowsInvalidDataException`,
`SvgCodec_GetInfo_DocumentWithExternalEntityDoctype_ThrowsInvalidDataException`

Asserts `Load` throws `InvalidDataException` for a well-formed-XML document that nonetheless
declares a bare `DOCTYPE` (no entities involved), proving `DtdProcessing.Prohibit` rejects DTD
processing outright rather than merely limiting what an entity can resolve to. Separately, asserts
`Load` throws `InvalidDataException` for a document whose `DOCTYPE` declares and references an
external general entity (a `file:///etc/passwd` SYSTEM URI - the canonical XXE payload shape). Per
.NET's documented `DtdProcessing.Prohibit` behavior, the reader raises `XmlException` as soon as it
encounters the `DOCTYPE` node itself, before any entity resolution is ever attempted, so the
referenced resource is never read or requested - the tests prove the document is rejected, not
(via a resolver spy or similar) that no fetch attempt occurs; that guarantee rests on the
documented framework behavior of `DtdProcessing.Prohibit` combined with the `null` `XmlResolver`.
This coverage is for external *general* entities only; external *parameter* entities are not
separately tested, though the same `DtdProcessing.Prohibit` rejection applies equally to both.
Separately, asserts `GetInfo` throws `InvalidDataException` for the same external-entity-declaring
document, proving the hardening applies equally to `LoadRootElementAttributesOnly`'s
root-start-tag-only `XmlReaderSettings`, not only to `LoadRootElement`'s full-document
`XmlReaderSettings`.

#### CanvasNet-Codecs-SvgCodec-MalformedPathDataRejected: Malformed Path "d" Data Rejected

**Tests**: `SvgCodec_Load_MalformedPathDataUnknownCommand_ThrowsInvalidDataException`,
`SvgCodec_Load_MalformedPathDataMissingArguments_ThrowsInvalidDataException`

Asserts `Load` throws `InvalidDataException` for a `path` `d` attribute containing an
unrecognized command letter and, separately, a command missing its required numeric arguments.

A further, independent case is relative-coordinate accumulation (or an `S`/`T` smooth-curve
reflection) overflowing an individually-finite pair of literals to a non-finite value:
`SvgCodec_Load_PathRelativeAccumulationOverflowsToInfinity_TerminatesPromptlyWithoutHanging` and
`SvgCodec_Load_PathSmoothCubicReflectionOverflowsToInfinity_SkipsPathWithoutThrowing` originally
proved (respectively) that this was tolerated - by relative-coordinate `RequireFinite`/
`OverflowException` skipping, and by the `S`/`T` reflection's identical mechanism - rather than
hanging or throwing uncaught. **Superseded by the coordinate-magnitude bound (see "Coordinate
Magnitude Bound" under Additional Regression Test Scenarios below)**: both tests' original inputs
(`3e38`) now exceed the codec's fixed `MaxCoordinateMagnitude` bound and are rejected by
`TryReadNumber` before path-data parsing even begins - both tests are retained under their
original names, repurposed to prove this new, earlier rejection point (`InvalidDataException`)
instead. The underlying `RequireFinite`/`OverflowException` tolerant-skip mechanisms remain in the
source as defense-in-depth, now unreachable through any input `Load` can be given.

A distinct, non-overflow case is a path spanning coordinates that are huge but individually
entirely finite (no `Infinity`/`NaN` anywhere), combined with a fine `stroke-dasharray`:
`SvgCodec_Load_HugeFinitePathWithFineDashPattern_TerminatesPromptlyWithoutHanging` originally
proved that `Drawing.DashSplitter.BuildOnIntervals`'s cheap pre-flight iteration estimate detects
this input's cost up front and short-circuits directly to the solid-stroke fallback rather than
running its traversal loop at all. **Superseded by the coordinate-magnitude bound**: this test's
original input (`1e20`) likewise now exceeds `MaxCoordinateMagnitude` and is rejected before
`DashSplitter` is ever reached - retained under its original name, repurposed identically to the
two tests above.

#### CanvasNet-Codecs-SvgCodec-PercentageGeometryRejected: Percentage Rejected on Geometry Attributes

**Tests**: `SvgCodec_Load_RectXPercentage_ThrowsInvalidDataException`,
`SvgCodec_Load_RectWidthPercentage_ThrowsInvalidDataException`

Asserts `Load` throws `InvalidDataException` for a `rect`'s `x` attribute expressed as a
percentage and, separately, for its `width` attribute expressed as a percentage, proving a
shape/text geometry attribute's percentage value is explicitly rejected rather than silently
resolved against an undefined basis. Separately confirms (no dedicated regression test needed,
since both already exist and are unaffected) that
`SvgCodec_Load_NegativeScientificAndPercentageValues_RendersWithoutThrowing` (which only exercises
`opacity="50%"`) and `SvgCodec_Load_GradientStopOffsetPercentage_RendersGradientCorrectly` (which
exercises gradient `stop` `offset` percentages) continue to pass unmodified, proving
opacity-family attributes and gradient coordinates remain correctly unaffected by this rejection.

#### CanvasNet-Codecs-SvgCodec-UnsupportedConstructsIgnored: Out-of-Scope Constructs Tolerated

**Tests**: `SvgCodec_Load_UnsupportedConstructs_StillRendersRestOfDocument`,
`SvgCodec_Load_ToleratesUnsupportedConstructFixture_StillRendersRemainingContent`,
`SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent`

Builds a document containing `style`, `filter`, `mask`, `clipPath`, `pattern`, and a
nested `svg` alongside an ordinary `rect`, and asserts the ordinary `rect` still renders — proving
none of the out-of-scope elements abort the whole document. A real fixture file exercises the
same property end-to-end.
`SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent` corroborates this
with a large, real, unmodified, third-party Wikimedia Commons fixture
(`SvgFixtures/InkscapeFilters.svg`) containing dozens of `filter="url(#...)"` references to
`feGaussianBlur`/`feComposite`/`feSpecularLighting`-based filter effects, proving the
tolerant-ignore policy holds at real-world scale and complexity, not only for a small synthetic
document.

#### CanvasNet-Codecs-SvgCodec-ValidationNull: Null Stream/Path Rejected

**Tests**: `SvgCodec_Load_NullStream_ThrowsArgumentNullException`,
`SvgCodec_Load_NullPath_ThrowsArgumentNullException`,
`SvgCodec_GetInfo_NullStream_ThrowsArgumentNullException`,
`SvgCodec_GetInfo_NullPath_ThrowsArgumentNullException`

Calls each of `Load(Stream, ...)`, `Load(string, ...)`, `GetInfo(Stream)`, and `GetInfo(string)`
with a null stream/path argument and asserts `ArgumentNullException` is thrown in every case.

#### CanvasNet-Codecs-SvgCodec-ValidationEmptyPath: Empty/Whitespace Path Rejected

**Tests**: `SvgCodec_Load_EmptyPath_ThrowsArgumentException`,
`SvgCodec_Load_WhitespacePath_ThrowsArgumentException`,
`SvgCodec_GetInfo_EmptyPath_ThrowsArgumentException`

Calls `Load(string, ...)` with an empty path and, separately, a whitespace-only path, and
`GetInfo(string)` with an empty path, asserting `ArgumentException` is thrown in every case.

#### CanvasNet-Codecs-SvgCodec-ValidationOutputDimensions: Output Dimension Validation Delegates to Surface

**Test**: `SvgCodec_Load_NonPositiveWidth_PropagatesSurfaceArgumentOutOfRangeException`

Calls `Load` with a well-formed SVG document but a non-positive requested output width, and
asserts `Surface`'s own `ArgumentOutOfRangeException` propagates unwrapped (not the
`InvalidDataException` this codec uses for malformed *file* data), confirming the deliberate
design decision that caller-supplied raster dimensions are ordinary API parameters rather than
untrusted input.

### Additional Regression Test Scenarios (Unlinked)

The tests below are defensive/regression tests added for a bug fix, not new observable features;
per `requirements-principles.md`, tests may exist without a linked requirement, so these entries
deliberately do not use the `CanvasNet-Codecs-SvgCodec-{Id}:` heading pattern above.

#### Bounded GetInfo Header-Only Parsing

**Test**: `SvgCodec_GetInfo_MalformedXmlAfterRootElement_DoesNotThrowAndReturnsViewBoxDimensions`

Asserts `GetInfo`'s bounded, root-start-tag-only `XmlReader` parse still resolves and returns
`viewBox` dimensions for a document that is malformed only beyond the root element's own
attributes - the same markup the `MalformedXmlRejected` scenario above proves `Load`'s
full-document parse still correctly rejects.

#### Non-Finite Numeric Attribute/Token Rejection or Tolerant Fallback

**Tests**: `SvgCodec_Load_WidthAttributeNaN_ThrowsInvalidDataException`,
`SvgCodec_Load_StrokeWidthInfinity_ThrowsInvalidDataException`,
`SvgCodec_Load_PathDataNumberOverflowToInfinity_ThrowsInvalidDataException`,
`SvgCodec_Load_PointsListNumberOverflowToInfinity_ThrowsInvalidDataException`,
`SvgCodec_Load_NegativeScientificAndPercentageValues_RendersWithoutThrowing`,
`SvgCodec_Load_GradientStopOffsetNaN_DoesNotThrowAndRenders`,
`SvgCodec_Load_GradientX1Infinity_FallsBackToDefaultAndRenders`,
`SvgCodec_Load_RgbaAlphaInfinity_TreatsColorAsUnrecognizedNoPaint`,
`SvgCodec_GetInfo_WidthHeightInfinity_FallsBackToDefaultSize`,
`SvgCodec_GetInfo_WidthHeightScientificNotation_ResolvesToBareValue`,
`SvgCodec_Load_GradientStopOffsetPercentage_RendersGradientCorrectly`

A non-finite (`NaN`/`Infinity`) numeric value reaching this codec's parsing is handled by one of
two distinct, deliberate failure modes depending on which private parsing method the attribute or
token flows through - not a single uniform "rejected everywhere" rule:

- **Throwing (`ParseCoordinate`/`TryReadNumber`)**: a shape attribute parsed by `ParseCoordinate`
  (the literal text `"NaN"`/`"Infinity"` reaches `float.Parse` unfiltered there), and a `path` `d`
  coordinate/`points` list entry parsed by `TryReadNumber` (which requires a legitimately-scanned,
  exponent-overflowing token such as `"1e400"`, since its character-class scan never matches
  literal `"NaN"`/`"Infinity"` text in the first place), both throw `InvalidDataException` when
  the parsed value is non-finite.
- **Tolerant null/fallback (`ParsePercentOrNumber`/`ParseLength`)**: a gradient `stop`'s `offset`,
  a gradient's `x1`/`y1`/`x2`/`y2`/`cx`/`cy`/`r`/`fx`/`fy`/`fr` coordinate, an `rgb()`/`rgba()`
  channel or alpha, and the root `<svg>` element's `width`/`height` fallback tier are each parsed
  by `ParsePercentOrNumber` or `ParseLength` - methods whose pre-existing, already-documented
  contract treats an unparseable value as "absent"/"unrecognized" rather than an error. A
  non-finite result is now rejected the same tolerant way (falling back to each attribute's
  documented default, or causing the enclosing color/gradient to be treated as unrecognized),
  rather than introducing a new throw site inconsistent with that pre-existing contract.

All eleven tests above, together, confirm both failure modes reject the exact malformed input
each is responsible for, while confirming legitimate finite values sharing similar syntax (a
negative number, scientific notation, a percentage) still parse and render correctly in both
code paths.

A closely related but distinct case is a gradient radius (`r`/`fr`) that parses to a finite but
**negative** value: `GetGradientCoordinateOrDefault`'s existing tolerant fallback above already
guarantees finiteness, but a radius is the only gradient-geometry attribute with an additional
sign constraint (`Drawing.RadialGradient`'s constructor requires `startRadius`/`endRadius` to be
"finite and greater than or equal to zero"). `SvgCodec_Load_RadialGradientRNegative_FallsBackToDefaultRadiusWithoutThrowing`
and `SvgCodec_Load_RadialGradientFrNegative_FallsBackToDefaultFocalRadiusWithoutThrowing` prove a
negative `r`/`fr` falls back to the same default used for an absent attribute rather than
reaching that constructor and throwing an uncaught `ArgumentOutOfRangeException`, matching the
tolerant-fallback convention documented above for every other gradient coordinate;
`SvgCodec_Load_RadialGradientRFrValid_RendersNormally` confirms a valid, non-negative `r`/`fr`
still renders correctly.

A further, independent case is a composed transform overflowing to a non-finite value across
nested `transform="scale(...)"` groups, each individually finite but whose cross-element product
is not. `RenderElement` guards its own composed transform against this before rendering the
element at all, but that guard is deliberate defense-in-depth, not a closed crash repro: every
currently-known way a non-finite composed transform could otherwise reach a throwing
`Drawing`-namespace constructor is already independently guarded closer to that constructor -
a non-finite effective stroke width is caught by `RenderStroke`'s own check
(`SvgCodec_Load_NestedTransformScaleOverflowsStrokeWidthToInfinity_SkipsStrokeWithoutThrowing`
proves this), and a non-finite gradient transform is caught by `BuildGradient`'s own check (see
below) - and the rasterizer/stroker otherwise tolerate non-finite path coordinates without
throwing, including for a plain solid fill with no stroke or gradient at all. An earlier version
of this test suite included a solid-fill-only repro claiming to exercise `RenderElement`'s guard
independently of `BuildGradient`'s; that test was removed because it passed identically whether or
not `RenderElement`'s guard existed (a solid-fill shape never reaches any throwing constructor
regardless), so it could not actually detect regressions in that specific guard.
`SvgCodec_Load_NestedTransformScaleOverflowsGradientTransformToNonFinite_SkipsElementWithoutThrowing`
is the concrete crash-closing regression test: the same nested-`<g>` overflow around a
`fill="url(#id)"` shape referencing a `linearGradient` used to reach `Drawing.Gradient`'s
constructor and throw an uncaught `ArgumentOutOfRangeException`, and now loads successfully with
the affected element's rendering tolerantly skipped.
`SvgCodec_Load_GradientTransformComposedWithHugeBoundingBoxOverflowsToNonFinite_TreatsAsNoPaintWithoutThrowing`
proves `BuildGradient`'s own, independent composed-transform guard: an extreme-but-individually-finite
shape bounding box combined with the gradient's own `gradientTransform` overflows only inside
`BuildGradient`, with the shape's own `RenderElement`-composed transform staying finite throughout,
so this case is not covered by the gradient-transform test above.

A further, independent overflow site is `RenderStroke`'s own scaling of an already-finite,
already-parsed `stroke-dasharray`/`stroke-dashoffset` by the composed transform's scale factor
(the same scale `stroke-width` is already scaled by): the raw parsed dash values can be
individually finite yet overflow once scaled by an extreme-but-finite transform.
`SvgCodec_Load_ScaledDashArrayOverflowsToInfinity_FallsBackToSolidStrokeWithoutThrowing` proves a
`stroke-dasharray` combined with a `transform="scale(...)"` large enough to overflow the scaled
dash entry to non-finite falls back to "no dashing" (a solid stroke) rather than reaching
`Drawing.StrokeStyle`'s constructor and throwing an uncaught exception - the stroke itself still
renders, just without its dash pattern, matching `ParseDashArray`'s own existing tolerant
"malformed dash array -> no dashing" convention.

#### Arc-Conversion Overflow Tolerant Skip

**Tests**: `SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing`,
`SvgCodec_Load_RectRoundedCornerArcConversionOverflowsToNonFinite_SkipsShapeWithoutThrowing`

Regression tests for the unguarded `SvgArcConverter` output finding: an extreme-but-individually-
finite arc radius drives `Geometry.SvgArcConverter.ToBeziers`'s internal ellipse-center arithmetic
(which squares the radii) to overflow one of its emitted control points or endpoints to a
non-finite value, even though every raw literal token is itself finite. Both tests were originally
written to exercise this directly - one via an explicit `A` path-data command, proving
`PathDataParser.AppendArc`'s new `RequireFinite`-validated segment output causes the affected
`path` element to be tolerantly skipped; the other via `rect`'s rounded-corner construction
(`AppendArcTo`, also used by `circle`/`ellipse`), proving `BuildRectPath`'s new narrowly-scoped
`catch (OverflowException)` causes the affected `rect` element to be tolerantly skipped instead of
propagating a raw non-finite value into the rasterizer.

**Superseded by the coordinate-magnitude bound (see "Coordinate Magnitude Bound" below)**: both
tests' original radius/width/height literals (`1e18`/`2e18`) now exceed the codec's fixed
`MaxCoordinateMagnitude` bound (1,000,000) and are rejected by `TryReadNumber`/`ParseCoordinate`
before path-data/shape-attribute parsing even begins - squaring any radius within the new bound
(at most `1e12`) can no longer overflow `SvgArcConverter`'s own arithmetic (whose intermediate
products stay near `1e24`, far short of float's ~3.4e38 range). Both tests are retained under
their original names, repurposed to prove this new, earlier rejection point (`InvalidDataException`)
instead. The `RequireFinite`/`catch (OverflowException)` tolerant-skip mechanisms added by
Finding 4 remain in the source as defense-in-depth, now unreachable through any input `Load` can
be given, matching this class's other now-unreachable-but-retained guards (see the
"Budget/Counter Check-Before-Add Ordering" reachability note below).

#### Budget/Counter Check-Before-Add Ordering

**Tests**: `SvgCodec_Load_TextExceedingGeometryWorkBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_UseFanOutExceedingTotalElementBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_UnrenderedDefsElementCountExceedingDocumentElementBudget_ThrowsInvalidDataException`

Regression tests (already-existing, boundary-condition tests, not new ones) for the add-then-check
budget/counter ordering finding: `GeometryWorkBudget.Charge`, `BuildIdIndex`'s `totalElements`
counter, and `RenderElement`'s `totalElements` counter all previously added the new amount to the
running total *before* checking it against the fixed budget, an ordering that is not correct
defense-in-depth against a single call charging an amount large enough to make the addition itself
wrap `int` (a check that would then incorrectly pass). Every counter site was changed to
check-before-add (rejecting when the new amount would exceed the remaining budget, before ever
adding it), with no observable behavior change at the real, already-tested boundary — the tests
above (which already existed prior to this fix) continue to pass unchanged, confirming the
reordering is behavior-preserving for every realistically reachable input.

**Reachability note**: given today's fixed constants (`MaxDocumentCharacters = 5,000,000`,
`MaxTotalGeometryWork = 200,000`, `MaxTotalRenderedElements = 100,000`), no call site can charge
an `amount` anywhere near large enough to make `_total += amount`/`totalElements++` wrap `int`
(~2.147 billion) in one step - the very next charge past each budget already throws well before
`_total`/`totalElements` could climb anywhere near that range. This fix is therefore verified as
defense-in-depth against a future change to these constants, not as a closed repro of a presently
reachable overflow; no synthetic overflow-triggering test is included, since one is not
achievable through a realistic `Load()` call today, and fabricating one (for example, by calling
`Charge` directly with a contrived huge value) would not exercise any code path a real caller can
reach.

#### Coordinate Magnitude Bound

**Tests**: `SvgCodec_Load_PathDataCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException`,
`SvgCodec_Load_PointsListCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException`,
`SvgCodec_Load_CoordinateWithinMaxMagnitude_RendersSuccessfully`,
`SvgCodec_Load_ScaledDashArrayOverflowsToInfinity_FallsBackToSolidStrokeWithoutThrowing`,
`SvgCodec_GetInfo_ViewBoxWidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing`,
`SvgCodec_Load_PathRelativeAccumulationOverflowsToInfinity_TerminatesPromptlyWithoutHanging`,
`SvgCodec_Load_HugeFinitePathWithFineDashPattern_TerminatesPromptlyWithoutHanging`,
`SvgCodec_Load_PathSmoothCubicReflectionOverflowsToInfinity_SkipsPathWithoutThrowing`,
`SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing`,
`SvgCodec_Load_RectRoundedCornerArcConversionOverflowsToNonFinite_SkipsShapeWithoutThrowing`

Regression tests for the "budget counts parsed units, not real downstream cost" mismatch finding:
a document with only a handful of `path`/`points` commands using extreme-but-individually-finite
coordinate magnitudes (for example `3e38`) could previously drive `Geometry.BezierFlattening`'s
per-curve flattening cost far out of proportion to the command-count budget
(`GeometryWorkBudget`) that is supposed to bound total work, and could also feed
`Geometry.SvgArcConverter`'s arc-conversion arithmetic with near-`float.MaxValue` inputs.
`TryReadNumber` (the shared numeric-parsing choke point behind path `d` data, `points` lists,
`viewBox`, and transform-function arguments) and `ParseCoordinate` (the shared choke point behind
every single-value geometry/length/opacity attribute) now both additionally reject a
syntactically valid, finite value whose magnitude exceeds a new, generously-bounded
`MaxCoordinateMagnitude` constant, with the same `InvalidDataException`/"malformed token"
convention already used for a non-finite value at each site.
`SvgCodec_Load_PathDataCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException` and
`SvgCodec_Load_PointsListCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException` prove a
coordinate just over the new bound is rejected from each of `TryReadNumber`'s two call sites, and
`SvgCodec_Load_CoordinateWithinMaxMagnitude_RendersSuccessfully` proves an ordinary, real-world-
sized coordinate well under the bound is unaffected and still renders correctly.

**Ripple effect on prior rounds' overflow-tolerant-skip tests**: this new, low-magnitude parse-time
bound necessarily intercepts several inputs that six earlier regression tests (from this round and
prior rounds) relied on to reach their own, deeper tolerant-skip/hang-avoidance mechanisms - since
each of those tests' inputs used a coordinate/radius/dasharray-entry/viewBox-dimension literal
(`1e18`-`3e38`) now rejected by `MaxCoordinateMagnitude` before parsing ever reaches the mechanism
under test. In every one of these six cases, the underlying downstream mechanism (`RequireFinite`/
`OverflowException` tolerant-skip in `PathDataParser`, `RenderStroke`'s scaled-dasharray fallback,
`DashSplitter`'s pre-flight iteration-budget short-circuit, and `GetInfo`'s clamp-before-cast
logic) is now **provably unreachable** through any input `Load`/`GetInfo` can be given (verified by
direct calculation - see each test's own updated remarks for the specific bound), since any input
extreme enough to trigger the downstream mechanism is, by construction, also extreme enough to
already exceed `MaxCoordinateMagnitude`. Each of the six tests below is retained under its
original name (preserving its git history/traceability) and repurposed to instead prove this new,
earlier, and simpler rejection point; the downstream mechanisms themselves remain in the source as
defense-in-depth, following the same "retain as defense-in-depth even where unreachable given
today's constants" precedent already established for the check-before-add budget/counter fix
above:

- `SvgCodec_Load_ScaledDashArrayOverflowsToInfinity_FallsBackToSolidStrokeWithoutThrowing` - a
  dasharray entry over the bound is now rejected before `RenderStroke`'s own scaling runs (proven
  by direct calculation that the scaled-entry overflow this test originally repro'd cannot occur
  for any entry/transform-scale combination that stays within the bound, since
  `EstimateUniformScale`'s own determinant computation would itself overflow first for any scale
  large enough to still overflow a bound-compliant entry).
- `SvgCodec_GetInfo_ViewBoxWidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing` - a
  `viewBox` width/height large enough to overflow `Int32` also necessarily exceeds the bound - the
  `width`/`height`-fallback-tier sibling test remains the sole regression coverage for the
  clamp-before-cast logic itself (see "GetInfo Three-Tier Fallback Policy" above).
- `SvgCodec_Load_PathRelativeAccumulationOverflowsToInfinity_TerminatesPromptlyWithoutHanging`,
  `SvgCodec_Load_HugeFinitePathWithFineDashPattern_TerminatesPromptlyWithoutHanging`, and
  `SvgCodec_Load_PathSmoothCubicReflectionOverflowsToInfinity_SkipsPathWithoutThrowing` - each
  relied on a single coordinate/control-point literal at or beyond `1e18`, now rejected before the
  path-data command it appears in is even parsed (see "Malformed Path 'd' Data Rejected" above for
  each test's updated description).
- `SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing` and
  `SvgCodec_Load_RectRoundedCornerArcConversionOverflowsToNonFinite_SkipsShapeWithoutThrowing` -
  see "Arc-Conversion Overflow Tolerant Skip" above.

#### Post-Transform Coordinate/Stroke-Width Magnitude Bound

**Tests**: `SvgCodec_Load_TransformScaleAmplifiesCoordinatePastMagnitudeBound_SkipsShapeWithoutThrowing`,
`SvgCodec_Load_StrokeWidthScaledPastMagnitudeBound_SkipsStrokeWithoutThrowing`

Regression tests for a cloud-PR-review finding that the "Coordinate Magnitude Bound" section above
enforces `MaxCoordinateMagnitude` only at parse time, on a source-literal value - strictly
*before* any `transform` attribute has been applied. A `transform="scale(...)"` function's own
argument only needs to stay at or under `MaxCoordinateMagnitude` to pass its own parse-time check
(the comparison is strict `>`, so a literal of exactly `1,000,000` is not rejected), so an in-bound
local coordinate composed with an in-bound-but-large transform (for example `scale(1000000)`) can
still produce a final pixel-space magnitude the flattening/stroking pipeline was never meant to
see, even though neither individual literal involved is itself out of bounds.
`SvgCodec_Load_TransformScaleAmplifiesCoordinatePastMagnitudeBound_SkipsShapeWithoutThrowing`
proves the fix: a small, entirely in-bound closed-square path (built from `LineTo` commands and a
degenerate `CubicBezierTo` command, exercising the new check's `Control1`/`Control2` handling as
well as its `EndPoint` handling) combined with a `scale(1000000)` transform produces a final
magnitude of `10,000,000`, far past the bound; `RenderShape` now re-checks every point of the
transformed path immediately after `TransformPath` and, on failure, tolerantly skips the whole
shape - proven by the sampled pixel (which the scaled square's huge bounding box would otherwise
cover if the shape were not skipped) staying unfilled, with `Load` not throwing.

A second, independent gap exists for stroke width: `RenderStroke` scales the already-validated,
locally-finite `stroke-width` by the same transform's estimated uniform scale, and its pre-existing
guard (`!float.IsFinite(strokeWidth) || strokeWidth <= 0f`) only catches an overflow-to-`Infinity`
composed scale (see `SvgCodec_Load_NestedTransformScaleOverflowsStrokeWidthToInfinity_SkipsStrokeWithoutThrowing`
in "Non-Finite Numeric Attribute/Token Rejection or Tolerant Fallback" above) - it says nothing
about a finite-but-extreme effective width.
`SvgCodec_Load_StrokeWidthScaledPastMagnitudeBound_SkipsStrokeWithoutThrowing` proves this second
fix: a tiny, near-origin rect (so its own transformed vertex coordinates, up to `90`, stay
comfortably under `MaxCoordinateMagnitude`, meaning the shape's coordinate-magnitude check above
passes and the fill still renders) combined with a small, compliant `stroke-width` of `2` and a
`scale(900000)` transform yields an effective width of `1,800,000` - finite, and therefore
invisible to the pre-existing guard, but still far past the bound. `RenderStroke`'s guard is
extended to also reject when the post-transform-scaled stroke width exceeds
`MaxCoordinateMagnitude`; the test proves the fill still renders (its own geometry stays in
bounds) while the stroke is skipped, by sampling the canvas's far corner - a location an
oversized, `1,800,000`-unit-wide stroke outline (if not skipped) would engulf, but which stays
unfilled once the stroke is tolerantly skipped instead.

Both fixes reuse the tolerant-skip convention already established by
`BuildRectPath`/`BuildEllipsePath`'s arc-conversion-overflow handling (skip the affected
shape/stroke, not the whole document) rather than the parse-time check's hard
`InvalidDataException` rejection, since a huge final magnitude here arises from perfectly valid,
independently-in-bound inputs composing to a multiplied extreme rather than from a single
malformed literal.

#### Post-Stroke Miter-Join Magnitude Bound

**Tests**: `SvgCodec_Load_ExtremeMiterLimitWithSpikeVertexSynthesizesOversizedMiterPoint_SkipsStrokeWithoutThrowing`,
`SvgCodec_Load_NormalMiterJoinWithDefaultMiterLimit_RendersNormally`

A regression test for a local-code-review finding that a third, independent gap survives the
"Post-Transform Coordinate/Stroke-Width Magnitude Bound" fix above: even when `pixelPath`'s
coordinates and the effective `strokeWidth` are both individually in-bound, `stroke-miterlimit`
(see `ParseValidMiterLimit`) is validated only against `Drawing.StrokeStyle`'s documented
lower-bound contract ("finite and at least `1`"), never against any upper bound.
`Drawing.StrokeOutliner`'s miter-join synthesis (`TryCreateMiter`) only rejects a candidate miter
point whose ratio to half the stroke width exceeds `StrokeStyle.MiterLimit` - a check about the
point's ratio to the stroke width, not about the point's own absolute magnitude. An
in-bound-but-large `strokeWidth`, composed with an in-bound-but-extreme `stroke-miterlimit` and a
near-straight/near-reversed "spike" vertex (an interior angle only a fraction of a degree from a
full reversal), can therefore still synthesize a miter point many orders of magnitude beyond
`MaxCoordinateMagnitude`, even though every individual literal involved - each `pixelPath`
coordinate, `strokeWidth`, and the miterlimit - independently passed its own check. Unlike the two
findings above, this one does not currently cause a hang, crash, or exception on its own (the
rasterizer's clip-bounds intersection with the canvas absorbs the resulting oversized fill
harmlessly), so it is a defense-in-depth/contract-completeness fix rather than an urgent one.

`SvgCodec_Load_ExtremeMiterLimitWithSpikeVertexSynthesizesOversizedMiterPoint_SkipsStrokeWithoutThrowing`
proves the fix: a three-point path (`(10,50)`, `(50,50)`, `(10.0000002,50.0034907)`) forms a
needle-thin spike whose interior angle is only ~`0.005` degrees from a full reversal, combined
with an in-bound `stroke-width` of `900000` and an in-bound `stroke-miterlimit` of `1e12`. The
resulting miter ratio (~`22,900`) stays comfortably under the extreme miterlimit, so
`TryCreateMiter`'s own ratio check does not reject it, but the synthesized point's distance from
the vertex (~`1.03e10`) is many orders of magnitude past `MaxCoordinateMagnitude`. Because the
900,000-unit stroke width alone already dwarfs the 100x100 canvas, an un-skipped stroke would
engulf the entire canvas in solid color; the test proves the whole stroke - not just the spike
vertex - was tolerantly skipped by sampling several canvas locations (including the canvas center
and far corner) and finding none of them filled. `RenderStroke` now re-checks the actual outline
`PathStroker.Stroke` synthesizes - not a guessed upper bound on `stroke-miterlimit` itself -
against `MaxCoordinateMagnitude` immediately after stroking, reusing the same
`IsWithinCoordinateMagnitudeBudget` helper `RenderShape` already uses for its own post-transform
check above (since `PathStroker.Stroke` only ever emits `LineTo` commands into its returned
outline, that helper directly applies without modification), and tolerantly skips the whole
stroke on failure, the same way an oversized post-transform stroke width already is above.

`SvgCodec_Load_NormalMiterJoinWithDefaultMiterLimit_RendersNormally` proves the new check has no
effect on an ordinary, legitimate miter join: a simple 90-degree corner with a small `stroke-width`
of `6` and the SVG spec's own default `stroke-miterlimit` of `4` (a miter ratio of `~1.41`, and a
synthesized miter point well within `MaxCoordinateMagnitude`) continues to render exactly as
before, both at the corner's miter tip and along each straight segment.

#### Gradient Stop Caching

**Tests**:

- `SvgCodec_Load_GradientReferencedByManyShapes_CachesStopsAndRendersIdenticallyToUncached`
- `SvgCodec_ResolveGradientStops_SameGradientElementResolvedTwice_ReturnsCachedListInstance`

Regression test for the repeated-work-without-caching amplification finding: `BuildGradient` (via
`ResolveGradientStops`) previously re-parsed a gradient element's `stop` children from scratch on
every single shape (or `use`-fan-out-multiplied shape reference) that referenced the same
`linearGradient`/`radialGradient`, even though a gradient's own stops are immutable for the
lifetime of one `Load` call (confirmed by direct inspection: nothing in this codec mutates the
parsed `XDocument` mid-render, and no scripting/animation support exists to redefine a gradient's
stops mid-document). `ResolveGradientStops`'s pre-alpha result is now cached per gradient element
(keyed by the gradient `XElement`'s own reference identity) in a new `RenderContext.GradientStopCache`
field, populated once and reused across every subsequent reference to the same gradient, mirroring
the existing `IdIndex` field's identical "populated once, read many times" lifetime.
`SvgCodec_Load_GradientReferencedByManyShapes_CachesStopsAndRendersIdenticallyToUncached` proves a
gradient referenced by many shapes still renders every shape identically to the pre-fix (uncached)
behavior, confirming the cache introduces no observable rendering change.

That pixel-rendering test alone cannot distinguish cached-and-correct output from
always-re-parsed-and-still-correct output, since both produce identical pixels - it would still
pass even if the caching fix were fully reverted. `SvgCodec_ResolveGradientStops_SameGradientElementResolvedTwice_ReturnsCachedListInstance`
closes that gap by invoking `ResolveGradientStops` directly (via reflection, the same idiom
already used by e.g. `CmapTable_Format4_GlyphIndexAddressArithmeticOverflow_ReturnsZero`) and
asserting the second call for the same gradient element returns the identical cached
`List<GradientStop>` instance rather than a freshly re-parsed one.

### Acceptance Criteria

A unit test run passes when every test method listed above, across both `SvgCodecTests.cs` and
`SvgFixtureTests.cs`, passes without error or unexpected exception; any unexpected exception type
or wrong pixel/return value constitutes a failure.
