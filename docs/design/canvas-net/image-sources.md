## ImageSources

![ImageSources Structure](ImageSourcesView.svg)

The `ImageSources` subsystem is the ninth software subsystem in CanvasNet. It lets a document that links to other
pictures (an SVG `<image href="...">`, for example) draw 3D models, charts, other vector files, document pages, and
rasters without the program that draws the document knowing any of those formats. It consists of three units:
`ImageSourceContract` (the public types that anyone can implement or use to plug a format or a live source in),
`FolderImageResolver` (the resolver that confines links to a folder, picks the source, and bounds the work), and
`RasterImageSource` (the source for the raster formats the core library decodes).

### Overview

Before this subsystem existed, the logic that turned an SVG link into a picture lived in the command-line tool, so a
library user who wanted the same feature had to write it again, and every format package would have had to know every
other. The subsystem moves the format-independent part into the core package once, and leaves the formats to small
sources that ship in the package of each format (`SvgImageSource`, `ScadImageSource`, `VegaLiteImageSource`,
`PdfImageSource`, `PptxImageSource`, `VsdxImageSource`), which reference only the core package. The
`DemaConsulting.CanvasNet.Viewer` package lists them all.

The dependency direction is one-way: the renderer of a document (`SvgCodec`) depends on the `IImageResolver`
contract; `FolderImageResolver` depends on `IImageSource`; every source depends on the contract; nothing in the core
package depends on a format package. There is no global registry and no discovery: a source is used only when the
caller passes it in.

### Interfaces

The public types of the subsystem are in the namespace `DemaConsulting.CanvasNet.ImageSources`:

- **`IImageResolver`** - `Surface? Resolve(string href, int pixelWidth, int pixelHeight)`: what a renderer calls to turn
  a
  link into a picture. It returns a surface the caller owns, or `null` (nothing is drawn).
- **`IImageSource`** - one format or live source: `Name`, `Extensions` (file-based claims, such as `.png` or
  `.vl.json`),
  `Schemes` (scheme claims, such as `camera`), and `Render(ImageSourceRequest)`.
- **`ImageSourceRequest`, `ImageSourceResult`, `ImageSourceDiagnostic`, `ImageDiagnosticSeverity`, `EmbeddedImage`,
  `ImageSourceException`, `ImageSourceBudgetException`** - the data that flows through the contract.
- **`ImageSourceSet`** - an ordered, immutable list of sources that validates claims and applies the precedence rule.
- **`FolderImageResolver`, `FolderImageResolverOptions`** - the resolver; `Resolve` is the tolerant embed path and
  `Render(path)` and `Render(Stream, fileName)` draw a top-level file and fail loudly.
- **`RasterImageSource`** - the raster source.

The subsystem consumes `SandboxedFileResolver` of the `Files` subsystem (to confine and read files), `ImageDecoder`,
`JpegCodec`, and `Surface` of the `Codecs` and `Canvas` subsystems (the raster source), and nothing else.

### Design

**Claims and precedence.** A source claims file name extensions (resolved through the folder sandbox; the source
receives
the bytes), URI schemes (the link is handed over as written; no sandbox, no file; the source owns access and security),
or both. `ImageSourceSet` validates every claim when it is built and records the winner of each extension and scheme:
**the later source of the sequence wins**, so sources an application lists after the built-in ones override them. The
reserved schemes `file` (always a path inside the folder, handled by the resolver) and `data` (decoded by the SVG codec)
cannot be claimed, and a single letter followed by a colon is a Windows drive, never a scheme.

**One resolver, one render.** A `FolderImageResolver` owns the sandbox, the count of images, and the lists of notes,
printed text, and embedded images of one top-level render, and it is not thread-safe; a new resolver is created for
every render (it is cheap). It never caches a drawn picture: every call reaches the source again, so a live source (a
video frame) is fresh each time.

**Scoped resolvers.** When a source draws a file, the request carries a resolver scoped to that file: it shares the
counts and lists of the render, resolves relative links against the folder of the file, and carries the chain of files
for the cycle check and the depth. A source that draws a document with links of its own (the SVG source) just passes
that resolver on, so recursion is bounded without any state in the sources.

**Safety and failure.** A link that cannot be resolved is a warning and draws nothing; a file that is found but cannot
be
drawn throws `ImageSourceException`; cancellation is never wrapped. A link is read as a URI reference: the query and
the fragment are cut off before percent-decoding, so neither can change which file is read. See the unit designs for
the details.

**One pixel budget for the whole render.** A small placement can ask for a huge picture (a raster is decoded at its own
size), so `FolderImageResolverOptions.MaxDecodedPixels` limits the pixels every source of one resolver may allocate in
total, at every level of nesting. The resolver clamps and charges the requested size before it calls a source
(`ImageSourceRequest.ReservedPixels`), tells the source what is left after that charge in
`ImageSourceRequest.RemainingPixels`, and charges the larger of the requested and the returned size afterwards.
`FolderImageResolver.RemainingPixels` is the part of the budget not charged yet. A source checks the size it will
allocate **before** allocating
(`ImageSourceRequest.EnsureWithinBudget`; the raster source from the file header, every vector source from the size it
draws at) and refuses with `ImageSourceBudgetException`; the resolver reports the refusal as the warning
`svg-image-too-large` and draws nothing. A source that does not check is still charged afterwards, and its oversize
surface is discarded.

### Dependencies

The subsystem depends on the `Files` subsystem (`SandboxedFileResolver`, `SandboxOptions`, `SandboxedFile`,
`SandboxException`) for every file access, on the `Canvas` subsystem (`Surface`, `Rgba32`) for pictures, and on the
`Codecs` subsystem (`ImageDecoder`, `ImageFormats`, `JpegCodec`) for the raster source. No other CanvasNet subsystem
depends on it. Its consumers are the format packages (each supplies a source), the `CanvasNetSvg` system (its
`SvgLoadOptions.ImageResolver` takes an `IImageResolver`), the `CanvasNetViewer` system, and applications.

### Callers

`FolderImageResolver` is called by the SVG codec through `IImageResolver` and by applications directly (`Render`);
each source is called only by a resolver. `ImageSourceSet` is used by `FolderImageResolver` and by applications that
compose their own list.
