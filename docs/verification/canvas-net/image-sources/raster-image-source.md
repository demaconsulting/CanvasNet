### RasterImageSource Unit Verification Design

This document describes the unit-level verification strategy for the `RasterImageSource` unit, in the
`DemaConsulting.CanvasNet.ImageSources` namespace.

#### Verification Approach

The unit is verified by calling `Render` with requests that carry real encoded pictures: surfaces from the shared
`ImageFixtures` encoded in memory with the core codecs that can save (PNG, JPEG, BMP, TIFF), a GIF and a JPEG 2000 file
of the
checked-in fixture corpora of the core test project, and a JPEG with an Exif orientation made by
`InsertExifOrientation`. Tests
reside in `RasterImageSourceTests.cs` in the `ImageSources` folder of the `DemaConsulting.CanvasNet.Tests` project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None; the codecs are real
- **Files**: the GIF and JPEG 2000 fixtures copied to the test output directory; nothing is written
- **Isolation**: each test builds its own request

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A format
decided by the name instead of the content, an upright JPEG that is turned, a sideways JPEG that is not, a wrong size,
or a wrong
exception type constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-ImageSources-RasterImageSource-Formats: Formats Decided By Content

**Tests**: `RasterImageSource_Render_Format_IsDecidedByContentNotName`,
`RasterImageSource_Render_GifAndJpeg2000Fixtures_AreDecoded`,
`RasterImageSource_Properties_NameExtensionsAndNoSchemes`,
`RasterImageSource_Properties_ExtensionsAndSchemesAreReadOnly`

Asserts, as a theory, that a PNG, a JPEG, a BMP, and a TIFF named with another extension are decoded at their natural
size with the
detected format name and no notes or text, that the GIF and JPEG 2000 fixtures are decoded and named, the name, the
ten
extensions, and the absence of schemes, and that the extension and scheme lists are read-only (not an array or a
`List<T>`, and refusing `Add` when cast to `IList<T>`).

##### CanvasNet-ImageSources-RasterImageSource-Orientation: Exif Orientation

**Tests**: `RasterImageSource_Render_JpegWithExifOrientation_IsTurnedUpright`

Asserts that a JPEG of 12 by 8 pixels with orientation 6 is returned 8 by 12, and the same JPEG without orientation 12
by 8.

##### CanvasNet-ImageSources-RasterImageSource-Budget: Declared Size Checked From The Header

**Tests**: `RasterImageSource_Render_RemainingPixels_IsCheckedFromHeaderForEveryFormat`,
`RasterImageSource_Render_HeaderOnlyHugePicture_IsRefusedWithoutDecoding`

Asserts, as a theory over PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 files, that a request whose `Budget` has exactly the
size of the picture left is decoded and one pixel less throws `ImageSourceBudgetException` carrying the declared
size and the remainder; and that a 33-byte PNG that only declares 8192 by 8192 pixels, and one that declares 30,000 by
30,000 (more than the codecs decode), are refused with the declared pixel count with an allocation below 1 MiB for the
two refusals (no decode is attempted, which would fail with `InvalidDataException`), while a truncated header still
fails with `InvalidDataException`.

##### CanvasNet-ImageSources-RasterImageSource-Failures: Bad Input

**Tests**: `RasterImageSource_Render_CorruptOrUnknownContent_ThrowsInvalidDataException`,
`RasterImageSource_Render_InvalidRequests_Throw`

Asserts `InvalidDataException` for garbage, empty content, and a truncated PNG, `InvalidOperationException` for a
request
without a file, `ArgumentNullException` for a null request, and `OperationCanceledException` for a canceled request.
