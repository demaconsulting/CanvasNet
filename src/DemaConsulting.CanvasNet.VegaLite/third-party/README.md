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
| Vega-Lite (`vega-lite`) | 6.x (npm latest 6.5.0 at the time of the study) | BSD-3-Clause | (c) 2015, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`) | Default configuration values: background, padding, view size and stroke, title, axis, mark and style defaults, the `tableau10` default category colors |
| Vega (`vega`, `vega-parser`, `vega-scale`, `vega-view-transforms`, `vega-scenegraph`) | 6.x (npm latest 6.4.0 at the time of the study) | BSD-3-Clause | (c) 2015-2023, University of Washington Interactive Data Lab | `ConfigResolver` (`SpecModel/`), `ViewLayout` (`Layout/`), `TitleBuilder` and `AxisBuilder` (`Guides/`), `Ticks` (`Scales/`) | Default Vega configuration values, axis/title/legend layout constants and offsets, tick-count rules, text-baseline offsets |
| d3-array | 3.2.4 | ISC | Copyright 2010-2023 Mike Bostock | `Ticks` (`Scales/`) | The `ticks`, `tickStep` and `nice` algorithms |
| d3-format | 3.1.2 | ISC | Copyright 2010-2026 Mike Bostock | `NumberFormat` (`Scales/`) | The number-format specifier grammar and the quantitative label subset of `format` |

The units named above are delivered by later steps of the Vega-Lite implementation; the table is the plan of record
for what will be ported and is extended in the same change that adds any further ported item. Until `ConfigResolver`
exists, the only ported values are the default view size (200 by 200 pixels) and the default padding (5 pixels) in
`VegaLiteDocument.Render.cs`, which carries the attribution header.

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
