### CssColor Unit Verification Design

The `CssColor` class - the shared CSS color parser and formatter of the `Canvas` subsystem - is verified by
`test/DemaConsulting.CanvasNet.Tests/Canvas/CssColorTests.cs`.

#### Verification Approach

The unit is verified by parsing colors and comparing them with the pixel values that CSS defines for the text, which the
tests state as `Rgba32` literals worked out by hand. The package consumers keep adapter tests of their own (the SVG codec,
the Vega-Lite paint rules, the OpenSCAD `color` module, and the command-line value grammar), while the grammar itself is
verified only here.

- **Hexadecimal forms**: `#4c78a8` is (76, 120, 168, 255); the short form `#abc` expands each digit to a pair,
  (170, 187, 204, 255), and `#abcd` adds the alpha 0xdd = 221; `#4c78a8cc` has the alpha 0xcc = 204; `#FFF` and
  `#ddd` check the letter case and the surrounding blanks. `TryParseHex` is checked for the four valid lengths and
  for lengths 0, 1, 2, 5, 7, and 9, a leading `#`, signs, a blank, non-hexadecimal characters, and non-ASCII digits.
  `#11223380` gives the alpha 0x80 last, while `Rgba32.Parse("#11223380")` reads the alpha first.
- **Named colors**: the count is 148 (the 147 names of CSS Color 3 plus `rebeccapurple`), and nineteen names spread over
  the alphabet are compared with their hexadecimal spellings, in lower and upper case; `grey` equals `gray`;
  `transparent` is (0, 0, 0, 0). The names are stored as PascalCase words with `0xRRGGBB` numbers, so the source needs no
  spell-checker suppression; the test is the evidence that this representation gives exactly the CSS values, and the
  repository spell check (`lint.ps1`) is the evidence that no suppression or word-list entry is needed. Names with
  surrounding blanks, hexadecimal, function text, and unknown names are not names.
- **Functions**: `rgba(255,0,0,0.5)` has the alpha 0.5 x 255 = 127.5, which rounds to 128; `rgb(10% 20% 30% / 50%)` is
  (26, 51, 77, 128) because 10% of 255 is 25.5, which rounds up to 26; `hsl(120 100% 25%)` is (0, 128, 0),
  `hsl(0.5turn 100% 50%)` and `hsl(180deg, 100%, 50%)` are both cyan, `hsl(-120, 100%, 50%)` is blue, and
  `hsl(200 0% 40%)` is the gray (102, 102, 102).
- **Rounding and clamping**: `rgb(300, -5, 0)` clamps to (255, 0, 0); `rgba(1, 2, 3, 7)` clamps the alpha to 255 and
  `rgba(1, 2, 3, -1)` to 0; `rgb(30%, 0, 0)` is 76.5, which is 77 (half to even would give 76); `rgb(0.5, 1.5, 2.5)` is
  (1, 2, 3).
- **Invalid text**: null, blank text, `none`, malformed hexadecimal and function text, an unknown name, non-numeric,
  `NaN`, and overflowing numbers all make `TryParse` return `false` with the default color; `Parse` throws.
- **Formatting**: `ToCss` gives `#4c78a8` and `#4c78a880`, and parsing the result of `ToCss` gives back the same color.

The unit has no dependencies other than `Rgba32`, so no mocking or stubbing is required.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every test operates purely on `string` and `Rgba32` values
- **Isolation**: Each test method is self-contained, with no shared state between tests

#### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception, and when every named
test method listed for each requirement ID passes across every target framework. Any color that parses to a value other
than the hand-derived pixel, any invalid text that is accepted, or any exception other than the documented one
constitutes a failure.

#### Test Scenarios

##### CanvasNet-Canvas-CssColor-ParseHex: Hexadecimal Forms

**Tests**: `CssColor_TryParse_HexForms_MatchExpectedPixels`,
`CssColor_TryParseHex_DigitsOnly_AcceptsExactLengthsAndRejectsEverythingElse`,
`CssColor_Parse_EightDigitHex_PutsAlphaLastUnlikeRgba32Parse`

Verifies the 3, 4, 6, and 8 digit forms through `TryParse` and `Parse`, the digit-only `TryParseHex`, and the rejection
of every other length and character; verifies that the alpha pair is last.

##### CanvasNet-Canvas-CssColor-NamedColors: Named Colors

**Tests**: `CssColor_TryGetNamedColor_Names_ReturnTheCssTableEntries`,
`CssColor_TryGetNamedColor_EveryCssName_ReturnsTheSpecificationValue`,
`CssColor_TryGetNamedColor_NotAName_ReturnsFalseWithDefaultColor`, `CssColor_TryParse_Names_MatchExpectedPixels`

Verifies the count of 148, the sampled values against hexadecimal spellings in lower and upper case, every one of the
148 names (and `transparent`) in mixed, lower, and upper case against a table of the specification values written out
independently in the test (so an incorrect or extra production entry fails), the `gray` and `grey` pairs, and that the
lookup is exact for anything that is not a name.

##### CanvasNet-Canvas-CssColor-Functions: rgb and hsl Functions

**Tests**: `CssColor_TryParse_FunctionForms_MatchExpectedPixels`,
`CssColor_TryParse_MixedOrMalformedSyntax_ReturnsFalseWithDefaultColor`,
`CssColor_TryParse_HueOverflowingAfterUnitConversion_ReturnsFalse`

Verifies `rgb()`, `rgba()`, `hsl()`, and `hsla()` in comma and space-and-slash syntax, with numbers, percentages, the
`none` keyword (space syntax), hue units, `hsl(0 100 50)` with plain numbers in the space syntax, and the alpha as a
number or a percentage; verifies that the syntaxes are never mixed (`rgb(1 2 3 0.5)`, `rgb(1,2,3 / 0.5)`, empty
components, trailing commas, `none` or bare-number saturation in the comma syntax are rejected); and that
`hsl(1e308turn,100%,50%)`, whose converted hue overflows, is rejected rather than read as black.

##### CanvasNet-Canvas-CssColor-RoundingAndClamping: Rounding And Clamping

**Tests**: `CssColor_TryParse_OutOfRangeAndFractions_ClampAndRoundHalfAwayFromZero`

Verifies that out-of-range channels and alpha values are clamped and that the exact halves 76.5, 127.5, 0.5, 1.5, and
2.5 round away from zero.

##### CanvasNet-Canvas-CssColor-InvalidText: Invalid Text Fails Cleanly

**Tests**: `CssColor_TryParse_InvalidText_ReturnsFalseWithDefaultColor`,
`CssColor_Parse_InvalidText_ThrowsFormatException`, `CssColor_Parse_Null_ThrowsArgumentNullException`

Verifies that every invalid text makes `TryParse` return `false` with the default color, that `Parse` throws
`FormatException` naming the text, and that `Parse(null)` throws `ArgumentNullException`.

##### CanvasNet-Canvas-CssColor-ToCss: Formatting And Round Trip

**Tests**: `CssColor_ToCss_Colors_FormatAsHexAndRoundTrip`

Verifies the six-digit and eight-digit spellings and the round trip of three colors, including the fully transparent
and the fully opaque white.
