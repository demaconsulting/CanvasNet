## CanvasNetViewer Unit Verification Design

<!-- cspell:ignore vsdx pptx scad href -->

`CanvasNetViewer` is distributed as the separate `DemaConsulting.CanvasNet.Viewer` NuGet package (namespace
`DemaConsulting.CanvasNet.Viewer`); its unit tests live in the sibling `DemaConsulting.CanvasNet.Viewer.Tests` project.
`CanvasNetViewer` is the sole unit of the CanvasNetViewer system.

### Verification Approach

The unit is verified through its public API with the real format packages, from `CanvasNetViewerTests.cs`: the list of
sources, the creation of resolvers, and rendering a file by path or from a stream with its sizes, notes, text, embedded
images, failures, and options. The files are generated in memory by `ViewerFixtures` (a solid-color picture in any
raster format, a PDF, a model, a chart, and SVG documents) and written to per-test temporary folders (`ViewerTestFolder`).
The custom sources used for the extra-source requirement are real implementations of `IImageSource` that record every
request. Tests assert on picture sizes and pixels, the notes and their codes, the exception types (with the inner
exception), and the embedded image list - never on "no exception thrown" alone.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None; the format packages are real
- **Files**: per-test temporary folders, deleted by the test; one scenario creates a real symbolic link and skips itself
  where the host cannot
- **Isolation**: each test builds its own folder and resolver

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong size, a raster that is not fitted into the box, a link outside the folder that is drawn, a missing
embedded image record, a wrong exception type, or a disposed surface that is still usable constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetViewer-CanvasNetViewer-ImageSources: Fixed List Of Sources

**Tests**: `CanvasNetViewer_ImageSources_AreFixedAndExtensionsAreSortedUnion`,
`CanvasNetViewer_ImageSources_ExtensionsAndSchemesAreReadOnly`, `CanvasNetViewer_Collections_AreReadOnly`

Asserts that the list is the same read-only list on every call (not an `IList` that can be modified), has seven sources,
and that `Extensions` is sorted, has no duplicate, has `.vl.json` but not `.json`, and equals the union of the claims of
the sources. A theory over the seven built-in sources asserts that each source's `Extensions` and `Schemes` are
read-only: not a `string[]` or `List<string>`, and when cast to `IList<string>` they report read-only and throw
`NotSupportedException` on `Add`. A further scenario asserts the same for `ImageSources`, `Extensions`, the extension
and scheme lists of a created resolver, and the `Diagnostics`, `Echo`, and `Embedded` lists of a rendered `ViewerImage`.

#### CanvasNetViewer-CanvasNetViewer-CreateImageResolver: New Resolver Per Call

**Tests**: `CanvasNetViewer_CreateImageResolver_CreatesNewResolverWithBuiltInAndExtraSources`

Asserts that every call returns a new resolver, that its extensions are those of the built-in sources, that extra
sources are added after them (their extensions and schemes appear), and that a null, empty or white-space folder and
null extra sources are rejected.

#### CanvasNetViewer-CanvasNetViewer-RenderPath: Render By Path

**Tests**: `CanvasNetViewer_Render_PathNatural_ReturnsNaturalSizeAndFormat`,
`CanvasNetViewer_Render_PathSized_ReturnsRequestedSize`, `CanvasNetViewer_Render_MaxDecodedPixels_IsPassedToTheResolver`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`,
`CanvasNetViewer_Render_ResizedFinalBuffer_IsChargedToTheBudget`,
`CanvasNetViewer_Render_SvgWithEmbeds_SurfacesDiagnosticsEchoAndEmbedded`,
`CanvasNetViewer_Render_SvgEscapingAndSelfEmbedding_AreWarnings`, `CanvasNetViewer_Render_PathFailures_ThrowSpecificExceptions`,
`CanvasNetViewer_Render_ThrowOnUnsupportedFeature_FailsInsteadOfNote`, `CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceledException`,
`CanvasNetViewer_Render_PathThroughSymbolicLink_ThrowsSandboxException`

Asserts the natural size and format name, that disposing the result disposes the surface, a vector drawn at the requested
size and a raster fitted into the box (or scaled for one dimension), that `MaxDecodedPixels` reaches the resolver (null
keeps the default, a cap that fits renders, a smaller cap throws `ImageSourceBudgetException`, a negative one is
rejected), that a width or height of 0, -1, or `Surface.MaxDimension + 1` throws `ArgumentOutOfRangeException`
(parameter `options`, message naming `ViewerRenderOptions.Width` or `Height`) before the file is read while 1 and
`Surface.MaxDimension` pass the validation, that the final buffer of a resized render is charged to the budget in
addition to the surface of the source (a 10 by 10 picture resized to 1 by 1 needs 101 pixels: a cap of 100 throws
`ImageSourceBudgetException`, 101 returns the 1 by 1 picture), the notes, printed text, and embedded images of an
SVG that embeds files (and that an escaping link and a self-embedding SVG are warnings), the exceptions for a missing
file, an unknown extension, a corrupt file (with the original failure inside), a file outside the folder option, and a
null path, the strict switch, a canceled token, and a symbolic link.

#### CanvasNetViewer-CanvasNetViewer-RenderStream: Render From A Stream

**Tests**: `CanvasNetViewer_Render_Stream_UsesFileNameAndOptionalFolder`, `CanvasNetViewer_Render_StreamInvalidArguments_Throw`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`

Asserts that a stream is rendered by the extension of its file name, that the files the document names are resolved
only from the folder option, that null arguments and an unknown extension are rejected, and that a width or height
outside 1 to `Surface.MaxDimension` throws `ArgumentOutOfRangeException` without the stream being read.

#### CanvasNetViewer-CanvasNetViewer-ExtraSources: Your Own Sources

**Tests**: `CanvasNetViewer_CreateImageResolver_CreatesNewResolverWithBuiltInAndExtraSources` and the five custom-source
system tests of the `CanvasNetViewerSystemIntegrationTests` class (see _CanvasNetViewer System Verification_,
`canvas-net-viewer.md`)

Asserts that extra sources are placed after the built-in ones, that the later source wins for an extension, that a
scheme-claimed source is handed the link as written, and that nothing is cached.
