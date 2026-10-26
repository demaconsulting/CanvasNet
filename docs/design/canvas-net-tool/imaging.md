## Imaging

![Imaging Structure](ImagingView.svg)

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad openscad -->

### Overview

The `Imaging` subsystem (namespace `DemaConsulting.CanvasNet.Tool.Imaging`, folder
`src/DemaConsulting.CanvasNet.Tool/Imaging/`) turns files into pixels, transforms pixels, and turns pixels into
files. It is free of command-line concerns: it knows nothing of `Context` or the commands, and the dependency runs
one way (`Commands` use `Imaging`); the one exception is that `ImagePipeline` reads its operations from an
`ArgumentReader`, because the parsing of the transformation options belongs with the transformations. It is a
subsystem rather than a single unit because loading, transforming, and writing are separate responsibilities with
distinct requirements and tests. It contains five units: `SourceLoader`, which recognizes a file, reads its
header, and loads it (PNG, JPEG, BMP,
TIFF, GIF, JPEG 2000, SVG, the pages of PDF, PPTX, and VSDX documents, and a view of an OpenSCAD model),
`ImagePipeline`, which crops, resizes, fits, rotates, flips, and pads pixels in the order the options were written,
`ImageWriter`, which writes PNG,
JPEG, BMP, and TIFF files, and `ImageComparison`, which compares two images of the same size (error metrics, a
structural similarity, a per-cell grid, and threshold checks) and creates difference, overlay, and side-by-side
pictures, and `ContactSheet`, which composes a grid of images, each fitted into a cell, with optional labels drawn in
the bundled font. The supporting
types shared by the units (`SourceKind`, `OutputFormat`, `SourceInfo`, `SourceOptions`, `ScadViewOptions`,
`LoadedSource`, `AppliedOperation`, `ImageDiagnostic`, `SourcePage`, and the pixel helpers `SurfaceOperations`) are
documented in the design of the unit that owns them; the operation records of the pipeline are documented with
`ImagePipeline`.

### Interfaces

**SourceLoader.Detect(path)**, **SourceLoader.Probe(path)**, **SourceLoader.Load(path, options)**,
**SourceLoader.Load(info, options)**, and **SourceLoader.OpenPages(info, options)** (provided to the commands):
recognize a file, read its header facts (for a document including every page, for a model its modules, functions,
and directives), load one page into a
`LoadedSource` that owns its `Surface` and says what was done to produce it, and open a document once to load
several of its pages.

**ImagePipeline.Parse(reader, verb, verbOperand)** and **ImagePipeline.Apply(input, operations, defaultPadColor,
out applied)** (provided to the commands): read the ordered operations of a command line (the operation of a verb
command first), and apply them to a surface, giving a new surface and the list of what was done.

**ImageWriter.ResolveFormat**, **ImageWriter.CheckTarget**, **ImageWriter.CheckTargets**, and **ImageWriter.Write**
(provided to the commands): choose the output format, check an output (or all the outputs of a command against all its
inputs) before any work is done, and write a surface atomically.

**ContactSheet.CalculateLayout(count, options)**, **new ContactSheet(count, options)**, **ContactSheet.Add(image, label)**,
and **ContactSheet.Detach()** (provided to the commands): calculate the geometry of a sheet (refusing one that is too
large) before anything is loaded, then compose the sheet one image at a time and hand it to the caller.

**ImageComparison.Compare(a, b, tolerance, columns, rows)**, **ImageComparison.Evaluate(result, thresholds)**,
**ImageComparison.CreateDiff**, **ImageComparison.CreateOverlay**, and **ImageComparison.CreateSideBySide** (provided to
the commands): compare two surfaces of the same size, check the result against thresholds, and create the pictures.

**Surface and the codecs** (consumed, from the CanvasNet core library): `Surface` holds the pixels; `PngCodec`,
`JpegCodec`, `BmpCodec`, `TiffCodec`, `GifCodec`, and `Jpeg2000Codec` decode and encode; `SvgCodec` (from the
CanvasNet SVG system) renders SVG; `PdfDocument`, `PptxDocument`, and `VsdxDocument` (from the CanvasNet PDF, PPTX,
and VSDX systems) list and render the pages of a document; `ScadDocument` (from the CanvasNet SCAD system, which
draws with the Render3D system) parses and draws a model.

### Design

A load is: detect the format (signature bytes, then ZIP entries, then SVG text, then extension); read the header
(`Probe`); refuse an image that is too large; compute the target size from the options; decode or render; apply
what the options ask for (EXIF orientation, resize, flatten over a background); and return the pixels with the list
of operations that were applied, which commands report as the `operations` of a JSON result. A vector source (SVG,
a page of a PDF, PPTX, or VSDX document, or a view of an OpenSCAD model) is rendered directly at the target size,
which is sharper than
rendering at the natural size and resampling; a raster source is decoded at its own size and resized afterwards.
A page of a document is chosen by its number counted from 1 and sized at a resolution in dots per inch (96 by
default); every page of one document is loaded from one opened source. A model is drawn from a named viewpoint
(`iso`, `front`, `back`, `left`, `right`, `top`, or `bottom`, in the camera convention of the Render3D library: the
eye on the side the name says, Z up) at 800 by 800 pixels unless a size is given, with the variables and limits
the options set; the files its `include` and `use` directives name are read from the folder of the model and below
it only. A write is: resolve the format, check the
target, flatten transparency for JPEG, and encode into a temporary file in the target directory (staging); moving
it over the target (publishing, `ImageWriter.PublishAll`) is a separate step, so a command that writes several outputs
touches no target until every one was encoded and then publishes them all or nothing (an existing target is renamed to
a backup first, and a failure rolls the published targets back).

Between the load and the write a command may transform the pixels with the pipeline: the operations are applied
left to right, each making a new surface from the previous one, so one decode and one encode serve the whole chain.
The size options of a source (`--scale`, `--width`, `--height`, `--size`) belong to the load: a vector source is
rendered at the target size and a raster source is resized once, by the loader (with the chosen filter), before the
first operation of the pipeline runs, so a vector source is never scaled twice. The pipeline's own `--resize` and
`--fit` work on the pixels as they are at that point.

The subsystem owns the policy for the things that must be decided in one place so that every command agrees:
the size policy (including the maximum of `Surface.MaxDimension` pixels per side, enforced before decoding
wherever a header gives the size), the page numbering and resolution, the views of a model, and the mapping of
library exceptions to expected errors. The mapping is an
explicit list of exception types in `SourceLoader.Read`; any other exception is a defect and propagates to the
entry point, which reports and re-throws it. `Imaging` adds no dependency on a system other than the CanvasNet
core library (its pixel, codec, font, and text-rendering parts; `ContactSheet` draws its labels with the text renderer and
the bundled font) and the CanvasNet SVG, PDF, PPTX, VSDX, SCAD, and Render3D systems. Every source kind is read; a
further kind is added by a new partial file of `SourceLoader` and one new case in `Probe` and in `OpenPages`.
