### MermaidConfigResolver

![Configuration Structure](MermaidConfigurationView.svg)

The `MermaidConfigResolver` unit is the first software unit in the `Configuration` subsystem. It is the internal
static partial class `MermaidConfigResolver` in four source files of the `DemaConsulting.CanvasNet.Mermaid.Configuration`
namespace - `MermaidConfigResolver.cs` (the pipeline of `Resolve`, the frontmatter and directive layers, and the theme
computation with recovery), `MermaidConfigResolver.Defaults.cs` (the defaults, the configuration keys, and the default
secure keys), `MermaidConfigResolver.Merge.cs` (the JSON operations: `assignWithDepth`, lodash's `merge`, the
sanitizers, and the directive JSON parser), and `MermaidConfigResolver.Validation.cs` (the validation and the reports of
one layer) - and the grouped typed view of its result in `MermaidConfig.cs`: the sealed class `MermaidConfig`, the
records `FlowchartConfig`, `SequenceConfig`, and `ClassConfig`, the font record `MermaidFontConfig`, and the value
`ConfigScalar` (a number or a string, kept as JavaScript holds it).

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
  of Mermaid's `SequenceDiagramConfig`), and the `class` section of Mermaid's `config.schema.yaml` and
  `defaultConfig.ts`, with the variables of the `default` theme. The class section is the one `defaultConfig.ts`
  writes in place of the schema's: `defaultRenderer` (`dagre-wrapper`), `hideEmptyMembersBox` (false), and
  `hierarchicalNamespaces` (true), with `padding` deliberately left undefined so that each use keeps its own fallback.
  `DefaultFontFamily` is Mermaid's font family list.
- **Configuration keys** (`ConfigKeys`, a frozen set of 705 names): every key that Mermaid's `sanitizeDirective`
  accepts (the keys of every diagram section and of the default theme variables); a key of the text outside the set is
  deleted by Mermaid and reported here as unknown.
- **Secure keys** (`DefaultSecureKeys`): `secure`, `securityLevel`, `startOnLoad`, `maxTextSize`,
  `suppressErrorRendering`, and `maxEdges`, replaced by the `secure` list of the site configuration when it has one.
- **Typed keys** (`TypedKeys` of the validation): the kind of value (text, number, Boolean, object, or a number or a
  string for the font sizes and weights) of each key this version reads; a value of another kind is reported and
  dropped. The typed keys include the class keys (`class` as an object, `class.defaultRenderer` as text,
  `class.hideEmptyMembersBox` and `class.hierarchicalNamespaces` as Booleans, and `class.padding` as a number) and the
  state keys that the class renderer reads (`state` as an object, and `state.nodeSpacing`, `state.rankSpacing`, and
  `state.titleTopMargin` as numbers). The sequence alignments (`sequence.messageAlign` and `sequence.noteAlign`) must
  also be one of the values that Mermaid's `drawText` honors (`ChoiceKeys`: `left`, `center`, `right`, `start`,
  `middle`, and `end`).
- **Locations**: a dictionary from each top-level key to the location of the layer that set it last (the frontmatter
  key, or the `%%{` of the directive); keys of the defaults and of the site configuration have no location.
- **`MermaidConfig`**: `Raw` (the whole effective configuration, including the keys of other diagram types),
  `ThemeVariables`, and typed properties with Mermaid's defaults - `Theme`, `FontFamily`, `AltFontFamily`,
  `FontSize`, `Look`, `Layout`, `ThemeCss`, `HtmlLabels` (the global key, else the deprecated `flowchart.htmlLabels`,
  else true), `MarkdownAutoWrap`, `Wrap`, `SecurityLevel`, `MaxTextSize`, `MaxEdges`, `DeterministicIds`,
  `Flowchart`, `Sequence`, `Class`, and `DetectorSettings` (the `defaultRenderer` of the flowchart, class, and state
  sections, the class one read through `Class`).
  `FlowchartConfig` holds the flowchart keys the drawing reads: `UseMaxWidth`, `TitleTopMargin`,
  `SubGraphTitleMarginTop`, `SubGraphTitleMarginBottom`, `DiagramPadding`, `HtmlLabels`, `NodeSpacing`, `RankSpacing`,
  `Curve`, `Padding`, `DefaultRenderer`, `WrappingWidth`, `InheritDir`, and `ArrowMarkerAbsolute`. `SequenceConfig`
  holds the sequence keys as the sequence renderer reads them: the sizes and margins (`Width`, `Height`,
  `ActorMargin`, `BoxMargin`, `BoxTextMargin`, `NoteMargin`, `MessageMargin`, `ActivationWidth`, `DiagramMarginX`,
  `DiagramMarginY`, `BottomMarginAdj`, `WrapPadding`, `LabelBoxWidth`, and `LabelBoxHeight`), the switches
  (`MirrorActors`, `HideUnusedParticipants`, `ForceMenus`, `RightAngles`, `ShowSequenceNumbers`, `UseMaxWidth`, and
  `ArrowMarkerAbsolute`), the alignments (`MessageAlign` and `NoteAlign`), `TextPlacement`, the effective `Wrap`, and
  the fonts `ActorFont`, `NoteFont`, and `MessageFont` (`MermaidFontConfig`: family, size, and weight, the size and
  weight as `ConfigScalar` values, because Mermaid's `parseFontSize` treats a number and a string differently).
  `ClassConfig` holds the class keys as the class database and renderer read them: `DefaultRenderer`,
  `HideEmptyMembersBox`, `HierarchicalNamespaces`, `Padding` (null when it is not set, so the class box, namespace,
  and note paddings keep their fallbacks of 12, 16, and 6), and, from the state section as Mermaid's class renderer
  reads them (it takes `const { state: conf } = getConfig()`), `NodeSpacing` and `RankSpacing`
  (`state.nodeSpacing || 50` and `state.rankSpacing || 50`, so a zero falls back to 50) and `TitleTopMargin`
  (`state.titleTopMargin ?? 25`, so a zero is kept). Because every layer is validated before the merge, a typed
  property always has a value of its type.

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
     user configuration, else `elk` for an ELK flowchart, and the copy of `arrowMarkerAbsolute` into the section.
- `MermaidConfig.Sequence` (built with the configuration) - reads the sequence section over Mermaid's defaults, then
  applies the sequence diagram's `init` (texts wrap when the top-level `wrap`, which a `wrap` directive sets, or the
  section's `wrap` is true) and the sequence renderer's `setConf` (the top-level `fontFamily` and `fontSize` replace
  the families and sizes of the three sequence fonts, and a top-level `fontWeight` replaces their weights; otherwise
  the section's weights apply).
- `MermaidConfig.Class` (built with the configuration) - reads the class section over the defaults of
  `defaultConfig.ts`, and the node spacing, rank spacing, and title margin from the state section, as Mermaid's class
  database and `classRenderer-v3-unified.ts` read them; a `padding` that is not a finite number is left unset.
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
mapping, a value of the wrong type (a class or state value included, such as a `class.hierarchicalNamespaces` that is
not a Boolean or a `state.nodeSpacing` that is not a number), a sequence alignment that Mermaid does not draw, a
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
measure or render call, and hands the `MermaidConfig` to the diagram through
the render context; `FlowchartStyles.Create`, `ClassStyles.Create`, and `SequenceStyles.Create` (the `ThemeResolver`
unit) read its theme variables and `themeCSS`; the `Flowchart` subsystem (`FlowchartModel`), the `Sequence` subsystem
(`SequenceModel`, `SequenceLayout`, and `SequenceRenderer`, through `MermaidConfig.Sequence`), the `Class` subsystem
(`ClassModel`, through `MermaidConfig.Class`, and `ClassParser`, which `MermaidDocument` gives the text's own
`hierarchicalNamespaces`), and the `Rendering` subsystem (through `LayoutData`, `ShapeLibrary`, which reads
`MermaidConfig.Class` for a class box, and `LabelBuilder`, which measures with `MermaidFontConfig`) read the typed
settings.
The tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/MermaidConfigResolverTests.cs` and
`MermaidConfigResolverVectorTests.cs` call the unit directly through `InternalsVisibleTo`.
