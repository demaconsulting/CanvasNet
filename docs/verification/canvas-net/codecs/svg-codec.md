## SvgCodec Unit Verification Design

<!-- cspell:ignore unstroked Letterboxing uncatchable Glyf Loca unparseable Cmap -->
<!-- cspell:ignore unitless -->

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
`SvgCodec_Load_MarkerOrientOmittedOnDiagonalLine_UsesFixedZeroDegreeDefaultNotTangent`,
`SvgCodec_Load_MarkerStartOrientAuto_PointsIntoLine`,
`SvgCodec_Load_MarkerStartOrientAutoStartReverse_PointsAwayFromLine`,
`SvgCodec_Load_MarkerUnitsUserSpaceOnUseVsStrokeWidthDefault_ScalesDifferently`,
`SvgCodec_Load_MarkerWithViewBox_FitsContentToMarkerWidthHeight`,
`SvgCodec_Load_MarkerOnMultiSubpathPath_AppliesStartEndOnlyAtWholePathEnds`,
`SvgCodec_Load_MarkerOnRectCircleEllipse_NeverRendersMarker`,
`SvgCodec_Load_MarkerContent_DoesNotInheritReferencingShapeFillOrStroke`,
`SvgCodec_Load_ArrowMarkersFixture_RendersArrowheadPastLineEnd`,
`SvgCodec_Load_MarkerEndOnClosedPolygon_OrientsUsingClosingEdgeTangent`,
`SvgCodec_Load_MarkerStrokeWidthUnitsOnScaledDocument_ScalesOnceNotTwice`,
`SvgCodec_Load_MarkerContentWithFilterAttribute_FilterHasNoEffect`,
`SvgCodec_Load_MarkerWithExplicitPreserveAspectRatioSlice_UsesLargerUniformScale`

Asserts a `marker-end` reference renders its `marker` element's content past a `line`'s own end
point, sized in user-space units; asserts `marker-start`/`marker-mid`/`marker-end` each
independently resolve and render their own distinct marker at the correct vertex of a
multi-vertex `polyline`, with each marker's own reference point (`refX`/`refY`) landing exactly on
its vertex regardless of rotation; asserts `orient="auto"` orients a marker along a horizontal,
vertical, and 45-degree diagonal segment's own direction of travel (the diagonal case uses an
alpha threshold rather than exact full opacity, tolerating this rasterizer's edge anti-aliasing on
a thin, diagonally rotated shape); asserts an omitted `orient` attribute uses the fixed 0-degree
default rather than following the vertex tangent like an explicit `orient="auto"` (a regression
test for a defect where an absent/blank `orient` was incorrectly routed into the same tangent-
following branch as an explicit `auto`); asserts a plain `orient="auto"` `marker-start` points into the
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
`marker-end` arrowhead scenario end-to-end. Asserts a closed `polygon`'s implicit closing segment
(back to its first vertex) contributes to the `orient="auto"` tangent computed for the last
vertex's `marker-end` - averaged with the incoming open-path edge's own tangent - rather than the
closing edge being silently ignored (a regression test for a defect where a closed shape's final
vertex was oriented using only its incoming edge). Asserts a default `markerUnits="strokeWidth"`
marker on a document with a non-identity root `viewBox` fit (a uniform 2x scale from local units
to pixels) scales by exactly `stroke-width * root-scale` once, not twice (a regression test for a
defect where the already-pixel-scaled effective stroke width was passed into the marker's own
`strokeWidth`-units sizing, and then the root scale was re-applied a second time via the shared
`shapeTransform`, making the marker four times too large instead of two times). Asserts a shape
inside a `marker` element's own content with a `filter="url(#id)"` attribute referencing a real
(`feFlood`) filter renders exactly as if that `filter` attribute were absent - the marker's own
fill color, not the flood's - a regression test for a defect where marker-content rendering
re-entered the same shape/text dispatch used everywhere else, which unconditionally resolved and
evaluated a shape's own `filter` attribute, contradicting this codec's documented "filters on
marker content have no effect" scope decision.
`SvgCodec_Load_MarkerWithExplicitPreserveAspectRatioSlice_UsesLargerUniformScale` is a new-
capability test proving an explicit `preserveAspectRatio="... slice"` attribute on a marker with
its own `viewBox` selects `slice`'s larger uniform scale (`max(...)`) rather than the pre-existing
default path's `meet`-equivalent (`min(...)`) scale, exercised via a canvas point covered only by
the wider `slice` fit. It deliberately does not exercise `<align>`/origin variation: because a
marker's content is always anchored by its own `refX`/`refY` (never clipped to
`markerWidth`/`markerHeight`), an `<align>`'s `Min`/`Mid`/`Max` offset and the viewBox's own
origin both algebraically cancel out of the final rendered position once that ref-anchoring is
applied, regardless of their value - only the uniform scale factor itself is ever visibly
different between the default (no attribute) path and an explicit `preserveAspectRatio`, making
`meet` versus `slice` the only genuinely pixel-distinguishable part of this new capability given
this codec's marker content is never clipped.

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

#### CanvasNet-Codecs-SvgCodec-FontWeightStyleCascade: Font-Weight/Font-Style Attribute Parsing and Cascade

**Tests**: `SvgCodec_Load_GroupFontWeightInheritance_AppliesToChildTextWithoutOwnFontWeight`,
`SvgCodec_Load_GroupFontStyleInheritance_AppliesToChildTextWithoutOwnFontStyle`

Uses two new synthetic test fonts (`BuildBoldTestFont`, an 80-wide square glyph; and
`BuildItalicTestFont`, a 50-wide square glyph shifted right by 20 units), each distinguishable
from `BuildTestFont`'s original 50-wide square by which canvas pixels are/are not filled, so a
test can prove *which* registered face actually rendered. Asserts a `g` element's own
`font-weight="bold"` cascades to a child `text` element with no own `font-weight` (the child
selects the bold face), and likewise for a `g` element's own `font-style="italic"` cascading to a
child `text` element with no own `font-style` (the child selects the italic face) - mirroring
`SvgCodec_Load_GroupFillInheritance_AppliesToChildWithoutOwnFill`'s inheritance pattern for
`font-family`/`font-size`'s other presentation-attribute siblings.

#### CanvasNet-Codecs-SvgCodec-FontFaceMatching: Closest-Face Selection Algorithm

**Tests**: `SvgCodec_Load_TextFontWeightBold_SelectsBoldFaceOverNormalFace`,
`SvgCodec_Load_TextFontStyleItalic_SelectsItalicFaceOverNormalFace`,
`SvgCodec_Load_TextFontWeightNumeric_SelectsClosestRegisteredFaceByDistance`,
`SvgCodec_Load_TextFontWeightTie_PrefersMatchingBoldnessSide`,
`SvgCodec_Load_TextFontStyleNoItalicRegistered_FallsBackToOnlyAvailableFace`,
`SvgCodec_Load_TextFontWeightAndStyleCombined_SelectsExactMatchAmongThreeFaces`,
`SvgCodec_Load_TextFontWeightExtremeValue_DoesNotOverflowAndSelectsClosestFace`

Exercises every branch of `SelectClosestFace`'s three-part priority order using the
`LoadWithFontFaces(..., IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>?)` overload: a two-face family
(400/Normal narrow, 700/Normal wide) selects the bold face for `font-weight="bold"` and the
normal face for a sibling with no own `font-weight`; a two-face family (400/Normal narrow,
400/Italic shifted) selects the italic face for `font-style="italic"`; faces registered at 400
and 900 with a requested weight of 600 select the closer 400 face (distance 200 versus 300); faces
registered at 300 and 500 with a requested weight of 400 (an equidistant tie) select the 500 face,
proving the boldness-side tie-break; a family with only a 400/Normal face still renders it when
`font-style="italic"` is requested (graceful fallback, not silent skipping); and three faces
(400/Normal, 700/Normal, 700/Italic) with a combined `font-weight="bold" font-style="italic"`
request select the 700/Italic face specifically - proven by asserting both a pixel unique to the
italic face's glyph (proving it is not the normal face) and a pixel unique to the wide/bold face's
glyph is *not* filled (proving it is not merely the weight-matched bold/normal face). Asserts an
extreme, out-of-range `font-weight="-2147483648"` request (still parsed, not rejected, by
`ParseFontWeight`) does not throw an `OverflowException` from `SelectClosestFace`'s
weight-distance computation and still deterministically selects the closer-by-magnitude
registered face - a regression test for a defect where the weight-distance computation used `int`
arithmetic, which can overflow for such extreme caller-supplied weight values.

#### CanvasNet-Codecs-SvgCodec-LegacySingleFontCompatibility: Legacy Single-Font-Per-Family Overload Regression

**Tests**: `SvgCodec_Load_TextLegacySingleFontOverload_IgnoresRequestedWeightAndStyle`,
`SvgCodec_Load_ExplicitNullFontsLiteral_CompilesUnambiguouslyAndFallsBackToBuiltInFont`

Proves the pre-existing `Load(..., IReadOnlyDictionary<string, TrueTypeFont>?)` overload's
behavior is unchanged by the additive `SvgFontFace`-based overload: with only one plain
`TrueTypeFont` registered for a family, a `text` element requesting both `font-weight="bold"` and
`font-style="italic"` still renders that single registered font at its known pixel position,
proving the legacy overload's internal `ToFontFaces` wrapping (one normal-weight/normal-style
face per family) makes weight/style irrelevant to face selection, exactly as before this feature
existed. Also proves a regression for a source-breaking ambiguous-overload defect: prior to the
richer, `SvgFontFace`-based overload being renamed to `LoadWithFontFaces`, a caller passing an
explicit `null` literal (rather than omitting the argument) to `SvgCodec.Load(stream, width,
height, null)` failed to compile with "the call is ambiguous", because `null` matched both the
legacy `IReadOnlyDictionary<string, TrueTypeFont>?` overload and the richer overload equally
well; now that the richer overload has its own distinct name, this call is unambiguous and falls
back to this legacy overload's own documented "no font registered" behavior.

#### CanvasNet-Codecs-SvgCodec-ViewBoxFitting: ViewBox `preserveAspectRatio` Fitting

**Tests**: `SvgCodec_Load_WideViewBoxIntoSquareRaster_LetterboxesTopAndBottom`,
`SvgCodec_Load_TallViewBoxIntoSquareRaster_LetterboxesLeftAndRight`,
`SvgCodec_Load_RootPreserveAspectRatio_AppliesAlignAndMeetOrSlice`,
`SvgCodec_Load_RootPreserveAspectRatioNone_StretchesNonUniformly`,
`SvgCodec_Load_NoPreserveAspectRatioAttribute_MatchesExplicitXMidYMidMeet`

Asserts a wide (landscape) viewBox fit into a square raster is centered with transparent
letterbox bars above and below its content, and a tall (portrait) viewBox fit into a square
raster is centered with transparent letterbox bars to the left and right, in each case also
asserting the content band itself is filled - these two pre-existing tests continue to prove the
default (no explicit `preserveAspectRatio` attribute) "xMidYMid meet" fit is unchanged.
`SvgCodec_Load_RootPreserveAspectRatio_AppliesAlignAndMeetOrSlice` is a data-driven `[Theory]`
covering all 9 non-`none` `<align>` values combined with both `meet` and `slice` (18 rows total),
rendering a single 200x100 viewBox with 4 marker stripes into a mismatched 100x200 raster and
sampling pixels whose expected color was independently hand-verified via matrix composition, to
prove every align/meetOrSlice combination is honored.
`SvgCodec_Load_RootPreserveAspectRatioNone_StretchesNonUniformly` proves the new `align="none"`
capability stretches the intrinsic viewBox independently on each axis with no uniform-scale
constraint and no letterboxing, filling the raster completely.
`SvgCodec_Load_NoPreserveAspectRatioAttribute_MatchesExplicitXMidYMidMeet` is a dedicated,
explicitly-named regression test (not merely relying on other tests happening to still pass)
that loops over every pixel of a document with no `preserveAspectRatio` attribute at all and
asserts it is pixel-for-pixel identical to the same document with an explicit
`preserveAspectRatio="xMidYMid meet"` attribute added - proving Finding #1 from this phase's
planning report (the pre-existing default was already spec-conformant "meet, centered", not the
non-uniform stretch a naive reading of the original feature request might assume) holds, and that
adding `preserveAspectRatio` parsing introduced no default-behavior regression.

#### CanvasNet-Codecs-SvgCodec-SymbolViewBoxFitting: `symbol`/`use` ViewBox Fitting

**Tests**: `SvgCodec_Load_UseReferencingSymbolWithViewBox_FitsContentToResolvedWidthHeight`,
`SvgCodec_Load_UseReferencingSymbolWithPreserveAspectRatio_HonorsAlign`

Asserts a `use` element referencing a `symbol` element with its own `viewBox` fits that viewBox's
content into the `use`/`symbol` element's resolved `width`/`height`, positioned by the `use`
element's own `x`/`y` translation applied in the outer, already-fitted coordinate space.
`SvgCodec_Load_UseReferencingSymbolWithPreserveAspectRatio_HonorsAlign` further asserts a
`symbol`'s own `preserveAspectRatio` attribute (a portrait viewBox fitted into a square box,
`xMinYMin` align) is honored, positioning content flush to the top-left rather than centered, and
that the slack area a centered default would otherwise fill is left transparent - unlike a
marker's ref-anchored content (see `CanvasNet-Codecs-SvgCodec-MarkerRendering` below), a
`symbol`/`use`'s align offset is a plain, non-anchored translation, so it remains visibly
distinguishable in rendered output.

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

#### CanvasNet-Codecs-SvgCodec-PercentageGeometryResolved: Percentage Geometry Resolved Against Viewport

**Tests**: `SvgCodec_Load_RectXPercentage_ResolvesAgainstViewportWidth`,
`SvgCodec_Load_RectWidthPercentage_ResolvesAgainstViewportWidth`,
`SvgCodec_Load_RectYHeightPercentage_ResolvesAgainstViewportHeight`,
`SvgCodec_Load_CircleCxCyRPercentage_ResolvesAgainstHorizontalVerticalAndDiagonalBases`,
`SvgCodec_Load_LineCoordinatePercentage_ResolvesAgainstViewport`,
`SvgCodec_Load_GradientCoordinatePercentage_UnaffectedByViewportPercentageResolution`,
`SvgCodec_Load_StrokeWidthPercentage_ResolvesAgainstDiagonalBasis`,
`SvgCodec_Load_FontSizePercentage_ResolvesAgainstParentFontSize`,
`SvgCodec_Load_StrokeDasharrayPercentage_ResolvesAgainstDiagonalBasis`

Asserts a `rect`'s `x`/`width` percentages resolve against the current viewport's width (
`SvgCodec_Load_RectXPercentage_ResolvesAgainstViewportWidth`/
`SvgCodec_Load_RectWidthPercentage_ResolvesAgainstViewportWidth` - repurposed, rather than
silently deleted, from this codec's original percentage-*rejection* `Fact`s covering this exact
attribute/markup, now proving successful resolution instead of `InvalidDataException`), and its
`y`/`height` percentages resolve against the current viewport's height on a non-square viewBox
(`SvgCodec_Load_RectYHeightPercentage_ResolvesAgainstViewportHeight`). Asserts a `circle`'s
`cx`/`cy` percentages resolve against the horizontal/vertical bases respectively, and its `r`
percentage resolves against the diagonal basis `sqrt(w^2 + h^2) / sqrt(2)` - an axis-agnostic
length per the SVG specification
(`SvgCodec_Load_CircleCxCyRPercentage_ResolvesAgainstHorizontalVerticalAndDiagonalBases`). Asserts
a `line`'s `x1`/`y1`/`x2`/`y2` percentages resolve against the viewport
(`SvgCodec_Load_LineCoordinatePercentage_ResolvesAgainstViewport`), and separately, that a
`linearGradient`'s own `x1`/`x2` coordinates continue to resolve as basis-1 fractions of the
gradient's own `objectBoundingBox` coordinate space, unaffected by this phase's new viewport-
relative percentage resolution
(`SvgCodec_Load_GradientCoordinatePercentage_UnaffectedByViewportPercentageResolution` - uses a
`rect` fill rather than a `line` stroke, since a horizontal/vertical `line`'s own fill-geometry
bounding box is degenerate on one axis and always falls back to an identity object-bounding-box
map regardless of this change). Asserts `stroke-width`'s and each `stroke-dasharray` entry's
percentage resolves against the diagonal basis, exactly like `r` above
(`SvgCodec_Load_StrokeWidthPercentage_ResolvesAgainstDiagonalBasis`,
`SvgCodec_Load_StrokeDasharrayPercentage_ResolvesAgainstDiagonalBasis`). Asserts `font-size`'s
percentage resolves against the parent element's own already-cascaded `font-size` (the CSS/SVG-
defined basis), not any viewport dimension
(`SvgCodec_Load_FontSizePercentage_ResolvesAgainstParentFontSize`). Percentage resolution happens
inside the existing `ParseGeometryCoordinate`/`ParseCoordinate` choke point, so the existing
finite/magnitude guards those methods already apply still run *after* percentage resolution
rather than being bypassed - `stroke-miterlimit` remains the sole documented exception (a
unitless ratio, not a length), continuing to tolerantly fall back to the inherited value for a
percentage there, unchanged. Separately confirms (no dedicated regression test needed, since both
already exist and are unaffected) that
`SvgCodec_Load_NegativeScientificAndPercentageValues_RendersWithoutThrowing` (which only exercises
`opacity="50%"`) and `SvgCodec_Load_GradientStopOffsetPercentage_RendersGradientCorrectly` (which
exercises gradient `stop` `offset` percentages) continue to pass unmodified, proving
opacity-family attributes and gradient `stop` `offset` remain correctly unaffected by this
capability.

#### CanvasNet-Codecs-SvgCodec-UnsupportedConstructsIgnored: Out-of-Scope Constructs Tolerated

**Tests**: `SvgCodec_Load_UnsupportedConstructs_StillRendersRestOfDocument`,
`SvgCodec_Load_ToleratesUnsupportedConstructFixture_StillRendersRemainingContent`

Builds a document containing `style`, `mask`, `clipPath`, `pattern`, and a
nested `svg` alongside an ordinary `rect`, and asserts the ordinary `rect` still renders — proving
none of the out-of-scope elements abort the whole document. `mask`/`clipPath` are defined but
never referenced by any element's own `mask`/`clip-path` attribute in this particular fixture, so
this test only exercises their (unchanged) "non-rendering element, tolerated as a defs-only
declaration" status - see `CanvasNet-Codecs-SvgCodec-ClipPathRendering`/
`CanvasNet-Codecs-SvgCodec-MaskRendering` below for their own dedicated coverage once actually
referenced; like `filter`, they are no longer an out-of-scope construct when referenced. A real
fixture file
(`SvgFixtures/tolerant-unsupported.svg`, whose `filter` def is now genuinely supported but simply
never referenced by any element) exercises the same property end-to-end. See
`CanvasNet-Codecs-SvgCodec-FilterRendering`/`CanvasNet-Codecs-SvgCodec-FilterResourceSafety` below
for `filter`'s own dedicated coverage - it is no longer an out-of-scope construct.

#### CanvasNet-Codecs-SvgCodec-FilterRendering: Filter Primitive Chain Evaluation and Region Computation

**Tests**: `SvgCodec_Load_FeFloodFilter_RendersSolidColorBehindElement`,
`SvgCodec_Load_FeFloodFeMergeFilter_RendersFloodBehindSourceGraphic`,
`SvgCodec_Load_FeFloodFeGaussianBlurFeCompositeFilter_RendersBlurredHaloBehindContent`,
`SvgCodec_Load_FilterUnsupportedPrimitive_PassesThroughSourceGraphicUnchanged`,
`SvgCodec_Load_FilterDefaultRegion_ExpandsBoundingBoxByTenAndTwentyPercent`,
`SvgCodec_Load_FilterExplicitRegion_UsesDeclaredXYWidthHeight`,
`SvgCodec_Load_FilterUserSpaceOnUse_FallsBackToObjectBoundingBoxDefault`,
`SvgCodec_Load_FeOffsetFilter_ShiftsSourceGraphicByDxDy`,
`SvgCodec_Load_FeOffsetFilterWithHalfPixelDx_RoundsAwayFromZeroNotToEven`,
`SvgCodec_Load_LabelHaloFixture_RendersWhiteHaloBehindLineMidpointLabel`,
`SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent`,
`SvgCodec_Load_FeCompositeOperatorIn_KeepsForegroundWeightedByBackgroundAlpha`,
`SvgCodec_Load_FeCompositeOperatorAtop_BlendsBothInputsWeightedByBothAlphas`,
`SvgCodec_Load_FeCompositeOperatorXor_KeepsEachInputWhereTheOtherHasNoCoverage`,
`SvgCodec_Load_FeCompositeOperatorXorWithHalfChannelValue_RoundsAwayFromZeroNotToEven`,
`SvgCodec_Load_FilterOnStrokedHorizontalLine_UsesStrokeAwareBoundsNotDegenerate`,
`SvgCodec_Load_OpacityWithFeFloodFilter_AppliesOpacityToFilteredResultNotSource`

Asserts a bare `feFlood` primitive's flood color entirely replaces the referencing element's own
content, filling the (default, bounding-box-relative) filter region including the area behind the
element's own shape, since a lone `feFlood` never references `SourceGraphic`; asserts `feMerge`
layers a `feFlood` result behind a `SourceGraphic` layer in document order, so the flood is visible
outside the element's own bounds while the element's own fill remains visible on top at its own
location; asserts a conventional `feFlood` → `feComposite(operator="out")` → `feGaussianBlur` →
`feMerge` halo/glow recipe produces a genuinely softened (partially transparent, not hard-edged)
flood color just outside the element's own edge - proving the blur actually ran - while the
element's own fill remains fully opaque and unaffected at its center; asserts a filter primitive
type this codec does not implement (`feColorMatrix`) is treated as a no-op passthrough of its own
input rather than throwing or blanking the element's content; asserts the filter region's default
computation expands the referencing element's own bounding box by exactly -10%/-10%/120%/120%,
verified at pixels just inside and just outside each computed edge; asserts an explicit `filter`
`x`/`y`/`width`/`height` overrides the default region computation; asserts
`filterUnits="userSpaceOnUse"` tolerantly falls back to the same objectBoundingBox-relative region
computation as the default, rather than being interpreted as literal absolute user-space
coordinates; and asserts `feOffset` shifts its input by `dx`/`dy` prior to compositing back onto
the canvas. Two real fixture files corroborate this end to end:
`SvgFixtures/label-halo.svg` mirrors the reported real-world bug (a `filter="url(#label-bg)"`
white halo behind a line's own midpoint label, previously invisible while `filter` was ignored),
and the large, real, third-party `SvgFixtures/InkscapeFilters.svg` (see
`CanvasNet-Codecs-SvgCodec-UnsupportedConstructsIgnored`'s predecessor coverage of this same
fixture) proves this genuinely-evaluated filter-chain support, including its tolerant-passthrough
handling of several unsupported primitives mixed into the same chains
(`feSpecularLighting`/`feDiffuseLighting`/arithmetic-mode `feComposite`), holds at real-world scale
and complexity, not only for small synthetic documents. `feComposite`'s `in`, `atop`, and `xor`
Porter-Duff operators (`over`/`out` were already covered above) are each additionally verified
deterministically and synthetically, against two full-region, semi-transparent `feFlood` inputs
that isolate the operator's own per-pixel formula from any shape-geometry/filter-region overlap
concern: `in` keeps the "in" input's own color weighted by the "in2" input's own alpha; `atop`
blends both inputs' own colors weighted by (`in2`'s alpha, `1 - in`'s alpha); and `xor` keeps each
input only where the other has no coverage - each asserting the exact expected pixel value the
documented formula produces. `feOffset`'s own `dx`/`dy`-to-pixel conversion and
`feComposite`'s own per-channel output are each additionally verified to round
away-from-zero (matching `Surface.CompositeOverSpanCore`'s established convention), not the
`MathF.Round` default of banker's/round-to-even rounding: a `dx` that scales to exactly 0.5
pixels rounds up to a full 1-pixel shift, and an `xor` composite chosen to produce a computed
channel value of exactly 126.5 rounds up to 127, not down to 126. Asserts a horizontal, stroked
`line` with a `feFlood` filter is
evaluated (not skipped as degenerate) despite its zero-height centerline/fill bounds - a
regression test for a defect where the filter-region degeneracy guard used the bare
centerline/fill bounds instead of the actually-painted (stroke-expanded) bounds, incorrectly
treating a valid stroked line's filter as degenerate and silently falling back to unfiltered
rendering. Asserts a `rect` with `opacity="0.5"` and a filter whose entire chain is a single,
fully-opaque `feFlood` renders the flood at approximately 50% alpha, not fully opaque - a
regression test for a defect where `opacity` was applied while painting the pre-filter
`SourceGraphic` (so the filter chain, and the final compositing step, both then operated on
already-attenuated content with no further opacity ever applied) instead of being applied exactly
once, afterward, to the filter's own final output, per SVG's "opacity applies to the filtered
result as a whole" semantics.

#### CanvasNet-Codecs-SvgCodec-FilterResourceSafety: Filter Dangling Reference and Resource-Bound Tolerance

**Tests**: `SvgCodec_Load_FilterDanglingReference_RendersElementNormally`,
`SvgCodec_Load_FilterPathologicallyLargeRegion_SkipsFilterRatherThanUnboundedAllocation`,
`SvgCodec_Load_FilterWithZeroPrimitivesAndHugeRegion_SkipsFilterRatherThanAllocatingSurface`,
`SvgCodec_Load_FilterPathologicallyLargeBlurStdDeviation_ClampsRatherThanUnboundedWork`,
`SvgCodec_Load_FilterExcessivePrimitiveCount_SkipsFilterRatherThanUnboundedWork`,
`SvgCodec_Load_FeMergeExcessiveNodeCount_SkipsFilterRatherThanUnboundedWork`,
`SvgCodec_Load_FilterReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessShapes`

Asserts a `filter="url(#id)"` reference to a nonexistent id renders the element normally, exactly
as if no `filter` attribute were present, matching this codec's general dangling-reference
convention (`ResolvePaint`/`ResolveMarkerElement`); asserts a filter region large enough to require
an unreasonably large temporary surface (`width`/`height` of `100000%`) is tolerantly skipped
entirely, rather than attempting an allocation exceeding `Surface.MaxDimension`, with the element
still rendering its own normal content; asserts a `filter` element with zero primitive children,
paired with that same pathologically large region, is likewise tolerantly skipped before any
temporary surface is allocated - a regression test for a defect where a zero primitive/work-unit
count trivially passed the upfront work-budget check, letting the code allocate a potentially
enormous `SourceGraphic` surface for a filter that, having no primitives, could never change the
rendered output; asserts an `feGaussianBlur` `stdDeviation` many orders
of magnitude larger than the fixed `MaxFilterBlurStdDeviationPixels` bound is clamped rather than
causing unbounded work - the box-blur implementation's own cost does not scale with the requested
radius, so this completes promptly and produces a heavily diluted (rather than crashing or
hanging) result; and asserts a filter chain with an excessive number of primitives (5,000 chained
`feGaussianBlur` primitives, mirroring a reported repro that took ~26 seconds prior to this bound)
is tolerantly skipped entirely rather than evaluated, completing promptly instead of performing
5,000 region-sized blur passes - bounding the primitive-count × region-area cost dimension that
`MaxTotalRenderedElements`/`GeometryWorkBudget` do not cover, since neither tracks a single
filter's own per-primitive, region-sized buffer cost. Also asserts a `feMerge` primitive with an
excessive number (5,000) of `feMergeNode` children is likewise tolerantly skipped, rather than
bypassing the primitive-count work budget - a regression test for a defect where the budget check
counted a `feMerge` as a single primitive regardless of its own `feMergeNode` child count, even
though `ApplyFeMerge` performs one full-surface composite per child, letting a pathological
`feMerge` node count bypass the budget while still performing unbounded work. Finally, asserts a
regression test for a code-review finding that the per-filter `MaxFilterPrimitiveWorkUnits`
ceiling is enforced independently for every individual filtered element, so a single `filter`
definition referenced by many shapes could be charged that same ceiling once per reference with no
bound on the total number of references: 13 shapes reference one filter chain that individually
charges exactly the per-filter ceiling (500 primitives against a 100×100 region) every time - the
first 10 references' cumulative total stays within the new `FilterWorkBudget`'s
`MaxCumulativeFilterWorkUnits` ceiling (50,000,000) and are genuinely filtered (rendering the
chain's trailing `feFlood` solid color), while the 11th through 13th references would push the
cumulative total over that ceiling and tolerantly fall back to unfiltered rendering (their own
plain fill color) instead of throwing - proving the new cumulative, per-`Load`-call budget bounds
total filter-evaluation work across the whole document, a dimension neither
`MaxFilterPrimitiveWorkUnits` (per-filter-application only) nor `MaxTotalRenderedElements`
(counts a filtered shape identically to an unfiltered one) previously covered.

#### CanvasNet-Codecs-SvgCodec-GroupFilterRendering: Group-Level Filter Rendering on g/symbol/use

**Tests**: `SvgCodec_Load_GroupFilter_FeFloodOnGWithTwoChildren_FillsCombinedBoundsIncludingGap`,
`SvgCodec_Load_GroupFilter_UseReferencingSymbolWithFilter_FillsCombinedBounds`,
`SvgCodec_Load_GroupFilter_FilterOnUseTargetingPlainG_FillsCombinedBounds`,
`SvgCodec_Load_GroupFilterWithOpacity_AppliesOpacityToFilteredResultNotChildren`,
`SvgCodec_Load_GroupFilterWithTransform_EvaluatesFilterInGroupsLocalSpace`,
`SvgCodec_Load_GroupFilterWithDanglingReference_RendersChildrenNormally`,
`SvgCodec_Load_GroupFilterOnEmptyGroup_RendersNothingWithoutThrowing`,
`SvgCodec_Load_GroupFilterInsideMarkerContent_FilterHasNoEffect`,
`SvgCodec_Load_GroupFilterReferencedByManyUsesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessUses`,
`SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent`,
`SvgCodec_Load_FilteredGroupNearTotalElementBudget_RendersWithoutThrowing`,
`SvgCodec_Load_FilteredGroupWithLineMarkerExtendingBeyondLineBounds_MarkerPixelsSurviveFilter`,
`SvgCodec_Load_GroupFilterWithNestedFilteredChildExpandingBeyondOwnBounds_PreservesFullNestedFilterOutput`,
`SvgCodec_Load_DeeplyNestedFilteredGroupsExceedingCumulativeBoundsPrePassBudget_ThrowsInvalidDataException`,
`SvgCodec_Load_ModestlyNestedFilteredGroups_RenderCorrectlyWithoutFalsePositiveFallback`,
`SvgCodec_Load_OuterGroupFilterWithPathologicallyOversizedDescendantFilter_StillAppliesOuterFilter`,
`SvgCodec_Load_DeeplyNestedFilteredGroupsWithEnormousInnerPath_ThrowsInvalidDataException`,
`SvgCodec_Load_ModestlyNestedFilteredGroupsWithNormalPath_RenderCorrectlyWithoutFalsePositiveFallback`

Asserts a `filter` on a `<g>` wrapping two non-overlapping `rect` elements is evaluated once
against the group's own *combined* subtree bounds, not per-child: a bare `feFlood` fills its
whole region solid, so the gap between the two `rect`s - which neither child's own individual
bounds cover - being filled proves the filter region was computed from the group's combined
bounds. Asserts a
`filter` on a `<use>` element referencing a `<symbol>` renders the resolved subtree offscreen and
filters the combined result the same way, and a further test proves the same holds when the
`<use>`'s referenced target is a plain `<g>` rather than a `<symbol>`, since those two target
kinds are dispatched through separate code paths (`RenderElement`'s `"g"`/`"symbol"` case versus
`RenderUse`). Asserts a group's own `opacity` attenuates the filtered result exactly once, the
same convention already established for single filtered shapes
(`CanvasNet-Codecs-SvgCodec-FilterRendering`'s own
`SvgCodec_Load_OpacityWithFeFloodFilter_AppliesOpacityToFilteredResultNotSource`). Asserts a
group's own `transform` establishes the coordinate space both the filter region and an
`feOffset` primitive's `dx`/`dy` shift are computed in, by reproducing the same on-canvas
geometry as `SvgCodec_Load_FeOffsetFilter_ShiftsSourceGraphicByDxDy`'s single-shape case through a
combination of the group's own `transform` and the child rect's own local position. Asserts a
group `filter` attribute referencing a nonexistent id is tolerated as a silent no-op, mirroring
this codec's general dangling-reference convention, and that an empty, filtered `<g>` (no
children at all, so the bounds pre-pass has nothing to compute a region from) is likewise
tolerated without throwing rather than requiring special-case handling. Asserts a `filter`
attribute on a `<g>` nested *inside* a `<marker>`'s own content continues to have no effect - the
group-level counterpart of
`CanvasNet-Codecs-SvgCodec-FilterRendering`'s predecessor marker-content coverage - since
`marker` content is never recursed into by the ordinary element walk regardless of any nested
`filter` attribute. Asserts the same cumulative, per-`Load`-call `FilterWorkBudget` also caps
group-level filter work: the group-level counterpart of
`SvgCodec_Load_FilterReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessShapes`
above, using 13 `<use>` elements referencing a shared filtered `<g>` target instead of 13
directly-filtered `rect` elements, with the same first-10-filtered/last-3-fallback split. Finally,
the large, real, third-party `SvgFixtures/InkscapeFilters.svg` fixture (already exercised by
`CanvasNet-Codecs-SvgCodec-FilterRendering` above) corroborates group-level filtering at real-world
scale: its flower template places most flower instances via `<use filter="url(#...)"
xlink:href="#b"/>`, where `#b` is itself a `<g>` containing several petal `<use>` references - a
filter on the outermost `<use>` therefore now genuinely evaluates against the whole resolved
flower subtree, rather than having no effect. The fixture test asserts a genuinely altered pixel
color at a previously-asserted interior pixel (proving the filter chain, including its
`feGaussianBlur`/`feSpecularLighting` steps, now actually runs), and asserts a "blur bleed"
pixel pair - one offset position relative to a filtered flower shows nonzero alpha from blur
spread, while the identical offset relative to the one unfiltered flower remains exactly zero -
directly proving the filter is genuinely evaluated rather than merely tolerated without error.

Two further regression tests close a code-review finding pair in this same bounds pre-pass
(`ComputeSubtreeLocalBounds`). First,
`SvgCodec_Load_FilteredGroupNearTotalElementBudget_RendersWithoutThrowing` proves the pre-pass no
longer double-charges the real, per-`Load`-call `MaxTotalRenderedElements` counter/
`GeometryWorkBudget`: a filtered `<g>` containing 60,000 elements - comfortably under the fixed
100,000-element ceiling for a single charge, but enough to have exceeded it under the old
double-charge (once from the bounds pre-pass, once again from the real render pass that follows
it) - now renders successfully (its `feFlood` fills the whole canvas red) instead of throwing
`InvalidDataException`, matching what an equivalent *unfiltered* document of the same size would
have done all along. Second,
`SvgCodec_Load_FilteredGroupWithLineMarkerExtendingBeyondLineBounds_MarkerPixelsSurviveFilter`
proves the pre-pass now folds a `line`'s own placed marker content into the group's combined
bounds: a 20x20 arrowhead-style marker centered on the line's end vertex extends far beyond the
line's own tiny stroke-expanded outline, and a point deep inside the marker's own painted square
(but well outside the line's own bounds) shows the marker's fill color surviving an identity
(`feOffset dx="0" dy="0"`) group filter - proving the offscreen `SourceGraphic` buffer was sized
large enough to avoid silently clipping the marker's pixels before the filter/composite step ever
saw them.

A third regression test closes a further code-review finding in the same bounds pre-pass: a
descendant that itself carries its own `filter` attribute whose own filter region extends beyond
its raw geometry (an `feFlood` combined with a deliberately enlarged filter
`x`/`y`/`width`/`height` region, in `SvgCodec_Load_GroupFilterWithNestedFilteredChildExpandingBeyondOwnBounds_PreservesFullNestedFilterOutput`)
used to have only its raw geometry bounds folded into the pre-pass, not its own filter's actual
(larger) output extent. A tiny 4x4 rect carries an `innerFlood` filter whose region is expanded to
roughly cover the whole 100x100 canvas, wrapped in an outer `<g>` with an identity
(`feOffset dx="0" dy="0"`) pass-through filter; points far outside the rect's own raw bounds but
inside the inner filter's actual expanded region now show the inner `feFlood`'s red output
surviving the outer group's own offscreen round-trip, proving the outer group's buffer is sized
from the inner filter's own expanded output rather than merely the inner shape's raw geometry -
while a point outside even that generously-expanded region remains untouched, confirming the fix
is a genuine bounds correction rather than an unconditional expand-to-fill-the-canvas regression.

A fourth pair of regression tests closes a further code-review finding in this same bounds
pre-pass: the local-scratch counter/budget fix above means each individual
`RenderFilteredGroup` invocation's own pre-pass gets a fresh, full-sized ceiling every time, so a
document with many levels of nested filtered groups, each wrapping a large subtree, could force
total pre-pass work proportional to `depth * subtree-size` - unbounded by
`MaxTotalRenderedElements`/`GeometryWorkBudget`, since every nesting level "resets" those
per-invocation limits.
`SvgCodec_Load_DeeplyNestedFilteredGroupsExceedingCumulativeBoundsPrePassBudget_ThrowsInvalidDataException`
constructs a chain of nested filtered `<g>` elements deep enough (comfortably within
`MaxElementDepth`) wrapping a leaf `<rect>` count chosen so their combined, per-nesting-level
bounds pre-pass work exceeds the new cumulative `BoundsPrePassWorkBudget` ceiling, while the
document's own *real* total rendered-element count stays comfortably under the separate
`MaxTotalRenderedElements` ceiling - isolating the assertion to the new cumulative pre-pass
budget specifically, rather than incidentally tripping a pre-existing guard - and asserts `Load`
throws `InvalidDataException` rather than performing unbounded pre-pass work.
`SvgCodec_Load_ModestlyNestedFilteredGroups_RenderCorrectlyWithoutFalsePositiveFallback` is the
companion non-regression test: a modest, realistic few levels of nested filtered groups (each
wrapping a small number of shapes) renders its innermost filter's effect correctly, proving the
new cumulative ceiling is generous enough that ordinary real-world nested-filtered-group
documents are never spuriously rejected.

A fifth regression test closes a further code-review finding in `ApplyOwnFilterToLocalBounds`: it
used to always substitute a descendant's filter-expanded local region for its raw geometry bounds
whenever that descendant's filter had at least one primitive, even when the descendant's own
filter region was so pathologically oversized that the real render pass would later reject it via
`ComputeFilterRegionPixelBounds`'s `MaxCoordinateMagnitude` check and fall back to unfiltered
rendering at the descendant's own raw bounds - by which point the pre-pass had already used the
doomed, much larger expanded region to compute the *ancestor's* own combined bounds, which could
itself then trip the ancestor's own real pixel-space rejection checks, incorrectly skipping a
perfectly reasonable outer filter.
`SvgCodec_Load_OuterGroupFilterWithPathologicallyOversizedDescendantFilter_StillAppliesOuterFilter`
wraps a tiny rect - whose own `innerHuge` filter's region is deliberately expanded to roughly
4,000,000 local-space units square, far beyond `MaxCoordinateMagnitude`'s 1,000,000, guaranteeing
real-render-time rejection - inside an outer `<g>` with its own plain `feFlood` filter, and asserts
the outer `feFlood`'s own color still paints its own (correctly, modestly sized) filter region:
proving the new local-space "is this descendant filter obviously doomed" check keeps the doomed
inner filter's region from poisoning the outer filter's own bounds and causing it to be
tolerantly skipped, while a point outside the outer filter's own region remains untouched,
confirming the fix does not merely disable region expansion altogether.

A sixth pair of regression tests closes a final code-review finding in this same bounds pre-pass:
the cumulative `BoundsPrePassWorkBudget` added above only charged once per element visit, never
accounting for the actual geometry-parsing work each visit performs, so a single element with an
enormous `d`/`points`/text value, nested under many levels of filtered groups, would still have
that same enormous geometry fully re-parsed once per nesting level - a `depth * geometry-size` CPU
cost neither that per-visit charge nor any individual invocation's own fresh `GeometryWorkBudget`
bounds. `SvgCodec_Load_DeeplyNestedFilteredGroupsWithEnormousInnerPath_ThrowsInvalidDataException`
constructs 20 nested filtered `<g>` levels wrapping a single `<path>` whose `d` attribute has
30,000 commands - comfortably under the 200,000-command per-invocation `GeometryWorkBudget`
ceiling on its own, isolating the assertion to the new geometry-weighted cumulative charge
specifically - and asserts `Load` throws `InvalidDataException` rather than performing 20 full
re-parses of that same path.
`SvgCodec_Load_ModestlyNestedFilteredGroupsWithNormalPath_RenderCorrectlyWithoutFalsePositiveFallback`
is the companion non-regression test: 5 levels of nested filtered groups wrapping one small,
ordinary path renders its fill color correctly, proving the new geometry-weighted ceiling is
generous enough that ordinary real-world documents are never spuriously rejected.

#### CanvasNet-Codecs-SvgCodec-ClipPathRendering: `clipPath` Union-of-Children Hard Clipping

**Tests**: `SvgCodec_Load_ClipPathUserSpaceOnUse_ClipsToAbsoluteCircle`,
`SvgCodec_Load_ClipPathObjectBoundingBox_ScalesClipContentToReferenceBounds`,
`SvgCodec_Load_ClipPathMultipleChildren_ClipsToUnionOfAllShapes`,
`SvgCodec_Load_DanglingClipPathReference_RendersUnclipped`,
`SvgCodec_Load_ClipPathChildClipRuleEvenOdd_ProducesHoleAtOverlap`,
`SvgCodec_Load_ClipPathOnGroup_ClipsCombinedGroupContent`,
`SvgCodec_Load_ClipPathReferencesNonClipPathElement_RendersUnclipped`

Asserts a `clip-path="url(#id)"` referencing a `clipPath` with the default (`userSpaceOnUse`)
`clipPathUnits` clips a rect's own fill to a circle positioned in the referencing element's own
local space, verified at points inside and outside the circle; asserts
`clipPathUnits="objectBoundingBox"` instead resolves the clip child's own coordinates as
fractions of the referencing element's own bounding box, verified by comparing against the
same-shaped `userSpaceOnUse` case scaled to a different reference rect's own bounds; asserts a
`clipPath` with two non-overlapping children (a rect and a circle) clips to their combined union
rather than only the last child or their intersection, verified at a point inside each child
individually; asserts a `clip-path` reference to a nonexistent id renders the element fully
unclipped, matching this codec's general dangling-reference convention; asserts a `clipPath`
child's own `clip-rule="evenodd"` produces a hole where two overlapping shapes coincide (an
outer square minus an inner, evenodd-ruled square), distinguishing it from the `nonzero` default
which would instead produce their union with no hole; asserts `clip-path` on a `<g>` clips the
combined rendered content of all of its children as a single unit, the same
"whole-subtree-as-one-unit" convention `CanvasNet-Codecs-SvgCodec-GroupFilterRendering` already
establishes for group-level `filter`; and asserts a `clip-path` reference that resolves to a
well-formed element which is not literally a `clipPath` (a plain `rect`) is tolerated as a no-op,
rendering unclipped, mirroring this codec's existing `filter`/marker wrong-element-type tolerance.

#### CanvasNet-Codecs-SvgCodec-MaskRendering: `mask` Luminance Semantics and Independent Unit Attributes

**Tests**: `SvgCodec_Load_MaskDefaultLuminance_WhiteRevealsBlackHides`,
`SvgCodec_Load_MaskGrayShape_AttenuatesAlphaProportionallyToLuminance`,
`SvgCodec_Load_MaskUnitsUserSpaceOnUse_ReadsRegionAsAbsoluteCoordinates`,
`SvgCodec_Load_MaskContentUnitsObjectBoundingBox_ScalesMaskContentToReferenceBounds`,
`SvgCodec_Load_MaskOnUseElement_MasksResolvedTarget`

Asserts a `mask="url(#id)"` referencing a `mask` element with a white rect over half the
referencing element's own bounds and a black rect over the other half fully reveals the element
under the white region and fully hides it under the black region, proving the default luminance
mask semantics; asserts a mid-gray mask shape attenuates the referencing element's own alpha
proportionally to that gray's own computed luminance (the standard sRGB coefficients
`0.2125*R + 0.7154*G + 0.0721*B`), verified against the exact expected alpha value rather than
merely "some partial value"; asserts `maskUnits="userSpaceOnUse"` reads the mask's own
`x`/`y`/`width`/`height` region box as literal absolute local-space coordinates rather than
fractions of the referencing element's own bounding box; asserts `maskContentUnits="objectBoundingBox"`
independently scales the mask's own *content* coordinates as fractions of the referencing
element's own bounding box while the mask's own region box (`maskUnits`, left at its default)
is unaffected - proving the two attributes are resolved independently rather than one being
mistakenly conflated with (or overriding) the other; and asserts `mask` on a `<use>` element
masks the whole resolved target subtree as a single unit, the same convention
`CanvasNet-Codecs-SvgCodec-GroupFilterRendering` already establishes for group-level `filter`
and `CanvasNet-Codecs-SvgCodec-ClipPathRendering` establishes for group-level `clip-path`.

#### CanvasNet-Codecs-SvgCodec-EffectOrdering: Clip/Mask-Before-Filter Ordering and Regression Coverage

**Tests**: `SvgCodec_Load_ClipPathWithFilter_AppliesClipBeforeFilter`,
`SvgCodec_Load_MaskWithFilter_AppliesMaskBeforeFilter`,
`SvgCodec_Load_FilterOnlyNoClipOrMask_RendersUnaffected`,
`SvgCodec_Load_PlainShapeNoEffects_RendersUnaffected`

Asserts an element carrying both `clip-path` and `filter` clips the element's own content to the
clip region *before* the filter's own primitive chain runs, by using a filter whose `feFlood`
would otherwise flood the whole (unclipped) filter region: only the portion of that flooded
region inside the clip shape is visible, proving the filter operated on already-clipped content
rather than the clip being applied (or ignored) after the filter, or applied to the filter's own
final output instead of its input. Asserts the analogous ordering for `mask` combined with
`filter`: the mask's own white region reveals the filter's flood output while the mask's own
black region (still within the filter's own, larger, expanded region) hides it entirely, proving
the mask was applied to the filter's `SourceGraphic` input, not its output. Two regression tests
close the loop on the shared effects-pipeline generalization these two features required:
`SvgCodec_Load_FilterOnlyNoClipOrMask_RendersUnaffected` re-verifies a group-level, filter-only
`<g>` (no `clip-path`/`mask` attribute anywhere) still renders identically to
`CanvasNet-Codecs-SvgCodec-GroupFilterRendering`'s own pre-existing coverage, proving the
generalized `RenderGroupWithEffects` entry point did not alter pre-existing filter-only
behavior; and `SvgCodec_Load_PlainShapeNoEffects_RendersUnaffected` proves a plain shape with
none of `filter`/`clip-path`/`mask` present still renders through the ordinary fast path (no
offscreen buffer of any kind allocated) with its own plain fill color, unaffected by either
generalization.

#### CanvasNet-Codecs-SvgCodec-EffectResourceSafety: Clip/Mask Resource-Safety Bounds

**Tests**: `SvgCodec_Load_MaskReferenceCycle_ThrowsInvalidDataException`,
`SvgCodec_Load_ClipPathReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnclippedForExcessShapes`,
`SvgCodec_Load_MaskReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnmaskedForExcessShapes`,
`SvgCodec_Load_ClipPathAndMaskCombinedExceedingCumulativeBudget_FallsBackToUnclippedUnmaskedForExcessShape`,
`SvgCodec_Load_ClipPathExceedsMaxShapesPerClipPath_TruncatesToFirst256Shapes`,
`SvgCodec_Load_MaskRegionExceedsMaxDimension_FallsBackToUnmaskedRendering`

Asserts a `mask` whose own content transitively re-references an ancestor element's own `mask`
(forming a reference cycle solely through `mask` attribute resolution, not plain element nesting
or `use`/`marker` references) throws `InvalidDataException` rather than overflowing the call
stack or hanging - caught for free by the pre-existing `MaxElementDepth` guard, since mask
content is rendered through the ordinary `RenderElement` walk rather than a bespoke traversal (see
the design doc's discussion of why clip-path, by contrast, needs no analogous cycle guard).
Asserts the shared, cumulative `FilterWorkBudget` ceiling (the same budget class filter
application already charges, reused rather than duplicated for clip/mask offscreen-buffer sizing

- see the design doc's rationale) is genuinely exhausted by clip-path-only and mask-only
offscreen-buffer charges, not merely approached: five giant, staggered shapes share one empty
`clipPath` (respectively, one empty, explicitly-sized `mask`), each charging exactly
`pixelWidth * pixelHeight` work units per application; the first four shapes' cumulative charge
stays within the ceiling, so their (empty) clip/mask genuinely applies and hides them completely,
while the fifth shape's charge would exceed the ceiling, so it tolerantly falls back to
unclipped/unmasked rendering and remains fully visible in its own exclusive band. Regression test
for a code-review finding that combining `clip-path` *and* `mask` on the same element previously
charged only one region-area unit total (rather than one per offscreen buffer actually allocated):
two giant, staggered shapes each combine an empty `clip-path` and an empty, explicitly-sized
`mask`, so each application now correctly charges three region-area units (content + clip coverage
- mask source); the first shape's combined charge stays within the ceiling and both its clip-path
and mask genuinely apply (fully invisible), while the second shape's combined charge exceeds the
ceiling and falls back to fully unclipped/unmasked rendering (visible in its own plain fill
color) - proving the combined charge, not merely one of the two effects' own charge, is what
correctly exhausts the shared budget. Asserts
`MaxClipPathShapesPerClipPath` caps a single `clipPath` element to its first 256 recognized direct
children - a 257th, distinctly positioned child is silently truncated (never rasterized onto the
clip's coverage buffer), while the first 256 children's own union still clips correctly, proving
truncation rather than an exception is the enforcement mechanism. Asserts a `mask` region whose
explicit `maskUnits="userSpaceOnUse"` `x`/`y`/`width`/`height` spans a pathologically large
local-space area - large enough that, once transformed to pixel space, it would exceed
`Surface.MaxDimension` - is rejected by the same pixel-space magnitude/dimension guard already
enforced for filter regions, tolerantly falling back to unmasked rendering rather than attempting
an oversized offscreen buffer allocation.

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
