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
- **TryParse**: valid, invalid, and null inputs all behave per contract, with `result` set to
  `default` on failure.

### Traceability

Every requirement in `docs/reqstream/canvas-net/canvas/rgba32.yaml` links to one or more of
these tests.
