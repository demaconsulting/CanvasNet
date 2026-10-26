### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad letterboxed Visio EMU -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`SourceLoader` turns a path into facts (`Probe`) or into pixels (`Load`) for the files the tool reads. Its single
responsibility is to know how to read each source format: it recognizes the format, reads the header, applies the
size policy and the options that belong to reading (page, resolution, orientation, background, strictness), and
reports a bad file as an expected error. The unit consists of the static class `SourceLoader` (partial files
`SourceLoader.cs`, `SourceLoader.Detect.cs`, `SourceLoader.Raster.cs`, `SourceLoader.Svg.cs`,
`SourceLoader.Documents.cs`, `SourceLoader.Pdf.cs`, `SourceLoader.Pptx.cs`, and `SourceLoader.Vsdx.cs`),
`ExifOrientation`, the pixel helpers `SurfaceOperations`, and the supporting types in `SourceModels.cs`.

#### Data Model

**SourceKind** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`, `Gif`, `Jpeg2000`, `Svg`, `Pdf`, `Pptx`, `Vsdx`, `Scad`, in the
order the `formats` command lists them; `ToFormatName` gives the lower-case member name used in output and JSON.
**OutputFormat** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`.

**SourceInfo** (record): `Path`, `Format`, `Bytes`, `Width`, `Height` (stored size; for a document the size of page 1
at 96 dots per inch), `Channels`, `HasAlpha`, `CanDecode`, `FrameCount`, `PageCount` (1 for a single image),
`ExifOrientation` (JPEG only), and the lists `Pages` (the pages of a document; empty for a single image), `Modules`,
and `Functions` (empty until the model kind is read).

**SourceOptions** (record, all properties `init`): `Page` (1-based, default 1), `Dpi` (nullable, 96 when absent),
`Strict`, `Scale`, `Width`, `Height` (the size options), `Background` (a nullable `Rgba32`), and `AutoOrient`
(default `true`). Properties for further source kinds are added here.

**LoadedSource** (record, `IDisposable`): the `Surface` (disposed with the record), the `Info` (for a document page,
sized at the natural size of that page at the requested resolution), the 1-based `Page`, `IsVector`, `Diagnostics`
(`ImageDiagnostic` records: source, severity, code, message, and the page), `Echo` (text printed by a model), and
`Operations` (`AppliedOperation` records: an operation name and its parameters in order).

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
(a header without NUL bytes that contains `<svg`); and finally the extension.
The content wins over the extension, but a file whose content says nothing keeps its extension's kind, so that an
empty or truncated `.png` is reported as corrupt by the load instead of as an unknown format. Otherwise throws
`unsupported-format` with a hint to run `canvasnet formats`.

**Probe(path)**: `Detect`, then reads the header with the codec's `GetInfo` (and, for a JPEG, the EXIF orientation
with `ExifOrientation.Read`) or, for an SVG, the intrinsic size with `SvgCodec.GetInfo`. A PDF, PPTX, or VSDX
document is opened with its library and every page is listed in `Pages` (`SourcePage`: the 1-based number, the size
in points and in pixels at 96 dots per inch, the rotation of a PDF page, the name of a VSDX page); the page count
is `PageCount`, and `Width` and `Height` are those of page 1. A PDF page's size is the library's displayed
(rotation-applied) size in points; a slide or a page of a drawing is converted from EMU (12,700 EMU per point). A
document with no pages is `corrupt-input`. No pixels are decoded
(a GIF's `GetInfo` does decode the first frame's data, as the library documents). A kind that is recognized but
not readable yet (OpenSCAD) throws `unsupported-format` ("cannot read yet").

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
`InvalidDataException`, `XmlException`, `EndOfStreamException`, and `FormatException` to `corrupt-input`. Every
other exception - a null reference, an index out of range, an out-of-memory failure - is a defect and propagates to
the entry point, which reports it as an unexpected error and re-throws it. The tests feed every supported format
truncated and garbage files to prove that the allow-list covers the failures the libraries actually raise (for the
documents also every byte-prefix length of a sample, overwritten and zeroed middles, a valid archive with a broken
part, malformed PDF content, and an unsupported PDF encryption handler); a gap found that way is closed by
extending the list, and none was found for PDF, PPTX, or VSDX. Size problems are `size-limit`; a recognized but
unreadable kind is `unsupported-format`; a page that does not exist is `page-out-of-range`. Null arguments throw
`ArgumentNullException`.

#### Dependencies

- **ToolException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface`, `Rgba32`, and the PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 codecs.
- **CanvasNet SVG system** - `SvgCodec` renders SVG and reports its intrinsic size.
- **CanvasNet PDF, PPTX, and VSDX systems** - `PdfDocument`, `PptxDocument`, and `VsdxDocument` list and render the
  pages of a document, and report the features they cannot draw.

#### Callers

- **RenderCommand** - probes, then loads, and reports the operations.
- **DiscoveryCommands** - the `info` command probes.
- **ImageWriter** - uses `SurfaceOperations` (flatten) for JPEG output.
