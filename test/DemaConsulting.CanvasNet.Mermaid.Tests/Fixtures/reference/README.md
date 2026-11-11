# Mermaid Reference Layouts

The `*.layout.json` files in this folder are **goldens**: the layout that the reference implementation gives each
flowchart fixture of `../flowchart/`. `MermaidReferenceLayoutTests` compares CanvasNet's layout with them. The
`sequence/` subfolder holds the goldens of the sequence fixtures of `../sequence/`; their format is described in
[Sequence Goldens](#sequence-goldens). The `class/` subfolder holds the goldens of the class fixtures of `../class/`;
their format is described in [Class Goldens](#class-goldens). The `state/` and `er/` folders hold the goldens of the
state and ER fixtures of `../state/` and `../er/`; their format is described in [State Goldens](#state-goldens) and
[ER Goldens](#er-goldens). The `pie/` folder holds the goldens of the pie fixtures of `../pie/`, which
`MermaidChartReferenceLayoutTests` compares; their format is described in [Pie Goldens](#pie-goldens). The `gantt/`
folder holds the goldens of the gantt fixtures of `../gantt/`, which the same test compares; their format is
described in [Gantt Goldens](#gantt-goldens).

## Provenance

- **Generator**: `tools/visual-diff/render-mermaid.mjs`, run by
  `pwsh tools/visual-diff/update-mermaid-goldens.ps1` (developer machine, not CI).
- **Versions**: mermaid 11.17.2 drawn by `@mermaid-js/mermaid-cli` 11.16.0 (`renderMermaid`, the code behind
  `mmdc`) in the headless Chrome of puppeteer 25.11.0, Node.js 22.18.0, on Windows. Each file names the mermaid and
  mermaid-cli versions it was made with.
- **Fonts**: `tools/visual-diff/liberation.css` is added to the page before Mermaid draws. It declares the
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

Run `npm install` in the repository root, then:

```powershell
pwsh tools/visual-diff/update-mermaid-goldens.ps1                        # every fixture of every type
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Fixture 25-subgraph-basic
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Diagram sequence      # the sequence goldens only
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Diagram class         # the class goldens only
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Diagram state, er     # the state and ER goldens only
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Diagram pie           # the pie goldens only
pwsh tools/visual-diff/update-mermaid-goldens.ps1 -Diagram gantt         # the gantt goldens only
```

A new fixture in `../flowchart/`, `../sequence/`, `../class/`, `../state/`, `../er/`, `../pie/` or `../gantt/` needs a
golden, or an entry in the exclusions of the script, the test and this file. A golden change in a pull request is
reviewed like code: the diff shows which coordinates moved. Regenerating can change a few numbers by 0.001: the
browser's geometry is single precision, and a value near a rounding boundary can round either way.

## Sequence Goldens

The files in `sequence/` hold the layout that the reference gives each fixture of `../sequence/`. They have the same
provenance, versions, fonts, and extraction as the flowchart goldens. Positions are in the coordinates of the view box,
mapped through `getScreenCTM` (the database and queue drawings are in translated groups), and rounded to 0.001. Arrays
and objects that fit in 120 columns are written on one line.

### Common Values

- **`box`**: `[x, y, width, height]` of `getBBox` (for a group, the union of its drawing, texts included).
- **`line`**: `[x1, y1, x2, y2]`.
- **`paint`**: the computed style, so the CSS of the theme and the cascade are applied. It holds `fill` (left out for
  a line), `stroke`, `strokeWidth` (in px), and `dash` (`stroke-dasharray`) and `filter` when they are set.
- **A text**: `class`, `lines` (the text of each `tspan`, or the text itself), `at` (the `x` and `y` attributes),
  `anchor` (the computed `text-anchor`), `baseline` (the `dominant-baseline` attribute), and the computed `fontSize`,
  `fontWeight`, and `fill` of the first `tspan` (where the theme's text rules apply) or of the text. `box` is given
  when the text is drawn. A label that Mermaid wraps or breaks is several text elements, one per line.

### Sequence Layout Content

Each file holds:

- `diagram`: `sequence`.
- `viewBox`: `[x, y, width, height]`.
- `title`: the title text, or null.
- `actors`: every participant of the database, in declaration order: `name`, `type`, and `description`, then `top`
  and `bottom` (the drawing at the top and the mirrored one at the bottom, or null when not drawn) and `lifeline`
  (`line` and `paint`, or null). A drawing has its `box`, its `parts` (each `rect`, `path`, `circle`, `ellipse`,
  `line`, and `polygon` of the drawing with its `tag`, `box`, `paint`, and marker ids), and its `texts`.
- `messages`: in drawing order. Each has `id` (the `data-id`, `i` and the database index), `from`, `to`, `class`
  (`messageLine0` solid, `messageLine1` dotted), `tag`, `markerStart` and `markerEnd` (the marker ids without the
  render prefix, for example `arrowhead`), `paint`, and `texts` (the texts drawn just before the line). A `line` has
  `line`; a `path` (a self message) has `d`, `length`, and 21 `points` sampled with `getPointAtLength`. `number` is
  the sequence number (its zero-length `line` with the `sequencenumber` marker, its `paint`, and its `text`), or null.
  `circles` are the central-connection circles (`center`, `r`, and `paint`).
- `notes`: `id`, the `box` and `paint` of the note's rect, and its `texts`.
- `activations`: `class` (`activation0` to `activation2`), `box`, and `paint`, in document order.
- `frames`: the loop, alt, opt, par, critical, and break frames: `id`, `lines` (the four sides and the dashed section
  separators, each with its `paint`), `labelBox` (the label polygon's `points` and `paint`), and `texts` (the
  `labelText`, `loopText`, and `sectionTitle` texts).
- `rects`: the backgrounds of `rect` blocks (`box` and `paint`), in document order, which is the reverse of the
  source order because Mermaid lowers each one to the back.
- `boxes`: the `box` groups (`box`, `paint`, and the title `texts`), in document order (also reversed).
- `menus`: the actor popup menus: `id`, `displayed` (false unless `forceMenus` is set), `panel` (`[x, y, width,
  height]` from the attributes, because a hidden menu has no box), `paint` when displayed, and `texts`.
- `order`: the paint order, from back to front: one key per item (`box:i`, `rect:i`, `actor-bottom:name`,
  `lifeline:name`, `actor-top:name`, `activation:i`, `frame:id`, `note:id`, `message:id`, `number:id`,
  `circles:id`, `menu:id`, `title`), where `i` is the index in the list above.

### Sequence Tolerances

`MermaidSequenceReferenceLayoutTests` compares every position and size at **0.01 px**, with two exceptions:

- **Text boxes**, 0.02 px, also for the box of a participant's drawing, which includes its texts: Chrome snaps text
  baselines to its layout unit of 1/64 px, so a text box is 17.016 px high where CanvasNet gives 17. The largest
  difference seen is 0.016 px.
- **Cylinder arcs** (the `queue` and `database` participants), 0.02 px: Chrome's `getBBox` of an arc far from the
  origin can stop short of the ellipse's extreme (the mirrored queue shows 0.017 px), while the geometry is the same.

The test also makes three allowances, documented in the test:

- An empty line, which Mermaid draws as a zero-width space, is a line without runs with the same box.
- The harness lists a mirrored participant's menu panel as an `actor-bottom` key just before its `menu` key, because
  the panel has the box's class; the comparison drops that key.
- The arrowhead of the `control` participant (role `actor-marker`) is left out of the parts and the box, as the
  harness leaves markers out.

### Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded:

| Fixture | Covers |
| --- | --- |
| `01-basic-messages` | Implicit participants, `->>` and `-->>` |
| `02-participants-aliases` | `participant` and `actor`, `as` aliases, declaration order, an implicit participant |
| `03-arrows-classic` | `->`, `-->`, `->>`, `-->>`, `<<->>`, `<<-->>`, `-x`, `--x`, `-)`, and `--)` |
| `04-arrows-half-heads` | The 8 half-head arrows (solid and stick heads, top and bottom), solid and dotted |
| `05-arrows-reverse` | The 8 reverse half-head arrows (the head at the sender), solid and dotted |
| `06-central-connections` | `()` at the end, at the start, and at both ends, with `autonumber` |
| `07-activation-shorthand` | `+` and `-`, stacked and on self messages |
| `08-activations-nested` | `activate` and `deactivate` three deep |
| `09-notes` | `left of`, `right of`, `over` one, `over` two (and reversed), a long note |
| `10-self-messages` | Self messages of several arrows, a note over a self message |
| `11-loop-opt-break` | `loop`, `opt`, and `break` nested, a loop without a title |
| `12-alt-else-nested` | `alt` and `else`, nested |
| `13-par-and-par-over` | `par` and `and`, `par_over` |
| `14-critical-option` | `critical` and `option`, a `critical` alone, the `dark` theme (frontmatter) |
| `15-rect-backgrounds` | `rect` with `rgb()`, `rgba()`, a named color, and `hsl()`, nested |
| `16-boxes` | Colored, title-only, and color-only boxes, a participant outside, the `forest` theme |
| `17-autonumber` | `autonumber`, a start and a step, `off`, decimals, 3 and 4 digits |
| `18-create-destroy` | `create` and `destroy` of participants, an actor, and a database |
| `19-participant-types` | The 8 types with `@{ type }`, the `alias` key, `as` after metadata, `actor` with a type |
| `20-config-keys` | Frontmatter `title`, no mirror, left messages, right notes, right angles, sizes, font sizes |
| `21-wrap-and-breaks` | Top-level `wrap: true`, `wrap:` and `nowrap:`, `<br>` variants, a hyphenated long word |
| `22-title-menus-hidden` | `title`, `accTitle`, `accDescr`, `link` and `links` (hidden menus), hidden participants |
| `23-font-size-override` | Top-level `fontSize: 12`, bold weights, right messages, left notes, `showSequenceNumbers` |
| `24-menus-forced-properties` | `forceMenus`, `properties` with a `class` |

## Class Goldens

The files in `class/` hold the layout that the reference gives each fixture of `../class/`. They have the same
provenance, fonts, and extraction as the flowchart goldens, but were made with puppeteer 25.16.0, the version that
`package-lock.json` pins now; regenerating the flowchart and sequence goldens with it gives the same files. Arrays and
objects that fit in 120 columns are written on one line, as in the sequence goldens.

### Class Layout Content

Each file holds the flowchart content, extended for the diagrams of Mermaid's unified renderer:

- `diagram`: `classDiagram` (the SVG's `aria-roledescription`).
- `title`: the frontmatter title (`class`, `text`, and `box` of the `classDiagramTitleText` text), or null.
- `nodes`: the DOM id (`classId-<name>-<n>` for a class, `note<n>` for a note, and `interface<n>` for a lollipop
  interface), the shape from the diagram's data (`classBox`, `note`, or `rect`), `x`, `y`, and `box` as for a
  flowchart, then `class` (the node group's classes, with the CSS classes of the class) and `parts`. The parts are the
  node's drawing in document order: each `rect`, `path`, `line`, `circle`, `ellipse`, and `polygon` with its `tag`,
  `class`, `box`, and `group`, the class of the node's child group that holds it (`basic label-container outer-path`
  for the box, `divider` for the two divider lines); and each label (`g.label`, or a `text` outside one) with its
  `tag`, `class`, `group` (`annotation-group text`, `label-group text`, `members-group text`, or
  `methods-group text`), `box`, `text`, and its `style` attribute when set (`font-weight: bolder` for the class name,
  `font-style:italic;` or `text-decoration:underline;` for an abstract or static member).
- `clusters`: the namespace id and the `box` of its rectangle, then `class`, `inner` (the box of an inner rectangle,
  or null; a namespace has none), and `label` (`text` and `box` of the cluster label).
- `edges`: as for a flowchart, with `class` (the path's classes, for example `edge-pattern-dashed relation`), and
  `markerStart` and `markerEnd` (the marker ids without the render prefix `my-svg_`, for example
  `classDiagram-extensionStart`, or null).
- `edgeLabels`: as for a flowchart.
- `terminals`: the terminal labels (cardinalities) in document order: `edge` and `end` (`start` for the label next to
  the first class, `end` for the label next to the second), `text`, `x` and `y` (the position of the terminal group),
  and `box`. Mermaid inserts terminal labels concurrently, so their order does not name their edge: the harness
  matches each to the nearest route end of an edge whose terminal text is the same.

### Class Tolerances

`MermaidReferenceLayoutTests` compares every class golden at **0.01 px**: the view box, each node's position and box,
each part of its drawing (a rectangle drawn as a fill path and a stroke path with the same box counts once; the
lines of a label are united), each namespace rectangle and title, each edge as a flowchart edge is compared, each
edge label's center, each terminal label's position, and the title's box. There is one exception:

- **`16-config-title-links`**, 0.23 px. Its class texts are SVG text (`htmlLabels: false`). Chrome's `getBBox` of an
  SVG text does not depend on where the text is: it is the advance, grown to the boxes its font rasterizer gives the
  first and last glyphs, and CanvasNet grows it the same way (the rule of the sequence texts), so `Gateway`,
  `+String name`, the method and `Docs` match at 0.01 px. The rasterizer's box of the bold `d` at 16 px reaches to
  10 px, one pixel further than the rule gives (its outline ends at 8.72 px; the bold `s` at 8.24 px ends at 9 px
  as the rule gives), so `Backend` is 66.922 px wide in Chrome and 66.703 px (the advance) in CanvasNet. The class box
  of `Backend` is 0.219 px narrower, and the nodes and edges laid out beside it move by up to 0.06 px. The goldens are
  read back at a scale of exactly 1: shown at its `max-width`, which is the view box width rounded to 0.001 px, the
  diagram is scaled by about 0.99995, and Chrome then lays the SVG texts out about 0.03 px narrower.

### Class Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded.

| Fixture | Covers |
| --- | --- |
| `01-basic-classes` | Class declarations, an empty class, `:` member lines, an implicit class, a labeled relation |
| `02-members-methods` | Bodies with attributes, methods, parameters, and return types |
| `03-visibility-classifiers` | `+`, `-`, `#`, `~`, `$` (underline), and `*` (italic) on attributes and methods |
| `04-generics` | `~T~` in class names, members, methods, and relations, nested `List~List~int~~` |
| `05-annotations` | `<<interface>>` in a body, as a statement, and inline; two annotations; `<<enumeration>>` |
| `06-relations-all` | The 8 relation forms and their reversed forms, solid and dotted, with labels |
| `07-two-way-relations` | `<\|--\|>`, `*--*`, `o--o`, `<-->`, `<..>`, `*--o`, and `<\|..\|>` |
| `08-cardinality-labels` | Cardinalities at the start, at the end, and at both ends, with and without labels |
| `09-lollipop` | `()--` and `--()` interfaces, `direction LR` |
| `10-namespaces` | Namespaces, dotted and nested namespaces, a namespace label |
| `11-notes` | `note`, `note for`, and notes with `\n` (drawn as written) and `<br/>` |
| `12-direction` | The `classDiagram-v2` header, `direction RL`, a namespace |
| `13-styles` | `classDef`, `classDef default`, `cssClass`, `:::`, and `style` |
| `14-labels-backticks` | `class X["Label"]`, backtick names, a dash in a name, an id that starts with `o` |
| `15-hide-empty-members` | `class.hideEmptyMembersBox: true` with empty classes and an annotation-only class |
| `16-config-title-links` | Frontmatter title, `htmlLabels: false`, `class.padding`, `state` spacing, `click` |

`16-config-title-links` also has `flowchart.curve: linear`, the accessibility texts, `link`, and `callback`. `click`,
`link`, and `callback` have no effect on the drawing, and `state.nodeSpacing` and `state.rankSpacing` are the keys
that space a class diagram.

The class texts are drawn as HTML labels by default (a top-level `htmlLabels` that is not set counts as true), so
only `16-config-title-links` has SVG text labels.

## State Goldens

The files in `state/` hold the layout that the reference gives each fixture of `../state/`. They have the provenance,
fonts, extraction, one-line arrays and objects, and the read-back at a scale of exactly 1 of the class goldens.

### State Layout Content

Each file holds the [class layout content](#class-layout-content), with these differences:

- `diagram`: `stateDiagram` (for the `stateDiagram-v2` and the `stateDiagram` headers alike).
- `title`: the `statediagramTitleText` text of the frontmatter title, or null.
- `nodes`: the DOM id is `state-<id>-<n>` (for a note, `state-<id>----note-<n>`), and the shape is `stateStart`,
  `stateEnd`, `rect`, `rectWithTitle`, `fork`, `join`, `choice`, or `note`. The parts are the drawing as for a class:
  the `circle.state-start` of a start; the rough.js paths of an end (`outer-path`), a fork or join bar, a choice
  diamond, and a note; the `rect.basic label-container` of a state; and the `rect.outer title-state`, the
  `line.divider`, and the label of a state with descriptions. A label drawn in several pieces (the title and the
  descriptions of a state with descriptions) also has `items`: the `tag` (`foreignObject`, or `text` for an SVG label),
  `box`, and `text` of each piece.
- `clusters`: composite states, the regions of a concurrent state, and the groups that hold a state with its note are
  clusters. Each has its DOM id (`state-<id>-<n>`, or `state-<id>----parent-<n>` for a note group), `shape`
  (`roundedWithTitle`, `divider`, or `noteGroup`, from the diagram's data), `box` (the outer or divider rectangle, or
  the note group's invisible rectangle), `class` (for example
  `statediagram-state statediagram-cluster statediagram-cluster-alt`, or `note-cluster`), `inner` (the inner rectangle
  of a composite state), and `label` (the composite state's title).
- `edges`: the transitions (`edge<n>`, class `transition`, marker `stateDiagram-barbEnd`) and the note links
  (`<from>-<to>`, class `transition note-edge`, no marker).
- `terminals`: always empty.

The last region of a concurrent state has an id that Mermaid makes at random (`id-`, a random token, `-`, and a page
counter). The harness writes it as `id-random-<counter>`, in the node, cluster, and DOM ids alike, so that the goldens
do not change from run to run.

### State Tolerances

`MermaidReferenceLayoutTests` compares every state golden at **0.01 px**, with no exception: the view box, each
node's position, box, and parts (as for a class), each cluster's rectangle, inner rectangle, and title, each edge as a
flowchart edge is compared, each edge label's center, and the title's box. The largest difference is 0.003 px. Two
browser rules make this possible:

- **The rough.js circles of an end state** are drawn as rough.js draws them without roughness, so the outer ring is
  14.013 px wide, as in Chrome (the last curve of the outline doubles back past the start point).
- **SVG text runs** advance on Chrome's grid of a 64th of a pixel: a text or `tspan` of its own takes its advance
  rounded up to the grid. Mermaid writes each word of an SVG label as its own `tspan`, so in `13-config-title`
  `waiting for the bank` is 140.766 px wide (140.75 px as one run).

### State Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded.

| Fixture | Covers |
| --- | --- |
| `01-basic-transitions` | `[*]` start and end, transitions with labels |
| `02-descriptions` | `state "d" as s`, `s : text`, several descriptions (`rectWithTitle`), a plain state |
| `03-composite-nested` | Three levels of composite states, each with its own start and end |
| `04-composite-direction` | `direction LR` at the root, `TB`, `LR`, and `BT` in composite states |
| `05-fork-join` | `<<fork>>`, `<<join>>`, `[[fork]]`, and `[[join]]` |
| `06-choice` | `<<choice>>` and `[[choice]]` with labeled branches |
| `07-concurrency` | A composite state with three regions (`--`) |
| `08-notes` | `note left of`, `note right of`, a multi-line `end note`, `<br/>`, a note in a composite state |
| `09-styles` | `classDef`, `class` with one and two states, `:::`, and `style` |
| `10-v1-header` | The `stateDiagram` header (drawn as `stateDiagram-v2`), a composite state, a description |
| `11-self-and-parallel-transitions` | Self transitions and parallel transitions with labels |
| `12-start-end-in-composites` | Start and end states in nested composite states |
| `13-config-title` | Frontmatter title, `htmlLabels: false`, `state` spacing, a description, a composite, a note |
| `14-transitions-into-composites` | Transitions to and from a composite state and into a state inside it |

`13-config-title` also has the accessibility texts, `hide empty description`, `scale`, and `click`, which have no
effect on the drawing. The state texts are drawn as HTML labels by default, so only `13-config-title` has SVG text
labels.

## ER Goldens

The files in `er/` hold the layout that the reference gives each fixture of `../er/`. They have the provenance, fonts,
extraction, one-line arrays and objects, and the read-back at a scale of exactly 1 of the class goldens.

### ER Layout Content

Each file holds the [class layout content](#class-layout-content), with these differences:

- `diagram`: `er`.
- `title`: the `erDiagramTitleText` text of the frontmatter title, or null.
- `nodes`: the DOM id is the entity's id, `entity-<name>-<n>`, and the shape is `erBox`. An entity without attributes
  is drawn as a `rect.basic label-container` with a label. An entity with attributes has the parts of its box: two
  rough.js paths each for the box (`outer-path`) and for each row (`row-rect-odd` or `row-rect-even`), the labels
  (`label name`, then `label attribute-type`, `label attribute-name`, `label attribute-keys`, and
  `label attribute-comment` for each row; an empty cell has an empty box), and two paths for each divider (`divider`:
  the line under the name, a line at each column boundary, and the line under the name again, as Mermaid draws it).
- `clusters`: the subgraphs, with `shape` (`rect`), `class` (`cluster`), and `label` (the title).
- `edges`: the relationships (`id_<entityA>_<entityB>_<n>`, class `relationshipLine`, with `edge-pattern-solid` or
  `edge-pattern-dashed`), and the markers `er-onlyOne`, `er-zeroOrOne`, `er-oneOrMore`, or `er-zeroOrMore`, with
  `Start` or `End`.
- `terminals`: always empty.

### ER Tolerances

`MermaidReferenceLayoutTests` compares every ER golden at **0.01 px**, with no exception: the view box, each node's
position, box, and parts (each text of a table by its own box, an empty text by the point where it is placed), each
subgraph's rectangle and title, each edge as a flowchart edge is compared, each edge label's center, and the title's
box. The largest difference is 0.002 px. Three browser rules make this possible:

- **An empty SVG text counts in a node's box.** With `htmlLabels: false`, the empty keys or comment cell of a table
  is an empty `text` element whose point Chrome's `getBBox` includes, so in `12-config-title` the node `MEMBER` is
  7.5 px wider than its table, and the layout uses that width. An empty HTML cell (a `foreignObject` of no size) does
  not count.
- **An SVG edge label's background is measured in the inherited font size.** Mermaid measures the text before the
  label moves under `.edgeLabel .label` (14 px), so the background rectangle, and with it the label's size, is that of
  the 16 px text, while the text is drawn at 14 px.
- **An SVG edge label is centered by its text as drawn** (the 14 px text), so the label's box, which the rectangle
  makes, sits 1.5 px lower than the layout's label position in `12-config-title`.

### ER Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded.

| Fixture | Covers |
| --- | --- |
| `01-basic-relationships` | Relationships with crow's foot markers, a quoted role |
| `02-cardinality-symbols` | `\|o`, `\|\|`, `}o`, `}\|`, `o\|`, `o{`, and `\|{` in several combinations |
| `03-cardinality-words` | The cardinality words and their short forms, with `to` and `--` |
| `04-identifying-non-identifying` | `--`, `..`, `.-`, `-.`, `to`, and `optionally to` |
| `05-attributes` | Attribute types and names, `varchar(255)`, `string[]`, `decimal?` |
| `06-keys-comments` | `PK`, `FK`, `UK`, key lists, comments, and boxes without the keys or the comment column |
| `07-aliases` | `e["Alias"]` and `e[Alias]`, an alias after a relationship, a second alias (ignored) |
| `08-direction` | `direction LR` |
| `09-styles` | `classDef`, `:::`, `class` with one and two entities, and `style` |
| `10-generics-backticks` | `List~int~`, `Map~string,int~`, backtick names and types, a quoted entity name |
| `11-subgraphs` | Nested subgraphs with a title, `direction LR` in a subgraph with and without outside links |
| `12-config-title` | Frontmatter title, `htmlLabels: false`, the `er` spacing and box settings |

`03-cardinality-words` uses `one or zero`, `zero or one`, `only one`, `one or more`, `one or many`, `zero or more`,
`zero or many`, `many(0)`, `many(1)`, `many`, `1+`, `0+`, and `1`. `12-config-title` sets `er.nodeSpacing`,
`er.rankSpacing`, `er.minEntityWidth`, `er.entityPadding`, and `er.diagramPadding`, and has the accessibility
texts. The ER texts are drawn as HTML labels by default (a top-level `htmlLabels` that is not set counts as true), so
only `12-config-title` has SVG text labels.

## Pie Goldens

The files in `pie/` hold the layout that the reference gives each fixture of `../pie/`. They have the provenance,
fonts, one-line arrays and objects, and the read-back at a scale of exactly 1 of the class goldens, and are written
by `extractPie` of `render-mermaid.mjs`.

### Pie Layout Content

- `diagram`: `pie`.
- `viewBox`: the view box Mermaid sets (`[x, 0, width, height]`; `x` is negative when the title is wider than the
  chart).
- `origin`: the point of the pie group (the outer circle, the slices and their labels), which a top or left legend
  moves away from (225, 225).
- `outerCircle`: the `circle.pieOuterCircle`, with its `center`, `r`, and `paint`.
- `slices`: each `path.pieCircle` in order, with its `class` (`pieCircle`, with `highlighted` for the highlighted
  slice), its path data `d` as d3 writes it (in the coordinates of the pie group), its `box`, and its `paint`.
- `labels`: each percentage label (`text.slice`).
- `title`: the `text.pieTitleText` (empty when the chart has no title).
- `legend`: each `g.legend`, with the point of the group (`at`), the `rect` with its `box` and `paint`, and the
  `text`.

A `paint` holds the computed `fill`, `stroke`, `strokeWidth`, `opacity`, and CSS `scale` (`none`, or `1.05` for the
highlighted slice). A text holds its `class`, `text`, the point of its anchor (`at`), its computed `anchor`,
`fontSize`, `fontWeight`, and `fill`, and its `box`.

### Pie Tolerances

`MermaidChartReferenceLayoutTests` compares every pie golden at **0.01 px**: the view box, the origin, the outer
circle, each slice's path data (the golden's numbers scaled by its CSS `scale`, the arc flags kept), the anchors of
the texts, the legend points and squares, and the paint, with the opacity folded into the colors as CanvasNet draws
them. Text boxes are compared at 0.02 px, as for the sequence goldens, and a slice's box at 0.02 px (Chrome's arc
approximation; the test computes the exact box of the circle arcs). The largest difference is 0.001 px. Two browser
rules make this possible:

- **The legend is placed by the measured text width.** Mermaid takes the widest legend text from
  `getBoundingClientRect` before the view box is set, which is the width of the text's `getBBox`, so the view box
  width is 512 px plus that width for a legend at the right or the left.
- **A color in `hsl()` is converted in single precision.** The default theme's `pie11` is
  `hsl(150, 100%, 56.2745098039%)`, whose blue channel is 143.49999... in double precision and 143.5 in single
  precision, which Chrome rounds to 144.

### Pie Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded.

| Fixture | Covers |
| --- | --- |
| `01-basic` | Three slices, no title (an empty title text), the legend at the right |
| `02-showdata-title-long` | `showData`, a title on the `pie` line wider than the chart (a negative view box `x`) |
| `03-acc-comments` | `title`, `accTitle`, a multi-line `accDescr`, comments, single quotes, escapes |
| `04-small-zero-duplicate` | A section under 1% (in the legend only), a zero value, a repeated label |
| `05-many-slices` | Fourteen sections (the colors wrap after `pie12`), a title |
| `06-legend-left-donut-textposition` | `legendPosition: left`, `donutHole`, `textPosition` |
| `07-legend-top-forest` | `legendPosition: top`, the forest theme, the frontmatter title |
| `08-legend-bottom-highlight-dark-themevariables` | `legendPosition: bottom`, `highlightSlice`, dark, theme variables |

## Gantt Goldens

The files in `gantt/` hold the layout that the reference gives each fixture of `../gantt/`. They have the provenance,
fonts, one-line arrays and objects, and the read-back at a scale of exactly 1 of the pie goldens, and are written by
`extractGantt` of `render-mermaid.mjs`. The page runs in the UTC time zone with its clock fixed at
2024-06-12T10:30:00Z, so the today marker and the dates Mermaid fills in from "today" do not change from run to run;
the test lays each fixture out with `MermaidRenderOptions.Now` set to the same instant.

### Gantt Layout Content

- `diagram`: `gantt`.
- `viewBox`: the view box Mermaid sets (`[0, 0, width, height]`).
- `excludes`: each `rect.exclude-range` (a run of excluded days), with its `corners` and `paint`.
- `axes`: each axis group (`g.grid`: the bottom axis and, with `topAxis`, the top one): the point of the group (`origin`)
  and each tick, with the point of its line (`at`) and the line's other `end`, the opacity of the tick group, the
  `paint` of the line, and its text.
- `sections`: each section background `rect.section`, with its `class`, `corners` and `paint`.
- `tasks`: each task `rect` (and vert marker) in order, with its `id`, `class`, corner radius `rx`, the four
  `corners` (after the CSS transform of a milestone) and the `paint`.
- `texts`: each task text, with its `id` and `class` (which tells inside from outside placement).
- `sectionTitles`: each `text.sectionTitle`, with its `class`, `fill`, `fontSize`, and its `lines` (a section name
  with `<br>` has one line per `tspan`), each with its `text` and `box`.
- `today`: the line of `g.today` with its `start`, `end` and `paint`, or `null` without one.
- `title`: the `text.titleText`.

A `paint` holds the computed `fill`, `stroke`, `strokeWidth` and `opacity`. A text holds its `text`, the point of its
anchor (`at`), its `dy`, its computed `anchor`, `fontSize`, `fontWeight`, `fontStyle` and `fill`, and its `box`.

### Gantt Tolerances

`MermaidChartReferenceLayoutTests` compares every gantt golden at **0.01 px**: the view box, the axis origins and tick
lines, the corners of every rectangle (exclude ranges, sections, tasks and milestones), the today line, the anchors
of the texts, and the paint, with the opacity folded into the colors as CanvasNet draws them. Text boxes are compared
at 0.02 px, as for the other goldens. A rectangle without area (a zero-length bar) is not drawn by the browser and is
skipped. The largest difference is 0.001 px.

### Gantt Fixtures

The fixtures are original and use only text that Liberation Sans covers, so no golden depends on the machine and
none is excluded.

| Fixture | Covers |
| --- | --- |
| `01-basic` | Two sections, `after`, an implicit start, no `dateFormat` (end dates give zero-length bars) |
| `02-dateformat-durations-inclusive` | `dateFormat DD-MM-YYYY`, `inclusiveEndDates`, day, week and hour durations |
| `03-after-until` | `after` and `until` with several ids, a forward reference |
| `04-task-states` | `done`, `active`, `crit` and their combinations |
| `05-milestones-vert` | Milestones (rotated and scaled) and a `vert` marker |
| `06-excludes-includes` | `excludes` with weekends, a date and a weekday, `includes` |
| `07-weekend-friday` | `excludes weekends` with `weekend friday` |
| `08-axisformat-tickinterval-week` | `axisFormat`, `tickInterval 1week`, `weekday monday` |
| `09-topaxis-compact` | `displayMode: compact` and `topAxis` from the frontmatter |
| `10-time-of-day` | `dateFormat HH:mm`, `axisFormat %H:%M`, minute and hour durations |
| `11-click-links` | `click` with `href` and `call` (not drawn as links) |
| `12-today-marker-style` | A `todayMarker` style, the today line at the fixed clock |
| `13-config-theme-br-sections` | The dark theme, bar and font sizes, `useWidth`, a section name with `<br>` |
| `14-text-outside` | Task texts beside short bars (left and right), a title wider than the chart |
| `15-excludes-second-pass` | `excludes weekends` on a Friday task when a forward `after` needs a second pass |
| `16-end-dates-strict` | End dates read strictly: an ISO end that `DD-MM-YYYY` does not read ends at the start |
| `17-dateformat-unix-seconds` | `dateFormat X`: a numeric start is milliseconds, a numeric end is seconds |
| `18-number-section-styles-zero` | `gantt.numberSectionStyles: 0`: the `NaN` section classes that no style rule names |
