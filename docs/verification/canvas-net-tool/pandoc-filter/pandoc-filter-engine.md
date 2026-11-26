### PandocFilterEngine

#### Verification Approach

`PandocFilterEngine` is verified with xUnit unit tests in `PandocFilterEngineTests.cs`. The engine is real, and so are
the units it calls (`PandocAst`, `FilterSettings`, `FilterPaths`, `PictureAttributes`, `DependencyHasher`, and
`PictureCache`); only the drawer is stubbed. The helpers in `FilterTestSupport.cs` provide `PandocDoc`, which builds
Pandoc JSON documents and reads the image sources and the `canvasnet-warnings` metadata of an output; `FilterTree`, a
temporary document tree with the sources in `docs/guide`, the cache in `docs/generated/canvasnet`, HTML output to
`out/doc.html`, and its own environment variables; and `StubDrawer`, an `IPictureDrawer` that records each call as
`render NAME` or `flatten NAME` with the arguments it was given, throws a configured failure, or returns a
configured `PictureDrawing` and writes a
fixed picture when it is flattened. `ToolPictureDrawer` is not exercised here; the drawing itself is verified with the
render and flatten commands and through the subsystem and system tests of the filter.

#### Test Environment

N/A - standard test environment. The tests use a temporary folder each and do not change the process environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A picture is drawn once per key and found in the cache on the next run; the elements point at the cached files.
- Diagrams left as written and drawings with nothing to flatten are recorded in markers and warned about on every run.
- Mermaid code blocks become a figure or a paragraph of the documented shape; Vega-Lite blocks and OpenSCAD blocks
  marked `draw` do too, written to the cache with their own extension and with their own classes dropped, while a
  plain `openscad` block is left as written without a file or a draw.
- A Vega-Lite or OpenSCAD block that names a local file stops the run with the documented message before anything is
  written or drawn.
- An equation (a `.mml` picture or a `mathml` block) is drawn at scale 2 unless the author scales or sizes it, and shows
  at its natural width unless the author gives a size, width, or height, on every run.
- One job and four jobs give the same output, warnings, cache files, and reported failure, also after a failure that
  later jobs ran past.
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

**PandocFilterEngine_Run_PlantUmlBlocks_BecomeFigureOrAreLeftAsWritten**: a captioned `plantuml` block with the class
`wide` becomes a `Figure` whose image keeps only `wide` (the class `plantuml` is dropped, `scale` consumed); a legacy
activity block for which the tool reports `plantuml-diagram-unsupported` stays a `CodeBlock` with the one warning
`plantuml block (@startuml): activity-legacy diagrams are not drawn; it is left as written and not drawn`; two `.puml`
files are
written to the cache, one `.unsupported` marker is left, and both blocks were drawn once.

**PandocFilterEngine_Run_MathMlPictureAndBlock_AreDrawnAtTheirNaturalWidth**: Ohm's law as a `.mml` picture with the
attribute `id`, and the quadratic formula as a captioned `mathml` block, are drawn once each with `--scale=2`, and one
`.mml` block source is in the cache; the picture keeps `id` and gets `width` of its probed natural width (`<w>px`), the
block becomes a `Figure` whose image has only `width` of the quadratic formula's natural width, which is wider; a second
run draws nothing and gives the same document.

**PandocFilterEngine_Run_MathMlWithWidth_KeepsTheAuthorsWidth**: one equation shown with `width=50%`, `height=2em`,
`size=400x100`, and `scale=3`, and a text that is not an equation, keep `width=50%`, `height=2em`, nothing, the natural
`width` (with `scale=3`), and nothing (the probe fails, so the attributes stay as written) respectively; the draws are
given `--scale=2` (the width and height pictures share one draw, and the broken file has its own), `--scale=3`, and
`--size=400x100`.

**PandocFilterEngine_Run_MathMlPlainAndWithWidth_ShareOneDrawAndCacheFile**: one equation shown plainly and with
`width=50%` is drawn once with `--scale=2` into one PNG of the cache, which both pictures show; the plain picture gets
the natural `width` and the other keeps `width=50%`, so the added natural width is not part of the cache key.

**PandocFilterEngine_Run_OneAndFourJobs_AreIdentical**: eight diagram images and one code block, each with a note,
give the same document and the same cache files with one job and with four jobs over an empty cache, with nine
warnings.

**PandocFilterEngine_Run_FailureWithOneAndFourJobs_LeavesTheSameCache**: of six diagrams the second fails. With four
jobs its failure is held until the fourth has been drawn (signaled from the drawer, no timing), so later jobs ran
before the failure was known; both runs fail with the same code and message and leave the same cache files, the
picture of the first diagram only, with no temporary file, while only the four-job run drew the fourth diagram.

**PandocFilterEngine_Run_TwoFailures_ReportsTheLowestPosition** (one and four jobs): of seven diagrams, the third and
the sixth fail; the error has the tool's code `mermaid-error` and the message
`drawing 'd2.mmd' failed: d2.mmd is broken (Fix it.)`, and the pictures before it were drawn.

**PandocFilterEngine_Run_PlanningAndDrawingFailures_LowestPositionWins**: with four jobs, a failing drawing before a
picture with `view=top` reports `drawing 'bad.mmd' failed: broken`, and the attribute error before a failing drawing
reports `'good.mmd' has view=top, but view applies to .scad models only` with `pandoc-filter-error`.

**PandocFilterEngine_Run_FailureAfterWarnings_KeepsTheEarlierWarnings** (one and four jobs): of four diagrams that each
report a note, the third fails; `WarningsBeforeFailure` holds the notes of the first two in order, and is empty after
the same engine then runs a document that succeeds.

**PandocFilterEngine_Run_MissingSourcesAndOtherPictures_FollowTheLuaFilter**: a missing model fails with `cannot find
'gone%20model.scad' in the document's resource path`; a document with a missing SVG drawing, a PNG photo, a remote
model, non-ASCII text, and a `python` code block is returned byte for byte unchanged, and the drawer is never called.

**PandocFilterEngine_Run_PicturesItDoesNotDraw_PassThroughUntouched**: a document of pictures that do not exist and
that the engine does not draw (`a.bmp`, `scan.tif`, `favicon.ico`, `photo.avif`, `photo.webp`, `photo.JPG`, `diagram`,
`docs/README`, `report.pdf#page=2`, `chart.vl.json?x`, a remote `.mmd`, and a `data:` URL ending in `.svg`) and of
`bash`, `yaml`, `csharp`, `text`, `Mermaid`, and class-less code blocks is returned byte for byte unchanged, the drawer
is never called, and nothing is written to the cache: everything outside the shim's fast-path lists passes through.

**PandocFilterEngine_Run_OversizedPicture_HintNamesTheScaleAttribute**: a stub drawer raises `size-limit` with the
message `'<name>' is too large; a scale of 0.56 or less fits.` and a command-line hint; with the scale that fits in the
exception's `Data`, a Mermaid block fails with `drawing 'mermaid block (flowchart TD)' failed: ...` ending in `(Set the
block's scale attribute to 0.56 or less, for example {.mermaid scale=0.56}.)` and a `.mmd` picture with `(Set the
picture's scale attribute to 0.56 or less, for example {scale=0.56}.)`; without it the tool's hint is kept.

**PandocFilterEngine_Run_VegaLiteBlocks_BecomeFigureFromAVlJsonFile**: a captioned `vega-lite` block with the
identifier `fig-sales`, a class `wide`, `param-cutoff=3`, and `width=50%` becomes a `Figure` with that identifier whose
picture has `["",["wide"],[["width","50%"]]]`; a `vegalite` block with the identifier `visits` becomes a `Para` whose
picture has `["visits",[],[]]`. Both pictures are `vegalite-<key>.png`, both texts are `.vl.json` files in the cache,
both draws read a `.vl.json` file, exactly one is given `--param=cutoff=3`, and there are no warnings.

**PandocFilterEngine_Run_OpenScadBlocks_DrawnOnlyWithTheDrawClass**: a plain `openscad` block (which includes
`lib.scad`, no error for code) stays a `CodeBlock` with its class and text; a block with the identifier `part`, the
classes `openscad`, `draw`, and `wide`, `view=iso`, and `width=40%` becomes a `Para` whose picture
`scad-<key>.png` has `["part",["wide"],[["width","40%"]]]`. One `.scad` file is in the cache and one draw reads it,
with `--view=iso`, `--size=1600x1200`, `--background=white`, and `--max-triangles=2000000`.

**PandocFilterEngine_Run_BlockNamingAFile_StopsWithAHintToUseAPicture**: a `vega-lite` block `#sales` that reads
`sales.csv` on line 2 fails with `pandoc-filter-error` and
`'vega-lite block #sales' reads the data file 'sales.csv' at line 2, but a code block has no folder to find it in; put
the chart in a .vl.json file beside the files it names and link it as a picture, for example ![Sales](sales.vl.json)`;
a `{.draw .openscad}` block that uses `shapes.scad` fails with `'openscad block (use <shapes.scad>)' uses 'shapes.scad'
at line 1, ...; put the model in a .scad file beside the files it names and link it as a picture, for example
![Part](part.scad){view=iso}`. Nothing is drawn and the cache folder holds no file.

**PandocFilterEngine_Run_OversizedOpenScadBlock_HintNamesBothClasses**: a stub drawer raises `size-limit` with the
scale that fits (0.5); an `openscad` `draw` block with `scale=9` fails with the code `size-limit`, a message that starts
`drawing 'openscad block (cube(10);)' failed: ` and ends `(Set the block's scale attribute to 0.5 or less, for example
{.openscad .draw scale=0.5}.)`, and a `vegalite` block's hint is `{.vegalite scale=0.5}`.
