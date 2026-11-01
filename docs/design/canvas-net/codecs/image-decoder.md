### ImageDecoder

![Codecs Structure](CodecsView.svg)

The `ImageDecoder` unit is the `Codecs` subsystem's format-neutral decoding facade. It is made of
three small public types in `Codecs/ImageFormat.cs`, `Codecs/ImageFormats.cs`, and
`Codecs/ImageDecoder.cs`: the `ImageFormat` enum, the static `ImageFormats` recognizer, and the
static `ImageDecoder` dispatcher. Together they let a caller (or another CanvasNet package) turn an
image of any supported format into a `Surface` without carrying its own per-format switch.

#### Purpose

Before this unit existed, three consumers each repeated the same dispatch: the `CanvasNetSvg`
system mapped a `data:` URI's MIME type to a codec's `Load`, `CanvasNetPptx` mapped an OPC content
type to the identical set of codecs, and the `CanvasNetTool` system sniffed a file's first bytes
(including a BMP plausibility check) to pick a source kind. `ImageDecoder` centralizes the
recognition (by signature and by media type) and the codec dispatch in the core package, where
library users can also use it directly. It adds **no decoding logic**: every `Load` call delegates
to the matching codec's existing `Load(Stream)`, so every codec limit, tolerance, and exception
contract applies unchanged.

#### Data Model

##### ImageFormat enum

| Value      | Codec           | Signature recognized by `ImageFormats.Detect`                         |
| ---------- | --------------- | --------------------------------------------------------------------- |
| `Png`      | `PngCodec`      | `89 50 4E 47 0D 0A 1A 0A`                                             |
| `Jpeg`     | `JpegCodec`     | `FF D8 FF`                                                            |
| `Gif`      | `GifCodec`      | `GIF87a` or `GIF89a`                                                  |
| `Bmp`      | `BmpCodec`      | `BM`, zero reserved fields, and a bitmap-header size from the list    |
| `Tiff`     | `TiffCodec`     | `II*\0` or `MM\0*`                                                    |
| `Jpeg2000` | `Jpeg2000Codec` | the 12-byte JP2 signature box, or a raw codestream (`FF 4F FF 51`)    |

The accepted bitmap-header sizes are 12, 40, 52, 56, 64, 108, and 124.

##### Recognized media types

`ImageFormats.FromMediaType` recognizes `image/png`; `image/jpeg` and `image/jpg`; `image/gif`;
`image/bmp`, `image/x-bmp` and `image/x-ms-bmp`; `image/tiff` and `image/x-tiff`; and `image/jp2`.
The input is trimmed, lower-cased, and cut at the first `;`. This is exactly the union of the
tables the `Svg` and `Pptx` dispatchers carried (plus `image/jp2`), so migrating those consumers
changed no behavior.

#### Key Methods

##### ImageFormats.Detect(ReadOnlySpan&lt;byte&gt; header)

Compares the leading bytes against the signatures above in a fixed order (PNG, JPEG, GIF, TIFF,
JPEG 2000, BMP) and returns the matching `ImageFormat`, or `null`. The two-letter BMP signature
alone also begins ordinary text, so a BMP additionally requires at least 18 bytes, zero reserved
fields at offsets 6-9, and a bitmap-header size at offset 14 from the accepted list. The method is
pure, never throws, and needs at most 18 bytes.

##### ImageFormats.FromMediaType(string? mediaType)

Normalizes the media type as described above and maps it with a `switch`; `null`, empty, and
unsupported types (including `image/svg+xml` and EMF/WMF metafiles) return `null`.

##### ImageDecoder.Load(Stream stream)

Reads up to 32 leading bytes, detects the format, and decodes. A seekable stream is rewound to the
position it started at (so a stream already positioned inside a larger container works); a
non-seekable stream is wrapped in a private forward-only `PrefixedStream` that replays the already
consumed header bytes and then continues with the underlying stream, so the image is never buffered
whole. The stream is read, not closed.

##### ImageDecoder.Load(ReadOnlySpan&lt;byte&gt; data)

Detects the format from the span and decodes it from a `MemoryStream` over a copy of the data.

##### ImageDecoder.Load(Stream stream, ImageFormat format)

Decodes the stream as the given format with a `switch` over the six codecs' `Load(Stream)`,
without sniffing the signature (the codec itself rejects data of another format).

**Throws (all overloads):**

- `ArgumentNullException` — `stream` is null
- `ArgumentOutOfRangeException` — `format` is not a defined `ImageFormat` (`Load(Stream, ImageFormat)`)
- `InvalidDataException` — no signature matched (`Load(Stream)`/`Load(ReadOnlySpan<byte>)`), or
  the chosen codec rejected the data
- Any other exception the chosen codec documents (for example `UnsupportedImageFeatureException`)

#### Error Handling

An unrecognizable input is reported as `InvalidDataException`, the same exception every codec uses
for malformed data, so callers need only one catch for "not a decodable image". Null and undefined
enum arguments are programming errors and throw immediately. No exception is swallowed or
translated: codec exceptions propagate unchanged.

#### Dependencies

`ImageDecoder` depends on `PngCodec`, `JpegCodec`, `GifCodec`, `BmpCodec`, `TiffCodec`, and
`Jpeg2000Codec` (their `Load(Stream)` methods) and on `Surface` as the return type. Beyond these it
uses only `System.IO` and `System.Buffers.Binary` from the .NET base class library.

#### Callers

`ImageDecoder`/`ImageFormats` are public API entry points for consumers of the CanvasNet package
and are called by three other CanvasNet packages: `SvgCodec` (`ResolveRasterDecoder` — a `data:`
URI's MIME type; JPEG 2000 is deliberately not embedded and the SVG/EMF types stay unsupported),
`PptxDocument` (`ResolveRasterDecoder` — a media part's OPC content type, same exclusions), and
the Tool's `SourceLoader.DetectSignature` (header bytes; the Tool keeps its own PDF, SVG, Vega-Lite,
OpenSCAD, and package kinds on top).
