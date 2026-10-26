## Imaging

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx dpi scad -->

### Verification Approach

The `Imaging` subsystem is verified at two tiers. The unit tests of `SourceLoader`, `ImagePipeline`, `ImageWriter`,
`ImageComparison`, and `ContactSheet`
(see _SourceLoader Unit Verification Design_, _ImagePipeline Unit Verification Design_, _ImageWriter Unit
Verification Design_, _ImageComparison Unit Verification Design_, and _ContactSheet Unit Verification Design_
under `imaging/`) verify the units in isolation. The subsystem tests in `ImagingSubsystemTests.cs`
verify them working together, the way every
command uses them: a source file of each supported kind (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF, PPTX, VSDX,
and SCAD) is loaded and written as each output format (PNG, JPEG, BMP, and TIFF) - an input-by-output matrix of 44
cases -
and the output is decoded with the codec of its format and compared with the pixels the codec or the document library
decodes or renders from the source (or, for the SVG, with the drawing flattened over white, and for the model, the
library's orthographic isometric render at 800x800): exactly for a lossless
output, on average for JPEG. Every named view of a model is also written as every output format and compared with the
library's render for that view (28 cases). The tests generate their inputs in a temporary directory (see
`Fixtures/README.md`
for the fixture files and for how the PDF, PPTX, and VSDX documents and the models are written by the test project).

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

### Test Scenarios

**Imaging_Convert_InputKindToOutputFormat_WritesTheSourcePixels**: for each of the 44 combinations the output decodes
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
