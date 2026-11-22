## PandocFilter

### Verification Approach

The `PandocFilter` subsystem is verified at two tiers. The unit tests of `PandocAst`, `FilterPaths`,
`PictureAttributes`, `DependencyHasher`, `FilterSettings`, `PictureCache`, `PandocFilterEngine`, and `PandocFilterShim`
(see the unit verification designs under `pandoc-filter/`) verify each unit in isolation; the engine is tested there
with a stub drawer. The subsystem tests in `PandocFilterSubsystemTests.cs` run the real engine with the real
`ToolPictureDrawer`, so every picture is drawn by the `render` and `flatten` commands and the libraries of the tool, in
a temporary document tree built by the `FilterTree` helper of `FilterTestSupport.cs` (a guide folder as the resource
path, the shim's path in `docs/filters`, and the cache in `docs/generated/canvasnet`), with Pandoc documents built by
the `PandocDoc` helper as `pandoc -t json` writes them. Nothing is mocked at the subsystem boundary except the
environment, which is a dictionary of the test. The Lua shim running inside real Pandoc is verified end to end by
`docs/filters/test-canvasnet.ps1` (see *PandocFilterShim*).

### Test Environment

N/A - standard test environment. The fixtures `cube.scad`, `baskets.vl.json`, and `mermaid/flow.mmd` of the shared
test support are copied into the temporary tree.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A model, a chart, a diagram, a composite drawing, and a captioned diagram block are drawn into the cache and the
  document points at them; one job and four jobs give the same document and the same cache files.
- An unchanged document draws nothing on the second run, and a changed include draws its picture again.
- A mistake in a picture stops the run with `pandoc-filter-error` and a message that names the picture; a picture the
  filter does not handle is left alone.
- A restrictive file-access mode denies a picture outside its folders with `file-access-denied`.
- The shim the tool writes speaks a protocol the tool accepts.

### Test Scenarios

**PandocFilter_Run_RealPictures_DrawnTheSameWithOneOrFourJobs**: a document with an OpenSCAD model (`size=32x24`), a
Vega-Lite chart, a Mermaid picture with `theme=forest`, an SVG drawing that links the diagram, and a captioned Mermaid
code block is run with one job and, over an empty cache, with four; both give the same document and cache files and no
warnings, five pictures are in the document, the chart is a PNG and the drawing a flattened SVG in the cache, the model
is 32x24 pixels, and the block became a `Figure`.

**PandocFilter_Run_UnchangedAndChangedSources_DrawOnlyWhatChanged**: a model that includes a second file is drawn once,
a second run draws nothing and gives the same document, and after the included file changes a third run draws once more
and points at a new picture, leaving two pictures in the cache.

**PandocFilter_Run_PlantUmlPictureAndBlock_DrawnAndRedrawnWhenAnIncludeChanges**: with the real drawer, a `.puml`
picture whose participants come from an `!include` file, a captioned `plantuml` block without `@startuml`, and a
`plantuml` legacy activity block are drawn three times (the legacy activity diagram is left as written with the one
warning
`plantuml block (@startuml): ... it is left as written and not drawn`, and the blocks become a `Figure` and stay a
`CodeBlock`); a second run draws nothing and gives the same document; after the included file changes, a third run
draws the picture once more with a new name while the block keeps its picture.

**PandocFilter_Run_ChartFieldWithoutType_GivesNoWarning**: with the real drawer, the chart of issue #165 (a field
without a type, which the tool reports as the informational note `vega-lite-info`) is drawn into the cache and the run
returns no warning, so a build that treats Pandoc warnings as errors does not fail for a note.

**PandocFilter_Run_PictureMistakes_StopTheRunNamingThePicture**: a model with `view=sideways` fails with
`pandoc-filter-error` and a message that starts with `'cube.scad' has view=sideways`; a diagram that is not in the
resource path fails with `cannot find 'gone.mmd' in the document's resource path`; a PNG picture is left unchanged and
nothing is written to the cache.

**PandocFilter_Run_OversizedMermaidBlock_SuggestsTheScaleAttribute**: with the real drawer, a Mermaid block of a
151-node chain (`flowchart LR`) fails with `size-limit`, a message that starts with `drawing 'mermaid block (flowchart
LR)' failed: ` and gives the laid-out size, `larger than the maximum of 8192 pixels per side; a scale of <fit> or less
fits.`, and the hint `(Set the block's scale attribute to <fit> or less, for example {.mermaid scale=<fit>}.)`; the
same block with `scale=<fit>` is drawn.

**PandocFilter_Run_FileAccessVariables_ConfineWhatAPictureReads**: a diagram whose node shows a picture in the folder
above the guide fails with `file-access-denied` and `drawing 'flow.mmd' failed: ...` under `CANVASNET_FILE_ACCESS=roots`
with `CANVASNET_ROOTS` set to the guide folder, and is drawn without warnings when `CANVASNET_ROOTS` is the folder
above.

**PandocFilter_Shim_WrittenShimSpeaksTheProtocolTheToolAccepts**: the shim written by `PandocFilterShim.Write` passes
`--protocol` with its `PROTOCOL`, the tool accepts that protocol, and the protocol before it is a `usage-error` whose
hint is `Write the shim again: dotnet canvasnet pandoc-filter --write-shim <path>` for the path of the shim.
