## CanvasNetViewer Unit Verification Design

`CanvasNetViewer` is distributed as the separate `DemaConsulting.CanvasNet.Viewer` NuGet package (namespace
`DemaConsulting.CanvasNet.Viewer`); its unit tests live in the sibling `DemaConsulting.CanvasNet.Viewer.Tests` project.
`CanvasNetViewer` is one of the two units of the CanvasNetViewer system (the other is `ViewerException`; the `Imaging`
subsystem is the third part of the system).

### Verification Approach

The unit is verified through its public API with the real format packages, from `CanvasNetViewerTests.cs`: the list of
sources, the creation of resolvers, and rendering a file by path or from a stream with its sizes, notes, text, embedded
images, failures, and options, and, from `CanvasNetViewerPositionTests.cs`, the feature positions of a flowchart written
to a per-test `TempDirectory`. The files are generated in memory by `ViewerFixtures` (a solid-color picture in any
raster format, a PDF, a model, a chart, and SVG documents) and written to per-test temporary folders
(`ViewerTestFolder`).
The custom sources used for the extra-source requirement are real implementations of `IImageSource` that record every
request. Tests assert on picture sizes and pixels, the notes and their codes, the exception types (with the inner
exception), and the embedded image list - never on "no exception thrown" alone.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None; the format packages are real
- **Files**: per-test temporary folders, deleted by the test; the file-symbolic-link scenario skips itself where file
  links cannot be created (Windows without the privilege), while the directory-link scenario uses a junction on Windows
  and never skips
- **Isolation**: each test builds its own folder and resolver

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong size, a raster that is not fitted into the box, a link that the file-access policy denies that is
drawn, an allowed link that is not drawn, a missing
embedded image record, a wrong exception type, or a disposed surface that is still usable constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetViewer-CanvasNetViewer-ImageSources: Fixed List Of Sources

**Tests**: `CanvasNetViewer_ImageSources_AreFixedAndExtensionsAreSortedUnion`,
`CanvasNetViewer_ImageSources_ExtensionsAndSchemesAreReadOnly`, `CanvasNetViewer_Collections_AreReadOnly`

Asserts that the list is the same read-only list on every call (not an `IList` that can be modified), has ten sources,
and that `Extensions` is sorted, has no duplicate, has `.vl.json` but not `.json`, and equals the union of the claims of
the sources. A theory over the ten built-in sources asserts that each source's `Extensions` and `Schemes` are
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
`CanvasNetViewer_Render_MermaidPictureEnlargingTheLayout_IsChargedAtTheDrawnSize`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`,
`CanvasNetViewer_Render_ResizedFinalBuffer_IsChargedToTheBudget`,
`CanvasNetViewer_Render_SvgWithEmbeds_SurfacesDiagnosticsEchoAndEmbedded`,
`CanvasNetViewer_Render_MermaidNodePictures_AreListedOnceEach`,
`CanvasNetViewer_Render_SvgEmbeddingMermaidWithPicture_ListsThePictureOnce`,
`CanvasNetViewer_Render_SvgEscapingAndSelfEmbedding_AreWarnings`,
`CanvasNetViewer_Render_PathFailures_ThrowSpecificExceptions`,
`CanvasNetViewer_Render_ThrowOnUnsupportedFeature_FailsInsteadOfNote`,
`CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceledException`

Asserts the natural size and format name, that disposing the result disposes the surface, a vector drawn at the
requested size and a raster fitted into the box (or scaled for one dimension), that `MaxDecodedPixels` reaches the
resolver (null keeps the default, a cap that fits renders, a smaller cap is a `budget-exceeded` `ViewerException`, a
negative one is rejected), that a Mermaid diagram whose picture node without `w` and `h` (a 400 by 300 PNG) enlarges the
layout is held to `MaxDecodedPixels` at its drawn size (300 000 pixels, enough for both picture loads and the probed
size, fails with `budget-exceeded` whose inner `ImageSourceBudgetException` has the requested pixels of the drawn
surface; 400 000 draws it larger than the probed size, about 416 by 316), that a width or height of 0, -1, or
`Surface.MaxDimension + 1` throws `ArgumentOutOfRangeException` (parameter `options`, message naming
`ViewerRenderOptions.Width` or `Height`) before the file is read while 1 and `Surface.MaxDimension` pass the validation,
that the final buffer of a resized render is charged to the budget in addition to the surface of the source (a 10 by 10
picture resized to 1 by 1 needs 101 pixels: a cap of 100 fails with `budget-exceeded` around an
`ImageSourceBudgetException`, 101 returns the 1 by 1 picture), the notes, printed text, and embedded images of an SVG
that embeds files, that a Mermaid diagram whose two nodes show a PNG and an SVG file lists each picture once at depth 0
(although the diagram measures each picture before drawing it) and that an SVG document that shows such a diagram lists
its picture once at depth 1 before the diagram (and, under `UnderRoots(site)`, that a link to the parent folder is the
`image-access-denied` warning and a self-embedding SVG the cycle warning), the `ViewerException` codes for a missing
file (`input-not-found`), an unknown extension (`unsupported-format`), and a corrupt file (`corrupt-input`, with the
original failure inside), the `ArgumentNullException` of a null path (a folder option that holds the file works), the
strict switch (`unsupported-feature` around the library exception), and a canceled token.

#### CanvasNetViewer-CanvasNetViewer-FileAccess: The File-Access Policy Of A Render

**Tests**: `CanvasNetViewer_Render_SvgParentFolderLink_IsDrawnByDefault`,
`CanvasNetViewer_Render_FileOutsideTheFolderOption_IsRendered`,
`CanvasNetViewer_Render_PathThroughSymbolicLink_IsRenderedWhateverThePolicy`,
`CanvasNetViewer_Render_LinkThroughDirectoryLink_NoSymlinks_IsAccessDenied`,
`CanvasNetViewer_Render_NestedModelInclude_IsCheckedByTheRenderPolicy`,
`CanvasNetViewer_Render_NestedModelInclude_NoSymlinks_ReadsPlainIncludeAndDeniesLink` (Windows, Linux, macOS),
`CanvasNetViewer_Render_NestedChartData_IsCheckedByTheRenderPolicy`,
`ViewerRenderOptions_FileAccess_DefaultsToAllowAllAndRejectsNull`

Asserts that under the default policy an SVG file embeds a picture of its parent folder; that the rendered file itself
is never judged by the policy: a file outside the `Folder` option is rendered even under a policy that denies
everything, and a file reached through a file symbolic link (skipped where file links cannot be created) is rendered by
default and under `NoSymlinks`; that under `NoSymlinks` a link of the rendered file through a directory link (a junction
on Windows) is the one `image-access-denied` warning with nothing drawn, while the default policy draws it; that the
policy of the render reaches a model and a chart that an SVG file embeds: under `UnderRoots(site)` an include or a data
file of the parent folder fails the render with a `ViewerException` with the code `file-access-denied` (naming the
embedding and the embedded file) whose chain of inner exceptions holds a `FileReferenceException` of kind
`AccessDenied`, while the default draws both; that under `NoSymlinks` a model an SVG
file embeds reads a plain include of its folder (the nested resolver captures the outer origin's folder before its
open) while an include through a directory link (a junction on Windows) fails the render with the denial; and that
`ViewerRenderOptions.FileAccess`
is `AllowAll` by default, keeps another policy, and refuses `null`.

#### CanvasNetViewer-CanvasNetViewer-FileAccessDenied: A Denied Link Under The Strict Option

**Tests**: `CanvasNetViewer_Render_StrictDeniedLink_ThrowsFileAccessDenied`

Asserts that, under `UnderRoots(site)`, a link to the parent folder is a `ViewerException` with the code
`file-access-denied` naming `../secret.png` when `ThrowOnUnsupportedFeature` is set, and the one `image-access-denied`
warning without it.

#### CanvasNetViewer-CanvasNetViewer-RenderStream: Render From A Stream

**Tests**: `CanvasNetViewer_Render_Stream_UsesFileNameAndOptionalFolder`,
`CanvasNetViewer_Render_StreamInvalidArguments_Throw`,
`CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading`, `CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted`

Asserts that a stream is rendered by the extension of its file name, that the files the document names are resolved
relative to the folder option, that null arguments, a closed stream (`ArgumentException` naming `content`), and an
unknown extension (`unsupported-format`) are rejected, and that a width or height outside 1 to `Surface.MaxDimension`
throws `ArgumentOutOfRangeException` without the stream being read.

#### CanvasNetViewer-CanvasNetViewer-RenderErrors: Every Failure Of The Input Is A ViewerException

**Tests**: `CanvasNetViewer_Render_MermaidSyntaxError_ThrowsViewerExceptionWithLocationAndDetailCode`,
`CanvasNetViewer_Render_PlantUmlSyntaxError_ThrowsViewerExceptionWithLocationAndDetailCode`,
`CanvasNetViewer_Render_MathMlSyntaxError_ThrowsViewerExceptionWithLocationAndDetailCode`,
`CanvasNetViewer_Render_ScadSyntaxError_ThrowsViewerExceptionWithLocation`,
`CanvasNetViewer_Render_ChartErrors_ThrowVegaLiteErrorWithLocation`,
`CanvasNetViewer_Render_MalformedSvg_ThrowsCorruptInputWithXmlLineAndColumn`,
`CanvasNetViewer_Render_DeniedReferences_ThrowFileAccessDenied`,
`CanvasNetViewer_Render_InputFailures_ThrowViewerExceptionWithCode`,
`CanvasNetViewer_Render_Failures_HaveNoCommandLineWording`,
`CanvasNetViewer_Render_ExtraSourceFailure_IsMappedOrOperationFailed`,
`CanvasNetViewer_Render_EmbeddedSourceError_KeepsCodeLocationAndDetailCode`,
`CanvasNetViewer_Render_PathFailures_ThrowSpecificExceptions`,
`CanvasNetViewer_Render_StreamInvalidArguments_Throw`,
`CanvasNetViewer_Render_StreamReadFailures_AreInputUnreadable` (`CanvasNetViewerTests.cs`),
`CanvasNetViewer_Render_Failures_HaveTheDocumentedShapes`,
`CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceled` (`CanvasNetViewerRenderOptionsTests.cs`)

Asserts the rows of issue #166 through `Render(stream, fileName)` and `Render(path)`: a Mermaid syntax error is
`mermaid-error` with the detail code `mermaid-syntax-error`, the location `diagram.mmd` line 2 column 5, and the
`MermaidSyntaxException` inside; a PlantUML message without a target is `plantuml-error` with `plantuml-syntax-error` at
line 3; a MathML element closed by the wrong end tag is `mathml-error` with `mathml-syntax-error` at `eq.mml` line 2
with a column and the `MathMlSyntaxException` inside, and an empty `.mml` stream is the same error located at line 1,
column 1; an OpenSCAD syntax error is `scad-error` with `scad-syntax-error` at line 2 column 1; a truncated chart is
`vega-lite-error` located in its text and not a depth error, and an invalid `type` is located at line 4 and the pointer
`/encoding/x/type` (neither has a detail code: the library gives a chart error none); malformed SVG markup
is `corrupt-input` with the line and column of the XML error and no detail code; under `UnderRoots(site)` a chart's
`data.url`, a PlantUML `!include`, and a model's `include` of the parent folder are each `file-access-denied`, located
(the pointer `/data/url`, line 2, line 1) and with the denial in the chain of inner exceptions; a missing file is
`input-not-found`, an unknown extension `unsupported-format`, a picture over `MaxDecodedPixels` `budget-exceeded` (with
the `ImageSourceBudgetException` inside and a hint that names `MaxDecodedPixels`), and a stream longer than 16 MiB
`input-unreadable`, none of them located; a caller's stream whose read throws an `IOException` or a
`NotSupportedException` is `input-unreadable` with that exception inside, while a name no source claims is
`unsupported-format` before the stream is read; the strict failures of a model, a chart, a Mermaid and a PlantUML
diagram, an SVG link, and a position selector that matched nothing (`positions-unmatched`), the triangle limit of a
model, and an SVG larger than the largest image name no option of the command-line tool (no `--` option) in the
message or the hint; a model whose include is missing is `scad-error`, a corrupt raster `corrupt-input` with the
codec's `InvalidDataException` inside, and a truncated chart `vega-lite-error` with the `VegaLiteSpecException`
inside; a canceled token stops the render of a document with `OperationCanceledException`; an extra source that
throws `InvalidDataException` is
`corrupt-input` and one that throws any other exception `operation-failed`, each with the original inside; and a
Mermaid diagram embedded in an SVG file with a syntax error keeps its code, location, and detail code with a message
that names both files.

#### CanvasNetViewer-CanvasNetViewer-LoaderSources: Linked Files Are Drawn By The Loader

**Tests**: `CanvasNetViewer_Render_PdfPageFragment_DrawsTheChosenPage`,
`CanvasNetViewer_Render_SlideAndVsdxPageFragments_DrawTheChosenPage`,
`CanvasNetViewer_Render_PageBeyondTheLast_IsUnsupportedNoteAndNothingDrawn`,
`CanvasNetViewer_Render_InvalidPageValue_IsInvalidFragmentNote`,
`CanvasNetViewer_Render_UnknownParameter_IsWarningAndStillDrawn`,
`CanvasNetViewer_Render_DuplicatePage_LastWinsAndWarnsOnlyWhenDifferent`,
`CanvasNetViewer_Render_QueryAndEncodedFragment_AreHandled`,
`CanvasNetViewer_Render_ScadViewFragment_ChoosesTheViewOrIsInvalid`,
`CanvasNetViewer_Render_ScadAngleFragments_TurnTheViewOrAreInvalid`,
`CanvasNetViewer_Render_ScadProjectionFragment_ChoosesTheProjectionOrIsInvalid`,
`CanvasNetViewer_Render_MathMlDisplayFragment_ChoosesTheLayout`,
`CanvasNetViewer_Render_SchemeSource_ReceivesRawLinkAndParsedFragment`,
`CanvasNetViewer_Render_ExtraSourceForBuiltInExtension_WinsAndGetsFragment`,
`CanvasNetViewer_Render_NestedSvgWithFragment_UsesTheScopedResolver`,
`CanvasNetViewer_Render_HostileFragment_IsRefusedAndShortInTheMessage`,
`SourceLoader_Load_StrictWithFragmentProblems_FailsOnlyForRefusedLinks`,
`SourceLoader_Load_FragmentProblems_AreNotesOfTheDocument`
(`CanvasNetViewerFragmentTests.cs`),
`InputSelection_ApplyTo_Diagram_FragmentThemeReplacesTheCommandTheme`,
`SourceLoader_ProbeInputReference_InvalidValue_IsImageFragmentInvalid` (`InputSelectionTests.cs`),
`CanvasNetViewer_Render_ChartFieldWithoutType_ReportsInfoNoteInStrictMode` (`CanvasNetViewerTests.cs`)

Writes the two-page PDF, the two-slide presentation and the two-page drawing of the shared fixtures next to an SVG that
links their pages with `#page=N` and `#slide=N`, and asserts by the color of the pixels that the chosen page is the one
drawn (page 1 without a fragment); that a page beyond the last is one unsupported note with the code
`image-page-out-of-range` (the message names the number of pages, the origin the embedding file), nothing is drawn, the
image is not listed as embedded, and the rest of the document is drawn; that `page=0` and `page=abc` are
`image-fragment-invalid`; that an unknown parameter is a warning and the picture is drawn, a duplicate with another
value
a warning with the last value used, and the same value twice silent; that a `?query` and `%`-encoded values work; that
`#view=front` chooses the view of a model and an unknown view is invalid; that, for a bar `cube([30, 10, 10])`,
`#view=front&azimuth=90`, `#view=right`, and `#azimuth=90&elevation=0` draw the same pixels, which differ from
`#view=front`, with no notes, and `#azimuth=abc` and `#elevation=95` are two `image-fragment-invalid` notes naming their
values with nothing drawn; that, for the same bar, no fragment, `#projection=ortho`, and `#projection=Orthographic` draw
the same pixels, `#projection=perspective` and `#projection=PERSPECTIVE` draw the same other pixels, with no notes, and
`#projection=fisheye` and an empty `#projection=` are two `image-fragment-invalid` notes (the first naming its value,
the second listing the projections) with nothing drawn; that, for the quadratic formula (which says `display="block"`),
`#display=BLOCK` draws the pixels of the link without a fragment and `#display=inline` the pixels of a copy that
says `display="inline"`, which differ from them, with no notes; that a scheme source receives the raw link and
the parsed fragment, and that an extra source for a built-in extension wins and gets the fragment; that a nested SVG
file
draws its pages through the scoped resolver; that a fragment of thousands of characters is refused with a short message;
and that `Strict` fails for the two refused codes only. Through the built-in Mermaid source (which reads the fragment of
an input as it reads that of a link), `#theme=Dark&page=2` of a diagram is read as the canonical theme `dark` and draws
in it, `page` being an unknown parameter of a diagram (a warning), and `#theme=midnight` is `image-fragment-invalid`.
The chart of issue #165 (a field without a type), rendered from a stream with `ThrowOnUnsupportedFeature`, is drawn and
carries one note of the severity `Info` (source `vega-lite`, code `vega-lite-info`, detail `vl-type-inferred`, pointer
`/encoding/x`) that names the field and the inferred type `nominal`.

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
from `true`, and an encoded `%26` and `%3D` kept inside a value; that `def.width=[1,2` (a vector cut short), `def.1x=3`
and `def.a%20b=3`
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
`CanvasNetViewer_Render_PageAndDpi_DrawsThePageAtTheResolution`, `CanvasNetViewer_Render_StreamWithPage_DrawsThePage`,
`CanvasNetViewer_Render_PageBeyondTheLast_ThrowsViewerExceptionPageOutOfRange`,
`CanvasNetViewer_Render_InvalidOptions_ThrowsBeforeReadingTheContent`,
`CanvasNetViewer_Render_FractionalDpiForVsdx_ThrowsViewerExceptionUsageError`,
`CanvasNetViewer_Render_Background_FlattensTransparentAreas`,
`CanvasNetViewer_Render_DefaultBackground_IsOpaqueWhiteForVectorKinds`,
`CanvasNetViewer_Render_TransparentBackground_KeepsAlpha`,
`CanvasNetViewer_Render_DefaultBackground_LeavesRasterUnchanged`,
`CanvasNetViewer_Render_EmbeddedPictures_StayTransparent`, `CanvasNetViewer_Render_VectorInBox_MarginIsWhiteByDefault`,
`CanvasNetViewer_Render_VegaLiteBackground_DefaultIsTheChartsOwn`,
`CanvasNetViewer_Render_ScadOptions_ChooseViewDefinesAndReportAnchorsEchoAndDetail`,
`CanvasNetViewer_Render_MatchesTheLoaderOfTheTool`, `CanvasNetViewer_Render_RasterWithBox_IsFittedAndCharged`
(`CanvasNetViewerRenderOptionsTests.cs`), `CanvasNetViewer_Render_UndrawnLink_IsNoteWithoutStrict`,
`CanvasNetViewer_Render_UndrawnLinkWithStrict_ThrowsUnsupportedFeature`,
`CanvasNetViewer_Render_StrictWithOnlyWarnings_Succeeds`,
`CanvasNetViewer_Render_UndrawnLinkInNestedSvgWithStrict_NamesTheNestedFile`,
`SourceLoader_Load_UndrawnLinkWithStrict_StillThrowsUnsupportedFeature`,
`CanvasNetViewer_Render_FitIntoBox_MarginHasTheBackground`,
`CanvasNetViewer_Render_PdfFitIntoBox_MarginHasTheBackground`,
`CanvasNetViewer_FitIntoBox_AnchorsFollowTheScaleAndOffset`,
`CanvasNetViewer_Render_ScadWithBox_AnchorsAreInsideTheReturnedPicture` (`CanvasNetViewerStrictAndFitTests.cs`)

Asserts the defaults of the new options; that `Page = 2` with `Dpi = 192` gives the size of page 2 at that resolution
and `ViewerImage.Page` and `PageCount` report the page drawn and the number of pages; that a page beyond the last is a
`ViewerException` with the code `page-out-of-range`; that a page of 0, a `Dpi` of 0, NaN or 2401, a width of 0, and a
bad model option each throw `ArgumentOutOfRangeException` before the stream is read; that a fractional `Dpi` for a Visio
drawing is a `usage-error`; that `Background` flattens a transparent PDF page and SVG file; that without a background an
SVG file, a PDF page, a Mermaid diagram, a PlantUML diagram, a MathML equation, and a model come back over opaque white,
as the tool draws them, while an explicit fully transparent background keeps them transparent (and a chart keeps its
own); that a raster with transparency keeps its alpha by default, also with a transparent margin in a box, while a
vector page in a box gets a white margin; that a model, a diagram, and an equation an SVG file embeds are drawn
transparent, so only the rendered file gets the background; that a model gets its view and variables, reports its
anchors, echo, and notes with `Detail` and `Location`; that the pixels, format, and notes equal those of
`SourceLoader.Load` with the default options of both, for every kind including the diagrams, which proves the shared
default background; and that a raster in a box is fitted and charged. A link of an SVG file that is not drawn (a page
that does not exist, an invalid fragment, a missing file, a picture over the budget) is a note with its code without
`ThrowOnUnsupportedFeature` and an `unsupported-feature` error naming the file and the link with it, by path and by
stream, also inside a nested SVG file (the message names the nested file) and through `SourceLoader` in strict mode;
strict mode with only warnings (an unknown fragment parameter) succeeds and draws both links. A picture or a PDF page
fitted into a box has the chosen `Background` in its margin (transparent for a raster without one), a position follows
the fit (a resize then a pad, the mapped point where the same pixel lands, a lost one stays lost), and a model rendered
into a box of another shape reports its anchor inside the returned picture. The tool side of the shared default is
verified by `CanvasNetTool_Defaults_ModelAndDiagramBackground_WhiteUnlessTransparent` (`CanvasNetTool-System-Defaults`).

#### CanvasNetViewer-CanvasNetViewer-ResourceLimits: Limits Of Charts, Diagrams, And Equations Set By The Host

**Tests**: `CanvasNetViewer_Render_LimitsNotSet_AreTheLibraryDefaults`,
`CanvasNetViewer_Render_LowerVegaLiteLimits_ThrowBudgetExceeded`,
`CanvasNetViewer_Render_RaisedVegaLiteLimits_AdmitALargerChart`,
`CanvasNetViewer_Render_LowerMermaidLimits_ThrowBudgetExceeded`,
`CanvasNetViewer_Render_RaisedMermaidLimits_AdmitALargerDiagram`,
`CanvasNetViewer_Render_LowerPlantUmlLimits_ThrowBudgetExceeded`,
`CanvasNetViewer_Render_RaisedPlantUmlLimits_AdmitALargerDiagram`,
`CanvasNetViewer_Render_LowerMathMlLimits_ThrowBudgetExceeded`,
`CanvasNetViewer_Render_RaisedMathMlLimits_AdmitALargerEquation`,
`CanvasNetViewer_Render_Limits_ApplyToEmbeddedChartsAndDiagrams` (`CanvasNetViewerRenderOptionsTests.cs`)

Asserts that the four limits are null by default and that a render with them unset draws the same pixels as one with the
default limits of each library, for a chart, a Mermaid diagram, a PlantUML diagram, and a MathML equation. For each
format, a source that renders by default fails under a lower limit (`MaxDataRows` 2 for a chart of three rows,
`MaxNodes` 2 for a flowchart of three nodes, `MaxNodes` 1 for a sequence diagram of two participants) with a
`ViewerException` of the code `budget-exceeded` whose message names the limit (with the detail code
`mermaid-limit-exceeded` or `plantuml-limit-exceeded` for a diagram), and a source that the default refuses (a chart row
of 1,100 fields over `MaxFieldCount` 1,024, a Mermaid text over `MaxTextSize` 50,000, a PlantUML diagram of 70 includes
over `MaxIncludes` 64) is refused without the option (with the code and the limit named) and drawn with a raised limit,
which proves that the probe of the file uses the limit as well as its render. For an equation, the quadratic formula
fails under `MaxElements` 3 with `budget-exceeded`, the detail code `mathml-limit-exceeded`, and the limit named, and an
equation nested 300 rows deep, refused by default with `MaxNestingDepth` named, is drawn under `MaxNestingDepth` 400.
For embedded files, an SVG file that embeds the three-node flowchart renders by default and fails with `budget-exceeded`
under `MaxNodes` 2 with a message that names both files and the limit, and an SVG file that embeds the wide chart fails
by default and is drawn without a warning under a raised `MaxFieldCount`. The bounds are proved by the specific limit
error, never by time.

#### CanvasNetViewer-CanvasNetViewer-ExtraSources: Your Own Sources

**Tests**: `CanvasNetViewer_CreateImageResolver_CreatesNewResolverWithBuiltInAndExtraSources` and the five custom-source
system tests of the `CanvasNetViewerSystemIntegrationTests` class (see *CanvasNetViewer System Verification*,
`canvas-net-viewer.md`)

Asserts that extra sources are placed after the built-in ones, that the later source wins for an extension, that a
scheme-claimed source is handed the link as written, and that nothing is cached.

#### CanvasNetViewer-CanvasNetViewer-Positions: Positions Into The Requested Box

**Tests**: `CanvasNetViewer_Render_PositionsIntoABox_FollowTheFit`,
`CanvasNetViewer_Render_UnmatchedSelector_NoteOrStrictFailure` (`CanvasNetViewerPositionTests.cs`)

Renders a two-node flowchart by path with the selector `node` at its natural size and into a box twice as large, and
asserts that the boxed render reports the nodes `A` and `B`, not truncated, each pixel within one pixel of twice its
natural pixel (the fit of the box). Renders it with the selector `node:Zed` and asserts that the image has no positions
and a note with the code `positions-unmatched`, and that with `ThrowOnUnsupportedFeature` the render throws a
`ViewerException` with that code.

#### CanvasNetViewer-CanvasNetViewer-BindingPositions: Positions Of Embedded Pictures For Bindings

**Tests**: `CanvasNetViewer_CreateImageResolver_ResolveWithPositions_ReportsByKind`,
`CanvasNetViewer_Render_StrictUnresolvedBinding_Fails` (`CanvasNetViewerBindingTests.cs`)

The resolver of `CreateImageResolver` asked through `ResolveWithPositions` reports the nodes `A` and `B` of a flowchart,
the two bars of a chart, the anchor `corner` of a model, and no positions for a PNG file. An SVG file whose binding
matches nothing renders with the warning `svg-binding-unmatched`, and with `ThrowOnUnsupportedFeature` fails with
`svg-binding-failed`.
