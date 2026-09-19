## Surface Unit Verification Design

This document describes the unit-level verification strategy for the `Surface` class (and the
supporting `Rgba32` value type).

### Verification Approach

The `Surface` unit is verified through unit tests that exercise each public constructor, property,
indexer, span accessor, and the `Crop` method in isolation. Because `Surface` has no external
dependencies beyond the .NET base class library, no mocking or stubbing is required. Tests supply
controlled inputs and assert on returned values, span-observable side effects, and thrown
exception types.

Unit tests reside in `SurfaceTests.cs` within the `DemaConsulting.CanvasNet.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `Surface` has no injectable dependencies
- **Isolation**: Each test method constructs its own `Surface` instance(s); no shared state
  between tests

### Unit-Level Test Scenarios

#### CanvasNet-Canvas-Surface-Construction: Constructor Sets Width and Height

**Test**: `Surface_Constructor_ValidDimensions_SetsWidthAndHeight`

Constructs a `Surface` with known width and height and asserts both properties reflect the
constructor arguments.

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

#### CanvasNet-Canvas-Surface-PixelGet / CanvasNet-Canvas-Surface-PixelSet: Indexer Set Then Get Round-Trips

**Test**: `Surface_Indexer_SetThenGet_ReturnsStoredPixel`

Constructs a `Surface`, stores a distinct `Rgba32` value at a coordinate via the indexer setter,
then reads the same coordinate via the indexer getter. Asserts the returned value exactly matches
the stored value.

#### CanvasNet-Canvas-Surface-RowSpanBytes: Writes Through the Byte Row Span Are Visible via the Indexer

**Test**: `Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange`

Obtains the raw byte span for a row via `GetRowSpanBytes`, writes four bytes representing one
pixel directly into the span, and asserts the indexer at the corresponding coordinate returns the
matching `Rgba32` value — confirming the span aliases the surface's own storage.

#### CanvasNet-Canvas-Surface-RowSpanPixels: Writes Through the Pixel Row Span Are Visible via the Indexer

**Test**: `Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange`

Obtains the pixel span for a row via `GetRowSpan`, writes an `Rgba32` value directly into the
span, and asserts the indexer at the corresponding coordinate returns the same value — confirming
the span aliases the surface's own storage.

#### CanvasNet-Canvas-Surface-Crop: Crop Returns the Expected Pixels

**Test**: `Surface_Crop_ValidRegion_ReturnsExpectedPixels`

Constructs a surface with a distinct pixel value at every coordinate, crops a sub-region, and
asserts the cropped surface has the requested dimensions and that each of its pixels matches the
corresponding source pixel.

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

#### Rgba32 Sanity Checks (no requirement link)

**Tests**: `Rgba32_FieldAssignment_StoresChannelValues`, `Rgba32_Equals_SameChannelValues_ReturnsTrue`

Confirm that `Rgba32`'s constructor stores each channel value in the corresponding field, and
that two instances with identical channel values compare equal via `Equals` and the `==`/`!=`
operators. These sanity tests support the other `Surface` scenarios above (which depend on
`Rgba32` equality) but are not independently linked to a requirement.

### Acceptance Criteria

A unit test run passes when all eighteen requirement-linked scenarios above, plus the two
`Rgba32` sanity tests, pass without error or unexpected exception; any unexpected exception type
or wrong return value constitutes a failure.
