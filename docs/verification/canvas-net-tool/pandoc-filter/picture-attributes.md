### PictureAttributes

#### Verification Approach

`PictureAttributes` is verified with xUnit unit tests in `PictureAttributesTests.cs`. The tests call the internal static
methods directly (`KindOf`, `IsSvg`, `Build`, `BuildSvg`, `SplitCaption`, `BlockName`, and `FormatNumber`) with
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

#### Test Scenarios

**PictureAttributes_KindOf_FileNameEnd_GivesTheKind**: each handled name end gives its kind in any letter case
(`chart.VL.JSON`, `chart.vl`, `C:/models/part.scad` with a drive letter, `.vsdx`, `.pptx`, `.pdf`, `.mmd`, `.mermaid`),
while `https://example.com/part.scad` and `photo.png` give none.

**PictureAttributes_IsSvg_LocalSvgOnly**: `a/Drawing.SVG` is a drawing; a URL ending in `.svg` and `a.svgz` are not.

**PictureAttributes_PictureSuffixesAndBlockClasses_AreWhatTheEngineDraws**: `PictureSuffixes` is `.svg`, `.vl.json`,
`.vl`, `.scad`, `.vsdx`, `.pptx`, `.pdf`, `.mmd`, `.mermaid` and `BlockClasses` is `mermaid`; every suffix, in lower
and upper case, is a drawing (`IsSvg`) or has a kind (`KindOf`); `BlockKindOf` gives `mermaid` for `mermaid` alone or
after another class, and nothing for `Mermaid`, `bash`, or no class.

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
`...` (a non-ASCII character counts as one); the last of two captions is the caption and the other attribute stays.
