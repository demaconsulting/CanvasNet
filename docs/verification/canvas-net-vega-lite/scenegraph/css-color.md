### CssColor Unit Verification Design

This document describes the unit-level verification strategy for the `CssColor` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal static `CssColor` class (`TryParse`, `Parse`,
`IsNone`, `TryResolvePaint`, `WithOpacity`, `ToCss`, and `NamedColorCount`). The unit parses the CSS color text that
specifications and configurations use — hexadecimal forms, the named colors and `transparent`, `rgb()` and `rgba()`, and
`hsl()` and `hsla()` — into a straight-alpha `Rgba32`, resolves a paint (a color string and an opacity) to the color
that is drawn, and prints a color back as the shortest exact hexadecimal text.

#### Verification Approach

The unit is verified by parsing colors and comparing them with the pixel values that CSS defines for the text, which the
tests state as `Rgba32` literals worked out by hand: `#4c78a8` is (76, 120, 168, 255); the short form `#abc` expands each
digit to a pair, (170, 187, 204, 255), and `#abcd` adds the alpha 0xdd = 221; `#4c78a8cc` has the alpha 0xcc = 204;
`steelblue` is (70, 130, 180, 255) and `rebeccapurple` (102, 51, 153, 255) in any letter case; `rgba(255,0,0,0.5)` has
the alpha 0.5 x 255 = 127.5, which rounds to 128; `rgb(10% 20% 30% / 50%)` is (26, 51, 77, 128) because 10% of 255 is
25.5, which rounds up to 26; `rgba(1, 2, 3, 7)` clamps the alpha to 255 and `rgba(1, 2, 3, -1)` to 0;
`rgb(300, -5, 0)` clamps to (255, 0, 0); `hsl(120 100% 25%)` is (0, 128, 0), `hsl(0.5turn 100% 50%)` and
`hsl(180deg, 100%, 50%)` are both cyan, `hsl(-120, 100%, 50%)` is blue, and `hsl(200 0% 40%)` is the gray
(102, 102, 102). The table of named colors is checked for its count (148: the 147 names of CSS Color 3 plus
`rebeccapurple`) and against fifteen names spread over the alphabet whose values are written as hexadecimal literals
and compared with the parsed names. The unit has no dependencies other than the core `Rgba32`, so no mocking or
stubbing is required; the internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `CssColorTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any color that parses to a value other than the hand-derived pixel, any invalid text that is accepted or that
throws something other than the documented exception, or any paint that is resolved when none is documented
constitutes a failure.

#### Unit-Level Test Scenarios

##### Supported Forms Match The Derived Pixels

**Tests**: `CssColorTests.CssColor_Parse_SupportedFormsMatchRgba32`

Asserts, through both `TryParse` and `Parse`, the thirty derived color texts: hexadecimal in the 3, 4, 6, and 8 digit
forms in any letter case and with surrounding blanks, named colors in any letter case (including `grey` and `gray`) and
`transparent`, `rgb()` and `rgba()` with integers, percentages, alpha as a number or a percentage, the comma and the space
syntax, and clamping, and `hsl()` and `hsla()` with the hue in degrees, turns, and negative degrees, percentages, and alpha.

##### Named Colors Are Complete

**Tests**: `CssColorTests.CssColor_Parse_NamedColorsAreComplete`

Asserts that the table has 148 names and that fifteen names across the alphabet (for example `aliceblue` is `#f0f8ff`,
`crimson` is `#dc143c`, `lightgoldenrodyellow` is `#fafad2`, and `yellowgreen` is `#9acd32`) parse to the same color as
their hexadecimal spelling.

##### Invalid Text Fails Cleanly

**Tests**: `CssColorTests.CssColor_Parse_InvalidTextFailsCleanly`

Asserts that null, empty and blank text, `none`, malformed hexadecimal (`#`, `#12`, `#12345`, `#1234567`, `#ggg`,
`#12 456`), an unknown name, malformed functions (`rgb`, `rgb()`, too few or too many arguments, an unclosed
parenthesis, NaN, an overflowing number, non-numeric arguments, `hsl` with a text hue or too few arguments, an
unknown function, trailing text, and a bare parenthesis) all make `TryParse` return false with the default color, and
that `Parse` throws `FormatException` (asserted for `bogus` and null).

##### Paint Rules And Round Trip

**Tests**: `CssColorTests.CssColor_ResolvePaint_NoneOpacityAndRoundTrip`

Asserts that null, empty, and ` NONE ` are no paint while `transparent` is a color; that no paint, `none`, garbage,
`transparent`, and an opacity of 0 resolve to nothing; that the opacity multiplies the alpha of the color and is
clamped to 0 to 1 with NaN counting as 1 (`#000` at 0.5 is alpha 128, `rgba(0,0,0,0.5)` at 0.5 is alpha 64 because
0.25 x 255 = 63.75 rounds to 64, and `#123456` at 7 stays opaque); and that `ToCss` gives the shortest exact
hexadecimal form (`#4c78a8`, and `#4c78a880` with the alpha 128) that parses back to the same color, for three colors
including the fully transparent and the fully opaque white.
