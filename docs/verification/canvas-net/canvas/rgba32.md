### Rgba32 Unit Verification Design

The `Rgba32` `Parse` and `TryParse` methods are verified by
`test/DemaConsulting.CanvasNet.Tests/Canvas/Rgba32Tests.cs`.

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

### Traceability

Every requirement in `docs/reqstream/canvas-net/canvas/rgba32.yaml` links to one or more of
these tests.
