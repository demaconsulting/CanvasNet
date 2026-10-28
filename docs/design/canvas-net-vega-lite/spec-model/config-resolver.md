### ConfigResolver

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `ConfigResolver` unit is the third software unit in the `SpecModel` subsystem. It is one source file,
`ConfigResolver.cs`, in the `DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace, and defines four `internal` types:
`ConfigResolver` (static class), `ResolvedConfig` (sealed class), `ConfigNode` (sealed class), and `ConfigNodeKind`
(enumeration). The file carries the provenance header comment for the Vega and Vega-Lite default values it contains.

#### Purpose

`ConfigResolver` builds the effective configuration of a chart: the Vega guide defaults, the Vega-Lite defaults, the
`config` of the specification, and the `Config` of the caller's render options, merged in that order of increasing
precedence, and gives the later stages typed access to it together with the precedence chains that Vega-Lite uses to
look up the properties of marks, views, titles, and styles. It exists so that every default value of a chart is
defined once, with attribution, and every other unit asks for a property instead of hard-coding a constant.

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-ConfigResolver-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/config-resolver.yaml`.

#### Data Model

**`ConfigNode`** (sealed class, immutable) is a JSON-like tree: `Kind` (`ConfigNodeKind`: object, array, string,
number, boolean, null), the typed values, `Items`, `Properties`, an indexer by name, `Get(dottedPath)`, and the
conversions `AsString`, `AsNumber`, and `AsBool`. Its factories are `FromSpec` (from a `SpecNode`), `FromJson` (from
text), `Create`, and `EmptyObject`, and `Merge(base, overlay)` merges objects deeply and replaces everything else
(arrays and scalars), which is a true deep merge (Vega merges most objects one level only).

**`ResolvedConfig`** (sealed class) holds the merged `Root` and offers the dotted-path accessors `Get`, `GetNumber`,
`GetString`, `GetBool`, `TryGetObject`, and `TryGetArray`, and the precedence chains:

- `ResolveMarkProperty(markType, styles, property)`: the style of the mark type and the named styles, then the
  configuration of the mark type, then `config.mark`, then the Vega default styles, then the Vega mark default; a value
  in the mark definition itself is the caller's and wins outside this chain
- `ResolveViewProperty(property)`: the `cell` style, then `view`, then the Vega cell style
- `ResolveStyleProperty(styles, property)`: later style names win
- `ResolveTitleProperty(property, subtitle)`: `config.title` (with `color` read as `fill` and the subtitle counterparts),
  then the `group-title` or `group-subtitle` style

**`ConfigResolver`** exposes `Defaults` (the shared defaults-only configuration), the rule constants
`DefaultReducedOpacity` (0.7, for point, tick, circle, and square marks without aggregation), `DefaultStep` (20), and
`DefaultStrokeWidth` (1), and `IsFilledByDefault(markType)` (every mark except point, line, and rule).

**Default values** are two embedded JSON trees ported with attribution. The Vega tree provides the padding and
autosize, the guide, group, and mark styles (`guide-label` 10 pixel, `guide-title` 11 pixel bold, `group-title` 13 pixel
bold, `group-subtitle` 12 pixel, `point`, `circle`, `square` with size 30 and stroke width 2, `cell` with transparent
fill and `#ddd` stroke), the title (orient top, anchor middle, offset 4, subtitle padding 3), the axis (minimum extent 0,
maximum extent 200, band position 0.5, domain, grid, label, and tick defaults, title padding 4) and `axisBand`
(tick offset -0.5), the legend, projection, and range defaults (`range.category` is the scheme `tableau10`). The
Vega-Lite tree provides the background (white), padding (5), view size (continuous width and height 300, step 20), the
mark colors, the bar, rect, and tick configuration, the scale configuration (band paddings, minimum band size, font,
opacity, size, and stroke-width ranges, `zero`), the legend gradient lengths, and the header, facet, and concat
spacing. The Vega mark-type keys (`rect`, `symbol`, and so on) are kept out of the merged tree, because Vega-Lite uses
the same names for its own mark configuration; they are consulted only as the last link of `ResolveMarkProperty`.
Defaults that Vega-Lite computes by rule (a scale's `nice` and a continuous padding) are not in the tree.

#### Key Methods

- `Resolve(SpecTree tree, string? callerConfigJson)` and `Resolve(SpecNode? specConfig, string? callerConfigJson)` -
  build the merged tree: the Vega guide defaults, then the Vega-Lite defaults, then the specification's `config`
  (converted from the `SpecNode`), then the caller's text (parsed with a depth limit of 64); a `font` shorthand in the
  configuration is applied to the text and guide styles; the result is a `ResolvedConfig`. The caller's configuration
  therefore wins, which is the documented order of this package (see _Deliberate Strictness And Deviations_ in
  _CanvasNetVegaLite System Design_).
- `ConfigNode.Merge(base, overlay)` - objects merge key by key recursively; anything else in the overlay replaces the
  base.
- `ResolvedConfig.Get(path)` and the typed getters - follow a dotted path through objects; null when absent or of the
  wrong kind.
- The `Resolve*Property` chains - return the first configured value in the precedence order, or null.

**Preconditions and postconditions**: the specification configuration is a valid tree (checked by `SpecParser`); the
result is immutable and thread-safe. The defaults are built once.

#### Error Handling

The caller's configuration text is validated when the render options are initialized (a JSON object, at most 4,194,304
characters and 64 levels); `Resolve` repeats the checks as a guard and throws `ArgumentException` for a text that is not
a JSON object, is not valid JSON, or is nested more than 64 levels, which therefore cannot happen through the public
options. A configuration property of an unexpected kind is ignored by the typed accessors (they return null) rather than
thrown, because a configuration that merely does not set a property is not an error; unknown keys are ignored. The
specification's own configuration was already validated by `SpecParser` (a wrong type for a known property of `config`
is a `VegaLiteSpecException` there). The unit does no file or network access.

#### Dependencies

- **`SpecNode`** and **`SpecTree`** - the `SpecParser` unit's tree types, for the specification's `config`
- **.NET base class library** - `System.Text.Json` (parsing the caller's configuration and the embedded default
  trees), `System.Globalization`, and `System.Collections.ObjectModel`

#### Callers

`VegaLiteDocument` resolves the configuration on every `Measure` and `Render` (the caller's configuration can differ per
call). `ScaleResolver`, `TitleBuilder`, `AxisBuilder`, `ViewLayout`, and `MarkBuilder` read the `ResolvedConfig`.
The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/ConfigResolverTests.cs` call the resolver directly
through `InternalsVisibleTo`.
