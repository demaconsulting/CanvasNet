### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif gif RRGGBB RRGGBBAA pptx vsdx dpi docx -->

#### Verification Approach

`SourceLoader` (with `ExifOrientation` and `SurfaceOperations`) is verified with unit tests in
`SourceLoaderTests.cs`, `SourceLoaderVectorTests.cs` (the page-based sources), and `ExifOrientationTests.cs`. The
tests generate their inputs: rasters are written in memory
with the CanvasNet codecs (a gradient that differs at every pixel and channel, so a mirroring, rotation, or offset
changes it), the GIF with a small encoder in the test project (the libraries have none), EXIF segments are injected
into a codec-written JPEG in either byte order, and a PNG that declares an oversized image but holds no pixels is
built from its header. The documents are written by the test project (`FixtureFactory.Pdf.cs`, `.Pptx.cs`, and
`.Vsdx.cs`): a two-page PDF with a rotated second page, and a two-slide presentation and a two-page drawing built
with `System.IO.Compression`, each with a few filled rectangles and no text so that the pixels do not depend on
installed fonts; variants add an EMF picture, an unsupported shape, an unsupported geometry row, malformed content,
an unsupported encryption handler, and a broken part. The fixture files are an original SVG drawing and a JPEG 2000
image copied from the
repository's own codec fixtures (see `Fixtures/README.md`). Expected pixels come from the codecs and document
libraries themselves or from the generator, never from the loader. The tests use real files in a temporary
directory that each test deletes.

Error mapping is verified in four ways: directly, by feeding `Read` each allow-listed exception and a set of
unexpected ones (which must propagate unchanged); by loading, for every supported format, files that are empty, one
byte, a header only, half, and short by one byte, and garbage of the file's own length, and requiring that each
either loads or raises a `ToolException` with an expected code - an unexpected exception type fails the test, which
is how gaps in the allow-list are found; by doing the same for each document with eight prefix lengths, a dropped
tail, and an overwritten or zeroed middle, probing and loading every page; and by loading garbage with each known
extension and requiring `corrupt-input`. A one-off sweep during development (about 950 truncations and random byte
changes per document kind) raised only expected errors, so the allow-list needed no extension for PDF, PPTX, or VSDX.

#### Test Environment

N/A - standard test environment; the tests need no network and write only to temporary directories.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every supported format is recognized by its signature or its package entries, and the content wins over the
  extension.
- The header facts of every format are reported without decoding, including every page of a document.
- Lossless rasters load with exactly their pixels; SVG renders at the requested size over white.
- A page of a document is chosen by its number from 1, rendered at 96 dpi or the resolution given, with the pixels
  the library renders, over white or the background given, and a page that does not exist is `page-out-of-range`.
- Skipped pictures and approximated constructs are reported with their page, or are `unsupported-feature` in strict
  mode.
- The size policy follows the documented rules; a side beyond 8192 pixels is `size-limit`, before decoding.
- A JPEG is turned upright by its EXIF orientation unless switched off, and the size applies to the upright image.
- A damaged file of any format is an expected error; an unexpected exception type is never mapped.

#### Test Scenarios

**SourceLoader_Detect_EachKind_ReturnsKind**, **SourceLoader_Detect_SignatureAndExtensionDisagree_TrustsSignature**,
**SourceLoader_Detect_SvgTextWithoutExtension_ReturnsSvg**,
**SourceLoader_Detect_UnrecognizedContent_FallsBackToExtension**, **SourceLoader_Detect_ZipPackage_UsesExtension**,
**SourceLoader_Detect_ZipPackages_UsesEntriesOverExtension**,
**SourceLoader_Detect_OtherSignatures_ReturnsKind**, **SourceLoader_Detect_TextStartingWithBm_IsNotBmp**, and
**SourceLoader_ExtensionKinds_AreWellFormed**: each supported kind is recognized by its content; a PNG named
`.jpg` is a PNG and SVG text named `.png` is an SVG; SVG text is recognized without an extension, after a byte
order mark, a prolog, and a comment; content that matches nothing keeps the kind of its extension; a damaged ZIP
signature is a presentation or a drawing by extension, while a readable archive is told by its entries (a presentation
named `.vsdx` is a presentation, one with no extension is a presentation, and a Word-like archive named `.pptx` is
`unsupported-format`); PDF, a raw JPEG 2000 codestream, and both TIFF byte orders are
recognized; text that starts with `BM` is not a bitmap; the extension table is lower case with a dot and covers
every kind.

**SourceLoader_Detect_UnknownFormat_ThrowsUnsupportedFormat**,
**SourceLoader_Detect_MissingFileOrDirectory_ThrowsInputError**, and
**SourceLoader_ProbeAndLoad_KindNotLoadableYet_ThrowsUnsupportedFormat**: an unknown file is
`unsupported-format` with a hint; a missing file is `input-not-found` and a directory `input-unreadable`; a
recognized kind that is not readable yet (OpenSCAD) is `unsupported-format` for both operations.

**SourceLoader_Probe_EachKind_ReportsHeaderFacts**, **SourceLoader_Probe_PngAndSvg_ReportChannelsAndAlpha**, and
**SourceLoader_Probe_JpegWithExif_ReportsOrientation**: for each kind the format, path, byte count, size, decodability,
frame count, and page facts are right (a document has two pages and lists them); a PNG with transparency and an SVG
report four channels with alpha; a JPEG with an EXIF segment reports its orientation and its stored size.

**SourceLoader_Probe_Pdf_ListsPagesWithSizeAndRotation**, **SourceLoader_Probe_Pptx_ListsSlidesWithPresentationSize**,
and **SourceLoader_Probe_Vsdx_ListsPagesWithNamesAndSizes**: the PDF lists 144x96 and (rotated) 96x144 points with the
rotation, the presentation two slides of 360x270 points, and the drawing the pages `First` and `Second` with their
own sizes; each in pixels at 96 dpi as well.

**SourceLoader_Load_LosslessRaster_KeepsEveryPixel** and **SourceLoader_Load_GifJpegAndJpeg2000_MatchTheCodecs**:
PNG, BMP, and TIFF load with exactly the generated pixels; the GIF loads with its palette image; the JPEG and the
JPEG 2000 image equal what their codecs decode; none reports operations, diagnostics, or echo.

**SourceLoader_Load_FirstPage_RendersOverWhiteAt96Dpi**, **SourceLoader_Load_SecondPage_RendersThatPage**, and
**SourceLoader_OpenPages_EveryPage_MatchesLoadingEachAlone**: each document renders its first page at 96 dpi with its
rectangles where the document puts them and white elsewhere (vector, one `render` operation, no notes); page 2 is the
rotated PDF page (displayed size, yellow square at the top left), the second slide, and the second drawing page of
its own size; both pages loaded in reverse order from one opened source equal the pages loaded alone.

**SourceLoader_Load_PageThatDoesNotExist_ThrowsPageOutOfRange**: page 0 and page 3 of a document, and page 2 of a PNG
and of an SVG, are `page-out-of-range` with a message naming the page and the file.

**SourceLoader_Load_Dpi_SizesThePage**, **SourceLoader_Load_Dpi_MatchesTheLibraryRender**, and
**SourceLoader_Load_DpiNotUsable_ThrowsUsageError**: resolutions from 48 to 192 dpi give the documented pixel sizes for
each document (with rounding half away from zero), the dpi is recorded in the `render` operation, the PDF at 120 dpi
equals the library's own render at 120 dpi pixel for pixel; zero and not-a-number are `usage-error`, and so is a
fractional resolution for a drawing.

**SourceLoader_Load_SizeOptions_ActOnTheNaturalSize** and **SourceLoader_Load_WidthAndHeight_FitsThePageInsideTheBox**:
scale, width, and height act on the page's size at the resolution; two sides given together fit the page inside that
box without stretching it, centered over the background (white or green), recording a `fit` operation, and need no
fitting when the box has the page's aspect ratio.

**SourceLoader_Load_Background_IsWhatThePageIsRenderedOver**: a transparent background leaves an unpainted pixel
transparent and black gives black, for each document; the `render` operation names the background.

**SourceLoader_Load_SizeBeyondMaximum_ThrowsSizeLimit**: a resolution or a width that makes a page larger than 8192
pixels is `size-limit`.

**SourceLoader_Load_PptxMetafilePicture_IsSkippedAndReported**,
**SourceLoader_Load_PptxMetafilePictureStrict_ThrowsUnsupportedFeature**,
**SourceLoader_Load_PptxUnsupportedShape_ThrowsUnsupportedFeature**,
**SourceLoader_Load_VsdxUnsupportedRow_IsReportedAndStrictRefuses**, and
**SourceLoader_Load_PdfStrict_HasNoDiagnostics**: an EMF picture is skipped with one `pptx` note (code
`pptx-image-metafile`, page 1) and the rest of the slide is drawn, with none on slide 2; strict mode makes it
`unsupported-feature` naming the file; a shape with a preset that does not exist is `unsupported-feature` with the
feature token in the message, strict or not; an unknown geometry row is one `vsdx` note
(`vsdx-geometry-row-FutureRowKind`) with the page drawn, and `unsupported-feature` when strict; a PDF has no notes
and strict mode changes nothing.

**SourceLoader_Load_SvgBackground_TransparentKeepsAlphaColorFlattens** and
**SourceLoader_Load_Svg_RendersNaturalSizeOverWhite** and
**SourceLoader_Load_SvgWithSizeOptions_RendersAtRequestedSize**: the SVG fixture renders at 100x80 with the red
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
**SourceLoader_Load_DamagedDocument_ThrowsToolExceptionOrLoads**,
**SourceLoader_Load_GarbageWithKnownExtension_ThrowsCorruptInput**, **SourceLoader_Load_MalformedSvg_ThrowsCorruptInput**,
**SourceLoader_Load_PdfWithBadContentOrEncryption_ThrowsExpectedErrors**,
**SourceLoader_Load_PackageWithBrokenPart_ThrowsCorruptInput**,
**SourceLoader_Read_AllowListedExceptions_AreMappedToErrorCodes**, **SourceLoader_Read_MultiLineMessage_IsReducedToOneLine**,
and **SourceLoader_Read_UnexpectedExceptions_Propagate**: the error-mapping checks described above; in addition a PDF
whose rectangle operator has too few operands is `corrupt-input`, one that declares an unsupported encryption handler
is `unsupported-feature`, and a valid archive whose slide, page, or presentation part is not valid is `corrupt-input`.

**SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException** and
**SourceLoader_OpenPages_NullArguments_ThrowArgumentNullException**: each null argument throws.
