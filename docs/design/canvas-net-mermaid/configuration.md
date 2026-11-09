## Configuration

![Configuration Structure](MermaidConfigurationView.svg)

### Overview

The `Configuration` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Configuration`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Configuration/`) turns the site configuration of the caller, the frontmatter, and
the directives of a diagram text into the effective configuration of one measure or render call, and the theme of that
configuration into the paint and fonts every flowchart, sequence diagram, and class diagram element class is drawn
with, as the pinned Mermaid release computes them. It is **internal**: its types are visible only to the other
subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains four units:

- **MermaidConfigResolver** - the internal static `MermaidConfigResolver` (a partial class in
  `MermaidConfigResolver.cs`, `MermaidConfigResolver.Defaults.cs`, `MermaidConfigResolver.Merge.cs`, and
  `MermaidConfigResolver.Validation.cs`) and the typed view of its result, `MermaidConfig` with `FlowchartConfig`,
  `SequenceConfig`, `ClassConfig`, `MermaidFontConfig`, and `ConfigScalar` (`MermaidConfig.cs`): the layers of the
  configuration in Mermaid's order with Mermaid's merge rules, sanitizers, secure keys, and theme recomputation, and
  the located reports of the keys and values that have no effect; see _MermaidConfigResolver Unit Design_
  (`configuration/mermaid-config-resolver.md`)
- **ThemeResolver** - the internal static `ThemeResolver` (a partial class in `ThemeResolver.cs` and one file per
  theme, `ThemeResolver.Default.cs`, `.Base.cs`, `.Dark.cs`, `.Forest.cs`, and `.Neutral.cs`), the JavaScript object
  model `ThemeObject` and `Js`, the shared base `DiagramStyles`, the base `UnifiedStyles` of the diagrams that the
  shared rendering units draw, and the flowchart style sheet `FlowchartStyles` with `ShapePaint` and `TextPaint`
  (`ThemeResolver.Styles.cs`), the class diagram style sheet `ClassStyles` (`ThemeResolver.DiagramStyles.cs`), and the
  sequence style sheet `SequenceStyles` with `StrokePaint` and `ShadowPaint` (`ThemeResolver.SequenceStyles.cs`): the
  five built-in themes and the style rules Mermaid writes for a flowchart, a sequence diagram, and a class diagram; see
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
  with the class section and the spacing the class renderer reads from the state section), `DetectorSettings`, and
  `LocationOf(key)`;
- `ThemeResolver.GetThemeVariables(theme, overrides)`, `ThemeResolver.IsTheme`, and
  `ThemeResolver.UnsupportedMermaidThemes`;
- `UnifiedStyles`, the base of the flowchart and class diagram style sheets, with the typed paints `Node`, `Cluster`,
  `NodeLabel`, `EdgeLabel`, `NoteLabel`, `ClusterLabel`, `Title`, `EdgeLabelBackground`, and `TerminalLabel(html)`,
  the label background selectors `HtmlEdgeLabelBackgrounds` and `SvgEdgeLabelBackground`, and `Resolve(selector)`;
- `FlowchartStyles.Create(MermaidConfig, MermaidReportSink?)` with the flowchart's own typed paints `EdgeColor`,
  `EdgeWidth`, `ThickEdgeWidth`, and `MarkerColor`;
- `ClassStyles.Create(MermaidConfig, MermaidReportSink?)` with the class diagram's own typed paint `Divider`;
- `SequenceStyles.Create(MermaidConfig, MermaidReportSink?)` with the resolvers `Shape`, `Line`, `TextColor`,
  `Shadow`, `Box`, and `MessageLine`, the typed paints `Lifeline`, `LoopLine`, `LabelBox`, `BoxShadow`,
  `MenuPanelShadow`, `ArrowheadColor`, `CrossheadColor`, and `SequenceNumberMarkerColor`, and `TryParseFilter`;
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
or layout at `LocationOf`, and builds the `ClassStyles` of the call; the `Rendering` subsystem reads the configuration
through its `LayoutData` (`ClusterLayout` the subgraph margins), `ShapeLibrary` reads the class section for a class box
(its padding and `hideEmptyMembersBox`), and `ShapeLibrary`, `EdgeBuilder`, and `LabelBuilder` parse the `style`,
`classDef`, and `linkStyle` declarations and inline label styles with `StyleDeclarations` and draw with the resolved
paints of the call's `UnifiedStyles` (`ClusterLayout` the diagram title).

### Design

**A literal port of Mermaid's configuration pipeline.** `MermaidConfigResolver.Resolve` follows the calls that the
command line tool of Mermaid makes before it draws: `initialize` with the theme option and the site configuration
(`setSiteConfig`), then `processFrontmatter`, `detectInit` and the `wrap` directive, lodash's `merge` of the two
text layers, `addDirective`, and `updateCurrentConfig`, which computes the theme again only when the text names a
theme. The flowchart's `init` then sets the layout (ELK for an ELK flowchart) and copies `arrowMarkerAbsolute`; for a
sequence diagram the typed `SequenceConfig` applies the `wrap` setting of the diagram's `init` and the font overrides
of the sequence renderer's `setConf` (the top-level font family and size replace the section's own).
Configurations are kept as JSON objects with JavaScript semantics (truthiness, `null` against missing), so the merge
quirks of Mermaid reproduce exactly. Each layer is validated before the merge, so that every report is located at the
frontmatter key or at the directive that caused it; the silent sanitizers of Mermaid then run on the merged layer.

**Themes derived as Mermaid derives them.** `ThemeResolver` ports each `theme-*.js` class to a constructor and an
`updateColors` in the class's own derivation order, over a `ThemeObject` that behaves as the JavaScript `this` of the
class. Every color is derived with `ColorMath`, the port of khroma, so a derived color has the same text as in
Mermaid. `FlowchartStyles`, `ClassStyles`, and `SequenceStyles` share the global rules of Mermaid's `getStyles`
(`DiagramStyles`), add the flowchart's `styles.ts`, the class diagram's `styles.js`, or the sequence diagram's
`styles.js` from the theme variables as selector-keyed rules, append the `themeCSS` rules of the same selectors (parsed
by `StyleDeclarations`), and resolve the cascade into typed paints for the drawing; the sequence resolvers apply the
cascade over the attributes that Mermaid writes on each element, as a browser does. The flowchart and the class diagram
are drawn by the same rendering units, which read either sheet only through `UnifiedStyles`: each sheet supplies its
title selector, the rule its labels are colored by, and the selectors of its label backgrounds. The class diagram's
title has a CSS class that no rule of the sheet styles, so it takes the root font and fill, as in Mermaid.

**Checked against upstream vectors.** The fixture `Fixtures/upstream-vectors/themes.json`, generated from the pinned
Mermaid and khroma by `benchmarks/visual-diff/mermaid/gen-theme-vectors.mjs`, records 622 khroma calls, 53
configuration cases (each theme without overrides, with theme variables and in dark mode, the theme switches of
directives, and precedence, sanitizing, secure-key, wrap, ELK, and HTML-label cases), Mermaid's configuration keys,
and the global and flowchart defaults; the unit tests compare the ports with them value for value. The sequence
defaults and style rules are checked by the unit tests against Mermaid's schema and style sheet and, through the paint
of every element, by the sequence reference layout goldens; the class section and the class style rules are checked in
the same way against Mermaid's default configuration, its class style sheet, and the class reference layout goldens.

**Attribution.** `MermaidConfigResolver` ports `config.ts`, `mermaidAPI.ts` (`initialize`), `assignWithDepth.ts`,
`utils.ts` (`detectInit`, `cleanAndMerge`), `preprocess.ts` (`processFrontmatter`), `utils/sanitizeDirective.ts`,
`defaultConfig.ts`, the defaults of `config.schema.yaml`, the flowchart's `init` of `flowDiagram.ts`, the sequence
diagram's `init` of `sequenceDiagram.ts` and `setConf` of `sequenceRenderer.ts`, and the class section of
`defaultConfig.ts` with the state section's spacing that `classRenderer-v3-unified.ts` reads. `ThemeResolver` ports
`themes/theme-default.js`, `theme-base.js`, `theme-dark.js`, `theme-forest.js`, `theme-neutral.js`, `theme-helpers.js`,
`erDiagram-oldHardcodedValues.ts`, the global `styles.ts`, the flowchart's `styles.ts`, the sequence diagram's
`styles.js`, and the class diagram's `styles.js`. `ColorMath` ports the parsers, channel model, and functions of
khroma. `StyleDeclarations` is original work of this repository. The source files that contain ported code carry a
`Portions derived from` header, and the provenance is recorded in `third-party/README.md` of the package.

**Deterministic and bounded.** No regular expression runs on configuration text; khroma's expressions are scanners
with the same matches, linear in the length of the text. Every loop that a value controls is bounded: the JSON depth
of a directive (64), the color-scale loops of a theme (`THEME_COLOR_LIMIT` capped at 64), the repeated theme
computations after a dropped variable (16), and the nearest-key hints of unknown keys (32). Every type is stateless,
immutable, or used by one call on one thread, and numbers are formatted with the invariant culture as JavaScript
formats them.
