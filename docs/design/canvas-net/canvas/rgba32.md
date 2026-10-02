### Rgba32 Unit Design

The `Rgba32` struct is the pixel color type used throughout CanvasNet. Beyond its four-byte
channel layout, this unit design covers the `Parse` and `TryParse` methods that construct an
`Rgba32` from a hex color literal string.

#### Purpose

`Rgba32` is a plain, four-byte carrier of RGBA channel values with no additional behavior beyond
value equality and hex-string parsing. Keeping it a minimal, fixed-layout struct lets `Surface`
row buffers be reinterpreted between raw `byte` spans and `Rgba32` spans via
`System.Runtime.InteropServices.MemoryMarshal` with no copying or conversion. Additional pixel
formats (if ever needed) would be introduced as new standalone struct types rather than by
extending this one, keeping this type's memory layout stable.

#### Data Model

`Rgba32` is a `[StructLayout(LayoutKind.Sequential)]` struct implementing `IEquatable<Rgba32>`,
with four public `byte` fields in declaration order (matching its sequential memory layout):

- `R` — the red channel value
- `G` — the green channel value
- `B` — the blue channel value
- `A` — the alpha (opacity) channel value

There are no invariants beyond the field types themselves - every combination of four `byte`
values is a valid `Rgba32`. The constructor `Rgba32(byte r, byte g, byte b, byte a)` sets all four
fields directly.

#### Key Methods

##### Equals(Rgba32) / Equals(object?) / GetHashCode / == / !=

Value equality compares all four channels. `GetHashCode` packs the four channels into a single
`int` (`(R << 24) | (G << 16) | (B << 8) | A`) for a fast, allocation-free hash consistent with
`Equals`, without depending on `System.HashCode` (not available on all supported target
frameworks).

##### Parse and TryParse

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

#### Error Handling

`Parse(string)` throws `ArgumentNullException` on a null argument, and `FormatException` on any
other invalid input (wrong length, missing `#`, or a non-hex character) - the message names the
accepted formats or identifies the invalid character. `TryParse(string?, out Rgba32)` never
throws: it returns `false` and sets `result` to `default` on any invalid input, including `null`.
No other member of this unit validates its input or throws.

#### Dependencies

`Rgba32` depends only on `System.Globalization.CultureInfo`/`NumberStyles` (used by `Parse`/
`TryParse`'s hex-digit decoding) and `System.Runtime.InteropServices.StructLayoutAttribute` from
the .NET Base Class Library. It has no dependency on any other CanvasNet unit.

#### Callers

`Rgba32` is a public API entry point, used throughout CanvasNet wherever a pixel color value is
read or written: `Surface` (as the element type of its pixel spans), every `Codecs` unit, and
every `Drawing` and `Fonts`/`Rendering` unit that accepts or returns a fill color.
