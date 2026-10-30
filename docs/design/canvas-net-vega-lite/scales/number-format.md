### NumberFormat

![Scales Structure](VegaLiteScalesView.svg)

The `NumberFormat` unit is the fourth software unit in the `Scales` subsystem. It is one source file, `NumberFormat.cs`,
in the `DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines two `internal` types: `FormatSpecifier` (sealed
record) and `NumberFormat` (sealed partial class, with a generated regular expression for the specifier grammar). The file
carries the provenance header comment for the d3-format and d3-scale behavior it ports.

#### Purpose

`NumberFormat` formats numbers the way d3-format does, so that axis tick labels read as they do in Vega-Lite: the
specifier mini-language (`,.2f`, `~s`, `d`, `.0%`, and so on), the SI prefixes, the grouping and sign rules, and the
precision that `tickFormat` derives from the tick step. It exists so that number formatting is a faithful, exact,
culture-invariant port, independent of the platform's own number formatting.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-NumberFormat-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/number-format.yaml`.

#### Data Model

**`FormatSpecifier`** (sealed record, immutable) holds the parts of a d3 specifier: `Fill`, `Align`, `Sign`, `Symbol`,
`Zero`, `Width`, `Comma`, `Precision`, `Trim`, and `Type`; `ToString()` returns the specifier text in d3's form.

**`NumberFormat`** (immutable and thread-safe) holds a parsed specifier and, for a prefix format, the SI prefix exponent.
The numeric types are `e f g r s % p b o d x X c n` and the empty type; the locale is d3's default `en-US` (a period as
decimal point, a comma as the grouping separator, grouping of three digits), and the minus sign
is U+2212, as in d3. The SI prefixes run from yocto to yotta. Widths above 1,000 are clamped (a denial-of-service guard
for hostile specifiers).

**Digits** that JavaScript's `toFixed`, `toExponential`, and `toPrecision` would produce are reproduced from the exact
decimal expansion of the double (using `BigInteger` and the shortest digits of `JsNumber`), so the result does not depend
on the platform's floating-point formatting.

#### Key Methods

- `ParseSpecifier(string)` and `TryParseSpecifier(string?, out FormatSpecifier)` - parse the d3 specifier grammar; an
  invalid specifier is a `FormatException` with the message `invalid format: ...` (as d3's error).
- `Create(string)` and `Create(FormatSpecifier)` - build a formatter; `CreatePrefix(specifier, value)` builds d3's
  `formatPrefix`: the SI prefix is chosen from the reference value so that a set of numbers shares one prefix.
- `Format(double)` - applies the sign, symbol, zero padding, width, grouping, precision, and trim rules of d3 for the
  type; NaN and infinity format as d3 does (`d` of infinity gives the infinity sign).
- `TickFormat(start, stop, count, specifier)` - d3-scale's `tickFormat`, which is also what Vega's axis label format
  (`formatSpan`) uses: with no specifier it is `,f` with the precision of the tick step (`precisionFixed`); with a
  specifier of type `s` it uses `precisionPrefix`, and with types `e g p r` it uses `precisionRound`.
- `PrecisionFixed(step)`, `PrecisionPrefix(step, value)`, and `PrecisionRound(step, max)` - d3's precision helpers (NaN
  is tolerated). For a domain without span the tick step is zero and `exponent(0)` is NaN in d3-format 3.1.2 (the version
  followed; 3.1.0 returned 0), so no precision is derived and a specifier without one keeps d3's default of six decimals
  (`5.000000` for `,f`); this is pinned by a test.
- `FormatFloat(specifier)` - Vega's `formatFloat`, the label format of a log axis and legend: the specifier defaults to
  `,`; one without a precision gets 12 (10 for `%`, 11 for `e`) and the zeros after the decimal point are trimmed with the
  point when nothing remains, in front of an exponent or a unit suffix (`TrimZeroes`, Vega's `trimZeroes`), so 1000 is
  `1,000`, 0.001 is `0.001`, and 1e-7 is `1e-7`; one with a precision is used as it is.

**Preconditions and postconditions**: a specifier is non-null text; `Format` is deterministic and never depends on the
current culture.

#### Error Handling

An invalid specifier is a `FormatException` (`invalid format: ...`), and a null specifier is `ArgumentNullException`;
`AxisBuilder` converts the `FormatException` of a user-supplied `axis.format` into a `VegaLiteSpecException` located at
the `format` value. Numeric edge cases do not throw: NaN and infinity have defined text forms and an oversized width is
clamped. The unit performs no I/O.

#### Dependencies

- **`JsNumber`** - JavaScript number text and digits, from the `DataTable` unit
- **.NET base class library** - `System.Numerics` (`BigInteger`), `System.Text.RegularExpressions` (the specifier grammar,
  generated at build time), `System.Globalization` (invariant culture), and `System.Text`

#### Callers

`AxisBuilder` formats tick labels with `TickFormat` and `Create`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/NumberFormatTests.cs` call the formatter directly through
`InternalsVisibleTo`, including under a non-English culture to prove culture independence.
