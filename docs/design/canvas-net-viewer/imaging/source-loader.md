### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad letterboxed Visio EMU embedder -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`SourceLoader` turns a path into facts (`Probe`) or into pixels (`Load`) for the files the tool reads. Its single
responsibility is to know how to read each source format: it recognizes the format, reads the header, applies the
size policy and the options that belong to reading (page, resolution, orientation, background, strictness), and
reports a bad file as an expected error. The unit consists of the static class `SourceLoader` (partial files
`SourceLoader.cs`, `SourceLoader.Detect.cs`, `SourceLoader.Raster.cs`, `SourceLoader.Svg.cs`,
`SourceLoader.Documents.cs`, `SourceLoader.Pdf.cs`, `SourceLoader.Pptx.cs`, `SourceLoader.Vsdx.cs`,
`SourceLoader.Scad.cs`, `SourceLoader.VegaLite.cs`, `SourceLoader.SvgImages.cs`, and `SourceLoader.Input.cs`),
the input types `InputReference` and `InputSelection` (`InputReference.cs`, `InputSelection.cs`),
and the supporting types in `SourceModels.cs`. The EXIF orientation is read by the core `JpegCodec.ReadExifOrientation`
and applied by the core `Surface.ApplyExifOrientation`. The pixel helpers that used to be part of this unit
(`FlattenOver`, `HasTransparency`, `FitInside`, and `FitInto`, with the `FitMode` enum) are methods of the core
`Surface` class (the `SurfaceTransforms` unit of the `Canvas` subsystem).

#### Data Model

**SourceKind** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`, `Gif`, `Jpeg2000`, `Svg`, `Pdf`, `Pptx`, `Vsdx`, `Scad`,
`VegaLite`, in the
order the `formats` command lists them; `ToFormatName` gives the lower-case member name used in output and JSON
(`vega-lite` for `VegaLite`, the one name that is not the lower-case member name). `IsChart(kind)` is true for
`VegaLite`.
**OutputFormat** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`.

**SourceInfo** (record): `Path`, `Format`, `Bytes`, `Width`, `Height` (stored size; for a document the size of page 1
at 96 dots per inch; for a model 800 by 800, the natural size of a view; for a chart the size the library lays it
out at), `Channels`, `HasAlpha`, `CanDecode`,
`FrameCount`, `PageCount` (1 for a single image or a model),
`ExifOrientation` (JPEG only), and the lists `Pages` (the pages of a document; empty for a single image or a model),
`Modules`, and `Functions` (the names a model defines at file scope, in source order; empty for every other kind),
`Directives` (an `init` property: the `include` and `use` directives of a model as `SourceDirective` records of
kind, path, and line; empty for every other kind), and `Chart` (an `init` property: a `ChartFacts` record - the `Marks`
as written, the `DataReferences` as `ChartDataReference` records of `Kind` (`inline`, `url`, `name`, or `sequence`),
`Url`, `Rows`, and `Line`, the `Parameters` names, the `Title`, and the `SchemaMajorVersion` - for a Vega-Lite chart;
`null` for every other kind), `Selection` (an `init` property: what the fragment of the input chose, an
`InputSelection`; `null` for an input without a fragment) and `Specifier` (computed: the path, and `#` and the fragment
when there is a selection).

**SourceOptions** (record, all properties `init`): `Page` (1-based, default 1), `Dpi` (nullable, 96 when absent),
`Strict`, `Scale`, `Width`, `Height` (the size options), `Background` (a nullable `Rgba32`), `AutoOrient`
(default `true`), and `Scad` (a nullable `ScadViewOptions`; the defaults when absent).

**ScadViewOptions** (record, all properties `init`): `View` (a name from `ScadViews`, `iso` by default), `ViewGiven`
(`true` when `--view` was written, even `--view iso`, which is how the command line asks for the isometric view of a
model that is entirely 2D; for `compare` the per-input views set it too), `Azimuth`
and `Elevation` (nullable; they replace the angles of the view), `IsViewExplicit` (computed: `ViewGiven`, or an
`Azimuth` or an `Elevation` was given, or `View` is a name other than `iso`), `Perspective` (default `false`),
`Defines` (the
variables as a dictionary of `double`, `bool`, `string` and vector (`List<object?>`) values; by default one shared
empty read-only dictionary),
`Time` (nullable, the value of `$t`),
`MaxTriangles` (nullable, the library default of 5,000,000 when absent), and `DefaultColor` (nullable). **ScadView**
(record): a view's `Name`, `Azimuth`, and `Elevation`. **SourceLoader.ScadViews** is the read-only table of the views
(it
cannot be changed, not even through a cast), in the
order the help lists them: `iso` (45 and the library's isometric elevation, about 35.26), `front` (0, 0), `back`
(180, 0), `left` (270, 0), `right` (90, 0), `top` (0, 90), and `bottom` (0, -90). The camera convention is the
library's: the eye is at `(sin az cos el, -cos az cos el, sin el)` looking at the model, Z up, so azimuth 0 looks
from the -Y side (the front) and positive azimuth moves the eye toward +X.

**LoadedSource** (record, `IDisposable`): the `Surface` (disposed with the record), the `Info` (for a document page,
sized at the natural size of that page at the requested resolution), the 1-based `Page`, `IsVector`, `Diagnostics`
(`ImageDiagnostic` records: source, severity, code, message, and the page, with the optional `Detail`, `Location`, and
the `Origin`: the embedded files,
outermost first, that the note is about, empty by default and compared entry by entry), `Echo` (text printed by a
model),
`Anchors` (`SourceAnchor` records, an init property that is empty for every kind but a model: the name, the world
point `X`, `Y`, `Z`, and the nullable continuous pixel position `PixelX` and `PixelY` in the loaded surface), and
`Operations` (`AppliedOperation` records: an operation name and its parameters in order; a parameter is a string, a
number, a flag, or a list of strings).

**PageSource** (abstract, `IDisposable`): a source opened for loading its pages; `ReadPage(page)` loads one. It is
what `OpenPages` returns.

**ExtensionKinds**: the read-only table (it cannot be changed, not even through a cast) of file name extensions (lower
case, with the dot) and their kinds (`.vl` is the
chart extension); the `formats` command lists the same extensions and a test keeps the two consistent. The two-part
suffix of a chart, `.vl.json` (`VegaLiteJsonSuffix`), cannot be an extension (the extension of such a file is `.json`),
so it is checked on the file name and the `formats` listing adds it to the extensions of the `vega-lite` row.

The extensions an SVG `image` `href` may link are not a table of the tool: they are `CanvasNetViewer.Extensions`, the
union of what the image sources of the core and format libraries claim (the raster extensions, `.svg`, `.pdf`,
`.pptx`, `.vsdx`, `.scad`, `.vl`, and `.vl.json`; a plain `.json` is not linkable). The list is documented in the `svg`
help topic, and a unit test compares it with the topic in both directions (and with the `formats` table). The
resolver decides by the file name, not by the content, unlike `Detect`, which is what the tool's own commands use
for the file they are given.

#### Key Methods

**Detect(path)**: throws `input-unreadable` for a directory and `input-not-found` for a missing file. Reads the
first 4096 bytes and decides in this order: the signature bytes of PNG, JPEG, GIF, TIFF (either byte order),
JPEG 2000 (a JP2 signature box or a raw codestream), and BMP (the `BM` letters alone would also match text, so
the reserved fields and the size of the bitmap header that follows are checked too) - all recognized by the core
`ImageFormats.Detect` and mapped to the matching kind - and then PDF (the one signature this tool adds itself);
for a ZIP signature, the
entries of the archive (`ppt/presentation.xml` is a presentation, `visio/document.xml` is a drawing, and any other
readable archive, such as a Word or Excel file, is `unsupported-format` whatever its extension), while an archive
that cannot be read falls back to the extension `.pptx` or `.vsdx` so that the load reports it as corrupt; SVG text
(a header without NUL bytes that contains `<svg`, unless the extension is `.scad`, because a model is text and
only a model's extension identifies it, whatever its comments mention); and finally the extension. A Vega-Lite chart
is text without a signature as well, so it is recognized before the SVG sniff, by the file name and the header: a
file name ending in `.vl.json` (checked on the whole name, in any case, because `Path.GetExtension` of it is only
`.json`) is a chart; a file with the extension `.json` is a chart when its header contains
`vega.github.io/schema/vega-lite/` (the address every version of the schema is under); and the extension `.vl` is a
chart by the extension table. A `.json` file, a `.vl` file, and a `.vl.json` file are never taken for an SVG, whatever
their text mentions (the title of a chart can contain `<svg`). A signature still wins: a PNG named `x.vl.json` is a PNG.
A `.json` file whose top-level `$schema` string property names `vega.github.io/schema/vega/` is a plain Vega
specification,
which the tool does not read, and any other `.json` file is not a chart; both are `unsupported-format`, the first with
the
message "is a Vega specification; only Vega-Lite charts are supported" and the hint "Vega is not supported", the second
with a hint that a chart is recognized by the suffix `.vl.json` (or `.vl`) or by its top-level `$schema` in the first
4 KiB, so that the file can be renamed. The `$schema` is read with a `Utf8JsonReader` over the 4 KiB header (a document
cut off by the end of the header is not an error; nested values are skipped; comments, trailing commas, and a byte order
mark are tolerated), so the schema address in another property, in a nested object, or in an array element never
makes a chart.
The content wins over the extension, but a file whose content says nothing keeps its extension's kind, so that an
empty or truncated `.png` is reported as corrupt by the load instead of as an unknown format. Otherwise throws
`unsupported-format` with a hint to run `canvasnet formats`.

**Probe(path)**: `Detect`, then reads the header with the codec's `GetInfo` (and, for a JPEG, the EXIF orientation
with `JpegCodec.ReadExifOrientation`) or, for an SVG, the intrinsic size with `SvgCodec.GetInfo`. A PDF, PPTX, or VSDX
document is opened with its library and every page is listed in `Pages` (`SourcePage`: the 1-based number, the size
in points and in pixels at 96 dots per inch, the rotation of a PDF page, the name of a VSDX page); the page count
is `PageCount`, and `Width` and `Height` are those of page 1. A PDF page's size is the library's displayed
(rotation-applied) size in points; a slide or a page of a drawing is converted from EMU (12,700 EMU per point). A
document with no pages is `corrupt-input`. An OpenSCAD model is parsed with `ScadDocument.Open(path)` (the parse
reads no other file and evaluates nothing): its `ModuleNames` and `FunctionNames` become `Modules` and `Functions`,
its `Directives` become `SourceDirective` records, and a syntax error is a `scad-error`. A Vega-Lite chart is parsed
with `VegaLiteDocument.Open(path)` (the parse reads no other file and draws nothing; the folder of the path becomes the
base directory of the library's data sandbox) and
measured with `Measure()` (the library lays the chart out, which reads its data files from that folder and below it, but
draws no pixel): the size is
`Width` and `Height`, there are 4 channels with transparency, one page, and no `Pages`, and the `ChartFacts` come from
the document (`MarkTypes`, `DataReferences`, `ParameterNames`, `Title`, `SchemaMajorVersion`). A chart that lays out
larger than `Surface.MaxDimension` makes the library throw `ArgumentOutOfRangeException`, which is `size-limit`. No
pixels
are decoded (a GIF's `GetInfo` does decode the first frame's data, as the library documents).

**Load(path, options)** and **Load(info, options)**: open the source with `OpenPages` and read the page
`options.Page`. **OpenPages(info, options)** returns a `PageSource` (`IDisposable`) whose `ReadPage(page)` loads
one page; for a document it holds the parsed document, so that loading every page parses it once. `CheckPage`
throws `page-out-of-range` (message naming the page and the file, hint that pages count from 1) for a page that
is not in `1..PageCount`; a single image has the one page 1.

A *raster* load (`LoadRaster`)
refuses an image whose stored size exceeds the maximum, applies the EXIF orientation when `AutoOrient` is set and
the orientation is 2 to 8, computes the target size from the *oriented* size, checks it against the maximum, all
before decoding; then decodes, orients (`Surface.ApplyExifOrientation`), resizes when the size differs, and flattens
over
the background when one was given with some opacity. An *SVG* load (`LoadSvg`) computes the target size from the
intrinsic size, checks it, renders with `SvgCodec.LoadWithFontFaces(path, width, height, null, options)` whose
`SvgLoadOptions.ImageResolver` is the image resolver described below, and flattens over the background
(white when none was given; none when the background has zero alpha). An SVG rendered at a size with another aspect
ratio than the drawing is fitted by the SVG codec's rule (preserve the aspect ratio, centered), so the drawing is
letterboxed. The notes, echo lines, and `embed` operations that the resolver collected are the diagnostics, echo,
and operations of the loaded source.

**SVG embedding** (`SourceLoader.SvgImages.cs`, the internal class `SvgImages`). The SVG library never reads a file
for an `image` reference; it calls the `IImageResolver` of `SvgLoadOptions.ImageResolver`. For every SVG load the tool
creates the resolver with `CanvasNetViewer.CreateImageResolver(folder, options)`, where the folder is the folder of the
SVG file the user named (the sandbox root, the same rule as for `include` in a model and for the data files of a chart:
there is no option for another folder) and `ThrowOnUnsupportedFeature` carries `--strict` to the sources. The whole of
the embedding behavior belongs to the libraries: the sandbox, the extension dispatch, the sizes (a vector file at the
size of the placement over a transparent background, a chart keeping its own background, a raster at its natural size),
the nesting depth (4), the cycle check, the count limit (64), the diagnostics with the `href` in front, and the
no-caching rule are specified and tested in the core `ImageSources` subsystem and the format packages. The tool has no
plug-in mechanism: it never passes extra sources, so the supported types are exactly `CanvasNetViewer.Extensions`.

`SvgImages` is the thin wiring around that resolver and keeps only the tool's own policy:

- **Mapping.** After the render, `Notes()` maps each `ImageSourceDiagnostic` to an `ImageDiagnostic` (the severity
  `Info`, `Warning`, and `Unsupported` become `info`, `warning`, and `unsupported`; the `Origin` chain of the core
  diagnostic is carried over unchanged, and the message and the code are not touched, so the output of the tool does
  not change), the printed text becomes the echo,
  and `Operations()` maps each `EmbeddedImage` with `Depth == 0` (a file the SVG file itself embeds; files that an
  embedded SVG file embeds are not listed, as before) to an `embed` operation (`href`, `format`, `width`, `height`).
  The outer sizing, page, resolution, and model options are not passed on: the resolver draws an embedded file from
  its own defaults.
- **Strict.** With `--strict`, `ThrowForUnresolved()` turns the first `svg-image-unresolved` or `svg-image-too-large`
  diagnostic into a `ViewerException` with code `unsupported-feature`, message
  `'<svg>' embeds '<href>', which was not drawn: <reason>.` and the hint that without `--strict` the image is reported
  as the warning with that code. The check runs after the render, so the surface
  is disposed and the work of the rest of the picture is spent first.
- **Errors.** A file that is found but cannot be drawn reaches the tool as an `ImageSourceException` (the resolver
  wraps whatever the source threw; the original is the inner exception with its type intact). `Map` runs the inner
  exception through the tool's `Read` allow-list, so every code is the one the same failure has when the file is
  rendered alone (`corrupt-input`, `scad-error`, `vega-lite-error`, `unsupported-feature`, `budget-exceeded`), and
  re-wraps it with the message `'<embedder>' embeds '<href>': <original message>`, where the embedder is the file
  that contains the `href` (the SVG file itself, or the nested SVG file when the failure is deeper). An inner
  exception that the allow-list does not map is a defect and is rethrown as itself.

An unresolved reference is the warning `svg-image-unresolved` (source `svg`, message
`image '<href>' was not drawn: <reason>`) and nothing is drawn; a picture that is too large for the pixel budget of the
render (64 million pixels in all, charged by the resolver before anything is decoded) is the warning
`svg-image-too-large` in the same form; in strict mode either is the `unsupported-feature` error
above (the same rule as a skipped picture of a PPTX slide). A query or fragment after the file name of an `href` is not
part of the name (the resolver cuts it off), and `KindOfName` ignores it when it words an error.

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
`Azimuth` and `Elevation` replacing its angles; an unknown name is a `usage-error` listing the views. Only an
**explicit** view (`IsViewExplicit`) is passed to the library as `ViewAzimuthDegrees` and `ViewElevationDegrees`;
otherwise the loader leaves the angles unset and the library decides: the isometric view for a model that is
3D, a flat top view for a model whose whole result is 2D (for example a `projection()` drawing sheet), or the view
that the model sets with its viewport variables `$vpr`, `$vpt`, `$vpd`, and `$vpf`. An explicit view wins over the
viewport variables of the model. The projection is orthographic unless `Perspective` is set, and is always passed. The
`ScadRenderOptions` carry the evaluation options (the
`Defines` as the variables, `Time` as `$t`, the limits with `MaxTriangles` when given, `ThrowOnUnsupportedFeature`
from `Strict`, and handlers that collect the `echo` lines, the warnings, and the unsupported features), a
`ResolvedViewHandler` that receives the view the library used, an `AnchorHandler` that adds a `SourceAnchor` (the name,
the world point, and the pixel the library projected with the camera of this very render, or none) to the list that
becomes `LoadedSource.Anchors` for every anchor the model declares with `echo("anchor", "name", [x, y, z])` (both
option constructions, with and without an explicit view, install it), and a
`SceneRenderOptions` whose `BackgroundColor` is the background option (white by default; zero alpha is a
transparent picture) and whose `DefaultColor` is the default color when one is given, so no flatten step is needed.
A warning becomes a note (`scad`, `warning`, `scad-warning`, the library's `file:line:column: message`); an
unsupported feature becomes a note (`scad`, `unsupported`, the feature identifier such as `scad-module-surface`, the
position and message); the echo lines are returned as the library prints them, with their `ECHO:` prefix. A name
the library does not accept as a variable (it is an `ArgumentException` from constructing the options) is a
`usage-error`. The picture is the same for the same model and options: the library's render is deterministic.
The operation recorded is `render` with the `view`, the resolved `azimuth` and `elevation`, the `projection`
(`orthographic` or `perspective`), the size, the `background`, and, when they were given, `defines` (a list of
`name=value` in name order, text in quotes), `time`, `maxTriangles`, and `defaultColor`. For an explicit view the
`view`, `azimuth`, and `elevation` are those that were asked for, so they are unchanged from earlier releases; for a
view the library chose they report what it used (`ScadResolvedView`): `iso`, 45, and about 35.26 with
`orthographic` for a 3D model, `top`, 0, and 90 for a flat model, and `custom` with the angles for a view that
the model set itself (the name of a view is the viewpoint of `ScadViews` whose angles match, otherwise `custom`).

A *Vega-Lite chart* load (`VegaLitePageSource`) opens the chart once and draws it with
`VegaLiteDocument.Render(width, height, options)`. The target size is `ComputeTargetSize` of the natural size from the
probe,
so a scale multiplies it, one side keeps the aspect ratio, and two sides are exact; when the target has another aspect
ratio than the chart, the library scales the chart uniformly to fit and centers it over the background, so nothing is
stretched. The `VegaLiteRenderOptions` are fixed except for what the tool options say, so that the picture does not
depend
on the machine: `TimeZone` is UTC, `Fonts` use only the bundled fonts (`UseSystemFonts` is false), `Background` is the
background option (`null` when none was given, so the chart keeps its own background, white by default; zero alpha is a
transparent picture), `ThrowOnUnsupportedFeature` is `Strict`, and the handlers collect the notes. A warning becomes a
note (`vega-lite`, `warning`, `vega-lite-warning`, the library's `file:line:column /pointer: vl-id: message`); an
unsupported
feature becomes a note (`vega-lite`, `unsupported`, the feature identifier such as `vl-mark-geoshape` as the code, and
`file:line:column /pointer: message`). The only option of the chart is `SourceOptions.VegaLite` (`ChartOptions`, see
*Chart parameters* below): no datasets,
configuration, or data folders are passed, and there is **no data-folder option** (the shared option grammar of the
render, inspect, compare, and montage commands has no place for one, and the library's `DataPaths` stay unused). A
chart's local data files (CSV, TSV, DSV, or JSON named by a relative `url`) are therefore read by the library through
its own sandbox from the folder of the specification and below it only - the base directory that `Open(path)` sets,
never
the current directory - and no link below that folder is followed; a file that is missing, outside the folder, or not
valid is a `VegaLiteRenderException` (`vega-lite-error`, in strict mode too), and a remote URL is only a diagnostic that
is never fetched (an `unsupported-feature` error with the strict switch). The
picture is the same for the same chart and options: the library's render is deterministic. The operation recorded
is `render` with the `width`, the `height`, the `timeZone` (`UTC`), and, when it was given, the `background`.

*Chart parameters.* `SourceOptions.VegaLite` (`ChartOptions`: `Parameters`, a read-only dictionary of null, boolean,
number,
string, list and dictionary values, and `WarnOnUnknownParameters`) is the option behind `--param` of the tool, the
`param.`
fragment of a link and `ViewerRenderOptions.VegaLite`. `VegaLitePageSource.SelectParameters` compares the names with the
document's `ParameterNames`: a name the chart does not declare is, by default, a `ViewerException` with the code
`vega-lite-error`
that names the parameter and lists the declared names (a mistake of a command line must be loud), and with
`WarnOnUnknownParameters` (what an embedding link sets) a note (`vega-lite`, `warning`, code `vl-param-unknown`) and the
value is
dropped. When there are parameters the page source **measures the chart again with them** (a parameter can change the
layout,
for example a filter that changes the number of bands, and the probe measured the defaults) and works out the target
size from
that natural size, and the `SourceInfo` of the result carries it, so the natural size reported by the tool is that of
the
parameterized chart; the probe itself is unchanged. The values go to `VegaLiteRenderOptions.Parameters` for both the
measure and
the render. The `render` operation lists them in a `params` field (name order, `name=value` with the value as JSON, so a
selection reads `brush={"x":[2,6]}`). The tests are in `CanvasNetViewerParameterFragmentTests.cs`.

*Chart pictures* (phase 13). The `image` marks of a chart show files that lie beside it, through the same wiring as the
pictures of an SVG file: unless the caller supplied the resolver of an embedding document
(`LoadContext.UseScopedResolver`,
then that one is used and it, not the result, collects what the nested pictures report), the loader creates
`SvgImages(path, options)` (the `CanvasNetViewer.CreateImageResolver` of the folder of the chart, with
`ThrowOnUnsupportedFeature` set from `Strict`) and gives it to the library as `VegaLiteRenderOptions.ImageResolver`. The
resolver is **only** given to `Render`; the probe measures the chart without one, so `info` and the natural size never
read
a picture. A `url` may therefore name a raster, an SVG file, a model, a page of a document (`report.pdf#page=2`), or
another chart, always inside the folder of the chart: a remote URL, an absolute path, a link through a symbolic link,
and
a path that leaves the folder are the resolver's warning `svg-image-unresolved` and draw nothing. After the render the
notes of the resolver become the notes of the result, in the order they were reported, after the library's; the
library's
own warning `vl-mark-image-unresolved` for a link that the resolver reported itself (`DropDuplicateUnresolved`: the
resolver's message names the reason, the library's only says that nothing came back) is left out, and one for a link the
resolver did not report stays. The files that were drawn are `embed` operations (`href` as written, `format`, `width`,
`height`) after the `render` operation, and the printed text of a model is the `Echo` of the result. With the strict
option
the first link that was not drawn is an `unsupported-feature` error that names the chart and the link
(`SvgImages.ThrowForUnresolved`: `svg-image-unresolved`, `svg-image-too-large`, `image-fragment-invalid`,
`image-page-out-of-range`; an unknown or repeated fragment parameter stays a warning), exactly as for an SVG file. A
file
that is found but cannot be loaded is the error of its own failure naming both files (`SvgImages.Map`), and a chart that
asks for more pictures than `VegaLiteLimits.MaxImages` (256) is `budget-exceeded` (the library's message names the
limit).
The tests are in `SourceLoaderVegaLiteImageTests.cs`.

*Include and use.* The library resolves the `include` and `use` directives of a model: a relative path is looked up
from the folder of the file that names it, nothing outside the folder of the model (the base directory, which is the
folder of the file that was opened) is ever read, a link below that folder is not followed, and no library path or
file resolver is configured, so the folder of the model and below it are the only places a model can read. A path
that leaves the folder, an include cycle, nesting that is too deep, and a syntax error in an included file are errors
of the library. A directive whose file does not exist is only a *warning* in the library (as in OpenSCAD itself);
the loader treats that warning as an error, because a picture drawn without a file the model asked for would be
wrong without saying so: after the render the picture is discarded and a `scad-error` ("SCAD include not found: ...")
with a hint about where files are looked up is thrown. The loader recognizes that warning by its code
(`ScadDiagnosticCodes.IncludeNotFound`, `scad-include-not-found`), not by its text, which a test pins.

Each step that changed the pixels is recorded in `Operations` (`auto-orient` with the orientation,
`resize` with the size, `render` with the size - and for a document the page, the dots per inch, and the
background - `fit` with the box, `flatten` with the background as `#RRGGBB`). Surfaces replaced along the way are
disposed, including when a step throws.

**Input fragments** (`InputReference.cs`, `InputSelection.cs`; the types are public). An input path of the command line
may end in a URL fragment, the syntax of a link of an SVG image (`assembly.scad#def.explode=0.5`, `report.pdf#page=3`).
`InputReference.Parse(text)` splits it: a text without a `#`, or one that names a file as written (`File.Exists`), is the
path (so a real file `chart#2.png` keeps working and wins over `chart` with the fragment `2.png`); otherwise the path is
the longest start of the text, ending before a `#`, that names an existing file (the last such `#` is tried first, so a
`#` in a folder name or in the file name is not a problem, and the fragment may itself hold a `#`), and when no start
names a file the first `#` splits, so that the missing file reported is the one before it. `Probe(InputReference)` is
`Probe(path)` plus, when there is a fragment, `InputSelection.Create`, which parses the fragment with the core
`ImageFragment` (over its limits: `image-fragment-invalid`) and reads it with the **built-in image source of the kind**
of the file (`CanvasNetViewer.FindBuiltIn(kind)`, the `LoaderImageSource` that draws a link of that kind): its
`ReadPage` (`page`, and `slide` for a presentation), `ReadScad` (`view`, and the `def.` variables with
`ImageParameterValues.ReadVariables`, vectors included, checked by constructing `ScadEvaluationOptions`) and `ReadChart`
(the `param.` parameters), so a link and an input accept exactly the same parameters with the same values; a value that
cannot be used is a `ViewerException` with the code `image-fragment-invalid` (`ErrorCodes.ImageFragmentInvalid`) whose
message names the input, and a page the file does not have is `page-out-of-range` (`CheckPage`, at probe time). The
notes are the `ReviewParameters` notes of the core `ImageFragment` (`image-fragment-unknown-parameter`,
`image-fragment-duplicate-parameter`; the same code that reports them for a link) and, for a chart, one
`vl-param-unknown` warning for each `param.` name the chart does not declare (the names are compared with
`SourceInfo.Chart.Parameters`; the unknown names are dropped from the selection, so the loader never sees them and the
`--param` error of an unknown name stays an error); they are warnings of the source `input`, never errors, not even in
strict mode. The result is `SourceInfo.Selection` (an `InputSelection`: `Fragment`, `Page`, `Scad`, `VegaLite`,
`Diagnostics`) and `SourceInfo.Specifier` (the path, `#` and the fragment). **`InputSelection.ApplyTo(options)`** lays it
over the options of a command: the page replaces the page, the view of the fragment (only when it names one) replaces the
view while the azimuth, elevation and projection of the command still refine it, the variables and the chart parameters
are merged by name with the fragment winning, and applying it twice gives the same options. `Load(info, options)`,
`OpenPages`, and so every operation, apply it (`ApplySelection`), `PageSource.ReadPage` adds the `Diagnostics` to the
notes of the first page that is loaded from the source (once, not for every page), and the operations take the page
from it (see the units of the operations). Embedded links never go through this path: `LoaderImageSource` is given the
link's own fragment by the resolver. The tests are in `InputSelectionTests.cs`.

**Viewport variables.** The camera of a model is `$vpt` (the target, a vector), `$vpr` (the rotation, a vector), `$vpd`
(the distance) and `$vpf` (the field of view). `ScadEvaluationOptions.Variables` overrides them (an explicit view or
view angles still replace `$vpr`), so `-D '$vpd=300' -D '$vpt=[0,0,55]'` and `def.$vpd=300&def.$vpt=[0,0,55]` fix the
camera; for an orthographic picture the scale is `size / (2 * $vpd * tan($vpf / 2))`, so one distance gives every
picture of the model one scale. Without them the camera is fitted to the geometry of each picture, and the scale
differs between two versions of a model. `FormatDefine` lists a vector as `[a, b, c]` in the `defines` of the `render`
operation.

**ComputeTargetSize(path, naturalWidth, naturalHeight, options)**: a scale multiplies both sides; a width and a
height are exact; a width alone derives the height from the aspect ratio, and a height alone derives the width;
with none, the natural size. The result is rounded to the nearest pixel (half away from zero) and is at least 1.
A side larger than `Surface.MaxDimension` throws `size-limit`, with a message that says whether the natural size
or the requested size is too large and a hint on how to choose a smaller one.

**Read(path, kind, action)**: runs a read and maps exceptions (see *Error Handling*).

**The EXIF orientation**: `JpegCodec.ReadExifOrientation(path)` (core; it finds the orientation without throwing for
malformed data, see *JpegCodec Unit Design*, `codecs/jpeg-codec.md`) runs inside `Read`, so a file that cannot be
opened is mapped like any other read failure, and `Surface.ApplyExifOrientation(orientation)` (core;
*SurfaceTransforms Unit Design*) turns the decoded surface upright, an exact move of the pixels that swaps the width and
height for 5 to 8.

**Surface.FlattenOver(background)** (core) composites a surface over a solid color into a new surface, and
**Surface.HasTransparency()** (core) says whether any pixel is not opaque; the loader flattens with the first when a
background was given, and `ImageWriter` uses both for JPEG output.

**SourceKindExtensions.IsVector(kind)** is true for SVG, PDF, PPTX, VSDX, SCAD, and Vega-Lite, the kinds that are
rendered at the
size they are asked for. **SourceLoader.BoxOptions(info, options, width, height)** gives the options that load a
source into a box: for a vector kind a copy of the options with `Width` and `Height` set to the box and `Scale`
cleared, so the source is rendered sharply at the box (a document page and a chart are fitted inside it and centered
over the
background, an SVG follows its own `preserveAspectRatio`, and a model fills it); for a raster kind the options
unchanged, because the caller fits the decoded pixels with the core `Surface.FitInto`. The loader keeps its own
private center helper for documents (a page rendered smaller than the box is centered over the background); the fitted
size comes from the core `Surface.FitInside` (the smaller of the two box ratios scales both sides, so a small image is
enlarged, and each side is rounded half away from zero and limited to 1 and the box; the algorithm is documented in
*SurfaceTransforms Unit Design*, `canvas/surface-transforms.md`).

**SourceInput and LoadContext** (`SourceLoader.Input.cs`): the loader reads a file through a `SourceInput`, not a path.
`SourceInput.FromFile(path)` opens a new `FileStream` for every use, and `SourceInput.FromMemory(bytes, name,
directory)` serves bytes; both expose `Name` (the path as given, used in every message), `Directory` (the folder that
the
files a format names itself are looked up in), `Length`, and `Open()`. Every `ProbeX` and page source reads through it
and
calls the library by its **stream** overload with `BaseDirectory = input.Directory` and
`SourceName = input.Name`, which is
what the path overloads of the libraries do, so a file loaded from disk and the same bytes loaded from memory give the
same
kind, size, pixels, and notes. The public `Probe(path)`, `Load`, and `OpenPages` use `FromFile` and the default
`LoadContext`, so the command line behaves as before (it probes once for `info` and opens once for `render`, as
before). The internal entry points `ProbeInput(input, kind)`, `OpenInput(input, kind)` and
`OpenInputPages(input, info, options, context, opened)` serve `LoaderImageSource` of the `CanvasNetViewer` unit:
`OpenInput` returns an `OpenedSource` that keeps the parsed PDF, presentation or drawing open after reading its pages,
and `OpenInputPages` takes the document from it (`TakeDocument`, which transfers ownership to the page source), so a
render parses a document once. A per-thread counter (`DocumentParseCount`) counts the parses, for the tests.
`LoadContext` carries the hooks of a render that embeds the file: a `CancellationToken` (checked before each load, and
passed to the model and chart renderers), `Charge(width, height)` (called once for every surface before it is
allocated, so the pixel budget of the render is charged first - a raster from its declared size, before the stored-size
check, so a huge picture stays a `svg-image-too-large` warning and not a `size-limit` error), `ScopedResolver` (used
instead of building an `SvgImages` resolver for the nested pictures of an SVG file, so the depth, cycle, and budget
limits of the embedding render apply and the resolver's notes are not duplicated), and `ReturnFittedPage` (a document
page
comes back at its fitted size without the margin, as a linked page is drawn). The default context has no hook.

**Option validation** (`FindOptionProblem`): `Load(path, options)`, `Load(info, options)`, and `OpenPages` check the
`SourceOptions` first, before any file is touched, with the ranges of the command-line readers: `Width` and `Height`
at least 1 (above `Surface.MaxDimension` stays `size-limit` when the file is loaded), `Scale` finite and from 0.001 to
8192, `Dpi` finite, above 0 and at most `MaxDpi` (2400, the constant the tool's `ArgumentReader` shares), a `Filter`
that
is a defined value, and for a model a known `View`, a finite `Azimuth`, a finite `Elevation` from -90 to 90, a finite
`Time` within 1e9, `MaxTriangles` at least 1, and `Defines` that the model library accepts, and for a chart `VegaLite`
parameters that
`VegaLiteRenderOptions.Parameters` accepts (an empty name, a value nested too deeply or holding too many values;
`FindChartProblem`).
A violation is a
`ViewerException` with the code `usage-error`; `CanvasNetViewer.Render` calls the same validator and reports the problem
as
an `ArgumentOutOfRangeException`. A page below 1 or beyond the last keeps its `page-out-of-range` from `CheckPage`,
and the
whole-number rule of the Visio `Dpi` stays in `ValidateDpi` because it depends on the kind of file.

**Diagnostic detail** (`ImageDiagnostic.Detail` and `.Location`): the generic `Code` of a note is unchanged
(`scad-warning`,
`vega-lite-warning`, the feature of an unsupported construct). `Detail` is the finer identifier of the library: for a
model
warning the `ScadDiagnostic.Code` when it differs from `scad-warning` (`scad-anchor-invalid`), for a chart warning the
documented `vl-*` prefix of the message. `Location` is `(File, Line, Column)` of a model (left out when the line is 0)
and `(File, Pointer, Line, Column)` of a chart when the position is known; a note about a page of a document has the
`Page` and no location. For the notes of embedded files `SvgImages` copies `Detail`, `Location`, `Page`, and `Origin`
unchanged. `SvgImages.ThrowForUnresolved` also treats `image-fragment-invalid` and `image-page-out-of-range`
(besides `svg-image-unresolved` and `svg-image-too-large`) as an image that was not drawn, so `Strict` fails with
`unsupported-feature`; the warnings `image-fragment-unknown-parameter` and `image-fragment-duplicate-parameter` stay
warnings.

#### Error Handling

`Read` maps these library exceptions (an explicit list, not a catch-all) to a `ViewerException` with the file path in
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
to simplify the model. A model longer than the library reads is an `InvalidDataException`, so `corrupt-input`. The
Vega-Lite library adds
three, caught before the generic list (its unsupported-feature exception is an `IOException` and its specification
exception a `FormatException`): `VegaLiteUnsupportedFeatureException` (thrown in strict mode) to `unsupported-feature`
naming the feature and the position; `VegaLiteSpecException` (malformed JSON, a property of the wrong type, plain Vega,
nesting deeper than the parser limit, a string longer than the string limit, an invalid axis or format) to
`vega-lite-error`
("Vega-Lite specification error: file:line:column /pointer: ..."); and `VegaLiteRenderException` (an
`InvalidOperationException`: a local data file that is missing, outside the folder of the specification, a link, not
valid
UTF-8, or not valid JSON or delimited text, an undefined dataset name, a data limit, or a chart that cannot be laid
out) to
`budget-exceeded` when it
reports a limit and otherwise to `vega-lite-error` ("Vega-Lite render error: ..."). The limits of the specification
parser are deliberately `vega-lite-error`, as the nesting limit of the SCAD parser is a `scad-error`: only the limits
applied while the chart is laid out or drawn (data rows, data files, data bytes, ticks, scene items, and so on) are
`budget-exceeded`. The
library names every limit in the message, either as "the limit of N", as "the limit (MaxViews)", or with the
name of the limit ("VegaLiteLimits.MaxSceneItems"), which `MapVegaLiteRender` recognizes; the library gives no other way
to tell a limit from another failure, so a test pins the texts by provoking real limit failures with small limits.
A chart longer than the library reads is an `InvalidDataException`, so `corrupt-input`, as for a model. An
`ArgumentException` of the library is not on the allow-list of `Read`, because the library throws it for arguments
that the caller chose; the two places that call the library with arguments the tool chose handle it themselves:
`RenderChart` maps any `ArgumentException` of a render (the target size was validated by `ComputeTargetSize` before)
to `vega-lite-error` ("Vega-Lite render error: ..."), and `MeasureChart` maps an `ArgumentOutOfRangeException` for
the parameter `options` (the library's check that the laid-out size is between 1 and 8192 pixels on a side) to
`size-limit` with a hint and every other `ArgumentException` to `vega-lite-error` ("Vega-Lite measure error: ...").
Neither is ever reported as a usage error or as an unexpected exception. Every
other exception - a null reference, an index out of range, an out-of-memory failure - is a defect and propagates to
the entry point, which reports it as an unexpected error and re-throws it. The tests feed every supported format
truncated and garbage files to prove that the allow-list covers the failures the libraries actually raise (for the
documents also every byte-prefix length of a sample, overwritten and zeroed middles, a valid archive with a broken
part, malformed PDF content, and an unsupported PDF encryption handler; for a chart a cut after every kind of damage
of the shared sweep (the garbage and truncated files of a chart are
`vega-lite-error`, and a cut that leaves valid JSON may still load); for models a cut after every seventh
character of a model with several kinds of statement, garbage of several sizes, and some forty stress models - huge
numbers, degenerate or empty shapes, unsupported constructs, runaway recursion and loops, bad arguments, a deep nest of
parentheses, and unusual text); a gap found that way is closed by extending the list, and none was found for any
format. Size problems are `size-limit`; a page that does not exist is `page-out-of-range`. A source kind that the
loader has no case for - which cannot happen while every kind of `SourceKind` is handled - is an
`InvalidOperationException`, a defect, not a `ViewerException`. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface`, `Rgba32`, the PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 codecs, and
  `ImageFormats.Detect` (the shared signature recognizer behind `DetectSignature`).
- **CanvasNet SVG system** - `SvgCodec` renders SVG and reports its intrinsic size; `SvgLoadOptions.ImageResolver` is
  the hook through which an SVG file embeds other files.
- **CanvasNet Viewer system** - `CanvasNetViewer.CreateImageResolver` creates the image resolver of an SVG render, and
  `CanvasNetViewer.Extensions` is the list of the types an `image` may link. The core `ImageSources` subsystem supplies
  the resolver, its diagnostics, the embedded images, and `ImageSourceException`.
- **CanvasNet PDF, PPTX, and VSDX systems** - `PdfDocument`, `PptxDocument`, and `VsdxDocument` list and render the
  pages of a document, and report the features they cannot draw.
- **CanvasNet SCAD and Render3D systems** - `ScadDocument` parses and draws a model, and
  `ScadRenderOptions.ResolvedViewHandler` with `ScadResolvedView` tells the loader which view it used; `Camera`,
  `CameraProjection`, and
  `SceneRenderOptions` give the camera convention, the projection, and the background and default colors.
- **CanvasNet Vega-Lite system** - `VegaLiteDocument` parses, measures, and draws a chart, with the render options,
  the diagnostics, and the exceptions that the loader maps.

#### Callers

- **ImageRenderer**, **ImageInspector**, **ImageComparer**, and **MontageBuilder** - probe (or take the probed facts),
  load, and report the operations.
- **DiscoveryCommands** (of the tool) - the `info` command probes.
- **Library users** - call `Probe`, `Load`, and `OpenPages` directly.
- **ImageWriter** - uses the core `Surface.FlattenOver` and `Surface.HasTransparency` for JPEG output.
- **ContactSheet** and the compare operation - use `BoxOptions` and the core `Surface.FitInto` to load images into
  one size.
