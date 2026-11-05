## CanvasNetViewer Unit Design

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

<!-- cspell:ignore vsdx pptx scad href -->

### Purpose

`CanvasNetViewer` is one of two units of the `CanvasNetViewer` system (the other is `ViewerException`, beside the
`Imaging` subsystem). It is a static class, with its two supporting types
`ViewerRenderOptions` and `ViewerImage`, that does three things: it lists the image sources of every format CanvasNet
can read, it creates the image resolver that lets an SVG document embed all of them, and it renders any file as a
picture. It adds no format knowledge of its own: each format is drawn by the source of its own package, and every file
access is confined by the core `FolderImageResolver`. See _CanvasNetViewer System Design_ (`canvas-net-viewer.md`) for
the system-level architecture.

The unit's behavior is specified by the `CanvasNetViewer-CanvasNetViewer-*` requirements (`ImageSources`,
`CreateImageResolver`, `RenderPath`, `RenderStream`, and `ExtraSources`) in
`docs/reqstream/canvas-net-viewer/canvas-net-viewer.yaml`.

### Data Model

- **`CanvasNetViewer`** — static; holds two read-only values created once: the built-in sources
  (`RasterImageSource`, `SvgImageSource`, `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`, `ScadImageSource`,
  and `VegaLiteImageSource`, in that order; their extensions do not overlap) and the sorted union of their extensions,
  computed through an `ImageSourceSet` so that the claims are validated once. Both are exposed as read-only lists
  (`ImageSources`, `Extensions`). The sources are stateless and thread-safe, so the shared instances serve any number of
  resolvers.
- **`ViewerRenderOptions`** (immutable record) — `Width` and `Height` (nullable; none = natural size, one = keep the
  aspect ratio, both = a box; when given, each must be from 1 to `Surface.MaxDimension`), `Folder` (the sandbox for the
  files the rendered file itself includes or embeds; by
  default the folder of the file for a render by path, none for a stream), `ThrowOnUnsupportedFeature`,
  `MaxDecodedPixels` (nullable; none = the resolver default budget), `ExtraSources`,
  and `CancellationToken`.
- **`ViewerImage`** (`IDisposable`) — owns the `Surface` and carries `Format`, `Diagnostics`, `Echo`, and `Embedded`
  as read-only snapshots (`ReadOnlyCollection<T>` copies, not castable to a mutable list).
  Disposing it disposes the surface.

### Key Methods

**`CreateImageResolver(folder, options)`** and **`CreateImageResolver(folder, extraSources, options)`**: validate the
arguments and return `new FolderImageResolver(folder, sources, options)`, where `sources` is the built-in list, followed
by the extra sources in the second overload. A new resolver is created on every call and nothing is shared between
calls, because a resolver counts the images and collects the notes of one render and never caches a picture. The
precedence rule is the `ImageSourceSet` rule: when several sources claim the same extension or scheme, the later one
wins, so an extra source overrides a built-in one.

**`Render(path, options)`**: validate the options (see _Error Handling_), resolve the sandbox folder (`options.Folder`,
or the folder of the file), refuse a file outside it with `ArgumentException`, express the file as a path relative to the
folder, and call
`FolderImageResolver.Render(relative, width, height)` on a resolver built from the built-in and extra sources, the
strictness switch, and the cancellation token; then build the `ViewerImage`. The file therefore goes through the same
sandbox checks, dispatch, and limits as an embedded link.

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
`Width` or `Height` that is given and is not from 1 to `Surface.MaxDimension` throws `ArgumentOutOfRangeException`
(parameter `options`, message naming `ViewerRenderOptions.Width` or `ViewerRenderOptions.Height`) before any file or
stream is read; a final picture that does not fit in what is left of `MaxDecodedPixels` throws
`ImageSourceBudgetException`. The other
failures are those of `FolderImageResolver.Render`: `FileNotFoundException` for a missing file, `SandboxException` when
the sandbox refuses the file (a symbolic link, more than 16 MiB), `NotSupportedException` when no source claims the
extension (the message lists the extensions), `ImageSourceException` when the file is found but cannot be drawn (the
original failure is the inner exception), and `OperationCanceledException`. An unsupported feature is a note unless
`ThrowOnUnsupportedFeature` is set.

### Dependencies

The core `ImageSources` subsystem (`IImageSource`, `ImageSourceSet`, `FolderImageResolver`, `FolderImageResolverOptions`,
`ImageSourceResult`, `ImageSourceDiagnostic`, `EmbeddedImage`, `ImageSourceException`, and `RasterImageSource`), the
`Canvas` subsystem (`Surface.FitInto`, `Surface.Resize`, `Rgba32`), and the sources of the `CanvasNetSvg`,
`CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, and `CanvasNetVegaLite` systems.

### Callers

Applications and the `CanvasNetTool` system: its `SourceLoader` (now part of the `Imaging` subsystem of this system)
calls `CreateImageResolver` for every SVG file it renders, and the tool reads `Extensions` in the drift test of the `svg`
help topic. The tool passes no extra sources.
