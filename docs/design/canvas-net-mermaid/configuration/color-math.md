### ColorMath

![Configuration Structure](MermaidConfigurationView.svg)

The `ColorMath` unit is the third software unit in the `Configuration` subsystem. It is the internal static partial
class `ColorMath` in two source files of the `DemaConsulting.CanvasNet.Mermaid.Configuration` namespace - `ColorMath.cs`
(the color functions, the channel conversions, rounding, number formatting, and output formats, with the grouped
exception type `ColorFormatException`) and `ColorMath.Parsers.cs` (khroma's keyword table, the channel model
`Channels`, and the parsers of the hexadecimal, `rgb()`, `hsl()`, and keyword forms with their scanner).

#### Purpose

Mermaid derives most theme colors with the khroma color library, and the text of a derived color is what reaches the
style sheet and the theme variables. `ColorMath` ports the subset of khroma that Mermaid's themes and styles use, with
every detail that shows in the text: the parsers, the lazy RGB and HSL channel model that decides whether a result is
written as `#rrggbb`, `rgb()`, `rgba()`, `hsl()`, or `hsla()`, an unchanged color returned as it was written, rounding
to ten decimals, JavaScript's number formatting, and khroma's arithmetic quirks (a missing amount gives `NaN`, a hue
is reduced with the sign-preserving remainder, and the luminance threshold compares the unscaled channel).

The unit's behavior is specified by the `CanvasNetMermaid-Configuration-ColorMath-*` requirements in
`docs/reqstream/canvas-net-mermaid/configuration/color-math.yaml`.

#### Data Model

- **`Channels`** - khroma's color object: the red, green, and blue channels and the hue, saturation, and lightness
  channels, each computed lazily from the other family, the alpha, the text the color was parsed from (`Color`),
  whether a channel was changed (`Changed`), and through which family it was changed (`Kind`, khroma's `TYPE`:
  `All`, `Rgb`, or `Hsl`). Changing a channel of one family after the other is an error, as in khroma. Where khroma
  reuses one channel object, every call here creates its own, which gives the same results for every call the themes
  make and makes the unit safe for concurrent use.
- **Keyword table** - khroma's own table of color keywords (a frozen dictionary, matched without regard to letter case;
  it differs from the CSS named colors, for example it has `CyanAqua` and lacks `steelblue`).
- **`ColorFormatException`** - a `FormatException` whose `Value` is the value that is not a color (a string in none of
  khroma's formats, with khroma's message, or a value that is not a string).
- **`Missing`** - the value of an amount that is missing in JavaScript (`NaN`).

#### Key Methods

- `Parse(string color)`, `IsColor(string color)`, and `Stringify(Channels channels)` - khroma's `parse` and
  `stringify`: the hexadecimal forms (3, 4, 6, and 8 digits), `rgb()` and `rgba()`, `hsl()` and `hsla()` (with angle
  units, percentages, signs, and exponents where khroma accepts them), and the keywords; the output form follows the
  channel model. khroma's regular expressions are replaced by a scanner (`ColorScanner`) with the same matches, so
  parsing is linear in the length of the text.
- `Adjust(color, h, s, l, r, g, b)`, `Darken(color, amount)`, `Lighten(color, amount)`, `Invert(color)`,
  `Mix(color1, color2, weight)`, `Rgba(color, alpha)`, and `Rgba(r, g, b, a)` - khroma's functions, which return the
  color text (an unchanged color in its written form, a keyword as its hexadecimal form).
- `Channel(color, channel)`, `Luminance(color)`, and `IsDark(color)` - khroma's channel reading, relative luminance,
  and `isDark` (luminance below 0.5).
- `Round(number)`, `JsRound(number)`, and `FormatNumber(number)` - khroma's rounding to ten decimals, JavaScript's
  `Math.round` (halves toward positive infinity), and JavaScript's `Number.prototype.toString` (shortest round-trip
  digits, exponent forms below 1e-6 and from 1e21, `NaN`, and negative zero as `0`).
- `Same(a, b)` and `IsTruthy(value)` - the bit-pattern equality and JavaScript truthiness of numbers used by the
  resolver and the themes.

**Preconditions and postconditions**: the color arguments are JavaScript values (a string, or the `Channels` of an
earlier step); the methods are pure functions of their arguments, culture-invariant, and deterministic.

#### Error Handling

A color function throws `ColorFormatException` when its color is not a string or is a string in none of khroma's
formats (khroma's own error), and `InvalidOperationException` when both RGB and HSL channels are changed in one step
(khroma's error). The exceptions propagate to the theme computation, where `MermaidConfigResolver` handles them.

#### Known Differences From Mermaid

- A color function given a value that is not a string throws `ColorFormatException`; khroma would produce `NaN` in a
  few code paths. The themes never pass such a value unless the user's theme variables do, and the resolver drops that
  variable.

#### Dependencies

- **.NET base class library** - `System.Math`, `System.Globalization`, and `System.Collections.Frozen`

The unit has no dependency on another unit of the system.

#### Callers

`ThemeResolver` derives every theme color with it (`Adjust`, `Darken`, `Lighten`, `Invert`, `Rgba`, and `IsDark`), and
its `FlowchartStyles` fades colors with it; `MermaidConfigResolver` catches `ColorFormatException` and uses `Same`, and
`MermaidConfig.ThemeVariable` formats numbers with `FormatNumber`. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ColorMathTests.cs` call the unit directly through
`InternalsVisibleTo`, against the khroma vectors of `Fixtures/upstream-vectors/themes.json`.
