## Imaging

![Imaging Structure](ImagingView.svg)

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif -->

### Overview

The `Imaging` subsystem (namespace `DemaConsulting.CanvasNet.Tool.Imaging`, folder
`src/DemaConsulting.CanvasNet.Tool/Imaging/`) turns files into pixels and pixels into files. It is free of
command-line concerns: it knows nothing of `Context`, `ArgumentReader`, or the commands, and the dependency runs
one way (`Commands` use `Imaging`). It is a subsystem rather than a single unit because later releases add units
for image transformation, comparison, and montage behind the same pixel interfaces. As of this release it
contains two units: `SourceLoader`, which recognizes a file, reads its header, and loads it (PNG, JPEG, BMP,
TIFF, GIF, JPEG 2000, and SVG), and `ImageWriter`, which writes PNG, JPEG, BMP, and TIFF files. The supporting
types shared by the units (`SourceKind`, `OutputFormat`, `SourceInfo`, `SourceOptions`, `LoadedSource`,
`AppliedOperation`, `ImageDiagnostic`, `SourcePage`, and the pixel helpers `SurfaceOperations`) are documented in
the design of the unit that owns them.

### Interfaces

**SourceLoader.Detect(path)**, **SourceLoader.Probe(path)**, **SourceLoader.Load(path, options)** and
**SourceLoader.Load(info, options)** (provided to the commands): recognize a file, read its header facts, and load
it into a `LoadedSource` that owns its `Surface` and says what was done to produce it.

**ImageWriter.ResolveFormat**, **ImageWriter.CheckTarget**, and **ImageWriter.Write** (provided to the
commands): choose the output format, check an output before any work is done, and write a surface atomically.

**Surface and the codecs** (consumed, from the CanvasNet core library): `Surface` holds the pixels; `PngCodec`,
`JpegCodec`, `BmpCodec`, `TiffCodec`, `GifCodec`, and `Jpeg2000Codec` decode and encode; `SvgCodec` (from the
CanvasNet SVG system) renders SVG.

### Design

A load is: detect the format (signature bytes, then SVG text, then extension); read the header (`Probe`); refuse an
image that is too large; compute the target size from the options; decode or render; apply what the options ask for
(EXIF orientation, resize, flatten over a background); and return the pixels with the list of operations that were
applied, which commands report as the `operations` of a JSON result. A vector source (SVG) is rendered directly at
the target size, which is sharper than rendering at the natural size and resampling; a raster source is decoded at
its own size and resized afterwards. A write is: resolve the format, check the target, flatten transparency for
JPEG, encode into a temporary file in the target directory, and move it over the target.

The subsystem owns the policy for the two things that must be decided in one place so that every command agrees:
the size policy (including the maximum of `Surface.MaxDimension` pixels per side, enforced before decoding
wherever a header gives the size) and the mapping of library exceptions to expected errors. The mapping is an
explicit list of exception types in `SourceLoader.Read`; any other exception is a defect and propagates to the
entry point, which reports and re-throws it. `Imaging` adds no dependency on a system other than the CanvasNet
core library and, for SVG, the CanvasNet SVG system. The other source kinds (`pdf`, `pptx`, `vsdx`, `scad`) are
recognized by `Detect` and reported as not readable yet by `Probe` and `Load`; each is added by a new partial file
of `SourceLoader` and one new case in the two dispatch methods.
