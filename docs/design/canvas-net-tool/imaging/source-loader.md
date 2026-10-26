### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad letterboxed Visio EMU -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`SourceLoader` turns a path into facts (`Probe`) or into pixels (`Load`) for the files the tool reads. Its single
responsibility is to know how to read each source format: it recognizes the format, reads the header, applies the
size policy and the options that belong to reading (page, resolution, orientation, background, strictness), and
reports a bad file as an expected error. The unit consists of the static class `SourceLoader` (partial files
`SourceLoader.cs`, `SourceLoader.Detect.cs`, `SourceLoader.Raster.cs`, `SourceLoader.Svg.cs`,
`SourceLoader.Documents.cs`, `SourceLoader.Pdf.cs`, `SourceLoader.Pptx.cs`, `SourceLoader.Vsdx.cs`, and
`SourceLoader.Scad.cs`),
`ExifOrientation`, the pixel helpers `SurfaceOperations`, and the supporting types in `SourceModels.cs`.

#### Data Model

**SourceKind** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`, `Gif`, `Jpeg2000`, `Svg`, `Pdf`, `Pptx`, `Vsdx`, `Scad`, in the
order the `formats` command lists them; `ToFormatName` gives the lower-case member name used in output and JSON.
**OutputFormat** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`.

**SourceInfo** (record): `Path`, `Format`, `Bytes`, `Width`, `Height` (stored size; for a document the size of page 1
at 96 dots per inch; for a model 800 by 800, the natural size of a view), `Channels`, `HasAlpha`, `CanDecode`,
`FrameCount`, `PageCount` (1 for a single image or a model),
`ExifOrientation` (JPEG only), and the lists `Pages` (the pages of a document; empty for a single image or a model),
`Modules`, and `Functions` (the names a model defines at file scope, in source order; empty for every other kind),
and `Directives` (an `init` property: the `include` and `use` directives of a model as `SourceDirective` records of
kind, path, and line; empty for every other kind).

**SourceOptions** (record, all properties `init`): `Page` (1-based, default 1), `Dpi` (nullable, 96 when absent),
`Strict`, `Scale`, `Width`, `Height` (the size options), `Background` (a nullable `Rgba32`), `AutoOrient`
(default `true`), and `Scad` (a nullable `ScadViewOptions`; the defaults when absent).

**ScadViewOptions** (record, all properties `init`): `View` (a name from `ScadViews`, `iso` by default), `Azimuth`
and `Elevation` (nullable; they replace the angles of the view), `Perspective` (default `false`), `Defines` (the
variables as a dictionary of `double`, `bool`, and `string` values), `Time` (nullable, the value of `$t`),
`MaxTriangles` (nullable, the library default of 5,000,000 when absent), and `DefaultColor` (nullable). **ScadView**
(record): a view's `Name`, `Azimuth`, and `Elevation`. **SourceLoader.ScadViews** is the table of the views, in the
order the help lists them: `iso` (45 and the library's isometric elevation, about 35.26), `front` (0, 0), `back`
(180, 0), `left` (270, 0), `right` (90, 0), `top` (0, 90), and `bottom` (0, -90). The camera convention is the
library's: the eye is at `(sin az cos el, -cos az cos el, sin el)` looking at the model, Z up, so azimuth 0 looks
from the -Y side (the front) and positive azimuth moves the eye toward +X.

**LoadedSource** (record, `IDisposable`): the `Surface` (disposed with the record), the `Info` (for a document page,
sized at the natural size of that page at the requested resolution), the 1-based `Page`, `IsVector`, `Diagnostics`
(`ImageDiagnostic` records: source, severity, code, message, and the page), `Echo` (text printed by a model), and
`Operations` (`AppliedOperation` records: an operation name and its parameters in order; a parameter is a string, a
number, a flag, or a list of strings).

**PageSource** (abstract, `IDisposable`): a source opened for loading its pages; `ReadPage(page)` loads one. It is
what `OpenPages` returns.

**ExtensionKinds**: the table of file name extensions (lower case, with the dot) and their kinds; the `formats`
command lists the same extensions and a test keeps the two consistent.

#### Key Methods

**Detect(path)**: throws `input-unreadable` for a directory and `input-not-found` for a missing file. Reads the
first 4096 bytes and decides in this order: the signature bytes of PNG, JPEG, GIF, TIFF (either byte order),
JPEG 2000 (a JP2 signature box or a raw codestream), PDF, and BMP (the `BM` letters alone would also match text, so
the reserved fields and the size of the bitmap header that follows are checked too); for a ZIP signature, the
entries of the archive (`ppt/presentation.xml` is a presentation, `visio/document.xml` is a drawing, and any other
readable archive, such as a Word or Excel file, is `unsupported-format` whatever its extension), while an archive
that cannot be read falls back to the extension `.pptx` or `.vsdx` so that the load reports it as corrupt; SVG text
(a header without NUL bytes that contains `<svg`, unless the extension is `.scad`, because a model is text and
only a model's extension identifies it, whatever its comments mention); and finally the extension.
The content wins over the extension, but a file whose content says nothing keeps its extension's kind, so that an
empty or truncated `.png` is reported as corrupt by the load instead of as an unknown format. Otherwise throws
`unsupported-format` with a hint to run `canvasnet formats`.

**Probe(path)**: `Detect`, then reads the header with the codec's `GetInfo` (and, for a JPEG, the EXIF orientation
with `ExifOrientation.Read`) or, for an SVG, the intrinsic size with `SvgCodec.GetInfo`. A PDF, PPTX, or VSDX
document is opened with its library and every page is listed in `Pages` (`SourcePage`: the 1-based number, the size
in points and in pixels at 96 dots per inch, the rotation of a PDF page, the name of a VSDX page); the page count
is `PageCount`, and `Width` and `Height` are those of page 1. A PDF page's size is the library's displayed
(rotation-applied) size in points; a slide or a page of a drawing is converted from EMU (12,700 EMU per point). A
document with no pages is `corrupt-input`. An OpenSCAD model is parsed with `ScadDocument.Open(path)` (the parse
reads no other file and evaluates nothing): its `ModuleNames` and `FunctionNames` become `Modules` and `Functions`,
its `Directives` become `SourceDirective` records, and a syntax error is a `scad-error`. No pixels are decoded
(a GIF's `GetInfo` does decode the first frame's data, as the library documents).

**Load(path, options)** and **Load(info, options)**: open the source with `OpenPages` and read the page
`options.Page`. **OpenPages(info, options)** returns a `PageSource` (`IDisposable`) whose `ReadPage(page)` loads
one page; for a document it holds the parsed document, so that loading every page parses it once. `CheckPage`
throws `page-out-of-range` (message naming the page and the file, hint that pages count from 1) for a page that
is not in `1..PageCount`; a single image has the one page 1.

A *raster* load (`LoadRaster`)
refuses an image whose stored size exceeds the maximum, applies the EXIF orientation when `AutoOrient` is set and
the orientation is 2 to 8, computes the target size from the *oriented* size, checks it against the maximum, all
before decoding; then decodes, orients (`ExifOrientation.Apply`), resizes when the size differs, and flattens over
the background when one was given with some opacity. An *SVG* load (`LoadSvg`) computes the target size from the
intrinsic size, checks it, renders with `SvgCodec.Load(path, width, height)`, and flattens over the background
(white when none was given; none when the background has zero alpha). An SVG rendered at a size with another aspect
ratio than the drawing is fitted by the SVG codec's rule (preserve the aspect ratio, centered), so the drawing is
letterboxed.

A *document page* load (`DocumentPageSource`, shared by the PDF, PPTX, and VSDX page sources) sizes the page at
the resolution (`options.Dpi`, 96 by default; a resolution that is not finite and greater than zero is
`usage-error`, and a VSDX page needs a whole number of dots per inch) as `round(points * dpi / 72)` pixels per
side, at least 1; applies the size policy to that natural size (`ComputeTargetSize`, so `--scale` multiplies the
size at the resolution); when both a width and a height are given the page is fitted inside that box (largest
size with the aspect ratio of the page, never stretched) and centered over the background (a `fit` operation);
renders the page with the library's `Render(index, width, height, options)`, whose background color is the
background option (white by default, zero alpha for transparent), so no separate flatten step is needed; and
returns the page with `IsVector` set, the `Info` sized at the natural size of that page, and the notes the render
reported. The PPTX source passes `StrictUnsupportedPictures` and collects each skipped picture
(`OnUnsupportedPictureSkipped`) as an `ImageDiagnostic` (`pptx`, `unsupported`, the feature token such as
`pptx-image-metafile`, the first line of the message, the page); the VSDX source passes `ThrowOnUnsupportedFeature`
and collects each approximated construct (`UnsupportedFeatureHandler`) the same way (`vsdx`, for example
`vsdx-geometry-row-FutureRowKind`). The PDF library has no diagnostics channel and renders an unsupported feature
as an error, so a PDF page never has notes and `Strict` changes nothing for it.

An *OpenSCAD model* load (`ScadPageSource`) opens the model once and draws it with `ScadDocument.Render(width, height,
options)`. The target size is `ComputeTargetSize` of the natural size 800 by 800 (`DefaultScadSize`), so a scale
multiplies it, one side keeps the picture square, and two sides are exact; the renderer fits the camera to the
model and to the aspect ratio, so nothing is stretched. The view is the named viewpoint of `ScadViews` with
`Azimuth` and `Elevation` replacing its angles; an unknown name is a `usage-error` listing the views. The
projection is orthographic unless `Perspective` is set. The `ScadRenderOptions` carry the evaluation options (the
`Defines` as the variables, `Time` as `$t`, the limits with `MaxTriangles` when given, `ThrowOnUnsupportedFeature`
from `Strict`, and handlers that collect the `echo` lines, the warnings, and the unsupported features) and a
`SceneRenderOptions` whose `BackgroundColor` is the background option (white by default; zero alpha is a
transparent picture) and whose `DefaultColor` is the default color when one is given, so no flatten step is needed.
A warning becomes a note (`scad`, `warning`, `scad-warning`, the library's `file:line:column: message`); an
unsupported feature becomes a note (`scad`, `unsupported`, the feature identifier such as `scad-module-text`, the
position and message); the echo lines are returned as the library prints them, with their `ECHO:` prefix. A name
the library does not accept as a variable (it is an `ArgumentException` from constructing the options) is a
`usage-error`. The picture is the same for the same model and options: the library's render is deterministic.
The operation recorded is `render` with the `view`, the resolved `azimuth` and `elevation`, the `projection`
(`orthographic` or `perspective`), the size, the `background`, and, when they were given, `defines` (a list of
`name=value` in name order, text in quotes), `time`, `maxTriangles`, and `defaultColor`.

*Include and use.* The library resolves the `include` and `use` directives of a model: a relative path is looked up
from the folder of the file that names it, nothing outside the folder of the model (the base directory, which is the
folder of the file that was opened) is ever read, a link below that folder is not followed, and no library path or
file resolver is configured, so the folder of the model and below it are the only places a model can read. A path
that leaves the folder, an include cycle, nesting that is too deep, and a syntax error in an included file are errors
of the library. A directive whose file does not exist is only a *warning* in the library (as in OpenSCAD itself);
the loader treats that warning as an error, because a picture drawn without a file the model asked for would be
wrong without saying so: after the render the picture is discarded and a `scad-error` ("SCAD include not found: ...")
with a hint about where files are looked up is thrown. The match is on the library's warning text ("Can't open include
file"), which a test pins.

Each step that changed the pixels is recorded in `Operations` (`auto-orient` with the orientation,
`resize` with the size, `render` with the size - and for a document the page, the dots per inch, and the
background - `fit` with the box, `flatten` with the background as `#RRGGBB`). Surfaces replaced along the way are
disposed, including when a step throws.

**ComputeTargetSize(path, naturalWidth, naturalHeight, options)**: a scale multiplies both sides; a width and a
height are exact; a width alone derives the height from the aspect ratio, and a height alone derives the width;
with none, the natural size. The result is rounded to the nearest pixel (half away from zero) and is at least 1.
A side larger than `Surface.MaxDimension` throws `size-limit`, with a message that says whether the natural size
or the requested size is too large and a hint on how to choose a smaller one.

**Read(path, kind, action)**: runs a read and maps exceptions (see *Error Handling*).

**ExifOrientation.Read(stream or path)**: finds the orientation without throwing for malformed data. It checks
the JPEG start-of-image marker, walks the marker segments (at most 64, stopping at the start of the scan or the end
of the image, skipping fill bytes and segments without a length), reads the first `Exif` application segment, and
parses the TIFF structure inside it: the byte-order mark, the magic number 42, and the first directory, looking for
tag `0x0112` of type SHORT with count 1 and a value of 1 to 8. Anything else yields `null`.

**ExifOrientation.Apply(surface, orientation)**: returns a new surface turned upright: for each pixel of the result,
it fetches the source pixel the EXIF specification names (orientation 2 mirrors left to right, 3 turns by 180
degrees, 4 mirrors top to bottom, 5 transposes, 6 turns clockwise, 7 transverses, 8 turns counter-clockwise); for
5 to 8 the width and height are swapped.

**SurfaceOperations.FlattenOver(surface, background)** composites a surface over a solid color into a new surface;
**SurfaceOperations.HasTransparency(surface)** says whether any pixel is not opaque (used by `ImageWriter`).

#### Error Handling

`Read` maps these library exceptions (an explicit list, not a catch-all) to a `ToolException` with the file path in
a one-line message and the cause attached: `FileNotFoundException` and `DirectoryNotFoundException` to
`input-not-found`; `UnauthorizedAccessException` and any other `IOException` to `input-unreadable`;
`UnsupportedImageFeatureException`, `PptxUnsupportedFeatureException`, and `VsdxUnsupportedFeatureException` (each an
`IOException`, so caught first) to `unsupported-feature`, with the feature token in the message; and
`InvalidDataException`, `XmlException`, `EndOfStreamException`, and `FormatException` to `corrupt-input`. The SCAD
library adds three: `ScadUnsupportedFeatureException` (an `IOException`, so caught early, thrown in strict mode) to
`unsupported-feature` naming the feature and the position; `ScadSyntaxException` (a `FormatException`, so caught
before it) to `scad-error` ("SCAD syntax error: file:line:column: ..."); and `ScadEvaluationException` (an
`InvalidOperationException`) to `budget-exceeded` when it reports a limit - the library words every limit as
`(limit N)`, for example "Triangle limit exceeded (limit 5)" or "Recursion too deep (limit 5000)", and reports a
renderer budget failure with `RenderBudgetExceededException` as the cause, which `MapScadEvaluation` recognizes - and
otherwise to `scad-error` (a failed `assert`, an include problem, and so on). The hint of a triangle-limit failure
names `--max-triangles` and the default; the other limits cannot be changed from the command line and the hint says
to simplify the model. A model longer than the library reads is an `InvalidDataException`, so `corrupt-input`. Every
other exception - a null reference, an index out of range, an out-of-memory failure - is a defect and propagates to
the entry point, which reports it as an unexpected error and re-throws it. The tests feed every supported format
truncated and garbage files to prove that the allow-list covers the failures the libraries actually raise (for the
documents also every byte-prefix length of a sample, overwritten and zeroed middles, a valid archive with a broken
part, malformed PDF content, and an unsupported PDF encryption handler; for models a cut after every seventh
character of a model with several kinds of statement, garbage of several sizes, and some forty stress models - huge
numbers, degenerate or empty shapes, unsupported constructs, runaway recursion and loops, bad arguments, a deep nest of
parentheses, and unusual text); a gap found that way is closed by extending the list, and none was found for any
format. Size problems are `size-limit`; a page that does not exist is `page-out-of-range`. A source kind that the
loader has no case for - which cannot happen while every kind of `SourceKind` is handled - is an
`InvalidOperationException`, a defect, not a `ToolException`. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **ToolException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface`, `Rgba32`, and the PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 codecs.
- **CanvasNet SVG system** - `SvgCodec` renders SVG and reports its intrinsic size.
- **CanvasNet PDF, PPTX, and VSDX systems** - `PdfDocument`, `PptxDocument`, and `VsdxDocument` list and render the
  pages of a document, and report the features they cannot draw.
- **CanvasNet SCAD and Render3D systems** - `ScadDocument` parses and draws a model; `Camera`, `CameraProjection`, and
  `SceneRenderOptions` give the camera convention, the projection, and the background and default colors.

#### Callers

- **RenderCommand** - probes, then loads, and reports the operations.
- **DiscoveryCommands** - the `info` command probes.
- **ImageWriter** - uses `SurfaceOperations` (flatten) for JPEG output.
