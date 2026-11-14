# System Design

<!-- cspell:ignore vsdx pptx scad href -->

This document provides the system-level design for CanvasNetViewer.

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

## Architecture

CanvasNetViewer is a .NET library that aggregates the image sources of every CanvasNet format package, distributed as
its own NuGet package (`DemaConsulting.CanvasNet.Viewer`, namespace `DemaConsulting.CanvasNet.Viewer`). It exists so
that an application (or the command-line tool) can draw "any file CanvasNet can read" and let an SVG diagram embed
models, charts, Mermaid diagrams, other SVG files, document pages, and pictures with one call, without referencing and
wiring each format package itself. It is also the library form of the `canvasnet` command-line tool: everything the tool
does with images - report the facts of a file, render it with an ordered pipeline of transformations, inspect its colors
and pixels, compare two images, and compose a contact sheet - is a call of the `Imaging` subsystem, and the tool is a
thin shell that parses a command line into the requests of those calls and prints their results. The system consists
of two units and one subsystem:

- **CanvasNetViewer** (unit; namespace `DemaConsulting.CanvasNet.Viewer`, folder `src/DemaConsulting.CanvasNet.Viewer/`,
  file `CanvasNetViewer.cs`): a static class that lists the built-in image sources and their extensions, creates the
  `FolderImageResolver` of the core `ImageSources` subsystem for one render, and renders any file by path or from a
  stream. The two supporting types, `ViewerRenderOptions` and `ViewerImage`, are covered inline. See _CanvasNetViewer Unit
  Design_ (`canvas-net-viewer/canvas-net-viewer.md`).
- **ViewerException** (unit; file `ViewerException.cs`): the exception with which every expected failure of the
  operations is reported, and the stable `ErrorCodes`. See _ViewerException Unit Design_
  (`canvas-net-viewer/viewer-exception.md`).
- **Imaging** (subsystem; namespace `DemaConsulting.CanvasNet.Viewer.Imaging`, folder `Imaging/`): `SourceLoader`,
  `ImagePipeline`, `ImageWriter`, `ImageComparison`, `ContactSheet`, and `GridOverlay`, and the operation units
  `ImageRenderer`, `ImageInspector`, `ImageComparer`, `MontageBuilder`, and `SvgFlattener`. See _Imaging Subsystem
  Design_ (`canvas-net-viewer/imaging.md`).

`CanvasNetViewer` is modeled as its own top-level software system because it is distributed as its own NuGet package,
per `software-items.md`'s rule that a software package contains exactly one software system. The `CanvasNetViewer` unit
and the `Imaging` subsystem are independent of each other in operation: the `CanvasNetViewer.Render` path draws a file
for an application that wants a picture of it through the image sources, while the `Imaging` operations are the
command-line parity path (pages, resolution, EXIF orientation, transformations, error codes). They meet in one place:
the loader of an SVG document that embeds other files obtains its resolver from `CanvasNetViewer.CreateImageResolver`,
so the embedding is the same for both.

The dependency direction is one-way. The core `ImageSources` subsystem defines the contract (`IImageSource`,
`IImageResolver`) and the resolver; each format package ships a small source that references only the core package
(`SvgImageSource`, `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`, `ScadImageSource`, `VegaLiteImageSource`,
`MermaidImageSource`); the viewer is the one place that knows all of them. No package references the viewer except the
command-line tool, and the viewer has no plug-in mechanism: the built-in list is fixed and explicit (no reflection, no
discovery, no global registry). A **library user** who needs another format, an override, or a live source passes extra
sources for one render.

## External Interfaces

The system exposes the following public API:

- **`CanvasNetViewer.ImageSources`** / **`CanvasNetViewer.Extensions`**: the fixed, read-only list of the built-in
  sources (raster, SVG, PDF, PowerPoint, Visio, OpenSCAD, Vega-Lite, Mermaid) and the sorted union of the file name
  extensions they claim. The command-line tool's `svg` help topic is checked against `Extensions`.
- **`CanvasNetViewer.CreateImageResolver(folder, options)`** / **`CreateImageResolver(folder, extraSources, options)`**:
  a new `FolderImageResolver` for one render, over the built-in sources followed by the extra ones; assign it to
  `SvgLoadOptions.ImageResolver`. Throws `ArgumentException` for a null, empty or white-space folder and
  `ArgumentNullException` for null extra sources. Use it for a render that you drive yourself; `Render` builds its own
  resolver and takes extra sources from `ViewerRenderOptions.ExtraSources` instead.
- **`CanvasNetViewer.Render(path, options)`** / **`Render(stream, fileName, options)`**: draw one file as a
  `ViewerImage` (the picture, its format, the notes, the printed text, and the embedded images). Throw
  `FileNotFoundException`, `NotSupportedException` (no source claims the extension), `FileReferenceException` (the file
  is not a regular file, is too large, or cannot be read), `ViewerException` (with `ThrowOnUnsupportedFeature`, also
  `file-access-denied` for a link the file-access policy denied), `ImageSourceException` (a file that is found but
  cannot be drawn), and `OperationCanceledException`. The file a caller renders by path may lie anywhere (also outside
  the folder option and behind a link): it is the caller's choice and is never judged by the file-access policy.
- **`ViewerRenderOptions`**: `Width`, `Height`, `Folder` (the base of relative links, not a boundary), `FileAccess` (the
  `FileAccessPolicy` of every file the rendered file references, at every level; `FileAccessPolicy.AllowAll` by
  default, like Pandoc), `ThrowOnUnsupportedFeature`, `MaxDecodedPixels`, `ExtraSources`, and `CancellationToken`.
- **The `Imaging` operations** (namespace `DemaConsulting.CanvasNet.Viewer.Imaging`): `SourceLoader.Probe` /
  `Load` / `OpenPages` (the facts of a file, and its pixels, with pages, resolution, and options; `Probe(path,
  FileAccessPolicy)` and `Probe(InputReference, FileAccessPolicy)` read the files a probe needs, the data of a chart,
  under a policy),
  `ImageRenderer.Execute` (load, transform, and write), `ImageInspector.Execute` (colors and pixels),
  `ImageComparer.Execute` (measures, thresholds, and pictures), `MontageBuilder.Execute` (a contact sheet),
  `SvgFlattener.Execute` (a copy of an SVG drawing that other SVG readers can show), and the
  lower-level `ImagePipeline.Apply`, `ImageWriter.Write`, `ContactSheet`, and `GridOverlay.Draw`. Each takes plain
  request values and returns plain result values; see _Imaging Subsystem Design_ for the signatures.
- **`ViewerException`** / **`ErrorCodes`**: the failure of an expected kind, with a stable code, a message, and a hint.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| ----------------------------------- | ---------------- | ---------------------------------------- | ---------------------------------------- |
| `ImageSources` / `Extensions` | Outbound | Property / read-only lists | Fixed; cannot be changed |
| `CreateImageResolver(folder, ...)` | Outbound | Method call / `FolderImageResolver` | One resolver per render; folder not empty |
| `Render(path, ...)` | Inbound/Outbound | Method call / `ViewerImage` return | Extension claimed by a source; the file itself is not judged by the file-access policy |
| `Render(stream, fileName, ...)` | Inbound/Outbound | Method call / `ViewerImage` return | At most 16 MiB; extension claimed by a source |
| `ImageRenderer.Execute` and the other operations | Inbound/Outbound | Method call / request and result records | Files read and written by path; failures are `ViewerException` |
<!-- markdownlint-enable MD013 -->

See _CanvasNetViewer Unit Design_ (`canvas-net-viewer/canvas-net-viewer.md`) for the complete parameter, return, and
exception detail of the render path, and _Imaging Subsystem Design_ for the operations.

## Dependencies

`CanvasNetViewer` depends on the separate `CanvasNet` system (its own package, `DemaConsulting.CanvasNet`) and on the
format systems, each through a project reference from
`src/DemaConsulting.CanvasNet.Viewer/DemaConsulting.CanvasNet.Viewer.csproj`:

- The core `ImageSources` subsystem: `IImageSource`, `ImageSourceSet`, `FolderImageResolver`,
  `FolderImageResolverOptions`, `ImageSourceDiagnostic`, `EmbeddedImage`, `ImageSourceException`, and the
  `RasterImageSource`; and the `Canvas` subsystem's `Surface` and `Rgba32` (the fit of a raster into a box).
- The `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, `CanvasNetVegaLite`, and
  `CanvasNetMermaid` systems: the `SvgImageSource`, `PdfImageSource`, `PptxImageSource`, `VsdxImageSource`,
  `ScadImageSource`, `VegaLiteImageSource`, and `MermaidImageSource` of each package, and for the `Imaging` operations
  the codecs and documents of the same packages (`SvgCodec`, `PdfDocument`, `PptxDocument`, `VsdxDocument`,
  `ScadDocument`, `VegaLiteDocument`, `MermaidDocument`).
- The `CanvasNetRender3D` system (`SceneRendering`, used by the OpenSCAD view loading of `SourceLoader`), referenced
  explicitly. `CanvasNetCharts` arrives transitively.

No other system depends on `CanvasNetViewer` except `CanvasNetTool`, which is a command-line shell over the `Imaging`
operations and creates the resolver for the SVG files it renders; a format package must never reference the viewer.

## Risk Control Measures

`CanvasNetViewer` renders files and documents that may be untrusted. The risk control is the file-access policy of the
core `FolderImageResolver` (`ViewerRenderOptions.FileAccess`, or `SourceOptions.FileAccess` and the `FileAccess` of
the flatten and montage requests for the `Imaging` operations): every file a rendered file references goes through
the core `FileReferenceResolver`, which asks the policy about the real path of the opened file; the policy reaches
every nested picture, model include, and chart data file, anchored at the host-chosen origin of the chain. The
default, `FileAccessPolicy.AllowAll`, reads every regular local file, like Pandoc (`..`, absolute paths, local `file:`
URIs, and symbolic links are followed); a host that renders untrusted documents opts in to confinement with
`FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(folder), FileAccessPolicy.NoSymlinks)`. Under every policy,
network paths and URLs are never loaded, device paths, alternate data streams, and non-regular files are refused, and
depth, count, size, and the pixels allocated in total are bounded (a picture that would exceed the pixel budget being
refused before it is decoded). A denied picture is the warning `image-access-denied` (with
`ThrowOnUnsupportedFeature` or `--strict` the error `file-access-denied`), and a denied include or data file the
error `file-access-denied`; the paths of a denial are only in `ImageSourceDiagnostic.Detail` and the exception
properties, which a service that returns diagnostics to untrusted authors should redact. The file a caller names
itself is never judged by the policy. The viewer adds no file access of its own on the render path. The `Imaging`
operations read the files that a caller names and write the files that a caller names (the tool's own file access is
the same), and the files that an SVG document, an OpenSCAD model, a Vega-Lite chart, or a Mermaid diagram refers to are
read through the same resolver and policy, and never from the network. A source that a **library user** adds is
trusted code of the application: a source that claims a URI scheme receives the link as written, with no file access, and
alone owns what the link may reach (for example, a source that claims `http` must decide which hosts it fetches); no
built-in source claims `http` or `https`, so such links stay unresolved. A viewer never caches a picture, so a live
source is asked again on every render. The shape contains this risk control inherently (IEC 62304 §5.3.3).

## Data Flow

**Embedding path:**

1. **Input**: the folder of a document and optional extra sources
2. **Processing**: `CreateImageResolver` builds a `FolderImageResolver` over the built-in sources followed by the extra
   ones (later sources win) and returns it; the application assigns it to `SvgLoadOptions.ImageResolver`; the SVG codec
   calls `Resolve(href, width, height)` for each `image` and the resolver checks, dispatches, draws, and records
3. **Output**: the pictures in the rendered document, and the resolver's `Diagnostics`, `Echo`, and `Embedded` lists

**Render path:**

1. **Input**: a path (or a stream and a file name) and optional `ViewerRenderOptions`
2. **Validation**: a null argument, an unknown extension, a missing file, or a file that is not a regular file, is too
   large, or cannot be read throws before anything is drawn
3. **Processing**: a resolver for the render draws the file at the requested size; a raster (always returned at its
   natural size) is fitted into a requested box or scaled to keep its aspect ratio
4. **Output**: a `ViewerImage` that owns the `Surface` and carries the format, notes, text, and embedded images

**Operation path** (`Imaging`):

1. **Input**: a probed source (`SourceLoader.Probe`) and a request record (the page and the load options, the operations
   of the pipeline, the output and write options; for a comparison two inputs; for a montage a list of sources)
2. **Validation**: everything that needs no pixels - the page, the output format, the outputs against the inputs, the
   size of a sheet - is checked first, in a fixed order, as `ViewerException` values with stable codes
3. **Processing**: each source is loaded (`SourceLoader`), transformed (`ImagePipeline`), measured or analyzed (the core
   `Surface`), and encoded to a temporary file (`ImageWriter`); only when every output was encoded are all of them
   moved into place, so a failure leaves nothing behind
4. **Output**: a result record that says what was loaded, what was applied, and what was written (a render), the
   dominant colors or the pixel (an inspection), the measures, checks, and pictures (a comparison), or the layout and
   cells of the sheet (a montage); the files written

## Design Constraints

- **Simplicity**: the render path is one static class, no state, no registry; the formats are listed in one place
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Fixed built-in list**: the built-in sources cannot be changed at run time; a library user composes
  `CanvasNetViewer.ImageSources` with their own sources instead
- **No caching**: nothing is cached between renders; create a resolver for each render
- **One error contract**: every expected failure of the `Imaging` operations is a `ViewerException` with a code of
  `ErrorCodes`; the codes are stable, because scripts and the results of the tool rely on them
- **No command-line concerns**: the library knows nothing of a command line, a console, or a result document; the
  tool parses and prints. The text of some messages and hints still names the options of the tool
- **Not a tool plug-in point**: the command-line tool wires the viewer in for its commands and offers no plug-in mechanism
- **Documented examples compile**: the C# examples of the Viewer user guide are complete programs that are compiled
  (never run) against the public API by a system test; a block is marked `<!-- compile -->` (a program) or
  `<!-- signature -->` (a listing), and an unmarked block fails the test. The test project, not the library, depends on
  the compiler package for this
- **Request records**: required data is positional, everything optional is an `init` property with the default of the
  command-line option (see _Imaging Subsystem Design_)

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on, enabling compatibility
across modern .NET runtimes:

| Target Framework | Runtime / Environment                                   |
| ---------------- | ------------------------------------------------------- |
| `net8.0`         | .NET 8 LTS (end of support; kept for library consumers) |
| `net9.0`         | .NET 9 STS (end of support; kept for library consumers) |
| `net10.0`        | .NET 10 LTS                                             |
| `net11.0`        | .NET 11                                                 |

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
