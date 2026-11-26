### PictureAttributes

#### Verification Approach

`PictureAttributes` is verified with xUnit unit tests in `PictureAttributesTests.cs`. The tests call the internal static
methods directly (`KindOf`, `IsSvg`, `Build`, `NaturalWidthNeeded`, `BuildSvg`, `SplitCaption`, `BlockName`, and
`FormatNumber`, `BlockOf`) with
attribute lists written in the test; nothing is stubbed, no file is read, and no tool is run. Refusals are asserted as
the `ToolException` the method throws, with its code and its exact message.

#### Test Environment

N/A - standard test environment. The unit is pure computation and needs no files, processes, or console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every handled name end gives its kind, and a URL or another name end gives none.
- Every refused attribute fails with `pandoc-filter-error`, no hint, and the exact message of the Lua filter.
- The defaults, the normalized values, the byte order of the options, and the kept attributes match the Lua filter.
- Numbers are written exactly as C's `%.15g`.
- A `vega-lite` or `vegalite` block is a chart, an `openscad` block is a model only together with `draw`, and a
  block is named by the class the author wrote.
- An equation (`.mml` or a `mathml` block) is drawn at scale 2 unless a size or scale is given, refuses the attributes
  of the other kinds, and wants its natural display width only without a size, width, or height.

#### Test Scenarios

**PictureAttributes_KindOf_FileNameEnd_GivesTheKind**: each handled name end gives its kind in any letter case
(`chart.VL.JSON`, `chart.vl`, `C:/models/part.scad` with a drive letter, `.vsdx`, `.pptx`, `.pdf`, `.mmd`, `.mermaid`,
`.puml`, `.PlantUML`, `.pu`), while `https://example.com/part.scad`, `photo.png`, and the include fragment `part.iuml`
give none.

**PictureAttributes_IsSvg_LocalSvgOnly**: `a/Drawing.SVG` is a drawing; a URL ending in `.svg` and `a.svgz` are not.

**PictureAttributes_PictureSuffixesAndBlockClasses_AreWhatTheEngineDraws**: `PictureSuffixes` is `.svg`, `.vl.json`,
`.vl`, `.scad`, `.vsdx`, `.pptx`, `.pdf`, `.mmd`, `.mermaid`, `.puml`, `.plantuml`, `.pu`, `.mml`, `BlockClasses` is
`mermaid`, `plantuml`, `mathml`, `vega-lite`, `vegalite`, `DrawBlockClasses` is `openscad`, and `DrawClass` is `draw`;
every suffix, in lower and upper case, is a drawing (`IsSvg`) or has a kind (`KindOf`); `BlockKindOf` gives `mermaid`
for `mermaid` alone or after another class, `plantuml` for `plantuml`, and `mathml` for `mathml`, and nothing for
`Mermaid`, `puml`, `mml`, `bash`, or no class.

**PictureAttributes_Build_InvalidAttribute_ThrowsTheLuaMessage**: each invalid value (`size` 0x10 and 8193x10, `scale`
0 and `1e3`, `background` `#12`, `page` 0, `dpi` 1.5 for `.vsdx` and 0 for `.pdf`, an unknown `view`, `azimuth`
360001, `elevation` -90.5, an unknown `projection` and `theme`), each attribute on the wrong kind (`view` on a PDF,
`page` and `theme` on a model, `define-x` on a PDF, `param-x` and a bare `param-` on a model), a bad variable name
(`define-1x` and a bare `define-`), an empty parameter name (a bare `param-` on a chart), a parameter name with `=`, a
control character, and an empty value fails with `pandoc-filter-error`, the exact message, and no hint.

**PictureAttributes_Build_SizeAndScale_ThrowsUseOneOfThem**: `size` with `scale` fails with `'f.mmd' has both size and
scale; use one of them`.

**PictureAttributes_Build_Model_AddsDefaultsAndSortsByBytes**: a model with attributes in an arbitrary order gets
`--background=white`, `--define=a=1`, `--define=b=2`, `--max-triangles=2000000`, `--size=1600x1200`, and
`--view=top` (lowercased) in byte order, and keeps `width` (first position, last value `60%`) and `id` in order.

**PictureAttributes_Build_Document_NormalizesPageAndNumbers**: a PDF gets `--dpi=150` and the default `--page=1`; a
presentation gets `--page=7` from `007`, `--dpi=1e-05`, and `--background=transparent`; a Mermaid diagram gets
`--size=640x480` from `0640x0480` and `--theme=forest` from `Forest`.

**PictureAttributes_FormatNumber_MatchesPrintfG15**: the text of `%.15g` for `1e-05`, `0.0001`, `1e+15`,
`123456789012346`, `0.3`, `2400`, `-1.5`, `-0`, and `1e+100`.

**PictureAttributes_BuildSvg_ReadsScaleOnly**: a drawing with `width`, `scale=2.50`, and `view` gives only
`--scale=2.5` and keeps `width` and `view` in order.

**PictureAttributes_BlockNameAndCaption_FollowTheLuaFilter**: a block with an identifier is `mermaid block #flow`; one
without is named by its first non-blank line trimmed (`mermaid block (flowchart LR)`) and cut to 40 characters with
`...` (a non-ASCII character counts as one); a PlantUML block is `plantuml block #seq` or
`plantuml block (@startuml)`, and a MathML block `mathml block (<math display="block">)`; the last of two captions is
the caption and the other attribute stays.

**PictureAttributes_KindOf_Mml_IsMathMl**: `eq.mml` and `docs/Quadratic.MML` are equations; `eq.mathml`,
`eq.mml#display=inline` (a fragment is not cut), and `https://example.com/eq.mml` are not.

**PictureAttributes_Build_MathMlPicture_DefaultsToScaleTwo**: an equation with only `id` gets `--scale=2` and keeps
`id`; with `scale=3` and `background=transparent` it gets `--background=transparent` and `--scale=3`; with
`size=400x100` only `--size=400x100`; with `width=50%` it gets `--scale=2` and keeps the width.
`NaturalWidthNeeded` is true for an equation with only `id` or `scale`, and false with `size`, `width`, or `height`,
and for a Mermaid diagram.

**PictureAttributes_Build_MathMlTheme_IsRefused**: `theme=dark` on `eq.mml` fails with `pandoc-filter-error` and
`'eq.mml' has theme=dark, but theme applies to Mermaid diagrams only`, and `page=2` on `mathml block #eq` with
`'mathml block #eq' has page=2, but page applies to .vsdx, .pptx and .pdf files only`.

**PictureAttributes_BlockOf_VegaLiteAndItsAlias_AreCharts**: `vega-lite` alone and `vegalite` after another class are
`BlockClass` rows of the kind `vegalite` that need no `draw`, each with its own class; `vega-lite` with `draw` is still
a chart; `Vega-Lite`, `vl`, and `json` are not drawn.

**PictureAttributes_BlockOf_OpenScadNeedsTheDrawClass**: `openscad` alone is not drawn; `openscad` with `draw` (in
either order, also with another class) is the row of the kind `scad` that needs `draw`; `draw` alone, `OpenSCAD` or
`scad` with `draw`, and `openscad` with `Draw` are not drawn.

**PictureAttributes_BlockName_NamesTheClassTheAuthorWrote**: a block is named `vega-lite block #sales`,
`vegalite block ({"mark": "bar",)` (its first non-blank line), `openscad block (cube(10);)`, or `openscad block #part`
by the class it was given.
