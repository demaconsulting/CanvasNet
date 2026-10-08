## VsdxDocument Unit Verification Design

<!-- cspell:ignore vsdx Visio VisioML davehoward jgreywolfvsdxjs Jgreywolf Foregnd Themed -->
<!-- cspell:ignore THEMEVAL unitsperem NURBSTo -->

`VsdxDocument` is distributed as the separate `DemaConsulting.CanvasNet.Vsdx` NuGet package
(namespace `DemaConsulting.CanvasNet.Vsdx`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Vsdx.Tests` project.
`VsdxDocument` is the sole unit of the CanvasNetVsdx system.

### Verification Approach

The `VsdxDocument` unit is verified through dedicated unit-test classes, one per resolver area,
mirroring the already-established approach of _PptxDocument Unit Verification_
(`canvas-net-pptx/pptx-document.md`): `VsdxDocumentTests.cs` (OPC package layer, page index),
`VsdxGeometryTests.cs` (geometry-row resolution), `VsdxTransformTests.cs` (shape-local-to-page-space
transform), `VsdxMasterInheritanceTests.cs` (Master/MasterShape cell and geometry-row merge),
`VsdxStyleResolutionTests.cs` (StyleSheet chain walk, color/fill), `VsdxColorPaletteTests.cs`
(built-in palette/hex color resolution), `VsdxThemeResolutionTests.cs` (`"Themed"` sentinel
resolution), `VsdxTextParsingTests.cs`/`VsdxTextStyleTests.cs`/`VsdxTextBoxPositioningTests.cs`/
`VsdxTextLayoutTests.cs`/`VsdxTextRenderTests.cs` (the text pipeline), `VsdxConnectsParsingTests.cs`/
`VsdxGluePointResolutionTests.cs`/`VsdxArrowheadResolutionTests.cs` (connectors),
`VsdxGroupResolutionTests.cs` (recursive group/nested-shape resolution), and `VsdxRenderTests.cs`
(the public `Render` API). Most unit-level tests construct a small, hand-authored, in-memory
`.vsdx`-shaped ZIP package via a shared `VsdxTestPackages.BuildPackage` helper (parameterized by
page-shapes/StyleSheets/Masters/Connects/theme XML fragments), reusing the BCL
`System.IO.Compression.ZipArchive` writer over a `MemoryStream`, mirroring `PptxDocumentTests.cs`'s
own `BuildPackage` precedent. A complementary, smoke-level tier (`VsdxFixtureShapeResolutionTests.cs`,
`VsdxFixtureTextResolutionTests.cs`, `VsdxConnectorFixtureTests.cs`, `VsdxRenderFixtureTests.cs`)
exercises the same resolvers against the real, third-party `.vsdx` fixture corpus staged in-project
under `VsdxFixtures/`, confirming the hand-authored unit tests' assumptions hold against genuine
Visio-authored content, not just synthetic fragments; several worked-example regression tests
(`VsdxTransformTests`'s rotation example, `VsdxGluePointResolutionTests`'s glue-coordinate examples)
read exact cell values directly from a staged fixture's own XML and independently (Python, double
precision) compute the expected result, rather than merely asserting "did not throw".

Tests access a number of `internal` resolver members (`ResolveMasterShape`, geometry/paint/style
resolution helpers, `VsdxColorPalette.Resolve`, `ResolveTextLayout`, `PaintTextLayout`) through the
`InternalsVisibleTo` grant to the `DemaConsulting.CanvasNet.Vsdx.Tests` project (see the
`VsdxDocument` project file), exercising a specific resolution phase directly from a real,
package-resolved input rather than only through the fully-composed public API - while the public-
API-only system tier (see _System Verification Design_, `canvas-net-vsdx.md`) separately proves the
whole pipeline composes correctly end-to-end. Because `VsdxDocument`'s dependencies
(`System.IO.Compression.ZipArchive`, `System.Xml.Linq`, and the core `CanvasNet.Canvas`/`Geometry`/
`Drawing`/`Fonts` subsystems) are BCL types or already-tested sibling-package types, no mocking or
stubbing is required. Tests assert on resolved cell values, resolved transform coordinates, resolved
paint colors, resolved text runs/layout, resolved `Connect`/endpoint records, and thrown exception
types - never on "no exception thrown" alone, so every test can actually fail if the implementation
is wrong.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Fixtures**: 14 real-world `.vsdx` files staged in-project under `VsdxFixtures/` (sourced from
  two third-party test suites - see that folder's own `README.md` for provenance/licensing),
  copied to the test output directory by the `.csproj`'s own `<None Include="VsdxFixtures\**">`
  wiring and resolved at test time via `AppContext.BaseDirectory`; most unit-level tests instead
  build their own small, in-memory package via `VsdxTestPackages`
- **Isolation**: Each test method either constructs its own in-memory package or opens its own
  staged fixture file; no shared state between tests

### Unit-Level Test Scenarios

#### CanvasNetVsdx-VsdxDocument-OpenStream: Open(Stream) Buffers Input and Validates Null/Oversized Arguments

**Tests**: `VsdxDocument_Open_WellFormedMinimalPackage_Succeeds`,
`VsdxDocument_Open_NullStream_ThrowsArgumentNullException`,
`VsdxDocument_Open_NonTerminatingOversizedStream_ThrowsInvalidDataException`

Proves `Open(Stream)` succeeds against a minimal, well-formed, in-memory package, proves a null
stream throws `ArgumentNullException` before any parsing is attempted, and proves a
non-terminating, oversized stream throws `InvalidDataException` once more than the documented
maximum package size has been read, rather than exhausting memory or blocking indefinitely.

#### CanvasNetVsdx-VsdxDocument-OpenPath: Open(string) Validates Null and Empty/Whitespace Path Arguments

**Tests**: `VsdxDocument_Open_NullPath_ThrowsArgumentNullException`,
`VsdxDocument_Open_WhitespacePath_ThrowsArgumentException`

Proves `Open(string)` throws `ArgumentNullException` for a null path and `ArgumentException` for a
whitespace-only path, mirroring `PptxDocument.Open(string)`'s own validation order and exception
types.

#### CanvasNetVsdx-VsdxDocument-ZipValidation: Corrupt/Non-ZIP Stream Throws InvalidDataException

**Test**: `VsdxDocument_Open_CorruptNonZipStream_ThrowsInvalidDataException`

Proves a stream of arbitrary bytes that is not a valid ZIP local-file-header signature throws
`InvalidDataException` from `Open`, rather than an unrelated `ZipArchive`-internal exception type
leaking through uncaught.

#### CanvasNetVsdx-VsdxDocument-ContentTypesValidation: Missing/Oversized Content-Types Part Throws

**Tests**: `VsdxDocument_Open_MissingContentTypes_ThrowsInvalidDataException`,
`VsdxDocument_Open_OversizedContentTypesPart_ThrowsInvalidDataException`

Proves a package containing no `[Content_Types].xml` entry at all throws `InvalidDataException`
from `Open`, and proves an attacker-controlled content-types part padded past the documented
bounded character budget also throws `InvalidDataException` rather than risking unbounded memory
use.

#### CanvasNetVsdx-VsdxDocument-RelationshipResolution: Document/Masters/Pages/Theme Resolved via r:id

**Tests**: `VsdxDocument_Open_ResolvesDocumentMastersPagesViaRelationships`,
`VsdxDocument_Open_MissingRequiredRelationshipTarget_ThrowsInvalidDataException`

Proves `Open(Stream)` resolves `visio/document.xml`, `visio/masters/masters.xml`,
`visio/pages/pages.xml`, and the optional `visio/theme/theme1.xml` exclusively through the
relationship graph (never a filename-number convention) - confirmed via the internal
`MastersPartPath`/`ThemePartPath` accessors - and proves a package missing a required relationship
target throws `InvalidDataException`.

#### CanvasNetVsdx-VsdxDocument-Dispose: IDisposable Releases the Underlying ZipArchive

**Tests**: `VsdxDocument_Dispose_ReleasesUnderlyingZipArchive`,
`VsdxDocument_Dispose_CalledTwice_DoesNotThrow`

Proves `Dispose()` releases the underlying `ZipArchive` (a subsequent operation against the
disposed archive fails as expected) and that calling `Dispose()` twice does not throw, matching the
standard .NET disposable-idempotence contract.

#### CanvasNetVsdx-VsdxDocument-PageCount: PageCount Reflects pages.xml's Declared Page Index

**Test**: `VsdxDocument_PageCount_ReturnsDeclaredPageCount`

Proves `PageCount` returns the exact number of page entries parsed from `visio/pages/pages.xml`,
available immediately after `Open` succeeds, with no page shape tree resolved yet.

#### CanvasNetVsdx-VsdxDocument-GetPageSize: GetPageSize Resolves Declared Size in EMU and Validates the Index

**Tests**: `VsdxDocument_GetPageSize_ReturnsDeclaredSizeInEmu`,
`VsdxDocument_GetPageSize_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`

Proves `GetPageSize(int)` resolves the requested page's declared width/height (converted from
inches to the internal EMU unit) and name, and proves a `pageIndex` outside `[0, PageCount)` throws
`ArgumentOutOfRangeException`.

#### CanvasNetVsdx-VsdxDocument-LazyPageResolution: A Page's Shape Tree Is Resolved Once and Cached

**Test**: `Render_AfterRendering_GetPageShapesReturnsTheSameCachedShapeTreeInstance`

Proves a page's full shape tree is resolved on first access (via `Render` or `GetPageShapes`) and
that a subsequent call returns the exact same cached instance (`Assert.Same`), confirming the page
is not re-resolved from scratch on every call.

#### CanvasNetVsdx-VsdxDocument-GeometryRowResolution: MoveTo/LineTo/RelMoveTo/RelLineTo Resolve to Path Segments

**Tests**: `Geometry_MoveToLineTo_ProducesShapeLocalPoints`,
`Geometry_RelMoveToRelLineTo_NormalizedByWidthAndHeight`

Proves `MoveTo`/`LineTo` rows resolve into shape-local `Path` points with the exact declared
`X`/`Y` coordinates, and proves `RelMoveTo`/`RelLineTo` rows' normalized `[0,1]` coordinates are
scaled by the shape's own resolved `Width`/`Height` before being added to the path.

#### CanvasNetVsdx-VsdxDocument-GeometryUnrecognizedRowSkip: Unrecognized Row Types Are Skipped, Not Thrown

**Tests**: `Geometry_UnrecognizedRowType_SkippedWithoutThrowing`,
`Geometry_EllipticalArcToAndNurbsToRows_AreSkippedWithoutThrowing`

Proves an arbitrary unrecognized geometry row type is skipped without throwing, and specifically
proves `EllipticalArcTo` and `NURBSTo` rows fall through to the same tolerant skip as any other
unrecognized row type, rather than being converted to an approximating Bezier curve. **Milestone 8
correction note**: an earlier revision of this requirement and of `canvas-net-vsdx.md`/
`canvas-net-vsdx/vsdx-document.md` claimed `EllipticalArcTo`/`NURBSTo` were converted to Bezier-
curve path segments; direct inspection of `VsdxDocument.Geometry.cs` confirmed this was never
implemented. The design docs and this requirement's own text have been corrected to match the
actual, tolerant-skip behavior; this is a documentation-only correction (no production code
change), discovered while writing this requirement's own test coverage, analogous to the
plan-authorized `FillPatternDeferral` correction below.

#### CanvasNetVsdx-VsdxDocument-ShapeTransform: 2-D Shape Transform Order (LocPin, Flip, Rotate, Pin)

**Tests**: `Transform_NoRotationNoFlip_MapsLocalOriginRelativeToPin`,
`Transform_WorkedRotationExample_MatchesIndependentlyComputedCoordinates`,
`Transform_FlipXFlipY_MirrorsAboutLocPinBeforeRotation`

Proves an unrotated, unflipped shape's transform maps a local point relative to its own
`LocPinX`/`LocPinY`-to-`PinX`/`PinY` offset; proves a worked, independently (Python, double
precision) computed rotation example - reproducing `davehoward-test11-rotate.vsdx`'s own Shape
`ID='1'` cells exactly - matches to within `1e-9`; and proves `FlipX`/`FlipY` mirror a local point
about the `LocPin` before rotation is applied.

#### CanvasNetVsdx-VsdxDocument-ConnectorTransform: 1-D (Connector) Shapes Use the Same Unified Transform

**Test**: `Transform_OneDimensionalShape_UsesSameUnifiedFormula`

Proves a 1-D (connector) shape's pre-baked `PinX`/`PinY`/`Width`/`Height`/`Angle` cells resolve
through the exact same transform formula as a 2-D shape, trusting Visio's own pre-derivation from
the connector's `BeginX`/`Y`/`EndX`/`Y` endpoints rather than re-deriving them independently.

#### CanvasNetVsdx-VsdxDocument-MasterResolution: Master="N" Resolves via masters.xml's Relationship Chain, Cached

**Tests**: `MasterInheritance_InstanceLiteralCell_OverridesMasterCell`,
`MasterInheritance_InstanceOmitsCells_FallsThroughToMasterCells`,
`VsdxDocument_NestedMasterShapeResolution_MatchesChildrenByIdNotPosition`,
`MasterInheritance_RepeatedResolution_ReusesCachedParsedMasterContentTree`

Proves a shape instance's `Master="N"` attribute resolves through `masters.xml`'s `Master`
element, its child relationship, and the masters `.rels` file to the target `masterN.xml` part's
parsed content tree, and - closing this milestone's own caching coverage gap - proves two separate
calls to the internal `ResolveMasterShape` for the same master ID return the exact same (`Assert.
Same`) parsed content tree instance, confirming the per-master-file parse result is cached rather
than re-parsed on every shape resolution.

#### CanvasNetVsdx-VsdxDocument-CellMerge: Instance-Wins, Else-Master Merge (Transform-Cell Exception)

**Tests**: `MasterInheritance_InstanceLiteralCell_OverridesMasterCell`,
`MasterInheritance_InstanceOmitsCells_FallsThroughToMasterCells`,
`MasterInheritance_InstanceInhMarkedTransformCell_PreferredOverMasterValue`,
`MasterInheritance_InstanceInhMarkedNonTransformCell_StillDefersToMasterValue`

Proves an instance's own literal (non-`Inh`, present) cell value takes precedence over the
Master's same-named cell, and proves an instance cell that is absent entirely falls through to the
Master's resolved value for that cell name. Milestone 10 adds two further tests covering a
deliberate, narrowly-scoped exception to this rule for the nine transform cells `PinX`/`PinY`/
`Width`/`Height`/`LocPinX`/`LocPinY`/`Angle`/`FlipX`/`FlipY`: `MasterInheritance_
InstanceInhMarkedTransformCell_PreferredOverMasterValue` proves an instance's own `F="Inh"`-marked
`PinX` cell still wins over the Master's own distinct `PinX` value (unlike the generic rule
above), and `MasterInheritance_InstanceInhMarkedNonTransformCell_StillDefersToMasterValue` proves
an `F="Inh"`-marked _non_-transform cell (`FillForegnd`) still defers to the Master's own literal
value exactly as before, confirming the exception did not widen beyond its intended nine cell
names. This exception was discovered during this milestone's own root-cause investigation of Bug
2 (missing connector lines): the stroke hairline floor (see
`CanvasNetVsdx-VsdxDocument-MinimumVisibleStrokeWidth` below) alone did not resolve
`60973.vsdx`'s missing connector-line symptom against the Visio-reference PNG, because the generic
merge rule was also collapsing connector shape `802`'s resolved position to its Master's own
small, unrelated template-local corner; re-inspecting the raw XML confirmed the instance's own
`PinX`/`PinY` cells were themselves already correct (baked from that instance's own `BeginX`/
`BeginY`/`EndX`/`EndY` endpoints) but marked `F="Inh"`, so the generic rule discarded them - a
deviation beyond this milestone's originating plan report's own stroke-width-only diagnosis,
confirmed resolved via the external smoke-test visual-comparison procedure (§6) after the
refinement.

#### CanvasNetVsdx-VsdxDocument-DeletedShapeExclusion: Shape-Level Del="1" Group-Child Exclusion

**Test**: `VsdxDocument_GroupChildDeletedStub_ExcludedFromResolvedShapeTree`

Proves a group child `<Shape Del="1">` stub (shape-level deletion, distinct from the row-level
`Del="1"` geometry-row marker covered by `CanvasNetVsdx-VsdxDocument-GeometryRowMerge` below) is
excluded from the resolved shape tree entirely via a synthetic fixture mirroring `60973.vsdx`'s
own structure - confirmed, via a controlled `git stash`-based A/B pixel diff of the external
smoke-test harness's rendered output, to eliminate the spurious duplicate placeholder text
(`"OOB: N/A"`/`"[B] Lo1: N/A"`/`"TS: N/A"`) `60973.vsdx` was rendering before this fix.

#### CanvasNetVsdx-VsdxDocument-GeometryRowMerge: Geometry-Row Merge by Matching IX (Replace, Delete, Inherit)

**Tests**: `MasterInheritance_GeometryRowDelete_RemovesMasterRowFromMergedGeometry`,
`MasterInheritance_InstanceGeometryRowWithoutDel_ReplacesMasterRowAtMatchingIndex`

Proves an instance geometry row marked `Del="1"` removes the Master's row at the matching `IX`
entirely (not merely overriding it), and - closing this milestone's own replace-by-IX coverage
gap - proves an instance row at the same `IX` as a Master row, without `Del`, fully replaces that
row's values while every other, unmatched Master row at a different `IX` is still inherited
verbatim into the same merged subpath.

#### CanvasNetVsdx-VsdxDocument-NestedMasterShapeResolution: Group Children Matched by MasterShape ID

**Test**: `VsdxDocument_NestedMasterShapeResolution_MatchesChildrenByIdNotPosition`

Proves a Group-typed master shape's own nested children are correlated against a page instance's
group children by matching each child's `MasterShape` attribute against the master's nested
`Shapes` element IDs, not by declaration order/position.

#### CanvasNetVsdx-VsdxDocument-StyleSheetChainWalk: LineStyle/FillStyle/TextStyle Parent-Pointer Chain Walk

**Test**: `StyleResolution_ChainWalk_ResolvesThroughMultipleParents`

Proves a shape's effective line/fill/text style resolves by walking its `LineStyle`/`FillStyle`/
`TextStyle` StyleSheet-ID reference up a multi-level StyleSheet parent-pointer chain until a
literal, non-`Inh` value is found - confirmed with a three-level chain, independent of Master/
MasterShape inheritance.

#### CanvasNetVsdx-VsdxDocument-TransparencyResolution: FillForegndTrans/LineColorTrans Alpha Modulation

**Tests**: `StyleResolution_FillForegndTrans_ReducesResolvedFillAlpha`,
`StyleResolution_LineColorTrans_ReducesResolvedStrokeAlpha`

Proves a literal `FillForegndTrans`/`LineColorTrans` cell, resolved through the same `FillStyle`/
`LineStyle` StyleSheet chain as `FillForegnd`/`LineColor` themselves, modulates the resolved
`FillColor`/`StrokeColor`'s own alpha channel by the parsed `0..1` transparency fraction (a
literal `"0.4"` reducing alpha to 60% of fully opaque, i.e. `153`). Confirmed against
`60973.vsdx`'s "Virtual Devices" container shape via the external smoke-test harness: before this
fix, its Master's literal 40%-transparent `FillForegndTrans`/`LineColorTrans` cells were never
consulted, so the container rendered fully opaque, obscuring its own label and its children's top
edges; direct pixel sampling of the rendered output after the fix (fill color
`(127,127,127,153)` blended over a white background) confirmed the rasterizer's own alpha
compositing produces the expected `(178,178,178)` result, matching the Visio-reference PNG's
lighter, mostly-see-through frame.

#### CanvasNetVsdx-VsdxDocument-DirectOverridePrecedence: A Shape's Own Direct Cell Wins Over the StyleSheet Chain

**Test**: `StyleResolution_DirectShapeOverride_TakesPrecedenceOverStyleSheetChain`

Proves a shape's own direct, non-`Inh` cell value is applied in preference to any value resolved
through its StyleSheet chain for the same cell name.

#### CanvasNetVsdx-VsdxDocument-NoStyleTermination: The Chain Walk Terminates at StyleSheet ID 0 ("No Style")

**Test**: `StyleResolution_NoAncestorSuppliesValue_TerminatesAtStyleSheetZeroDefault`

Closing this milestone's own coverage gap: proves the StyleSheet chain walk terminates at
StyleSheet `ID="0"` ("No Style") and applies its own documented default values when no ancestor
StyleSheet in the chain declares a literal value for a given cell name, guaranteeing the walk
cannot loop unboundedly even for a shape with no explicit style chain of its own.

#### CanvasNetVsdx-VsdxDocument-ColorTableResolution: Built-In Palette Index and Direct Hex Literal Resolution

**Tests**: `VsdxColorPalette_BuiltInPaletteIndex_ResolvesDocumentedRgbValue`,
`VsdxColorPalette_DirectHexValue_ResolvesLiterally`

Closing this milestone's own coverage gap: proves a `FillForegnd`/`LineColor` cell holding a small
non-negative integer resolves against the built-in 24-entry Visio color-index palette (index `2`,
documented as pure red), and proves a cell holding a direct `#RRGGBB` hex literal resolves
literally - both exercised end-to-end through a resolved shape's `Paint`. **Milestone 8 correction
note**: this requirement previously also claimed resolution against "the document's own explicit
hex color table (`visio/document.xml`'s `Colors` section)". A repository-wide search confirms no
`<Colors>` element is ever parsed anywhere in `VsdxDocument`'s source; the design docs and this
requirement's own title/text have been corrected to describe only the two resolution forms this
package actually implements. This is a documentation-only correction, discovered while writing
this requirement's own test coverage - no production code was changed, and implementing a
document-level `<Colors>` table remains out of scope for this test/docs milestone.

#### CanvasNetVsdx-VsdxDocument-ThemedCellResolution: "Themed" Resolves Against Theme, Else Falls Back

**Tests**: `VsdxDocument_ThemedCellResolution_FallsBackToNeutralDefaultWhenNoThemePart`,
`VsdxDocument_ThemedCellResolution_ResolvesAgainstParsedTheme`

Proves a literal `"Themed"` cell value falls back to a documented neutral default color when no
`visio/theme/theme1.xml` part is present, and proves it resolves against the parsed theme's own
`<a:clrScheme>` slot when the cell's own formula carries a recognized `THEMEVAL("slotName")`
reference - necessarily a hand-built synthetic package, since no real staged fixture's own
`THEMEVAL(...)` argument happens to use a canonical DrawingML slot name (see
`VsdxThemeResolutionTests`'s own remarks).

#### CanvasNetVsdx-VsdxDocument-FillPatternDeferral: Solid Resolves Directly, Any Other Value Degrades

**Tests**: `StyleResolution_FillPatternZero_ResolvesNoFill`,
`StyleResolution_DegradesNonSolidFillPatternToSolidFillWithoutThrowing`

Proves `FillPattern="0"` resolves to no fill, and - closing this milestone's own coverage gap -
proves a non-solid, non-zero `FillPattern` value (`"25"`) still degrades to the same solid-fill
treatment as `FillPattern="1"`, using the shape's resolved `FillForegnd` color, rather than
throwing. **Milestone 8 correction note**: this requirement previously stated the library "shall...
throw `VsdxUnsupportedFeatureException`" for any non-solid `FillPattern` value - the plan
explicitly authorized this correction. Direct inspection of `VsdxDocument.Paint.cs` and a
repository-wide search confirm `VsdxUnsupportedFeatureException` is never constructed or thrown
anywhere in the Vsdx codebase; every non-zero `FillPattern` value has always degraded to solid
fill. The design docs and this requirement's own text have been corrected to describe this actual,
already-implemented, already-design-doc-approved behavior; no production code was changed.

#### CanvasNetVsdx-VsdxDocument-TextRunParsing: cp/pp Marker Interleaving Resolves Ordered Formatted Runs

**Tests**: `TextParsing_PpThenCpMarkers_BothRowIndicesAttached`,
`TextParsing_MultipleCpMarkers_EachStartsNewRunUntilNextMarkerOrEnd`

Proves a `<Text>` element's `<pp>` (paragraph) marker followed by a `<cp>` (character) marker both
attach their own row index to the resulting run, and proves multiple `<cp>` markers each start a
new run that persists until the next marker or the end of the text, matching the format
reference's own §8.1 interleaving rule.

#### CanvasNetVsdx-VsdxDocument-TextBoxPositioning: Explicit Txt* Cells Override the Implicit Shape-Box Default

**Tests**: `TextBox_ExplicitTxtCells_OverrideImplicitDefault`,
`TextBox_NoTxtCells_ResolvesImplicitDefaultEqualToShapeGeometryBox`,
`TextBox_MasterDeclaresExplicitTxtCells_InstanceInheritsThem`

Proves a shape's own explicit `TxtPinX`/`TxtPinY`/`TxtLocPinX`/`TxtLocPinY`/`TxtWidth`/`TxtHeight`/
`TxtAngle` cells override the implicit default (the shape's own geometry bounding box), proves the
implicit default is used verbatim when no such cells are present, and proves a Master's own
explicit `Txt*` cells are inherited by an instance that declares none of its own, via the existing,
shared `MergeCells` helper.

#### CanvasNetVsdx-VsdxDocument-GlyphRendering: Text Runs Paint Visible Glyph Ink via the Core Fonts/Drawing Subsystems

**Test**: `Render_TextShape_PaintsVisibleGlyphInk`

Proves a shape's resolved text run paints visible, non-transparent glyph ink onto the destination
`Surface`, reusing the core `Fonts` subsystem's typeface resolution and the core `Drawing`
subsystem's `PathFiller`, consistent with the painting primitives already established by the
sibling Pdf/Pptx rendering systems.

#### CanvasNetVsdx-VsdxDocument-ConnectsParsing: Connects Section Parses Into From/To Sheet/Cell/Part Records

**Tests**: `ConnectsParsing_DaveHowardConnectorsFixture_ResolvesExactConnectEntryAttributes`,
`ConnectsParsing_JgreywolfConnectorsFixture_ResolvesConnectionPointIndexedGlue`,
`ConnectsParsing_NoConnectsSection_EveryShapeHasEmptyConnectsList`

Proves every `<Connect>` entry in two real fixtures' own page content (whole-shape-pin glue in
`davehoward-test4-connectors.vsdx`; connection-point-indexed glue in
`jgreywolfvsdxjs-connectors.vsdx`) resolves with the exact `FromSheet`/`FromCell`/`FromPart`/
`ToSheet`/`ToCell`/`ToPart` attribute values read directly from each fixture's own XML, and proves
a page with no `<Connects>` section at all resolves every shape with an empty `Connects` list
rather than throwing.

#### CanvasNetVsdx-VsdxDocument-GluePointResolution: Connector Endpoints Trust Pre-Baked BeginX/Y/EndX/Y Cells

**Tests**: `GluePointResolution_PlainLineFixture_MatchesExactBeginEndCellValues`,
`GluePointResolution_WholeShapePinGlueFixture_MatchesExactPreBakedCoordinates`,
`GluePointResolution_ConnectionPointIndexedGlueFixture_MatchesExactPreBakedCoordinates`

Proves a connector's rendered endpoints are read directly from its own already-resolved `BeginX`/
`BeginY`/`EndX`/`EndY` cells - confirmed with exact-value worked-example regression tests against
three real fixtures (an unmastered plain line, whole-shape-pin glue, and connection-point-indexed
glue) - without performing any live glue-point tracking/constraint solving.

#### CanvasNetVsdx-VsdxDocument-ArrowheadRendering: Recognized Arrowhead Indices Paint, Unrecognized Degrade Gracefully

**Tests**: `Render_ConnectorWithRecognizedEndArrow_PaintsArrowheadInkBeyondLineStroke`,
`Render_ConnectorWithUnrecognizedEndArrowIndex_DegradesGracefullyWithoutThrowing`

Proves a connector with a recognized `EndArrow` index paints additional ink beyond its own plain
line stroke (the arrowhead itself), and proves an unrecognized `EndArrow` index degrades to a
plain, unadorned line end rather than throwing - the same graceful-degradation convention already
established for `FillPattern`.

#### CanvasNetVsdx-VsdxDocument-DanglingGlueTargetTolerance: A Dangling Connect Target Skips Only That Glue Resolution

**Test**: `ConnectsParsing_DanglingGlueTarget_SkipsGlueResolutionNotWholePage`

Proves a `<Connect>` entry whose target shape ID cannot be resolved within the page is tolerated:
only that connector's glue resolution is skipped, while the connector shape itself still renders
using its own already-resolved `BeginX`/`Y`/`EndX`/`Y` cells, unaffected.

#### CanvasNetVsdx-VsdxDocument-GroupChildTransformComposition: Nested Group Children Compose Parent-Chain Transforms

**Test**: `VsdxDocument_GroupChildTransformComposition_ResolvesNestedGroupChildPosition`

Proves a 3-level-deep nested group's leaf child composes its absolute page-space position by
applying every intermediate ancestor's own local-to-parent transform from the leaf up to the page -
worked by hand against `davehoward-test10-nested-shapes.vsdx`'s own cells, not merely "did not
throw".

#### CanvasNetVsdx-VsdxDocument-GroupNestingDepthBudget: Depth/Shape-Count Budgets Reject Pathological Nesting

**Tests**: `VsdxDocument_GroupNestingDepthBudget_ExceedingDepthThrowsInvalidDataException`,
`VsdxDocument_GroupNestingDepthBudget_ExceedingShapeCountThrowsInvalidDataException`

Proves a page whose group nesting exceeds the documented maximum recursion depth, and separately a
page whose total resolved shape count exceeds the documented maximum, both throw
`InvalidDataException` rather than recursing or allocating without bound.

#### CanvasNetVsdx-VsdxDocument-RenderPixelDimensions: Render(width, height, options) Paints a Cleared Surface

**Tests**: `Render_ExplicitBackgroundColor_OverridesTheOpaqueWhiteDefault`,
`Render_TextShape_PaintsVisibleGlyphInk`, `Render_DefaultOptions_BackgroundIsOpaqueWhite`

Proves the pixel-dimensions `Render` overload clears a new surface to the requested (or default
opaque white) background color before painting the resolved page shape tree, and returns the
painted surface.

#### CanvasNetVsdx-VsdxDocument-RenderDpi: Render(pageIndex, dpi, options) Computes Pixel Dimensions

**Test**: `Render_DpiOverload_ComputesExpectedPixelDimensions`

Proves the DPI `Render` overload resolves the page's declared size, converts it to pixel
dimensions at the requested DPI (`round(pageSizeInches * dpi)`), and delegates to the
pixel-dimension overload - confirmed with an exact, rounding-ambiguity-free worked example (8.5in
x 11in at 100 dpi = 850 x 1100 px).

#### CanvasNetVsdx-VsdxDocument-RenderArgumentValidation: Invalid Arguments Throw ArgumentOutOfRangeException

**Tests**: `Render_PageIndexOutOfRange_ThrowsArgumentOutOfRangeException`,
`Render_NonPositiveWidthOrHeight_ThrowsArgumentOutOfRangeException`,
`Render_DpiNonPositive_ThrowsArgumentOutOfRangeException`,
`Render_DpiOverload_PageIndexOutOfRange_ThrowsArgumentOutOfRangeException`

Proves `Render` throws `ArgumentOutOfRangeException` for a `pageIndex` outside `[0, PageCount)`
(both overloads), a non-positive `width`/`height`, and a non-positive `dpi`.

#### CanvasNetVsdx-VsdxDocument-RenderDocumentOrderZOrder: Shapes Paint in Document (Z-)Order

**Test**: `Render_DocumentOrder_LaterDeclaredShapePaintsOnTopOfOverlap`

Proves two overlapping, differently-colored rectangles paint in document order: the
later-declared shape's own color wins within the overlap region, matching the z-order convention
already established by `PptxDocument`/`PdfDocument`.

#### CanvasNetVsdx-VsdxDocument-NonPrintingSuppression: NonPrinting Shapes Skip Self-Paint but Still Render Children

**Test**: `Render_NonPrintingShape_SkipsOwnFillButStillRendersChildren`

Proves a `Type="Group"` shape with its own resolved fill and a `NonPrinting="1"` cell paints none
of its own fill ink (a pixel sampled inside the parent's own box but outside its child's box stays
fully transparent), while its child shape - nested entirely within the parent's own box - still
paints its own, distinctly-colored fill, proving self-paint suppression does not also suppress
recursion into children. Confirmed against `44501b.vsdx`'s Watermark Title shape via the external
smoke-test harness: before this fix, the shape's `NonPrinting` cell was unconsulted and a spurious
"Activity" heading was painted that the Visio-reference PNG never shows; after the fix, the
rendered page matches the reference with no regression.

#### CanvasNetVsdx-VsdxDocument-MinimumVisibleStrokeWidth: Sub-Pixel LineWeight Still Paints a Visible Hairline

**Test**: `Render_SubPixelLineWeight_StillPaintsVisibleHairlineStroke`

Proves a shape with a deliberately sub-pixel resolved `LineWeight` (`0.002in`, 0.2px at 100 DPI)
still paints at least one visible, non-transparent stroke pixel rather than rasterizing to a blank
surface. Confirmed against `60973.vsdx`'s own connector shapes (`LineWeight≈0.0033in`, ~0.5px at
this milestone's 150 DPI smoke-test render) via the external smoke-test harness: before this fix,
sub-pixel connector lines vanished entirely against the Visio-reference PNG's own visible
hairlines; after the fix (a `MinStrokeWidthPixels = 1f` floor applied in `PaintShapeGeometry`'s
`PaintStroke` call, combined with the `CanvasNetVsdx-VsdxDocument-CellMerge` transform-cell
refinement above, which was additionally required to correct the same connector's resolved
position), the rendered page matches the reference's visible connector lines.

### Supplementary (Non-Requirement-Mapped) Coverage

Beyond the 40 `CanvasNetVsdx-VsdxDocument-*` requirement-mapped scenarios above, the test project
carries additional smoke-level and fixture-conformance tiers that increase confidence without
mapping to a single dedicated requirement each:

- `VsdxFixtureShapeResolutionTests.cs` - proves the full shape-resolution pipeline runs to
  completion against every real-world fixture this project's own planning singled out, and that a
  Master-driven group instance with almost no cells of its own still resolves a complete,
  non-degenerate transform.
- `VsdxFixtureTextResolutionTests.cs` - proves the text-resolution pipeline runs to completion
  against every real-world fixture confirmed to contain non-empty `<Text>` content, plus a
  dedicated zero-text fixture proving an empty-text shape resolves empty runs/layout rather than
  throwing.
- `VsdxConnectorFixtureTests.cs` - proves the connector/arrowhead-resolution pipeline runs to
  completion against every real-world fixture that declares a `<Connects>` section.
- `VsdxRenderFixtureTests.cs` - renders one representative real-world fixture per shape-family
  category (plain shapes, text, connectors, nested groups) and asserts real, expected content is
  painted.
- `VsdxGroupResolutionTests.cs` - proves arbitrary-depth nested-group resolution and Master-group-
  child ID correlation against real fixtures, beyond the single worked example cited under
  `GroupChildTransformComposition` above.
- `VsdxTextLayoutTests.cs`/`VsdxTextRenderTests.cs` - unit-level word-wrap/line-breaking/alignment
  and glyph-painting coverage using a deterministic synthetic `TrueTypeFont` (via the shared
  `SyntheticFontBuilder` test-support helper), so every expected glyph coordinate can be
  hand-computed exactly, complementing `GlyphRendering`'s own single representative scenario.
