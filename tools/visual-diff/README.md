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

A remote `data.url` is not fetched.

### Status

Tried in Phase 14 with Node 22.18, `vega` 6.4.0, and `vega-lite` 6.5.0 on several charts of the gallery
(`docs/user_guide/examples/gallery`): the charts agree with Vega-Lite's in their structure (marks, axes, legends, and
layout), with the differences that the side-by-side pictures show (for example the filled symbols that Vega-Lite gives
the legend of a size channel next to a color field, where CanvasNet draws rings).

## Mermaid

The Mermaid scripts use the packages that the repository pins in `package.json`. These are `@mermaid-js/mermaid-cli`
11.16.0 with puppeteer and its headless Chrome, and mermaid 11.17.2, which is pinned on its own (mermaid-cli accepts any
mermaid 11 from 11.14.0, so a fresh install would otherwise take the newest one). Run `npm install` in the repository
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
the plain layout, and a class diagram the extended one, so their goldens do not change. A pie chart
(`aria-roledescription="pie"`) gets the pie layout instead (`extractPie`): the outer circle, the slices with their path
data, the percentage labels, the title and the legend entries, with the computed paint (opacity and CSS `scale`) and
fonts. A gantt chart (`aria-roledescription="gantt"`) gets the gantt layout (`extractGantt`): the excluded days, the
axes and their ticks, the section rows, the tasks and their texts, the section titles, the today line and the title.
Gantt charts read the clock, so every page runs in the UTC time zone with its clock fixed at 2024-06-12T10:30:00Z
(`FIXED_NOW`), and the script checks the page clock before it reads a diagram back. A mindmap
(`aria-roledescription="mindmap"`) gets the extended layout plus, at full precision, each node's `size` (the
`getBBox` of its group: the size the cose-bilkent layout was given, in Chrome's single precision) and `center` (parsed
from its `transform`, which holds the layout's position as JavaScript writes the number), the computed paint of its
shapes and the color of its label, and each edge's `dataPoints` (cytoscape's start, middle and end points, decoded from
the path's `data-points`) and computed paint. A mindmap is drawn twice, and the script fails when the two layouts
differ. Every page also wraps `Math.random`: the script fails when it is called from cose-base's
`findPlaceforPrunedNode`, the only place where the layout could use a random number (it is reached only by a graph that
is not a tree, which a mindmap never is). A packet diagram (`aria-roledescription="packet"`) gets the packet layout
(`extractPacket`): each row group with its block rectangles (their attributes and computed paint) and texts (the labels
and bit numbers with their class, anchor point, computed anchor, dominant baseline, font size, fill and box), and the
title. A git graph (`aria-roledescription="gitGraph"`) gets the git graph layout (`extractGitGraph`): each branch's
line, label rectangle and label rows, each arrow's path data, each commit's bullet shapes, the commit labels and tags
in the order Mermaid drew them, and the title, with their attributes as written, the computed paint and fonts and the
text boxes. With `--seed-random`, the calls of Mermaid's id function (`makeRandomHex`) get the fixed sequence of
`mermaid/seeded-random.mjs` (installed in every page before mermaid loads), so that a commit without an id gets the
same label in every run, the label CanvasNet gives it (`0-eb28011`, `1-cc99105`, ...). The format is described in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/reference/README.md`.

### Layout Goldens

```powershell
pwsh tools/visual-diff/update-mermaid-goldens.ps1 [-Diagram <type>...] [-Fixture <name>...]
```

This regenerates the goldens under `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/reference/`: the flowchart
fixtures (`Fixtures/flowchart`) give `reference/*.layout.json`, the sequence fixtures (`Fixtures/sequence`) give
`reference/sequence/*.layout.json`, and the class, state, ER, pie, gantt, mindmap, packet and git graph fixtures
(`Fixtures/class`, `Fixtures/state`, `Fixtures/er`, `Fixtures/pie`, `Fixtures/gantt`, `Fixtures/mindmap`,
`Fixtures/packet` and `Fixtures/gitgraph`) give `reference/class`, `reference/state`, `reference/er`, `reference/pie`,
`reference/gantt`, `reference/mindmap`, `reference/packet` and `reference/gitgraph` (drawn with `--seed-random`).
`-Diagram` picks the types (`flowchart`, `sequence`, `class`, `state`, `er`, `pie`, `gantt`, `mindmap`, `packet`
and `gitgraph`; default: all). The script refuses to run unless the installed mermaid is the pinned 11.17.2.
Excluded fixtures (`38-unicode`, whose CJK text Chrome draws with a system font) get no golden; no sequence, class,
state, ER, pie, gantt, mindmap, packet or git graph fixture is excluded. `MermaidReferenceLayoutTests` compares
CanvasNet's flowchart layout with the goldens, `MermaidChartReferenceLayoutTests` its pie, gantt and packet layouts,
`MermaidGitGraphReferenceLayoutTests` its git graph layouts, and `MermaidMindmapReferenceLayoutTests` its mindmap
layouts (the node sizes, the layout given the golden's sizes bit for bit, and the whole drawing). The tolerance is
0.01 px, or 0.1 px for the two flowchart fixtures with rough.js circles; text boxes, the slice boxes of a pie and the
node sizes of a mindmap are compared at 0.02 px (see the Tolerances sections of `Fixtures/reference/README.md`). A
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
- `gen-diagram-vectors.mjs --type pie` writes `pie-parser.json`: the PieDB facts (the sections in order with each value
  as JavaScript's `String()` writes it, `showData`, the title and the accessibility texts) of the cases in
  `pie-vector-cases.mjs`, or the error (Langium's lexer and parser errors, or the database's negative-value error). The
  inputs are those of mermaid's pie spec and of the pie tests of `@mermaid-js/parser` (MIT), original texts and the
  pie fixtures. `getDiagramFromText` passes no frontmatter title, so a case's title is that of its text only.
- `gen-diagram-vectors.mjs --type gantt` writes `gantt-parser.json`: the GanttDB statements (formats, tick interval,
  today marker, includes, excludes, weekday, inclusive end dates, sections, sanitized links, title and accessibility
  texts) and the tasks of `getTasks()` (each date as its time in milliseconds) of the cases in
  `gantt-vector-cases.mjs`, or the parse error, or the error `getTasks` throws. Every page of the script runs in the UTC
  time zone with its clock fixed at 2024-06-12T10:30:00Z, so "today" is that day. The inputs are those of mermaid's
  gantt parser and database specs (MIT; the database calls written as gantt texts), original texts and the gantt
  fixtures.
- `gen-diagram-vectors.mjs --type mindmap` writes `mindmap-parser.json`: the MindmapDB nodes in the order `addNode`
  added them (id, node id, level, description, type, icon, class, section, padding, width and children) and the nodes
  and edges of `getData()` (shapes, classes, paddings, widths, edge ids, classes and depths) of the cases in
  `mindmap-vector-cases.mjs`, or the error (jison's lexer and parse errors, the database's "only one root" error, or the
  TypeError of a decoration before any node). The inputs are those of mermaid's mindmap parser and `getData` specs
  (MIT; the database calls written as mindmap texts), original texts and the mindmap fixtures.
- `gen-diagram-vectors.mjs --type packet` writes `packet-parser.json`: the PacketDB facts (the title and accessibility
  texts, the effective packet configuration of `getConfig()`, and the rows of `getPacket()` with each block's start,
  end, bits and label; the first and last 50 rows of a diagram with more than 100, marked `truncated`) of the cases in
  `packet-vector-cases.mjs`, or the error (Langium's lexer and parser errors, the database's contiguity, order and
  zero-bit errors, or the row split's error for a fractional `bitsPerRow`). The inputs are those of mermaid's packet
  spec and of the packet tests of `@mermaid-js/parser` (MIT), original texts (the row split for several `bitsPerRow`
  values, given as a site configuration, the only configuration `getDiagramFromText` applies) and the packet fixtures.
- `gen-diagram-vectors.mjs --type gitgraph` writes `gitgraph-parser.json`: the git graph database facts (the direction,
  the title and accessibility texts, the effective gitGraph configuration of `getConfig()`, the commits in the order of
  the commit map with all their fields, the head commit of each branch, the branch order of `getBranchesAsObjArray()`,
  the current branch and the head commit) of the cases in `gitgraph-vector-cases.mjs`, or the error (Langium's lexer
  and parser errors, or the database's errors of `branch`, `checkout`, `merge` and `cherry-pick`). Each page replaces
  `Math.random` by the fixed sequence of `seeded-random.mjs` after mermaid has loaded, so the ids mermaid generates for
  commits without one are those CanvasNet generates. The inputs are those of mermaid's git graph spec and of the git
  graph tests of `@mermaid-js/parser` (MIT), original texts (the main branch name and order given as a site
  configuration) and the git graph fixtures (their frontmatter left out, its `gitGraph` settings given as the site
  configuration).
- `gen-shape-vectors.mjs` writes `shapes.json`: node shape sizes, label boxes, drawn elements, paint and intersections
  (mermaid in Chrome with Liberation Sans). `--probe <file>` prints the results instead.
- `gen-curve-vectors.mjs` writes `curves.json`: d3-shape's path text for each edge curve on original point sets (Node).
- `gen-time-vectors.mjs` writes `time-axis.json`: the d3 time functions the gantt chart uses (date getters, the Date
  constructor and setters, the d3-time intervals with `every`, ticks of a time scale by count and by interval,
  `tickStep`, the `rangeRound` time scale and `timeFormat` with every directive and pad) on original inputs (Node).
  The script sets the time zone to UTC and refuses to run when it is not UTC or when an installed d3 package is not
  the version in mermaid's source map.
- `gen-cose-goldens.mjs` writes `cose-layouts.json` and `js-math.json` (`--layouts <file>` and `--math <file>` instead
  of `--out`). It runs only in Chrome, never in Node: the cose layout of mindmaps is chaotic, so its results depend on
  the last bit of every sine, cosine, logarithm and power, and Node's `Math` differs from Chrome's. The script extracts
  cytoscape, layout-base, cose-base and cytoscape-cose-bilkent from mermaid's source map (it checks their paths and
  versions), runs Mermaid's `createCytoscapeInstance` on seeded trees and on three mindmaps that the pinned mermaid draws
  in the same browser (the replay must give what mermaid drew), and records every node center, every edge's start,
  middle and end point, and the iteration count, or the error. It fails when `Math.random` is called during a layout or
  when a layout does not take the radial (flat forest) path. `js-math.json` holds Chrome's `Math.sin` and `Math.cos` of
  seeded angles and of every angle the layouts used, `Math.log` of seeded values and of the layouts' arguments, and
  `Math.pow` of every pair the cooling schedule makes for 1 to 1,016 nodes, each double as 16 hexadecimal digits.
  Regenerate both files together whenever puppeteer's Chrome changes.
