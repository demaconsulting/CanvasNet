### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif gif RRGGBB RRGGBBAA -->

#### Verification Approach

`SourceLoader` (with `ExifOrientation` and `SurfaceOperations`) is verified with unit tests in
`SourceLoaderTests.cs` and `ExifOrientationTests.cs`. The tests generate their inputs: rasters are written in memory
with the CanvasNet codecs (a gradient that differs at every pixel and channel, so a mirroring, rotation, or offset
changes it), the GIF with a small encoder in the test project (the libraries have none), EXIF segments are injected
into a codec-written JPEG in either byte order, and a PNG that declares an oversized image but holds no pixels is
built from its header. The fixture files are an original SVG drawing and a JPEG 2000 image copied from the
repository's own codec fixtures (see `Fixtures/README.md`). Expected pixels come from the codecs themselves or from
the generator, never from the loader. The tests use real files in a temporary directory that each test deletes.

Error mapping is verified in three ways: directly, by feeding `Read` each allow-listed exception and a set of
unexpected ones (which must propagate unchanged); by loading, for every supported format, files that are empty, one
byte, a header only, half, and short by one byte, and garbage of the file's own length, and requiring that each
either loads or raises a `ToolException` with an expected code - an unexpected exception type fails the test, which
is how gaps in the allow-list are found; and by loading garbage with each known extension and requiring
`corrupt-input`.

#### Test Environment

N/A - standard test environment; the tests need no network and write only to temporary directories.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every supported format is recognized by its signature, and the content wins over the extension.
- The header facts of every format are reported without decoding.
- Lossless rasters load with exactly their pixels; SVG renders at the requested size over white.
- The size policy follows the documented rules; a side beyond 8192 pixels is `size-limit`, before decoding.
- A JPEG is turned upright by its EXIF orientation unless switched off, and the size applies to the upright image.
- A damaged file of any format is an expected error; an unexpected exception type is never mapped.

#### Test Scenarios

**SourceLoader_Detect_EachKind_ReturnsKind**, **SourceLoader_Detect_SignatureAndExtensionDisagree_TrustsSignature**,
**SourceLoader_Detect_SvgTextWithoutExtension_ReturnsSvg**,
**SourceLoader_Detect_UnrecognizedContent_FallsBackToExtension**, **SourceLoader_Detect_ZipPackage_UsesExtension**,
**SourceLoader_Detect_OtherSignatures_ReturnsKind**, **SourceLoader_Detect_TextStartingWithBm_IsNotBmp**, and
**SourceLoader_ExtensionKinds_AreWellFormed**: each supported kind is recognized by its content; a PNG named
`.jpg` is a PNG and SVG text named `.png` is an SVG; SVG text is recognized without an extension, after a byte
order mark, a prolog, and a comment; content that matches nothing keeps the kind of its extension; a ZIP signature
is a presentation or a drawing by extension; PDF, a raw JPEG 2000 codestream, and both TIFF byte orders are
recognized; text that starts with `BM` is not a bitmap; the extension table is lower case with a dot and covers
every kind.

**SourceLoader_Detect_UnknownFormat_ThrowsUnsupportedFormat**,
**SourceLoader_Detect_MissingFileOrDirectory_ThrowsInputError**, and
**SourceLoader_ProbeAndLoad_KindNotLoadableYet_ThrowsUnsupportedFormat**: an unknown file is
`unsupported-format` with a hint; a missing file is `input-not-found` and a directory `input-unreadable`; a
recognized kind that is not readable yet is `unsupported-format` for both operations.

**SourceLoader_Probe_EachKind_ReportsHeaderFacts**, **SourceLoader_Probe_PngAndSvg_ReportChannelsAndAlpha**, and
**SourceLoader_Probe_JpegWithExif_ReportsOrientation**: for each kind the format, path, byte count, size, decodability,
frame count, and page facts are right; a PNG with transparency and an SVG report four channels with alpha; a JPEG
with an EXIF segment reports its orientation and its stored size.

**SourceLoader_Load_LosslessRaster_KeepsEveryPixel** and **SourceLoader_Load_GifJpegAndJpeg2000_MatchTheCodecs**:
PNG, BMP, and TIFF load with exactly the generated pixels; the GIF loads with its palette image; the JPEG and the
JPEG 2000 image equal what their codecs decode; none reports operations, diagnostics, or echo.

**SourceLoader_Load_Svg_RendersNaturalSizeOverWhite**,
**SourceLoader_Load_SvgWithSizeOptions_RendersAtRequestedSize**, and
**SourceLoader_Load_SvgBackground_TransparentKeepsAlphaColorFlattens**: the SVG fixture renders at 100x80 with the red
and blue squares where the drawing puts them, white where it is empty, and the half-transparent green flattened to
light green; scale, width, height, and exact sizes render at that size (the drawing is at the proportional place
when the aspect ratio is kept); a transparent background keeps alpha and a given color replaces white; the
operations list says what happened.

**SourceLoader_ComputeTargetSize_SizeOptions_FollowsPolicy** and
**SourceLoader_Load_RasterWithWidth_ResizesKeepingAspectRatio**: the size policy for none, scale, width, height, and
both, with rounding and the one-pixel minimum; a raster is resized and the operation recorded.

**SourceLoader_ComputeTargetSize_BeyondMaximum_ThrowsSizeLimit**,
**SourceLoader_Load_PngLargerThanMaximum_ThrowsSizeLimitBeforeDecoding**, and
**SourceLoader_Load_SvgWithHugeNaturalSize_NeedsSizeOption**: exact, scaled, aspect-derived, and natural sizes beyond
8192 are `size-limit` with messages and hints that say which size is too large; 8192 itself is allowed; a PNG header
declaring 9000x9000 is refused for its size (not as corrupt) while probing still reports it; an SVG of 100000x50 is
refused at its natural size and renders at a width that is allowed.

**SourceLoader_Load_RasterBackground_FlattensOnlyWhenAsked** and **SourceLoader_FormatColor_OpaqueAndTranslucent_FormatsHex**:
a raster keeps its transparency by default and with a transparent background, and is flattened when a color is
given; colors are written `#RRGGBB` or `#RRGGBBAA`.

**SourceLoader_Load_JpegWithExif_AppliesOrientationUnlessDisabled** and
**SourceLoader_Load_JpegWithExifAndWidth_SizesTheOrientedImage**: orientations 6 and 8 swap the size, 3 keeps it, 1
changes nothing, and `AutoOrient = false` leaves the stored image; the operation is recorded; a requested width
applies to the upright image and the result equals orient-then-resize.

**ExifOrientation_Read_ExifSegment_ReturnsOrientation** and the other `ExifOrientation_Read_*` tests: all eight
orientations in both byte orders; no segment, not a JPEG, and empty data give `null`; values 0, 9, and 255 give
`null`; every prefix of the first 40 bytes is read without an exception; the segment is found after an APP0 and a
non-EXIF APP1 segment in a stream that cannot seek; the path overload reads a file and propagates the I/O failure of
a missing one; a null stream throws. **ExifOrientation_Apply_Orientation_MapsPixels** and
**ExifOrientation_Apply_InvalidArguments_Throw**: each orientation turns a 3x2 image of numbered pixels into the
layout the EXIF specification defines (including the swapped sizes), the source is unchanged, and invalid arguments
throw.

**SourceLoader_Load_DamagedFile_ThrowsToolExceptionOrLoads**,
**SourceLoader_Load_GarbageWithKnownExtension_ThrowsCorruptInput**, **SourceLoader_Load_MalformedSvg_ThrowsCorruptInput**,
**SourceLoader_Read_AllowListedExceptions_AreMappedToErrorCodes**, **SourceLoader_Read_MultiLineMessage_IsReducedToOneLine**,
and **SourceLoader_Read_UnexpectedExceptions_Propagate**: the error-mapping checks described above.

**SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException**: each null argument throws.
