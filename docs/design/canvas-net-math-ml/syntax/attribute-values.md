### AttributeValues

![Syntax Structure](MathMlSyntaxView.svg)

The `AttributeValues` unit is a software unit of the `Syntax` subsystem. Its source files are `AttributeValues.cs`,
which defines the internal static class `AttributeValues`, the record struct `MathLength`, and the enum
`MathLengthUnit`, and `StyleDeclarations.cs`, which defines the record `StyleDeclarations`, the record structs
`MathDepthValue` and `StyleIssue`, and the enum `MathDepthKind`.

#### Purpose

`AttributeValues` parses the values of the MathML attributes that the layout reads and the inline `style` attribute.
Every parser is a pure function that refuses what it does not accept, so that its caller decides what to report: MathML
Core ignores an invalid attribute value, and so does the layout (with a warning). The unit's behavior is specified by
the `CanvasNetMathMl-Syntax-AttributeValues-*` requirements in
`docs/reqstream/canvas-net-math-ml/syntax/attribute-values.yaml`.

#### Data Model

- `MathLengthUnit` - `Px` (CSS pixels; `pt`, `pc`, `in`, `cm`, and `mm` are converted to it), `Em` (multiples of the
  font size, also the named spaces), `Ex` (multiples of the x-height), and `Percent` (of a reference length that depends
  on the attribute).
- `MathLength(Value, Unit)` - a length as written; `MaxMagnitude` (1,000,000 CSS pixels) bounds a resolved length.
- The named spaces of MathML 3 in eighteenths of an em (`veryverythinmathspace` 1 to `veryverythickmathspace` 7,
  and the `negative...` forms), matched ASCII case-insensitively.
- `StyleDeclarations(Color, BackgroundColor, FontSize, DisplayStyle, MathDepth, Italic, DisplayNone, Issues)` - the
  supported declarations of an inline style, null when absent, with the issues in order; `Empty` is the empty list.
- `MathDepthValue(Kind, Value)` with `MathDepthKind` - `AutoAdd`, `Add` (n), and `Set` (n).
- `StyleIssue(IsUnsupported, Message)` - an unsupported declaration or an invalid value of a supported one.

#### Key Methods

- `TryParseLength(text, out length)` - an optional sign, digits with an optional fraction, and a CSS unit or `%`; a
  named space; or a unitless zero. A unitless number other than zero, an unknown unit, the legacy pseudo-units, and
  other text are refused.
- `MathLength.Resolve(fontSize, xHeight, percentBase)` - pixels for `Px`, times the font size for `Em`, times the
  x-height for `Ex`, a percentage of the base for `Percent`; clamped to plus or minus `MaxMagnitude` so that the
  arithmetic of the layout stays finite.
- `TryParseBoolean` - `true` or `false`, ASCII case-insensitive. `TryParseUnsignedInteger` - ASCII digits only.
- `TryParseScriptLevel(text, out relative, out value)` - `+U` and `-U` change the inherited level, `U` sets it.
- `TryParseColor(text, out color)` - the core `CssColor` parser: names, `transparent`, `#rgb`, `#rrggbb` and their alpha
  forms, `rgb()`, and `hsl()`.
- `StyleDeclarations.Parse(text)` - removes the CSS comments, splits the declarations at the semicolons outside
  parentheses and quotes, ignores `!important`, and applies each: `color` and `background-color` (CSS colors),
  `font-size` (a length, a percentage, or `math`), `math-style` (`normal` or `compact`), `math-depth` (`auto-add`,
  `add(n)`, or `n`), `font-style` (`normal` or `italic`), and `display: none`; `text-align`, `padding-left`, and
  `padding-right` (which Pandoc writes on `mtd` beside `columnalign`) are accepted and have no effect. A later
  declaration of a property wins;
  every other property and every invalid value becomes an issue.

#### Error Handling

No parser throws for its input: an invalid value returns false (or becomes a `StyleIssue`), and the caller reports it
(`mathml-attribute-invalid` or `mathml-css-unsupported`). Numbers are read with the invariant culture.

#### Dependencies

The core `Canvas` subsystem (`CssColor`, `Rgba32`) and the .NET base class library.

#### Callers

The `StyleResolver` unit of the Layout subsystem parses the style attributes and resolves the length attributes of the
`TokenLayout` (`mspace`, `mo`) and `RowLayout` (`mpadded`) units with it.
