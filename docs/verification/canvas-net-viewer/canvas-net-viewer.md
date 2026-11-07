## CanvasNetViewer Unit Verification Design

<!-- cspell:ignore vsdx pptx scad href -->

`CanvasNetViewer` is distributed as the separate `DemaConsulting.CanvasNet.Viewer` NuGet package (namespace
`DemaConsulting.CanvasNet.Viewer`); its unit tests live in the sibling `DemaConsulting.CanvasNet.Viewer.Tests` project.
`CanvasNetViewer` is one of the two units of the CanvasNetViewer system (the other is `ViewerException`; the `Imaging`
subsystem is the third part of the system).

### Verification Approach

The unit is verified through its public API with the real format packages, from `CanvasNetViewerTests.cs`: the list of
sources, the creation of resolvers, and rendering a file by path or from a stream with its sizes, notes, text, embedded
images, failures, and options. The files are generated in memory by `ViewerFixtures` (a solid-color picture in any
raster format, a PDF, a model, a chart, and SVG documents) and written to per-test temporary folders
(`ViewerTestFolder`).
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
`CanvasNetViewer_Render_PathSized_ReturnsRequestedSize`,
`CanvasNetViewer_Render_MaxDecodedPixels_IsPassedToTheResolver`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`,
`CanvasNetViewer_Render_ResizedFinalBuffer_IsChargedToTheBudget`,
`CanvasNetViewer_Render_SvgWithEmbeds_SurfacesDiagnosticsEchoAndEmbedded`,
`CanvasNetViewer_Render_SvgEscapingAndSelfEmbedding_AreWarnings`,
`CanvasNetViewer_Render_PathFailures_ThrowSpecificExceptions`,
`CanvasNetViewer_Render_ThrowOnUnsupportedFeature_FailsInsteadOfNote`,
`CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceledException`,
`CanvasNetViewer_Render_PathThroughSymbolicLink_ThrowsSandboxException`

Asserts the natural size and format name, that disposing the result disposes the surface, a vector drawn at the
requested
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

**Tests**: `CanvasNetViewer_Render_Stream_UsesFileNameAndOptionalFolder`,
`CanvasNetViewer_Render_StreamInvalidArguments_Throw`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`

Asserts that a stream is rendered by the extension of its file name, that the files the document names are resolved
only from the folder option, that null arguments and an unknown extension are rejected, and that a width or height
outside 1 to `Surface.MaxDimension` throws `ArgumentOutOfRangeException` without the stream being read.

#### CanvasNetViewer-CanvasNetViewer-LoaderSources: Linked Files Are Drawn By The Loader

**Tests**: `CanvasNetViewer_Render_PdfPageFragment_DrawsTheChosenPage`,
`CanvasNetViewer_Render_SlideAndVsdxPageFragments_DrawTheChosenPage`,
`CanvasNetViewer_Render_PageBeyondTheLast_IsUnsupportedNoteAndNothingDrawn`,
`CanvasNetViewer_Render_InvalidPageValue_IsInvalidFragmentNote`,
`CanvasNetViewer_Render_UnknownParameter_IsWarningAndStillDrawn`,
`CanvasNetViewer_Render_DuplicatePage_LastWinsAndWarnsOnlyWhenDifferent`,
`CanvasNetViewer_Render_QueryAndEncodedFragment_AreHandled`,
`CanvasNetViewer_Render_ScadViewFragment_ChoosesTheViewOrIsInvalid`,
`CanvasNetViewer_Render_SchemeSource_ReceivesRawLinkAndParsedFragment`,
`CanvasNetViewer_Render_ExtraSourceForBuiltInExtension_WinsAndGetsFragment`,
`CanvasNetViewer_Render_NestedSvgWithFragment_UsesTheScopedResolver`,
`CanvasNetViewer_Render_HostileFragment_IsRefusedAndShortInTheMessage`,
`SourceLoader_Load_StrictWithFragmentProblems_FailsOnlyForRefusedLinks`,
`SourceLoader_Load_FragmentProblems_AreNotesOfTheDocument`
(`CanvasNetViewerFragmentTests.cs`)

Writes the two-page PDF, the two-slide presentation and the two-page drawing of the shared fixtures next to an SVG that
links their pages with `#page=N` and `#slide=N`, and asserts by the color of the pixels that the chosen page is the one
drawn (page 1 without a fragment); that a page beyond the last is one unsupported note with the code
`image-page-out-of-range` (the message names the number of pages, the origin the embedding file), nothing is drawn, the
image is not listed as embedded, and the rest of the document is drawn; that `page=0` and `page=abc` are
`image-fragment-invalid`; that an unknown parameter is a warning and the picture is drawn, a duplicate with another
value
a warning with the last value used, and the same value twice silent; that a `?query` and `%`-encoded values work; that
`#view=front` chooses the view of a model and an unknown view is invalid; that a scheme source receives the raw link and
the parsed fragment, and that an extra source for a built-in extension wins and gets the fragment; that a nested SVG
file
draws its pages through the scoped resolver; that a fragment of thousands of characters is refused with a short message;
and that `Strict` fails for the two refused codes only.

#### CanvasNetViewer-CanvasNetViewer-Parameters: Variables And Parameters Set From The Link And The Options

**Tests**: the `CanvasNetViewer_Render_*` tests of `CanvasNetViewerParameterFragmentTests.cs` named in the requirement,
and
`CanvasNetViewer_CreateImageResolver_SourcesDeclareTheirPrefixes`

Each override is proved by drawing the same diagram from files that have the value in their text and comparing the
pixels of the cells. Asserts that
`part.scad#view=front&def.width=20` is the picture of the model written with `width = 20`, with the prefix in any case,
`def.WIDTH` another variable, no `view=`
needed, the last of a repeated name used with the duplicate warning only for different values, the text `A` the same
from `%22A%22`, `A` and `%27A%27`, a boolean
from `true`, and an encoded `%26` and `%3D` kept inside a value; that `def.width=[1,2]`, `def.1x=3` and `def.a%20b=3`
are `image-fragment-invalid` notes with
nothing drawn or listed; that `chart.vl.json#param.cutoff=4` is the picture of the chart whose default is 4 (a narrower
chart), a selection takes the object
`{"y":[2,4]}` encoded or raw, a name the chart does not declare is the warning `vl-param-unknown` that lists the
declared names (also with `Strict`, which keeps it
a warning) while the declared one applies, and `{x`, `[1,` and `%22abc` are `image-fragment-invalid`; that `def.` on a
chart or a PNG, `param.` on a model and a bare `def.`
are the unknown-parameter warning with the picture drawn; that a link in a nested SVG file sets its model and chart
through the scoped resolver (the same pixels as a
nested file that has the values); and that `ViewerRenderOptions.VegaLite` renders the chart of the path with the same
pixels and the narrower size as the file with the
value, takes a selection value, throws `vega-lite-error` listing the declared names for an unknown name (a warning with
`WarnOnUnknownParameters`), throws
`ArgumentOutOfRangeException` for an empty name, and is ignored by a model and by a chart embedded in the rendered file.
Resolving `part.scad#view=front&def.width=20` and
`chart.vl.json#param.cutoff=4` through `CreateImageResolver` draws both with no unknown-parameter warning.

#### CanvasNetViewer-CanvasNetViewer-RenderOptions: Page, Resolution, Background, And Model Options

**Tests**: `ViewerRenderOptions_Defaults_PageOneAndNoDpiBackgroundOrScad`,
`CanvasNetViewer_Render_PageAndDpi_DrawsThePageAtTheResolution`,
`CanvasNetViewer_Render_StreamWithPage_DrawsThePage`,
`CanvasNetViewer_Render_PageBeyondTheLast_ThrowsViewerExceptionPageOutOfRange`,
`CanvasNetViewer_Render_InvalidOptions_ThrowsBeforeReadingTheContent`,
`CanvasNetViewer_Render_FractionalDpiForVsdx_ThrowsViewerExceptionUsageError`,
`CanvasNetViewer_Render_Background_FlattensTransparentAreas`,
`CanvasNetViewer_Render_VegaLiteBackground_DefaultIsTheChartsOwn`,
`CanvasNetViewer_Render_ScadOptions_ChooseViewDefinesAndReportAnchorsEchoAndDetail`,
`CanvasNetViewer_Render_Failures_HaveTheDocumentedShapes`,
`CanvasNetViewer_Render_MatchesTheLoaderOfTheTool`, `CanvasNetViewer_Render_RasterWithBox_IsFittedAndCharged`,
`CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceled` (`CanvasNetViewerRenderOptionsTests.cs`)

Asserts the defaults of the new options; that `Page = 2` with `Dpi = 192` gives the size of page 2 at that resolution
and `ViewerImage.Page` and `PageCount` report the page drawn and the number of pages; that a page beyond the last is a
`ViewerException` with the code `page-out-of-range`; that a page of 0, a `Dpi` of 0, NaN or 2401, a width of 0, and a
bad model option each throw `ArgumentOutOfRangeException` before the stream is read; that a fractional `Dpi` for a Visio
drawing is a `usage-error`; that `Background` flattens a transparent SVG while no background keeps it transparent (and a
chart keeps its own); that a model gets its view and variables, reports its anchors, echo, and notes with `Detail` and
`Location`; the exception shapes (a missing include is a `ViewerException` with `scad-error`, a corrupt raster an
`ImageSourceException` whose inner exception is the codec's); that the pixels equal those of `SourceLoader.Load` for
equal options; and that a raster in a box is fitted and charged.

#### CanvasNetViewer-CanvasNetViewer-ExtraSources: Your Own Sources

**Tests**: `CanvasNetViewer_CreateImageResolver_CreatesNewResolverWithBuiltInAndExtraSources` and the five custom-source
system tests of the `CanvasNetViewerSystemIntegrationTests` class (see _CanvasNetViewer System Verification_,
`canvas-net-viewer.md`)

Asserts that extra sources are placed after the built-in ones, that the later source wins for an extension, that a
scheme-claimed source is handed the link as written, and that nothing is cached.
