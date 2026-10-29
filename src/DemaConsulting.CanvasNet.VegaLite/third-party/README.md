# Third-Party Provenance and Attribution

`DemaConsulting.CanvasNet.VegaLite` implements the public Vega-Lite specification language. The package is
**derived with attribution**: the specification model, normalization, layout structure and marks are original code
written from the public Vega-Lite documentation, while a small number of numeric algorithms, default values and
layout constants were ported from the projects listed below. Every ported item is acknowledged here, the license
texts of the upstream projects ship with the package, and every source file that contains ported logic carries the
header comment `Portions derived from <project> (<license>); see third-party/README.md`.

This project is **not affiliated with, and is not endorsed or promoted by**, the Vega project, the Vega-Lite project,
the D3 project or the University of Washington Interactive Data Lab (UW IDL). The names are used only to describe
the language that is implemented.

## Provenance table

| Upstream project | Version studied | License | Copyright notice | CanvasNet unit that ports it | What is ported |
| ---------------- | --------------- | ------- | ---------------- | ---------------------------- | -------------- |
| Vega-Lite (`vega-lite`) | 6.5.0 (npm `latest` on 2026-10-28; git tag `v6.5.0`) | BSD-3-Clause | (c) 2015, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`), `ScaleResolver` and `ColorSchemes` (`Scales/`) | Default configuration values: background, padding, view size (300 by 300) and step, mark, bar, rect, tick, rule and text configuration, scale configuration, legend gradient lengths, header, facet and concat spacing, number and time format defaults, the `tableau10` default category colors, the precedence rules for mark, view, title and style properties, the opacity and filled defaults of the point-like marks; the scale inference rules (scale type per field type, channel and mark; domain order and `sort`; the `zero`, `nice`, `padding`, `paddingInner`, `paddingOuter` and `reverse` rules; the default ranges of size and opacity scales; `step` sizing of discrete views), the default axis tick count (one tick per 40 pixels) and minimum tick step, the axis defaults and rules (grid, label angle, label flush, label overlap, orientation, z-index), the axis configuration precedence, and the inferred orientation, band size, thickness, opacity, color and invalid-data rules of the bar, rect, point, circle, square, rule and tick marks |
| Vega (`vega`, `vega-parser`, `vega-util`, `vega-scale`, `vega-view`, `vega-view-transforms`, `vega-scenegraph`) | `vega` 6.4.0, `vega-parser` 7.1.3 (npm `latest` on 2026-10-28) | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`), `ViewLayout` (`Layout/`), `TextMeasurer`, `TitleBuilder` and `AxisBuilder` (`Guides/`), `SceneModel`, `ScenePainter` (`Scenegraph/`), `Ticks`, `ScaleModel` and `ScaleResolver` (`Scales/`) | Default Vega configuration values (padding, autosize, the guide, group and mark styles, title, axis, axisBand, legend, projection and range defaults), the default stroke width, axis/title/legend layout constants and offsets, the axis geometry (tick and label positions, the band offset, rounding, the half-pixel translate, extent clamping, title placement, label alignment and flush, parity and greedy label overlap removal), tick-count rules (`tickCount`), the `bandspace` rule, the order in which a scale applies zero, padding and nice, the ascending value order of sorted domains, text-baseline offsets, the text line-height and truncation rules, the group stroke offset, the symbol shape geometry, the view-layout rules for `autosize` and the title placement |
| d3-array | 3.2.4 | ISC | Copyright 2010-2023 Mike Bostock | `Ticks` (`Scales/`) | The `ticks`, `tickIncrement` and `tickStep` algorithms (the step is 1, 2 or 5 times a power of ten, chosen with the thresholds sqrt(50), sqrt(10) and sqrt(2); fractional steps are kept as reciprocals) |
| d3-format | 3.1.2 | ISC | Copyright 2010-2026 Mike Bostock | `NumberFormat` (`Scales/`) | The format specifier grammar and `format`, `formatPrefix`, `precisionFixed`, `precisionPrefix` and `precisionRound` for the numeric types `e f g r s % p b o d x X c n` and the empty type, with the default (en-US) locale: the sign, symbol, zero, width, grouping, precision and trim handling, the SI prefixes from yocto to yotta and the minus sign U+2212; JavaScript's `toFixed`, `toExponential` and `toPrecision` digits are reproduced from the exact decimal expansion of the double |
| d3-scale | 4.0.2 | ISC | Copyright 2010-2021 Mike Bostock | `ScaleModel`, `Ticks`, `NumberFormat` (`Scales/`) | The arithmetic of the linear scale (normalize and interpolate, `clamp`, `round`, reversed domain and range, `invert`), the `nice` method of a linear scale (at most ten rounds until the step settles), the band and point scales (step, bandwidth, padding, alignment, rounding, reversed range) and `tickFormat` (the precision chosen from the tick step) |
| d3-scale-chromatic | 3.1.0 | ISC | Copyright 2010-2024 Mike Bostock | `ColorSchemes` (`Scales/`) | Only the ten colors of the `tableau10` scheme, which Vega-Lite also lists as its default category palette; no ColorBrewer scheme (whose Apache-style license the package also carries) is used |

## Files read for `ConfigResolver`

Only values were taken; no code and no comments were copied. The files below were read for understanding and each
constant in `SpecModel/ConfigResolver.cs` was checked against them.

| Project | File (path in the upstream repository, at the version above) | Values taken |
| ------- | ------------------------------------------------------------ | ------------ |
| Vega-Lite | `src/config.ts` | `defaultConfig`, `defaultViewConfig`, `DEFAULT_STEP`, `tab10`; how the configuration is merged with the specification's and redirected to styles |
| Vega-Lite | `src/mark.ts` | `defaultMarkConfig`, `defaultRectConfig`, `defaultBarConfig`, `defaultTickConfig` |
| Vega-Lite | `src/scale.ts` | `defaultScaleConfig` |
| Vega-Lite | `src/legend.ts` | `defaultLegendConfig` (the Vega-Lite-only gradient lengths) |
| Vega-Lite | `src/spec/base.ts` | `DEFAULT_SPACING` |
| Vega-Lite | `src/title.ts`, `src/compile/model.ts` | which title properties are title configuration and which are text styles; the default title frame |
| Vega-Lite | `src/compile/common.ts` | the precedence of mark style, mark-specific and general mark configuration |
| Vega-Lite | `src/compile/mark/init.ts` | the default opacity of point-like marks and the default `filled` rule |
| Vega-Lite | `src/compile/scale/properties.ts` | which scale defaults are rules and not configuration (for the documentation only) |
| Vega | `packages/vega-parser/src/config.js` | the complete Vega default configuration |
| Vega | `packages/vega-parser/src/parsers/title.js` | how title and subtitle properties are looked up |
| Vega | `packages/vega-util/src/mergeConfig.ts` | how configurations are merged (CanvasNet merges deeper; see the unit's documentation) |
| Vega | `packages/vega-scenegraph/src/util/canvas/stroke.js` | the default stroke width of 1 |

The names of the properties that Vega-Lite defines (used so that valid properties are never reported as unknown) were
read from the published Vega-Lite version 6 JSON schema; only property names and enumerations were used.

## Files read for the scenegraph, text, title and view layout units

The units `TextMeasurer` and `TitleBuilder` (`Guides/`), `ViewLayout` (`Layout/`) and `SceneModel` and `ScenePainter`
(`Scenegraph/`) re-implement the following behavior in C#; only numeric constants, rules and algorithm structure were
taken, no code and no comments were copied. Files are read from the Vega `main` branch of the version above.

| Project | File | What was taken |
| ------- | ---- | -------------- |
| Vega | `packages/vega-scenegraph/src/util/text.js` | the default font size (11) and line height (font size + 2), the baseline offsets of a text mark (0.79, 0.30, -0.21 of the font size, 0.29 of it plus or minus half a line for `line-top` and `line-bottom`, rounded half up), trimming and the binary-search truncation with the ellipsis |
| Vega | `packages/vega-scenegraph/src/marks/text.js` | how a text mark is positioned (offsets, rotation about the position, one baseline per line) and how its bounds are computed (the 80% offset, alignment, rotation) |
| Vega | `packages/vega-scenegraph/src/marks/group.js` | the group stroke offset (half a pixel for a one-pixel stroke), background before children, clipping to the group's box |
| Vega | `packages/vega-scenegraph/src/util/canvas/stroke.js` | the stroke defaults (width 1, butt caps, miter joins, miter limit 4) |
| Vega | `packages/vega-scenegraph/src/path/symbols.js`, `path/shapes.js` | the geometry of the symbol shapes with `size` as the area of the bounding box, and the default symbol size 64 |
| Vega | `packages/vega-view-transforms/src/ViewLayout.js`, `layout/title.js` | how `autosize` computes the canvas from the bounds of the view, and how the title is placed (frame, anchor, orient, offset, subtitle position) |
| Vega | `packages/vega-view/src/render-size.js` | the canvas size and origin from the view size and the padding |
| Vega | `packages/vega-parser/src/parsers/title.js`, `guides/guide-util.js` | the title and subtitle properties, the alignment and angle implied by anchor and orient |
| Vega-Lite | `src/compile/model.ts` (`assembleTitle`) | the default title frame (`group` for single and layered views with a middle anchor) and anchor (`start` for compositions) |

The units are original code written from the public documentation and the rules above. The bundled Liberation
fonts, not the fonts that Vega assumes in a browser, are used for text measurement, so a label can differ from Vega's
by the difference between the two fonts' advance widths; layout rules do not depend on font metrics.

## Files read for the data and scale units

The units `ScaleModel`, `ScaleResolver`, `Ticks`, `NumberFormat` and `ColorSchemes` (`Scales/`) and `DataTable` and
`DataLoader` (`Data/`) re-implement the following behavior in C#. The algorithms of the d3 packages were read from the
repositories (the sources of the published versions named above are the same as the repository sources of the files
listed) and were rewritten in C# as ports; no code and no comments were copied. Vega and Vega-Lite files were read
for rules and numeric constants only. The tests of these units use hand-derived expected values; no upstream test
file, example or output was used.

| Project | File | What was taken |
| ------- | ---- | -------------- |
| d3-array | `src/ticks.js` | the `tickSpec` arithmetic behind `ticks`, `tickIncrement` and `tickStep`: the thresholds, the reciprocal encoding of fractional steps, the doubling of a count below 2, the reversed interval |
| d3-array | `src/nice.js` | read for comparison; the `nice` of the linear scale (below) is the one ported |
| d3-scale | `src/linear.js` | the `nice` method of a linear scale (at most ten rounds, a reversed domain keeps its direction) |
| d3-scale | `src/continuous.js` | how a linear scale normalizes and interpolates (`(x - a) / (b - a)`, `a * (1 - t) + b * t`, swapping both pairs for a descending domain, clamping before normalizing) |
| d3-scale | `src/band.js` | the band and point scales: `step = (stop - start) / max(1, n - paddingInner + 2 * paddingOuter)`, the start offset by `align`, `bandwidth`, rounding and the reversed range |
| d3-scale | `src/tickFormat.js` | choosing the precision of a label format from the tick step, with the default specifier `,f` |
| d3-format | `src/formatSpecifier.js`, `src/locale.js`, `src/defaultLocale.js` | the specifier grammar, the type aliases, the default precision and its limits, the sign, symbol, zero and alignment handling, the SI prefixes and the default locale |
| d3-format | `src/formatDecimal.js`, `src/formatRounded.js`, `src/formatPrefixAuto.js`, `src/formatTypes.js`, `src/formatTrim.js`, `src/formatGroup.js`, `src/exponent.js` | the digit and exponent computations of each type, trimming of insignificant zeros, grouping of the integer part |
| d3-format | `src/precisionFixed.js`, `src/precisionPrefix.js`, `src/precisionRound.js` | the precision helpers |
| d3-scale-chromatic | `LICENSE` | the copyright notice and the ISC license text (the ColorBrewer license part is not reproduced because no ColorBrewer scheme is used) |
| Vega | `packages/vega-scale/src/ticks.js` | the `tickCount` rule: a count is first limited to the number of minimum steps in the domain plus one and lowered while the tick step is below the minimum step |
| Vega | `packages/vega-scale/src/scales/bandSpace.js` | `bandspace`, the number of steps a band or point scale spans |
| Vega | `packages/vega-encode/src/Scale.js` | the order in which a scale applies `zero`, pixel padding (zoom around the middle) and `nice` (a count of 10 for `nice: true`), `rangeStep` as `step * bandspace`, and how `padding` sets both paddings of a band scale |
| Vega | `packages/vega-format/src/number.js` | `formatSpan`, which is the label format of a continuous axis (the same as d3-scale's `tickFormat`) |
| Vega | `packages/vega-util/src/compare.ts` | the ascending order of values used for sorted domains (null first, NaN after it) |
| Vega-Lite | `src/compile/scale/type.ts` | the default scale type per field type, channel and mark (band for rect, bar, rule and tick, point otherwise), and which explicit types a channel and a field type accept |
| Vega-Lite | `src/compile/scale/domain.ts` | the domain of a discrete scale and its `sort` (ascending by default, descending, an array, null) |
| Vega-Lite | `src/compile/scale/properties.ts` | the rules for `zero`, `nice`, `padding`, `paddingInner`, `paddingOuter` and `reverse` |
| Vega-Lite | `src/compile/scale/range.ts` | the default ranges of position, size, opacity and color scales, the size range by mark and step, and `step` sizing of discrete scales |
| Vega-Lite | `src/compile/layoutsize/parse.ts`, `src/config.ts` | the default size of a discrete view (the configured step, 20) |
| Vega-Lite | `src/compile/axis/properties.ts` | `defaultTickCount` (one tick per 40 pixels, rounded up) and `defaultTickMinStep` (1 for the format `d`) |
| Vega-Lite | `src/compile/mark/init.ts` | the inferred orientation of bar, tick and rule marks |
| Vega-Lite | `src/scale.ts` | the default scale configuration values and which scale types a channel supports |

The default palette `tableau10` is listed by Vega-Lite (`src/config.ts`) and Vega; its colors are also published in
d3-scale-chromatic. The ten color values are facts of a published palette and are covered by the notices above.

## Files read for the axis and mark units

`AxisBuilder` (`Guides/`) and `MarkBuilder` (`Marks/`) were written as own C# code from the behavior of the files
below, at the versions named in the provenance table (Vega-Lite `v6.5.0`, Vega `main` for the `vega-parser` 7.1.3
line). Constants, defaults and small numeric rules were taken; no code, tests, comments or examples were copied.

| Project | File | What was taken |
| ------- | ---- | -------------- |
| Vega | `packages/vega-parser/src/parsers/axis.js`, `guides/axis-config.js`, `guides/guide-util.js`, `guides/axis-util.js` | the parts of an axis and their order, the axis configuration merge (axis, axisX/axisY, the orient, axisBand), the tick band rule (band 0.5, offsets), the signs and conditions per orient |
| Vega | `packages/vega-parser/src/parsers/guides/axis-grid.js`, `axis-ticks.js`, `axis-labels.js`, `axis-domain.js`, `axis-title.js` | the positions of grid lines, ticks, labels, the domain line and the title; the label alignment and baseline per orient, `labelFlush` and `labelFlushOffset`, tick rounding, the label and title properties |
| Vega | `packages/vega-parser/src/parsers/encode/entry.js` | the order of scale, band, offset and rounding in an encoded position |
| Vega | `packages/vega-view-transforms/src/layout/axis.js` | the axis extent (the far edge of ticks and labels clamped to `minExtent` and `maxExtent`), the title offset by the extent and `titlePadding`, the half-pixel translate |
| Vega | `packages/vega-view-transforms/src/Overlap.js` | the parity and greedy label overlap removal, the separation test, the handling of the last label |
| Vega | `packages/vega-encode/src/AxisTicks.js`, `packages/vega-scale/src/ticks.js` | how ticks and labels are generated from a scale (`validTicks` for explicit values, the format of discrete labels) |
| Vega | `packages/vega-util/src/flush.ts` | the `flush` rule that selects the end alignment of a label near an end of the axis |
| Vega | `packages/vega-scenegraph/src/bound/boundStroke.js`, `marks/rule.js` | the bounds of a stroked rule (grown by half the stroke width) |
| Vega-Lite | `src/compile/axis/parse.ts`, `properties.ts`, `config.ts` | the axis property precedence (axis, Vega-Lite only configuration, Vega configuration), the rule defaults for `grid`, `labelAngle`, `labelFlush`, `labelOverlap`, `tickCount`, `title`, `zindex`, and the default label alignment and baseline for an angle |
| Vega-Lite | `src/compile/mark/bar.ts`, `rect.ts`, `point.ts`, `rule.ts`, `tick.ts` | which properties each mark encodes and how its position channels are combined |
| Vega-Lite | `src/compile/mark/encode/position-rect.ts`, `position-point.ts`, `position-range.ts`, `scaledZeroOrMinOrMax.ts`, `valueref.ts`, `position-align.ts` | the band size of bars and ticks, centering, the continuous band size, the range between a value and zero, the position of marks with a missing channel |
| Vega-Lite | `src/compile/mark/encode/base.ts`, `color.ts`, `nonposition.ts`, `src/compile/mark/init.ts` | the color, fill, stroke, opacity and size encoding, `filled`, the transparent fill, the default opacity of point-like marks |
| Vega-Lite | `src/compile/invalid/ScaleInvalidDataMode.ts`, `src/compile/mark/encode/invalid.ts` | the default invalid-data mode (`filter`) for marks that are not paths |
| Vega-Lite | `src/compile/mark/mark.ts` | the clipping of marks (only by `clip` or a selection-bound scale) |
| Vega-Lite | `src/channeldef.ts` | `getBandSize`: the default size of a rect-based mark by scale type |

`Ticks`, `NumberFormat`, `ScaleModel`, `ScaleResolver`, `ColorSchemes`, `DataTable`, `DataLoader`, `AxisBuilder` and
`MarkBuilder` are delivered.
Other units named in the provenance table are delivered by later steps of the Vega-Lite implementation; the table
is the plan of record for what will be ported and is extended in the same change that adds any further ported item.
The view size that the layout uses is the configured Vega-Lite 6 value of 300 by 300 pixels (Vega-Lite 5 used 200 by
200).

## Expression language and row transforms (nothing ported)

The units `ExpressionParser`, `ExpressionEvaluator`, `ExpressionFunctions` and `ExpressionRegex` (`Expressions/`) and
`RowTransforms`, `RowSampler`, `TableLayout` and `ParameterEnvironment` (`Transforms/`) are original C# code. Nothing
from Vega (including `vega-expression` and `vega-functions`), Vega-Lite or d3 was ported, copied or translated: no
source file, test, example, data set, constant table or documentation text was taken, which is why these units have no
row in the provenance table and carry no `Portions derived from` header.

They were written from public descriptions only:

- the documentation of the Vega expression language (the supported syntax, the function names and signatures, and what
  each function returns), and the ECMAScript semantics of the operators, the type conversions, equality, relational
  comparison, `Math` and the regular expression flags, which the evaluator follows because the language is a subset of
  JavaScript;
- the documentation of the Vega-Lite `calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot` and `lookup`
  transforms and of the `params` property (the property names and the behavior that the documentation states).

Where this package deliberately differs from the upstream behavior (a `lookup` takes the first row of a repeated key,
`sample` is deterministic, `pivot` supports `sum` and `count` only), the difference is stated in the user guide. The
`sample` transform does not reproduce the random number generator of Vega: it uses the published 64-bit FNV-1a hash
(offset basis and prime), the published SplitMix64 generator (its increment, multipliers and shifts) and the classic
reservoir sampling algorithm, all implemented from their public descriptions. The translation of a JavaScript regular
expression to a .NET one (`ExpressionRegex`) and the math functions (the host's `System.Math`) are not derived from any
upstream project.

## Package-distribution obligation

The BSD-3-Clause license requires that the copyright notice, the list of conditions and the disclaimer be
reproduced in binary distributions, and the ISC license requires that the copyright and permission notice appear in
all copies. The license texts are therefore shipped in the NuGet package under `licenses\`:

- `licenses\LICENSE-vega.txt` — BSD-3-Clause, Vega and Vega-Lite (both copyright notices, verbatim)
- `licenses\LICENSE-d3.txt` — ISC, d3-array, d3-format, d3-scale and d3-scale-chromatic (all four copyright notices, verbatim)

Any other package that bundles the `DemaConsulting.CanvasNet.VegaLite` assembly (for example the command-line
tool) must carry the same notices.

## What is not copied

- No Vega or Vega-Lite source code files are copied or translated line by line; ported items are numeric constants,
  default values and small numeric algorithms re-implemented in C#.
- No Vega-Lite tests, example specifications, documentation text or schema files are copied. Documentation is
  paraphrased from the public Vega-Lite documentation.
- No datasets are copied. In particular nothing from `vega-datasets` is used, because each of those datasets keeps
  its own license. All fixtures in the test project are original works created for this repository.
