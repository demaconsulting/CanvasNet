### PandocFilterEngine

#### Verification Approach

`PandocFilterEngine` is verified with xUnit unit tests in `PandocFilterEngineTests.cs`. The engine is real, and so are
the units it calls (`PandocAst`, `FilterSettings`, `FilterPaths`, `PictureAttributes`, `DependencyHasher`, and
`PictureCache`); only the drawer is stubbed. The helpers in `FilterTestSupport.cs` provide `PandocDoc`, which builds
Pandoc JSON documents and reads the image sources and the `canvasnet-warnings` metadata of an output; `FilterTree`, a
temporary document tree with the sources in `docs/guide`, the cache in `docs/generated/canvasnet`, HTML output to
`out/doc.html`, and its own environment variables; and `StubDrawer`, an `IPictureDrawer` that records each call as
`render NAME` or `flatten NAME`, throws a configured failure, or returns a configured `PictureDrawing` and writes a fixed
picture when it is flattened. `ToolPictureDrawer` is not exercised here; the drawing itself is verified with the render
and flatten commands and through the subsystem and system tests of the filter.

#### Test Environment

N/A - standard test environment. The tests use a temporary folder each and do not change the process environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A picture is drawn once per key and found in the cache on the next run; the elements point at the cached files.
- Diagrams left as written and drawings with nothing to flatten are recorded in markers and warned about on every run.
- Mermaid code blocks become a figure or a paragraph of the documented shape.
- One job and four jobs give the same output, warnings, cache files, and reported failure.
- Every failure has the documented message and code, and the one of the lowest position is reported.

#### Test Scenarios

**PandocFilterEngine_Run_CacheHit_DrawsNothing**: a diagram with `theme` and `width` is rendered once, a second run
makes no call and returns the same document, the `src` matches `../docs/generated/canvasnet/flow-<40 hex>.png`, and
only `width` stays on the picture.

**PandocFilterEngine_Run_DuplicateKey_DrawsOnce**: the same diagram twice, with four jobs, is drawn once, both images
point at the same file, and the tool's note and echo are reported once (`flow.mmd: a note`, `flow.mmd: echo`).

**PandocFilterEngine_Run_UnsupportedDiagram_LeftAsWrittenWithMarker**: a `journey` code block reported as
`mermaid-diagram-unsupported` stays in the document, one `.unsupported` marker is written, and its warning ends with
`; it is left as written and not drawn`; the same code about a picture embedded in a flowchart is an ordinary warning
that comes first (images are visited before code blocks); a second run with a fresh drawer gives only the block's
warning from the marker; no `.partial` file remains.

**PandocFilterEngine_Run_RestrictiveDenial_StopsTheBuild**: with `CANVASNET_FILE_ACCESS=roots`, an
`image-access-denied` note fails the run with `file-access-denied` and the message `drawing 'flow.mmd' failed: 'x.png'
is outside the roots (denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS)`, and neither a partial file nor a picture
remains.

**PandocFilterEngine_Run_SvgDrawings_PlainMarkerAndFlattenedCopy**: of two drawings that link a diagram, the one the
tool does not flatten keeps its `src` and its two-line note becomes the single warning `plain.svg: line one line two`;
the other points at `../docs/generated/canvasnet/flat-<40 hex>.svg` and keeps only `width`; a second run makes no call
and returns the same document.

**PandocFilterEngine_Run_MermaidBlocks_BecomeFigureOrParagraph**: a captioned block with the identifier `fig-a` and
the class `wide` becomes a `Figure` with `fig-a`, a caption of a `Plain` holding a `Span` of class `canvasnet-markdown`
with the attribute `markdown` set to the caption, and an image with the class `wide` and only `width`; a block `b`
without a caption becomes a `Para` whose image has the identifier `b` and an empty description; two `.mmd` files are
written to the cache and two pictures are drawn.

**PandocFilterEngine_Run_OneAndFourJobs_AreIdentical**: eight diagram images and one code block, each with a note,
give the same document and the same cache files with one job and with four jobs over an empty cache, with nine
warnings.

**PandocFilterEngine_Run_TwoFailures_ReportsTheLowestPosition** (one and four jobs): of seven diagrams, the third and the
sixth fail; the error has the tool's code `mermaid-error` and the message `drawing 'd2.mmd' failed: d2.mmd is broken
(Fix it.)`, and the pictures before it were drawn.

**PandocFilterEngine_Run_PlanningAndDrawingFailures_LowestPositionWins**: with four jobs, a failing drawing before a
picture with `view=top` reports `drawing 'bad.mmd' failed: broken`, and the attribute error before a failing drawing
reports `'good.mmd' has view=top, but view applies to .scad models only` with `pandoc-filter-error`.

**PandocFilterEngine_Run_FailureAfterWarnings_KeepsTheEarlierWarnings** (one and four jobs): of four diagrams that each
report a note, the third fails; `WarningsBeforeFailure` holds the notes of the first two in order, and is empty after
the same engine then runs a document that succeeds.

**PandocFilterEngine_Run_MissingSourcesAndOtherPictures_FollowTheLuaFilter**: a missing model fails with `cannot find
'gone%20model.scad' in the document's resource path`; a document with a missing SVG drawing, a PNG photo, a remote
model, non-ASCII text, and a `python` code block is returned byte for byte unchanged, and the drawer is never called.
