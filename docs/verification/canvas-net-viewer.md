# System Verification Design

<!-- cspell:ignore vsdx pptx scad href slnx -->

This document describes the system-level verification strategy for CanvasNetViewer.

## Verification Approach

The CanvasNetViewer system is verified through system-level integration tests that exercise the package as a whole from
the perspective of a consumer, through its public API only (`ImageSources`, `Extensions`, `CreateImageResolver`,
`Render`, the options and result types, and the `Imaging` operations with `ViewerException`). The tests use the real
format packages (nothing is mocked): an SVG document
that embeds a raster, a model, a chart, another SVG file, and a PDF page through the resolver of the viewer, and the
custom image sources an application plugs in. The custom sources are real implementations of `IImageSource` that record
every request: a source for a made-up file type, a source that takes over a built-in extension, a source that claims a
URI scheme (a live frame whose color changes on every call), and a render by path with extra sources. The platform
requirements reuse the first test, which only needs the package to load and list its sources.

System tests reside in `CanvasNetViewerSystemIntegrationTests.cs` within the `DemaConsulting.CanvasNet.Viewer.Tests`
project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Files**: per-test temporary folders created and deleted by the test; the pictures, models, charts, and PDF are
  generated in memory by `ViewerFixtures`; the source-tree guard reads the text of `src/DemaConsulting.CanvasNet.Viewer`
- **Isolation**: each test builds its own folder and its own resolver; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces. The live source of the system tests stands in for a camera or
a video frame: it is a plain class in the test project that returns a differently colored picture on every call.

## System-Level Test Scenarios

### Integration: Image Sources List Every Format

**Test**: `CanvasNetViewer_SystemIntegration_ImageSources_ListEveryFormat`

Asserts that the list holds the eight sources in order (`raster`, `svg`, `pdf`, `pptx`, `vsdx`, `scad`, `vega-lite`,
`mermaid`), that `Extensions` is exactly the sorted union of their claims (the nineteen extensions, `.mmd` and
`.mermaid` included), and that no built-in source
claims a URI scheme (so `http` and `https` links stay unresolved). Serves as the shared platform-proof test referenced
by every `CanvasNetViewer-Platform-*` requirement.

### Integration: Svg Embedding Every Format Draws All And Reports Embeds

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingEveryFormat_DrawsAllAndReportsEmbeds`

Writes a raster, a model, a chart, a PDF, and an SVG file in a sub folder that embeds its own raster, renders an SVG
that links all of them with the resolver of `CreateImageResolver`, and asserts that the picture of every placement is
drawn, that nothing is reported, that the five kinds of file appear in the embedded images with their formats, that
five images are embedded at depth 0, and that the one picture of the nested SVG file is at depth 1.

### Integration: Svg Embedding With Query And Fragment Draws The File

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingWithQueryAndFragment_DrawsTheFile`

Writes a raster and a model, renders an SVG whose links are `logo.png?v=2` and `part.scad?v=2#view=front`, and asserts
that both are drawn (the fragment chooses the viewpoint of the model), that nothing is reported, and that the embedded
images list the links as written.

### Integration: Svg Embedding Of A Huge Raster In A Tiny Placement Is Refused With A Warning

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingHugeRasterInTinyPlacement_IsRefusedWithWarning`

Writes a 33-byte PNG that only declares 8192 by 8192 pixels (decoding it would fail and take 256 MiB) and a normal
raster, renders an SVG that places the first in a 5 by 5 rectangle and the second beside it, and asserts that the
render succeeds, the normal picture is drawn, the huge placement shows the background, the one note is the warning
`svg-image-too-large`, and only the normal raster is listed as embedded.

### Integration: Mermaid Diagram Renders Directly And Embedded With Theme

**Test**: `CanvasNetViewer_SystemIntegration_MermaidDiagram_RendersDirectlyAndEmbeddedWithTheme`

Writes a two-node flowchart (`flow.mmd`) and renders it with `Render` by path, then renders an SVG document that links
it twice, as `flow.mmd#theme=dark` and as `flow.mmd#theme=midnight`, with the resolver of `CreateImageResolver`. Asserts
that the direct render has the format `mermaid`, a transparent corner (a direct render has no background, as Mermaid's
own picture), the node fill of the default theme (`#ECECFF`) across its middle, and no note; that the first cell of the
SVG document has the node fill of the dark theme (`#1F2020`) and is the one embedded image, listed with its link as
written; and that the second link is refused with the one note `image-fragment-invalid` whose message names the `theme`
parameter.

### Integration: Custom Source For New Extension Is Embedded

**Test**: `CanvasNetViewer_SystemIntegration_CustomSourceForNewExtension_IsEmbedded`

Asserts that a source for a file type the library does not know is embedded by `<image href>` once it is passed to
`CreateImageResolver`, receives the file name, folder, and the placement size, and is listed as an embedded image, and that
without the source the same link is only the `svg-image-unresolved` warning.

### Integration: Custom Source Overriding Built In Extension Wins

**Test**: `CanvasNetViewer_SystemIntegration_CustomSourceOverridingBuiltInExtension_Wins`

Asserts, as a theory over the `.png` and `.scad` extensions, that a custom source placed after the built-in ones draws
the file instead of the built-in source (the later source wins); the text of the file is not a valid picture or model,
so the built-in source could not have drawn it.

### Integration: Custom Scheme Source Is Embedded From Raw Href

**Test**: `CanvasNetViewer_SystemIntegration_CustomSchemeSource_IsEmbeddedFromRawHref`

Asserts that a source that claims a URI scheme is handed the link exactly as written, without a file, and that its
picture is drawn.

### Integration: Live Frame Source Gives Fresh Frame On Every Render

**Test**: `CanvasNetViewer_SystemIntegration_LiveFrameSource_GivesFreshFrameOnEveryRender`

Asserts that nothing is cached: the live source draws a different frame on every render, whether a new resolver is
created for each render or one resolver serves two renders.

### Integration: Render With Extra Sources Uses Custom Sources

**Test**: `CanvasNetViewer_SystemIntegration_RenderWithExtraSources_UsesCustomSources`

Asserts that `Render(path)` honors `ViewerRenderOptions.ExtraSources`: a file of the made-up type is drawn by the custom
source, and an SVG file rendered by path embeds a scheme-claimed live frame.

### Integration: Image Operations Probe Render Inspect Compare Montage

**Test**: `CanvasNetViewer_SystemIntegration_ImageOperations_ProbeRenderInspectCompareMontage`

Writes the SVG fixture, probes it, renders it twice with `ImageRenderer` (once flipped), inspects the flipped picture with
`ImageInspector`, compares the two renders with `ImageComparer`, and puts both on a contact sheet with `MontageBuilder`.
Asserts that every operation produced its result, that the two renders have the same size and are not identical, and
that the sheet has two cells and exists.

### Integration: Flatten Composed SVG Becomes Plain SVG

**Test**: `CanvasNetViewer_SystemIntegration_Flatten_ComposedSvgBecomesPlainSvg`

Writes a drawing that links the first page of the PDF of the fixture factory and a nested SVG file that links the
Mermaid diagram of the fixtures, flattens it with `SvgFlattener`, and draws the original with the loader (which follows
the links) and both files with the SVG library without a resolver (an SVG reader that follows no links). Asserts that
both links were replaced, in order, and that the flattened copy, drawn without a resolver, is at least four times closer
to the drawing of CanvasNet than the original drawn without a resolver.

### Integration: Failed Operation Is ViewerException With Stable Code

**Test**: `CanvasNetViewer_SystemIntegration_FailedOperation_IsViewerExceptionWithStableCode`

Probes a text file and a missing file, and asserts that each failure is a `ViewerException` (an
`InvalidOperationException`) with the documented code (`unsupported-format`, `input-not-found`) and a message.

### Integration: The File-Access Policy Reaches Every Kind Of Reference

**Test**: `CanvasNetViewer_SystemIntegration_FileAccess_DefaultFollowsAndPolicyConfinesEveryReferenceKind`

Writes a picture, a CSV file, and a model library in the parent of the folder of the documents, and in that folder an
SVG file and a Mermaid diagram that show the picture, a chart whose image mark shows it, a chart whose data is the CSV
file, and a model that includes the library. Asserts that under the default policy every picture is drawn and listed
as embedded with no unresolved or access-denied warning, and the chart and the model render; and that under
`UnderRoots(site)` every picture is an `image-access-denied` warning with nothing embedded, while the chart and the
model fail with an `ImageSourceException` whose chain of inner exceptions holds a `FileReferenceException` of kind
`AccessDenied`.

### Integration: Every Options Object Of The Viewer Carries The File-Access Policy

**Test**: `CanvasNetViewer_SourceTree_EveryOpenOptionsCarriesTheFileAccess`

A source-tree guard: scans `src/DemaConsulting.CanvasNet.Viewer` (comment lines ignored) for every creation of
`ScadOpenOptions`, `VegaLiteOpenOptions`, `FolderImageResolverOptions`, and `SourceOptions` and fails when one does not
set `FileAccess` (on its line or in its multi-line initializer), apart from two listed creations that only check
options or measure without reading a file, so that a new code path cannot fall back to the allow-all default silently.
It reads source text only.

### Integration: User Guide Examples Compile

**Test**: `CanvasNetViewer_UserGuideExamples_Compile`

Reads `docs/user_guide/canvas-net-viewer.md` (found by walking up from the test output folder to the folder that holds
`CanvasNet.slnx`) and extracts every fenced C# block with the HTML comment that precedes it. A block preceded by
`<!-- compile -->` is a complete program (top-level statements, then types) and is compiled, never run, with Roslyn:
one compilation for each example, with the implicit usings of a .NET console project added, a console application
output, nullable annotations enabled, and the assemblies of the test host (the platform libraries and the Viewer
package with its dependencies) as references. A block preceded by `<!-- signature -->` is a listing and is not
compiled. Asserts that every C# block of the guide carries one of the two markers (so a new example cannot escape the
check), that at least 15 examples are compiled, that no example has a compile error (a failure names the line of the
example, its first line, and the diagnostics), and that every example that mentions `ViewerException` has both the
`DemaConsulting.CanvasNet.Viewer` and the `DemaConsulting.CanvasNet.Viewer.Imaging` using directives.

### Integration: User Guide Examples Negative Control Reports Errors

**Test**: `CanvasNetViewer_UserGuideExamples_NegativeControl_ReportsErrors`

Runs the same compile step on a program that calls a method that does not exist, on a program that has a using
directive for a namespace that does not exist, and on a correct program. Asserts that the first two are reported with
an error that names the missing member or namespace, and that the correct program has no error, which proves that the
guide test can fail and does not pass on an empty check.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond those explicitly asserted.
An unexpected exception, a wrong source drawing a file, a missing or extra extension, a cached frame, a reference that
the file-access policy denies that is read, an allowed reference that is not read, or a wrong return value constitutes
a failure. Every requirement in `docs/reqstream/canvas-net-viewer.yaml` and
`docs/reqstream/canvas-net-viewer/platform-requirements.yaml` links to at least one passing test listed above or in
the unit verification design.
