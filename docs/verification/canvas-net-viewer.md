# System Verification Design

<!-- cspell:ignore vsdx pptx scad href -->

This document describes the system-level verification strategy for CanvasNetViewer.

## Verification Approach

The CanvasNetViewer system is verified through system-level integration tests that exercise the package as a whole from
the perspective of a consumer, through its public API only (`ImageSources`, `Extensions`, `CreateImageResolver`,
`Render`, and the options and result types). The tests use the real format packages (nothing is mocked): an SVG document
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
  generated in memory by `ViewerFixtures`
- **Isolation**: each test builds its own folder and its own resolver; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces. The live source of the system tests stands in for a camera or
a video frame: it is a plain class in the test project that returns a differently colored picture on every call.

## System-Level Test Scenarios

### Integration: Image Sources List Every Format

**Test**: `CanvasNetViewer_SystemIntegration_ImageSources_ListEveryFormat`

Asserts that the list holds the seven sources in order (`raster`, `svg`, `pdf`, `pptx`, `vsdx`, `scad`, `vega-lite`),
that `Extensions` is exactly the sorted union of their claims (the seventeen extensions), and that no built-in source
claims a URI scheme (so `http` and `https` links stay unresolved). Serves as the shared platform-proof test referenced by
every `CanvasNetViewer-Platform-*` requirement.

### Integration: Svg Embedding Every Format Draws All And Reports Embeds

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingEveryFormat_DrawsAllAndReportsEmbeds`

Writes a raster, a model, a chart, a PDF, and an SVG file in a sub folder that embeds its own raster, renders an SVG
that links all of them with the resolver of `CreateImageResolver`, and asserts that the picture of every placement is
drawn, that nothing is reported, that the five kinds of file appear in the embedded images with their formats, that
five images are embedded at depth 0, and that the one picture of the nested SVG file is at depth 1.

### Integration: Svg Embedding With Query And Fragment Draws The File

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingWithQueryAndFragment_DrawsTheFile`

Writes a raster and a model, renders an SVG whose links are `logo.png?v=2#top` and `part.scad#view`, and asserts that
both are drawn, that nothing is reported, and that the embedded images list the links as written.

### Integration: Svg Embedding Of A Huge Raster In A Tiny Placement Is Refused With A Warning

**Test**: `CanvasNetViewer_SystemIntegration_SvgEmbeddingHugeRasterInTinyPlacement_IsRefusedWithWarning`

Writes a 33-byte PNG that only declares 8192 by 8192 pixels (decoding it would fail and take 256 MiB) and a normal
raster, renders an SVG that places the first in a 5 by 5 rectangle and the second beside it, and asserts that the
render succeeds, the normal picture is drawn, the huge placement shows the background, the one note is the warning
`svg-image-too-large`, and only the normal raster is listed as embedded.

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

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond those explicitly asserted.
An unexpected exception, a wrong source drawing a file, a missing or extra extension, a cached frame, or a wrong return
value constitutes a failure. Every requirement in `docs/reqstream/canvas-net-viewer.yaml` and
`docs/reqstream/canvas-net-viewer/platform-requirements.yaml` links to at least one passing test listed above or in
the unit verification design.
