# System Design

<!-- cspell:ignore vsdx pptx scad href -->

This document provides the system-level design for CanvasNetViewer.

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

## Architecture

CanvasNetViewer is a .NET library that aggregates the image sources of every CanvasNet format package, distributed as
its own NuGet package (`DemaConsulting.CanvasNet.Viewer`, namespace `DemaConsulting.CanvasNet.Viewer`). It exists so
that an application (or the command-line tool) can draw "any file CanvasNet can read" and let an SVG diagram embed models,
charts, other SVG files, document pages, and pictures with one call, without referencing and wiring each format
package itself. The system consists of a single implemented unit:

- **CanvasNetViewer** (namespace `DemaConsulting.CanvasNet.Viewer`, folder `src/DemaConsulting.CanvasNet.Viewer/`,
  flat —
  no further nesting): a static class that lists the built-in image sources and their extensions, creates the
  `FolderImageResolver` of the core `ImageSources` subsystem for one render, and renders any file by path or from a
  stream. The two supporting types, `ViewerRenderOptions` and `ViewerImage`, are covered inline. See _CanvasNetViewer Unit
  Design_ (`canvas-net-viewer/canvas-net-viewer.md`).

`CanvasNetViewer` is modeled as its own top-level software system because it is distributed as its own NuGet package,
per `software-items.md`'s rule that a software package contains exactly one software system. It has exactly one unit,
so no Subsystem tier is interposed (the same reasoning as `CanvasNetVsdx`).

The dependency direction is one-way. The core `ImageSources` subsystem defines the contract (`IImageSource`,
`IImageResolver`) and the resolver; each format package ships a small source that references only the core package
(`SvgImageSource`, `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`, `ScadImageSource`, `VegaLiteImageSource`); the
viewer is the one place that knows all of them. No package references the viewer except the command-line tool, and the
viewer has no plug-in mechanism: the built-in list is fixed and explicit (no reflection, no discovery, no global
registry). A **library user** who needs another format, an override, or a live source passes extra sources for one
render.

## External Interfaces

The system exposes the following public API:

- **`CanvasNetViewer.ImageSources`** / **`CanvasNetViewer.Extensions`**: the fixed, read-only list of the built-in sources
  (raster, SVG, PDF, PowerPoint, Visio, OpenSCAD, Vega-Lite) and the sorted union of the file name extensions they
  claim. The command-line tool's `svg` help topic is checked against `Extensions`.
- **`CanvasNetViewer.CreateImageResolver(folder, options)`** / **`CreateImageResolver(folder, extraSources, options)`**:
  a new `FolderImageResolver` for one render, over the built-in sources followed by the extra ones; assign it to
  `SvgLoadOptions.ImageResolver`. Throws `ArgumentException` for a null, empty or white-space folder and
  `ArgumentNullException` for null extra sources.
- **`CanvasNetViewer.Render(path, options)`** / **`Render(stream, fileName, options)`**: draw one file as a
  `ViewerImage` (the picture, its format, the notes, the printed text, and the embedded images). Throw
  `FileNotFoundException`, `NotSupportedException` (no source claims the extension), `ArgumentException` (the file is
  outside the folder option), `SandboxException`, `ImageSourceException` (a file that is found but cannot be drawn), and
  `OperationCanceledException`.
- **`ViewerRenderOptions`**: `Width`, `Height`, `Folder`, `ThrowOnUnsupportedFeature`, `MaxDecodedPixels`,
  `ExtraSources`, and `CancellationToken`.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| ----------------------------------- | ---------------- | ---------------------------------------- | ---------------------------------------- |
| `ImageSources` / `Extensions` | Outbound | Property / read-only lists | Fixed; cannot be changed |
| `CreateImageResolver(folder, ...)` | Outbound | Method call / `FolderImageResolver` | One resolver per render; folder not empty |
| `Render(path, ...)` | Inbound/Outbound | Method call / `ViewerImage` return | Extension claimed by a source; file in the folder |
| `Render(stream, fileName, ...)` | Inbound/Outbound | Method call / `ViewerImage` return | At most 16 MiB; extension claimed by a source |
<!-- markdownlint-enable MD013 -->

See _CanvasNetViewer Unit Design_ (`canvas-net-viewer/canvas-net-viewer.md`) for the complete parameter, return, and
exception detail.

## Dependencies

`CanvasNetViewer` depends on the separate `CanvasNet` system (its own package, `DemaConsulting.CanvasNet`) and on the
format systems, each through a project reference from
`src/DemaConsulting.CanvasNet.Viewer/DemaConsulting.CanvasNet.Viewer.csproj`:

- The core `ImageSources` subsystem: `IImageSource`, `ImageSourceSet`, `FolderImageResolver`,
  `FolderImageResolverOptions`, `ImageSourceDiagnostic`, `EmbeddedImage`, `ImageSourceException`, and the
  `RasterImageSource`; and the `Canvas` subsystem's `Surface` and `Rgba32` (the fit of a raster into a box).
- The `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, and `CanvasNetVegaLite` systems:
  the `SvgImageSource`, `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`, `ScadImageSource`, and
  `VegaLiteImageSource` of each package. `CanvasNetRender3D` and `CanvasNetCharts` arrive transitively.

No other system depends on `CanvasNetViewer` except `CanvasNetTool`, which creates the resolver for the SVG files it
renders; a format package must never reference the viewer.

## Risk Control Measures

`CanvasNetViewer` renders files and documents from an externally supplied folder, which may be untrusted. The risk
control is the confinement of the core `FolderImageResolver` (every file access goes through the sandboxed file
resolver rooted at the folder; absolute paths, paths that leave the folder, and symbolic links are refused; network URLs
are never loaded; depth, count, size, and the pixels allocated in total are bounded, a picture that would exceed the
pixel budget being refused before it is decoded), which the viewer inherits unchanged by building every render on
it. The viewer adds no file access of its own. A source that a **library user** adds is trusted code of the
application: a source that claims a URI scheme receives the link as written, with no sandbox, and alone owns what the
link may reach (for example, a source that claims `http` must decide which hosts it fetches); no built-in source
claims `http` or `https`, so such links stay unresolved. A viewer never caches a picture, so a live source is asked
again on every render. The single-unit shape contains this risk control inherently (IEC 62304 §5.3.3).

## Data Flow

**Embedding path:**

1. **Input**: the folder of a document and optional extra sources
2. **Processing**: `CreateImageResolver` builds a `FolderImageResolver` over the built-in sources followed by the extra
   ones (later sources win) and returns it; the application assigns it to `SvgLoadOptions.ImageResolver`; the SVG codec
   calls `Resolve(href, width, height)` for each `image` and the resolver confines, dispatches, draws, and records
3. **Output**: the pictures in the rendered document, and the resolver's `Diagnostics`, `Echo`, and `Embedded` lists

**Render path:**

1. **Input**: a path (or a stream and a file name) and optional `ViewerRenderOptions`
2. **Validation**: a null argument, a path outside the folder option, an unknown extension, a missing file, or a sandbox
   refusal throws before anything is drawn
3. **Processing**: a resolver for the render draws the file at the requested size; a raster (always returned at its
   natural size) is fitted into a requested box or scaled to keep its aspect ratio
4. **Output**: a `ViewerImage` that owns the `Surface` and carries the format, notes, text, and embedded images

## Design Constraints

- **Simplicity**: one static class, no state, no registry; the formats are listed in one place
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Fixed built-in list**: the built-in sources cannot be changed at run time; a library user composes
  `CanvasNetViewer.ImageSources` with their own sources instead
- **No caching**: nothing is cached between renders; create a resolver for each render
- **Not a tool plug-in point**: the command-line tool wires the viewer in for SVG inputs and offers no plug-in mechanism

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on, enabling compatibility
across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation to Base Class Library (BCL) APIs and the public APIs of the
CanvasNet packages. No platform-specific native interop is used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the core and format packages as
  ordinary NuGet dependencies
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
