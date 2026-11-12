### MermaidConfigResolver

![Configuration Structure](MermaidConfigurationView.svg)

The `MermaidConfigResolver` unit is the first software unit in the `Configuration` subsystem. It is the internal static
partial class `MermaidConfigResolver` in four source files of the `DemaConsulting.CanvasNet.Mermaid.Configuration`
namespace - `MermaidConfigResolver.cs` (the pipeline of `Resolve`, the frontmatter and directive layers, and the theme
computation with recovery), `MermaidConfigResolver.Defaults.cs` (the defaults, the configuration keys, and the default
secure keys), `MermaidConfigResolver.Merge.cs` (the JSON operations: `assignWithDepth`, lodash's `merge`, the
sanitizers, and the directive JSON parser), and `MermaidConfigResolver.Validation.cs` (the validation and the reports of
one layer) - and the grouped typed view of its result in `MermaidConfig.cs`: the sealed class `MermaidConfig`, the
records `FlowchartConfig`, `SequenceConfig`, `ClassConfig`, `StateConfig`, `ErConfig`, `PieConfig`, `GanttConfig`, and
`MindmapConfig`, the font record
`MermaidFontConfig`, and the value `ConfigScalar` (a number or a string, kept as JavaScript holds it).

#### Purpose

`MermaidConfigResolver` computes the effective configuration of one measure or render call exactly as Mermaid builds
its current configuration before it draws a diagram, and reports, located, every part of the text's configuration that
has no effect. The layers, lowest first, are: Mermaid's defaults with the `default` theme's variables; the theme option
and the site configuration of the caller (`MermaidRenderOptions.Theme` and `MermaidRenderOptions.Config`), which play
`mermaid.initialize` as the Mermaid command line tool calls it (a `theme` in the configuration wins over the option);
the `config` of the frontmatter; and the `init` and `initialize` directives in text order. A `wrap` directive sets
`wrap`. `MermaidConfig` gives the later stages typed access to the keys this version reads.

The unit's behavior is specified by the `CanvasNetMermaid-Configuration-MermaidConfigResolver-*` requirements in
`docs/reqstream/canvas-net-mermaid/configuration/mermaid-config-resolver.yaml`.

#### Data Model

- **Configurations** are `System.Text.Json` `JsonObject` values read with JavaScript semantics: numbers are doubles,
  truthiness follows JavaScript (`IsTruthy`), and a JSON `null` is distinct from a missing key, so Mermaid's merge
  quirks (a `null` in a directive cancels the frontmatter's value under lodash's `merge`, but is skipped by
  `assignWithDepth`) are kept. A new object is built for every call because a JSON object is not safe for concurrent
  reads.
- **Defaults** (`CreateDefaults`): the global keys, the `flowchart` section, the `sequence` section (the defaults
  of Mermaid's `SequenceDiagramConfig`), the `class` section, the `state` section, the `er` section, the `pie` section,
  the `gantt` section, and the `mindmap` section of Mermaid's
  `config.schema.yaml` and `defaultConfig.ts`, with the variables of the `default` theme. The class section is the one
  `defaultConfig.ts` writes in place of the schema's: `defaultRenderer` (`dagre-wrapper`), `hideEmptyMembersBox`
  (false), and `hierarchicalNamespaces` (true), with `padding` deliberately left undefined so that each use keeps its
  own fallback. The state section (`CreateStateDefaults`) is the whole of the schema's `StateDiagramConfig`, 18 keys:
  `useMaxWidth` (true), `titleTopMargin` (25), `dividerMargin` (10), `sizeUnit` (5), `padding` (8), `textHeight`
  (10), `titleShift` (-15), `noteMargin` (10), `forkWidth` (70), `forkHeight` (7), `miniPadding` (2),
  `fontSizeFactor` (5.02), `fontSize` (24), `labelHeight` (16), `edgeLengthFactor` (the string `"20"`),
  `compositTitleSize` (35), `radius` (5), and `defaultRenderer` (`dagre-wrapper`); `nodeSpacing` and `rankSpacing`
  have no default in the schema and are absent. The `er` section (`CreateErDefaults`) is the whole of the schema's
  `ErDiagramConfig`, 12 keys: `useMaxWidth` (true), `titleTopMargin` (25), `diagramPadding` (20), `layoutDirection`
  (`TB`), `minEntityWidth` (100), `minEntityHeight` (75), `entityPadding` (15), `nodeSpacing` (140), `rankSpacing`
  (80), `stroke` (`gray`), `fill` (`honeydew`), and `fontSize` (12). The `pie` section (`CreatePieDefaults`) is the
  whole of the schema's `PieDiagramConfig`, 5 keys: `useMaxWidth` (true), `textPosition` (0.75), `donutHole` (0),
  `legendPosition` (`right`), and `highlightSlice` (empty). The `gantt` section (`CreateGanttDefaults`) is the whole
  of the schema's `GanttDiagramConfig`, 15 keys: `useMaxWidth` (true), `titleTopMargin` (25), `barHeight` (20),
  `barGap` (4), `topPadding` (50), `rightPadding` (75), `leftPadding` (75), `gridLineStartPadding` (35), `fontSize`
  (11), `sectionFontSize` (11), `numberSectionStyles` (4), `axisFormat` (`%Y-%m-%d`), `topAxis` (false),
  `displayMode` (empty), and `weekday` (`sunday`); `tickInterval` and `useWidth` have no default and are absent. A
  frontmatter `displayMode` moves to `gantt.displayMode`, as in Mermaid. The `mindmap` section
  (`CreateMindmapDefaults`) is the whole of the schema's `MindmapDiagramConfig`, 4 keys: `useMaxWidth` (true),
  `padding` (10), `maxNodeWidth` (200), and `layoutAlgorithm` (`cose-bilkent`). `DefaultFontFamily` is Mermaid's font
  family list.
- **Configuration keys** (`ConfigKeys`, a frozen set of 705 names): every key that Mermaid's `sanitizeDirective`
  accepts (the keys of every diagram section and of the default theme variables); a key of the text outside the set is
  deleted by Mermaid and reported here as unknown.
- **Secure keys** (`DefaultSecureKeys`): `secure`, `securityLevel`, `startOnLoad`, `maxTextSize`,
  `suppressErrorRendering`, and `maxEdges`, replaced by the `secure` list of the site configuration when it has one.
- **Typed keys** (`TypedKeys` of the validation): the kind of value (text, number, Boolean, object, or a number or a
  string for the font sizes and weights) of each key this version reads; a value of another kind is reported and
  dropped. The typed keys include the class keys (`class` as an object, `class.defaultRenderer` as text,
  `class.hideEmptyMembersBox` and `class.hierarchicalNamespaces` as Booleans, and `class.padding` as a number), the
  state keys (`state` as an object, `state.defaultRenderer` as text, `state.nodeSpacing`, `state.rankSpacing`, and
  `state.titleTopMargin` as numbers, and `state.useMaxWidth` as a Boolean), which the class renderer, the detectors, and
  the state renderer read, the `pie` keys (`textPosition` and `donutHole` as numbers, `legendPosition` and
  `highlightSlice` as text, `useMaxWidth` as a Boolean, and `useWidth` as a number), the `gantt` keys (the margins,
  sizes, paddings, font sizes, and `numberSectionStyles` as numbers, `axisFormat`, `tickInterval`, `displayMode`, and
  `weekday` as text, `topAxis` and `useMaxWidth` as Booleans, and `useWidth` as a number), the `mindmap` keys
  (`mindmap` as an object, `mindmap.padding` and `mindmap.maxNodeWidth` as numbers, `mindmap.layoutAlgorithm` as text,
  and `mindmap.useMaxWidth` as a Boolean), and the `er` keys (`er` as an
  object, `er.diagramPadding`, `er.entityPadding`, `er.minEntityWidth`, `er.nodeSpacing`, `er.rankSpacing`, and
  `er.titleTopMargin` as numbers, and `er.useMaxWidth` as a Boolean). The sequence alignments (`sequence.messageAlign`
  and `sequence.noteAlign`) must also be one of the values that Mermaid's `drawText` honors (`ChoiceKeys`: `left`,
  `center`, `right`, `start`, `middle`, and `end`).
- **Locations**: a dictionary from each top-level key to the location of the layer that set it last (the frontmatter
  key, or the `%%{` of the directive); keys of the defaults and of the site configuration have no location.
- **`MermaidConfig`**: `Raw` (the whole effective configuration, including the keys of other diagram types),
  `ThemeVariables`, and typed properties with Mermaid's defaults - `Theme`, `FontFamily`, `AltFontFamily`, `FontSize`,
  `Look`, `Layout`, `ThemeCss`, `HtmlLabels` (the global key, else the deprecated `flowchart.htmlLabels`, else true),
  `MarkdownAutoWrap`, `Wrap`, `SecurityLevel`, `MaxTextSize`, `MaxEdges`, `DeterministicIds`, `Flowchart`, `Sequence`,
  `Class`, `State`, `Er`, `Pie`, `Gantt`, `Mindmap`, and `DetectorSettings` (the `defaultRenderer` of the flowchart,
  class, and state sections, the
  class one read through `Class` and the state one through `State`). `FlowchartConfig` holds the flowchart keys the
  drawing reads: `UseMaxWidth`, `TitleTopMargin`, `SubGraphTitleMarginTop`, `SubGraphTitleMarginBottom`,
  `DiagramPadding`, `HtmlLabels`, `NodeSpacing`, `RankSpacing`, `Curve`, `Padding`, `DefaultRenderer`, `WrappingWidth`,
  `InheritDir`, and `ArrowMarkerAbsolute`. `SequenceConfig` holds the sequence keys as the sequence renderer reads them:
  the sizes and margins (`Width`, `Height`, `ActorMargin`, `BoxMargin`, `BoxTextMargin`, `NoteMargin`, `MessageMargin`,
  `ActivationWidth`, `DiagramMarginX`, `DiagramMarginY`, `BottomMarginAdj`, `WrapPadding`, `LabelBoxWidth`, and
  `LabelBoxHeight`), the switches (`MirrorActors`, `HideUnusedParticipants`, `ForceMenus`, `RightAngles`,
  `ShowSequenceNumbers`, `UseMaxWidth`, and `ArrowMarkerAbsolute`), the alignments (`MessageAlign` and `NoteAlign`),
  `TextPlacement`, the effective `Wrap`, and the fonts `ActorFont`, `NoteFont`, and `MessageFont` (`MermaidFontConfig`:
  family, size, and weight, the size and weight as `ConfigScalar` values, because Mermaid's `parseFontSize` treats a
  number and a string differently). `ClassConfig` holds the class keys as the class database and renderer read them:
  `DefaultRenderer`, `HideEmptyMembersBox`, `HierarchicalNamespaces`, `Padding` (null when it is not set, so the class
  box, namespace, and note paddings keep their fallbacks of 12, 16, and 6), and, from the state section as Mermaid's
  class renderer reads them (it takes `const { state: conf } = getConfig()`), `NodeSpacing` and `RankSpacing`
  (`state.nodeSpacing || 50` and `state.rankSpacing || 50`, so a zero falls back to 50) and `TitleTopMargin`
  (`state.titleTopMargin ?? 25`, so a zero is kept). `StateConfig` holds the state keys as the detectors and Mermaid's
  `stateRenderer-v3-unified.ts` read them: `DefaultRenderer` (`dagre-wrapper` by default; another value selects the
  legacy `state` detector for a `stateDiagram` text), `NodeSpacing` and `RankSpacing` (`conf.nodeSpacing || 50` and
  `conf.rankSpacing || 50`: the schema has no default for either, and a missing or zero spacing falls back to 50), and
  `TitleTopMargin` (`conf.titleTopMargin ?? 25`, so a zero is kept). `ErConfig` holds the `er` keys as Mermaid's
  `erRenderer-unified.ts` and its `erBox` shape read them: `DiagramPadding` (20) and `EntityPadding` (15), the paddings
  of an entity box, and `MinEntityWidth` (100), the smallest width of an entity without attributes, all taken as given;
  `NodeSpacing` and `RankSpacing` (`er.nodeSpacing || 140` and `er.rankSpacing || 80`, so a zero falls back); and
  `TitleTopMargin` (`er.titleTopMargin ?? 25`, so a zero is kept). The schema's `minEntityHeight`, `layoutDirection`,
  `stroke`, `fill`, and `fontSize` are kept in `Raw` but not read, as in Mermaid. The `||` and `??` reads are the
  renderers' own: `||` (`OrDefault`) replaces a zero, `??` does not. The ER spacing is the one the ER renderer writes
  into the flowchart spacing of its render; a top-level `nodeSpacing` still wins over it in the dagre layout, as in
  Mermaid. `PieConfig` holds the `pie` keys as Mermaid's pie renderer reads them: `TextPosition` (0.75), `DonutHole` (0,
  applied by the renderer only above 0 and up to 0.9), `LegendPosition` (`right`; the renderer knows `right`, `left`,
  `top`, `bottom`, and `center`), and `HighlightSlice` (empty, no slice). `GanttConfig` holds the `gantt` keys as
  Mermaid's gantt renderer reads them: `TitleTopMargin`, `BarHeight`, `BarGap`, `TopPadding`, `RightPadding`,
  `LeftPadding`, `GridLineStartPadding`, `FontSize`, `SectionFontSize`, `NumberSectionStyles`, `AxisFormat`,
  `TickInterval` (null by default), `TopAxis`, `DisplayMode`, and `UseWidth` (null by default; when set it replaces the
  page width). `MindmapConfig` holds the `mindmap` keys as Mermaid's mindmap database and renderer read them:
  `Padding` (10; the node padding, doubled by the database for a rectangle, a rounded rectangle, and a hexagon, and the
  margin the renderer grows the view box by), `MaxNodeWidth` (200; the width a node's label wraps at),
  `LayoutAlgorithm` (`cose-bilkent`, read by nothing, as in Mermaid), and `UseMaxWidth` (true). `Layout` is `dagre` by
  default and `cose-bilkent` for a mindmap unless the user set a layout. Because every layer is validated before the
  merge, a typed property always has a value of its type.

#### Key Methods

- `Resolve(PreprocessedSource source, string? diagramId, MermaidRenderOptions? options, MermaidReportSink? sink)` -
  the pipeline, as literal ports of Mermaid's steps:
  1. `Initialize`: the user options `{ startOnLoad: false, theme }` shallowly overridden by the site configuration,
     validated as the site layer, then `initialize` (`fontFamily` copied to `themeVariables.fontFamily`, the copy that
     `saveConfigFromInitialize` keeps) and `setSiteConfig` (the defaults, the options, and the theme variables of the
     site theme);
  2. `ReadFrontmatter`: the frontmatter `config` converted from YAML with the location of every key path, the
     frontmatter `displayMode` moved to `gantt.displayMode`, and the layer validated;
  3. `ReadDirectives`: `detectInit` - every init directive's arguments parsed first (a JSON error in any of them is
     reported and makes every init directive ignored), a single directive used as it is with its `config` key moved
     to the section of the diagram type that the default detector finds, several directives sanitized and merged in
     order (which deletes their `config` key, as in Mermaid) - and the `wrap` directive;
  4. lodash's `merge` of the frontmatter and directive layers, `SanitizeDirective`, and `addDirective`'s copy of
     `fontFamily` into the theme variables;
  5. `UpdateCurrentConfig`: `Sanitize` with the site's secure keys, the result over the site configuration with
     `assignWithDepth`, and the theme computed again only when the text names a theme (so a directive that sets
     `themeVariables` without a `theme` overrides variables without deriving the others again);
  6. for a flowchart (`flowchart-v2`, `flowchart`, or `flowchart-elk`), the flowchart's `init`: the `layout` of the
     user configuration, else `elk` for an ELK flowchart, and the copy of `arrowMarkerAbsolute` into the section;
  7. for a mindmap, `InitializeMindmap`, the layout step of the mindmap database's `getData`: the layout becomes
     `cose-bilkent` unless the user configuration (`getUserDefinedConfig`: the site configuration and the merged
     frontmatter and directives, not the defaults) sets one.
- `MermaidConfig.Sequence` (built with the configuration) - reads the sequence section over Mermaid's defaults, then
  applies the sequence diagram's `init` (texts wrap when the top-level `wrap`, which a `wrap` directive sets, or the
  section's `wrap` is true) and the sequence renderer's `setConf` (the top-level `fontFamily` and `fontSize` replace
  the families and sizes of the three sequence fonts, and a top-level `fontWeight` replaces their weights; otherwise
  the section's weights apply).
- `MermaidConfig.Class` (built with the configuration) - reads the class section over the defaults of
  `defaultConfig.ts`, and the node spacing, rank spacing, and title margin from the state section, as Mermaid's class
  database and `classRenderer-v3-unified.ts` read them; a `padding` that is not a finite number is left unset.
- `MermaidConfig.State` (built with the configuration, `ReadState`) - reads the state section over the defaults of
  `StateConfig`: the default renderer as text, the spacing through `OrDefault` (the renderer's `||`), and the title
  margin as given (the renderer's `??`). `DetectorSettings` takes its state renderer from here, so the detectors and
  the state model's report of the legacy renderer read the same value.
- `MermaidConfig.Er` (built with the configuration, `ReadEr`) - reads the `er` section over the defaults of
  `ErConfig`: the two paddings, the minimum entity width, and the title margin as given, and the spacing through
  `OrDefault`.
- `MermaidConfig.Pie` and `MermaidConfig.Gantt` (built with the configuration) - read the `pie` and `gantt` sections
  over the defaults of `PieConfig` and `GanttConfig`, every value as given.
- `MermaidConfig.Mindmap` (built with the configuration, `ReadMindmap`) - reads the `mindmap` section over the defaults
  of `MindmapConfig`, every value as given (`conf.mindmap?.key ?? defaultConfig.mindmap.key`).
- `OrDefault(value, fallback)` (private) - JavaScript's `value || fallback` for a number: a zero gives the fallback;
  shared by the class, state, and `er` readers.
- `ComputeTheme` (private) - calls `ThemeResolver.GetThemeVariables`; when it fails with a `ColorFormatException`,
  the override variables holding the offending value are reported, removed, and the theme computed again, at most 16
  times; when no variable holds the value (a hostile `THEME_COLOR_LIMIT`, for example), the overrides are reported and
  the theme is computed without them.
- `ValidateLayer` (private, with `LayerValidator`) - walks one layer against the configuration keys and the typed
  keys: secure keys and refused keys (names that start with `__` or contain `proto` or `constr`, Mermaid's prototype
  pollution guards) of the text, unknown keys with the
  nearest known key by edit distance (at most 32 hints per call), values of the wrong type, strings that the
  sanitizers refuse, and theme names that are not supported (a newer Mermaid theme is named as such).
- `AssignWithDepth(dst, src, depth)` - Mermaid's `assignWithDepth`: objects merged to a depth of 2 without letting a
  value of another type clobber an object, arrays merged as sets, `null` source values skipped, and shallow assignment
  below the depth.
- `LodashMerge(target, source)` - lodash's `merge` as `cleanAndMerge` uses it: objects merged recursively, any other
  source value (null included) replaces the target value.
- `SanitizeDirective(args)`, `Sanitize(options, secure)`, and `SanitizeCss(text)` - Mermaid's sanitizers: refused keys
  removed, dictionary values checked, CSS values with unbalanced braces replaced by an error comment, secure keys and
  keys that start with `__` removed, and strings with `<`, `>`, or `url(data:` removed at every depth.
- `TryParseJson(text, out node, out error)` - `JSON.parse` of directive arguments (whose single quotes the
  `Preprocessor` has already replaced as Mermaid does), strict JSON limited to a depth of 64 (`MaxJsonDepth`), with
  doubles for numbers and the last value of a repeated key.
- `CreateDefaults()` and `ConfigKeys` - the default configuration and the key set (checked against the recorded
  vectors).
- `MermaidConfig.ThemeVariable(name)` - a theme variable as text, a number formatted as JavaScript would;
  `MermaidConfig.LocationOf(key)` - the location of the layer that set a top-level key last, or an unknown location.

**Preconditions and postconditions**: `Resolve` requires a preprocessed source (null throws `ArgumentNullException`);
`diagramId` is null before detection. The result is a new configuration that no other call shares; resolving with and
without a sink gives the same configuration.

#### Error Handling

`Resolve` never throws for the content of the configuration: every problem is a warning, delivered through the sink when
there is one, and the offending key or value has no effect. The codes are `mermaid-config-secure-key` (the text sets a
secure key), `mermaid-config-unknown-key` (a key Mermaid does not define, with the nearest key as the hint), and
`mermaid-config-invalid` (invalid directive JSON, arguments that are not an object, a frontmatter `config` that is not a
mapping, a value of the wrong type (a class, state, or `er` value included, such as a
`class.hierarchicalNamespaces` that is not a Boolean, a `state.nodeSpacing` or `er.entityPadding` that is not a
number, or a `state.defaultRenderer` that is not text), a sequence alignment that Mermaid does not draw, a
refused string, a theme that is not supported, and a theme variable that is not a color). Frontmatter reports are
located at the key, directive reports
at the directive, and site configuration reports have no location. A site configuration text that is not valid JSON was
refused when the options were built, and is otherwise ignored here. A `ColorFormatException` of the theme computation is
handled locally as described above; no other exception is expected.

#### Known Differences From Mermaid

- The themes `neo`, `neo-dark`, `redux`, `redux-dark`, `redux-color`, and `redux-dark-color` of the pinned Mermaid are
  not ported: a text that names one is reported as `mermaid-config-invalid` ("not supported by this version") and the
  theme has no effect, as for an unknown theme.
- A value of the wrong type for a key this version reads is reported and dropped, so the lower layer's value is kept;
  Mermaid would use the value as it is. The same holds for a sequence alignment other than the six that Mermaid's
  `drawText` honors.
- A directive theme variable with characters outside Mermaid's allowed set is removed (Mermaid sets it to an empty
  string).
- A theme variable that khroma cannot read is reported, dropped, and the theme computed again (at most 16 times);
  Mermaid fails the render.
- Init directive arguments that are not a JSON object (a word or an array) are reported and ignored; Mermaid's result
  for them is an accident of lodash's `merge` on strings.
- The text cannot raise the secure keys: the `secure` key itself is secure, as in Mermaid, and its change is reported.
- `securityLevel` is resolved but has no effect, because the package draws pixels and runs no script or link of the
  diagram; the bounds of the package are the `MermaidLimits` of the caller, not the `maxTextSize` and `maxEdges` keys.

#### Dependencies

- **`ThemeResolver`** (this subsystem) - `GetThemeVariables` and `IsTheme`; `ThemeObject` for the value khroma failed
  on
- **`ColorMath`** (this subsystem) - `ColorFormatException`, `Same`, and `FormatNumber`
- **`Syntax` subsystem** - `PreprocessedSource`, `DirectiveText`, and the frontmatter of the `Preprocessor`; the YAML
  nodes of the `YamlSubsetParser`; `DiagramDetector.Detect`, `DiagramDetector.EditDistance`, and `DetectorSettings`
- **System root** - `MermaidRenderOptions`, `MermaidLocation`, `MermaidDiagnosticCodes`, `MermaidLimits`, and
  `MermaidReportSink`
- **.NET base class library** - `System.Text.Json` and `System.Collections.Frozen`

#### Callers

`MermaidDocument` resolves the configuration without a sink when a text is opened (its `DetectorSettings` choose the
detector, and its class section gives the class parser `hierarchicalNamespaces`) and with the call's sink in every
measure or render call, and hands the `MermaidConfig` to the diagram through the render context;
`FlowchartStyles.Create`, `ClassStyles.Create`, `StateStyles.Create`, `ErStyles.Create`, `PieStyles.Create`,
`GanttStyles.Create`, `MindmapStyles.Create`, and `SequenceStyles.Create` (the `ThemeResolver` unit) read its theme
variables and `themeCSS`;
the `Flowchart` subsystem (`FlowchartModel`), the `Sequence` subsystem (`SequenceModel`, `SequenceLayout`, and
`SequenceRenderer`, through `MermaidConfig.Sequence`), the `Class` subsystem (`ClassModel`, through
`MermaidConfig.Class`, and `ClassParser`, which `MermaidDocument` gives the text's own `hierarchicalNamespaces`), the
`State` subsystem (`StateModel`, through `MermaidConfig.State`), the `Er` subsystem (`ErModel`, through
`MermaidConfig.Er`), the `Pie` subsystem (`PieRenderer`, through `MermaidConfig.Pie`), the `Gantt` subsystem
(`GanttRenderer`, through `MermaidConfig.Gantt`), the `Mindmap` subsystem (`MindmapRenderer`, through
`MermaidConfig.Mindmap`, `Look`, `Layout`, and `LocationOf`), and the `Rendering` subsystem (through `LayoutData`,
`ShapeLibrary`,
which reads `MermaidConfig.Class` for a class box and `MermaidConfig.Er` for an entity box, and `LabelBuilder`, which
measures with `MermaidFontConfig`) read the typed settings. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/MermaidConfigResolverTests.cs` and
`MermaidConfigResolverVectorTests.cs` call the unit directly through `InternalsVisibleTo`.
