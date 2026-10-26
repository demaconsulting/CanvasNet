### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif pptx vsdx scad letterboxed -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`SourceLoader` turns a path into facts (`Probe`) or into pixels (`Load`) for the files the tool reads. Its single
responsibility is to know how to read each source format: it recognizes the format, reads the header, applies the
size policy and the options that belong to reading (orientation, background), and reports a bad file as an
expected error. The unit consists of the static class `SourceLoader` (partial files `SourceLoader.cs`,
`SourceLoader.Detect.cs`, `SourceLoader.Raster.cs`, and `SourceLoader.Svg.cs`), `ExifOrientation`, the pixel helpers
`SurfaceOperations`, and the supporting types in `SourceModels.cs`.

#### Data Model

**SourceKind** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`, `Gif`, `Jpeg2000`, `Svg`, `Pdf`, `Pptx`, `Vsdx`, `Scad`, in the
order the `formats` command lists them; `ToFormatName` gives the lower-case member name used in output and JSON.
**OutputFormat** (enum): `Png`, `Jpeg`, `Bmp`, `Tiff`.

**SourceInfo** (record): `Path`, `Format`, `Bytes`, `Width`, `Height` (stored size), `Channels`, `HasAlpha`,
`CanDecode`, `FrameCount`, `PageCount` (1 for a single image), `ExifOrientation` (JPEG only), and the lists `Pages`,
`Modules`, and `Functions` (empty until the multi-page and model kinds are read).

**SourceOptions** (record, all properties `init`): `Scale`, `Width`, `Height` (the size options), `Background`
(a nullable `Rgba32`), and `AutoOrient` (default `true`). Properties for further source kinds are added here.

**LoadedSource** (record, `IDisposable`): the `Surface` (disposed with the record), the `Info`, the 1-based `Page`,
`IsVector`, `Diagnostics` (`ImageDiagnostic` records), `Echo` (text printed by a model), and `Operations`
(`AppliedOperation` records: an operation name and its parameters in order).

**ExtensionKinds**: the table of file name extensions (lower case, with the dot) and their kinds; the `formats`
command lists the same extensions and a test keeps the two consistent.

#### Key Methods

**Detect(path)**: throws `input-unreadable` for a directory and `input-not-found` for a missing file. Reads the
first 4096 bytes and decides in this order: the signature bytes of PNG, JPEG, GIF, TIFF (either byte order),
JPEG 2000 (a JP2 signature box or a raw codestream), PDF, and BMP (the `BM` letters alone would also match text, so
the reserved fields and the size of the bitmap header that follows are checked too); a ZIP signature with the
extension `.pptx` or `.vsdx`; SVG text (a header without NUL bytes that contains `<svg`); and finally the extension.
The content wins over the extension, but a file whose content says nothing keeps its extension's kind, so that an
empty or truncated `.png` is reported as corrupt by the load instead of as an unknown format. Otherwise throws
`unsupported-format` with a hint to run `canvasnet formats`.

**Probe(path)**: `Detect`, then reads the header with the codec's `GetInfo` (and, for a JPEG, the EXIF orientation
with `ExifOrientation.Read`) or, for an SVG, the intrinsic size with `SvgCodec.GetInfo`. No pixels are decoded
(a GIF's `GetInfo` does decode the first frame's data, as the library documents). A kind that is recognized but
not readable yet throws `unsupported-format` ("cannot read yet").

**Load(path, options)** and **Load(info, options)**: dispatch on `info.Format`. A *raster* load (`LoadRaster`)
refuses an image whose stored size exceeds the maximum, applies the EXIF orientation when `AutoOrient` is set and
the orientation is 2 to 8, computes the target size from the *oriented* size, checks it against the maximum, all
before decoding; then decodes, orients (`ExifOrientation.Apply`), resizes when the size differs, and flattens over
the background when one was given with some opacity. An *SVG* load (`LoadSvg`) computes the target size from the
intrinsic size, checks it, renders with `SvgCodec.Load(path, width, height)`, and flattens over the background
(white when none was given; none when the background has zero alpha). An SVG rendered at a size with another aspect
ratio than the drawing is fitted by the SVG codec's rule (preserve the aspect ratio, centered), so the drawing is
letterboxed. Each step that changed the pixels is recorded in `Operations` (`auto-orient` with the orientation,
`resize` or `render` with the size, `flatten` with the background as `#RRGGBB`). Surfaces replaced along the way are
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
`UnsupportedImageFeatureException` (an `IOException`, caught first) to `unsupported-feature`; and
`InvalidDataException`, `XmlException`, `EndOfStreamException`, and `FormatException` to `corrupt-input`. Every
other exception - a null reference, an index out of range, an out-of-memory failure - is a defect and propagates to
the entry point, which reports it as an unexpected error and re-throws it. The tests feed every supported format
truncated and garbage files to prove that the allow-list covers the failures the codecs actually raise; a gap
found that way is closed by extending the list. Size problems are `size-limit`; a recognized but unreadable kind is
`unsupported-format`. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **ToolException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface`, `Rgba32`, and the PNG, JPEG, BMP, TIFF, GIF, and JPEG 2000 codecs.
- **CanvasNet SVG system** - `SvgCodec` renders SVG and reports its intrinsic size.

#### Callers

- **RenderCommand** - probes, then loads, and reports the operations.
- **DiscoveryCommands** - the `info` command probes.
- **ImageWriter** - uses `SurfaceOperations` (flatten) for JPEG output.
