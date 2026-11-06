## CanvasNetViewer Unit Design

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

<!-- cspell:ignore vsdx pptx scad href embedder -->

### Purpose

`CanvasNetViewer` is one of two units of the `CanvasNetViewer` system (the other is `ViewerException`, beside the
`Imaging` subsystem). It is a static class, with its two supporting types
`ViewerRenderOptions` and `ViewerImage`, that does three things: it lists the image sources of every format CanvasNet
can read, it creates the image resolver that lets an SVG document embed all of them, and it renders any file as a
picture. It adds no format knowledge of its own: each format is drawn by the `SourceLoader`, the same code the
command-line tool uses (which calls the libraries of the format packages), and every file
access is confined by the core `FolderImageResolver`. See _CanvasNetViewer System Design_ (`canvas-net-viewer.md`) for
the system-level architecture.

The unit's behavior is specified by the `CanvasNetViewer-CanvasNetViewer-*` requirements (`ImageSources`,
`CreateImageResolver`, `RenderPath`, `RenderStream`, `LoaderSources`, `RenderOptions`, and `ExtraSources`) in
`docs/reqstream/canvas-net-viewer/canvas-net-viewer.yaml`. The unit comprises `CanvasNetViewer.cs`,
`ViewerRenderOptions.cs`, `ViewerImage.cs`, and `LoaderImageSource.cs` (the adapter, with the internal
`DirectRenderOptions`).

### Data Model

- **`CanvasNetViewer`** — static; holds two read-only values created once: the built-in sources, seven
  `LoaderImageSource` adapters (raster, svg, pdf, pptx, vsdx, scad, and vega-lite, in that order, with the names,
  extensions and schemes of the format packages' sources; their extensions do not overlap), and the sorted union of their
  extensions,
  computed through an `ImageSourceSet` so that the claims are validated once. Both are exposed as read-only lists
  (`ImageSources`, `Extensions`). The sources are stateless and thread-safe, so the shared instances serve any number of
  resolvers.
- **`LoaderImageSource`** (internal sealed class, `IImageSource` and `IFragmentImageSource`) — an adapter over the
  `SourceLoader` of the `Imaging` subsystem, so that the tool, `Render` and a linked file are drawn by the same code.
  It is built from the claims of the package source it stands for, the `SourceKind`s it draws, the fragment parameters
  of its pages (`page` for PDF and Visio, `page` and `slide` for PowerPoint) and whether it accepts `view` (the model
  source only). It cannot be the package source itself: the format packages cannot call `SourceLoader`, which depends on
  all of them. The package sources stay public stand-alone providers with the same `page` support.
- **`DirectRenderOptions`** (internal sealed class, `IImageSourceOptions`) — the options of a direct render
  (the page, the resolution, the background, the model view options) and the output of the render (the page drawn, the
  number of pages, the anchors), handed through `FolderImageResolver.Render(..., options)`.
- **`ViewerRenderOptions`** (immutable record) — `Width` and `Height` (nullable; none = natural size, one = keep the
  aspect ratio, both = a box; when given, each must be from 1 to `Surface.MaxDimension`), `Page` (1-based, default 1),
  `Dpi` (nullable; none = 96), `Background` (nullable; none = no flattening), `Scad` (a `ScadViewOptions`, nullable),
  `Folder` (the sandbox for the
  files the rendered file itself includes or embeds; by
  default the folder of the file for a render by path, none for a stream), `ThrowOnUnsupportedFeature`,
  `MaxDecodedPixels` (nullable; none = the resolver default budget), `ExtraSources`,
  and `CancellationToken`. There is deliberately no `Scale`, `Filter` or `AutoOrient`: the size is `Width`/`Height` (or
  `Dpi`), and the resampling of a raster belongs to `ImageWriter` and `SourceLoader`.
- **`ViewerImage`** (`IDisposable`) — owns the `Surface` and carries `Format`, `Page`, `PageCount`, `Diagnostics`,
  `Echo`, `Anchors` and `Embedded`
  as read-only snapshots (`ReadOnlyCollection<T>` copies, not castable to a mutable list).
  Disposing it disposes the surface.

### Key Methods

**`LoaderImageSource.Render(request)`**: (1) take the options: a request with `DirectRenderOptions` uses them, a link
builds them from its fragment (`page` or `slide` through `GetPositiveInt32`, `view` for a model, the strictness switch,
a transparent background for a vector source, none for a chart, and for a vector source the placement as `Width` and
`Height`; an embedded file never inherits the options of its embedder); (2) wrap the bytes in a
`SourceLoader.SourceInput` named and based as the resolver found the file; (3) for a raster, refuse from its declared
size, before anything is decoded, a picture that cannot fit in the budget; (4) `OpenInput`, which parses a PDF,
presentation or drawing once and returns the facts about its pages together with the parsed document (any other kind
is described by `ProbeInput`), and a page below 1 or beyond the page count is an `ImageSourceRefusedException` with the
code `image-page-out-of-range`; (5) open the pages, handing the already parsed document to the page source (which then
owns it; the `OpenedSource` disposes it on every path where it was not handed over), with a
`LoadContext` that charges every surface to the request (`EnsureWithinBudget`), observes the cancellation token, draws a
nested SVG through the scoped resolver of the request (so the depth, cycle and budget limits of the render hold), and
returns a document page at its fitted size; (6) read the page and convert the notes of the loader to
`ImageSourceDiagnostic` (keeping `Detail`, `Location`, `Page` and `Origin`). A `ViewerException` that wraps a library
failure is rethrown as the original exception, so the documented `InnerException` of an `ImageSourceException` holds;
one without an inner exception (a size limit, a missing include of a model) is thrown as it is.

**`CreateImageResolver(folder, options)`** and **`CreateImageResolver(folder, extraSources, options)`**: validate the
arguments and return `new FolderImageResolver(folder, sources, options)`, where `sources` is the built-in list, followed
by the extra sources in the second overload. A new resolver is created on every call and nothing is shared between
calls, because a resolver counts the images and collects the notes of one render and never caches a picture. The
precedence rule is the `ImageSourceSet` rule: when several sources claim the same extension or scheme, the later one
wins, so an extra source overrides a built-in one. `Render` does not use this method: it builds its own resolver from
the built-in sources plus `ViewerRenderOptions.ExtraSources`, so `ExtraSources` is for a picture of one file made by
`Render`, and `CreateImageResolver` is for a render that the caller drives (for example `SvgCodec.Load` with
`SvgLoadOptions.ImageResolver`) and that needs the resolver and its notes.

**`Render(path, options)`**: validate the options (see _Error Handling_), resolve the sandbox folder (`options.Folder`,
or the folder of the file), refuse a file outside it with `ArgumentException`, express the file as a path relative to the
folder, and call
`FolderImageResolver.Render(relative, width, height, directOptions)` on a resolver built from the built-in and extra
sources, the
strictness switch, and the cancellation token; then build the `ViewerImage` from the picture, the notes, and the page,
page count and anchors that the adapter left in the `DirectRenderOptions`. The file therefore goes through the same
sandbox checks, dispatch, and limits as an embedded link. A raster is asked for no size (the post-processing below
keeps the documented fit rule). An `ImageSourceRefusedException` becomes a `ViewerException` with the code
`page-out-of-range`; an `ImageSourceException` whose inner exception is a `ViewerException` (a caller mistake or a limit
that happened inside the loader) is rethrown as that `ViewerException`, while a library failure stays an
`ImageSourceException`.

**`Render(stream, fileName, options)`**: the same, with `FolderImageResolver.Render(stream, fileName, ...)`; the sandbox
folder is `options.Folder` or none, so a stream cannot reach the disk by accident. The options are validated first, before
the stream is read.

**Building the `ViewerImage`**: the picture a source returns is used as is, except for the sizes the caller asked for.
A vector source is asked for the requested size; a raster is always returned at its natural size, so when both `Width`
and `Height` are given and the picture has another size, it is fitted inside the box (aspect ratio kept, centered over a
transparent background) so the result is exactly that size; with one dimension, it is scaled to keep the aspect ratio.
The notes are those of the file followed by those of the files it embedded; the printed text and the embedded images
follow the same order. The resolver clamps a requested size to 1 to `Surface.MaxDimension`, which is why the options are
validated to that range up front: a larger or non-positive value would otherwise be rendered at one size and
post-processed to another. The post-processing allocates a second buffer next to the surface of the source (peak memory
is both), so before it fits or resizes, the viewer checks the final size (width times height) against
`FolderImageResolver.RemainingPixels`, the live part of the shared `MaxDecodedPixels` budget that is still free (the
surface of the source is already charged; see the budget contract on `ImageSourceRequest`), and throws
`ImageSourceBudgetException` (disposing the source's surface) when it does not fit. A vector surface that is already the
requested size is not post-processed and costs nothing more.

### Error Handling

A null `path`, `stream`, or `fileName` throws `ArgumentNullException`; a null, empty or white-space folder for
`CreateImageResolver` throws `ArgumentException`; a path outside `options.Folder` throws `ArgumentException`; a
`Width` or `Height` that is given and is not from 1 to `Surface.MaxDimension`, a `Page` below 1, and a `Dpi` or `Scad`
value that the shared validator `SourceLoader.FindOptionProblem` rejects throw `ArgumentOutOfRangeException`
(parameter `options`, message naming the member) before any file or
stream is read; a page the file does not have is a `ViewerException` with the code `page-out-of-range`, and a model
whose `include` is missing, a size limit, or a Visio `Dpi` that is not a whole number is a `ViewerException` too; a
final picture that does not fit in what is left of `MaxDecodedPixels` throws
`ImageSourceBudgetException`. The other
failures are those of `FolderImageResolver.Render`: `FileNotFoundException` for a missing file, `SandboxException` when
the sandbox refuses the file (a symbolic link, more than 16 MiB), `NotSupportedException` when no source claims the
extension (the message lists the extensions), `ImageSourceException` when the file is found but cannot be drawn (the
original failure is the inner exception), and `OperationCanceledException`. An unsupported feature is a note unless
`ThrowOnUnsupportedFeature` is set.

### Dependencies

The core `ImageSources` subsystem (`IImageSource`, `IFragmentImageSource`, `ImageSourceSet`, `FolderImageResolver`,
`FolderImageResolverOptions`,
`ImageSourceResult`, `ImageSourceDiagnostic`, `EmbeddedImage`, `ImageSourceException`, `ImageSourceRefusedException`, and
`RasterImageSource` for its claims), the
`Imaging` subsystem (`SourceLoader` and its `SourceInput`, `LoadContext`, and option validator; `ImageDiagnostic`), the
`Canvas` subsystem (`Surface.FitInto`, `Surface.Resize`, `Rgba32`), and the claims of the sources of the `CanvasNetSvg`,
`CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, and `CanvasNetVegaLite` systems.

### Callers

Applications and the `CanvasNetTool` system: its `SourceLoader` (now part of the `Imaging` subsystem of this system)
calls `CreateImageResolver` for every SVG file it renders, and the tool reads `Extensions` in the drift test of the `svg`
help topic. The tool passes no extra sources.
