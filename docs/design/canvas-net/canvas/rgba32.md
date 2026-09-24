### Rgba32 Unit Design

The `Rgba32` struct is the pixel color type used throughout CanvasNet. Beyond its four-byte
channel layout, this unit design covers the `Parse` and `TryParse` methods that construct an
`Rgba32` from a hex color literal string.

#### Parse and TryParse

Two accepted forms:

- `"#RRGGBB"` — the six-hex form. Red, green, and blue channels come from the string; alpha
  is set to `255` (fully opaque).
- `"#AARRGGBB"` — the eight-hex form. Alpha, red, green, and blue channels all come from the
  string.

Both forms are case-insensitive. The leading `#` is required; short forms (`#RGB`, `#ARGB`),
missing `#`, wrong length, and non-hex characters are all rejected.

`Parse(string)` throws:

- `ArgumentNullException` on a null argument.
- `FormatException` on any other invalid input. The message names the accepted formats or,
  for a non-hex character, identifies the invalid character.

`TryParse(string?, out Rgba32)` returns:

- `true` and populates `result` on a valid input.
- `false` and sets `result` to `default` on any invalid input, including `null`.

Both methods share a single private `TryParseCore` implementation so their accept/reject
behavior is guaranteed to agree.
