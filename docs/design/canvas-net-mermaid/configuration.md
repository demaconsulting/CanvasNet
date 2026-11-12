## Configuration

![Configuration Structure](MermaidConfigurationView.svg)

### Overview

The `Configuration` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Configuration`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Configuration/`) turns the site configuration of the caller, the frontmatter, and
the directives of a diagram text into the effective configuration of one measure or render call, and the theme of that
configuration into the paint and fonts every flowchart, sequence diagram, class diagram, state diagram, entity
relationship diagram, pie chart, gantt chart, mindmap, and packet diagram element class is drawn with, as the pinned
Mermaid release computes them. It is **internal**: its types are visible only to the other subsystems of the system and,
through `InternalsVisibleTo`, to the test assembly. It contains four units:

- **MermaidConfigResolver** - the internal static `MermaidConfigResolver` (a partial class in
  `MermaidConfigResolver.cs`, `MermaidConfigResolver.Defaults.cs`, `MermaidConfigResolver.Merge.cs`, and
  `MermaidConfigResolver.Validation.cs`) and the typed view of its result, `MermaidConfig` with `FlowchartConfig`,
  `SequenceConfig`, `ClassConfig`, `StateConfig`, `ErConfig`, `PieConfig`, `GanttConfig`, `MindmapConfig`,
  `PacketConfig`, `MermaidFontConfig`, and `ConfigScalar`
  (`MermaidConfig.cs`): the layers of the configuration in Mermaid's order with Mermaid's merge rules, sanitizers,
  secure keys, and theme recomputation, and the located reports of the keys and values that have no effect; see
  _MermaidConfigResolver Unit Design_ (`configuration/mermaid-config-resolver.md`)
- **ThemeResolver** - the internal static `ThemeResolver` (a partial class in `ThemeResolver.cs` and one file per
  theme, `ThemeResolver.Default.cs`, `.Base.cs`, `.Dark.cs`, `.Forest.cs`, and `.Neutral.cs`), the JavaScript object
  model `ThemeObject` and `Js`, the shared base `DiagramStyles`, the base `UnifiedStyles` of the diagrams that the
  shared rendering units draw, and the flowchart style sheet `FlowchartStyles` with `ShapePaint` and `TextPaint`
  (`ThemeResolver.Styles.cs`), the class, state, and entity relationship style sheets `ClassStyles`, `StateStyles`,
  and `ErStyles` (`ThemeResolver.DiagramStyles.cs`), and the sequence style sheet `SequenceStyles` with `StrokePaint`
  and `ShadowPaint` (`ThemeResolver.SequenceStyles.cs`), and the chart style sheets `PieStyles` with `SlicePaint` and
  `GanttStyles` with `GanttElement` and the packet style sheet `PacketStyles` (`ThemeResolver.ChartStyles.cs`), and the
  mindmap style sheet `MindmapStyles`
  (`ThemeResolver.MindmapStyles.cs`): the five built-in themes and the style rules
  Mermaid writes for a flowchart, a sequence diagram, a class diagram, a state diagram, an entity relationship
  diagram, a pie chart, a gantt chart, a mindmap, and a packet diagram; see
  _ThemeResolver Unit Design_ (`configuration/theme-resolver.md`)
- **ColorMath** - the internal static `ColorMath` (`ColorMath.cs` and `ColorMath.Parsers.cs`, with the channel model
  `Channels` and the exception `ColorFormatException`): the subset of the khroma color library that the themes derive
  their colors with; see _ColorMath Unit Design_ (`configuration/color-math.md`)
- **StyleDeclarations** - the internal static `StyleDeclarations` with `StyleDeclaration`, `StyleDeclarationSet`, and
  `StyleRule` (`StyleDeclarations.cs`): the CSS subset of the `style`, `classDef`, and `linkStyle` statements and of
  `themeCSS`; see _StyleDeclarations Unit Design_ (`configuration/style-declarations.md`)

The subsystem's namespace documentation is in `Configuration/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Configuration-*` requirements in `docs/reqstream/canvas-net-mermaid/configuration.yaml`. The SysML2
model names the subsystem `MermaidConfiguration` (see _CanvasNetMermaid System Design_).

### Interfaces

**Exposed** (internal only):

- `MermaidConfigResolver.Resolve(PreprocessedSource, string?, MermaidRenderOptions?, MermaidReportSink?)` returning a
  `MermaidConfig`, and the helpers `CreateDefaults`, `ConfigKeys`, `DefaultSecureKeys`, `DefaultFontFamily`,
  `AssignWithDepth`, `LodashMerge`, `SanitizeDirective`, `Sanitize`, `SanitizeCss`, and `TryParseJson`;
- `MermaidConfig` - `Raw`, `Theme`, `ThemeVariables`, `ThemeVariable(name)`, `FontFamily`, `FontSize`, `Look`,
  `Layout`, `ThemeCss`, `HtmlLabels`, `MarkdownAutoWrap`, `Wrap`, `Flowchart` (a `FlowchartConfig`), `Sequence` (a
  `SequenceConfig` with the fonts of the sequence renderer as `MermaidFontConfig` values), `Class` (a `ClassConfig`
  with the class section and the spacing the class renderer reads from the state section), `State` (a `StateConfig`
  with the default renderer, the spacing, and the title margin of the state section), `Er` (an `ErConfig` with the
  entity box paddings, the minimum entity width, the spacing, and the title margin of the `er` section), `Pie` (a
  `PieConfig` with the text position, the donut hole, the legend position, and the highlighted slice), `Gantt` (a
  `GanttConfig` with the margins, bar sizes, paddings, font sizes, section styles, axis format, tick interval, top
  axis, display mode, and width of the `gantt` section), `Mindmap` (a `MindmapConfig` with the padding, the maximum
  node width, the layout algorithm, and `useMaxWidth` of the `mindmap` section), `Packet` (a `PacketConfig` with the row
  height, the bit width, the bits per row, `showBits`, the paddings, and the vertical padding the renderer uses),
  `DetectorSettings`, and `LocationOf(key)`;
- `ThemeResolver.GetThemeVariables(theme, overrides)`, `ThemeResolver.IsTheme`, and
  `ThemeResolver.UnsupportedMermaidThemes`;
- `UnifiedStyles`, the base of the flowchart, class, state, entity relationship, and mindmap style sheets, with the
  typed paints
  `Node`, `Cluster`, `NodeLabel`, `EdgeLabel`, `NoteLabel`, `ClusterLabel`, `ClusterTitle(html)`, `RootText`, `Title`,
  `EdgeLabelBackground`, and `TerminalLabel(html)`, the paints of a node by its classes and the SVG element its shape
  draws, `NodePaintFor(cssClasses, element)` and `NodeLabelFor(cssClasses, html)` (the node rule and label of the sheet
  unless the sheet paints by classes), the label background selectors `HtmlEdgeLabelBackgrounds` and
  `SvgEdgeLabelBackground`, the text fact `CentersNodeSvgText`, and `Resolve(selector)`;
- `FlowchartStyles.Create(MermaidConfig, MermaidReportSink?)` with the flowchart's own typed paints `EdgeColor`,
  `EdgeWidth`, `ThickEdgeWidth`, and `MarkerColor`;
- `ClassStyles.Create(MermaidConfig, MermaidReportSink?)` with the class diagram's own typed paint `Divider`;
- `StateStyles.Create(MermaidConfig, MermaidReportSink?)` and `ErStyles.Create(MermaidConfig, MermaidReportSink?)`,
  whose further paints (the state shapes, clusters, and `barb` marker; the entity boxes, rows, and ER markers) the
  rendering units resolve from the rules by their selectors;
- `SequenceStyles.Create(MermaidConfig, MermaidReportSink?)` with the resolvers `Shape`, `Line`, `TextColor`,
  `Shadow`, `Box`, and `MessageLine`, the typed paints `Lifeline`, `LoopLine`, `LabelBox`, `BoxShadow`,
  `MenuPanelShadow`, `ArrowheadColor`, `CrossheadColor`, and `SequenceNumberMarkerColor`, and `TryParseFilter`;
- `PieStyles.Create(MermaidConfig, MermaidReportSink?)` with `Slice(color, highlighted)` (a `SlicePaint`),
  `OuterCircle`, `TitleText`, `SliceText`, and `LegendText`;
- `GanttStyles.Create(MermaidConfig, MermaidReportSink?)` with `Compute(path)`, the style of an element from the classes
  and attributes of the element and its ancestors (`GanttElement`) through a class-set cascade;
- `MindmapStyles.Create(MermaidConfig, MermaidReportSink?, IEnumerable<string>?)`, a `UnifiedStyles` whose
  `NodePaintFor` and `NodeLabelFor` cascade the `.class element` rules of a node's classes in sheet order, with its
  `KnownSelectors` and the `.class element` selectors of the diagram's node classes for `themeCSS`;
- `PacketStyles.Create(MermaidConfig, MermaidReportSink?)` with `Block` (a `ShapePaint`), `Label`, `Title`,
  `ByteStart`, and `ByteEnd`, and its options `Options(JsonNode?)`;
- `ColorMath` (`Adjust`, `Darken`, `Lighten`, `Invert`, `Mix`, `Rgba`, `Channel`, `Luminance`, `IsDark`, `IsColor`);
- `StyleDeclarations.ParseItems`, `ParseBlock`, `ParseSheet`, `NormalizeSelector`, and the value parsers
  `TryParsePaint`, `TryParseLength`, `TryParseDashArray`, `TryParseOpacity`, `TryParseBold`, and `TryParseItalic`,
  and `StyleDeclarationSet.Cascade`, `Paint`, and `Length`.

**Consumed**: from the `Syntax` subsystem, the `PreprocessedSource` of the `Preprocessor` (frontmatter, directives,
and their locations), the YAML nodes of the `YamlSubsetParser`, and `DiagramDetector` (`Detect`, `EditDistance`,
`DetectorSettings`); the public `MermaidRenderOptions` (`Theme`, `Config`, `ThemeNames`), `MermaidLocation`,
`MermaidDiagnosticCodes`, `MermaidUnsupportedFeatureException`, and the internal `MermaidReportSink` of the system
root; the core `CanvasNet` `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API; and
`System.Text.Json` of the .NET base class library.

**Consumers**: `MermaidDocument` resolves the configuration without reports when a text is opened (to give the
detector its settings and the class parser the text's own `class.hierarchicalNamespaces`) and with reports for every
measure or render call, and builds the `FlowchartStyles` of the call; the `Flowchart` subsystem (`FlowchartModel`)
reads the typed configuration (and reports an unsupported look or layout at `LocationOf`) and the styles; the
`Sequence` subsystem (`SequenceModel`, `SequenceLayout`, and `SequenceRenderer`) reads the sequence section and builds
and draws with the `SequenceStyles` of the call; the `Class` subsystem (`ClassModel`) reads the class section (the
namespace and note paddings, `hierarchicalNamespaces`, the spacing, and the title margin), reports an unsupported look
or layout at `LocationOf`, and builds the `ClassStyles` of the call; the `State` subsystem (`StateModel`) reads the
state section (the spacing, the title margin, and the default renderer it names when it reports the legacy renderer)
and builds the `StateStyles` of the call; the `Er` subsystem (`ErModel`) reads the `er` section (the spacing and the
title margin) and builds the `ErStyles` of the call; the `Pie` subsystem (`PieRenderer`) reads the `pie` section and
builds the `PieStyles` of the call; the `Gantt` subsystem (`GanttRenderer`) reads the `gantt` section and builds the
`GanttStyles` of the call; the `Mindmap` subsystem (`MindmapRenderer`) reads the `mindmap` section, the look, and the
layout (and reports an unsupported look or layout at `LocationOf`) and builds the `MindmapStyles` of the call, which
the shared shapes and edges draw with; the `Packet` subsystem (`PacketRenderer`) reads the `packet` section and builds the
`PacketStyles` of the call; the `Rendering` subsystem reads the configuration
through its `LayoutData` (`ClusterLayout` the subgraph margins), `ShapeLibrary` reads the class section for a class box
(its padding and `hideEmptyMembersBox`) and the `er` section for an entity box (its two paddings and the minimum
entity width), and `ShapeLibrary`, `EdgeBuilder`, and `LabelBuilder` parse the `style`,
`classDef`, and `linkStyle` declarations and inline label styles with `StyleDeclarations` and draw with the resolved
paints of the call's `UnifiedStyles` (`ClusterLayout` the diagram title; `ShapeLibrary` the subgraph and composite
state titles through `ClusterTitle(html)` and the SVG node text through `CentersNodeSvgText`; `EdgeBuilder` the SVG
edge labels in the size of `RootText`).

### Design

**A literal port of Mermaid's configuration pipeline.** `MermaidConfigResolver.Resolve` follows the calls that the
command line tool of Mermaid makes before it draws: `initialize` with the theme option and the site configuration
(`setSiteConfig`), then `processFrontmatter`, `detectInit` and the `wrap` directive, lodash's `merge` of the two
text layers, `addDirective`, and `updateCurrentConfig`, which computes the theme again only when the text names a
theme. The flowchart's `init` then sets the layout (ELK for an ELK flowchart) and copies `arrowMarkerAbsolute`; for a
mindmap the layout step of the mindmap database's `getData` sets the layout to `cose-bilkent` unless the user's own
configuration (the site configuration, the frontmatter, and the directives, not the defaults) sets one; for a
sequence diagram the typed `SequenceConfig` applies the `wrap` setting of the diagram's `init` and the font overrides
of the sequence renderer's `setConf` (the top-level font family and size replace the section's own).
Configurations are kept as JSON objects with JavaScript semantics (truthiness, `null` against missing), so the merge
quirks of Mermaid reproduce exactly. Each layer is validated before the merge, so that every report is located at the
frontmatter key or at the directive that caused it; the silent sanitizers of Mermaid then run on the merged layer.

**Themes derived as Mermaid derives them.** `ThemeResolver` ports each `theme-*.js` class to a constructor and an
`updateColors` in the class's own derivation order, over a `ThemeObject` that behaves as the JavaScript `this` of the
class. Every color is derived with `ColorMath`, the port of khroma, so a derived color has the same text as in
Mermaid. `FlowchartStyles`, `ClassStyles`, `StateStyles`, `ErStyles`, and `SequenceStyles` share the global rules of
Mermaid's `getStyles` (`DiagramStyles`), add the flowchart's `styles.ts`, the class diagram's `styles.js`, the state
diagram's `styles.js`, the entity relationship diagram's `styles.ts`, or the sequence diagram's `styles.js` from the
theme variables as selector-keyed rules, append the `themeCSS` rules of the same selectors (parsed
by `StyleDeclarations`), and resolve the cascade into typed paints for the drawing; the sequence resolvers apply the
cascade over the attributes that Mermaid writes on each element, as a browser does. The flowchart and the class, state,
and entity relationship diagrams are drawn by the same rendering units, which read every sheet only through
`UnifiedStyles`: each sheet supplies its title selector, the rule its labels are colored by, the selectors of its label
backgrounds, the paint of a subgraph or composite state title as an HTML label and as SVG text (`ClusterTitle(html)`),
and whether it centers the SVG text of a node's label on its anchor (`CentersNodeSvgText`: the flowchart and class
sheets do, the state and entity relationship sheets have no such rule); `RootText` gives the root font and fill that a
text inherits where no rule of its own applies. The class and entity relationship diagrams' titles have a CSS class
that no rule of the sheet styles, so they take the root font and fill, as in Mermaid; the state diagram's title takes
the 18 pixels of `.statediagramTitleText`. `PieStyles` and `GanttStyles` are not unified sheets, because pie and gantt
charts have their own renderers: `PieStyles` resolves the few pie rules into typed paints, and `GanttStyles`, whose
rules select by compound classes (`.doneText0.taskTextOutsideRight`), descendants (`.grid .tick text`), and
`!important` fills, computes the style of each element from its own classes and those of its ancestors as a browser's
cascade does (important declarations, then specificity, then the order of the rules, over the element's presentation
attributes), with the `themeCSS` rules of the known gantt selectors after them. `MindmapStyles` is a unified sheet,
because a mindmap is drawn by the shared shapes and edges, but its rules select by a node's classes and the element
its shape draws (`.section-2 rect`, `.section-root text`, `.section-3 line`), so it overrides `NodePaintFor` and
`NodeLabelFor` to cascade those rules over the node's classes in sheet order; the edges take the single-class rules of
their classes (`.section-edge-k`, `.edge-depth-n`, and `.edge`) through the edge builder's cascade. The mindmap
sheet's negative `stroke-width` declarations (`.edge-depth-5` onward) are dropped as a browser drops them, and the
gradient rules that the themes with `useGradient` and `mainBkg` append make the line under a default node 0 pixels
wide. `PacketStyles` is not a unified sheet either: it builds its six rules from the sheet's default options merged
with the theme variable `packet` (an object that replaces the theme's own, as Mermaid's `calculate` re-applies the
user's overrides; each value written as JavaScript's `String` writes it), drops a font size without a unit as the
browser drops it, and resolves the paint of a block (inheriting the root rule's paint, as SVG paint is inherited,
where its own is dropped) and the fonts and colors of the labels, the bit numbers, and the title.

**Checked against upstream vectors.** The fixture `Fixtures/upstream-vectors/themes.json`, generated from the pinned
Mermaid and khroma by `tools/visual-diff/mermaid/gen-theme-vectors.mjs`, records 622 khroma calls, 53
configuration cases (each theme without overrides, with theme variables and in dark mode, the theme switches of
directives, and precedence, sanitizing, secure-key, wrap, ELK, and HTML-label cases), Mermaid's configuration keys,
and the global and flowchart defaults; the unit tests compare the ports with them value for value. The sequence
defaults and style rules are checked by the unit tests against Mermaid's schema and style sheet and, through the paint
of every element, by the sequence reference layout goldens; the class section and the class style rules are checked in
the same way against Mermaid's default configuration, its class style sheet, and the class reference layout goldens,
and the state and `er` sections and the state and entity relationship style rules against Mermaid's schema, its state
and entity relationship style sheets, and the state and entity relationship reference layout goldens; the `pie` and
`gantt` sections and the pie and gantt rules likewise against Mermaid's schema, its `pieStyles.ts` and gantt
`styles.js`, and the computed paint recorded in the pie and gantt reference layout goldens; and the `mindmap` section
and the mindmap rules against Mermaid's schema, its mindmap `styles.ts`, and the computed paint of every node, line,
label, and edge recorded in the mindmap reference layout goldens; and the `packet` section and the packet rules against
the effective packet configuration of the packet vectors, Mermaid's packet `styles.ts`, and the computed paint of every
block and text recorded in the packet reference layout goldens.

**Attribution.** `MermaidConfigResolver` ports `config.ts`, `mermaidAPI.ts` (`initialize`), `assignWithDepth.ts`,
`utils.ts` (`detectInit`, `cleanAndMerge`), `preprocess.ts` (`processFrontmatter`), `utils/sanitizeDirective.ts`,
`defaultConfig.ts`, the defaults of `config.schema.yaml`, the flowchart's `init` of `flowDiagram.ts`, the sequence
diagram's `init` of `sequenceDiagram.ts` and `setConf` of `sequenceRenderer.ts`, and the class section of
`defaultConfig.ts` with the state section's spacing that `classRenderer-v3-unified.ts` reads, and the state and `er`
sections of `config.schema.yaml` with the reads of `stateRenderer-v3-unified.ts` and `erRenderer-unified.ts`, and the
`pie` and `gantt` sections of `config.schema.yaml`, and the `mindmap` section of `config.schema.yaml` with the layout
step of the mindmap database's `getData`, and the `packet` section of `config.schema.yaml` with the `getConfig` of the
packet database.
`ThemeResolver` ports `themes/theme-default.js`, `theme-base.js`, `theme-dark.js`, `theme-forest.js`,
`theme-neutral.js`, `theme-helpers.js`, `erDiagram-oldHardcodedValues.ts`, the global `styles.ts`, the flowchart's
`styles.ts`, the sequence diagram's `styles.js`, the class diagram's `styles.js`, the state diagram's `styles.js`,
the entity relationship diagram's `styles.ts`, the pie chart's `pieStyles.ts`, the gantt chart's `styles.js`, the
mindmap's `styles.ts`, and the packet diagram's `styles.ts`.
`ColorMath` ports the parsers, channel model, and functions of
khroma. `StyleDeclarations` is original work of this repository. The source files that contain ported code carry a
`Portions derived from` header, and the provenance is recorded in `third-party/README.md` of the package.

**Deterministic and bounded.** No regular expression runs on configuration text; khroma's expressions are scanners
with the same matches, linear in the length of the text. Every loop that a value controls is bounded: the JSON depth
of a directive (64), the color-scale loops of a theme (`THEME_COLOR_LIMIT` capped at 64), the repeated theme
computations after a dropped variable (16), and the nearest-key hints of unknown keys (32). Every type is stateless,
immutable, or used by one call on one thread, and numbers are formatted with the invariant culture as JavaScript
formats them.
