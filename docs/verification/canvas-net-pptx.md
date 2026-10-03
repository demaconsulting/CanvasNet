# System Verification Design

<!-- cspell:ignore ooxml pptx -->

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
well-formed, in-memory OPC package through the public API, then uses the internal
`ResolveRelationship`/`ResolvePart` methods to follow the package-level relationship to
`ppt/presentation.xml` and resolve its overridden content type. Confirms that opening a
well-formed `.pptx`-shaped package and navigating its content-type/relationship graph integrate
correctly through the system's own public entry point, and serves as the shared platform-proof
test referenced by every `CanvasNetPptx-Platform-*` requirement.

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

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current CanvasNetPptx
feature set: opening a well-formed OOXML/`.pptx` package, resolving its content-type and
relationship graph, resolving its declared slide count/size (Phase 1b), and validating the
documented argument- and structural-validation contracts. No shape geometry/paint rendering,
non-placeholder shape parsing, font loading, or rendering surface is covered because none is
implemented yet - later phases will extend this document's scenarios as that content is added.
