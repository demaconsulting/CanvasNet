## Surface Unit Verification Design

This document describes the unit-level verification strategy for the `Surface` class (and the
supporting `Rgba32` value type).

### Verification Approach

The `Surface` unit is verified through unit tests that exercise each public constructor, property,
indexer, span accessor, the `Crop` method, and the vectorized bulk pixel operations
(`PremultiplyAlpha`, `UnpremultiplyAlpha`, `CompositeOver`) in isolation. `Surface`'s only runtime
dependency, `System.Numerics.Tensors`, is not injectable and has no seams to mock — it is
exercised indirectly, end-to-end, through its observable effect on pixel bytes, so no mocking or
stubbing is required. Tests supply controlled inputs and assert on returned values,
span-observable side effects, and thrown exception types. Expected values for the compositing and
premultiplication tests are computed independently of the implementation (via hand-computed
Porter-Duff results for compositing, and via scalar `double` arithmetic for
premultiply/unpremultiply), not by re-deriving the formula under test.

Unit tests reside in `SurfaceTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `Surface` has no injectable dependencies
- **Isolation**: Each test method constructs its own `Surface` instance(s); no shared state
  between tests

### Unit-Level Test Scenarios

#### CanvasNet-Canvas-Surface-Construction: Constructor Sets Width and Height

**Tests**: `Surface_Constructor_ValidDimensions_SetsWidthAndHeight`,
`Surface_Constructor_WidthAtMaximum_Succeeds`, `Surface_Constructor_HeightAtMaximum_Succeeds`

Constructs a `Surface` with known width and height and asserts both properties reflect the
constructor arguments. Additionally, constructs surfaces with width, and separately height, at the
maximum permitted dimension (8192) and asserts construction succeeds with the requested
dimension.

#### CanvasNet-Canvas-Surface-ZeroInitialized: Constructor Produces an All-Zero Buffer

**Test**: `Surface_Constructor_ValidDimensions_BufferIsAllZero`

Constructs a `Surface` with no explicit initialization and asserts every pixel in every row equals
`default(Rgba32)` (all channels zero), confirming the documented fully transparent default.

#### CanvasNet-Canvas-Surface-InvalidWidth: Constructor Rejects Zero/Negative Width

**Tests**: `Surface_Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException`,
`Surface_Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException`

Attempts to construct a `Surface` with a zero width, and separately with a negative width. Asserts
`ArgumentOutOfRangeException` is thrown in both cases.

#### CanvasNet-Canvas-Surface-InvalidHeight: Constructor Rejects Zero/Negative Height

**Tests**: `Surface_Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException`,
`Surface_Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException`

Attempts to construct a `Surface` with a zero height, and separately with a negative height.
Asserts `ArgumentOutOfRangeException` is thrown in both cases.

#### CanvasNet-Canvas-Surface-WidthExceedsMaximum: Constructor Rejects Width Exceeding the Maximum Dimension

**Test**: `Surface_Constructor_WidthExceedsMaximum_ThrowsArgumentOutOfRangeException`

Attempts to construct a `Surface` with a width one greater than the maximum permitted dimension
(8193). Asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-HeightExceedsMaximum: Constructor Rejects Height Exceeding the Maximum Dimension

**Test**: `Surface_Constructor_HeightExceedsMaximum_ThrowsArgumentOutOfRangeException`

Attempts to construct a `Surface` with a height one greater than the maximum permitted dimension
(8193). Asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-MaxDimensionPublic: MaxDimension Is Publicly Accessible

**Test**: `Surface_MaxDimension_IsPubliclyAccessible_Equals8192`

Asserts `Surface.MaxDimension` equals the documented value of 8192, and, via reflection on
`typeof(Surface).GetField(nameof(Surface.MaxDimension))`, asserts the field's declared
accessibility is genuinely `public` - a plain compile-time reference to `Surface.MaxDimension`
alone cannot distinguish `public` from `internal` here, since the test assembly already has
`InternalsVisibleTo` access to the main assembly, so the reflection-based accessibility check is
required to prove the requirement.

#### CanvasNet-Canvas-Surface-PixelGet / CanvasNet-Canvas-Surface-PixelSet: Indexer Set Then Get Round-Trips

**Test**: `Surface_Indexer_SetThenGet_ReturnsStoredPixel`

Constructs a `Surface`, stores a distinct `Rgba32` value at a coordinate via the indexer setter,
then reads the same coordinate via the indexer getter. Asserts the returned value exactly matches
the stored value.

#### CanvasNet-Canvas-Surface-RowSpanBytes: Writes Through the Byte Row Span Are Visible via the Indexer

**Tests**: `Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange`,
`Surface_GetRowSpanBytes_BoundaryWidths_ReturnsWidthTimesFourLength`

Obtains the raw byte span for a row via `GetRowSpanBytes`, writes four bytes representing one
pixel directly into the span, and asserts the indexer at the corresponding coordinate returns the
matching `Rgba32` value — confirming the span aliases the surface's own storage. Additionally,
constructs surfaces at internal row-padding boundary widths (1, 15, 16, 17, 31, 32, 33, 100, 257 —
straddling the 16-pixel padding boundary from both sides) and asserts `GetRowSpanBytes` always
returns a span of exactly `Width * 4` bytes regardless of the internal padding. (The
codec-round-trip regression at these same boundary widths is a system-level scenario — see
`CanvasNet_SystemIntegration_PngCodecRoundTrip_BoundaryWidths_ReturnsExpectedPixels` in the system
verification design, `../../canvas-net.md` — because it exercises the `Codecs` → `Surface`
integration boundary rather than `Surface` in isolation.)

#### CanvasNet-Canvas-Surface-RowSpanPixels: Writes Through the Pixel Row Span Are Visible via the Indexer

**Tests**: `Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange`,
`Surface_GetRowSpan_BoundaryWidths_ReturnsWidthLength`

Obtains the pixel span for a row via `GetRowSpan`, writes an `Rgba32` value directly into the
span, and asserts the indexer at the corresponding coordinate returns the same value — confirming
the span aliases the surface's own storage. Additionally, constructs surfaces at the same
internal row-padding boundary widths listed above and asserts `GetRowSpan` always returns a span
of exactly `Width` pixels regardless of the internal padding.

#### CanvasNet-Canvas-Surface-Crop: Crop Returns the Expected Pixels

**Tests**: `Surface_Crop_ValidRegion_ReturnsExpectedPixels`,
`Surface_Crop_BoundaryWidths_ReturnsExpectedPixels`

Constructs a surface with a distinct pixel value at every coordinate, crops a sub-region, and
asserts the cropped surface has the requested dimensions and that each of its pixels matches the
corresponding source pixel. Additionally, repeats this at each internal row-padding boundary
width, cropping the entire surface, to confirm `Crop` remains byte-exact regardless of the
internal padding.

#### CanvasNet-Canvas-Surface-CropIndependent: Cropped Result and Source Do Not Share Storage

**Tests**: `Surface_Crop_ModifyResult_DoesNotAffectSource`,
`Surface_Crop_ModifySource_DoesNotAffectResult`

Crops a surface, then mutates the cropped result and asserts the source is unaffected; separately,
mutates the source after cropping and asserts the previously cropped result is unaffected.
Together these confirm `Crop` produces an independent copy in both directions.

#### CanvasNet-Canvas-Surface-CropInvalidX: Crop Rejects Negative X

**Test**: `Surface_Crop_NegativeX_ThrowsArgumentOutOfRangeException`

Calls `Crop` with a negative `x` argument and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-CropInvalidY: Crop Rejects Negative Y

**Test**: `Surface_Crop_NegativeY_ThrowsArgumentOutOfRangeException`

Calls `Crop` with a negative `y` argument and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-CropInvalidWidth: Crop Rejects Zero Width

**Test**: `Surface_Crop_ZeroWidth_ThrowsArgumentOutOfRangeException`

Calls `Crop` with a zero `width` argument and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-CropInvalidHeight: Crop Rejects Zero Height

**Test**: `Surface_Crop_ZeroHeight_ThrowsArgumentOutOfRangeException`

Calls `Crop` with a zero `height` argument and asserts `ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-CropExceedsWidth: Crop Rejects a Region Wider Than the Source

**Test**: `Surface_Crop_WidthExceedsSourceBounds_ThrowsArgumentOutOfRangeException`

Calls `Crop` with `x + width` exceeding the source `Width` and asserts
`ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-CropExceedsHeight: Crop Rejects a Region Taller Than the Source

**Test**: `Surface_Crop_HeightExceedsSourceBounds_ThrowsArgumentOutOfRangeException`

Calls `Crop` with `y + height` exceeding the source `Height` and asserts
`ArgumentOutOfRangeException` is thrown.

#### CanvasNet-Canvas-Surface-PremultiplyAlpha: PremultiplyAlpha Computes Expected Pixels and Round-Trips

**Tests**: `Surface_PremultiplyAlpha_VariousValues_ComputesExpectedPixels`,
`Surface_PremultiplyThenUnpremultiplyAlpha_PartialAlpha_RoundTripsWithinTolerance`,
`Surface_PremultiplyThenUnpremultiplyAlpha_BoundaryAlpha_RoundTripsExactly`,
`Surface_PremultiplyAlpha_MultiRowBoundaryWidths_ComputesExpectedPixelForEveryPixel`

Calls `PremultiplyAlpha` on single-pixel surfaces across a table of color/alpha combinations
(including alpha 0, alpha 255, and partial alpha) whose expected premultiplied values were
computed independently of the implementation (`round(color * alpha / 255)`,
round-half-away-from-zero), and asserts an exact match. Additionally, round-trips
`PremultiplyAlpha` followed by `UnpremultiplyAlpha` for partial-alpha pixels and asserts the
result is within one rounding step of the original (not falsely exact, since premultiplication is
lossy), and for boundary alphas (0 and 255) asserts an exact round-trip. Additionally, exercises
a three-row surface at each internal row-padding boundary width (widths at, just below, and just
above each multiple-of-16 boundary), with a distinct color/alpha pair per pixel position, and
asserts every visible pixel across every row matches an independently computed expected value -
confirming no row-offset or padding-boundary corruption at non-16-aligned widths.

#### CanvasNet-Canvas-Surface-UnpremultiplyAlpha: UnpremultiplyAlpha Computes Expected Pixels

**Tests**: `Surface_UnpremultiplyAlpha_VariousValues_ComputesExpectedPixels`,
`Surface_UnpremultiplyAlpha_AlphaZero_ResultIsZeroRgb`,
`Surface_PremultiplyThenUnpremultiplyAlpha_PartialAlpha_RoundTripsWithinTolerance`,
`Surface_PremultiplyThenUnpremultiplyAlpha_BoundaryAlpha_RoundTripsExactly`,
`Surface_UnpremultiplyAlpha_MultiRowBoundaryWidths_ComputesExpectedPixelForEveryPixel`

Calls `UnpremultiplyAlpha` on single-pixel surfaces across a table of premultiplied-color/alpha
combinations whose expected straight-alpha values were computed independently of the
implementation (`round(color * 255 / alpha)`, round-half-away-from-zero, clamped), including a
case where the raw division exceeds 255 to exercise the clamp. Separately, asserts a fully
transparent pixel (`alpha == 0`) with arbitrary color-channel garbage produces `R = G = B = 0`,
the documented degenerate-case result. Additionally, exercises a three-row surface at each
internal row-padding boundary width (widths at, just below, and just above each multiple-of-16
boundary), with a distinct color/alpha pair (alpha restricted to `[1, 255]` to keep the
degenerate `alpha == 0` case out of scope) per pixel position, and asserts every visible pixel
across every row matches an independently computed expected value - confirming no row-offset or
padding-boundary corruption at non-16-aligned widths.

#### CanvasNet-Canvas-Surface-CompositeOverSurface: CompositeOver(Surface) Matches Independently Computed Results

**Tests**: `Surface_CompositeOverSurface_OpaqueForeground_ReplacesBackground`,
`Surface_CompositeOverSurface_TransparentForeground_LeavesBackgroundUnchanged`,
`Surface_CompositeOverSurface_BothFullyTransparent_ResultIsZero`,
`Surface_CompositeOverSurface_PartialAlpha_MatchesIndependentlyComputedResult`

Composites a fully opaque foreground pixel over a distinct background pixel and asserts the
result exactly equals the foreground. Separately, composites a fully transparent foreground pixel
(with garbage color channels) over a background and asserts the background is completely
unaffected. Separately, composites a fully transparent foreground pixel (with garbage color
channels) over a fully transparent background (also with garbage color channels) and asserts the
result is exactly zero RGB and zero alpha, exercising the `outA == 0` division-guard path that a
nonzero-alpha background never reaches. Separately, composites two partially transparent pixels
and asserts the result exactly matches a value independently hand-computed via the Porter-Duff
"over" formula (not by re-deriving the same formula under test).

#### CanvasNet-Canvas-Surface-CompositeOverSurfaceNull: CompositeOver(Surface) Rejects a Null Foreground

**Test**: `Surface_CompositeOverSurface_NullForeground_ThrowsArgumentNullException`

Calls `CompositeOver` with a `null` foreground surface and asserts `ArgumentNullException` is
thrown.

#### CanvasNet-Canvas-Surface-CompositeOverSurfaceDimensionMismatch: CompositeOver(Surface) Rejects a Size Mismatch

**Tests**: `Surface_CompositeOverSurface_WidthMismatch_ThrowsArgumentException`,
`Surface_CompositeOverSurface_HeightMismatch_ThrowsArgumentException`

Calls `CompositeOver` with a foreground surface whose width differs from this surface's, and
separately whose height differs, and asserts `ArgumentException` is thrown in both cases.

#### CanvasNet-Canvas-Surface-CompositeOverColor: CompositeOver(Rgba32) Matches Independently Computed Results

**Tests**: `Surface_CompositeOverColor_OpaqueColor_ReplacesBackground`,
`Surface_CompositeOverColor_TransparentColor_LeavesBackgroundUnchanged`,
`Surface_CompositeOverColor_BothFullyTransparent_ResultIsZero`,
`Surface_CompositeOverColor_PartialAlpha_MatchesIndependentlyComputedResult`,
`Surface_CompositeOverColor_MultiRowSurface_AppliesToEveryPixel`

Composites a fully opaque constant color over a background pixel and asserts the result exactly
equals the color. Separately, composites a fully transparent constant color (with garbage color
channels) over a background and asserts the background is unaffected. Separately, composites a
fully transparent constant color (with garbage color channels) over a fully transparent
background pixel (also with garbage color channels) and asserts the result is exactly zero RGB
and zero alpha, exercising the `outA == 0` division-guard path that a nonzero-alpha background
never reaches. Separately, composites a partially transparent constant color over an opaque
background and asserts the result exactly matches an independently hand-computed value.
Separately, composites a constant opaque color over a multi-row, multi-column surface and asserts
every pixel is replaced, confirming the per-row loop is applied uniformly across the whole
surface, not just a single pixel.

#### CanvasNet-Canvas-Surface-CompositeOverSpan: CompositeOverSpan Matches Independently Computed Results

**Tests**: `Surface_CompositeOverSpan_FullCoverage_MatchesCompositeOverColor`,
`Surface_CompositeOverSpan_ZeroCoverage_LeavesBackgroundUnchanged`,
`Surface_CompositeOverSpan_PartialCoverage_MatchesLinearInterpolationOracle`,
`Surface_CompositeOverSpan_MultiPixelRun_AppliesPerPixelCoverage`,
`Surface_CompositeOverSpan_EmptyCoverageAtWidthBoundary_NoOp`

Composites a constant color over a row with a coverage of exactly `1` at every pixel via
`CompositeOverSpan`, and separately composites the same color over an identical background via
`CompositeOver(Rgba32)`, and asserts the two results are byte-identical - full coverage must be
indistinguishable from the existing full-pixel overload. Separately, composites a constant color
with a coverage of exactly `0` and asserts the background pixel is completely unaffected.
Separately, composites a constant color at a fractional coverage value and asserts the result
exactly matches an independently hand-computed linear-interpolation oracle (not the same formula
under test). Separately, composites a constant color across a multi-pixel run with a distinct
coverage value per pixel (`0`, a fractional value, and `1`) and asserts each pixel reflects its
own coverage value independently, confirming per-pixel (not per-row-uniform) scaling. Separately,
calls `CompositeOverSpan` with an empty coverage span starting exactly at `x == Width` and asserts
this is accepted as a no-op rather than throwing, confirming the boundary case of a zero-length
run at the surface's right edge is valid.

#### CanvasNet-Canvas-Surface-CompositeOverSpanZeroCoveragePreservesBytes: Zero/Negative Coverage Preserves Original Bytes

**Tests**: `Surface_CompositeOverSpan_ZeroCoverageOnTransparentPixelWithNonzeroColor_LeavesPixelUnchanged`,
`Surface_CompositeOverSpan_NegativeCoverageOnTransparentPixelWithNonzeroColor_LeavesPixelUnchanged`,
`Surface_CompositeOverSpan_MixedZeroAndFullCoverageRun_OnlyTouchesFullCoverageColumn`

Composites over a fully transparent background pixel that legitimately carries nonzero RGB (for
example, a premultiplied-adjacent transparent fringe pixel) with a coverage of exactly `0`, and
asserts the pixel is byte-for-byte unchanged rather than zeroed out - a regression test for a
zero-coverage pixel-corruption bug in which the shared blend pipeline's zero-alpha degenerate-case
handling incorrectly overwrote such a pixel's untouched RGB bytes with `(0, 0, 0, 0)`. Separately,
repeats the same assertion for a negative coverage value, confirming negative coverage is treated
identically to zero coverage per the documented contract. Separately, composites a two-pixel run
mixing a zero-coverage column (over a fully transparent, nonzero-RGB pixel) with a full-coverage
column and asserts only the full-coverage column is modified, confirming the byte-restoration
step applies independently per column rather than skipping the whole row.

#### CanvasNet-Canvas-Surface-CompositeOverSpanValidation: CompositeOverSpan Rejects Out-of-Range Row/Column Arguments

**Tests**: `Surface_CompositeOverSpan_NegativeY_ThrowsArgumentOutOfRangeException`,
`Surface_CompositeOverSpan_YAtHeight_ThrowsArgumentOutOfRangeException`,
`Surface_CompositeOverSpan_NegativeX_ThrowsArgumentOutOfRangeException`,
`Surface_CompositeOverSpan_RunExceedsWidth_ThrowsArgumentOutOfRangeException`

Calls `CompositeOverSpan` with a negative `y`, and separately with `y` equal to `Height` (one
past the last valid row), and asserts `ArgumentOutOfRangeException` is thrown in both cases.
Separately, calls `CompositeOverSpan` with a negative `x`, and separately with a coverage run
whose `x + coverage.Length` exceeds `Width`, and asserts `ArgumentOutOfRangeException` is thrown
in both cases.

#### CanvasNet-Canvas-Surface-CompositeOverSpanPerPixelColor: Per-Pixel Colors Match the Constant-Color Overload

**Tests**:
`Surface_CompositeOverSpan_PerPixelColors_MatchesConstantColorOverload_WhenAllColorsEqual`,
`Surface_CompositeOverSpan_PerPixelColors_FullCoverage_AppliesEachPixelsOwnColor`,
`Surface_CompositeOverSpan_PerPixelColors_ZeroCoverage_LeavesBackgroundUnchanged`,
`Surface_CompositeOverSpan_PerPixelColors_LengthMismatch_ThrowsArgumentException`,
`Surface_CompositeOverSpanWithWorkspace_PerPixelColors_MatchesPublicOverload`

Composites a per-pixel `ReadOnlySpan<Rgba32>` over an identical background using both the
per-pixel-color overload and the constant-color overload (with every entry of the per-pixel span
set to the same color) and asserts the two produce byte-for-byte identical surfaces - proving the
refactor sharing `CompositeOverSpanCore`'s blend math between both overloads left the pre-existing
constant-color overload's behavior unchanged. Separately verifies each pixel's own color is
applied at full coverage, that zero coverage leaves the background unchanged, that a
`colors`/`coverage` length mismatch throws `ArgumentException`, and that the internal
workspace-reusing per-pixel-color overload matches the public overload's output exactly.

#### Rgba32 Sanity Checks (no requirement link)

**Tests**: `Rgba32_FieldAssignment_StoresChannelValues`, `Rgba32_Equals_SameChannelValues_ReturnsTrue`

Confirm that `Rgba32`'s constructor stores each channel value in the corresponding field, and
that two instances with identical channel values compare equal via `Equals` and the `==`/`!=`
operators. These sanity tests support the other `Surface` scenarios above (which depend on
`Rgba32` equality) but are not independently linked to a requirement.

### Acceptance Criteria

A unit test run passes when every requirement-linked scenario above, plus the two `Rgba32`
sanity tests and the additional boundary-width regression tests, pass without error or
unexpected exception; any unexpected exception type or wrong return value constitutes a failure.
