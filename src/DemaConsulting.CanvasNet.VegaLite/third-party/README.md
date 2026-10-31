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
| Vega (`vega`, `vega-parser`, `vega-util`, `vega-scale`, `vega-view`, `vega-view-transforms`, `vega-scenegraph`) | `vega` 6.4.0, `vega-parser` 7.1.3 (npm `latest` on 2026-10-28) | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`), `ViewLayout` and `LegendLayout` (`Layout/`), `TextMeasurer`, `TitleBuilder`, `AxisBuilder`, `LabelOverlap` and `LegendBuilder` (`Guides/`), `SceneModel`, `ScenePainter` (`Scenegraph/`), `Ticks`, `ScaleModel` and `ScaleResolver` (`Scales/`) | Default Vega configuration values (padding, autosize, the guide, group and mark styles, title, axis, axisBand, legend, projection and range defaults), the default stroke width, axis/title/legend layout constants and offsets, the axis geometry (tick and label positions, the band offset, rounding, the half-pixel translate, extent clamping, title placement, label alignment and flush, parity and greedy label overlap removal), tick-count rules (`tickCount`), the `bandspace` rule, the order in which a scale applies zero, padding and nice, the ascending value order of sorted domains, text-baseline offsets, the text line-height and truncation rules, the group stroke offset, the symbol shape geometry, the gradient object (a start and an end as fractions of the item's bounding box, and the direction of a legend gradient), the view-layout rules for `autosize` and the title placement, and the legend rules (the entry size and offsets, the symbol and label layout, the gradient bar and its `scaleGradient` stops and tick counts, the discrete gradient bands, the `symbolLimit` rule, the grid layout of legend entries, and the placement of legends by orient, offset, margin, and anchor) |
| d3-array | 3.2.4 | ISC | Copyright 2010-2023 Mike Bostock | `Ticks` (`Scales/`), `AggregateMeasures` (`Transforms/`) | The `ticks`, `tickIncrement` and `tickStep` algorithms (the step is 1, 2 or 5 times a power of ten, chosen with the thresholds sqrt(50), sqrt(10) and sqrt(2); fractional steps are kept as reciprocals); the `quantileSorted` interpolation (the R-7 method: position `(n - 1) * p`, linear between the two closest ranks) behind the median, the quartiles and the bootstrap interval of the aggregate operations |
| d3-format | 3.1.2 | ISC | Copyright 2010-2026 Mike Bostock | `NumberFormat` (`Scales/`) | The format specifier grammar and `format`, `formatPrefix`, `precisionFixed`, `precisionPrefix` and `precisionRound` for the numeric types `e f g r s % p b o d x X c n` and the empty type, with the default (en-US) locale: the sign, symbol, zero, width, grouping, precision and trim handling, the SI prefixes from yocto to yotta and the minus sign U+2212; JavaScript's `toFixed`, `toExponential` and `toPrecision` digits are reproduced from the exact decimal expansion of the double |
| d3-scale | 4.0.2 | ISC | Copyright 2010-2021 Mike Bostock | `ScaleModel`, `Ticks`, `NumberFormat` (`Scales/`) | The arithmetic of the linear scale (normalize and interpolate, `clamp`, `round`, reversed domain and range, `invert`), the `nice` method of a linear scale (at most ten rounds until the step settles), the band and point scales (step, bandwidth, padding, alignment, rounding, reversed range) and `tickFormat` (the precision chosen from the tick step); the time scale `scaleTime` and `scaleUtc` (the same arithmetic on milliseconds), its `tickInterval` table of candidate intervals (one, five, 15 and 30 seconds and minutes, one, three, six and twelve hours, one and two days, a week, one and three months, a year), the nearest-neighbor choice among them, the fallback to the `tickStep` of d3-array below one second and above one year, and its `nice` method (floor and ceiling by the tick interval) |
| d3-scale-chromatic | 3.1.0 | ISC (the ColorBrewer colors it lists: Apache-2.0, see the ColorBrewer row) | Copyright 2010-2024 Mike Bostock | `ColorSchemes` and `ColorSchemeData` (`Scales/`) | The color values of the categorical schemes `category10`, `observable10` and the ColorBrewer qualitative sets `accent`, `dark2`, `paired`, `pastel1`, `pastel2`, `set1`, `set2` and `set3` (as listed in its categorical source files and in Vega's palette table, which takes them from it), and the 31 color samples of the d3 cyclical `rainbow` and `sinebow` ramps that Vega's palette table lists |
| d3-interpolate | 3.0.1 (`main` branch, read on 2026-10-30) | ISC | Copyright 2010-2021 Mike Bostock | `ColorInterpolator` (`Scales/`) | The color interpolators `interpolateRgb` (with the gamma), `interpolateHsl`, `interpolateHslLong`, `interpolateLab`, `interpolateHcl` and `interpolateHclLong` (which channel is interpolated how: the shorter way round the hue circle, a missing hue taking the other color's hue, linear opacity) and `piecewise` (the segment choice `floor(t * n)` clamped to the first and last segment, so that a position outside 0 to 1 extrapolates) |
| d3-color | 3.1.0 (`main` branch, read on 2026-10-30) | ISC | Copyright 2010-2022 Mike Bostock | `ColorInterpolator` (`Scales/`) | The sRGB, HSL, CIELAB (D50 white point) and HCL conversions: the gamma transfer function of sRGB, the matrices and white point constants of CIELAB, the handling of a grey (no hue, no chroma), the HSL channel formulas, and the clamping and rounding of a channel to a whole number between 0 and 255 |
| Vega (`vega-scale`) | `vega` 6.4.0 line, `main` branch read on 2026-10-30 | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `ColorSchemes`, `ColorSchemeData` and `ColorInterpolator` (`Scales/`) | The palette table (the names and the colors of the 68 schemes in the compact form, which is a list of facts about published palettes), the rule that a continuous scheme is a piecewise linear sRGB ramp through its colors (`interpolateColors`, the `interpolate` types and the gamma that only sRGB has), the case-insensitive lookup of a scheme by name, `interpolateRange` (a sub-range and a reversal of a ramp), `quantizeInterpolator` (the colors of a discrete range sampled at `i / (count + 1)`) and the rules that decide how many colors a scheme gives to an ordinal, quantile, quantize, threshold and bin-ordinal scale (the `scheme` handling of the `scale` transform of `vega-encode`) |
| ColorBrewer (`colorbrewer2.org`; Cynthia Brewer, Mark Harrower, The Pennsylvania State University) | as published in the palette tables of Vega and d3-scale-chromatic | Apache-2.0 (the "Apache-Style Software License for ColorBrewer software and ColorBrewer Color Schemes") | Copyright 2002 Cynthia Brewer, Mark Harrower, and The Pennsylvania State University | `ColorSchemeData` (`Scales/`) | The color values of the qualitative sets `accent`, `dark2`, `paired`, `pastel1`, `pastel2`, `set1`, `set2` and `set3`, and of the sequential and diverging ramps that Vega names `blues`, `greens`, `greys`, `oranges`, `purples`, `reds`, `blueGreen`, `bluePurple`, `greenBlue`, `orangeRed`, `purpleBlue`, `purpleBlueGreen`, `purpleRed`, `redPurple`, `yellowGreen`, `yellowOrangeBrown`, `yellowOrangeRed`, `brownBlueGreen`, `purpleGreen`, `purpleOrange`, `redBlue`, `redGrey`, `yellowGreenBlue`, `redYellowBlue`, `redYellowGreen`, `pinkYellowGreen` and `spectral` (Vega's documentation credits ColorBrewer, and Maureen Stone of Tableau Software for some of its ramps; the values are Vega's, not the 3 to 12 class tables of ColorBrewer) |
| matplotlib colormaps (Nathaniel J. Smith, Stefan van der Walt, Eric Firing; `BIDS/colormap`) | `colormaps.py` of the `main` branch, read on 2026-10-30 | CC0 1.0 (public domain dedication; the header asks for credit but imposes no legal restriction) | none (dedicated to the public domain) | `ColorSchemeData` (`Scales/`) | The color values of `viridis`, `magma`, `inferno` and `plasma`, as Vega samples them in 31 colors; credited here as the authors ask |
| cividis (Jamie R. Nunez, Christopher R. Anderton, Ryan S. Renslow; Pacific Northwest National Laboratory) | `pnnl/cmaputil` `main` branch, read on 2026-10-30; the colors are Vega's sampling of the polynomial approximation in d3-scale-chromatic | Battelle Memorial Institute license of `pnnl/cmaputil` (permissive, BSD kind: redistribution with notice, conditions and disclaimer, no use of the name Battelle) | Copyright (c) 2017, Battelle Memorial Institute | `ColorSchemeData` (`Scales/`) | The 64 color samples of `cividis`; the license text is shipped as `LICENSE-cividis.txt` |
| Turbo (Anton Mikhailov, Google LLC) | the published `turbo_colormap.py` header, read on 2026-10-30; the colors are Vega's sampling of the polynomial approximation in d3-scale-chromatic | Apache-2.0 | Copyright 2019 Google LLC | `ColorSchemeData` (`Scales/`) | The 64 color samples of `turbo`; the notice is shipped as `LICENSE-turbo.txt` together with the Apache License text |
| Vega (`vega-statistics`, `vega-transforms`, `vega-encode`) | `main` branch of the `vega` 6.4.0 line, read on 2026-10-29 | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `BinAlgorithm`, `AggregateMeasures`, `AggregateTransforms` and `StackRules` (`Transforms/`) | The bin algorithm of `vega-statistics` (the starting step `base^(round(log_base(span)) - level)`, the bounded-by-us growth of the step, the divisors, the `steps` choice, the rounding of the extent with the precision-based epsilon) and the assignment rules of the `bin` transform (the `1e-14` epsilon, the infinite bins outside the extent, the closed last bin, the anchor shift and the upper boundary formula); the semantics of the aggregate operations (the classification of missing and valid values, the incremental variance, the standard error, the exponential weighting formulas, distinct counts, min and max with the relational operators, argmin and argmax) and the default output names; the behavior rules of the `window` transform (the window-only operations, the default frame, the clamping of frame offsets, the widening of a frame to the peers of its rows), of the `stack` transform of `vega-encode` (separate positive and negative stacks, the `center` and `normalize` offsets) and of the `impute` transform (the missing key combinations, the appended rows, the methods over a window) |
| Vega-Lite (`vega-lite`) | 6.5.0 (see the first row) | BSD-3-Clause | (c) 2015, University of Washington Interactive Data Lab | `AggregateTransforms`, `EncodingPlan`, `EncodingTransforms` and `StackRules` (`Transforms/`), `ScaleResolver` (`Scales/`), `MarkBuilder` (`Marks/`), `AxisBuilder` (`Guides/`) | The default of ten bins for a `bin` transform without `maxbins` and `step`, and the output names of a `bin` transform given one name (`name` and `name_end`); the compile rules that turn an encoding into transforms (the order bin, aggregate, stack; the group-by and stack-by fields; which marks stack and in which direction, the default offset, the layer order and the `order` channel; the names of the derived fields such as `sum_x`, `__count`, `bin_maxbins_10_x` and `x_start`; the default titles such as `Sum of x`, `Count of Records` and `x (binned)`); the default `maxbins` of the channels; the scale domain of a stacked, binned or aggregated field (the union of start and end, no zero and no nice for a bin, the sort of a discrete domain by an aggregate over the unaggregated rows, `unaggregated`); the geometry of a binned bar or rect (the edges moved in by half the bin spacing, the minimum band size, the bin midpoint of point-like marks) and the tick count of a binned axis |
| d3-time | 3.1.0 | ISC | Copyright 2010-2022 Mike Bostock | `TimeCalendar` (`Data/`: the `TimeInterval` type), `Ticks` (`Scales/`) | The calendar interval algorithms for the millisecond, second, minute, hour, day, week (Sunday based), month and year, in local (here: the zone of the render options) and UTC form: `floor`, `ceil`, `round`, `offset`, `count`, `every` (the filter-based forms for seconds, minutes, hours, days and months that floor to a multiple of a field of the unit, the closed forms for years and milliseconds, a result of nothing for a step that is not positive) and `range` (the start rounded up, the stop excluded); the day-of-the-month rule of a local `day.every` and the days-since-the-epoch rule of the UTC one |
| d3-time-format | 4.1.0 | ISC | Copyright 2010-2021 Mike Bostock | `TimeFormat` (`Data/`) | The formatter and parser for the directives `%a %A %b %B %c %d %e %f %g %G %H %I %j %L %m %M %p %q %Q %s %S %u %U %V %w %W %x %X %y %Y %Z %%`, the padding flags (`-`, `_`, `0`) and the `pad` rule, the English locale (day and month names, the periods, and the `%c`, `%x` and `%X` expansions), the week-number arithmetic of `%U`, `%W`, `%V`, `%G` and `%g`, the parse rules (the width of each number, leading white space, names matched without regard to case, the pivot of `%y` at 68 and 69, the defaults for missing parts, no range check of the parsed fields, the `%Z` offset, the whole text consumed), and the treatment of an unknown directive; the regular expressions of the upstream parser are replaced by a hand-written scanner with the same acceptance |
| Vega (`vega-time`, `vega-format`, `vega-transforms`, `vega-functions`, `vega-expression`) | `vega` 6.4.0 line, `main` branch read for the time rules | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `TimeCalendar` and `TimeFormat` (`Data/`), `ExpressionFunctions` (`Expressions/`), `TimeUnitTransform` (`Transforms/`) | The Vega time unit table (the unit names, their order and the parts of a unit), `timeFloor` and `utcFloor` (the reference year 2012 of a unit without a year, the Sunday-based week and the first week of a year, a step that applies to the last part only), `timeBin` (the choice of the interval and step for a `maxbins` request), `timeUnitSpecifier` and `timeMultiFormat` (the nine levels and their formats), the names accepted by `timeOffset` and `timeSequence`, the `week`, `quarter` and `dayofyear` helpers, the start and end fields of the `timeunit` transform, and the names, the argument rules and the results of the date and time expression functions (`datetime`, `utc`, the date part getters and their UTC forms, `timeOffset`, `timeSequence`, `timeFormat`, `timeParse`, `monthFormat` and the rest) |
| Vega-Lite (`vega-lite`) | 6.5.0 (see the first row); the scale-type table and the rect position rules were read at 6.4.1 | BSD-3-Clause | (c) 2015, University of Washington Interactive Data Lab | `TimeCalendar` (`Data/`), `TimeUnitTransform` and `TemporalFields` (`Transforms/`), `EncodingPlan`, `EncodingTransforms` (`Transforms/`), `PredicateSource` (`SpecModel/`), `ScaleResolver` (`Scales/`), `AxisBuilder`, `LegendBuilder` and `TimeLabels` (`Guides/`), `MarkBuilder` (`Marks/`) | The Vega-Lite time unit names (the 40 local names and their `utc` forms, the object form with `maxbins` and `step`), the derived field names and titles of a time unit (such as `yearmonth_date`) and the specifier overrides of the unit labels, the date-time object (its keys, the month and day names, the quarter, the rule for `day`, the default year 2012), the filter and condition expressions for time units and date-time values, the implicit parse of temporal fields, the scale types that a temporal field accepts, the `nice`, `tickCount` and `zero` rules of time scales, the default tick count and minimum tick step of a time axis, the default label, grid, flush and overlap rules of temporal axes, and the geometry of a bar, rect or tick over a time unit (the band size and band position) |

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
| Vega | `packages/vega-parser/src/parsers/guides/legend-gradient.js` | the gradient rectangle of a legend and the direction of its gradient: from `(0, 1)` to `(0, 0)` as fractions of the box for a vertical legend (the largest value at the top) and from `(0, 0)` to `(1, 0)` for a horizontal one; the scene model has the gradient object with those fractions and the painter fills with it |
| Vega | `packages/vega-functions/src/functions/scale-gradient.js` | which stops a scale gradient has: the end points of the domain and the ticks of the scale (15 by default), each at its fraction of the domain; read for the legend builder (a later unit) that makes the stops, and not ported into the scene model, which only holds the stops it is given |
| Vega | `packages/vega-parser/src/parsers/title.js`, `guides/guide-util.js` | the title and subtitle properties, the alignment and angle implied by anchor and orient |
| Vega-Lite | `src/compile/model.ts` (`assembleTitle`) | the default title frame (`group` for single and layered views with a middle anchor) and anchor (`start` for compositions) |

The units are original code written from the public documentation and the rules above. The bundled Liberation
fonts, not the fonts that Vega assumes in a browser, are used for text measurement, so a label can differ from Vega's
by the difference between the two fonts' advance widths; layout rules do not depend on font metrics.

## Files read for the data and scale units

The units `ScaleModel`, `ScaleResolver`, `Ticks`, `NumberFormat`, `ColorSchemes` and `ColorInterpolator` (`Scales/`) and
`DataTable` and
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
| d3-scale-chromatic | `LICENSE` | the copyright notice and the ISC license text, and the Apache-style license for ColorBrewer color schemes (reproduced in `LICENSE-colorbrewer.txt`) |
| d3-scale-chromatic | `src/categorical/*.js` (the categorical color sets), `src/sequential-multi/cividis.js` (read to identify the origin of Vega's `cividis` samples) | the color values of the categorical sets (a run of hexadecimal colors per set) |
| d3-interpolate | `src/rgb.js`, `src/hsl.js`, `src/hcl.js`, `src/lab.js`, `src/color.js`, `src/piecewise.js` | which channel of a color space is interpolated how (`nogamma`, `gamma`, `hue`), the shorter and the long way round the hue circle, the segment choice and the extrapolation of `piecewise`, and how the result is turned into a whole-number color |
| d3-color | `src/color.js`, `src/lab.js` | the sRGB, HSL, CIELAB and HCL conversions and their constants, and the formatting of a color with clamped, rounded channels |
| Vega | `packages/vega-scale/src/palettes.js`, `schemes.js`, `interpolate.js` | the palette table (the names and the colors of every scheme), the rule that a continuous scheme is a piecewise sRGB ramp, which interpolation types exist and that only sRGB has a gamma, `interpolateRange` and `quantizeInterpolator`, and the lower-case lookup of a scheme by name |
| Vega | `packages/vega-encode/src/Scale.js` (`configureScheme`) | how many colors a scheme gives to a quantile, quantize, threshold, bin-ordinal and ordinal scale, the extent and the reversal of a scheme |
| matplotlib colormaps (`BIDS/colormap`) | the header of `colormaps.py` | the license (CC0, public domain dedication) and the names of the authors |
| cmaputil (Pacific Northwest National Laboratory) | `LICENSE` | the license of the cividis colormap (Battelle Memorial Institute; reproduced in `LICENSE-cividis.txt`) |
| Turbo (Google) | the header of `turbo_colormap.py` | the license (Apache-2.0), the copyright notice and the author (reproduced in `LICENSE-turbo.txt`) |
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

## Color scheme data (`ColorSchemeData`)

`ColorSchemeData.cs` holds the colors of 68 schemes (15 categorical and 53 continuous) as one run of hexadecimal
colors per scheme. The values are facts of published palettes that were transcribed from the palette tables of Vega
(`vega-scale`, `palettes.js`) and, for the categorical ColorBrewer sets, `category10` and `observable10`, from the
categorical source files of d3-scale-chromatic; no code was copied. The transcription was verified by comparing every
scheme, color by color, with a second download of the same tables, and the tests compare the number of colors and
the first and last color of every scheme with the published tables. The tests of the ramps use hand-derived values.

| Group (names are Vega's; the lookup ignores the letter case) | Origin | License | Notice that ships with the package |
| ------------------------------------------------------------ | ------ | ------- | ---------------------------------- |
| `tableau10`, `tableau20` | Vega palette table (Vega-Lite's default category colors) | BSD-3-Clause (Vega) | `LICENSE-vega.txt` |
| `category10`, `category20`, `category20b`, `category20c`, `observable10`, `rainbow`, `sinebow` | Vega palette table; d3-scale-chromatic | ISC (d3) | `LICENSE-d3.txt` |
| `accent`, `dark2`, `paired`, `pastel1`, `pastel2`, `set1`, `set2`, `set3` | d3-scale-chromatic and Vega (ColorBrewer qualitative sets) | ISC (d3) and Apache-2.0 (ColorBrewer) | `LICENSE-d3.txt`, `LICENSE-colorbrewer.txt`, `LICENSE-apache-2.0.txt` |
| `blues`, `greens`, `greys`, `oranges`, `purples`, `reds`, `blueGreen`, `bluePurple`, `greenBlue`, `orangeRed`, `purpleBlue`, `purpleBlueGreen`, `purpleRed`, `redPurple`, `yellowGreen`, `yellowOrangeBrown`, `yellowOrangeRed`, `brownBlueGreen`, `purpleGreen`, `purpleOrange`, `redBlue`, `redGrey`, `yellowGreenBlue`, `redYellowBlue`, `redYellowGreen`, `pinkYellowGreen`, `spectral` | Vega palette table (the ramps Vega documents as originally created by ColorBrewer, or by Maureen Stone of Tableau Software) | BSD-3-Clause (Vega) and, where the ColorBrewer colors are the origin, Apache-2.0 (ColorBrewer) | `LICENSE-vega.txt`, `LICENSE-colorbrewer.txt`, `LICENSE-apache-2.0.txt` |
| `blueOrange`, `browns`, `tealBlues`, `teals`, `warmGreys`, `goldGreen`, `goldOrange`, `goldRed`, `lightGreyRed`, `lightGreyTeal`, `lightMulti`, `lightOrange`, `lightTealBlue`, `darkBlue`, `darkGold`, `darkGreen`, `darkMulti`, `darkRed` | Vega palette table (published by Vega; Vega's documentation credits Maureen Stone of Tableau Software with a part of its ramps) | BSD-3-Clause (Vega) | `LICENSE-vega.txt` |
| `viridis`, `magma`, `inferno`, `plasma` | matplotlib colormaps by Nathaniel J. Smith, Stefan van der Walt and Eric Firing (as sampled by Vega) | CC0 1.0, public domain dedication | none required; credited above |
| `cividis` | Jamie R. Nunez, Christopher R. Anderton and Ryan S. Renslow (as sampled by Vega from the polynomial approximation of d3-scale-chromatic) | Battelle Memorial Institute license of `pnnl/cmaputil` (permissive, BSD kind); ISC (d3) for the approximation | `LICENSE-cividis.txt`, `LICENSE-d3.txt` |
| `turbo` | Anton Mikhailov, Google LLC (as sampled by Vega from the polynomial approximation of d3-scale-chromatic) | Apache-2.0 | `LICENSE-turbo.txt`, `LICENSE-apache-2.0.txt` |

Two facts about these tables are recorded openly. First, the ramps that Vega names after ColorBrewer
(`blues` to `spectral`) are Vega's own, 9 to 11 color tables and not the 3 to 12 class tables of ColorBrewer itself,
so a discrete scheme taken from them (a `count`) is sampled from the ramp as Vega does it and does not equal the
class table of ColorBrewer for the same count. Second, Vega's documentation attributes some of its ramps to Maureen
Stone of Tableau Software; no separate license text for those ramps could be found, so they are used as they are
published in Vega's BSD-3-Clause repository, with the credit given here and in the user documentation. The
ColorBrewer notice, the Apache License text and the Turbo and cividis notices are shipped with every package that
contains the table because a copy of the table is a copy of those colors.

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

## Files read for the bin and aggregate units

`BinAlgorithm` (`Transforms/`) is a C# port of the bin algorithm of Vega and carries the header
`Portions derived from Vega (BSD-3-Clause)`; `AggregateMeasures` (`Transforms/`) re-implements the aggregate
operations of Vega from their documented semantics and uses the quantile interpolation of d3-array; the window,
stack and impute transforms (`AggregateTransforms`) follow the behavior of the Vega transforms, and `EncodingPlan`,
`ScaleResolver`, `MarkBuilder` and `AxisBuilder` follow the compile rules of Vega-Lite named below. Rules and small
numeric formulas were taken; no code was copied. The files below were read from the `main` branch of the repositories;
no tests, examples, comments or documentation text were copied.
The tests of these units use values worked out by hand from the algorithm; no upstream test or output was used.

| Project | File | What was taken |
| ------- | ---- | -------------- |
| Vega | `packages/vega-statistics/src/bin.js` | the bin algorithm (ported): the span of the extent, the starting step, the growth by the base, the divisors, the list of steps, the precision and epsilon of the rounding, `nice` |
| Vega | `packages/vega-transforms/src/Bin.js` | the assignment of a value to a bin: the stop rounded up to whole steps, the anchor shift, the infinite bins, the epsilon, the upper boundary of the bin, the default output names `bin0` and `bin1` |
| Vega | `packages/vega-transforms/src/Aggregate.js`, `util/AggregateOps.js` | the aggregate operations and what each reads: the missing, valid and NaN classification, the running sum, product, mean and variance, the standard error, the exponential formulas, `min` and `max` by the relational operators, `argmin` and `argmax` keeping a row |
| Vega | `packages/vega-statistics/src/quartiles.js`, `bootstrapCI.js` | the quartiles as the 0.25, 0.5 and 0.75 quantiles, the bootstrap interval (1,000 resamples of the mean, the 2.5% and 97.5% quantiles) |
| Vega | `packages/vega-transforms/src/JoinAggregate.js`, `Window.js`, `util/WindowOps.js` | the `joinaggregate` result written to every row of a group; the window operations, the default frame, the clamped offsets and the widening to peers |
| Vega | `packages/vega-transforms/src/Impute.js` | the key domain, the missing combinations and their order after the input rows, the methods computed over a window |
| Vega | `packages/vega-encode/src/Stack.js` | the stacking of positive and negative values, the `center` and `normalize` offsets |
| d3-array | `src/quantile.js` | `quantileSorted`: the R-7 interpolation, the handling of the ends |
| Vega-Lite | `src/bin.ts` | the default number of bins (10) of a `bin` without parameters, the default `maxbins` of the channels, and the output names of a `bin` transform with a single name |
| Vega-Lite | `src/stack.ts` | which marks stack, the stacked channel, the group-by and stack-by fields, the default offset and the conditions that switch a stack off |
| Vega-Lite | `src/compile/data/parse.ts`, `bin.ts`, `aggregate.ts`, `stack.ts`, `impute.ts` | the order of the implicit transforms, the group-by and measures of the implicit aggregate, the naming of the bin fields, the order of the layers of a stack |
| Vega-Lite | `src/channeldef.ts` | the names of derived fields (`vgField`) and the default titles of binned and aggregated fields |
| Vega-Lite | `src/compile/scale/properties.ts`, `domain.ts` | the domain of a stacked, binned, ranged or aggregated field, the `zero` and `nice` rules for a bin, the sort of a discrete domain by an aggregate |
| Vega-Lite | `src/compile/mark/encode/position-rect.ts` | the geometry of a bar or rect on a binned channel: the bin spacing, the minimum band size and the midpoint of a binned point |
| Vega-Lite | `src/compile/axis/properties.ts` | the tick count of the axis of a binned field |

Where this package deliberately differs from Vega: the groups of an `aggregate` are keyed by the structure of the values
(a number and a text that spell the same are two groups) and are in the order they are first seen, not by the text of
the values in the order of an object; the bootstrap interval is deterministic, drawn with the published SplitMix64
generator seeded from the JSON pointer of the transform (the same generator as the `sample` transform) instead of
`Math.random`; the growth of the bin step is bounded, the number of bins is bounded by the limit of the scale domain
size, and an extent that no finite step can cover is an error instead of an endless loop; the groups of `joinaggregate`,
`window`, `stack` and `impute` are keyed and ordered the same way, and the default output names of `stack` are
`field_start` and `field_end`.

## Files read for the time units, time formats and time scales

`TimeCalendar` (including `TimeInterval`, `TimeUnitSpec` and `DateTimeObject`), `TimeFormat` and `JsDateParser`
(`Data/`), `TimeUnitTransform` and `TemporalFields` (`Transforms/`), the date and time functions in
`ExpressionFunctions.Time.cs` (`Expressions/`), `Ticks.Time.cs` and `ScaleResolver.Time.cs` (`Scales/`), and `TimeLabels`
and `LegendBuilder.Time.cs` (`Guides/`) re-implement the following behavior in C#. The sources were read from the
upstream repositories (the d3 packages at the versions of the provenance table, Vega from the `main` branch of the
`vega` 6.4.0 line, Vega-Lite as stated in its row) and were rewritten as ports; no code, comments, tests, examples or
output were copied, and the tests of these units use values worked out by hand from the definitions (calendar
arithmetic, the directive definitions and the algorithm steps), never output of d3 or Vega. The unit headers carry
`Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause)` (with Vega-Lite where
it applies).

| Project | File | What was taken |
| ------- | ---- | -------------- |
| d3-time | `src/interval.js`, `src/millisecond.js`, `src/second.js`, `src/minute.js`, `src/hour.js`, `src/day.js`, `src/week.js`, `src/month.js`, `src/year.js` (each file holds the local and the UTC interval of its unit), the interval object (`floor`, `ceil`, `round`, `offset`, `range`, `filter`, `count`, `every`), the floor and offset rule of each unit, the Sunday-based week, the day-of-month `every` of a local day and the days-since-the-epoch `every` of a UTC day, the closed forms of year and millisecond |
| d3-time | `src/ticks.js` | the `tickInterval` table of candidate intervals and the choice by the ratio to the target step |
| d3-time-format | `src/locale.js`, `src/defaultLocale.js` | the formatter and parser tables: the directives, the padding rule, the week-number arithmetic, the English locale, the parse rules (the number windows, the `%Z` rule, the two-digit-year pivot, the defaults, the missing range check) |
| d3-scale | `src/time.js`, `src/utcTime.js`, `src/nice.js` | the time scale as a linear scale over milliseconds, its `ticks` (the interval from the count, the inclusive stop) and its `nice` by the tick interval |
| Vega | `packages/vega-time/src/floor.js`, `util.js`, `bin.js`, `interval.js`, `units.js` | the time unit table and its order, `timeFloor` and `utcFloor` (reference year, week handling, step on the last part), `timeBin` (the interval table and the step choice for `maxbins`), the unit names of `timeOffset` and `timeSequence`, `timeUnitSpecifier`, the `week`, `quarter` and `dayofyear` helpers |
| Vega | `packages/vega-transforms/src/TimeUnit.js` | the `timeunit` transform: the default output names, the start and the end of the unit, the step |
| Vega | `packages/vega-format/src/time.js` | `timeMultiFormat`: the nine levels, the default formats and how a format object overrides them |
| Vega | `packages/vega-functions/src/functions/format.js`, `codegen.js`, `packages/vega-expression/src/functions.js` | the names, signatures and results of the date and time functions (`datetime`, `utc`, the getters, `timeOffset`, `timeSequence`, `timeFormat`, `timeParse`, `monthFormat`, `dayFormat` and the rest) and their registration in the expression language |
| Vega | `packages/vega-scale/src/ticks.js` | the `tickCount` rule for a time scale: a number, an interval name or an interval with a step, and the minimum tick step |
| Vega-Lite | `src/timeunit.ts` | the time unit names and parts, the utc forms, the object form, the derived field names (`timeUnitToString`), the specifier overrides of the unit labels and the field expression of a unit |
| Vega-Lite | `src/datetime.ts` | the date-time object: the keys, the month and day names, the quarter, the rule that drops `day`, the default year 2012, the expression of an object |
| Vega-Lite | `src/predicate.ts` | the meaning of the filter and condition predicates on time units and date-time values, the parts that Vega-Lite does not apply (`week` and `dayofyear`) |
| Vega-Lite | `src/channeldef.ts` | `valueExpr` (the value of a time unit in a domain or a sort), `hasBandEnd` and `getBandPosition` |
| Vega-Lite | `src/compile/data/formatparse.ts` | which fields are parsed as dates implicitly |
| Vega-Lite | `src/scale.ts`, `src/compile/scale/type.ts`, `src/compile/scale/properties.ts` | the scale types per field type (`scaleTypeSupportDataType`), the default type of a temporal field, the `zero`, `nice` and `padding` rules of time scales |
| Vega-Lite | `src/compile/axis/properties.ts`, `src/compile/axis/config.ts` | the default tick count of a time axis, the minimum tick step of a time unit (`tickMinStep`), the default label flush, label overlap and grid of temporal axes, the temporal axis configuration |
| Vega-Lite | `src/compile/mark/encode/position-rect.ts`, `valueref.ts`, `position-point.ts` | the geometry of a bar, rect or tick over a time unit (the unit spanning its end, the band size and band position, the `interpolatedSignalRef` interpolation), and a point-like mark at the start or at the band position of a unit |

The JavaScript `Date` behaviors that the date parsing follows come from the ECMAScript language specification
(ECMA-262: the date time string format, `MakeDay`, `MakeTime`, `MakeDate`, the interpretation of a local time in a gap
or an overlap of the zone) and from the documented behavior of the common JavaScript engines for the legacy date forms;
nothing of an engine was read or copied. The documented subset of the legacy forms is listed in the user guide.

Where this package deliberately differs from the upstream behavior: the calendar is that of the zone of the render
options (UTC when none is given) and never the zone of the machine; an instant outside the years 1 to 9999 is not a
date (no `Invalid Date` object and no range of 8.64e15 milliseconds); the parsers read at most 128 characters (the
format parser 256), accept only the documented legacy forms and have no regular expressions; the formatter and the
parser know the English locale only and never read the current culture; the `now` function is not provided; the
`isoweek` family is an addition; a predicate on a `utc` unit uses the UTC functions; and the time ticks, the label
text and the sequences are bounded by `MaxTicks`, `MaxStringLength` and `MaxScaleDomainSize`.

## Expression language and row transforms (nothing ported)

The units `ExpressionParser`, `ExpressionEvaluator`, `ExpressionFunctions` (except its date and time functions, see _Files
read for the time units, time formats and time scales_ above) and `ExpressionRegex` (`Expressions/`) and
`RowTransforms`, `RowSampler`, `TableLayout` and `ParameterEnvironment` (`Transforms/`) are original C# code. Nothing
from Vega (including `vega-expression` and `vega-functions`), Vega-Lite or d3 was ported, copied or translated into
them: no source file, test, example, data set, constant table or documentation text was taken, which is why these units
have no row in the provenance table and carry no `Portions derived from` header. The date and time functions are in
the separate file `ExpressionFunctions.Time.cs`, which carries the header and is covered by the time rows of the
provenance table.

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
- `licenses\LICENSE-d3.txt` — ISC, d3-array, d3-format, d3-scale, d3-time, d3-time-format, d3-interpolate, d3-color and d3-scale-chromatic (all eight copyright notices, verbatim)
- `licenses\LICENSE-colorbrewer.txt` — Apache-2.0 notice of the ColorBrewer color schemes (verbatim, as published with d3-scale-chromatic)
- `licenses\LICENSE-apache-2.0.txt` — the text of the Apache License, Version 2.0 (ColorBrewer and Turbo)
- `licenses\LICENSE-turbo.txt` — the Google notice of the Turbo color map (Apache-2.0)
- `licenses\LICENSE-cividis.txt` — the Battelle Memorial Institute license of the cividis color map (verbatim)

Any other package that bundles the `DemaConsulting.CanvasNet.VegaLite` assembly (for example the command-line
tool) must carry the same notices.

## What is not copied

- No Vega or Vega-Lite source code files are copied or translated line by line; ported items are numeric constants,
  default values and small numeric algorithms re-implemented in C#.
- No Vega-Lite tests, example specifications, documentation text or schema files are copied. Documentation is
  paraphrased from the public Vega-Lite documentation.
- No datasets are copied. In particular nothing from `vega-datasets` is used, because each of those datasets keeps
  its own license. All fixtures in the test project are original works created for this repository.
- No time zone database and no locale data other than the English day, month and period names of the default
  d3-time-format locale are bundled or copied; zone offsets come from the `TimeZoneInfo` that the caller supplies.
