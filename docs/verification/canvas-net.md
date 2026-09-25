# System Verification Design

<!-- cspell:ignore codepoint -->

<!-- cspell:ignore codepoint -->
This document describes the system-level verification strategy for CanvasNet.

## Verification Approach

The CanvasNet system is verified through system-level integration tests that
exercise the library as a whole from the perspective of a consumer. Tests instantiate the library
using its public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `CanvasNetTests.cs` within the
`DemaConsulting.CanvasNet.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external interfaces requiring simulation. It is a pure in-process .NET library
with no I/O, network calls, or platform services. System tests call the public API directly
with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Canvas Construct and Set Pixel Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_CanvasConstructAndSetPixel_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit: constructs a `Surface` and sets a
pixel through the public indexer, then reads it back. Asserts the read value exactly matches the
stored value, confirming the system's public pixel-access API integrates correctly.

### Integration: Canvas Crop Returns an Independent Sub-Region

**Test**: `CanvasNet_SystemIntegration_CanvasCrop_ReturnsIndependentSubRegion`

Constructs a `Surface`, sets a distinct pixel within a region, crops that region, then mutates the
source surface. Asserts the cropped result retains the pixel value captured at crop time,
confirming that `Crop` produces an independent copy at the system level.

### Integration: BMP Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_BmpSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `BmpCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory BMP stream via `BmpCodec.Save`, and
loads it back via `BmpCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public BMP save/load API integrates correctly with `Surface`.

### Integration: PNG Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_PngSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `PngCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory PNG stream via `PngCodec.Save`, and
loads it back via `PngCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public PNG save/load API integrates correctly with `Surface`.

### Integration: TIFF Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_TiffSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `TiffCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory TIFF stream via `TiffCodec.Save`, and
loads it back via `TiffCodec.Load`. Asserts the reloaded pixel matches the original, confirming
that the system's public TIFF save/load API integrates correctly with `Surface`.

### Integration: JPEG Save Then Load Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_JpegSaveThenLoad_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Surface` and `JpegCodec` units: constructs a
`Surface`, sets a distinct pixel, saves it to an in-memory JPEG stream via `JpegCodec.Save`, and
loads it back via `JpegCodec.Load`. Unlike the BMP/PNG/TIFF scenarios above, this scenario uses a
per-channel tolerance rather than exact equality because JPEG is lossy; passing requires every RGB
channel of the reloaded pixel to remain within the documented tolerance bound while alpha remains
opaque.

### Integration: Composite Color Over Surface Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_CompositeColorOverSurface_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit's compositing operation: constructs an
opaque background `Surface`, composites a semi-transparent solid color over it via
`Surface.CompositeOver(Rgba32)`, then reads the result back through the public indexer. Asserts
the resulting pixel exactly matches the expected Porter-Duff "over" compositing result, confirming
the system's public compositing API integrates correctly with `Surface`.

### Integration: Composite Surface Over Surface Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_CompositeSurfaceOverSurface_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit's `CompositeOver(Surface)` overload:
constructs an opaque background `Surface` and a semi-transparent foreground `Surface`, both
through the public API, composites the foreground over the background via
`Surface.CompositeOver(Surface)`, then reads the result back through the public indexer. Asserts
the resulting pixel exactly matches the expected Porter-Duff "over" compositing result, confirming
the system's public `CompositeOver(Surface)` API integrates correctly with `Surface`.

### Integration: Premultiply Alpha Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_PremultiplyAlpha_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit's alpha-premultiplication operation:
constructs a `Surface`, sets a straight-alpha pixel through the public indexer, converts it to
premultiplied alpha via `Surface.PremultiplyAlpha`, then reads it back. Asserts the resulting pixel
exactly matches the expected rounded premultiplied value, confirming the system's public
premultiply API integrates correctly with `Surface`.

### Integration: Unpremultiply Alpha Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_UnpremultiplyAlpha_ReturnsExpectedPixel`

Exercises end-to-end system behavior for the `Surface` unit's alpha-unpremultiplication operation:
constructs a `Surface`, sets a premultiplied-alpha pixel (chosen so the unpremultiplied red channel
overshoots 255) through the public indexer, converts it to straight alpha via
`Surface.UnpremultiplyAlpha`, then reads it back. Asserts the resulting pixel exactly matches the
expected rounded-and-clamped value, confirming the system's public unpremultiply API integrates
correctly with `Surface`, including its documented clamping behavior.

### Integration: PNG Codec Round-Trip at Boundary Widths Returns Expected Pixels

**Test**: `CanvasNet_SystemIntegration_PngCodecRoundTrip_BoundaryWidths_ReturnsExpectedPixels`

Exercises end-to-end system behavior across the `Surface` and `PngCodec` units at every internal
row-padding boundary width (1, 15, 16, 17, 31, 32, 33, 100, 257 pixels — straddling the 16-pixel
padding boundary from both sides): constructs a `Surface` with distinct, non-trivial per-pixel
values at each boundary width, saves it to an in-memory PNG stream via `PngCodec.Save`, and loads
it back via `PngCodec.Load`. Asserts every pixel round-trips byte-exactly, confirming that
`Surface`'s internal row-stride/padding storage detail (owned by the `Surface` unit) is never
observable through the `PngCodec` unit's save/load API. This is a system-level scenario, not a
`Surface` unit scenario, because it exercises the `Codecs` → `Surface` integration boundary
(`PngCodec` depends on `Surface`, not vice versa); a `Surface` unit test may only depend on
`Surface` itself and its documented dependencies.

### Integration: Build, Flatten, and Bound a Path Returns Expected Bounds

**Test**: `CanvasNet_SystemIntegration_BuildFlattenAndBoundPath_ReturnsExpectedBounds`

Exercises end-to-end system behavior across the `Geometry` subsystem's four units together:
constructs a `PathBuilder`, issues a `MoveTo`/`CubicBezierTo`/`ArcTo`/`Close` command sequence, and
calls `Build()` to obtain an immutable `Path`. Separately flattens the same cubic segment directly
via `BezierFlattening.FlattenCubic` and asserts the resulting polyline is non-empty and ends
exactly at the curve's declared end point. Calls `Path.GetBounds()` (the default, conservative
mode) and asserts the result is non-empty with positive width and height. Asserts the built
`Path` contains exactly one subpath and that it is closed. This is a system-level scenario, not a
single-unit scenario, because it exercises the collaboration between `PathBuilder`, `Path`
(including its internal use of `SvgArcConverter` to convert the `ArcTo` command), and
`BezierFlattening` together, rather than any one of the four `Geometry` units in isolation.

### Integration: Fill Empty or Out-of-Bounds Path No-Ops, Leaving the Surface Unchanged

**Test**: `CanvasNet_SystemIntegration_FillEmptyOrOutOfBoundsPath_NoOpLeavesSurfaceUnchanged`

Exercises end-to-end system behavior across the `Geometry`, `Drawing`, and `Canvas` subsystems
together for the no-op edge case: constructs a `Surface`, builds an empty `Path` via
`PathBuilder().Build()`, and separately builds a closed triangular `Path` whose vertices all fall
entirely outside the surface's bounds. Calls `PathFiller.Fill` with each path in turn and asserts
every pixel of the surface remains at its initial, fully transparent state after both calls,
confirming that neither an empty path nor a path whose bounds do not intersect the surface causes
`PathFiller.Fill` to throw or to write any pixel. This is a system-level scenario, not a
single-unit scenario, because it exercises the same `PathBuilder`/`Path` (`Geometry`),
`PathFiller` (`Drawing`), and `Surface` (`Canvas`) collaboration as the triangle-fill scenario
below, but for the no-op boundary condition rather than the happy path.

### Integration: Build and Fill a Triangle Path Returns Expected Pixels

**Test**: `CanvasNet_SystemIntegration_BuildAndFillTrianglePath_ReturnsExpectedPixels`

Exercises end-to-end system behavior across the `Geometry`, `Drawing`, and `Canvas` subsystems
together: constructs a `PathBuilder`, issues a `MoveTo`/`LineTo`/`LineTo`/`Close` sequence
describing a triangle, calls `Build()` to obtain an immutable `Path`, constructs a `Surface`, and
calls `PathFiller.Fill` with a solid opaque color. Asserts a pixel well inside the triangle is
fully opaque with the exact requested color, a pixel well outside the triangle remains fully
transparent (the surface's untouched initial state), and a pixel straddling the triangle's
slanted edge has a partial (neither `0` nor `255`) alpha value, confirming the antialiased
coverage rasterizer produced a genuine fractional-coverage result rather than a hard-edged
(aliased) one. This is a system-level scenario, not a single-unit scenario, because it exercises
the collaboration between `PathBuilder`/`Path` (`Geometry`), `PathFiller` (`Drawing`), and
`Surface`/`Surface.CompositeOverSpan` (`Canvas`) together, rather than any one subsystem in
isolation.

### Integration: Load a Font and Fill a Glyph Outline Returns Expected Pixels

**Test**: `CanvasNet_SystemIntegration_LoadFontAndFillGlyphOutline_ReturnsExpectedPixels`

Exercises end-to-end system behavior across the `Fonts`, `Geometry`, `Drawing`, and `Canvas`
subsystems together: loads a synthetic TrueType font, maps codepoint `'A'` to a glyph index,
extracts the glyph outline as `Geometry.Path`, scales and flips the outline into canvas
coordinates, and fills it through `PathFiller` onto a `Surface`. Asserts the mapped glyph index
is correct, the outline is non-trivial, the glyph interior renders fully opaque, and at least one
edge pixel has fractional alpha, confirming the system integrates font parsing with vector
rasterization successfully.

### Integration: Load a Font and Query Metrics Returns Expected Values

**Test**: `CanvasNet_SystemIntegration_LoadFontAndQueryMetrics_ReturnsExpectedValues`

Exercises end-to-end system behavior for the `Fonts` subsystem's metric APIs: loads the same
synthetic TrueType font through `TrueTypeFont.Load`, then asserts `UnitsPerEm`, `Ascender`,
`Descender`, `GlyphCount`, `GetAdvanceWidth`, and `GetKerning` match the font's declared table
values. This scenario proves the system exposes font-level scalar metrics independently of glyph
rendering.

### Integration: Parse Hex Color and Set Surface Pixel Returns Expected Pixel

**Test**: `CanvasNet_SystemIntegration_ParseHexColorAndSetSurfacePixel_ReturnsExpectedPixel`

Exercises end-to-end system behavior across the `Canvas` subsystem's `Rgba32` and `Surface`
units together: parses a `"#AARRGGBB"` hex color literal via `Rgba32.Parse`, stores the parsed
value into a `Surface` pixel, and reads it back. Asserts the round-tripped pixel's channels
match the literal exactly, confirming the system integrates hex-color parsing with pixel-buffer
storage across the `Canvas` subsystem's public boundary.

### Integration: Round Path Corners and Fill Onto Surface Clips Sharp Corner

**Test**: `CanvasNet_SystemIntegration_RoundPathCornersAndFillOntoSurface_ClipsSharpCorner`

Exercises end-to-end system behavior across the `Geometry`, `Drawing`, and `Canvas` subsystems
together: builds a closed square `Path` via `PathBuilder`, applies `CornerRoundEffect.Apply` to
round every corner with a generous radius, and fills the rounded path through `PathFiller` onto
a `Surface`. Asserts the original sharp top-left corner pixel is fully transparent (clipped away
by the rounding) while an interior pixel away from every corner remains fully opaque, confirming
the system integrates the `Geometry` subsystem's corner-rounding pre-processing with the
`Drawing` subsystem's fill rasterizer.

### Integration: Fill Round Rect Under Translated Canvas Matches Direct Placement

**Test**: `CanvasNet_SystemIntegration_FillRoundRectUnderTranslatedCanvas_MatchesDirectPlacement`

Exercises end-to-end system behavior across the `Rendering`, `Geometry`, `Drawing`, and `Canvas`
subsystems together: draws a rounded rectangle via `Rendering.Canvas.FillRoundRect` through a
translated `Rendering.Canvas` transform, and separately draws the same rounded rectangle directly
at the equivalent absolute coordinates on an untransformed `Rendering.Canvas`. Asserts both
surfaces are painted identically, pixel for pixel, confirming the `Rendering` subsystem's shape
helpers correctly compose with its transform stack all the way down to pixel output.

### Integration: Draw and Measure Text via Canvas Renders and Measures Expected Result

**Test**: `CanvasNet_SystemIntegration_DrawAndMeasureTextViaCanvas_RendersAndMeasuresExpectedResult`

Exercises end-to-end system behavior across the `Rendering`, `Fonts`, `Geometry`, `Drawing`, and
`Canvas` subsystems together: loads a synthetic TrueType font through `TrueTypeFont`'s public API,
measures a text run through `TextRenderer.MeasureText` and asserts the reported width, ascent, and
descent agree with the font's declared advance width and metrics, then draws the same text run
through `TextRenderer.DrawText` onto a public `Rendering.Canvas`. Asserts non-trivial rendered
pixel coverage on the underlying `Surface`, confirming the `Rendering` subsystem's text
measurement and text drawing boundaries integrate correctly end to end through the fully public
API surface.

### Integration: Clear Surface Then Read Pixel Returns Expected Color

**Test**: `CanvasNet_SystemIntegration_ClearSurfaceThenReadPixel_ReturnsExpectedColor`

Exercises end-to-end system behavior for the `Surface` unit's vectorized bulk-fill operation:
constructs a `Surface` through the public API, pre-fills it with distinct per-pixel data via the
public indexer, then calls `Surface.Clear` with a single color. Asserts every sampled pixel —
including the corners farthest from where the original per-pixel data was set — exactly matches
the clear color, confirming the system's public `Clear` API integrates correctly and fully
overwrites pre-existing pixel data across the whole surface.

### Integration: Canvas Clear Then Read Wrapped Surface Pixel Returns Expected Color

**Test**: `CanvasNet_SystemIntegration_CanvasClearThenReadWrappedSurfacePixel_ReturnsExpectedColor`

Exercises end-to-end system behavior for the `Rendering.Canvas` passthrough onto its wrapped
`Surface`'s vectorized bulk-fill operation, distinct from the `Surface`-only evidence above:
constructs a `Surface`, wraps it in a `Rendering.Canvas` through the public API, pre-fills the
wrapped surface with distinct per-pixel data, then calls `Canvas.Clear` with a single color. Reads
pixels back directly from the underlying wrapped `Surface` (not merely through the `Canvas`) and
asserts every sampled pixel — including the corners farthest from where the original per-pixel
data was set — exactly matches the clear color, confirming that a regression in the
`Canvas.Clear` -> `Surface.Clear` passthrough would be caught at the system level even if it were
otherwise masked by `Rendering` unit-level test isolation.

### Integration: Dispose Surface Then Use It Throws ObjectDisposedException

**Test**: `CanvasNet_SystemIntegration_DisposeSurfaceThenUseIt_ThrowsObjectDisposedException`

Exercises end-to-end system behavior for the `Surface` unit's disposal contract: constructs a
`Surface` through the public API, sets a pixel, then calls `Dispose()` through the public API.
Asserts calling `Dispose()` a second time remains safe (idempotent), and that a representative
sample of public buffer-touching members (the indexer, `GetRowSpan`, `Clear`, `Crop`) all reject
further use with `ObjectDisposedException`, confirming the system-level disposal guarantee - not
merely the individual unit-level guard on any single member.

### Integration: TIFF GetInfo on Non-Seekable Stream Returns Expected Info

**Test**: `CanvasNet_SystemIntegration_TiffGetInfoOnNonSeekableStream_ReturnsExpectedInfo`

Exercises end-to-end system behavior across the `Surface` and `TiffCodec` units: constructs a
`Surface`, saves it to an in-memory TIFF stream via `TiffCodec.Save`, then calls
`TiffCodec.GetInfo` through a stream that reports itself as non-seekable (matching a real-world
network stream). Asserts `GetInfo` returns the expected declared width and height without
throwing merely because the stream is non-seekable, confirming the system upholds the invariant
that `GetInfo` never throws for an input `Load` would successfully decode.

### Integration: JPEG GetInfo with Large Leading Segments Returns Expected Info

**Test**: `CanvasNet_SystemIntegration_JpegGetInfoWithLargeLeadingSegments_ReturnsExpectedInfo`

Exercises end-to-end system behavior across the `Surface` and `JpegCodec` units: constructs a
`Surface`, saves it to an in-memory JPEG stream via `JpegCodec.Save`, then pads the saved file
with a run of leading APP0 marker segments exceeding the codec's internal probe soft cap
(`JpegCodec.MaxProbeHeaderBytes`) before calling `JpegCodec.GetInfo`. Asserts `GetInfo` returns
the expected declared width and height without throwing merely because the leading metadata
exceeds the soft cap, confirming the system upholds the invariant that `GetInfo` never throws for
an input `Load` would successfully decode.

## Acceptance Criteria

A system-level test run passes when all twenty-four scenarios above pass without error or
exception beyond those explicitly asserted. Any unexpected exception, wrong exception type, or
wrong return value constitutes a failure.
