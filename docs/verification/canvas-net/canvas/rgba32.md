### Rgba32 Unit Verification Design

The `Rgba32` `Parse`/`TryParse` methods and the public single-pixel `CompositeOver` helper are
verified by `test/DemaConsulting.CanvasNet.Tests/Canvas/Rgba32Tests.cs`.

### Verification Approach

- **Six-hex form**: `Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255`,
  `Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255`, and
  `Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba` verify case-insensitive #RRGGBB parsing
  with alpha defaulting to 255.
- **Eight-hex form**: `Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb` and
  `Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb` verify case-insensitive #AARRGGBB parsing.
- **Invalid inputs**: missing-hash, short forms (#RGB, #ARGB), wrong length (7 and 9 hex),
  non-hex characters, null, and empty string all throw the correct exception type; the
  format-guidance test verifies the message names the accepted formats.
  `Rgba32_Parse_MissingHashPrefixAtValidLength_MessageIdentifiesMissingHash` verifies the
  missing-`#` message specifically names the missing prefix rather than the generic format
  message, and `Rgba32_Parse_NonHexCharacter_MessageIncludesOffendingCharacter` verifies the
  non-hex-character message includes the actual offending character.
- **TryParse**: valid, invalid, and null inputs all behave per contract, with `result` set to
  `default` on failure.
- **ToString**: representative channel values (including a multi-digit and a zero channel) render
  as `Rgba32(R, G, B, A)` under a non-invariant current culture.
- **CompositeOver** (public single-pixel Porter-Duff "over" helper): a fully
  transparent foreground leaves the background unchanged; a fully opaque foreground exactly
  replaces it; a partially transparent foreground over an opaque background, and both background
  and foreground partially transparent, each blend to exact expected bytes computed
  independently per the documented formula; both fully transparent composite to the zeroed
  degenerate result rather than a `NaN`; a grid of 1,024 background/foreground pairs agrees with
  `Surface.CompositeOver(Rgba32)`, and a reflection test confirms the member is public.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every test operates purely on `string`/`Rgba32` values
- **Isolation**: Each test method is self-contained, with no shared state between tests

### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception,
and when every named test method listed for each requirement ID passes across every target
framework.

### Test Scenarios

#### CanvasNet-Canvas-Rgba32-ParseSixHex: Six-Hex-Digit Strings Parse to RGB With Alpha 255

**Tests**: `Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255`,
`Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255`,
`Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba`

Verifies case-insensitive `#RRGGBB` parsing with alpha defaulting to 255.

#### CanvasNet-Canvas-Rgba32-ParseEightHex: Eight-Hex-Digit Strings Parse to ARGB

**Tests**: `Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb`,
`Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb`

Verifies case-insensitive `#AARRGGBB` parsing.

#### CanvasNet-Canvas-Rgba32-ParseValidation: Invalid Inputs Are Rejected With Specific Error Messages

**Tests**: `Rgba32_Parse_MissingHash_ThrowsFormatException`,
`Rgba32_Parse_MissingHashPrefixAtValidLength_MessageIdentifiesMissingHash`,
`Rgba32_Parse_3HexShortForm_ThrowsFormatException`,
`Rgba32_Parse_4HexArgbShortForm_ThrowsFormatException`, `Rgba32_Parse_7Hex_ThrowsFormatException`,
`Rgba32_Parse_9Hex_ThrowsFormatException`, `Rgba32_Parse_NonHexCharacter_ThrowsFormatException`,
`Rgba32_Parse_NonHexCharacter_MessageIncludesOffendingCharacter`,
`Rgba32_Parse_Null_ThrowsArgumentNullException`, `Rgba32_Parse_EmptyString_ThrowsFormatException`,
`Rgba32_Parse_ExceptionMessage_ContainsFormatGuidance`

Verifies missing-hash, short forms (`#RGB`, `#ARGB`), wrong length (7 and 9 hex digits), non-hex
characters, `null`, and an empty string all throw the correct exception type (`FormatException`,
except `ArgumentNullException` for `null`), and that the format-guidance test verifies the
message names the accepted formats. The missing-`#` message specifically names the missing
prefix rather than the generic format message, and the non-hex-character message includes the
actual offending character.

#### CanvasNet-Canvas-Rgba32-TryParse: TryParse Never Throws and Reports Success/Failure via Its Return Value

**Tests**: `Rgba32_TryParse_ValidInput_ReturnsTrueAndSetsResult`,
`Rgba32_TryParse_InvalidInput_ReturnsFalseAndSetsResultToDefault`,
`Rgba32_TryParse_Null_ReturnsFalse`

Verifies valid, invalid, and `null` inputs all behave per contract, with `result` set to
`default` on failure.

#### CanvasNet-Canvas-Rgba32-ToString: Readable Invariant-Culture String Representation

**Tests**: `Rgba32_ToString_RepresentativeChannels_ReturnsInvariantFormattedString`

Verifies `ToString()` returns `Rgba32(R, G, B, A)` with decimal channel values, unaffected by the
current culture.

#### CanvasNet-Canvas-Rgba32-CompositeOver: Shared Single-Pixel Porter-Duff "Over" Alpha Compositing

**Tests**: `Rgba32_CompositeOver_FullyTransparentForeground_ReturnsBackgroundUnchanged`,
`Rgba32_CompositeOver_FullyOpaqueForeground_ReturnsForegroundExactly`,
`Rgba32_CompositeOver_PartiallyTransparentForegroundOverOpaqueBackground_BlendsExactly`,
`Rgba32_CompositeOver_BothBackgroundAndForegroundPartiallyTransparent_BlendsExactly`,
`Rgba32_CompositeOver_BothBackgroundAndForegroundFullyTransparent_ReturnsZeroedResult`,
`Rgba32_CompositeOver_MatchesSurfaceCompositeOverColor_AcrossAlphaGrid`,
`Rgba32_CompositeOver_IsPublicStaticMethod`

Proves the public single-pixel `CompositeOver(Rgba32, Rgba32)` helper - called by
`DemaConsulting.CanvasNet.Pptx`'s `PaintPicture` and `DemaConsulting.CanvasNet.Pdf`'s
`CompositeImageOntoSurface` - implements the documented Porter-Duff "over" formula: a fully
transparent foreground (`alpha == 0`) leaves the background pixel completely unchanged,
regardless of the (irrelevant) RGB stored alongside that zero alpha; a fully opaque foreground
(`alpha == 255`) exactly replaces the background pixel; a partially transparent foreground
composited over a fully opaque background, and two partially transparent pixels composited
together, each blend to exact expected bytes computed independently via the documented formula
with round-half-away-from-zero; and two fully transparent pixels composite to the fully
transparent, zeroed-color degenerate result rather than a division-by-zero `NaN`. The grid test
crosses eight alphas (0, 1, 64, 127, 128, 200, 254, 255) with four colors for both background and
foreground (1,024 pairs) and asserts every result equals what `Surface.CompositeOver(Rgba32)`
writes into a one-row surface holding the backgrounds, so the single-pixel helper and the
vectorized surface operation are interchangeable. A reflection test asserts the method is a
public static member of the public struct.

### Traceability

Every requirement in `docs/reqstream/canvas-net/canvas/rgba32.yaml` links to one or more of
these tests.
