### ThemeResolver

![Configuration Structure](MermaidConfigurationView.svg)

The `ThemeResolver` unit is the second software unit in the `Configuration` subsystem. It is the internal static
partial class `ThemeResolver` in the `DemaConsulting.CanvasNet.Mermaid.Configuration` namespace, in
`ThemeResolver.cs` (the theme table, `GetThemeVariables`, the shared helpers and derivation blocks, and the grouped
helper types `ThemeObject` and `Js`), one file per theme - `ThemeResolver.Default.cs`, `ThemeResolver.Base.cs`,
`ThemeResolver.Dark.cs`, `ThemeResolver.Forest.cs`, and `ThemeResolver.Neutral.cs` - `ThemeResolver.Styles.cs`,
which holds the shared base `DiagramStyles` of the style sheets, the flowchart style sheet `FlowchartStyles`, and its
typed paints `ShapePaint` and `TextPaint`, and `ThemeResolver.SequenceStyles.cs`, which holds the sequence style sheet
`SequenceStyles` and its typed paints `StrokePaint` and `ShadowPaint`.

#### Purpose

`ThemeResolver` reproduces Mermaid's five built-in themes - `default`, `base`, `dark`, `forest`, and `neutral` - and
the style sheets that Mermaid writes for a flowchart and a sequence diagram from them. Given a theme name and the
user's theme variable overrides, it computes every theme variable of every diagram type with the same text as
Mermaid's `getThemeVariables`, and `FlowchartStyles` and `SequenceStyles` turn the theme variables and the `themeCSS`
of a configuration into the paint and fonts the diagram is drawn with.

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
  of the root rule, Mermaid's `&`), and the builder of the global rules of Mermaid's `getStyles`, which both sheets
  start with.
- **`FlowchartStyles`** - the rules of the flowchart's style sheet. `KnownSelectors` is the
  set of selectors of the sheet, which `themeCSS` may use.
  The typed properties `Node` and `Cluster` (`ShapePaint`: fill, stroke, and stroke width), `EdgeColor`, `EdgeWidth`,
  `ThickEdgeWidth`, `MarkerColor`, `NodeLabel`, `ClusterLabel`, and `Title` (`TextPaint`: font family, font size, and
  color), and `EdgeLabelBackground` resolve the rules the flowchart draws with. An instance is immutable.
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
missing scale colors, as in Mermaid. `FlowchartStyles.Create` and `SequenceStyles.Create` report each `themeCSS` rule
of another selector, at-rule, or nested rule as `mermaid-theme-css-unsupported` at the location of `themeCSS` and do
not apply it; in strict mode the sink throws `MermaidUnsupportedFeatureException` at the first one.

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

#### Dependencies

- **`ColorMath`** (this subsystem) - every color derivation (`Adjust`, `Darken`, `Lighten`, `Invert`, `Rgba`, `IsDark`)
  and `ColorFormatException`
- **`StyleDeclarations`** (this subsystem) - `ParseSheet`, `ParseBlock`, `NormalizeSelector`, the value parsers, and
  `StyleDeclarationSet`
- **`MermaidConfigResolver`** (this subsystem) - the `MermaidConfig` that `FlowchartStyles.Create` and
  `SequenceStyles.Create` read
- **System root** - `MermaidReportSink` and `MermaidLocation`
- **Core `CanvasNet`** - `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API
- **.NET base class library** - `System.Text.Json` and `System.Collections.Frozen`

#### Callers

`MermaidConfigResolver` computes the theme variables of the site theme and of a theme named by the text; the
`MermaidDocument` pipeline and the `Flowchart` subsystem (`FlowchartModel`) build the `FlowchartStyles` of a call; the
`Rendering` subsystem (`ShapeLibrary`, `EdgeBuilder`, and through them `ClusterLayout`) draws with the resolved paints,
the rules, and `FlowchartStyles.Root`; and in the `Sequence` subsystem `SequenceLayout` builds the `SequenceStyles` of
a render and `SequenceRenderer` draws with its resolvers. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ThemeResolverTests.cs` call the unit directly through
`InternalsVisibleTo`, against the vectors of `Fixtures/upstream-vectors/themes.json`.
