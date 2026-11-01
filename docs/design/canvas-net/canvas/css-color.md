### CssColor Unit Design

The `CssColor` static class is the one CSS color grammar of CanvasNet: it parses CSS color text into the
`Rgba32` pixel type and formats an `Rgba32` back as CSS text. It lives next to `Rgba32` in the
`DemaConsulting.CanvasNet.Canvas` namespace and is public so that every CanvasNet package, and any library user,
resolves the same text to the same pixel.

#### Purpose

Colors are written as text in style sheets, chart specifications, scripts, and command lines. Before this unit existed,
four packages each kept their own copy of the 148-entry named-color table and their own parser, with different grammars
and different rounding. `CssColor` is the single, tested implementation: the SVG codec, the Vega-Lite renderer, the
OpenSCAD renderer, and the command-line tool all delegate to it. Consumers that accept a narrower grammar than the
full CSS one combine the two building blocks `TryGetNamedColor` and `TryParseHex` instead of `TryParse`.

`Rgba32.Parse` stays as it is - it reads the alpha pair *first* (`#AARRGGBB`) and its documentation points to this unit
for CSS colors, whose alpha pair comes *last* (`#RRGGBBAA`).

#### Data Model

The unit has no mutable state. It holds one immutable, case-insensitive lookup table (a `FrozenDictionary` from color
name to `Rgba32`) with the 148 CSS named colors (the 147 names of CSS Color Level 3, including both `gray` and `grey`
spellings, plus `rebeccapurple`) and `transparent` (0, 0, 0, 0). The table is built once, on first use, from a static
array of a PascalCase name and a `0xRRGGBB` number (for example `("AliceBlue", 0xF0F8FF)`); the data is written in this
form so that the spell checker needs no suppression and no word list for it. `Rgba32` carries straight (not
premultiplied) alpha, so `rgba(255,0,0,0.5)` is `(255, 0, 0, 128)`.

The accepted forms of `TryParse` are:

- hexadecimal `#rgb`, `#rgba`, `#rrggbb`, and `#rrggbbaa` (a short digit is doubled; alpha 255 when absent);
- the 148 named colors and `transparent`, in any letter case;
- `rgb()` and `rgba()` with three channels as numbers (0 to 255) or percentages and an optional alpha as a number (0 to
  1) or a percentage, in one of the two CSS syntaxes: the legacy comma syntax (three or four comma-separated
  components, no slash, no `none`) or the modern space syntax (three space-separated components, optionally followed by
  a slash and one alpha: `rgb(10 20 30 / 50%)`; the keyword `none` counts as zero in a component). The two are never
  mixed: `rgb(1 2 3 0.5)`, `rgb(1,2,3 / 0.5)`, empty components, and trailing commas are rejected;
- `hsl()` and `hsla()` with a hue in degrees (a number, or with the unit `deg`, `rad`, `grad`, or `turn`; a hue whose
  converted value is not finite is rejected), saturation and lightness as percentages (in the space syntax also as
  numbers), and the same two syntaxes and alpha forms.

Out-of-range channels and alpha values are clamped, as CSS specifies, and fractional channel values are rounded half
away from zero (so 30% of 255, which is 76.5, is 77). Surrounding white space is ignored by `TryParse`; function names
and keywords are case-insensitive.

#### Key Methods

- `TryParse(string? text, out Rgba32 color)` - the full grammar above; `false` with `default` for null, empty, and
  white-space text, `none`, and any unsupported text; never throws.
- `Parse(string text)` - the same grammar; throws `ArgumentNullException` for null and `FormatException` (naming the
  text) for text that `TryParse` rejects.
- `TryParseHex(ReadOnlySpan<char> digits, out Rgba32 color)` - the digits after the `#` only: exactly 3, 4, 6, or 8
  hexadecimal digits in CSS order; rejects any other length, a sign, white space, and non-hexadecimal characters.
- `TryGetNamedColor(string? name, out Rgba32 color)` - an exact, case-insensitive lookup of the 148 names and
  `transparent`; no trimming and no other forms.
- `ToCss(Rgba32 color)` - the lower-case hexadecimal text, `#rrggbb` when the color is opaque and `#rrggbbaa`
  otherwise, which `TryParse` reads back to the same color.
- `NamedColorCount` - 148 (`transparent` not included).

**Preconditions and postconditions**: parsing is culture invariant and deterministic; the methods are pure functions
of their arguments.

#### Error Handling

`TryParse`, `TryParseHex`, and `TryGetNamedColor` never throw for bad text: they return `false` and set the color to
`default`. Only `Parse`, which exists for callers that want a loud failure, throws: `ArgumentNullException` for a null
argument and `FormatException` for text that is not a supported color. The unit performs no I/O.

#### Dependencies

- **`Rgba32`** (this subsystem) - the result type
- **.NET base class library** - `System.Collections.Frozen` (the immutable table) and `System.Globalization`
  (invariant-culture number parsing and formatting)

The unit has no dependency on any other CanvasNet unit.

#### Callers

`CssColor` is a public API entry point. Within the repository it is called by the SVG codec (`SvgCodec`, every color
value, through `TryParse`), the Vega-Lite renderer (`CssPaint`, `ScenePainter`, `ScaleResolver`, and `VegaLiteDocument`,
through `TryParse` and `ToCss`), the OpenSCAD renderer (`ScadColors`, the `color` module, through `TryGetNamedColor`
and `TryParseHex` so that `rgb()` and `hsl()` strings stay invalid there), and the command-line tool
(`ArgumentReader.ParseColor`, through `TryGetNamedColor` and `TryParseHex`).
