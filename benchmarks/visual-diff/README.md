# Visual Diff Against Vega and Vega-Lite

Developer tooling, **not** part of the build, the tests, or CI. It draws a Vega-Lite specification twice, once with
CanvasNet and once with the reference implementation (Vega and Vega-Lite running in Node), and reports how much the two
pictures differ. Use it while implementing a feature, to find the places where CanvasNet's chart is not the chart that
Vega-Lite draws.

```powershell
pwsh benchmarks/visual-diff/compare-with-vega.ps1 `
    -Spec docs/user_guide/examples/gallery/09-layer-rule-text.vl.json -Scale 2
```

The parameters:

- `-Spec`: the Vega-Lite specification (a `.vl.json` file; inline or local data).
- `-OutDir`: where the pictures are written (default: a new folder under the temporary folder, which is printed).
- `-Scale`: the scale of both pictures (default 1).
- `-ModulesFolder`: the folder that holds `node_modules/vega` and `node_modules/vega-lite` (default: a cache folder
  under the temporary folder).
- `-ToolCommand`: the `canvasnet` command (default: `CANVASNET_TOOL`, else `dotnet canvasnet`), for example a local
  build.

## How it works

1. `canvasnet render <spec> --scale N` draws the CanvasNet picture (a PNG).
2. `render-vega.mjs` compiles the specification with `vega-lite` and draws it with Vega's own SVG renderer
   (`view.toSVG()`): headless, with no browser and no native `canvas` module. The packages `vega@6` and
   `vega-lite@6.5.0` are installed once into the cache folder with `npm install --no-save`; **nothing is installed
   into the repository**. Needs Node.js 18 or later (tried with Node 22).
3. `canvasnet compare <ours.png> <vega.svg> --resize-to a --side-by-side ... --json` draws the SVG with the same tool
   at the size of the first picture and prints the RMSE, the structural similarity, the mean absolute error, and the
   share of differing pixels. The pictures and the side-by-side comparison (CanvasNet, Vega, and the differences) stay
   in the output folder.

## Reading the result

There are no thresholds and no pass or fail. The fonts, the width of text, and the anti-aliasing differ from a
browser's by design, so even a faithful chart has an RMSE of 30 or more; the number is a guide to where to look, and the
side-by-side picture is the result. The sizes of the two charts are not forced to agree: a Vega picture that has another
size is stretched to ours, so a layout difference shows as a difference of the whole picture, and the sizes of the two
pictures are the first thing to look at.

`render-vega.mjs` drops the unit of the `font-size` attributes that Vega writes (`10px` becomes `10`), because the SVG
reader of CanvasNet takes plain numbers for lengths; the picture does not change. A remote `data.url` is not fetched.

## Status

Tried in Phase 14 with Node 22.18, `vega` 6.4.0, and `vega-lite` 6.5.0 on several charts of the gallery
(`docs/user_guide/examples/gallery`): the charts agree with Vega-Lite's in their structure (marks, axes, legends, and
layout), with the differences that the side-by-side pictures show (for example the filled symbols that Vega-Lite gives
the legend of a size channel next to a color field, where CanvasNet draws rings).
