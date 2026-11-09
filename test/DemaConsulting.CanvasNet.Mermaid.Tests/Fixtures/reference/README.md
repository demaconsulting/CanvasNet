# Mermaid Reference Layouts

The `*.layout.json` files in this folder are **goldens**: the layout that the reference implementation gives each
flowchart fixture of `../flowchart/`. `MermaidReferenceLayoutTests` compares CanvasNet's layout with them. The
`sequence/` subfolder holds the goldens of the sequence fixtures of `../sequence/`; their format is described in
[Sequence Goldens](#sequence-goldens). The `class/` subfolder holds the goldens of the class fixtures of `../class/`;
their format is described in [Class Goldens](#class-goldens).

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
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1                        # every fixture of every type
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1 -Fixture 25-subgraph-basic
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1 -Diagram sequence      # the sequence goldens only
pwsh benchmarks/visual-diff/update-mermaid-goldens.ps1 -Diagram class         # the class goldens only
```

A new fixture in `../flowchart/`, `../sequence/` or `../class/` needs a golden, or an entry in the exclusions of the
script, the test and this file. A golden change in a pull request is reviewed like code: the diff shows which
coordinates moved. Regenerating can change a few numbers by 0.001: the browser's geometry is single precision, and a
value near a rounding boundary can round either way.

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
  as the rule gives), so `Backend` is 66.922 px wide in Chrome and 66.695 px (the advance) in CanvasNet. The class box
  of `Backend` is 0.227 px narrower, and the nodes and edges laid out beside it move by up to 0.06 px. The goldens are
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
