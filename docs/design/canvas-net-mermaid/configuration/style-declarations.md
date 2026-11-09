### StyleDeclarations

![Configuration Structure](MermaidConfigurationView.svg)

The `StyleDeclarations` unit is the fourth software unit in the `Configuration` subsystem. It is one source file,
`StyleDeclarations.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Configuration` namespace, and defines the internal
static class `StyleDeclarations` with the grouped types `StyleDeclaration` (one declaration), `StyleDeclarationSet`
(the declarations of one element class after the cascade), and `StyleRule` (a selector and its declarations).

#### Purpose

`StyleDeclarations` reads the small CSS subset that a Mermaid text carries in its `style`, `classDef`, and `linkStyle`
statements, in the inline styles of labels, and in the `themeCSS` of the configuration: declarations of a fixed set of
properties that change pixels, `!important`, the cascade for rules of equal specificity, and style sheets whose rules
use one of the diagram's own selectors. Everything else is reported and left out, so that the declaration below it in
the cascade stays in effect, as a browser drops a declaration it cannot use.

The unit's behavior is specified by the `CanvasNetMermaid-Configuration-StyleDeclarations-*` requirements in
`docs/reqstream/canvas-net-mermaid/configuration/style-declarations.yaml`.

#### Data Model

- **`SupportedProperties`** - the properties a user declaration may set: `fill`, `stroke`, `stroke-width`,
  `stroke-dasharray`, `color`, `font-weight`, `font-style`, `font-size`, `font-family`, and `opacity`.
- **`StyleDeclaration(Property, Value, Important)`** - a property name in lower case, the trimmed value without
  `!important`, and whether the declaration is important.
- **`StyleDeclarationSet`** - at most one declaration per property, in the order the properties first appeared;
  immutable. A later declaration replaces an earlier one unless the earlier one is important and the later one is not.
  `Empty` is the empty set.
- **`StyleRule(Selector, Declarations)`** - a normalized selector and its declarations.

#### Key Methods

- `ParseItems(IEnumerable<string> items, MermaidLocation location, MermaidReportSink? sink)` - the items of a `style`,
  `classDef`, or `linkStyle` statement (each `property:value`, as Mermaid's parser splits them at commas), restricted
  to the supported properties.
- `ParseBlock(string text, bool restrict, MermaidLocation location, MermaidReportSink? sink)` - a declaration block
  split at the semicolons outside quotes and parentheses, empty declarations skipped; `restrict` is true for user text
  and false for the diagram's own rules (which may use every property, for example `background-color`).
- `ParseSheet(string css, IReadOnlySet<string> knownSelectors, MermaidLocation location, MermaidReportSink? sink)` -
  a style sheet: comments removed, each rule's selector list split and normalized, the rules of known selectors kept
  in order, and every other selector, at-rule, and rule with nested rules reported; an empty prelude (such as the error
  comment that Mermaid's `sanitizeCss` leaves) applies nothing.
- `NormalizeSelector(string selector)` - white space collapsed to one space and one space around the child
  combinator, so that `.a>b` and `.a  >  b` are `.a > b`.
- `StyleDeclarationSet.Cascade(overlay)`, `Paint(property, fallback)`, `Length(property, fallback, fontSize)`, and
  `IsImportant(property)` - the cascade and the typed reading of a property.
- The value parsers: `TryParsePaint` (a CSS color through the core `CssColor` grammar, or `none` as no paint),
  `TryParseLength` (pixels, unitless numbers, `em` and `rem` relative to the font size and 16 pixels, and points;
  negative lengths refused), `TryParseFontSize` (a length that `IsDrawableFontSize` accepts: above zero also in
  single precision, the precision of the text renderer, and at most `MaxFontSize` = 32,768 pixels, the painter's
  largest drawn magnitude), `TryParseDashArray` (numbers separated by commas or spaces, an odd list repeated, `0`
  meaning solid), `TryParseOpacity` (a number or a percentage), `TryParseBold` (`bold`, `bolder`, `normal`,
  `lighter`, and the numeric weights), and `TryParseItalic` (`italic` and `oblique`, with an optional angle, and
  `normal`).

**Preconditions and postconditions**: the text arguments must not be null (`ArgumentNullException`); the methods are
pure functions of their arguments except for the reports, culture-invariant, and linear in the length of the text.

#### Error Handling

No method throws for the content of the CSS. A declaration of an unsupported property, a declaration without a colon,
and a value the property cannot have are reported as the warning `mermaid-style-ignored` at the location of the
statement (an unsupported property with the list of the supported ones as the hint, a font size that cannot be drawn
with the drawable range as the hint) and left out, so the inherited value stays in effect. The diagram's own rules
(the theme) drop such a value silently, as a browser drops an invalid declaration, and the places that resolve a
font size relative to another (`em` in a label's `style` attribute or an element's label styles, the theme's text
rules) keep the inherited size when the result is not drawable. A `themeCSS` rule
that cannot be applied is reported as the unsupported feature `mermaid-theme-css-unsupported` at the location of
`themeCSS`; in strict mode the sink throws `MermaidUnsupportedFeatureException` at the first one, which propagates to
the render call.

#### Known Differences From Mermaid

- Only the supported properties take effect; a user `background-color` and every other property is reported as
  `mermaid-style-ignored`, where a browser would apply it to the SVG or HTML elements.
- A `themeCSS` rule with a selector that is not one of the diagram's own selectors, an at-rule, or a nested rule is
  reported as `mermaid-theme-css-unsupported` and not applied, because there is no document tree to match it against.
- Colors of user declarations are read with the CSS color grammar of the core library (as a browser reads them), not
  with khroma.

#### Dependencies

- **Core `CanvasNet`** - `Rgba32` and `CssColor` (the `Canvas` subsystem), through the public API
- **System root** - `MermaidLocation`, `MermaidDiagnosticCodes`, and `MermaidReportSink`
- **.NET base class library** - `System.Text`, `System.Globalization`, and `System.Collections.Frozen`

The unit has no dependency on another unit of the system.

#### Callers

`FlowchartStyles` (the `ThemeResolver` unit) parses its own rules with `ParseBlock` and `themeCSS` with `ParseSheet`,
and cascades with `StyleDeclarationSet`; the `Rendering` subsystem parses the `style`, `classDef`, and `linkStyle`
items of nodes, subgraphs, and edges (`ShapeLibrary` and `EdgeBuilder` with `ParseItems`) and the inline styles of HTML
labels (`LabelBuilder` with `ParseBlock`), and reads their values with the value parsers. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/StyleDeclarationsTests.cs` call the unit directly through
`InternalsVisibleTo`.
