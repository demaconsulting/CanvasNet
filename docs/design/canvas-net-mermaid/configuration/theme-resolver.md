### ThemeResolver

![Configuration Structure](MermaidConfigurationView.svg)

The `ThemeResolver` unit is the second software unit in the `Configuration` subsystem. It is the internal static
partial class `ThemeResolver` in the `DemaConsulting.CanvasNet.Mermaid.Configuration` namespace, in
`ThemeResolver.cs` (the theme table, `GetThemeVariables`, the shared helpers and derivation blocks, and the grouped
helper types `ThemeObject` and `Js`), one file per theme - `ThemeResolver.Default.cs`, `ThemeResolver.Base.cs`,
`ThemeResolver.Dark.cs`, `ThemeResolver.Forest.cs`, and `ThemeResolver.Neutral.cs` - `ThemeResolver.Styles.cs`,
which holds the shared base `DiagramStyles` of the style sheets, the base `UnifiedStyles` of the sheets that the shared
rendering units draw with, the flowchart style sheet `FlowchartStyles`, and its typed paints `ShapePaint` and
`TextPaint`, `ThemeResolver.DiagramStyles.cs`, which holds the class, state, and entity relationship style sheets
`ClassStyles`, `StateStyles`, and `ErStyles`, and
`ThemeResolver.SequenceStyles.cs`, which holds the sequence style sheet `SequenceStyles` and its typed paints
`StrokePaint` and `ShadowPaint`, and `ThemeResolver.ChartStyles.cs`, which holds the chart style sheets `PieStyles` with
its typed paint `SlicePaint` and `GanttStyles` with the element description `GanttElement`, the computed style
`GanttComputedStyle`, and the selector `GanttSelector` of its cascade, and the packet style sheet `PacketStyles`, and
`ThemeResolver.MindmapStyles.cs`, which
holds the mindmap style sheet `MindmapStyles`.

#### Purpose

`ThemeResolver` reproduces Mermaid's five built-in themes - `default`, `base`, `dark`, `forest`, and `neutral` - and the
style sheets that Mermaid writes for a flowchart, a sequence diagram, a class diagram, a state diagram, an entity
relationship diagram, a pie chart, a gantt chart, a mindmap, and a packet diagram from them. Given a theme name and the
user's theme variable overrides, it computes every theme variable of every diagram type with the same text as Mermaid's
`getThemeVariables`, and `FlowchartStyles`, `SequenceStyles`, `ClassStyles`, `StateStyles`, `ErStyles`, `PieStyles`,
`GanttStyles`, `MindmapStyles`, and `PacketStyles` turn
the theme variables and the `themeCSS` of a configuration into the paint and fonts the diagram is drawn with.

The unit's behavior is specified by the `CanvasNetMermaid-Configuration-ThemeResolver-*` requirements in
`docs/reqstream/canvas-net-mermaid/configuration/theme-resolver.yaml`.

#### Data Model

- **`ThemeObject`** - a JavaScript object as the theme classes use `this`: string keys in insertion order with
  JavaScript values (`string`, `double`, `bool`, `null`, nested objects, and the `ThemeObject.Undefined` sentinel).
  Reading a missing key gives `Undefined`; `Or` and `Nullish` are `this.key = this.key || fallback` and
  `this.key = this.key ?? fallback`; `ToJson` gives the `JSON.stringify` form (undefined values left out, a number that
  is not finite written as `null`). An instance belongs to one theme computation.
- **`Js`** - the JavaScript semantics the derivations use: `Truthy`, `Or`, `Member` (member access that gives
  `Undefined` on a value that is not an object), `ToNumber`, and the conversions between JSON and JavaScript values.
- **The theme table** - each supported theme is a `ThemeDefinition` of a constructor (the base values of the class),
  an `UpdateColors` (the derivations of `updateColors` in the class's own order), and whether `calculate` first resets
  every `'calculated'` placeholder (only the `default` theme does). `UnsupportedMermaidThemes` lists the newer Mermaid
  themes that are not ported. `MaxColorLimit` (64) caps the color-scale loops.
- **`DiagramStyles`** - the shared base of the style sheets: the rules in cascade order (`Rules`, each a `StyleRule`
  of a normalized selector and a `StyleDeclarationSet`), the cascaded declarations by selector, `Root` (the selector
  of the root rule, Mermaid's `&`), the builder of the global rules of Mermaid's `getStyles`, which every sheet
  starts with, and `Fade` - Mermaid's `fade` (a color with the given opacity through khroma, null when khroma cannot
  read the color), which the flowchart's and the entity relationship diagram's sheets use and which therefore lives in
  the shared base rather than in `FlowchartStyles`.
- **`UnifiedStyles`** - the abstract base of the sheets of the diagrams that Mermaid draws with its unified renderer
  (the flowchart, the class, state, and entity relationship diagrams, and the mindmap), whose shapes, clusters, edges,
  labels, and title the shared rendering units
  draw. It gives those units every selector they read, whichever diagram's sheet wrote it: `Node` and `Cluster`
  (`ShapePaint`: fill, stroke, and stroke width, from `.node rect` and `.cluster rect`), `NodeLabel` (abstract: each
  sheet names the rule its labels are colored by), `EdgeLabel` and `NoteLabel` (the node label's paint unless the
  sheet overrides them), `ClusterLabel` (`.cluster-label span`), `ClusterTitle(html)` (virtual: the paint of a
  subgraph or composite state title as an HTML label or as SVG text, `ClusterLabel` for both unless the sheet colors
  the two differently), `RootText` (the root rule's font and fill, which a text inherits where no rule of its own
  applies), `Title` (the rule of the sheet's `TitleSelector` on
  the root font; a selector without a rule gives the root font and fill), `EdgeLabelBackground` (`.edgeLabel`), the
  selectors of the label backgrounds - `HtmlEdgeLabelBackgrounds` (painted in order over an HTML label's box; a
  flowchart's `.labelBkg` and `.edgeLabel p`) and `SvgEdgeLabelBackground` (a flowchart's `.edgeLabel rect`) -
  `TerminalLabel(html)`, the paint of a relation's terminal label (the size of `.edgeTerminals` on the root font, and
  the edge label's color for an HTML label or the root's fill for an SVG label), and `CentersNodeSvgText` (virtual,
  true by default: whether the sheet centers the SVG text of a node's label on its anchor, as a flowchart's
  `.node .label text { text-anchor: middle }` does; without such a rule the text starts at its anchor), and the
  virtual `NodePaintFor(cssClasses, element)` and `NodeLabelFor(cssClasses, html)`, the paint of a node's shape element
  (`rect`, `circle`, `polygon`, `path`, or `line`) and of its label by the node's classes: by default `Node` for a body
  and the `.node line` rule (which no such sheet has) for a line, and `NodeLabel` for a label, because only a mindmap's
  sheet paints by classes and elements.
- **`FlowchartStyles`** - the rules of the flowchart's style sheet, a `UnifiedStyles`. `KnownSelectors` is the
  set of selectors of the sheet, which `themeCSS` may use. Its labels are colored by `.label`, and its title selector
  is `.flowchartTitleText`. Its own typed properties `EdgeColor`, `EdgeWidth`, `ThickEdgeWidth`, and `MarkerColor`,
  with the inherited `Node`, `Cluster`, `NodeLabel`, `ClusterLabel`, `Title` (`TextPaint`: font family, font size, and
  color), and `EdgeLabelBackground`, resolve the rules the flowchart draws with. An instance is immutable.
- **`ClassStyles`** - the rules of the class diagram's style sheet (`class/styles.js`) after the global rules, a
  `UnifiedStyles` with its own `KnownSelectors`. The rules are those of `styles.js` in their order with the
  declarations that change pixels; the rules of the `neo` look and the icon rules are left out (that look is drawn
  classic and a class diagram has no icons), and a marker rule such as `[id$="-compositionStart"], .composition` is
  kept as its class selector (`.composition`), which matches the same marker element. The class rules give the class
  boxes (`.node rect` and `g.classGroup rect`: `mainBkg` and `nodeBorder`), the dividers (`.divider`, the typed
  `Divider`), the namespaces (`.cluster rect`), the relation lines (`.relation`, `.dashed-line`, and `.dotted-line`),
  the markers (`.composition`, `.dependency`, `.extension`, `.aggregation`, and `.lollipop`: the `lineColor` with
  `!important`, filled with it, `transparent`, or `mainBkg`), and the terminal labels (`.edgeTerminals`, 11 pixels).
  `NodeLabel` and `EdgeLabel` are colored by `.nodeLabel` and `.edgeLabel` (`classText`), `NoteLabel` by
  `.noteLabel .nodeLabel` (`noteTextColor`), and the label backgrounds are `.labelBkg` and `.edgeLabel .label span`
  for an HTML label and `.edgeLabel .label rect` for an SVG label (`mainBkg`). The title selector is
  `.classDiagramTitleText`, the class Mermaid gives the title text, which no rule of the sheet styles (its rule is for
  `.classTitleText`), so the title takes the root font and fill as Mermaid's class title does. An instance is
  immutable.
- **`StateStyles`** - the rules of the state diagram's style sheet (`state/styles.js`) after the global rules, a
  `UnifiedStyles` with its own `KnownSelectors`. The rules are those of `styles.js` in their order, nested rules
  flattened as Mermaid's style compiler writes them, with the declarations that change pixels, the CSS `rx` and `ry`
  of the rectangles included; the rules of the `neo` look are left out (that look is drawn classic), and the note rule
  is written twice, as Mermaid writes it. JavaScript's `||` fallbacks take the second value when the first variable is
  not set or empty (`stateBorder || nodeBorder`, `stateBkg || mainBkg`, `compositeBackground || background`,
  `transitionLabelColor || tertiaryTextColor`, `altBackground || #efefef`, and `strokeWidth || 1`). `NodeLabel` is
  colored by `.nodeLabel` (`stateLabelColor`), `EdgeLabel` by `.label div .edgeLabel` (`transitionLabelColor`, else
  `tertiaryTextColor`), and `NoteLabel` by `.statediagram-note .nodeLabel` (`noteTextColor`); the rule
  `.statediagram .edgeLabel` (red) never applies, because the class `statediagram` is on the root SVG element under
  which every selector is scoped. `ClusterTitle(html)` gives a composite state's title the color of `.cluster-label`
  (`stateLabelColor`) as an HTML label and the root fill as SVG text (no rule of the sheet fills it). The SVG edge
  label background is `.edgeLabel .label rect`, the title selector `.statediagramTitleText` (18 pixels in
  `textColor`), and `CentersNodeSvgText` is false (the sheet has no `text-anchor` rule for node labels). The shape
  library and the edge builder resolve the parts of the state shapes from these rules by their selectors: the start
  circle from `.node circle.state-start` (`specialStateColor`); a state from `.node rect` with the 5 pixel radius of
  `.statediagram-state rect.basic`; the `barb` transition marker, which has no class, from
  `defs [id$="-barbEnd"]` (fill and stroke `transitionColor`), which beats the later `[id$="-barbEnd"]` (`lineColor`)
  by its specificity, while `.marker` does not apply; the composite states, whose clusters are
  `g.statediagram-cluster` rather than `g.cluster` (so `.cluster rect` does not apply), from
  `.statediagram-cluster rect` and `.statediagram-cluster rect.outer` (radius 5) for the outer rectangle and from
  `.statediagram-cluster.statediagram-cluster .inner` (`compositeBackground || background`) or, for a concurrency
  region, `.statediagram-cluster.statediagram-cluster-alt .inner` (`altBackground`, `#f0f0f0` in the default theme;
  the `#efefef` fallback applies only when the variable is not set) for the inner one (radius 0); a concurrency
  divider from `.statediagram-state rect.divider` (dashed 10, 10 and filled `altBackground`) over
  `.statediagram-cluster rect`; the notes from `.statediagram-note rect`; and the links of notes from `.note-edge`
  (dashed 5). The sheet has no `.node path` rule, unlike the flowchart and class sheets, so the rough.js parts of the
  choice, fork and join, note, and end state shapes keep the paint of their attributes. An instance is immutable.
- **`ErStyles`** - the rules of the entity relationship diagram's style sheet (`er/styles.ts`) after the global rules,
  a `UnifiedStyles` with its own `KnownSelectors`. The rules are those of `styles.ts` in their order, nested rules
  flattened, with the declarations that change pixels and the values of the classic look; the per-color sections of
  the `redux-color` themes and the `neo` look rules are left out (those themes are not built in, and the look is drawn
  classic). JavaScript's `??` fallbacks take the second value only when the first variable is not set
  (`clusterBkg ?? mainBkg`, `clusterBorder ?? nodeBorder`, and `titleColor ?? textColor`). `NodeLabel` is colored by
  `.label` (`nodeTextColor`, else `textColor`), and `EdgeLabel` takes that color at the 14 pixels of
  `.edgeLabel .label`. The HTML edge label background is the div's `.labelBkg` (the `tertiaryColor` faded to 0.5
  with `Fade`) alone: the span's `.edgeLabel` holds only a block paragraph, so Chrome shows no background of it; the
  SVG one is `.edgeLabel .label rect`. `ClusterTitle(html)` gives an HTML subgraph title the root font in the page's
  initial black, because no rule colors the `.cluster-label` spans (Chrome draws them black in every theme), and an SVG
  title the fill of `.cluster-label text`
  (`titleColor ?? textColor`). The markers take `.marker`'s `none !important` fill and `lineColor !important` stroke at
  width 1, so a `themeCSS` fill cannot replace them; a zero marker's circles keep the white of their own `fill`
  attribute, because the rule matches the marker element and not the circle. The entity boxes take `.entityBox` and
  `.node rect` (`mainBkg` and `nodeBorder`), the relationship lines `.relationshipLine` (`lineColor`, 1 pixel), the
  non-identifying lines `.edge-pattern-dashed` (dashed 8, 8), and the subgraphs `.cluster rect`; the rows of an
  entity's attribute table are filled by the shape library with the theme's `rowOdd` and `rowEven` variables in turn
  and every line of the table is stroked in the entity's own stroke, else the theme's `nodeBorder` (the sheet has no
  row rule). The title's class `erDiagramTitleText` has no rule, so the title takes the root font and fill, and
  `CentersNodeSvgText` is false (the sheet has no `text-anchor` rule for node labels; the entity box centers a name
  itself). An instance is immutable.
- **`SequenceStyles`** - the rules of the sequence diagram's style sheet (`styles.js`) after the global rules, with
  its own `KnownSelectors`. The resolvers apply the cascade of the rules that match an element over the attributes
  Mermaid writes on it, as a browser does: `Shape` (`ShapePaint`), `Line` (`StrokePaint`: color, width, and dash
  pattern), `TextColor` (the fill of the matching rules, else the attribute, else the fill inherited from the root
  rule), `Shadow` (`ShadowPaint`: the offset, blur, and color of a CSS `drop-shadow()` filter), `Box`, and
  `MessageLine` (`.messageLine0` or `.messageLine1`, over the `line` rule for a straight message). The typed
  properties `Lifeline`, `LoopLine`, `LabelBox`, `BoxShadow`, `MenuPanelShadow`, `ArrowheadColor`, `CrossheadColor`,
  and `SequenceNumberMarkerColor` resolve the rules the sequence renderer draws with. An instance is immutable.
- **`PieStyles`** - the rules of the pie chart's style sheet (`pieStyles.ts`) after the global rules, with its own
  `KnownSelectors`. `Slice(color, highlighted)` gives the `SlicePaint` of a slice: the fill of `.pieCircle` over the
  section's own color (a `themeCSS` fill wins over it), the `pieStrokeColor` stroke at `pieStrokeWidth`, and the
  `pieOpacity` folded into both colors, or for `.pieCircle.highlighted` the opaque colors and the `scale` of 1.05.
  `OuterCircle` is `.pieOuterCircle` (no fill, `pieOuterStrokeColor` at `pieOuterStrokeWidth`); `TitleText`,
  `SliceText`, and `LegendText` are the fonts and colors of `.pieTitleText`, `.slice`, and `.legend text`. An instance
  is immutable.
- **`GanttStyles`** - the rules of the gantt chart's style sheet (`gantt/styles.js`) after the global rules, with its
  own `KnownSelectors`, and a small CSS cascade over them. Every rule of Mermaid is scoped by the diagram's id, so the
  rules that match an element are ordered by the number of their classes and tags, then by their place in the sheet,
  and an `!important` declaration beats every normal one; a rule beats a presentation attribute of the element (the
  `fill` of an axis label), and an inline style beats the normal declarations of the rules. The selectors are classes
  (one or several on one element, such as `.task.clickable` and `.doneText0.taskTextOutsideLeft`) and the
  descendant selectors `.grid .tick`, `.grid .tick text`, and `.grid path` (`GanttSelector`); fill, stroke, stroke
  width and dashes, the font, and the text anchor are inherited, `currentColor` is the inherited `color`, and the
  opacities of an element and its ancestors multiply. `Compute` takes the chain of `GanttElement` values from the svg
  root to the element (tag, classes, attributes, inline style) and gives a `GanttComputedStyle` (fill, stroke, stroke
  width and dashes, opacity, font, weight, style, text anchor, and transform); `Faded` folds the opacity into a
  color. An instance is used by one render on one thread, because it caches the rules that match a chain.
- **`PacketStyles`** - the rules of the packet diagram's style sheet (`packet/styles.ts`) after the global rules, with
  its own `KnownSelectors` (`.packetByte`, `.packetByte.start`, `.packetByte.end`, `.packetLabel`, `.packetTitle`, and
  `.packetBlock`). Its options are the sheet's defaults (`byteFontSize` 10px, `startByteColor`, `endByteColor`,
  `labelColor`, `titleColor`, and `blockStrokeColor` black, `labelFontSize` 12px, `titleFontSize` 14px,
  `blockStrokeWidth` 1, and `blockFillColor` `#efefef`) with the theme variable `packet` merged over them when it is an
  object, each value written as JavaScript's `String` writes it (a number as JavaScript writes it, `null` as `null`, an
  object as `[object Object]`, an array joined with commas). The dark and forest themes set `packet`; a user's
  `themeVariables.packet` replaces the theme's object (Mermaid's `calculate` applies the overrides again after the theme's
  `updateColors`), so the keys it leaves out take the defaults. A font size that is a bare number other than zero is
  dropped, as a browser drops it in an SVG document, so the text inherits the root rule's size. `Block` is the paint
  of `.packetBlock`, inheriting the root rule's fill, stroke, and stroke width where its own declaration is dropped, as
  SVG paint is inherited; `Label` and `Title` are the fonts and colors of `.packetLabel` and `.packetTitle`, and
  `ByteStart` and `ByteEnd` those of `.packetByte` cascaded with `.packetByte.start` or `.packetByte.end`. An instance is
  immutable.
- **`MindmapStyles`** - the rules of the mindmap's style sheet (`mindmap/styles.ts`) after the global rules, a
  `UnifiedStyles` with its own `KnownSelectors`. The rules are those of `styles.ts` in their order, with the
  declarations that change pixels and the values of the classic look: `.edge` with a stroke width of 3; then, for each
  of the `THEME_COLOR_LIMIT` section colors `i` (12 in every theme, capped by `MaxColorLimit`), with `k = i - 1`, the
  fill `cScale<i>` of the `rect`, `path`, `circle`, and `polygon` elements under `.section-k`, the label color
  `cScaleLabel<i>` of its `text` (fill) and `span` (color), the stroke `cScale<i>` of `.section-edge-k`, the stroke width
  `17 - 3i` of `.edge-depth-k`, and the stroke `cScaleInv<i>` 3 pixels wide of the `line` under `.section-k`; then the
  root's fill `git0` and label color `gitBranchLabel0` under `.section-root`, and `.edge`'s fill of none. A negative
  stroke width (`.edge-depth-5` onward) is not valid CSS, so it is left out as a browser drops it, and such an edge
  keeps `.edge`'s 3. No rule strokes a node's shape. **The gradient rule:** when the theme variables `useGradient` and
  `mainBkg` are both truthy (the `base`, `dark`, `forest`, and `neutral` themes; `default` sets `useGradient` false),
  Mermaid's `genGradient` rules come last, and of them only `.section-k line { stroke-width: 0 }` applies to the
  classic look, so those themes draw the line under a default node 0 pixels wide; a theme variable
  `useGradient: false` keeps the 3 pixels. The rules of the `neo` look, the values of the `redux` themes, and the
  `disabled` rules are left out (the look is drawn classic, those themes are not built in, and no node is disabled).
  `NodePaintFor` and `NodeLabelFor` cascade, in sheet order, the `.class element` rules whose class is one of the
  node's classes (`mindmap-node`, `section-k` or `section-root section--1`, and the node's own class): the root's
  `.section-root` rules come after `.section--1`'s, so the root takes `git0`, and a node class that names a later
  section takes that section's color. A node's own class can be styled only by `themeCSS`, so `Create` also accepts a
  `themeCSS` rule `.class element` for each class of the diagram's nodes that is a plain CSS identifier and each
  element a node draws (`rect`, `path`, `circle`, `polygon`, `line`, `text`, and `span`): `.urgent rect` paints a
  `:::urgent` node's rectangle in its place in the sheet, as Mermaid does, while a class no node has, or one that CSS
  would read as another selector (`a.b`), stays reported. The label is the root font with the `span` rules (an HTML
  label's color) or the
  `text` rules (SVG text's fill); `NodeLabel` is the root font and fill, the title selector is the root (a mindmap has
  no title), and `CentersNodeSvgText` is false. An edge's paint comes from the single-class rules of its classes,
  which the edge builder cascades. An instance is immutable.

#### Key Methods

- `GetThemeVariables(string theme, JsonNode? overrides)` - Mermaid's `getThemeVariables`: constructs the theme, runs
  `calculate` (the `default` theme's placeholder reset), copies the overrides in, derives every color in
  `updateColors` with `ColorMath`, and copies the overrides in again so that an overridden derived variable wins.
  Overrides that are not an object are ignored, as `typeof overrides !== 'object'` decides. Every variable of every
  diagram type is computed (shared identical blocks such as the quadrant chart, radar, XY chart, pie text,
  requirement, and git variables are factored into helpers), so the result equals Mermaid's object variable for
  variable.
- `IsTheme(object? theme)` - whether a value names one of the five supported themes (exact letter case).
- `MkBorder` and `ColorLimit` (private) - Mermaid's `mkBorder`, and `THEME_COLOR_LIMIT` as a JavaScript loop bound
  capped at `MaxColorLimit`.
- `FlowchartStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the rules of Mermaid's global
  `getStyles` and the flowchart's `styles.ts` (with `fade`) from the theme variables of the configuration, in their
  order, keeping only the declarations that change pixels (paint, stroke, dash, opacity, font, and the label
  backgrounds) and leaving out a declaration whose value is not valid for its property (as a browser drops it); then
  parses `themeCSS` with `StyleDeclarations.ParseSheet` against `KnownSelectors` and appends its rules, so that a user
  rule overrides the theme's rule of the same selector unless the theme's declaration is `!important`.
- `FlowchartStyles.Resolve(string selector)` - the cascaded declarations of a selector (empty when it has none).
- `ClassStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the same global rules and then the
  rules of the class diagram's `styles.js` from the theme variables, in their order (the legacy `g.classGroup text`
  fill follows JavaScript's `||`: the `nodeBorder` when it is not empty, else `classText`), and appends the `themeCSS`
  rules of the class selectors as `FlowchartStyles.Create` does. The cascade decides, for example, that a user rule of
  `.divider` or `.edgeTerminals` changes the dividers or the terminal label size, while a user marker fill does not
  beat the theme's `!important` marker fill.
- `StateStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the same global rules and then the
  rules of the state diagram's `styles.js` from the theme variables, in their order (with the `||` fallbacks above),
  and appends the `themeCSS` rules of the state selectors as `FlowchartStyles.Create` does; a user rule of
  `.statediagram-cluster rect` or `.transition`, for example, changes the composite state outline or the transition
  lines without a report.
- `ErStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the same global rules and then the rules
  of the entity relationship diagram's `styles.ts` from the theme variables, in their order (with the `??` fallbacks
  above and the faded `.labelBkg`), and appends the `themeCSS` rules of the ER selectors as `FlowchartStyles.Create`
  does; a user rule of `.relationshipLine` changes the lines, while a user `.marker` fill does not beat the theme's
  `!important` one.
- `StateStyles.ClusterTitle(bool html)` and `ErStyles.ClusterTitle(bool html)` - the overrides of the title paint
  described above; the flowchart and class sheets keep `ClusterLabel` for both kinds of title.
- `SequenceStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the same global rules and then the
  rules of the sequence diagram's `styles.js` from the theme variables, in their order, and appends the `themeCSS`
  rules of the sequence selectors as `FlowchartStyles.Create` does. The cascade decides, for example, that the `line`
  rule widens lifelines and block lines to 2 pixels, that a message rule beats it, and that a box (`g rect.rect`) gets
  the theme's node border and drop shadow.
- `SequenceStyles.TryParseFilter(string value, out ShadowPaint? shadow)` - reads the CSS `filter` values that are
  drawn: `none`, or one `drop-shadow()` with an optional color first or last and two or three lengths (a missing blur
  is 0, a missing color is black); any other filter is not drawn.
- `PieStyles.Create(MermaidConfig config, MermaidReportSink? sink)` and `GanttStyles.Create(MermaidConfig config,
  MermaidReportSink? sink)` - build the same global rules and then the rules of `pieStyles.ts` or of the gantt
  `styles.js` from the theme variables, in their order (`titleColor || textColor` for the gantt title), and append the
  `themeCSS` rules of the pie or gantt selectors as `FlowchartStyles.Create` does.
- `GanttStyles.Compute(IReadOnlyList<GanttElement> chain)` - the computed style of the last element of the chain by the
  cascade above; the matched rules of a chain are cached.
- `MindmapStyles.Create(MermaidConfig config, MermaidReportSink? sink, IEnumerable<string>? nodeClasses = null)` -
  builds the same global rules and then the rules of the mindmap's `styles.ts` from the theme variables, in their
  order, with the gradient rules last when `useGradient` and `mainBkg` are truthy, and appends the `themeCSS` rules of
  the mindmap selectors and of the node classes (`nodeClasses`, each node's classes separated by spaces) as
  `FlowchartStyles.Create` does.
- `MindmapStyles.NodePaintFor(string cssClasses, string element)` and `NodeLabelFor(string cssClasses, bool html)` -
  the cascades above: a body's fill, stroke, and stroke width, and a label's font and color.
- `PacketStyles.Create(MermaidConfig config, MermaidReportSink? sink)` - builds the same global rules and then the six
  rules of the packet `styles.ts` from the options (`Options`, the defaults merged with the theme variable `packet`), in
  their order, and appends the `themeCSS` rules of the packet selectors as `FlowchartStyles.Create` does.

**Preconditions and postconditions**: `GetThemeVariables` requires a supported theme name; it returns a new JSON object
of string, number, and Boolean values. The methods are pure functions of their arguments and safe for concurrent use.

#### Error Handling

`GetThemeVariables` throws `ArgumentException` for a theme that is not supported (the resolver checks the name with
`IsTheme` first) and lets the `ColorFormatException` of `ColorMath` propagate when a value the theme derives colors from
is not a color; the exception carries the offending value, so that `MermaidConfigResolver` can drop the variable and
compute the theme again. A hostile `THEME_COLOR_LIMIT` cannot make the loops unbounded; khroma then fails on the
missing scale colors, as in Mermaid. `FlowchartStyles.Create`, `ClassStyles.Create`, `StateStyles.Create`,
`ErStyles.Create`, `PieStyles.Create`, `GanttStyles.Create`, `MindmapStyles.Create`, `PacketStyles.Create`, and
`SequenceStyles.Create` report each `themeCSS` rule of another selector, at-rule, or nested rule as
`mermaid-theme-css-unsupported` at the location of `themeCSS` and do not apply it; in strict mode the sink throws
`MermaidUnsupportedFeatureException` at the first one.

#### Known Differences From Mermaid

- The themes `neo`, `neo-dark`, and the `redux` family of the pinned Mermaid are not ported (`IsTheme` is false for
  them, and the resolver reports them).
- The color-scale loops that `THEME_COLOR_LIMIT` controls are capped at 64 iterations.
- The style sheets keep only the declarations that change pixels; `background-color` is kept for the label
  backgrounds of the theme, but a user `background-color` is reported as `mermaid-style-ignored` by the
  `StyleDeclarations` unit (only the supported CSS subset is applied).
- A `themeCSS` rule whose selector is not one of the diagram's own selectors cannot be applied without a browser and
  is reported as `mermaid-theme-css-unsupported`.
- A CSS `filter` other than `none` or one `drop-shadow()` is not drawn.
- The `neo` look rules of the state and entity relationship sheets and the per-color sections that the
  `redux-color` themes add to the entity relationship sheet are not built, because that look is drawn classic and
  those themes are not ported.
- The opacity of a pie slice, a gantt section row, and the grid ticks is folded into the fill and stroke colors, so
  overlapping parts are not composited as a group; `shape-rendering: crispEdges` is not modeled; the CSS `transform` of
  a milestone and the `scale` of a highlighted slice are given to the renderers, which apply them to the geometry.
- The gantt cascade knows only the selectors of Mermaid's gantt sheet (classes, compound classes, and the three
  descendant selectors); a `themeCSS` rule with any other selector is reported.
- The mindmap sheet is built for the classic look only: the gradient fills, filters, and node strokes of the `neo`
  look and the shapes and colors of the `redux` themes are not built.
- The packet sheet drops a font size without a unit, as Chrome does in an SVG document; the other sheets read such a
  declaration as `StyleDeclarations` reads it.

#### Dependencies

- **`ColorMath`** (this subsystem) - every color derivation (`Adjust`, `Darken`, `Lighten`, `Invert`, `Rgba`, `IsDark`)
  and `ColorFormatException`
- **`StyleDeclarations`** (this subsystem) - `ParseSheet`, `ParseBlock`, `NormalizeSelector`, the value parsers, and
  `StyleDeclarationSet`
- **`MermaidConfigResolver`** (this subsystem) - the `MermaidConfig` that `FlowchartStyles.Create`,
  `ClassStyles.Create`, `StateStyles.Create`, `ErStyles.Create`, `PieStyles.Create`, `GanttStyles.Create`,
  `MindmapStyles.Create`, `PacketStyles.Create`, and `SequenceStyles.Create` read, and `IsTruthy` for the gradient
  rule
- **System root** - `MermaidReportSink` and `MermaidLocation`
- **Core `CanvasNet`** - `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API
- **.NET base class library** - `System.Text.Json` and `System.Collections.Frozen`

#### Callers

`MermaidConfigResolver` computes the theme variables of the site theme and of a theme named by the text; the `Flowchart`
subsystem (`FlowchartModel`) builds the `FlowchartStyles` of a call, the `Class` subsystem (`ClassModel`) the
`ClassStyles`, the `State` subsystem (`StateModel`) the `StateStyles`, and the `Er` subsystem (`ErModel`) the `ErStyles`
of a call, the `Pie` subsystem (`PieRenderer`) the `PieStyles`, the `Gantt` subsystem (`GanttRenderer`) the
`GanttStyles`, the `Mindmap` subsystem (`MindmapRenderer`) the `MindmapStyles`, and the `Packet` subsystem
(`PacketRenderer`) the `PacketStyles` of a render, which they draw with;
the `Rendering` subsystem (`ShapeLibrary`, `EdgeBuilder`, and through
them and for the diagram title `ClusterLayout`) draws with the resolved paints, the rules, and `DiagramStyles.Root` of
any of these sheets through `UnifiedStyles` (`ShapeLibrary` reads `ClusterTitle(html)`, `CentersNodeSvgText`,
`NodePaintFor`, and `NodeLabelFor`, `EdgeBuilder` the font size of `RootText`); and in the `Sequence` subsystem
`SequenceLayout` builds the `SequenceStyles`
of a render and `SequenceRenderer` draws with its resolvers. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ThemeResolverTests.cs` call the unit directly through
`InternalsVisibleTo`, against the vectors of `Fixtures/upstream-vectors/themes.json`.
