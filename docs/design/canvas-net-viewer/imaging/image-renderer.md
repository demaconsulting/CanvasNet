### ImageRenderer

<!-- cspell:ignore canvasnet jpeg tiff -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageRenderer` is the render operation of the library: it loads the pages of a probed source, runs the image pipeline
over each, and writes one file for each page. Its single responsibility is the order and the all-or-nothing behavior of
that chain; the loading, the transformation, and the writing are done by `SourceLoader`, `ImagePipeline`, and
`ImageWriter`. It is the library form of the `render` command and of the verb commands `crop`, `resize`, `rotate`,
`flip`, and `pad` of the `canvasnet` tool, whose commands parse their arguments into the request and print the result.
The unit is the static class `ImageRenderer` (`ImageRenderer.cs`) with the request and result records.

#### Data Model

**RenderRequest** (record): the required `Output` (positional; the output path; with every page it names a directory, or
contains `{page}`, replaced by the page number counted from 1) and the `init` properties `AllPages` (off), `Source` (the
`SourceOptions` for the load; default), `Write` (the `WriteOptions`; `WriteOptions.Default`), and `Operations` (the list
of `IImageOperation` applied in order; none).

**RenderedPage** (record): `Page` (from 1), `Info` (the facts of the source), `Diagnostics`, `Echo` (the text a model
printed), `Operations` (what the loader, the pipeline, and the writer did, as `AppliedOperation` values; for every page
of
a document each names its page), `Result` (the `WriteResult`), and `Anchors` (the anchors a model declared, mapped
through the pipeline).

**RenderResult** (record): `Info` and `Pages`, in the order they were rendered.

#### Key Methods

**Execute(info, request)**: first refuses a request with a missing member (`Output`, `Source`, `Write`, or `Operations`,
or a null operation) as `ArgumentNullException`, and an operation with an invalid value as a `usage-error` or
`size-limit` (`ImagePipeline.Validate`), so nothing is decoded for such a request. It then checks everything else that
needs no pixels in a fixed order - the page
(`SourceLoader.CheckPage`), the plan of the outputs (a directory output names each page after the input, a name without
`{page}` for several pages is a `usage-error`), the output format (`ImageWriter.ResolveFormat`, with the format of the
write options when set, else the extension), and every output path
(`ImageWriter.CheckTarget` against the input and the overwrite policy). It then opens the source once
(`SourceLoader.OpenPages`) and, for each page, loads it, applies the operations with `ImagePipeline.Apply` (the default
color of a border is the background the user gave, else white for a vector source, else transparent), stages the result
with `ImageWriter.Stage`, and maps the anchors with `ImagePipeline.MapAnchors`. Only when every page was staged are all
the outputs published together with `ImageWriter.PublishAll`; the staged files that were not published are removed
in every case. The input is never modified.

**Input fragment.** When the facts carry a `SourceInfo.Selection` (an input path with a fragment, from
`SourceLoader.Probe(InputReference)`), `Execute` renders the page the selection names instead of
`request.Source.Page`, and only that page even when `AllPages` is set (the request is copied with `AllPages` false and
the page set, so the targets are planned for one page); the model variables, view and chart parameters of the selection
are applied by `SourceLoader.OpenPages`. The warnings of the fragment come with the first page.

#### Error Handling

Every expected failure is a `ViewerException`: `page-out-of-range`, `usage-error` (format, one output for several pages,
an operation value out of range, an operation that does not fit the image), `size-limit` (an operation size above the
maximum), `same-input-output`, `output-exists`, `output-write-failed`, and the codes of
`SourceLoader` and `ImagePipeline`. A null argument or request member throws `ArgumentNullException`. Any other
exception is a defect and
propagates; the staged files are removed on every path, so a failure leaves no new or partial output and every existing
output untouched.

#### Dependencies

- **SourceLoader** - `CheckPage`, `OpenPages`, the loaded page, and `Operation`.
- **ImagePipeline** - `Apply` and `MapAnchors`.
- **ImageWriter** - `ResolveFormat`, `CheckTarget`, `Stage`, and `PublishAll`.
- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface` and `Rgba32`.

#### Callers

- **RenderCommand** (of the tool) - for `render`, `convert`, `crop`, `resize`, `rotate`, `flip`, and `pad`.
- **Library users** - render a file into image files without a command line.
