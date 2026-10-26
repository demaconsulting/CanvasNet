### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif gif RRGGBB RRGGBBAA pptx vsdx dpi docx scad openscad Minkowski -->

#### Verification Approach

`SourceLoader` (with `ExifOrientation` and `SurfaceOperations`) is verified with unit tests in
`SourceLoaderTests.cs`, `SourceLoaderVectorTests.cs` (the page-based sources), `SourceLoaderScadTests.cs` (the
OpenSCAD models), and `ExifOrientationTests.cs`. The
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
extension and requiring `corrupt-input` (`scad-error` for a model). The models have their own sweep in
`SourceLoaderScadTests.cs`, because a model is text and a cut one is
usually still a valid, shorter model: a model with several kinds of statement is cut after every seventh character,
garbage of four sizes is added, and some forty stress models probe the library for exceptions that are not mapped; a
one-off sweep during development (about 950 truncations and random byte
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
- A model is drawn from the named view chosen (iso by default) at 800x800 pixels unless a size is given, with the
  variables, time, default color, and triangle limit given, the same picture every time; its include and use files
  are read from its folder and below only; a missing include, a syntax error, and a failed assertion are `scad-error`,
  an exceeded limit is `budget-exceeded`, and an unsupported feature is a note or, in strict mode, an error.
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

**SourceLoader_Detect_UnknownFormat_ThrowsUnsupportedFormat** and
**SourceLoader_Detect_MissingFileOrDirectory_ThrowsInputError**: an unknown file is
`unsupported-format` with a hint; a missing file is `input-not-found` and a directory `input-unreadable`.

**SourceLoader_Detect_ScadMentioningSvg_ReturnsScad**: a file named `.scad` whose comment contains the text of an SVG
start tag is a model, not an SVG, while the same text without the `.scad` extension is still taken for an SVG.

**SourceLoader_Probe_EachKind_ReportsHeaderFacts**, **SourceLoader_Probe_PngAndSvg_ReportChannelsAndAlpha**, and
**SourceLoader_Probe_JpegWithExif_ReportsOrientation**: for each kind the format, path, byte count, size, decodability,
frame count, and page facts are right (a document has two pages and lists them; a model is 800x800 with one module,
`block`, one function, `half`, and no directives); a PNG with transparency and an SVG
report four channels with alpha; a JPEG with an EXIF segment reports its orientation and its stored size.

**SourceLoader_Probe_ModelWithDirectives_ListsModulesFunctionsAndDirectives**: a model with an `include`, a `use`, two
modules, and a function declared twice lists the names in source order (the repeated name kept) and the two directives
with their lines, although the files they name do not exist (probing opens no other file); it has one page, no pages
list, and the natural size 800x800.

**SourceLoader_Probe_SyntaxError_ThrowsScadErrorWithLocation**,
**SourceLoader_Load_FailedAssert_ThrowsScadError**, **SourceLoader_Probe_SourceTooLong_ThrowsCorruptInput**, and
**SourceLoader_Probe_DeeplyNestedExpression_ThrowsScadError**: a syntax error is a `scad-error` of one line that
starts with "SCAD syntax error", names the file, line, and column, and keeps the library's exception as its cause; a
failed `assert` is a `scad-error` with the message and the position; a model one character over the 4,194,304 the
library reads is `corrupt-input` ("exceeds the maximum length"); parentheses nested 400 deep are a `scad-error`.

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

**SourceLoader_BoxOptions_VectorKinds_SetBoxAndClearScale**, **SourceLoader_BoxOptions_RasterKinds_AreUnchanged**,
**SourceLoader_BoxOptions_NullArguments_ThrowArgumentNullException**, and **SourceLoader_Load_VectorWithBox_RendersNativelyAtBox**:
for each vector kind the box options carry the box and no scale and keep every other option; for each raster kind the
very same options come back; null arguments throw; an SVG loaded with the box options is exactly the box, and a 480x360
slide loaded into a 300x300 box is 300x225 centered with bands of the background above and below.

**SurfaceOperations_FitInside_RoundsAndClamps**, **SurfaceOperations_FitInside_NonPositiveSize_ThrowsArgumentOutOfRange**,
**SurfaceOperations_FitInto_Contain_ScalesToLimitingSideAndCenters**, **SurfaceOperations_FitInto_Stretch_ResamplesToExactBox**,
**SurfaceOperations_FitInto_SameSize_ReturnsEqualPixels**, and **SurfaceOperations_FitInto_InvalidArguments_Throw**: the
fitted size follows the limiting side, enlarges a small image, rounds half away from zero, and never leaves 1 to the
box; a contain fit centers the scaled image on the fill color and a stretch fills the box; an image that already has the
box size is an equal, independent copy in both modes; a null source or an undefined mode throws.

**SourceLoader_Load_Background_IsWhatThePageIsRenderedOver**: a transparent background leaves an unpainted pixel
transparent and black gives black, for each document; the `render` operation names the background.

**SourceLoader_Load_DefaultOptions_DrawsIsometricOrthographicAt800** and
**SourceLoader_Load_DefaultOptions_MatchesTheLibraryRender**: the model fixture loads at 800x800 as a vector source
with no notes and one `render` operation (view `iso`, azimuth 45, the library's isometric elevation, `orthographic`,
the size, and `#FFFFFF`), white in the corner and covered in the middle; at 64x64 the pixels equal the library's own
render of the same model with an orthographic projection.

**SourceLoader_Load_EachNamedView_IsDistinctAndDeterministic**, **SourceLoader_ScadViews_HaveTheDocumentedAngles**,
**SourceLoader_Load_OppositeViews_HaveMirroredSilhouettes**, **SourceLoader_Load_AzimuthAndElevationOverride_ReplaceTheViewAngles**,
**SourceLoader_Load_ViewName_IsCaseInsensitiveAndValidated**, and **SourceLoader_Load_Perspective_DiffersFromOrthographic**:
a model that looks different from every side (a slab with a tower near one corner and a sphere near the opposite one)
is drawn from each of the seven views twice; each picture is neither empty nor full, the same both times, and no
two views give the same picture; the table has the angles iso (45 and the isometric elevation), front (0, 0), back
(180, 0), left (270, 0), right (90, 0), top (0, 90), and bottom (0, -90) in the order of the help; the covered pixels
of front and back, and of left and right, are mirror images of each other to within a twentieth of the covered area;
an azimuth of 90 on the front view equals the right view, an elevation of 90 equals the top view, and an override
alone starts from `iso`; the name is accepted in any case and another name is a `usage-error` whose hint lists the
views; a perspective view differs from the orthographic one and says so in its operation.

**SourceLoader_Load_SizeOptions_FollowTheNaturalSize**, **SourceLoader_Load_Background_IsWhiteColoredOrTransparent**,
**SourceLoader_Load_DefaultColor_ColorsUncoloredSurfaces**, and **SourceLoader_Load_ModelWithoutGeometry_IsTheBackground**:
a scale of 0.1 gives 80x80, a width of 50 gives 50x50, two sides are exact, and a width over the limit is
`size-limit`; the background is white by default, blue when given, and transparent when it has no alpha (the corner
is clear and the middle covered), each named in the operation; a plain cube is gray, red with a default color, and a
cube the model colors blue stays blue; an empty model, a model of only comments, and one that only defines things
draw nothing but the background.

**SourceLoader_Load_Defines_ChangeTheModel**, **SourceLoader_Load_Defines_AreDescribedByNameAndType**,
**SourceLoader_Load_DefineWithInvalidName_ThrowsUsageError**, and **SourceLoader_Load_Time_SetsTheSpecialVariable**: a
number, a flag, and a text each replace the assignment in a parametric model and give four different pictures; the
operation lists them as `name=value` in name order with text in quotes; a name with a dollar sign in the middle is
a `usage-error` that mentions `-D`; `$t` changes the model and is listed only when given.

**SourceLoader_Load_EchoWarningAndUnsupported_AreReturned** and
**SourceLoader_Load_UnsupportedFeatureInStrictMode_ThrowsUnsupportedFeature**: a model that echoes, calls a module
that does not exist, and uses `text` returns the echo line with its `ECHO:` prefix, a `scad-warning` note with the
position, and an `unsupported` note whose code is `scad-module-text`, and the picture is still drawn; with strict mode
the same model is an `unsupported-feature` error that names the feature and the position in one line, with a hint
that mentions `--strict`.

**SourceLoader_Load_TriangleLimitExceeded_ThrowsBudgetExceeded**, **SourceLoader_Load_TriangleLimitLargeEnough_Loads**,
**SourceLoader_Load_EvaluationLimitExceeded_ThrowsBudgetExceeded**, and
**SourceLoader_MapScadEvaluation_LimitAndOtherFailures_MapToTheirCodes**: a limit of one triangle is `budget-exceeded`
with "Triangle limit exceeded (limit 1)", the position, and a hint that names `--max-triangles`; a limit of 100,000
loads and is recorded in the operation; runaway recursion and a loop of a billion iterations are `budget-exceeded`
with a hint that the limit cannot be changed; a library failure worded with a limit, or caused by the renderer's
budget, maps to `budget-exceeded`, and any other failure to `scad-error`.

**SourceLoader_Load_IncludeAndUse_AreReadFromTheModelFolderAndBelow**,
**SourceLoader_Load_NestedInclude_IsRelativeToTheIncludingFile**, **SourceLoader_Load_MissingInclude_ThrowsScadError**,
**SourceLoader_Load_IncludeOutsideTheModelFolder_ThrowsScadError**, and
**SourceLoader_Load_IncludeCycleAndBrokenInclude_ThrowScadError**: a model that includes a module from a subfolder and
uses a function from another file draws the same picture as the same model in one file, with no notes; an include
inside an included file is relative to that file; an `include` and a `use` of a file that does not exist are a
`scad-error` that starts with "SCAD include not found", names the position, and has a hint about where files are
looked up; an include of an existing file next to the model's folder is refused as "outside the allowed
directories"; an include cycle and a syntax error in an included file are `scad-error`, the latter naming the
included file and the position.

**SourceLoader_Read_ScadExceptions_AreMappedToErrorCodes**: the library's syntax and evaluation exceptions map to
`scad-error`, an evaluation exception worded with a limit to `budget-exceeded`, and its unsupported-feature exception
to `unsupported-feature`; each keeps the position in one line and the exception as its cause.

**SourceLoader_Load_TruncatedAndGarbageModels_GiveAPictureOrToolException** and
**SourceLoader_Load_StressModels_GiveAPictureOrToolException**: a model with several kinds of statement (a variable,
a function, a module, a boolean, a loop, an echo) cut after every seventh character, garbage of four sizes, and some
forty stress models (huge numbers, a negative or zero size, wrong argument types, extrusions and hulls of nothing,
unsupported modules such as `import` and `surface`, runaway recursion, a list of a million elements, a byte-order
mark, a NUL byte, non-ASCII names, modifier characters) are probed and drawn at 16x16 pixels; each either gives a
picture or raises a `ToolException` - any other exception type fails the test, which is how a gap in the allow-list
would be found. None was found.

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
Garbage in a file named `.scad` is a `scad-error` rather than `corrupt-input`, the one kind whose damage is a syntax
error; **SourceLoader_OpenPages_UnhandledKind_IsNotAToolException**: facts that name a kind the loader has no case for
raise an `InvalidOperationException`, which is a defect and not an expected error.

**SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException** and
**SourceLoader_OpenPages_NullArguments_ThrowArgumentNullException**: each null argument throws.
