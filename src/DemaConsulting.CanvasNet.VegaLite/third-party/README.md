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
| Vega-Lite (`vega-lite`) | 6.5.0 (npm `latest` on 2026-10-28; git tag `v6.5.0`) | BSD-3-Clause | (c) 2015, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`) | Default configuration values: background, padding, view size (300 by 300) and step, mark, bar, rect, tick, rule and text configuration, scale configuration, legend gradient lengths, header, facet and concat spacing, number and time format defaults, the `tableau10` default category colors, the precedence rules for mark, view, title and style properties, the opacity and filled defaults of the point-like marks |
| Vega (`vega`, `vega-parser`, `vega-util`, `vega-scale`, `vega-view-transforms`, `vega-scenegraph`) | `vega` 6.4.0, `vega-parser` 7.1.3 (npm `latest` on 2026-10-28) | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`), `ViewLayout` (`Layout/`), `TitleBuilder` and `AxisBuilder` (`Guides/`), `Ticks` (`Scales/`) | Default Vega configuration values (padding, autosize, the guide, group and mark styles, title, axis, axisBand, legend, projection and range defaults), the default stroke width, axis/title/legend layout constants and offsets, tick-count rules, text-baseline offsets |
| d3-array | 3.2.4 | ISC | Copyright 2010-2023 Mike Bostock | `Ticks` (`Scales/`) | The `ticks`, `tickStep` and `nice` algorithms |
| d3-format | 3.1.2 | ISC | Copyright 2010-2026 Mike Bostock | `NumberFormat` (`Scales/`) | The number-format specifier grammar and the quantitative label subset of `format` |

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

The units named above are delivered by later steps of the Vega-Lite implementation; the table is the plan of record
for what will be ported and is extended in the same change that adds any further ported item. `ConfigResolver` is
delivered; the other units are not yet. The default view size (200 by 200 pixels) that
`VegaLiteDocument.Render.cs` still uses for the foundation shell is the Vega-Lite 5 value; the layout unit replaces
it with the configured Vega-Lite 6 value of 300 by 300 pixels.

## Package-distribution obligation

The BSD-3-Clause license requires that the copyright notice, the list of conditions and the disclaimer be
reproduced in binary distributions, and the ISC license requires that the copyright and permission notice appear in
all copies. The license texts are therefore shipped in the NuGet package under `licenses\`:

- `licenses\LICENSE-vega.txt` — BSD-3-Clause, Vega and Vega-Lite (both copyright notices, verbatim)
- `licenses\LICENSE-d3.txt` — ISC, d3-array and d3-format (both copyright notices, verbatim)

Any other package that bundles the `DemaConsulting.CanvasNet.VegaLite` assembly (for example the command-line
tool) must carry the same notices.

## What is not copied

- No Vega or Vega-Lite source code files are copied or translated line by line; ported items are numeric constants,
  default values and small numeric algorithms re-implemented in C#.
- No Vega-Lite tests, example specifications, documentation text or schema files are copied. Documentation is
  paraphrased from the public Vega-Lite documentation.
- No datasets are copied. In particular nothing from `vega-datasets` is used, because each of those datasets keeps
  its own license. All fixtures in the test project are original works created for this repository.
