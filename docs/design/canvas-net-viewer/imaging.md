## Imaging

![Imaging Structure](ImagingView.svg)

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad openscad luma otsu grayscale deskews -->

### Overview

The `Imaging` subsystem (namespace `DemaConsulting.CanvasNet.Viewer.Imaging`, folder
`src/DemaConsulting.CanvasNet.Viewer/Imaging/`) turns files into pixels, transforms pixels, and turns pixels into
files, and chains those steps into the operations that the `canvasnet` command-line tool offers: render, inspect,
compare, and montage. It is the library form of the tool. It is free of command-line concerns: it knows nothing of
the tool's `Context`, its commands, or its option text, and the dependency runs one way (the commands of the
`CanvasNetTool` system use `Imaging`). The tool parses the text of its options into the operation records and
request records of this subsystem (the `ImagePipelineParser` type of the `RenderCommand` unit of the tool) and prints
the result records. It is a subsystem rather than a single unit because loading, transforming, and writing are
separate responsibilities with distinct requirements and tests. It contains ten units: `SourceLoader`, which
recognizes a file, reads its header, and loads it (PNG, JPEG, BMP,
TIFF, GIF, JPEG 2000, SVG, the pages of PDF, PPTX, and VSDX documents, a view of an OpenSCAD model, a Vega-Lite chart,
and a Mermaid diagram),
`ImagePipeline`, which crops, resizes, fits, rotates by any angle, flips, pads, adjusts the tone of, deskews, trims, and
draws a grid on pixels in the order the operations are given,
`ImageWriter`, which writes PNG,
JPEG, BMP, and TIFF files, and `ImageComparison`, which checks a comparison of two images against thresholds and holds
the defaults of the comparison options, `ContactSheet`, which composes a grid of images, each fitted into a cell,
with optional labels drawn in
the bundled font, and `GridOverlay`, which draws a labeled coordinate grid over pixels in place. Four operation
units chain those units: `ImageRenderer` (load, transform, and write the pages of a source), `ImageInspector` (load,
transform, and analyze), `ImageComparer` (load two inputs, measure, check thresholds, write pictures), and
`MontageBuilder` (load several sources into one contact sheet). The failures of every unit are
`ViewerException` values with the stable `ErrorCodes` (the `ViewerException` unit of the `CanvasNetViewer` system). The
pixel arithmetic that is not specific to the tool is part of the core
`Surface` class and its units of the `Canvas` subsystem: flip, pad, trim, fit, flatten, and the EXIF orientation
(_SurfaceTransforms Unit Design_), the tone adjustments (_SurfaceAdjustments Unit Design_), the rotation by any angle
and the skew estimation (_SurfaceGeometry Unit Design_), the comparison of two surfaces and the difference pictures
(_SurfaceComparison Unit Design_), and the analysis of colors (_SurfaceAnalysis Unit Design_); the units here delegate
to it. The
supporting
types shared by the units (`SourceKind`, `OutputFormat`, `SourceInfo`, `SourceOptions`, `ScadViewOptions`, `ChartFacts`,
`DiagramFacts`, `DiagramOptions`, `LoadedSource`, `SourceAnchor`, `AppliedOperation`, `ImageDiagnostic`, and
`SourcePage`) are documented in the design of the unit that owns them; the operation records of the pipeline (and the
`Dimension`, `GeometrySpec`, `PadSpec`, and `FlipAxis` values they carry) are documented with `ImagePipeline`, and the
request and result records of an operation with the operation unit.

**Record style.** One rule applies to every request and option record of the subsystem (`RenderRequest`,
`InspectRequest`, `CompareRequest`, `CompareInput`, `MontageRequest`, `SheetOptions`, and `WriteOptions`, like the
existing `SourceOptions` and `ViewerRenderOptions`): the positional members are exactly the required data, and every
optional member is an `init` property whose default is the default of the corresponding `canvasnet` command-line
option, with the numeric defaults published as constants (`ImageInspector.DefaultColors`, `ContactSheet.DefaultCellSize`,
`DefaultSpacing`, `DefaultPadding`, `ImageWriter.DefaultQuality`, and the defaults of `ImageComparison`). A request built
from its required members alone therefore does what the command does without options, `with` copies a request with
one member changed, and an optional member can be added later without breaking a caller. Optional constructor parameters
are not used because the defaults (colors, lists, nested records) are not compile-time constants. The result records
are built by the library only and stay positional. The tool passes every value explicitly, so its output does not depend
on these defaults. Pages are numbered from 1 throughout this subsystem (the format packages count from 0).

### Interfaces

**SourceLoader.Detect(path)**, **SourceLoader.Probe(path)**, **SourceLoader.Load(path, options)**,
**SourceLoader.Load(info, options)**, and **SourceLoader.OpenPages(info, options)** (provided to the operation units and
to library users): recognize a file, read its header facts (for a document including every page, for a model its
modules, functions,
and directives, for a chart its laid-out size, marks, data references, parameters, title, and schema version, and for a
diagram its laid-out size, type, whether it is drawn, direction, titles, and numbers of nodes, edges, and subgraphs),
load one page into a
`LoadedSource` that owns its `Surface` and says what was done to produce it, and open a document once to load
several of its pages.

**ImagePipeline.Apply(input, operations, defaultPadColor, out applied, userBackground)** (provided to the operation units
and to library users): apply an ordered list of operation records to a surface (calling the core `Surface`
adjustments, flip, pad, trim, rotation, and skew estimation) and report what was done; the values of the records are
validated first, so an invalid value is a `usage-error` (or `size-limit`) before any step runs. **ImagePipeline.TryParseFilter**,
**ImagePipeline.FilterName**, and **ImagePipeline.FilterNames** name the resampling filters (the public tables of the
imaging units, such as `FilterNames`, `ExtensionFormats`, `ExtensionKinds`, and `ScadViews`, are read-only and cannot
be changed through a cast). The text of the operations
of a command line is read by the tool, not by this unit.

**ImagePipeline.MapAnchors(anchors, width, height, applied)** (provided to the operation units): follow the pixel
positions of
the anchors an OpenSCAD model declared (`LoadedSource.Anchors`) through the list of what the pipeline did, so that a
position stays on the same spot of the processed picture (and is lost, rather than wrong, for a step it cannot follow).

**ImageWriter.ResolveFormat** and **ImageWriter.Write** (provided to the operation units and to library users): choose the
output format and write a surface atomically; **ImageWriter.CheckTarget** and **ImageWriter.CheckTargets**, which check
an output (or all the outputs of an operation against all its inputs) before any work is done, and the staging and
publishing of several outputs are used inside the subsystem.

**ImageRenderer.Execute(info, request)**, **ImageInspector.Execute(info, request)**, **ImageComparer.Execute(request)**,
and **MontageBuilder.Execute(infos, request)** (provided to library users and to the commands of the tool): the four
operations, each taking plain request records and returning plain result records.

**ContactSheet.CalculateLayout(count, options)**, **new ContactSheet(count, options)**, **ContactSheet.Add(image, label)**,
and **ContactSheet.Detach()** (provided to `MontageBuilder`, and to the tool for its early layout check): calculate the
geometry of a sheet (refusing one that is too
large) before anything is loaded, then compose the sheet one image at a time and hand it to the caller.

**GridOverlay.Draw(surface, spacing, color)** (provided to the pipeline): draw a labeled coordinate grid in place and
report the lines and labels drawn.

**ImageComparison.Evaluate(result, thresholds)**, **ImageComparison.MetricName**, **ImageComparison.TryParseMetric**,
**ImageComparison.IsMinimum**, and **ImageComparison.Range** (provided to `ImageComparer` and the tool): check a comparison
result of the
core `Surface.Compare` against thresholds and describe the metrics; the constants of the unit are the defaults of the
comparison options. The comparison itself, the difference pictures, and the analysis of colors are called directly on the
core `Surface` (`Compare`, `CreateDiff`, `CreateOverlay`, `CreateSideBySide`, `Analyze`, `Sample`).

**Surface and the codecs** (consumed, from the CanvasNet core library): `Surface` holds the pixels; `PngCodec`,
`JpegCodec`, `BmpCodec`, `TiffCodec`, `GifCodec`, and `Jpeg2000Codec` decode and encode; `SvgCodec` (from the
CanvasNet SVG system) renders SVG; `PdfDocument`, `PptxDocument`, and `VsdxDocument` (from the CanvasNet PDF, PPTX,
and VSDX systems) list and render the pages of a document; `ScadDocument` (from the CanvasNet SCAD system, which
draws with the Render3D system) parses and draws a model; `VegaLiteDocument` (from the CanvasNet Vega-Lite system)
parses, measures, and draws a chart; `MermaidDocument` (from the CanvasNet Mermaid system) parses, measures, and draws a
diagram.

### Design

A load is: detect the format (signature bytes, then ZIP entries, then SVG text, then file name and extension); read
the header
(`Probe`); refuse an image that is too large; compute the target size from the options; decode or render; apply
what the options ask for (EXIF orientation, resize, flatten over a background); and return the pixels with the list
of operations that were applied, which commands report as the `operations` of a JSON result. A vector source (SVG,
a page of a PDF, PPTX, or VSDX document, a view of an OpenSCAD model, a Vega-Lite chart, or a Mermaid diagram) is
rendered directly at
the target size,
which is sharper than
rendering at the natural size and resampling; a raster source is decoded at its own size and resized afterwards.
A page of a document is chosen by its number counted from 1 and sized at a resolution in dots per inch (96 by
default); every page of one document is loaded from one opened source. A model is drawn from a named viewpoint
(`iso`, `front`, `back`, `left`, `right`, `top`, or `bottom`, in the camera convention of the Render3D library: the
eye on the side the name says, Z up; with no view chosen the library decides: `iso` for a 3D model, `top` for a
model that is entirely 2D, or the view the model sets with its viewport variables) at 800 by 800 pixels unless a size
is given, with the variables and limits
the options set; the files its `include` and `use` directives name are read from the folder of the model and below
it only. A chart is laid out by the library at its natural size, which the probe reports, and is drawn at the target
size with UTC dates and only the bundled fonts over its own background (white by default); nothing is read from the
network. A diagram is laid out by the library at its natural size, which the probe reports without loading the pictures
of its nodes, and is drawn at that size times the scale exactly where Mermaid places it, or fitted into the box of the
size options, with only the bundled fonts over white (or the background given), in the theme the options or the fragment
of the input choose; the pictures of its nodes are read through the same sandboxed resolver as those of an SVG file. A
write is: resolve the format, check the target, flatten transparency for JPEG, and encode into a temporary file in the
target directory (staging); moving it over the target (publishing, `ImageWriter.PublishAll`) is a separate step, so a
command that writes several outputs touches no target until every one was encoded and then publishes them all or nothing
(an existing target is renamed to a backup first, and a failure rolls the published targets back).

Between the load and the write an operation may transform the pixels with the pipeline: the operations are applied
left to right, each making a new surface from the previous one, so one decode and one encode serve the whole chain.
The size options of a source (`--scale`, `--width`, `--height`, `--size` of the tool, which the tool turns into the size
settings of `SourceOptions`) belong to the load: a vector source is
rendered at the target size and a raster source is resized once, by the loader (with the chosen filter), before the
first operation of the pipeline runs, so a vector source is never scaled twice. The pipeline's own resize and
fit operations work on the pixels as they are at that point.

The four operation units chain the others and own the order of the steps, which the commands of the tool rely on:
the checks that need only the request (the page, the output format, the outputs against the inputs, the size of a
sheet) come first, so that nothing is decoded for a request that is bound to fail; then the loading, the pipeline,
and the staging of every output; and last the publishing of all the outputs together, so that a failure leaves no
output behind. A mistake that the user made is a `ViewerException` with a code from `ErrorCodes`; a threshold that a
comparison does not meet is data in the result, not an exception, and the tool decides to fail.

The subsystem owns the policy for the things that must be decided in one place so that every command agrees:
the size policy (including the maximum of `Surface.MaxDimension` pixels per side, enforced before decoding
wherever a header gives the size), the page numbering and resolution, the views of a model, the fixed deterministic
render options of a chart and of a diagram, and the mapping of
library exceptions to expected errors. The mapping is an
explicit list of exception types in `SourceLoader.Read`; any other exception is a defect and propagates to the
entry point, which reports and re-throws it. `Imaging` adds no dependency on a system other than the CanvasNet
core library (its pixel, codec, font, and text-rendering parts; `ContactSheet` draws its labels with the text
renderer and
the bundled font) and the CanvasNet SVG, PDF, PPTX, VSDX, SCAD, Render3D, Vega-Lite, and Mermaid systems. Every source
kind is read; a
further kind is added by a new partial file of `SourceLoader` and one new case in `Probe` and in `OpenPages`.
