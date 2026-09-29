# System Verification Design

<!-- cspell:ignore rasterizing -->

This document describes the system-level verification strategy for CanvasNetSvg.

## Verification Approach

The CanvasNetSvg system is verified through system-level integration tests that
exercise the library as a whole from the perspective of a consumer. Tests instantiate the library
using its public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `SvgSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Svg.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces requiring simulation - no HTTP calls,
databases, or remote services are involved. It does support local file-path-based I/O through its
public API (`Load(string path, ...)`/`GetInfo(string path)` read an SVG document from a local
file path), which system tests exercise using in-memory streams in preference to on-disk fixtures
wherever a scenario does not specifically target the file-path overloads. System tests call the
public API directly with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Svg Load Returns Expected Pixel

**Test**: `CanvasNetSvg_SystemIntegration_SvgLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `SvgCodec` unit: rasterizes a minimal SVG document
(a `viewBox` matching the requested raster exactly, containing one rectangle filled with a
distinct, fully opaque color via a plain `fill` presentation attribute) into a new `Surface`
through the public API. Asserts the rasterized pixel at the rectangle's center exactly matches
the source color, confirming the system's public SVG load API integrates correctly with the
`CanvasNet` system's `Surface`/`Geometry`/`Drawing` units it depends on. Unlike the BMP/PNG/TIFF/
JPEG system-integration scenarios in _CanvasNet System Verification_ (`canvas-net.md`), there is
no "Save" half to this round-trip: `SvgCodec` is decode/rasterize-only.

### Integration: Svg Load With Css Style Element Returns Expected Pixel

**Test**: `CanvasNetSvg_SystemIntegration_SvgLoadWithCssStyleElement_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `SvgCodec` unit's CSS engine: rasterizes an SVG
document whose only source of color is a `<style>` element's class selector rule (`.solid {
fill: rgb(11,22,33); }`) applied to a `rect` that itself carries no `fill` attribute at all,
integrating style-element parsing, selector matching, and cascade resolution with the rest of the
rendering pipeline, through the public API. Asserts the rasterized pixel at the rectangle's
center exactly matches the stylesheet-declared color, confirming the CSS engine is correctly
wired into the same rasterization pipeline the plain-attribute scenario above exercises.

### Integration: Svg Marker Rendering Renders Marker At Line End

**Test**: `CanvasNetSvg_SystemIntegration_SvgMarkerRendering_RendersMarkerAtLineEnd`

Exercises end-to-end system behavior for marker rendering: rasterizes a `line` element whose
`marker-end` references a `marker` element, through the public API. Asserts the marker's own
content is visible past the line's end vertex, confirming marker resolution, placement, and
rendering integrate correctly with the rest of the pipeline.

### Integration: Svg Filter Rendering Applies Filter Chain To Shape

**Test**: `CanvasNetSvg_SystemIntegration_SvgFilterRendering_AppliesFilterChainToShape`

Exercises end-to-end system behavior for filter rendering: rasterizes a shape referencing a
multi-primitive filter chain (`feFlood`/`feComposite`/`feGaussianBlur`/`feMerge`), through the
public API. Asserts the shape's own fill remains correctly composited at its center, confirming
the filter pipeline integrates correctly with shape rendering.

### Integration: Svg Clip Path Rendering Clips Content To Referenced Shape

**Test**: `CanvasNetSvg_SystemIntegration_SvgClipPathRendering_ClipsContentToReferencedShape`

Exercises end-to-end system behavior for `clipPath` rendering: rasterizes a shape whose
`clip-path` references a `clipPath` element, through the public API. Asserts content is visible
inside the clip region and absent outside it, confirming clip-path resolution and hard-clipping
integrate correctly with shape rendering.

### Integration: Svg Pattern Rendering Tiles Pattern Across Shape

**Test**: `CanvasNetSvg_SystemIntegration_SvgPatternRendering_TilesPatternAcrossShape`

Exercises end-to-end system behavior for `pattern` rendering: rasterizes a shape whose `fill`
references a `pattern` element, through the public API. Asserts the pattern's own tile content is
visible inside the filled shape's bounding box, confirming pattern resolution and tiling integrate
correctly with shape rendering.

### Integration: Svg Image Rendering Renders Embedded Raster Image

**Test**: `CanvasNetSvg_SystemIntegration_SvgImageRendering_RendersEmbeddedRasterImage`

Exercises end-to-end system behavior for `image` rendering: rasterizes an `image` element whose
`href` is a base64-encoded PNG data URI, through the public API. Asserts the decoded pixel is
visible at the element's placement rect and absent outside it, confirming the `PngCodec`
decode path integrates correctly with SVG image placement.

### Integration: Svg View Box Fitting Letterboxes Wide View Box

**Test**: `CanvasNetSvg_SystemIntegration_SvgViewBoxFitting_LetterboxesWideViewBox`

Exercises end-to-end system behavior for `viewBox` fitting: rasterizes a wide (landscape)
`viewBox` into a square raster, through the public API. Asserts the centered content band is
opaque and the letterbox bars above/below are transparent, confirming the default
`preserveAspectRatio` fit transform integrates correctly with rasterization.

### Integration: Svg Percentage Geometry Resolution Resolves Width Percentage

**Test**: `CanvasNetSvg_SystemIntegration_SvgPercentageGeometryResolution_ResolvesWidthPercentage`

Exercises end-to-end system behavior for percentage geometry resolution: rasterizes a `rect`
whose `width` is a percentage value, through the public API. Asserts the rendered extent matches
the viewport-relative resolved width, confirming percentage resolution integrates correctly with
shape geometry.

### Integration: Svg Get Info Returns View Box Dimensions

**Test**: `CanvasNetSvg_SystemIntegration_SvgGetInfo_ReturnsViewBoxDimensions`

Exercises end-to-end system behavior for `GetInfo`: queries an SVG document's intrinsic size
through the public `GetInfo` API, without rasterizing it. Asserts the reported width/height match
the document's `viewBox`, and channel count/alpha presence match the documented contract.

### Integration: Svg Text Rendering Renders Glyph At Expected Position

**Test**: `CanvasNetSvg_SystemIntegration_SvgTextRendering_RendersGlyphAtExpectedPosition`

Exercises end-to-end system behavior for text rendering: rasterizes a `text` element using a
caller-supplied font, through the public API. Asserts the glyph outline renders at the expected
position, confirming `SvgCodec`'s text layout integrates correctly with the `CanvasNet` system's
`Fonts`/`Drawing` units.

### Integration: Svg Font Weight Style Matching Selects Bold Face Over Normal Face

**Test**: `CanvasNetSvg_SystemIntegration_SvgFontWeightStyleMatching_SelectsBoldFaceOverNormalFace`

Exercises end-to-end system behavior for font-face matching: rasterizes two `text` elements
sharing a font-family, one with `font-weight="bold"`, through the public
`LoadWithFontFaces` API registering both a normal and a bold face. Asserts each text element
selects the correctly matching registered face, confirming face-matching integrates correctly
with text rendering.

### Integration: Svg Validation Null Null Stream Throws Argument Null Exception

**Test**: `CanvasNetSvg_SystemIntegration_SvgValidationNull_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for null-argument validation: calls the public `Load` API
with a null stream. Asserts `ArgumentNullException` is thrown, confirming the documented
validation contract is honored at the system's own public entry point.

### Integration: Svg Validation Empty Path Empty Path Throws Argument Exception

**Test**: `CanvasNetSvg_SystemIntegration_SvgValidationEmptyPath_EmptyPathThrowsArgumentException`

Exercises end-to-end system behavior for empty-path validation: calls the public `Load` API with
an empty path. Asserts `ArgumentException` is thrown, confirming the documented validation
contract is honored at the system's own public entry point.

### Integration: Svg Unsupported Format Validation Malformed Xml Throws Invalid Data Exception

**Test**: `CanvasNetSvg_SystemIntegration_SvgUnsupportedFormatValidation_MalformedXmlThrowsInvalidDataException`

Exercises end-to-end system behavior for malformed-input rejection: calls the public `Load` API
with a non-well-formed XML document. Asserts `InvalidDataException` is thrown, confirming
malformed input is rejected rather than silently producing incorrect output.

### Integration: Svg Xxe Hardening Doctype Declaration Throws Invalid Data Exception

**Test**: `CanvasNetSvg_SystemIntegration_SvgXxeHardening_DoctypeDeclarationThrowsInvalidDataException`

Exercises end-to-end system behavior for XXE hardening: calls the public `Load` API with a
document containing a DOCTYPE declaration. Asserts `InvalidDataException` is thrown, confirming
DTD/external-entity processing is disabled at the system's own public entry point.

### Integration: Svg Tolerate Unsupported Constructs Still Renders Rest Of Document

**Test**: `CanvasNetSvg_SystemIntegration_SvgTolerateUnsupportedConstructs_StillRendersRestOfDocument`

Exercises end-to-end system behavior for tolerant handling of out-of-scope constructs: rasterizes
a document containing an out-of-scope nested `svg` element alongside an ordinary rect, through
the public API. Asserts the ordinary rect still renders, confirming one unsupported construct
does not abort rendering of the rest of the document.

### Integration: Svg Get Info Validation Null Stream Throws Argument Null Exception

**Test**: `CanvasNetSvg_SystemIntegration_SvgGetInfoValidation_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for `GetInfo` null-argument validation: calls the public
`GetInfo` API with a null stream. Asserts `ArgumentNullException` is thrown, confirming `GetInfo`
honors the same validation contract as `Load` at the system's own public entry point.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure.
