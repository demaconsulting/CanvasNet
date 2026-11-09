# Mermaid Reference Layouts

The `*.layout.json` files in this folder are **goldens**: the layout that the reference implementation gives each
flowchart fixture of `../flowchart/`. `MermaidReferenceLayoutTests` compares CanvasNet's layout with them.

## Provenance

- **Generator**: `benchmarks/visual-diff/render-mermaid.mjs`, run by
  `pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1` (developer machine, not CI).
- **Versions**: mermaid 11.17.2 drawn by `@mermaid-js/mermaid-cli` 11.16.0 (`renderMermaid`, the code behind
  `mmdc`) in the headless Chrome of puppeteer 25.11.0, Node.js 22.18.0, on Windows. Each file names the mermaid and
  mermaid-cli versions it was made with.
- **Fonts**: `benchmarks/visual-diff/liberation.css` is added to the page before Mermaid draws. It declares the
  bundled `LiberationSans-*.ttf` faces under the names of Mermaid's default font stack (`"trebuchet ms"`, `verdana`,
  `arial`), so the browser measures labels with the same font as CanvasNet.
- **Extraction**: the SVG is read back from the live DOM. Ids lose the render id prefix (`my-svg-`). Positions are in
  the coordinates of the SVG's view box: nested groups are mapped with `getScreenCTM`. Numbers are rounded to 0.001.
- **License**: the fixtures and these files are original work of this repository. They hold coordinates, not
  Mermaid code.

## Content

Each file holds:

- `viewBox`: `[x, y, width, height]`.
- `nodes`: the DOM id (for example `flowchart-A-0`) and the shape (from the diagram's data). It also holds `x` and
  `y` (the node's position, its laid-out center) and `box` (the drawing's bounding box).
- `clusters`: the subgraph id and the `box` of its rectangle.
- `edges`: the edge id (for example `L_A_B_0`), the path `length` and 21 `points`. The points are sampled with
  `getPointAtLength` at 0 %, 5 %, ..., 100 % of the length.
- `edgeLabels`: the edge id and the `box` of each label that has a size.

## Tolerances

The test compares every number with the golden. Most fixtures pass at **0.01 px**: the largest difference seen is
0.001 px, the rounding of the goldens and of Mermaid's 3-decimal path text. The plan allows 2 px. These fixtures
need more:

- `12-new-shapes-flow`, 0.1 px: Mermaid draws the circles of some shapes with rough.js, as a path of sampled points.
  CanvasNet draws true circles, so the boxes differ by up to 0.034 px.
- `14-new-shapes-symbols`, 0.1 px: the same rough.js circles, up to 0.057 px.

## Exclusions

- `38-unicode`: Liberation Sans has no CJK glyphs. Chrome draws `日本語` with a system font, so the golden would depend
  on the machine. CanvasNet uses its bundled fallback and warns `mermaid-text-missing-glyph`. On the generating
  machine the node is 12 px wider in Chrome.

## Regenerating

Run `npm ci` in the repository root, then:

```powershell
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1                        # every fixture
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1 -Fixture 25-subgraph-basic
```

A new fixture in `../flowchart/` needs a golden, or an entry in the exclusions of the script, the test and this
file. A golden change in a pull request is reviewed like code: the diff shows which coordinates moved. Regenerating can
change a few numbers by 0.001: the browser's geometry is single precision, and a value near a rounding boundary can
round either way.
