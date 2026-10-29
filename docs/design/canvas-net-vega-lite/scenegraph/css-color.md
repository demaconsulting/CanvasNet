### CssColor

![Scenegraph Structure](VegaLiteScenegraphView.svg)

The `CssColor` unit is the third software unit in the `Scenegraph` subsystem. It is one source file, `CssColor.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace, and defines one `internal` type, the static class `CssColor`.

#### Purpose

`CssColor` parses CSS colors into core `Rgba32` values. Charts spell colors the CSS way (the Vega-Lite defaults are `#4c78a8`
and `#ddd`) and the core library has no CSS parser, so this unit holds one. It exists so that scene items can carry the
colors exactly as the specification and configuration spell them (and tests can assert the exact string), and so that
parsing is deterministic and never throws for bad text.

The unit's behavior is specified by the `CanvasNetVegaLite-Scenegraph-CssColor-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scenegraph/css-color.yaml`.

#### Data Model

The unit has no state beyond a table of the 148 CSS named colors (a static array of a PascalCase name and a
`0xRRGGBB` number, such as `("AliceBlue", 0xF0F8FF)`, lower-cased and built once into a dictionary with
`transparent` added; the table is written in this form so that the spell checker needs no suppression and no word
list for it) and the accepted forms:

- hexadecimal `#rgb`, `#rgba`, `#rrggbb`, and `#rrggbbaa` (a short digit is doubled);
- the CSS named colors, including `rebeccapurple` and the `grey` spellings, and `transparent`, in any letter case;
- `rgb()` and `rgba()` with three channels as numbers (0-255) or percentages and optional alpha as a number (0 to 1)
  or a percentage, written with commas or with spaces and a slash;
- `hsl()` and `hsla()` with a hue in degrees (a number, or with the unit `deg`, `rad`, `grad`, or `turn`), saturation and
  lightness as percentages, and the same alpha forms.

Out-of-range channels are clamped, as CSS specifies, and fractions are rounded half away from zero. The core pixel type
holds straight (not premultiplied) alpha, so `rgba(255,0,0,0.5)` is `(255, 0, 0, 128)`. A **paint** is a color or no color:
null, an empty or white-space string, and `none` mean "do not draw".

#### Key Methods

- `TryParse(string? text, out Rgba32 color)` - the parser; false for null, white space, `none`, and any unsupported text,
  never throwing.
- `Parse(string? text)` - the same but throws a `FormatException` naming the text when it is not a supported color.
- `IsNone(string? text)` - true for a paint that means "do not draw".
- `TryResolvePaint(string? paint, double opacity, out Rgba32 color)` - applies the no-paint rule and an opacity in one step
  (the opacity multiplies the color's alpha); false for no paint, for text that is neither a color nor none, and for a result
  that is fully transparent.
- `WithOpacity(Rgba32 color, double opacity)` - the color with its alpha scaled by an opacity from 0 to 1.
- `ToCss(Rgba32 color)` - the lower-case hexadecimal CSS text of a color (`#rrggbb`, or `#rrggbbaa` when it is not
  opaque), which `TryParse` reads back to the same color; used to turn the caller's `Background` option into the scene's
  string.
- `NamedColorCount` - 148 (the number of named colors, `transparent` not included).

**Preconditions and postconditions**: parsing is culture-invariant and deterministic.

#### Error Handling

`TryParse`, `IsNone`, and `TryResolvePaint` never throw for bad text: they return false. Only `Parse`, which exists for
callers that want a loud failure, throws a `FormatException` for text that is not a supported color. The unit performs no
I/O.

#### Dependencies

- **Core `CanvasNet`** - `Rgba32` (the `Canvas` subsystem), through its public API
- **.NET base class library** - `System.Globalization` (invariant-culture number parsing)

The unit has no dependency on another unit of the system.

#### Callers

`ScenePainter` parses every paint with `CssColor`; `VegaLiteDocument` converts the caller's `Background` with `ToCss`. The
tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/CssColorTests.cs` call the parser directly through
`InternalsVisibleTo`.
