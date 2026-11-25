## CanvasNetViewer Unit Design

![CanvasNetViewer Structure](CanvasNetViewerView.svg)

### Purpose

`CanvasNetViewer` is one of two units of the `CanvasNetViewer` system (the other is `ViewerException`, beside the
`Imaging` subsystem). It is a static class, with its two supporting types
`ViewerRenderOptions` and `ViewerImage`, that does three things: it lists the image sources of every format CanvasNet
can read, it creates the image resolver that lets an SVG document embed all of them, and it renders any file as a
picture. It adds no format knowledge of its own: each format is drawn by the `SourceLoader`, the same code the
command-line tool uses (which calls the libraries of the format packages), and every file
a rendered file references is read by the core `FolderImageResolver` under the file-access policy of the caller. See
*CanvasNetViewer System Design* (`canvas-net-viewer.md`) for
the system-level architecture.

The unit's behavior is specified by the `CanvasNetViewer-CanvasNetViewer-*` requirements (`ImageSources`,
`CreateImageResolver`, `RenderPath`, `RenderStream`, `RenderErrors`, `LoaderSources`, `RenderOptions`,
`ResourceLimits`, `ExtraSources`, `FileAccess`,
`FileAccessDenied`, and `Positions`) in
`docs/reqstream/canvas-net-viewer/canvas-net-viewer.yaml`. The unit comprises `CanvasNetViewer.cs`,
`ViewerRenderOptions.cs`, `ViewerImage.cs`, and `LoaderImageSource.cs` (the adapter, with the internal
`DirectRenderOptions`).

### Data Model

- **`CanvasNetViewer`** — static; holds two read-only values created once: the built-in sources, ten
  `LoaderImageSource` adapters (raster, svg, pdf, pptx, vsdx, scad, vega-lite, mermaid, plantuml, and mathml, in that
  order,
  with the
  names, extensions and schemes of the format packages' sources; their extensions do not overlap), and the sorted union
  of their extensions, computed through an `ImageSourceSet` so that the claims are validated once. Both are exposed as
  read-only lists (`ImageSources`, `Extensions`). The sources are stateless and thread-safe, so the shared instances
  serve any number of resolvers.
- **`LoaderImageSource`** (internal sealed class, `IImageSource` and `IFragmentImageSource`) — an adapter over the
  `SourceLoader` of the `Imaging` subsystem, so that the tool, `Render` and a linked file are drawn by the same code. It
  is built from the claims of the package source it stands for, the `SourceKind`s it draws, the fragment parameters of
  its pages (`page` for PDF and Visio, `page` and `slide` for PowerPoint) and whether it accepts `view`, `azimuth`,
  `elevation`, and `projection` (the model source only); the Mermaid source alone also accepts `theme`
  (`MermaidImageSource.ThemeParameter`), the MathML source alone accepts `display`
  (`MathMlImageSource.DisplayParameter`, read by `ReadEquation` into an `EquationOptions`: `block` or `inline` in any
  case, any other value refused with `image-fragment-invalid`), and the PlantUML source accepts no parameter at all. It
  also declares the prefix of the parameters whose names the link author chooses (`FragmentParameterPrefixes`): `def.`
  for the model source and `param.` for the chart source (the constants of `ScadImageSource` and `VegaLiteImageSource`),
  none for the others. It cannot be the package source itself: the format packages cannot call `SourceLoader`, which
  depends on all of them. The package sources stay public stand-alone providers with the same `page` support. It also
  holds the `SourceLoader.SourceLimits` (the limits of a chart and a diagram, the library defaults for the shared
  instances) that it gives to `OpenInput` and to the `LoadContext` of every file it draws; `WithLimits` creates a copy
  with other limits.
- **`DirectRenderOptions`** (internal sealed class, `IImageSourceOptions`) — the options of a direct render
  (the page, the resolution, the background, the model view options, the chart parameters, and `Positions`, the
  `PositionSelection` of the feature positions) and the output of the render
  (the page drawn, the
  number of pages, whether the file is a vector source (`IsVector`, for the default margin of a fit), the anchors, and
  `ReportedPositions` and `PositionsTruncated`, the positions a diagram or a chart
  reported), handed through `FolderImageResolver.Render(..., options)`. Only the file rendered directly gets the
  selection: a link builds its options without `DirectRenderOptions`, so a picture a document embeds never reports
  positions.
- **`ViewerRenderOptions`** (immutable record) — `Width` and `Height` (nullable; none = natural size, one = keep the
  aspect ratio, both = a box; when given, each must be from 1 to `Surface.MaxDimension`), `Page` (1-based, default 1),
  `Dpi` (nullable; none = 96), `Background` (nullable; none = the default of the `SourceLoader`, which the tool uses
  too: `SourceLoader.DefaultVectorBackground`, opaque white, for a vector source (a MathML equation included), the own
  background of a PlantUML diagram and a Vega-Lite chart, and no flattening for a raster; a color with zero alpha, set
  explicitly, keeps a vector source transparent), `Scad` (a `ScadViewOptions`, nullable), `VegaLite` (a `ChartOptions`
  with the values of the parameters of a chart, nullable; the option behind `--param`), `Folder` (the base of the
  relative references of the rendered file, not a boundary; by default the folder of the file for a render by path, none
  for a stream), `FileAccess` (the `FileAccessPolicy` of every file the rendered file references, its pictures, a
  model's includes, a chart's data, and the files those read in turn; `FileAccessPolicy.AllowAll` by default, `null`
  refused with `ArgumentNullException`; passed to the resolver as `FolderImageResolverOptions.FileAccess`),
  `ThrowOnUnsupportedFeature`, `MaxDecodedPixels` (nullable; none = the resolver default budget), `ExtraSources`,
  `Positions` (a core `PositionSelection`, nullable; none = no positions: the selectors of the feature positions a
  Mermaid diagram or a Vega-Lite chart reports, ignored by every other kind of file), `VegaLiteLimits`, `MermaidLimits`,
  `PlantUmlLimits`, and `MathMlLimits` (the `VegaLiteLimits`, `MermaidLimits`, `PlantUmlLimits`, and `MathMlLimits` of
  the format libraries, nullable; none = the library default; any value the type accepts, which validates itself, so the
  viewer adds no check; they apply at every level of nesting, unlike `Page`, `Dpi`, `Scad`, and `VegaLite`, which only
  the rendered file takes), and `CancellationToken`. There is deliberately no `Scale`, `Filter` or `AutoOrient`: the
  size is `Width`/`Height` (or `Dpi`), and the resampling of a raster belongs to `ImageWriter` and `SourceLoader`.
- **`ViewerImage`** (`IDisposable`) — owns the `Surface` and carries `Format`, `Page`, `PageCount`, `Diagnostics`,
  `Echo`, `Anchors`, `Embedded`, and `Positions` (the feature positions the selection matched, in the pixels of the
  returned picture; empty when none were asked for and for every other kind of file)
  as read-only snapshots (`ReadOnlyCollection<T>` copies, not castable to a mutable list), and `PositionsTruncated`
  (whether matching positions were left out because of the cap of `PositionCollector.MaxPositions`).
  Disposing it disposes the surface.

### Key Methods

**`LoaderImageSource.Render(request)`**: (1) take the options: a request with `DirectRenderOptions` uses them, a link
builds them from its fragment (`page` or `slide` through `GetPositiveInt32`, `view`, `azimuth`, `elevation`,
`projection`, and every
`def.<name>=<value>` for a
model
(`ReadScad`: `ReadView` checks the name of `view` against `SourceLoader.ScadViews` and `ReadDegrees` reads `azimuth`
(any finite number of degrees, normalized to 0 up to 360 as `--azimuth` is) and `elevation` (a finite number from -90
to 90) with the invariant culture, refusing any other value as `image-fragment-invalid`; the angles replace those of
the named view, iso without one, and any of the three makes the view explicit; `ReadPerspective` reads `projection`
with the words of `--projection` (`ortho` or `orthographic` for orthographic, `perspective`, in any case; any other
value or an empty one is `image-fragment-invalid`) into `ScadViewOptions.Perspective`, which does not make the view
explicit;
`ImageParameterValues.ReadVariables`,
the names checked by constructing `ScadEvaluationOptions`, the result
a
`ScadViewOptions` with `Defines` - and no explicit view when only definitions were given), every `param.<name>=<value>`
for a
chart (`ReadChart`: `ImageParameterValues.ReadParameters`, the values checked by constructing `VegaLiteRenderOptions`,
the
result a `ChartOptions` with `WarnOnUnknownParameters` set, because a link is not a command line), the `theme=<name>`
of a Mermaid diagram (`ReadDiagram`: the name compared with `MermaidRenderOptions.ThemeNames` without regard to case by
`SourceLoader.FindMermaidTheme`, the result a `DiagramOptions` with the canonical name), a value or name that
is not
usable being an `ImageSourceRefusedException` with the code `image-fragment-invalid`, the strictness switch,
`request.FileAccess` as the `SourceOptions.FileAccess` of the load (so the includes, data and
pictures of a linked model, chart, diagram or SVG file are read under the policy of the render, anchored at its
origin),
the background: for a direct render the `Background` of the `DirectRenderOptions` as given, so `null` keeps the
default of the loader (white for a vector source, as the tool), and for a link a transparent background for a vector
source and none for a chart (`DefaultBackground`), so the embedding document shows through, and for a vector source
the placement as `Width` and
`Height`; an embedded file never inherits the options of its embedder, and only `DirectRenderOptions` carry the
`Positions` selection, so a linked picture never reports positions); (2) wrap the bytes in a
`SourceLoader.SourceInput` named and based as the resolver found the file; (3) for a raster, refuse from its declared
size, before anything is decoded, a picture that cannot fit in the budget; (4) `OpenInput` (with `request.FileAccess`
and the limits of the source), which parses a PDF,
presentation or drawing once and returns the facts about its pages together with the parsed document (any other kind
is described by `ProbeInput`), and a page below 1 or beyond the page count is an `ImageSourceRefusedException` with the
code `image-page-out-of-range`; (5) open the pages, handing the already parsed document to the page source (which then
owns it; the `OpenedSource` disposes it on every path where it was not handed over), with a
`LoadContext` that charges every surface to the request (`EnsureWithinBudget`), observes the cancellation token, draws a
nested SVG through the scoped resolver of the request (so the depth, cycle and budget limits of the render hold),
carries the limits of the source (`LoadContext.Limits`), and
returns a document page at its fitted size; (6) read the page and convert the notes of the loader to
`ImageSourceDiagnostic` (keeping `Detail`, `Location`, `Page` and `Origin`). For an **embedded** picture a
`ViewerException` that wraps a library failure is rethrown as the original exception, so the documented
`InnerException` of an `ImageSourceException` holds for a resolver the caller drives, and the embedding render maps it
again with the shared mapping; one without an inner exception (a size limit, a missing include of a model) is thrown as
it is. For a **direct** render (the request carries `DirectRenderOptions`) the mapped `ViewerException` is thrown as it
is, with its code, location, detail code, and the library exception as its inner exception.

**`CreateImageResolver(folder, options)`** and **`CreateImageResolver(folder, extraSources, options)`**: validate the
arguments and return `new FolderImageResolver(folder, sources, options)`, where `sources` is the built-in list, followed
by the extra sources in the second overload. A new resolver is created on every call and nothing is shared between
calls, because a resolver counts the images and collects the notes of one render and never caches a picture. The
precedence rule is the `ImageSourceSet` rule: when several sources claim the same extension or scheme, the later one
wins, so an extra source overrides a built-in one. `Render` does not use this method: it builds its own resolver from
the built-in sources plus `ViewerRenderOptions.ExtraSources`, so `ExtraSources` is for a picture of one file made by
`Render`, and `CreateImageResolver` is for a render that the caller drives (for example `SvgCodec.Load` with
`SvgLoadOptions.ImageResolver`) and that needs the resolver and its notes.

**`Render(path, options)`**: validate the options (see *Error Handling*), resolve the base folder (`options.Folder`, or
the folder of the file), and call `FolderImageResolver.Render(fullPath, width, height, directOptions)` on a resolver
built from the built-in and extra sources, the strictness switch, the file-access policy, and the cancellation token
(`CreateResolver`; when any of `VegaLiteLimits`, `MermaidLimits`, `PlantUmlLimits`, and `MathMlLimits` is set, the
built-in sources of
the resolver are copies made by `LoaderImageSource.WithLimits` for this render, so the probe and the render of the file
and of every file drawn through the resolver, at any level, use the limits; otherwise the shared instances are used);
the file may lie anywhere, also outside the folder and behind a link, because it is the caller's choice and the resolver
reads it without judging it by the policy (`FileReferenceResolver.ResolveTopLevel`), while every file it references is
judged; then build the `ViewerImage` from the picture, the notes, and the page, page count, anchors, and feature
positions (with whether they were truncated) that the adapter left in the `DirectRenderOptions`. The file therefore goes
through the same regular-file check, size limit, dispatch, and limits as an embedded link, but not the policy. A raster
is asked for no size (the post-processing below keeps the documented fit rule). `RenderDirect` is the single exit of the
failures of a direct render (see *Error Handling*).

**`Render(stream, fileName, options)`**: the same, with `FolderImageResolver.Render(stream, fileName, ...)`; the base
folder is `options.Folder` or none, so a stream cannot reach the disk by accident. The options are validated first,
before
the stream is read.

**Building the `ViewerImage`**: the picture a source returns is used as is, except for the sizes the caller asked for. A
vector source is asked for the requested size; a raster is always returned at its natural size, so when both `Width` and
`Height` are given and the picture has another size, it is fitted inside the box (aspect ratio kept, centered over the
`Background`, or when none is given over `SourceLoader.DefaultVectorBackground` for a vector source, which the adapter
reports in `DirectRenderOptions.IsVector`, and transparent for a raster or an extra source) so the result is exactly
that size; with one dimension, it is scaled to keep the aspect ratio.
The notes are those of the file followed by those of the files it embedded; the printed text and the embedded images
follow the same order. The resolver clamps a requested size to 1 to `Surface.MaxDimension`, which is why the options are
validated to that range up front: a larger or non-positive value would otherwise be rendered at one size and
post-processed to another. The anchors of a model and the feature positions of a diagram or a chart follow the fit or
the resize: the steps are recorded as applied operations and mapped with `ImagePipeline.MapAnchors` and
`ImagePipeline.MapPositions`, so the boxes and pixels of `ViewerImage.Positions` are those of the returned picture. The
post-processing allocates a second buffer next to the surface of the source (peak memory is both), so before it fits or
resizes, the viewer checks the final size (width times height) against `FolderImageResolver.RemainingPixels`, the live
part of the shared `MaxDecodedPixels` budget that is still free (the surface of the source is already charged; see the
budget contract on `ImageSourceRequest`), and throws the `budget-exceeded` `ViewerException` (disposing the source's
surface) when it does not fit. A vector surface that is already the requested size is not post-processed and costs
nothing more.

### Position Bindings

The resolver of `CreateImageResolver` (and of every render) implements `IImagePositionResolver` through
`FolderImageResolver`, and `LoaderImageSource` answers `ImageSourceRequest.Positions` for a Mermaid diagram, a Vega-Lite
chart, and the anchors of an OpenSCAD model (kind `anchor`), so an SVG file the facade renders resolves its position
bindings from the pictures it draws. `CreateImage` applies the strict option to them after the links that were not
drawn: an `svg-binding-*` note of the rendered file (in the result) or of a nested file (in the resolver) is
`svg-binding-failed`. Requirement: `CanvasNetViewer-CanvasNetViewer-BindingPositions`.

### Error Handling

A null `path`, `stream`, or `fileName` throws `ArgumentNullException`; a null, empty or white-space folder for
`CreateImageResolver` throws `ArgumentException`; a null `FileAccess` is refused when the options are built; a path
outside `options.Folder` is not an error; a `Width` or `Height` that is given and is not from 1 to
`Surface.MaxDimension`, a `Page` below 1, and a `Dpi` or `Scad` value that the shared validator
`SourceLoader.FindOptionProblem` rejects throw `ArgumentOutOfRangeException` (parameter `options`, message naming the
member) before any file or stream is read; a page the file does not have is a `ViewerException` with the code
`page-out-of-range`, and a model whose `include` is missing, a size limit, or a Visio `Dpi` that is not a whole number
is a `ViewerException` too. Every other failure of the input leaves through `RenderDirect`, which turns each one into
a `ViewerException` with the original exception as its inner exception: an `ImageSourceRefusedException` is
`page-out-of-range`; an `ImageSourceException` whose inner exception is a `ViewerException` (every failure of a built-in
source, already mapped by the loader with its code, location, and detail code) is rethrown as that `ViewerException`;
any other `ImageSourceException` (a picture an SVG file embeds, whose source threw the library exception, or an extra
source) is mapped by the shared mapping of the loader (`SourceLoader.MapFailure` for the file itself,
`SourceLoader.SvgImages.MapPicture` for an embedded picture, whose message names both files), and is `operation-failed`
when the mapping does not know it; an `ImageSourceBudgetException` (the picture, or the final picture of a resized
render, does not fit in `MaxDecodedPixels`) is `budget-exceeded` with a hint that names `MaxDecodedPixels`; the
`FileNotFoundException` of a missing file is `input-not-found` (through the shared mapping); the
`NotSupportedException` of an extension no source claims is `unsupported-format` (the message lists the extensions),
and only that one: a failure inside a source arrives wrapped in an `ImageSourceException`, and for a render by stream
`RenderDirect` checks with `ClaimsExtension` (the rule of `ImageSourceSet.FindByExtension`: the part after the last
slash or backslash ends with a claimed extension, ignoring case) whether the name is claimed; the
`FileReferenceException` of a file that is not a regular file, is larger than 16 MiB, or cannot be read, the
`InvalidDataException` of a stream longer than 16 MiB, and a failure while reading the caller's stream (an
`IOException` through the shared mapping, or a `NotSupportedException` of a stream whose name is claimed) are
`input-unreadable`. `Render(stream, ...)` refuses a stream that cannot be read (`CanRead` is false) with an
`ArgumentException` before it reads anything. A cancellation stays an
`OperationCanceledException`. The messages and hints name no option of the command-line tool. An unsupported feature
is a note unless `ThrowOnUnsupportedFeature` is set. A link the file-access policy denies is the warning
`image-access-denied`, and with `ThrowOnUnsupportedFeature` a `ViewerException` with the code `file-access-denied`; a
denied include or data file, at any level, fails the render with `file-access-denied`, whose chain of inner exceptions
holds the `FileReferenceException`. A feature position selector that matched nothing is the note
`positions-unmatched` (the loader adds it), and with `ThrowOnUnsupportedFeature` a `ViewerException` with the code
`positions-unmatched`, thrown before the picture is returned.

### Dependencies

The core `ImageSources` subsystem (`IImageSource`, `IFragmentImageSource`, `ImageSourceSet`, `FolderImageResolver`,
`FolderImageResolverOptions`,
`ImageSourceResult`, `ImageSourceDiagnostic`, `EmbeddedImage`, `ImageSourceException`, `ImageSourceRefusedException`,
and
`RasterImageSource` for its claims), the
`Imaging` subsystem (`SourceLoader` and its `SourceInput`, `LoadContext`, and option validator; `ImageDiagnostic`; and
`ImagePipeline.MapAnchors` and `MapPositions`), the
`Canvas` subsystem (`Surface.FitInto`, `Surface.Resize`, `Rgba32`), the core `Positions` subsystem
(`PositionSelection`, `ImagePosition`), and the claims of the sources of the `CanvasNetSvg`,
`CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetScad`, `CanvasNetVegaLite`, `CanvasNetMermaid`,
`CanvasNetPlantUml`, and `CanvasNetMathMl` systems
(and, for the theme fragment, `MermaidImageSource.ThemeParameter` and `MermaidRenderOptions.ThemeNames`; for the display
fragment, `MathMlImageSource.DisplayParameter` and `MathMlDisplay`).

### Callers

Applications and the `CanvasNetTool` system: its `SourceLoader` (now part of the `Imaging` subsystem of this system)
calls `CreateImageResolver` for every SVG file it renders, and the tool reads `Extensions` in the drift test of the
`svg`
help topic. The tool passes no extra sources.
