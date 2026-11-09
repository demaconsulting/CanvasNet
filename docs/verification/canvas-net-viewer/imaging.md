## Imaging

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx dpi scad deskew deskewed -->

### Verification Approach

The `Imaging` subsystem is verified at two tiers. The unit tests of `SourceLoader`, `ImagePipeline`, `ImageWriter`,
`ImageComparison`, `ContactSheet`, `GridOverlay`, `ImageRenderer`, `ImageInspector`, `ImageComparer`, and
`MontageBuilder` (see _SourceLoader Unit Verification Design_, _ImagePipeline Unit Verification Design_, _ImageWriter
Unit Verification Design_, _ImageComparison Unit Verification Design_, _ContactSheet Unit Verification Design_,
_GridOverlay Unit Verification Design_, _ImageRenderer Unit Verification Design_, _ImageInspector Unit Verification
Design_, _ImageComparer Unit Verification Design_, and _MontageBuilder Unit Verification Design_
under `imaging/`) verify the units in isolation; the rotation, the skew estimation, the comparison metrics and pictures,
and the analysis of colors are core `Surface` operations verified in the `Canvas` subsystem (see _SurfaceGeometry_,
_SurfaceComparison_, and _SurfaceAnalysis Unit Verification Design_). The subsystem tests in `ImagingSubsystemTests.cs`
verify them working together, the way every
operation uses them: a source file of each supported kind (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF, PPTX, VSDX,
SCAD, Vega-Lite, and Mermaid) is loaded and written as each output format (PNG, JPEG, BMP, and TIFF) - an
input-by-output matrix of 52 cases -
and the output is decoded with the codec of its format and compared with the pixels the codec or the document library
decodes or renders from the source (or, for the SVG, with the drawing flattened over white, and for the model, the
library's orthographic isometric render at 800x800, for the chart, the library's render at its laid-out size, and for
the diagram, the library's render at its natural size over white):
exactly for a lossless
output, on average for JPEG. Every named view of a model is also written as every output format and compared with the
library's render for that view (28 cases). The tests generate their inputs in a temporary directory (see
`Fixtures/README.md`
for the fixture files and for how the PDF, PPTX, and VSDX documents and the models are written by the test project). The
pictures that a chart or a diagram shows (the requirements of the Imaging subsystem for chart and diagram images) are
verified by the tests of the pictures of the `SourceLoader` unit (`SourceLoaderVegaLiteImageTests.cs` and
`SourceLoaderMermaidImageTests.cs`; see _SourceLoader Unit Verification Design_), which load real files of every
linked kind through the sandboxed resolver.

### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Every input kind can be written as every output format with the source's pixels.
- Every page of a document loaded from one opened source is written with the pixels the library renders for it.
- The same input gives the same output bytes on every run, for a lossless and for the lossy output.
- A bad input, or a page that does not exist, is an expected error and leaves no output.
- Sources loaded by the loader (a vector source into a box of the reference's size) are compared exactly, the pictures of
  the differences are written, and the same comparison gives the same bytes.
- Sources of several kinds are composed one at a time into a contact sheet with the documented layout and written.
- The render, inspect, compare, and montage operations chain the loader, the pipeline, and the writer on sources of
  several kinds with the documented results.

### Test Scenarios

**Imaging_Convert_InputKindToOutputFormat_WritesTheSourcePixels**: for each of the 52 combinations the output decodes
to the source pixels - exactly for PNG, BMP, and TIFF outputs, within a mean channel difference of 8 for JPEG (for the
GIF fixture, a pixel-by-pixel pattern of saturated colors that JPEG's chroma subsampling cannot reproduce, only the
size is checked).

**Imaging_Convert_ScadViewToOutputFormat_WritesTheLibraryPixels**: each of the seven named views of a model that looks
different from every side is loaded at 48x48 and written as each output format; the file decodes to exactly the
pixels the SCAD library draws with that view's azimuth and elevation and an orthographic projection (on average for
JPEG).

**Imaging_Convert_SameInputTwice_WritesIdenticalBytes**: converting each input kind (including a model) twice to PNG
and to JPEG gives identical bytes.

**Imaging_Convert_TransparentPng_FlattensOnlyForJpeg**: a PNG with a transparent edge keeps the transparency as a PNG
and is white there as a JPEG.

**Imaging_Convert_RequestedSize_WritesThatSize**: a raster, an SVG, and a model loaded at an exact size are written at
that size.

**Imaging_Convert_DocumentPages_WritesEachPageWithItsPixels**: both pages of a PDF, a presentation, and a drawing are
loaded from one opened source and written as PNG; each file has the size of its page and exactly the pixels the library
renders at that size.

**Imaging_Convert_CorruptInput_LeavesNoOutput**: a truncated PNG is `corrupt-input`, and neither the output nor a
temporary file exists.

**Imaging_Convert_PageOutOfRange_LeavesNoOutput**: page 3 of a two-page document is `page-out-of-range`, and nothing is
written.

**Imaging_Pipeline_LoadedSource_TransformedAndWrittenOnce**: a PNG, an SVG, the PDF, and the model are loaded at their
natural size, taken through a crop of the left half, a quarter turn, and a blue border by the pipeline, and written as
PNG: the three operations are reported in order, the file decodes to exactly the transformed surface, which equals the
same operations applied by hand to the cropped load, the border pixel is blue, and the loaded surface is unchanged.

**Imaging_Photo_LoadedSourceAdjustedDeskewedTrimmedAndWritten**: a JPEG of a sketch with ruled lines tilted by 4 degrees,
stored a quarter turn away with the EXIF orientation 6, is loaded upright (400x300), taken through the pipeline
operations auto-levels, deskew, and trim, and written as PNG: the three operations are reported in order, the contrast
stretch was applied, the skew found is within 0.4 degrees of 4 and was corrected, the trim cropped the white border
the rotation left (the result is smaller than the rotated canvas), the PNG decodes to exactly the transformed surface,
and the skew that remains in it is within 0.4 degrees of level.

**Imaging_Photo_SameInputsTwice_WriteIdenticalBytes**: the same photo pipeline run twice on the same JPEG reports the same
operations with the same parameters (the angle, the confidence, and the points) and writes identical PNG bytes.

**Imaging_Compare_LoadedSourcesAreComparedAndVisualized**: the SVG fixture is loaded at its natural size and compared
with a raster copy of it that has one black pixel at 50,40, the vector being loaded into a box of the raster's size with
`SourceLoader.BoxOptions`: exactly one pixel differs and it is in the grid cell that holds it; the difference,
side-by-side, and overlay pictures are written with `ImageWriter` and decode at 100x80, 316x80, and 100x80, and the
difference picture is black at 0,0 and not at the changed pixel.

**Imaging_Compare_SameInputsTwice_WriteIdenticalBytes**: the same comparison and pictures twice give equal metrics and
grid cells and identical bytes of every picture.

**Imaging_Montage_LoadedSourcesAreComposedAndWritten**: an SVG, the first page of the PDF, and a PNG are each opened,
loaded into a 64x64 cell with `BoxOptions`, added to a labeled `ContactSheet` of two columns, and disposed in turn; the
sheet is written as PNG, has the layout of the design (140x184), and every cell has pixels other than the background.

**Imaging_Render_DocumentPages_AreLoadedTransformedAndWritten**: both pages of the PDF are rendered with `ImageRenderer`
into a folder with a quarter turn in the request: two files are written, one for each page, each decodes to the size
the result reports, and the `rotate` step is reported for each page.

**Imaging_Inspect_LoadedSource_ReportsColorsAndPixel**: `ImageInspector` reports a red PNG as one color covering the
image at the size of the image, and the pixel at a point as a sample without colors.

**Imaging_Compare_ComparerLoadsAndMeasuresTwoKinds_AndWritesThePicture**: `ImageComparer` compares the PNG fixture with
the SVG fixture loaded at the size of the first (`ResizeTarget("a", 0, 0)`): both sides have one size, the metrics are
present, and the one difference picture that was asked for is written.

**Imaging_Montage_BuilderComposesSeveralKinds_AndWritesOneSheet**: `MontageBuilder` composes an SVG, the PDF, and a PNG
into a labeled sheet of two columns: the layout has three cells in two rows, and the file decodes to the size of the
layout.

**Imaging_RequestDefaults_CompareRequestFromInputsAlone_CarriesCommandDefaults**: a `CompareRequest` built from its two
inputs alone has no resize, the contain fit, the automatic filter, an opaque white background, the default tolerance,
grid, gain, and overlay weight, no thresholds and no output paths, the heat style, and the default write options, and
the inputs carry page 1 and default source options.

**Imaging_RequestDefaults_OptionalMembers_CarryCommandDefaults**: a `RenderRequest`, an `InspectRequest`, and a
`MontageRequest` built from their required members alone, and a `SheetOptions` and a `WriteOptions` with no member set,
carry the defaults of the render, inspect, and montage commands (a single page, no operations, 8 colors, a radius of 0,
automatic orientation, a 256 pixel cell with a spacing and a padding of 8, no labels, and a JPEG quality of 90).

**Imaging_RequestDefaults_PublishedConstants_AreTheCommandDefaults**: the published constants are 8 colors, a 256 pixel
cell, and a spacing and a padding of 8.

**Imaging_RequestDefaults_InitProperties_CanBeSetAndCopied**: the optional members are set in an initializer and
copied with `with` while the required members stay as they were.

**Imaging_RequestDefaults_OperationDefaults_AreTheCommandDefaults**: `InspectRequest.ForColors` and `ForPixel` equal the
requests built with the constructor, the auto-levels operation clips 1 percent by default, resize and fit use the
automatic filter, and `Dimension.Pixels` and `Dimension.Percent` build the two kinds of dimension.

**Imaging_Diagnostics_Origin_DefaultsToEmptyAndIsComparedByEntries**: an `ImageDiagnostic` has an empty origin by
default, and two diagnostics are equal exactly when their fields and their origin entries are equal.
