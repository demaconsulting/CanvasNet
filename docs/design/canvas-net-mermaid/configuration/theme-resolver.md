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
`StrokePaint` and `ShadowPaint`.

#### Purpose

`ThemeResolver` reproduces Mermaid's five built-in themes - `default`, `base`, `dark`, `forest`, and `neutral` - and
the style sheets that Mermaid writes for a flowchart, a sequence diagram, a class diagram, a state diagram, and an
entity relationship diagram from them. Given a theme name and the user's theme variable overrides, it computes every
theme variable of every diagram type with the same text as Mermaid's `getThemeVariables`, and `FlowchartStyles`,
`SequenceStyles`, `ClassStyles`, `StateStyles`, and `ErStyles` turn the theme variables and the `themeCSS` of a
configuration into the paint and fonts the diagram is drawn with.

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
  (the flowchart and the class, state, and entity relationship diagrams), whose shapes, clusters, edges, labels, and
  title the shared rendering units
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
  `.node .label text { text-anchor: middle }` does; without such a rule the text starts at its anchor).
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
  `.edgeLabel .label`. The HTML edge label backgrounds are the div's `.labelBkg` (the `tertiaryColor` faded to 0.5
  with `Fade`) and then the span's `.edgeLabel`; the SVG one is `.edgeLabel .label rect`. `ClusterTitle(html)` gives an
  HTML subgraph title the root font in the page's initial black, because no rule colors the `.cluster-label` spans
  (Chrome draws them black in every theme), and an SVG title the fill of `.cluster-label text`
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

**Preconditions and postconditions**: `GetThemeVariables` requires a supported theme name; it returns a new JSON object
of string, number, and Boolean values. The methods are pure functions of their arguments and safe for concurrent use.

#### Error Handling

`GetThemeVariables` throws `ArgumentException` for a theme that is not supported (the resolver checks the name with
`IsTheme` first) and lets the `ColorFormatException` of `ColorMath` propagate when a value the theme derives colors from
is not a color; the exception carries the offending value, so that `MermaidConfigResolver` can drop the variable and
compute the theme again. A hostile `THEME_COLOR_LIMIT` cannot make the loops unbounded; khroma then fails on the
missing scale colors, as in Mermaid. `FlowchartStyles.Create`, `ClassStyles.Create`, `StateStyles.Create`,
`ErStyles.Create`, and `SequenceStyles.Create`
report each `themeCSS` rule of another selector, at-rule, or nested rule as `mermaid-theme-css-unsupported` at the
location of `themeCSS` and do not apply it; in strict mode the sink throws `MermaidUnsupportedFeatureException` at the
first one.

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

#### Dependencies

- **`ColorMath`** (this subsystem) - every color derivation (`Adjust`, `Darken`, `Lighten`, `Invert`, `Rgba`, `IsDark`)
  and `ColorFormatException`
- **`StyleDeclarations`** (this subsystem) - `ParseSheet`, `ParseBlock`, `NormalizeSelector`, the value parsers, and
  `StyleDeclarationSet`
- **`MermaidConfigResolver`** (this subsystem) - the `MermaidConfig` that `FlowchartStyles.Create`,
  `ClassStyles.Create`, `StateStyles.Create`, `ErStyles.Create`, and `SequenceStyles.Create` read
- **System root** - `MermaidReportSink` and `MermaidLocation`
- **Core `CanvasNet`** - `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API
- **.NET base class library** - `System.Text.Json` and `System.Collections.Frozen`

#### Callers

`MermaidConfigResolver` computes the theme variables of the site theme and of a theme named by the text; the
`Flowchart` subsystem (`FlowchartModel`) builds the `FlowchartStyles` of a call, the `Class` subsystem
(`ClassModel`) the `ClassStyles`, the `State` subsystem (`StateModel`) the `StateStyles`, and the `Er` subsystem
(`ErModel`) the `ErStyles` of a call; the `Rendering` subsystem (`ShapeLibrary`, `EdgeBuilder`, and through them
and for the diagram title `ClusterLayout`) draws with the resolved paints, the rules, and `DiagramStyles.Root` of
any of these sheets through `UnifiedStyles` (`ShapeLibrary` reads `ClusterTitle(html)` and `CentersNodeSvgText`,
`EdgeBuilder` the font size of `RootText`); and in the `Sequence` subsystem `SequenceLayout` builds the
`SequenceStyles` of a render and `SequenceRenderer` draws with its resolvers. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ThemeResolverTests.cs` call the unit directly through
`InternalsVisibleTo`, against the vectors of `Fixtures/upstream-vectors/themes.json`.
