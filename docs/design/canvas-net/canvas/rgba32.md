### Rgba32 Unit Design

The `Rgba32` struct is the pixel color type used throughout CanvasNet. Beyond its four-byte
channel layout, this unit design covers the `Parse` and `TryParse` methods that construct an
`Rgba32` from a hex color literal string.

#### Purpose

`Rgba32` is a plain, four-byte carrier of RGBA channel values with no additional behavior beyond
value equality, hex-string parsing, and (internal only) a single-pixel Porter-Duff "over"
alpha-blending helper reused by sibling renderer packages. Keeping it a minimal, fixed-layout
struct lets `Surface`
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

##### CompositeOver(Rgba32 background, Rgba32 foreground) — internal

A single-pixel counterpart of `Surface.CompositeOver(Rgba32)`'s own per-row blend pipeline,
computing the identical standard Porter-Duff "over" alpha-compositing formula for exactly one
pixel: with `fgA`/`bgA` normalized to `[0, 1]`, `outA = fgA + bgA * (1 - fgA)`, and for each color
channel, `outC = (fgC * fgA + bgC * bgA * (1 - fgA)) / outA` when `outA != 0`, otherwise `outC = 0`
(the same "alpha == 0 implies every color channel is 0" degenerate case `Surface`'s own
`ZeroColorWhereAlphaByteIsZero` documents). Every intermediate result is rounded
half-away-from-zero and clamped to `[0, 255]` before narrowing to a `byte`, matching `Surface`'s
own `NarrowRoundedClamp` rounding rule exactly.

`internal` (not a public API): exposed cross-assembly only to the two sibling renderer packages
that each composite a decoded source image onto a destination `Surface` one sampled pixel at a
time — `DemaConsulting.CanvasNet.Pptx`'s `PptxDocument.Images.cs`'s `PaintPicture` and
`DemaConsulting.CanvasNet.Pdf`'s `PdfDocument.Images.cs`'s `CompositeImageOntoSurface` — via this
assembly's `InternalsVisibleTo` grants (alongside the existing grant to
`DemaConsulting.CanvasNet.Pdf` for `Fonts.SystemFontCatalog` reuse). Introduced to fix a
confirmed real-world bug: both renderers previously overwrote the destination pixel outright
with the raw sampled source pixel, ignoring its alpha channel entirely — a source pixel with a
non-opaque (including fully transparent) alpha channel replaced existing destination content with
whatever RGB value happened to be stored alongside that non-opaque alpha (for example a logo PNG
with an unassociated-alpha white matte painting a solid white rectangle instead of a transparent
background). Factored into this one shared helper, rather than duplicated independently in both
renderer assemblies, so the identical blending math is written and tested exactly once.

#### Error Handling

`Parse(string)` throws `ArgumentNullException` on a null argument, and `FormatException` on any
other invalid input (wrong length, missing `#`, or a non-hex character) - the message names the
accepted formats or identifies the invalid character. `TryParse(string?, out Rgba32)` never
throws: it returns `false` and sets `result` to `default` on any invalid input, including `null`.
No other member of this unit validates its input or throws.

#### Dependencies

`Rgba32` depends only on `System.Globalization.CultureInfo`/`NumberStyles` (used by `Parse`/
`TryParse`'s hex-digit decoding), `System.Runtime.InteropServices.StructLayoutAttribute`, and
`System.MathF`/`System.Math` (used by `CompositeOver`'s rounding/clamping) from the .NET Base
Class Library. It has no dependency on any other CanvasNet unit.

#### Callers

`Rgba32` is a public API entry point, used throughout CanvasNet wherever a pixel color value is
read or written: `Surface` (as the element type of its pixel spans), every `Codecs` unit, and
every `Drawing` and `Fonts`/`Rendering` unit that accepts or returns a fill color. The internal
`CompositeOver(Rgba32, Rgba32)` helper is additionally called cross-assembly by
`DemaConsulting.CanvasNet.Pptx`'s `PaintPicture` and `DemaConsulting.CanvasNet.Pdf`'s
`CompositeImageOntoSurface` (see those packages' own design documentation).
