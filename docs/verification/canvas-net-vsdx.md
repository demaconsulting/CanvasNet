# System Verification Design

<!-- cspell:ignore vsdx Visio davehoward jgreywolfvsdxjs Foregnd NURBS -->

This document describes the system-level verification strategy for CanvasNetVsdx.

## Verification Approach

The CanvasNetVsdx system is verified through system-level integration tests that exercise the
library as a whole from the perspective of a consumer, mirroring the already-established approach
of _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`) and _CanvasNetSvg System
Verification_ (`canvas-net-svg.md`). Every test drives the library purely through its public API
(`Open(string)`/`Open(Stream)`, `PageCount`, `GetPageSize`, and the `Render` overloads) and asserts
on observable outputs, with no mocking or stubbing required at the system level. Most scenarios
are proven against the real `.vsdx` fixture corpus staged in
`test/DemaConsulting.CanvasNet.Vsdx.Tests/VsdxFixtures/` (14 real-world files sourced from two
third-party test suites - see that folder's own `README.md` for provenance/licensing), confirming
the full resolution pipeline (package/part resolution, Master/MasterShape inheritance, StyleSheet
chain walk, geometry build, transform, paint, text, connectors, groups, and rendering) composes
correctly against genuine Visio-authored content, not just hand-authored fragments. A minority of
scenarios (argument validation, a dangling-glue-target tolerance case, and a few Master-
inheritance/StyleSheet-chain compositions not conveniently reproduced by any single staged fixture)
instead use small, hand-authored, in-memory packages built via the shared `VsdxTestPackages`
helper already established by the unit-level test tier (see _VsdxDocument Unit Verification
Design_, `canvas-net-vsdx/vsdx-document.md`).

System tests reside in `VsdxSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Vsdx.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Fixtures**: 14 real-world `.vsdx` files staged in-project under `VsdxFixtures/` (copied to the
  test output directory by the `.csproj`'s own `<None Include="VsdxFixtures\**">` wiring) plus
  hand-authored, in-memory packages for scenarios not conveniently reproduced by a real sample
- **Isolation**: Each test method either opens its own staged fixture file or constructs its own
  in-memory package; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces requiring simulation - no HTTP calls,
databases, or remote services are involved. It does support local file-path-based I/O through its
public API (`Open(string path)` reads a `.vsdx` package from a local file path), and system tests
exercise this directly against the staged real-world fixture corpus (via `AppContext.BaseDirectory`-
relative resolution - see `VsdxDocumentTests.cs`'s own `FixturesDirectory`/`FixturePath` helpers,
reused verbatim by the system tier). A handful of scenarios instead construct an in-memory package
and open it via `Open(Stream)`, exactly as the unit-level tier already does, when no staged fixture
conveniently isolates the scenario under test (for example, a specific Master-inheritance
composition or an intentionally dangling glue target). System tests call the public API directly
with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Vsdx Open Succeeds On Well Formed Package

**Test**: `CanvasNetVsdx_SystemIntegration_VsdxOpen_SucceedsOnWellFormedPackage`

Exercises end-to-end system behavior for `Open`/package-layer resolution: opens the real
`davehoward-test1.vsdx` fixture through the public API, then asserts `PageCount` resolves to the
fixture's own declared page count and that its first page's shapes resolve. Confirms that opening
a well-formed `.vsdx`-shaped package and navigating its content-type/relationship graph integrate
correctly through the system's own public entry point, and serves as the shared platform-proof
test referenced by every `CanvasNetVsdx-Platform-*` requirement.

### Integration: Vsdx Open Validation Null Null Stream Throws Argument Null Exception

**Test**: `CanvasNetVsdx_SystemIntegration_VsdxOpenValidationNull_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for null-argument validation: calls the public `Open(Stream)`
API with a null stream. Asserts `ArgumentNullException` is thrown, confirming the documented
validation contract is honored at the system's own public entry point, before any parsing is
attempted.

### Integration: Vsdx Open Validation Empty Path Empty Path Throws Argument Exception

**Test**: `CanvasNetVsdx_SystemIntegration_VsdxOpenValidationEmptyPath_EmptyPathThrowsArgumentException`

Exercises end-to-end system behavior for empty-path-argument validation: calls the public
`Open(string)` API with `string.Empty`. Asserts `ArgumentException` is thrown, confirming the
documented validation contract is honored at the system's own public entry point, before any file
access is attempted.

### Integration: Page Count Returns Declared Page Count

**Test**: `CanvasNetVsdx_SystemIntegration_PageCount_ReturnsDeclaredPageCount`

Exercises end-to-end system behavior for page-index parsing: opens the real `davehoward-test1.vsdx`
fixture and asserts `PageCount` equals its known, independently-confirmed value (3), read directly
from the fixture's own `pages.xml`.

### Integration: Get Page Size Returns Declared Size In Emu

**Test**: `CanvasNetVsdx_SystemIntegration_GetPageSize_ReturnsDeclaredSizeInEmu`

Exercises end-to-end system behavior for page-size resolution: opens the real
`davehoward-test1.vsdx` fixture and asserts `GetPageSize(0)` resolves the exact page name and
width/height in EMU declared by the fixture's own `pages.xml`.

### Integration: Get Page Size Out Of Range Index Throws Argument Out Of Range Exception

**Test**: `CanvasNetVsdx_SystemIntegration_GetPageSize_OutOfRangeIndexThrowsArgumentOutOfRangeException`

Exercises end-to-end system behavior for argument validation: calls `GetPageSize` with an index
equal to `PageCount` (one past the last valid index). Asserts `ArgumentOutOfRangeException` is
thrown, confirming the documented validation contract at the system's own public entry point.

### Integration: Geometry Resolves Common Row Types

**Test**: `CanvasNetVsdx_SystemIntegration_Geometry_ResolvesCommonRowTypes`

Exercises end-to-end system behavior for geometry-row resolution: opens the real
`davehoward-test3-house.vsdx` fixture and asserts at least one shape resolves at least one
geometry section with at least one non-empty subpath, confirming `MoveTo`/`LineTo`/`ArcTo`-family
rows compose into a usable, paintable path when driven through the full package-load path.

### Integration: Geometry Unrecognized Row Type Is Skipped Not Thrown

**Test**: `CanvasNetVsdx_SystemIntegration_Geometry_UnrecognizedRowTypeIsSkippedNotThrown`

Exercises end-to-end system behavior for the geometry resolver's tolerant-skip contract: opens a
hand-authored, in-memory package whose single shape's `Geometry` section includes a synthetic
`EllipticalArcTo` row (a row type this package never converts to a Bezier approximation - see the
Milestone 8 correction note on `CanvasNetVsdx-VsdxDocument-GeometryUnrecognizedRowSkip`) interleaved
with recognized `MoveTo`/`LineTo` rows. Asserts shape resolution completes without throwing,
confirming the documented "skip, don't throw" contract end-to-end.

### Integration: Transform Rotated Shape Matches Expected Outline

**Test**: `CanvasNetVsdx_SystemIntegration_Transform_RotatedShapeMatchesExpectedOutline`

Exercises end-to-end system behavior for the unified shape-local-to-page-space transform: opens the
real `davehoward-test11-rotate.vsdx` fixture and maps its own rotated Shape `ID='1'`'s local origin
and opposite corner through its resolved transform. Asserts both points match the same
independently (Python, double precision) computed coordinates already proven by
`VsdxTransformTests.Transform_WorkedRotationExample_MatchesIndependentlyComputedCoordinates`'s own
hand-authored reproduction of this fixture's cells, confirming the real fixture and the unit-level
synthetic reproduction agree.

### Integration: Master Inheritance Instance Cell Overrides Master

**Test**: `CanvasNetVsdx_SystemIntegration_MasterInheritance_InstanceCellOverridesMaster`

Exercises end-to-end system behavior for Master/MasterShape cell-merge precedence: opens a
hand-authored package whose page-level shape instance supplies its own literal `FillForegnd` cell
while its Master supplies a different one. Asserts the resolved paint uses the instance's own
color, confirming "instance literal wins" end-to-end.

### Integration: Master Inheritance Inherited Cell Falls Through To Master

**Test**: `CanvasNetVsdx_SystemIntegration_MasterInheritance_InheritedCellFallsThroughToMaster`

Exercises end-to-end system behavior for Master/MasterShape cell-merge fallthrough: opens a
hand-authored package whose page-level shape instance supplies no transform cells of its own at
all. Asserts the resolved transform's `Width`/`Height` match the Master's own values verbatim,
confirming full-cell-set inheritance end-to-end.

### Integration: Master Inheritance Deleted Geometry Row Is Omitted

**Test**: `CanvasNetVsdx_SystemIntegration_MasterInheritance_DeletedGeometryRowIsOmitted`

Exercises end-to-end system behavior for the geometry-row-delete pattern: opens a hand-authored
package whose instance shape marks one of its Master's geometry rows `Del="1"` at a matching `IX`.
Asserts the merged geometry's single subpath carries no surviving command for that row, confirming
the row-delete (not override) semantics end-to-end.

### Integration: Style Resolution Walks Chain To Literal Value

**Test**: `CanvasNetVsdx_SystemIntegration_StyleResolution_WalksChainToLiteralValue`

Exercises end-to-end system behavior for the StyleSheet chain walk: opens a hand-authored package
declaring a three-level `FillStyle` parent-pointer chain (shape -> StyleSheet 2 -> StyleSheet 1,
which carries the literal `FillForegnd` -> StyleSheet 0). Asserts the resolved fill color matches
StyleSheet 1's own literal value, confirming a multi-level chain walk resolves correctly end-to-end.

### Integration: Style Resolution Direct Override Wins Over Style Sheet

**Test**: `CanvasNetVsdx_SystemIntegration_StyleResolution_DirectOverrideWinsOverStyleSheet`

Exercises end-to-end system behavior for direct-cell precedence: opens a hand-authored package
whose shape both references a `FillStyle` StyleSheet carrying a literal `FillForegnd` and supplies
its own, different literal `FillForegnd`. Asserts the resolved fill color is the shape's own value,
confirming direct-cell precedence over the entire StyleSheet chain end-to-end.

### Integration: Color Fill Resolves Built In Palette Index And Hex Colors

**Test**: `CanvasNetVsdx_SystemIntegration_ColorFill_ResolvesBuiltInPaletteIndexAndHexColors`

Exercises end-to-end system behavior for color resolution: opens a hand-authored package whose
shape carries a built-in-palette-index `FillForegnd` (`"2"`, documented as pure red) and a direct
hex-literal `LineColor` (`"#abcdef"`). Asserts both resolve to the expected concrete colors,
confirming both of this unit's two real resolution paths (see the Milestone 8 correction note on
`CanvasNetVsdx-VsdxDocument-ColorTableResolution` - no document-declared `<Colors>` table is ever
parsed by this package) work end-to-end.

### Integration: Color Fill Themed Cell Falls Back To Neutral Default When No Theme

**Test**: `CanvasNetVsdx_SystemIntegration_ColorFill_ThemedCellFallsBackToNeutralDefaultWhenNoTheme`

Exercises end-to-end system behavior for the `"Themed"` sentinel's graceful-degradation path: opens
a hand-authored package with no theme part at all whose shape's `FillForegnd` is the literal string
`"Themed"`. Asserts resolution completes without throwing and the resolved color is fully opaque,
confirming the documented neutral-default fallback end-to-end.

### Integration: Color Fill Unsupported Fill Pattern Degrades To Solid Fill

**Test**: `CanvasNetVsdx_SystemIntegration_ColorFill_UnsupportedFillPatternDegradesToSolidFill`

Exercises end-to-end system behavior for the `FillPatternDeferral` requirement's actual, corrected
behavior: opens a hand-authored package whose shape declares a non-solid, non-zero `FillPattern`
value (`"25"`). Asserts the shape still paints as solid-filled using its own `FillForegnd` color -
confirming the graceful-degradation behavior (not the originally-planned, never-implemented
`VsdxUnsupportedFeatureException` throw - see the Milestone 8 correction note on this requirement)
end-to-end.

### Integration: Text Renders Shape Label

**Test**: `CanvasNetVsdx_SystemIntegration_Text_RendersShapeLabel`

Exercises end-to-end system behavior for text rendering: opens a hand-authored package whose shape
carries a literal `<Text>Hello</Text>` element, then renders the page. Asserts at least one
non-transparent pixel is painted, confirming the text run/layout/paint pipeline composes end-to-end
through the public `Render` API.

### Integration: Text Positions Text Box Relative To Shape Transform

**Test**: `CanvasNetVsdx_SystemIntegration_Text_PositionsTextBoxRelativeToShapeTransform`

Exercises end-to-end system behavior for text-box positioning: opens the real
`davehoward-test6-shape-properties.vsdx` fixture and asserts at least one shape resolves non-empty
text runs together with both a resolved text-box transform and a resolved shape transform,
confirming the text box positions relative to its own shape's transform when driven through a real,
Master/StyleSheet-resolved fixture.

### Integration: Connectors Renders Straight Connector Between Shapes

**Test**: `CanvasNetVsdx_SystemIntegration_Connectors_RendersStraightConnectorBetweenShapes`

Exercises end-to-end system behavior for connector rendering: opens the real
`davehoward-test4-connectors.vsdx` fixture and renders its first page. Asserts at least one
non-transparent pixel is painted, confirming connector glue resolution, transform, and stroke paint
compose end-to-end against a real, whole-shape-pin-glued connector fixture.

### Integration: Connectors Dangling Glue Target Is Skipped Not Thrown

**Test**: `CanvasNetVsdx_SystemIntegration_Connectors_DanglingGlueTargetIsSkippedNotThrown`

Exercises end-to-end system behavior for the Connects resolver's tolerant-skip contract: opens a
hand-authored package whose single `<Connect>` entry targets a `ToSheet` ID that does not exist on
the page. Asserts page-shape resolution completes without throwing and the connector's own
`Connects` collection is empty (the dangling entry is skipped, not the whole page), confirming the
documented fault-tolerance contract end-to-end.

### Integration: Groups Composes Nested Group Child Transform

**Test**: `CanvasNetVsdx_SystemIntegration_Groups_ComposesNestedGroupChildTransform`

Exercises end-to-end system behavior for recursive group/nested-shape resolution: opens the real
`davehoward-test10-nested-shapes.vsdx` fixture's own 3-level-deep plain nested group (top-level
Group `ID='7'` -> nested Group `ID='3'` -> leaf Shape `ID='1'`). Asserts the full `Parent` chain is
wired correctly and every level resolves a usable transform, confirming arbitrary-depth group
composition end-to-end against a real fixture.

### Integration: Render Pixel Dimensions Overload Paints Expected Surface

**Test**: `CanvasNetVsdx_SystemIntegration_Render_PixelDimensionsOverload_PaintsExpectedSurface`

Exercises end-to-end system behavior for the public pixel-dimensions `Render` overload: opens the
real `davehoward-test3-house.vsdx` fixture and renders its first page at an explicit pixel size.
Asserts the resulting surface has the requested dimensions and at least one non-transparent pixel,
confirming the full render pipeline composes end-to-end against a real, multi-shape fixture.

### Integration: Render Dpi Overload Computes Expected Pixel Dimensions

**Test**: `CanvasNetVsdx_SystemIntegration_Render_DpiOverload_ComputesExpectedPixelDimensions`

Exercises end-to-end system behavior for the public DPI `Render` overload: opens the real
`davehoward-test1.vsdx` fixture and renders its first page by DPI. Asserts the resulting surface's
pixel dimensions match `round(pageSizeInches * dpi)`, computed independently from the fixture's own
declared EMU page size, confirming the documented EMU-to-pixel conversion end-to-end.

### Integration: Render Invalid Dimensions Throws Argument Out Of Range Exception

**Test**: `CanvasNetVsdx_SystemIntegration_Render_InvalidDimensionsThrowsArgumentOutOfRangeException`

Exercises end-to-end system behavior for argument validation: calls the pixel-dimensions `Render`
overload with a zero width and, separately, a zero height. Asserts `ArgumentOutOfRangeException` is
thrown in both cases, confirming the documented validation contract at the system's own public
entry point.

### Integration: Whole Document Opens Resolves And Renders Every Page

**Test**: `CanvasNetVsdx_SystemIntegration_WholeDocument_OpensResolvesAndRendersEveryPage`
(a `[Theory]` iterating all 14 staged real-world fixtures)

The strongest single end-to-end proof this milestone adds: for every staged real-world `.vsdx`
fixture, opens the package, resolves every page's shapes, and renders every page at a fixed pixel
size. Asserts each step completes without throwing and every rendered surface has the requested
dimensions. Complements `VsdxRenderFixtureTests`'s own per-category whole-document render tests
(one fixture per shape-family, each additionally asserting a specific painted pixel) with
full-corpus "opens, resolves, and renders cleanly" coverage in one place.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current CanvasNetVsdx
feature set: opening a well-formed `.vsdx` package, resolving its content-type/relationship graph
and declared page count/size (Platform/PageMetadata), resolving geometry rows and the unified
shape-local-to-page-space transform (Geometry/Transform), resolving Master/MasterShape cell and
geometry-row inheritance (MasterInheritance), walking the StyleSheet chain (StyleResolution),
resolving built-in-palette-index/hex/themed colors and the documented FillPattern
graceful-degradation behavior (ColorFill), resolving and rendering text (TextRendering), resolving
and rendering connectors including dangling-glue-target tolerance (Connectors), composing
arbitrarily-nested group child transforms (Groups), and rendering a full page through the public
`Render` API (RenderSurface) - plus validating the documented argument-validation contracts.
Every requirement in `docs/reqstream/canvas-net-vsdx.yaml` and
`docs/reqstream/canvas-net-vsdx/platform-requirements.yaml` links to at least one passing test
listed above, and the whole-document end-to-end scenario renders every staged real-world sample
without an unhandled exception. A document-level `<Colors>`/`<FaceNames>` custom color/font table
and a best-effort linear-gradient fill approximation remain unimplemented and explicitly out of
scope (see the Milestone 8 correction notes on `CanvasNetVsdx-ColorFill` and the design
documents), as does true `EllipticalArcTo`/`NURBSTo`/`InfiniteLine`-to-Bezier conversion (these row
types are tolerantly skipped, not converted); a future, corpus-driven hardening pass may add this
content if a real-world fixture is found to require it.
