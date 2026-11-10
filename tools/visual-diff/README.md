# Visual Diff Against Reference Implementations

Developer tooling, **not** part of the build, the tests, or CI. The scripts draw a document twice, once with CanvasNet
and once with the reference implementation, and report how much the two pictures differ. Use them while implementing a
feature, to find the places where CanvasNet's picture is not the picture that the reference draws. The Mermaid scripts
also produce the layout goldens and the upstream vectors that the Mermaid tests use.

## Vega and Vega-Lite

`compare-with-vega.ps1` draws a Vega-Lite specification with CanvasNet and with Vega and Vega-Lite running in Node.

```powershell
pwsh tools/visual-diff/compare-with-vega.ps1 `
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

### How it works

1. `canvasnet render <spec> --scale N` draws the CanvasNet picture (a PNG).
2. `render-vega.mjs` compiles the specification with `vega-lite` and draws it with Vega's own SVG renderer
   (`view.toSVG()`): headless, with no browser and no native `canvas` module. The packages `vega@6` and
   `vega-lite@6.5.0` are installed once into the cache folder with `npm install --no-save`; **nothing is installed
   into the repository**. Needs Node.js 18 or later (tried with Node 22).
3. `canvasnet compare <ours.png> <vega.svg> --resize-to a --side-by-side ... --json` draws the SVG with the same tool
   at the size of the first picture and prints the RMSE, the structural similarity, the mean absolute error, and the
   share of differing pixels. The pictures and the side-by-side comparison (CanvasNet, Vega, and the differences) stay
   in the output folder.

### Reading the result

There are no thresholds and no pass or fail. The fonts, the width of text, and the anti-aliasing differ from a
browser's by design, so even a faithful chart has an RMSE of 30 or more; the number is a guide to where to look, and the
side-by-side picture is the result. The sizes of the two charts are not forced to agree: a Vega picture that has another
size is stretched to ours, so a layout difference shows as a difference of the whole picture, and the sizes of the two
pictures are the first thing to look at.

`render-vega.mjs` drops the unit of the `font-size` attributes that Vega writes (`10px` becomes `10`), because the SVG
reader of CanvasNet takes plain numbers for lengths; the picture does not change. A remote `data.url` is not fetched.

### Status

Tried in Phase 14 with Node 22.18, `vega` 6.4.0, and `vega-lite` 6.5.0 on several charts of the gallery
(`docs/user_guide/examples/gallery`): the charts agree with Vega-Lite's in their structure (marks, axes, legends, and
layout), with the differences that the side-by-side pictures show (for example the filled symbols that Vega-Lite gives
the legend of a size channel next to a color field, where CanvasNet draws rings).

## Mermaid

The Mermaid scripts use the packages that the repository pins in `package.json`. These are `@mermaid-js/mermaid-cli`
11.16.0, which brings mermaid 11.17.2 and puppeteer with its headless Chrome. Run `npm install` in the repository
root once (the repository commits no lockfile, so `npm ci` does not apply). They need Node.js 20 or later (tried with
Node 22.18). Nothing is downloaded at run time.

### Comparing a Diagram

```powershell
pwsh tools/visual-diff/compare-with-mermaid.ps1 `
    -Diagram test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/flowchart/25-subgraph-basic.mmd -Scale 2
```

The parameters:

- `-Diagram`: the Mermaid diagram (a `.mmd` file).
- `-OutDir`: where the pictures are written (default: a new folder under the temporary folder, which is printed).
- `-Scale`: the scale of both pictures (default 1).
- `-ToolCommand`: the `canvasnet` command (default: `CANVASNET_TOOL`, else `dotnet canvasnet`), for example a local
  build.
- `-RenderCommand`: the command that draws CanvasNet's picture, called as `<command> render <diagram> --scale N -o
  <png>` (default: the tool), for example another build of the library.

The output folder gets these files:

- `ours.png`: drawn by `canvasnet render`.
- `ref.png` and `ref.layout.json`: drawn by `render-mermaid.mjs`.
- `side-by-side.png`: drawn by `canvasnet compare ours.png ref.png --resize-to a --side-by-side ... --json`. It shows
  CanvasNet, Mermaid, and the differences.

The script prints the sizes of the two pictures, the RMSE, the structural similarity, the mean absolute error, and the
share of differing pixels. As for Vega, there are no thresholds and no pass or fail. A faithful flowchart has a
structural similarity above 0.99: what remains is the anti-aliasing of the edges and the browser's text rendering.

### The Reference Renderer

`render-mermaid.mjs` draws diagrams with mermaid-cli's `renderMermaid`, the code behind `mmdc`, in headless Chrome:

```powershell
node tools/visual-diff/render-mermaid.mjs [--scale N] [--png-dir DIR] [--layout-dir DIR] <diagram.mmd>...
```

For each `<name>.mmd` it writes `<name>.png` and `<name>.layout.json`. Two details make the reference comparable:

- **Fonts**: `liberation.css` is added to every page before Mermaid draws. It declares the bundled Liberation Sans
  faces (`src/DemaConsulting.CanvasNet/Fonts/BundledFonts`) under the names of Mermaid's font stacks. The browser then
  measures labels with the same font as CanvasNet. `mmdc --cssFile` would not do: mermaid-cli adds that style sheet
  only after Mermaid has measured the labels.
- **Picture**: the PNG is taken from mermaid-cli's SVG at its natural size: the view box rounded up to whole pixels,
  times the scale. This is the size CanvasNet draws at. `mmdc`'s own PNG shrinks the diagram by the page margin: it
  narrows the page to the picture after the SVG (width 100 %) has been laid out.

The layout is read back from the live SVG: the view box, the node positions, shapes and boxes, the subgraph
rectangles, the edge paths sampled at 21 points along their length, and the edge label boxes. Nested groups are mapped
with `getScreenCTM`, and ids lose the render id prefix. For a sequence diagram (`aria-roledescription="sequence"`) it
reads the sequence layout instead: the actors (the top and bottom drawings with their parts and texts, and the
lifelines), the messages (lines or sampled self-message paths, markers, texts, sequence numbers, and central-connection
circles), the notes, activations, frames, `rect` backgrounds, boxes, menus, the title, and the paint order. It also
records the computed paint (fill, stroke, stroke width, dash, and filter), because Mermaid's CSS overrides many
attributes. Any other diagram of Mermaid's unified renderer (a class diagram, `aria-roledescription="classDiagram"`)
gets the flowchart layout extended with the diagram type, the title, the drawn parts of each node (each shape and
label of a class box, with the group that holds it, such as `label-group text` or `divider`), the label and inner
rectangle of each cluster, the class and marker ids of each edge, and the terminal labels (cardinalities), each
matched to its edge and end. The state and ER diagrams (`stateDiagram` and `er`) also get the shape of each
cluster (composite states, the regions of a concurrent state and the note groups are clusters too: Mermaid draws them
as `g.statediagram-cluster` and `g.note-cluster` groups) and, for a label drawn in several pieces (the title and
description texts of a state with descriptions), each piece. The last region of a concurrent state has a random id
(`id-` and a random token and a counter); it is written as `id-random-<counter>`, so the goldens do not change from run
to run. Such a diagram is read at a scale of exactly 1 (its view box grown to whole pixels from the same origin),
because at its `max-width` Chrome scales it by about 0.99995 and lays its SVG texts out narrower. A flowchart keeps
the plain layout, and a class diagram the extended one, so their goldens do not change. The format is
described in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/reference/README.md`.

### Layout Goldens

```powershell
pwsh tools/visual-diff/update-mermaid-goldens.ps1 [-Diagram flowchart|sequence|class|state|er] [-Fixture <name>...]
```

This regenerates the goldens under `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/reference/`: the flowchart
fixtures (`Fixtures/flowchart`) give `reference/*.layout.json`, the sequence fixtures (`Fixtures/sequence`) give
`reference/sequence/*.layout.json`, and the class, state and ER fixtures (`Fixtures/class`, `Fixtures/state` and
`Fixtures/er`) give `reference/class`, `reference/state` and `reference/er`. `-Diagram` picks the types (default:
all). Excluded fixtures (`38-unicode`, whose CJK text Chrome draws with a system font) get no golden; no sequence,
class, state or ER fixture is excluded. `MermaidReferenceLayoutTests` compares CanvasNet's
flowchart layout with the goldens. The tolerance is 0.01 px, or 0.1 px for the two fixtures with rough.js circles. A
golden change is reviewed like code.

### Upstream Vector Generators

The scripts in `mermaid/` record what the pinned packages compute. Their output is the upstream vectors under
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/`. Each takes `--modules <folder>` (the
folder that holds `node_modules`; default: the repository root). The generators also take `--out <file>`: the default
is their vector file.

- `extract-sources.mjs` writes the original sources of mermaid (from `dist/mermaid.js.map`) to `artifacts/mermaid-src`,
  which git ignores, for reading the code the port follows.
- `gen-theme-vectors.mjs` writes `themes.json`: the theme variables and the effective configuration (mermaid in
  Chrome), and khroma's colors (Node).
- `gen-dagre-goldens.mjs` writes `dagre-layouts.json`: dagre-d3-es's ranks, orders, positions and edge routes on
  seeded generated graphs (Node).
- `gen-flowchart-vectors.mjs` writes `flowchart-parser.json`: the flowDb facts and `getData` of the cases in
  `flowchart-vector-cases.mjs`, or the parse error (mermaid in Chrome). `--probe <file>` prints the results instead.
- `gen-sequence-vectors.mjs` writes `sequence-parser.json`: the sequenceDb facts (actors, boxes, messages with their
  line types and wrap flags, created and destroyed actors, title, and accessibility texts) of the cases in
  `sequence-vector-cases.mjs`, or the parse or database error (mermaid in Chrome). Cases may give a site
  configuration, which the wrap cases use. `--probe <file>` prints the results instead.
- `gen-diagram-vectors.mjs --type class` writes `class-parser.json`: the ClassDB facts (classes with their members,
  methods and display texts, relations, notes, namespaces, lollipop interfaces, style classes, direction, and
  accessibility texts) and the reduced `getData()` nodes and edges of the cases in `class-vector-cases.mjs`, or the
  parse or database error (mermaid in Chrome). Each case runs in a fresh page with a single `getDiagramFromText`, so
  the DOM id counter starts at 0 as in mermaid-cli; configuration is therefore given as a site configuration. Most
  inputs are those of mermaid's own class parser specs (MIT), named in each case's `spec` field; the others are
  original texts and the class fixtures. `--probe <file>` prints the results instead. The generator is generic, so
  later diagram types add a `<type>-vector-cases.mjs` and their facts.
- `gen-diagram-vectors.mjs --type state` writes `state-parser.json`: the StateDB facts (the statement tree after the
  translation of the start and end states and the concurrent regions, the states with their descriptions, notes,
  classes and styles, the relations, style classes, links, direction, and accessibility texts) and the reduced
  `getData()` nodes and edges of the cases in `state-vector-cases.mjs`, or the parse or database error. The inputs
  are those of mermaid's state parser specs (MIT), original texts (with the legacy renderer as a site configuration)
  and the state fixtures. The random token of the id that mermaid gives the last region of a concurrent state is
  written as `random` (`id-random-<counter>`).
- `gen-diagram-vectors.mjs --type er` writes `er-parser.json`: the ErDB facts (the entities with their aliases and
  attributes, the relationships with their cardinalities and type, style classes, subgraphs, direction, and
  accessibility texts) and the reduced `getData()` nodes and edges of the cases in `er-vector-cases.mjs`, or the
  parse error. The inputs are those of mermaid's ER parser specs (MIT; a sample of the per-character entity name
  tests), original texts (every cardinality on both sides, keyword-like names, aliases, styles and subgraphs) and the
  ER fixtures.
- `gen-shape-vectors.mjs` writes `shapes.json`: node shape sizes, label boxes, drawn elements, paint and intersections
  (mermaid in Chrome with Liberation Sans). `--probe <file>` prints the results instead.
- `gen-curve-vectors.mjs` writes `curves.json`: d3-shape's path text for each edge curve on original point sets (Node).
