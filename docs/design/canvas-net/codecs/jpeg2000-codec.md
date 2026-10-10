### Jpeg2000Codec

![Codecs Structure](CodecsView.svg)

The `Jpeg2000Codec` class is a software unit of CanvasNet's `Codecs` subsystem. It
provides a hand-rolled, decode-only implementation of JPEG 2000 Part 1 (ISO/IEC 15444-1 / ITU-T
T.800): JP2 files and raw codestreams are decoded into `Surface` pixel buffers, or into a
`Jpeg2000Image` that also exposes the decoded color space and opacity channel.

#### Purpose

`Jpeg2000Codec` lets callers load a JPEG 2000 image into a `Surface` and lets the PDF renderer
decode `/JPXDecode` image XObjects. It is implemented entirely against the .NET base class
library, with no third-party JPEG 2000 or imaging dependency. Like `GifCodec`, it is decode-only:
it has no `Save` method.

The class is a `static` partial class. The public surface lives in
`Codecs/Jpeg2000Codec.cs`; the decoder is split by concern across the files in
`Codecs/Jpeg2000/`:

| File                          | Responsibility                                                      |
| ----------------------------- | ------------------------------------------------------------------- |
| `Jpeg2000Codec.Jp2.cs`        | JP2 box parsing (color, palette, component mapping, channel defs)   |
| `Jpeg2000Codec.Codestream.cs` | Codestream markers (SIZ, COD/COC, QCD/QCC, POC, PPM/PPT, RGN, SOT)  |
| `Jpeg2000Codec.Geometry.cs`   | Tile, component, resolution, subband, precinct and code-block grids |
| `Jpeg2000Codec.Packets.cs`    | Tier-2 packet headers, tag trees, progression orders                |
| `Jpeg2000Codec.Mq.cs`         | MQ arithmetic decoder                                               |
| `Jpeg2000Codec.Tier1.cs`      | EBCOT tier-1 decoding (all styles, ROI), dequantization on store    |
| `Jpeg2000Codec.Dwt.cs`        | Inverse 5-3 and 9-7 wavelet transforms                              |
| `Jpeg2000Codec.Decoder.cs`    | Orchestration, decode budget, inverse component transforms, scaling |
| `Jpeg2000DecoderLimits.cs`    | Public resource limits applied before any allocation                |

#### Public API

- `Load(Stream)` / `Load(string)` decode to a `Surface` (grey expanded to RGB, CMYK converted to
  RGB, alpha preserved).
- `GetInfo(Stream)` / `GetInfo(string)` return an `ImageInfo` from the container and SIZ header
  only, without decoding and without enforcing `Surface.MaxDimension`.
- `Decode(Stream)` / `Decode(byte[])` (and the overloads taking a `Jpeg2000DecoderLimits`) return a
  `Jpeg2000Image` (`Width`, `Height`, `ColorSpace`, `ColorChannelCount`, `ColorSamples`,
  `AlphaSamples`, `HasAlpha` (true when `AlphaSamples` is present), `AlphaPremultiplied`, `IccProfile`,
  `BitDepth` (source depth of the first color channel's component, before 8-bit scaling) and
  `HasPalette` (the JP2 palette was applied to `ColorSamples`)) with
  8-bit interleaved samples. `Decode` enforces the resource limits below, including
  `Surface.MaxDimension` on the image size.
- `Jpeg2000DecoderLimits` is a public record of resource limits (maximum input bytes, width, height,
  total samples, per-tile samples, tiles, precincts, code-blocks and packets, progression-order
  changes, progression steps and tier-1 work). `Jpeg2000DecoderLimits.Default` is used by every
  overload that takes no limits; its dimension limit equals `Surface.MaxDimension`. Callers such as
  the PDF layer may pass tighter limits. Invalid limits throw `ArgumentOutOfRangeException`.

#### Resource Limits

Limits are enforced during header validation, before the memory they protect is allocated:

- SIZ: image dimensions, tile count, and the total plane size (sum over components) are checked
  against the limits immediately after SIZ is parsed; the output size (width x height x channels)
  is checked before the planes are allocated.
- Geometry: per-tile sample, precinct and code-block counts are computed in 64-bit arithmetic
  and checked before arrays are created. The precinct count is additionally bounded by the amount
  of packet data: every packet needs at least one header bit, so a tile cannot have more packets
  than eight times the bytes of its tile-part data plus packed headers. A few hundred bytes of
  header can therefore no longer force a large allocation. Precinct and packet counts are charged
  by a single `GeometryBudget.ChargePrecincts` check that reports distinct causes: more precincts
  than the limit, more packets than the limit, or more packets than the data can carry.
- Progression: one decode-wide `DecodeBudget` is charged per candidate packet and per position
  step, cumulatively across tiles and POC entries. Its ceiling is the smaller of
  `MaxProgressionSteps` and a base plus a per-input-byte allowance, so work scales with input size.
  The number of POC entries in effect (main or tile header) is capped by `MaxProgressionChanges`,
  and a POC volume empty or wholly contained in an earlier one is skipped up front. After
  enumeration the number of packets actually covered must equal the tile's packet count; a
  progression (including POC volumes) that leaves packets uncovered is `InvalidDataException`,
  since the standard requires every packet to be covered.
- Tier-1: the same `DecodeBudget` charges entropy-decoding work in sample-passes (block samples
  times coding passes). Its ceiling is `min(MaxTier1Work, 2^24 + 2^14 x input bytes)`, so the work
  a stream may demand scales with its size. The per-input-byte term is what bounds hostile input:
  a hostile 76 KB stream (8192 x 8192, 64 x 64 blocks, maximum passes per block) is rejected after
  about 4 seconds (Release) or 10 seconds (Debug, including JIT) instead of minutes. The absolute ceiling
  `MaxTier1Work` defaults to 2^34, which is at least `MaxTotalSamples` (2^27) times the maximum
  of 88 passes per block, so no image the default sample limit admits can reach it; it only
  matters for callers who raise `MaxTotalSamples` or want a hard cap. An earlier default of 2^30
  rejected valid large lossless images (a 5800 x 5800 16-bit sparse image needs about 1.5 x 10^9
  sample-passes). The 2^14 figure was measured by bisecting `MaxTier1Work` on streams from the
  test encoder. The worst valid case is a flat 16-bit plane with 64 x 64 blocks, which costs
  about 4,560 sample-passes per input byte regardless of size (1024 to 4096 square, zero or one
  decomposition level: 4,527 to 4,563; for example 4096 x 4096 needs about 7.2 x 10^8
  sample-passes from a 158 KB stream), because every block runs all of its bit-plane passes over
  almost no coded data. 2^14 leaves a margin of about 3.6; an earlier 2^12 rejected such images
  (8192 x 8192 constant 16-bit, as written by OpenJPEG). Denser streams need far less (under
  100). Worst-case hostile CPU cost is therefore the allowance times the cost of a sample-pass
  (about 3.4 ns in a Release build, twice that in Debug): about 55 microseconds per input byte,
  so a stream of about 1 MB that reaches the 2^34 absolute ceiling costs on the order of a minute
  of CPU (two or more in Debug). Callers decoding untrusted input should pass tighter
  `Jpeg2000DecoderLimits` (see the user guide). A pass count is deliberately not compared with the
  segment length: an MQ-coded pass can legitimately consume far less than one byte, so such a rule
  would reject valid streams; the work budget is the mitigation.
- `MaxBitPlanes` (30) is the single bit-plane ceiling. Each band's bit-plane count
  (guard bits + exponent - 1) is validated once during geometry construction: out-of-range values
  are `InvalidDataException`; a count that only exceeds the ceiling once the ROI shift is added
  is `UnsupportedImageFeatureException`.

#### Decoding Pipeline

1. **Container**: a JP2 signature box is parsed for the color specification (enumerated sRGB,
   grayscale, sYCC, CMYK, or an ICC profile that is reported but never applied), palette and
   component-mapping boxes, and channel-definition boxes (opacity, premultiplied opacity and
   channel reordering). Anything else is treated as a raw codestream.
2. **Codestream headers**: SIZ, COD/COC, QCD/QCC, RGN, POC, PPM/PPT, TLM/PLM/PLT/CRG/COM are
   handled or skipped; tiles may have arbitrary image and tile origins and may be split into
   tile-parts; tile-part headers may override coding and quantization parameters, but only in the
   first tile-part of a tile (COD/COC/QCD/QCC/RGN in a later tile-part is `InvalidDataException`;
   POC and PPT are accepted in any tile-part).
3. **Tier-2**: packet headers are decoded (tag trees, inclusion, zero bit-planes, pass counts,
   lengths) for every progression order and quality layer, with optional SOP/EPH markers. Every
   tile must have at least one tile-part; a tile with none (for example a codestream cut at a tile
   boundary) is `InvalidDataException`. Tile-parts of a tile must appear in order (TPsot equals
   the number of tile-parts already seen for that tile); an out-of-sequence index is
   `InvalidDataException`, as in OpenJPEG. TNsot (the declared tile-part count) is advisory and
   not enforced, because real encoders emit wrong counts that OpenJPEG only warns about.
4. **Tier-1**: each code-block is decoded with the MQ decoder through the significance
   propagation, magnitude refinement and cleanup passes, honouring selective arithmetic bypass,
   reset, termination, vertically causal, predictable termination and segmentation symbols (the
   decoded symbol must be 1010), and
   the ROI max-shift.
5. **Reconstruction**: dequantization (done as each code-block is stored, in `Tier1.Store`), inverse
   DWT (reversible 5-3 or irreversible 9-7), inverse
   RCT/ICT, DC level shift, clamping and scaling to 8 bits per sample (1 to 16 bit, signed or
   unsigned, subsampled components upsampled).

#### Error Handling

- `ArgumentNullException` for null stream, data or path; `ArgumentException` for an empty path;
  `FileNotFoundException` for a missing file.
- `InvalidDataException` for data that is not JPEG 2000, dimensions above
  a `Jpeg2000DecoderLimits` limit, and truncated or corrupt data. Parse sites validate counts,
  lengths and indexes explicitly and throw `InvalidDataException`; resource limits are enforced as
  described above, so hostile files fail quickly rather than exhausting memory or time. A narrow
  internal backstop (`Guard`) converts only `IndexOutOfRangeException` and `OverflowException`
  into `InvalidDataException` (keeping the original as the inner exception); it is a last resort,
  and the robustness tests assert that no documented failure relies on it.
- `UnsupportedImageFeatureException` for valid but unsupported features: JPEG 2000 Part 2
  extensions, High Throughput (Part 15) codestreams, unknown transforms or ROI styles, five or
  more components without a channel definition, more than sixteen components, four color channels
  in a declared color space other than CMYK, and bit depths above 16.

Four color channels are treated as CMYK only when the color space is CMYK, or when no color
specification exists at all (a raw codestream), as a documented heuristic. An sRGB image with an
extra channel and no channel definition keeps the three color channels and ignores the extra one.

There is no recovery or partial decoding: a failure always propagates to the caller.

#### Deliberately Lenient Behavior

The following are tolerated rather than rejected. Each matches OpenJPEG (verified by crafting
streams and decoding them with ImageMagick) or the standard leaves the behavior to the reader:

- TNsot (declared tile-part count) is advisory; see Decoding Pipeline.
- A repeated channel association for the same channel in the cdef box: the last one wins.
- A palette index beyond the last palette entry is clamped to the last entry.
- An enumerated color space other than sRGB, grayscale, sYCC and CMYK is treated as RGB (or
  unknown) without error; ICC profiles are reported but never applied.
- COD/COC/QCD/QCC/RGN markers are rejected in any tile-part after the first of a tile (the
  tile-parts share one coding state, so a later override would apply retroactively; ISO 15444-1
  A.6.1-A.6.4); POC and PPT remain accepted in every tile-part. A mix of PPM and PPT packed
  headers is accepted.
- SOP markers are not required to be present or numbered in sequence, and truncated tile data is
  never decoded leniently (it is InvalidDataException).
- GetInfo uses the default 256 MiB input limit; it does not take a Jpeg2000DecoderLimits.

#### Dependencies

`Jpeg2000Codec` depends on `Surface` (constructing the result of `Load`), on `ImageInfo` (the
result of `GetInfo`) and on `UnsupportedImageFeatureException`. It uses only the .NET base class
library (`System.IO`). No new NuGet package is introduced.

#### Conformance Testing

The codec is verified with a test-only JPEG 2000 encoder (`Jpeg2000TestEncoder`) that produces
streams exercising each coding feature, the ITU-T T.88 MQ coder test sequence, and robustness
suites (every truncation of a small stream, random bit flips, header byte substitutions, huge counts, hostile
boxes). No third-party JPEG 2000 corpus is checked in.

#### Callers

`Jpeg2000Codec` is a public API entry point for consumers of the CanvasNet package. Within the
repository it is also called by `PdfDocument` in `DemaConsulting.CanvasNet.Pdf` to decode
`/JPXDecode` image XObjects.
