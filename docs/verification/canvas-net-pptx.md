# System Verification Design

<!-- cspell:ignore ooxml pptx xfrm prst -->

This document describes the system-level verification strategy for CanvasNetPptx.

## Verification Approach

The CanvasNetPptx system is verified through system-level integration tests that exercise the
library as a whole from the perspective of a consumer. Tests instantiate the library using its
public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `PptxSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Pptx.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces requiring simulation - no HTTP calls,
databases, or remote services are involved. It does support local file-path-based I/O through its
public API (`Open(string path)` reads a `.pptx` package from a local file path), but system tests
exercise this entirely through hand-authored, in-memory `ZipArchive`-built packages passed via
`Open(Stream)` rather than a file-based fixture - no file-based fixture exists for this system
(see _PptxDocument Unit Verification Design_, `canvas-net-pptx/pptx-document.md`, for why).
System tests call the public API directly with controlled inputs and verify returned values and
thrown exceptions.

## System-Level Test Scenarios

### Integration: Pptx Open Succeeds On Well Formed Package

**Test**: `CanvasNetPptx_SystemIntegration_PptxOpen_SucceedsOnWellFormedPackage`

Exercises end-to-end system behavior for `Open`/package-layer resolution: opens a minimal,
well-formed, in-memory OPC package through the public API, then asserts `SlideCount`/`SlideSize`
resolve to the expected values declared by the package's `ppt/presentation.xml`. Confirms that
opening a well-formed `.pptx`-shaped package and navigating its content-type/relationship graph
integrate correctly through the system's own public entry point, and serves as the shared
platform-proof test referenced by every `CanvasNetPptx-Platform-*` requirement.

### Integration: Pptx Open Validation Null Null Stream Throws Argument Null Exception

**Test**: `CanvasNetPptx_SystemIntegration_PptxOpenValidationNull_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for null-argument validation: calls the public `Open(Stream)`
API with a null stream. Asserts `ArgumentNullException` is thrown, confirming the documented
validation contract is honored at the system's own public entry point, before any parsing is
attempted.

### Integration: Pptx Open Validation Empty Path Empty Path Throws Argument Exception

**Test**: `CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptyPath_EmptyPathThrowsArgumentException`

Exercises end-to-end system behavior for empty-path-argument validation: calls the public
`Open(string)` API with `string.Empty`. Asserts `ArgumentException` is thrown, confirming the
documented validation contract is honored at the system's own public entry point, before any
file access is attempted.

### Integration: Pptx Open Presentation Slide Count And Size Resolve End To End

**Test**: `CanvasNetPptx_SystemIntegration_PptxOpenPresentation_SlideCountAndSizeResolveEndToEnd`

Exercises end-to-end system behavior for Phase 1b's presentation parsing: opens a two-slide,
well-formed, in-memory `.pptx`-shaped package through the public API and asserts `SlideCount`
and `SlideSize` resolve to the package's declared `<p:sldIdLst>`/`<p:sldSz>` values, confirming
the presentation model integrates correctly through the system's own public entry point.

### Integration: Pptx Open Validation Empty Slide List Throws Invalid Data Exception

**Test**: `CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptySlideList_ThrowsInvalidDataException`

Exercises end-to-end system behavior for presentation validation: opens a package whose
`<p:sldIdLst>` is declared but empty, asserting `InvalidDataException` is thrown from the
public `Open` entry point, confirming the fail-closed validation contract for a non-navigable
presentation is honored end-to-end.

### Integration: Geometry And Paint Freeform Shape Resolves End To End

**Test**: `CanvasNetPptx_SystemIntegration_GeometryAndPaint_FreeformShapeResolvesEndToEnd`

Exercises end-to-end system behavior for Phase 1c's shape geometry and paint resolution: opens a
full presentation package whose one slide contains a single freeform `<p:sp>` declaring a rotated
`<a:xfrm>`, an `<a:prstGeom prst="roundRect">`, a theme-scheme-color `<a:solidFill>`, and an
`<a:ln>` stroke. Confirms the geometry transform, preset-geometry resolution, and fill/stroke
paint resolution all compose correctly when driven through the full package-load path (slide ->
layout -> master -> theme), not just from hand-built fragments.

### Integration: Geometry And Paint Group Shape Child Transform Composes End To End

**Test**: `CanvasNetPptx_SystemIntegration_GeometryAndPaint_GroupShapeChildTransformComposesEndToEnd`

Exercises end-to-end system behavior for Phase 1c's group child-coordinate-space transform: opens
a full presentation package whose one slide contains a `<p:grpSp>` (with its own `<a:xfrm>`
declaring both the group's parent-space placement and its child coordinate space) wrapping a
single child `<p:sp>`. Confirms the composed group/child transform resolves correctly end-to-end.

### Integration: Text Layout And Render Title Placeholder Resolves Master Title Style End To End

**Test**: `CanvasNetPptx_SystemIntegration_TextLayoutAndRender_TitlePlaceholderResolvesMasterTitleStyleEndToEnd`

Exercises end-to-end system behavior for Phase 1d's text property inheritance and layout: opens a
full presentation package whose one slide contains a `title` placeholder shape with a `<p:txBody>`
run declaring no font size of its own. Confirms the run's effective font size resolves through the
real slide -> layout -> master placeholder-matching chain all the way to the master's own
`<p:titleStyle>`, and that the resulting text body lays out and paints without error.

### Integration: Text Layout And Render Non Placeholder Shape Resolves Master Other Style End To End

**Test**: `CanvasNetPptx_SystemIntegration_TextLayoutAndRender_NonPlaceholderShapeResolvesMasterOtherStyleEndToEnd`

Exercises end-to-end system behavior for Phase 1d's text property inheritance: opens a full
presentation package whose one slide contains an ordinary, non-placeholder text box. Confirms the
`placeholderType: ""` sentinel convention correctly selects the master's `<p:otherStyle>` bucket
(not `<p:bodyStyle>`) when driven through a real, non-placeholder shape resolved from the full
package-load path.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current (through Phase 1d)
CanvasNetPptx feature set: opening a well-formed OOXML/`.pptx` package, resolving its content-type
and relationship graph, resolving its declared slide count/size (Phase 1b), resolving DrawingML
shape geometry and paint - including group child-transform composition (Phase 1c), and resolving
DrawingML text property inheritance, layout, and rendering (Phase 1d) - plus validating the
documented argument- and structural-validation contracts. A full per-slide public `Render` API,
non-placeholder (freeform) shape _enumeration_ from a slide's full `<p:spTree>`, bullets/
numbering, and text clipping on overflow are not covered because none is implemented yet - later
phases will extend this document's scenarios as that content is added.
