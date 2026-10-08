## VsdxDocument Unit Verification Design

<!-- cspell:ignore vsdx Visio VisioML davehoward jgreywolfvsdxjs Jgreywolf Foregnd Themed -->
<!-- cspell:ignore THEMEVAL unitsperem NURBSTo Nwwww Neeeee -->

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

**Test**: `Geometry_UnrecognizedRowType_SkippedWithoutThrowing`

Proves an arbitrary unrecognized geometry row type is skipped without throwing. **Milestone 8
correction note**: an earlier revision of this requirement and of `canvas-net-vsdx.md`/
`canvas-net-vsdx/vsdx-document.md` claimed `EllipticalArcTo`/`NURBSTo` were converted to Bezier-
curve path segments; direct inspection of `VsdxDocument.Geometry.cs` confirmed this was never
implemented. The design docs and this requirement's own text have been corrected to match the
actual, tolerant-skip behavior; this is a documentation-only correction (no production code
change), discovered while writing this requirement's own test coverage, analogous to the
plan-authorized `FillPatternDeferral` correction below. **Milestone 11 note**: `EllipticalArcTo`
is no longer part of this requirement's scope - see
`CanvasNetVsdx-VsdxDocument-ArcGeometryResolution` below, which supersedes the Milestone 8
correction note's treatment of `EllipticalArcTo` specifically. `NURBSTo` remains covered here; the
dedicated `Geometry_EllipticalArcToAndNurbsToRows_AreSkippedWithoutThrowing` test was replaced by
`Geometry_EllipticalArcToRow_ConvertsToArcReachingDestination` (below) since it no longer proved a
true statement once `EllipticalArcTo` conversion was implemented.

#### CanvasNetVsdx-VsdxDocument-ArcGeometryResolution: EllipticalArcTo/ArcTo Convert to an ArcTo Path Command

**Tests**: `Geometry_EllipticalArcToRow_ConvertsToArcReachingDestination`,
`Geometry_ArcToRow_ConvertsToArcWithBowDerivedRadius`, `Geometry_ArcToRow_ZeroBow_DegradesToLineTo`

Proves an `EllipticalArcTo` row (general ellipse/arc, cells `X`/`Y`/`A`/`B`/`C`/`D`) converts to a
`PathCommandType.ArcTo` command reaching the row's own destination point; proves an `ArcTo` row
(circular, bow-height-derived arc, cells `X`/`Y`/`A`) converts to an `ArcTo` command whose radius
is derived from the declared bow height via `TryResolveEllipticalArc`'s own geometry; and proves a
zero-bow `ArcTo` row (`A="0"`) degrades to a plain `LineTo` to its destination point rather than
throwing or producing a degenerate zero-radius arc command. Confirmed necessary against
`test.vsdx`'s header-bar shape via the external smoke-test harness: the shape's own
`EllipticalArcTo`-shaped rounded corner was rendering as a sharp wedge/triangle (the arc row
tolerantly skipped, leaving only the surrounding `LineTo` rows) instead of the smoothly rounded
bar Visio itself renders; re-rendering after this fix confirmed the bar now renders correctly
rounded.

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

#### CanvasNetVsdx-VsdxDocument-CellMerge: Instance-Wins, Else-Master Merge (Transform-Cell Exception, 1-D Only)

**Tests**: `MasterInheritance_InstanceLiteralCell_OverridesMasterCell`,
`MasterInheritance_InstanceOmitsCells_FallsThroughToMasterCells`,
`MasterInheritance_InstanceInhMarkedTransformCell_PreferredOverMasterValue`,
`MasterInheritance_InstanceInhMarkedNonTransformCell_StillDefersToMasterValue`,
`MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue`,
`ArrowheadResolution_InstanceInhMarkedArrowCell_PreferredOverMasterValue`,
`ArrowheadResolution_2DShapeInhMarkedArrowCell_DefersToMasterValue`

Proves an instance's own literal (non-`Inh`, present) cell value takes precedence over the
Master's same-named cell, and proves an instance cell that is absent entirely falls through to the
Master's resolved value for that cell name. Milestone 10 adds a deliberate, narrowly-scoped
exception to this rule for the nine transform cells `PinX`/`PinY`/`Width`/`Height`/`LocPinX`/
`LocPinY`/`Angle`/`FlipX`/`FlipY`, applied **only to a 1-D (connector) shape** (both a `BeginX` and
an `EndX` cell present - the same detection convention `VsdxDocument.Groups.cs`'s own
`ConnectorEndpoints` resolution uses): `MasterInheritance_
InstanceInhMarkedTransformCell_PreferredOverMasterValue` proves a 1-D connector instance's own
`F="Inh"`-marked `PinX` cell still wins over the Master's own distinct `PinX` value (unlike the
generic rule above), `MasterInheritance_InstanceInhMarkedNonTransformCell_StillDefersToMasterValue`
proves an `F="Inh"`-marked _non_-transform cell (`FillForegnd`) still defers to the Master's own
literal value exactly as before (confirming the exception did not widen beyond its intended nine
cell names), and `MasterInheritance_2DShapeInhMarkedTransformCell_DefersToMasterValue` proves a
2-D shape (no `BeginX`/`EndX` cell) whose own `PinX` is marked `F="Inh"` - a legitimate, never
locally overridden full Master-inheritance-of-position scenario - still defers entirely to the
Master's own `PinX`, unaffected by the transform-cell overlay. This exception was discovered
during this milestone's own root-cause investigation of Bug 2 (missing connector lines): the
stroke hairline floor (see `CanvasNetVsdx-VsdxDocument-MinimumVisibleStrokeWidth` below) alone did
not resolve `60973.vsdx`'s missing connector-line symptom against the Visio-reference PNG, because
the generic merge rule was also collapsing connector shape `802`'s resolved position to its
Master's own small, unrelated template-local corner; re-inspecting the raw XML confirmed the
instance's own `PinX`/`PinY` cells were themselves already correct (baked from that instance's own
`BeginX`/`BeginY`/`EndX`/`EndY` endpoints) but marked `F="Inh"`, so the generic rule discarded them

- a deviation beyond this milestone's originating plan report's own stroke-width-only diagnosis,
confirmed resolved via the external smoke-test visual-comparison procedure (§6) after the
refinement. Milestone 10 retry 1 (quality Finding #3) subsequently narrowed an initial, unscoped
implementation that applied this exception to every shape type - including 2-D shapes, for which
no test previously proved the exception was safe - to the 1-D-only scope this section now
documents and tests; a full `poi-fixtures` corpus re-render after the narrowing confirmed no
regression to either `60973.vsdx`'s connector lines or the other fidelity-improving fixtures
(`44501e.vsdx`, `60489.vsdx`) quality's own review had separately confirmed, since both of those
fixtures' affected shapes are themselves 1-D connectors. **Milestone 11 confirmed-but-deferred
limitation note**: this milestone's own real-world-corpus validation traced two independent
findings - `60973.vsdx`'s rack-mount frame container, and `44501e.vsdx`'s connector-label shape
`ID='45'` "end1_name" - to the 2-D-exclusion rule itself (not the 1-D-only overlay's own scope)
discarding a legitimately-cached, genuinely different instance position in favor of the Master's
unrelated template-local position. No safe, narrowly-scoped heuristic distinguishing this
counter-example from the already-locked `MasterInheritance_
2DShapeInhMarkedTransformCell_DefersToMasterValue` legitimate-2-D-inheritance case was identified
within this milestone's scope; see `canvas-net-vsdx.md`'s Design Constraints section for this
deferred limitation's own documented entry. No test or code change was made for this limitation
this milestone.

**Milestone 11 quality-retry (retry 1) note**: this cycle generalized the transform-cell overlay's
own helper (renamed `PreferInstanceCells(merged, instanceCells, names)`, taking any cell-name
array rather than a fixed list) and extended it to a new `ArrowCellNames` array
(`BeginArrow`/`EndArrow`/`BeginArrowSize`/`EndArrowSize`), applied through the same existing 1-D-
only gate. `ArrowheadResolution_InstanceInhMarkedArrowCell_PreferredOverMasterValue` proves a 1-D
connector instance's own `F="Inh"`-marked `EndArrow`/`EndArrowSize` cells still win over the
Master's own distinct literal values, and `ArrowheadResolution_
2DShapeInhMarkedArrowCell_DefersToMasterValue` proves a 2-D shape's own `F="Inh"`-marked
`EndArrow` still defers to the Master's own value, unaffected by the overlay - mirroring the
existing transform-cell tests' own 1-D/2-D split. This extension is defensive, planned alongside
the geometry-row fix below rather than because any real-world fixture in this unit's corpus was
confirmed to require it: this cycle's own investigation of a related arrowhead-rendering quality
finding (`44501e.vsdx` page 1) traced both of that finding's observed symptoms to causes other
than a missing arrowhead-cell overlay - see `CanvasNetVsdx-VsdxDocument-ArrowheadRendering` below
for the full evidence trail. Overlaying an arrowhead cell required writing it back as a literal
(dropping any `"Inh"` marking it carried), unlike the transform-cell overlay: a first
implementation attempt that preserved the `Inh` marking verbatim was caught by this cycle's own new
tests failing (`ArrowheadResolution_InstanceInhMarkedArrowCell_PreferredOverMasterValue` resolved
to `None` instead of the expected `Arrow`), because `ResolveLineCellValue`'s own `TryGetLiteral`
check (`VsdxDocument.Paint.cs`) treats any `Inh`-marked cell as "not a genuine override, defer to
the StyleSheet chain instead" - the same convention `VsdxCellBagMerge.Merge` applies generically -
so a non-literal overlay would have had no observable effect on the resolved arrowhead at all.

#### CanvasNetVsdx-VsdxDocument-DeletedShapeExclusion: Shape-Level Del="1" Group-Child Exclusion

**Test**: `VsdxDocument_GroupChildDeletedStub_ExcludedFromResolvedShapeTree`

Proves a group child `<Shape Del="1">` stub (shape-level deletion, distinct from the row-level
`Del="1"` geometry-row marker covered by `CanvasNetVsdx-VsdxDocument-GeometryRowMerge` below) is
excluded from the resolved shape tree entirely via a synthetic fixture mirroring `60973.vsdx`'s
own structure - confirmed, via a controlled `git stash`-based A/B pixel diff of the external
smoke-test harness's rendered output, to eliminate the spurious duplicate placeholder text
(`"OOB: N/A"`/`"[B] Lo1: N/A"`/`"TS: N/A"`) `60973.vsdx` was rendering before this fix. **Scope
note (Milestone 10 retry 1, quality Finding #2)**: this fix resolves that specific stale-stub
clutter text only; it does **not** resolve the separate, deeper `AIRttt`/`AIRuuu` duplicate-text
defect also present in `60973.vsdx` (page shapes `791`/`854`), which is a distinct, pre-existing
defect confirmed via git-history bisection to render identically broken before Milestone 9,
before Milestone 10, and after all 4 Milestone 10 commits - see `canvas-net-vsdx.md`'s Design
Constraints section for that defect's own documented, deferred-limitation entry.

#### CanvasNetVsdx-VsdxDocument-GeometryRowMerge: Geometry-Row Merge by Matching IX, Cell-by-Cell (Replace, Delete, Inherit)

**Tests**: `MasterInheritance_GeometryRowDelete_RemovesMasterRowFromMergedGeometry`,
`MasterInheritance_InstanceGeometryRowWithoutDel_ReplacesMasterRowAtMatchingIndex`,
`MasterInheritance_GeometryRowPartialOverride_MergesCellByCellNotWholesale`,
`MasterInheritance_GeometryRowAllCellsInhButInstanceSizeDiffers_InstanceCoordinatesWin`,
`MasterInheritance_GeometryRowCellGenuinelyAbsentOnInstance_StillFallsThroughToMaster`

Proves an instance geometry row marked `Del="1"` removes the Master's row at the matching `IX`
entirely (not merely overriding it), and - closing this milestone's own replace-by-IX coverage
gap - proves an instance row at the same `IX` as a Master row, without `Del`, fully replaces that
row's values while every other, unmatched Master row at a different `IX` is still inherited
verbatim into the same merged subpath. **Milestone 11 note**: `MasterInheritance_
GeometryRowPartialOverride_MergesCellByCellNotWholesale` proves the merge is now cell-by-cell, not
whole-row-replacement - an instance row overriding only its own `X` cell (leaving `Y` absent)
still inherits the Master row's own `Y` cell rather than defaulting it to `0`. Confirmed necessary
against `60973.vsdx`'s rack-mount frame container and `44501e.vsdx`'s connector elbow routing,
whose own matched instance rows overrode only a subset of their own cells; the prior
whole-row-replacement implementation was silently discarding the Master row's other,
legitimately-inherited cell values, collapsing the un-overridden coordinate to its CLR default
rather than the Master's own intended value.

**Milestone 11 quality-retry (retry 1) note**: `MasterInheritance_
GeometryRowAllCellsInhButInstanceSizeDiffers_InstanceCoordinatesWin` proves the deeper regression
this quality-retry cycle fixed: an instance row whose own cells are all cached as `F="Inh"` (the
typical case for a formula-derived geometry coordinate, which is never authored as a literal) must
still have its own, per-instance-correct coordinates win over the Master row's own, differently-
scaled cached value - the pre-fix cell-by-cell merge above correctly handled a _literal_ instance
override (proven by the pre-existing partial-override test) but incorrectly treated an
`Inh`-marked instance cell identically to a genuinely absent one, silently discarding it.
Confirmed necessary against a real-world regression in `60489.vsdx`'s Shape `ID='114'` (an ellipse
instance glued to its Master via a distinct, larger `Width`/`Height`, every one of its own
geometry-row cells cached as `Inh`): the pre-fix rule substituted the Master's own smaller cached
radius for every row, fusing Shape 114 and its sibling Shape 122 (both distinctly-sized instances
of the same Master) into a single, wrongly-proportioned blob instead of two independently-sized
ellipses - confirmed fixed via a before/after visual comparison of the external smoke-test
harness's rendered `60489.vsdx` page 0 against the Visio-reference PNG (the two ellipses render
distinct and correctly sized after the fix). The identical root cause was independently confirmed,
during this cycle's investigation of a related quality finding, to also explain a previously-
unexplained floating, mispositioned, wrong-colored triangle artifact in `44501e.vsdx` (Shape
`ID='64'`, `NameU='Directions'`, a vestigial "Data Graphic callout icon" helper shape deliberately
collapsed to `Width="0"`/`Height="0"` on its own instance, whose own all-`Inh` geometry-row cells
fell through, pre-fix, to the Master's own non-zero template triangle geometry and its own
unrelated light-blue fill) - also confirmed fixed by the same before/after visual comparison
(the floating triangle no longer renders after the fix). `MasterInheritance_
GeometryRowCellGenuinelyAbsentOnInstance_StillFallsThroughToMaster` proves the companion,
non-regressing half of the fix: a cell genuinely absent from the instance row (never declared at
all, not merely `Inh`-cached) still correctly falls through to the Master row's own value.

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
`StyleResolution_LineColorTrans_ReducesResolvedStrokeAlpha`,
`StyleResolution_LineColorTrans_FullyTransparent_HasNoVisibleStroke`,
`StyleResolution_FillForegndTrans_FullyTransparent_HasNoVisibleFill`

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

**Risk disclosure and re-verification (Milestone 10 retry 1, quality Finding #1)**: an initial
quality review disputed this fix, claiming `github260.vsdx`'s literal `LineColorTrans="1"` shapes
(100% transparent) regressed a previously-bordered shape against the Visio reference. This claim
was independently re-investigated and found **not to reproduce**: direct pixel-scanning of the
actual `visio-reference/github260-page-0.png` at the exact coordinates of every literal
`*Trans="1"` shape in the document (confirmed to be a Lucidchart export convention - this fixture
carries no `theme1.xml`/QuickStyle part at all, so no theme-governance subsystem applies here)
shows none of them carries a visible border in real Visio; the specific shape quality's report
cited (`com.lucidchart.UMLStartBlock.1`, page `ID="1"`) is confirmed, by direct geometry
inspection, to render with neither fill nor stroke for a wholly separate, already-documented
reason (its `MasterShape="6"` inner shape's `Section N="Geometry"` is a single `MoveTo` followed
entirely by unimplemented `NURBSTo` rows - see `canvas-net-vsdx.md`'s Design Constraints section),
confirmed byte-identical across the pre-Bug-#3-fix and post-Bug-#3-fix commits. `StyleResolution_
LineColorTrans_FullyTransparent_HasNoVisibleStroke`/`StyleResolution_
FillForegndTrans_FullyTransparent_HasNoVisibleFill` were added to lock in the current,
re-verified-correct `V="1"` behavior; no production-code change was made to
`VsdxDocument.Paint.cs`.

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
`VsdxThemeResolutionTests`'s own remarks). **Milestone 11 confirmation note**: the external
smoke-test harness confirmed a second, independent real-world instance of the documented neutral-
fallback limitation (`canvas-net-vsdx.md`'s Design Constraints section): `60973.vsdx`'s
`Nwwww`/`Neeeee` rack-slot bars resolve a bare `THEMEVAL()` formula through this same
unresolvable-scheme path, rendering gray rather than Visio's own orange/blue fill. No code change
was made; recorded purely as confirmed evidence this documented gap is real and already correctly
tolerated at a second site.

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

#### CanvasNetVsdx-VsdxDocument-TextRunParsing: cp/pp Marker Interleaving and fld Elements Resolve Ordered Formatted Runs

**Tests**: `TextParsing_PpThenCpMarkers_BothRowIndicesAttached`,
`TextParsing_MultipleCpMarkers_EachStartsNewRunUntilNextMarkerOrEnd`,
`TextParsing_FldElement_NestedTextContentParsedAsLiteralRun`,
`TextParsing_FldElementInterleavedWithText_MergesIntoSurroundingRun`

Proves a `<Text>` element's `<pp>` (paragraph) marker followed by a `<cp>` (character) marker both
attach their own row index to the resulting run, and proves multiple `<cp>` markers each start a
new run that persists until the next marker or the end of the text, matching the format
reference's own §8.1 interleaving rule. **Milestone 11** adds `<fld>` (Field reference) element
coverage: proves a `<Text>` element consisting solely of a bare `<fld IX='0'>42 U</fld>` (mirroring
`60973.vsdx`'s `master32.xml` Shape ID='9' verbatim) parses the field's own nested, save-time-
cached text content as a single literal run rather than the text being silently dropped entirely
(the pre-fix behavior, confirmed against the real fixture before this change), and proves a
`<fld>` element interleaved between plain text segments merges into the surrounding run exactly
like a plain `XText` node would, introducing no spurious run boundary merely because the content
came from a field reference.

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
`Render_ConnectorWithUnrecognizedEndArrowIndex_DegradesGracefullyWithoutThrowing`,
`ArrowheadResolution_EndArrowIndex4_ResolvesToArrowStyle`,
`ArrowheadResolution_EndArrowIndex254_ResolvesToHollowTriangleStyle`

Proves a connector with a recognized `EndArrow` index paints additional ink beyond its own plain
line stroke (the arrowhead itself), and proves an unrecognized `EndArrow` index degrades to a
plain, unadorned line end rather than throwing - the same graceful-degradation convention already
established for `FillPattern`. **Milestone 11** adds coverage for two confirmed-in-use indices
found via a full-document scan of the `poi-fixtures` corpus: index `4` resolves to the existing
solid-filled arrow style (the same geometry as index `5`/`6`'s own family, not previously
exercised by a dedicated test), and index `254` - confirmed, via the external smoke-test harness
against `44501e.vsdx`'s connector arrowheads, to have been silently degrading to the unrecognized-
index plain-line-end fallback before this fix - resolves to a new hollow/unfilled-triangle
`VsdxArrowheadStyle`, matching the Visio-reference PNG's own unfilled triangular arrowhead
outline. **Accepted limitation (not a regression)**: the arrowhead's own painted size remains
proportional to the connector's resolved stroke width, a pre-existing, unchanged sizing design
this milestone did not revisit; at `44501e.vsdx`'s own thin stroke width the painted arrowhead is
visibly smaller than the Visio-reference PNG's own arrowhead, though now the correct hollow-
triangle shape rather than absent.

**Milestone 11 quality-retry (retry 1) investigation note**: a subsequent quality finding reported
`44501e.vsdx` page 1 rendering an unexpected floating triangle and several flat "dash" marks in
place of expected UML connector arrowheads. Real fixture access during this cycle conclusively
traced these to two distinct, previously-conflated causes, neither of which required any change to
this requirement's own scope: (1) the floating, mispositioned, wrong-colored triangle was the
`CanvasNetVsdx-VsdxDocument-GeometryRowMerge` regression (Shape `ID='64'`, `NameU='Directions'`) -
fully resolved as a side effect of that fix, confirmed via before/after visual comparison (the
triangle no longer renders); (2) the "dash" marks were traced, via direct resolver inspection, to
the "Pipe flow arrow" marker shapes (`Master='29'`), ordinary 2-D `Shape`-typed icons (no
`BeginX`/`EndX` cell pair) whose `EndArrow`/`EndArrowSize` cells already resolve correctly - the
dash appearance is solely because `VsdxDocument.Render.cs`'s arrowhead-decoration painting is
gated on `shape.ConnectorEndpoints is { } endpoints` (1-D connectors only, per
`VsdxDocument.Groups.cs`'s own convention), so a 2-D shape's correctly-resolved arrowhead is never
painted as a decoration, only its bare line geometry renders. Confirmed via `git stash`/`git stash
pop` differential rendering that this dash-mark symptom is identical with and without this cycle's
own fix - a deliberate, pre-existing architectural scope boundary (arrowhead decoration is scoped
to 1-D connectors only), not a regression introduced or worsened by Milestone 11's cell-merge
work, and not a defect in this requirement's own documented scope (which has never claimed to
decorate a 2-D shape's arrowhead cells). **Determination**: accepted, out-of-scope limitation (not
force-fixed); no code or test change was made to this requirement for this finding. The single
UML "Binary Association" connector carrying a genuine `EndArrow="254"` override (the "-includes"
association) was already correctly resolving via the existing `EndArrowIndex254` fix above and
continues to render correctly, confirmed in the same before/after comparison.

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

#### CanvasNetVsdx-VsdxDocument-HideTextSuppression: HideText Shapes Skip Own Text but Still Paint Fill/Stroke/Children

**Test**: `Render_ShapeWithHideTextCell_SuppressesOwnTextButNotFillOrStroke`

Proves a shape whose effective `HideText` cell resolves truthy paints no pixel of its own text run
ink while still painting its own fill and stroke unaffected, distinguishing this suppression from
the broader `NonPrinting` suppression above (which also skips the shape's own fill/stroke).
Confirmed against `60489.vsdx`'s actor-label shapes via the external smoke-test harness: before
this fix, a `HideText`-marked shape's own `HideText` cell was unconsulted, so its text ran painted
a duplicate, overlapping copy of a sibling shape's already-correctly-positioned label; after the
fix, the duplicate text disappears and the rendered page matches the Visio-reference PNG.

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
