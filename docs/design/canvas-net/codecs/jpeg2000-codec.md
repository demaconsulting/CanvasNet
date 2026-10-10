### Jpeg2000Codec

![Codecs Structure](CodecsView.svg)

The `Jpeg2000Codec` class is the seventh software unit in CanvasNet's `Codecs` subsystem. It
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
| `Jpeg2000Codec.Tier1.cs`      | EBCOT tier-1 code-block decoding (all code-block styles, ROI)       |
| `Jpeg2000Codec.Dwt.cs`        | Inverse 5-3 and 9-7 wavelet transforms, dequantization              |
| `Jpeg2000Codec.Decoder.cs`    | Orchestration, inverse component transforms, 8-bit scaling          |

#### Public API

- `Load(Stream)` / `Load(string)` decode to a `Surface` (grey expanded to RGB, CMYK converted to
  RGB, alpha preserved).
- `GetInfo(Stream)` / `GetInfo(string)` return an `ImageInfo` from the container and SIZ header
  only, without decoding and without enforcing `Surface.MaxDimension`.
- `Decode(Stream)` / `Decode(byte[])` return a `Jpeg2000Image` (`Width`, `Height`, `ColorSpace`,
  `ColorChannelCount`, `ColorSamples`, `AlphaSamples`, `AlphaPremultiplied`, `IccProfile`) with
  8-bit interleaved samples. `Decode` enforces `Surface.MaxDimension` on the image size.

#### Decoding Pipeline

1. **Container**: a JP2 signature box is parsed for the color specification (enumerated sRGB,
   grayscale, sYCC, CMYK, or an ICC profile that is reported but never applied), palette and
   component-mapping boxes, and channel-definition boxes (opacity, premultiplied opacity and
   channel reordering). Anything else is treated as a raw codestream.
2. **Codestream headers**: SIZ, COD/COC, QCD/QCC, RGN, POC, PPM/PPT, TLM/PLM/PLT/CRG/COM are
   handled or skipped; tiles may have arbitrary image and tile origins and may be split into
   tile-parts; tile-part headers may override coding and quantization parameters.
3. **Tier-2**: packet headers are decoded (tag trees, inclusion, zero bit-planes, pass counts,
   lengths) for every progression order and quality layer, with optional SOP/EPH markers.
4. **Tier-1**: each code-block is decoded with the MQ decoder through the significance
   propagation, magnitude refinement and cleanup passes, honouring selective arithmetic bypass,
   reset, termination, vertically causal, predictable termination and segmentation symbols, and
   the ROI max-shift.
5. **Reconstruction**: dequantization, inverse DWT (reversible 5-3 or irreversible 9-7), inverse
   RCT/ICT, DC level shift, clamping and scaling to 8 bits per sample (1 to 16 bit, signed or
   unsigned, subsampled components upsampled).

#### Error Handling

- `ArgumentNullException` for null stream, data or path; `ArgumentException` for an empty path;
  `FileNotFoundException` for a missing file.
- `InvalidDataException` for data that is not JPEG 2000, dimensions above
  `Surface.MaxDimension`, and truncated or corrupt data. All counts and sizes read from the data
  are bounds-checked against the available data before any allocation, so hostile files fail
  quickly rather than exhausting memory or time.
- `UnsupportedImageFeatureException` for valid but unsupported features: JPEG 2000 Part 2
  extensions, High Throughput (Part 15) codestreams, unknown transforms or ROI styles, five or
  more components without a channel definition, and bit depths above 16.

There is no recovery or partial decoding: a failure always propagates to the caller.

#### Dependencies

`Jpeg2000Codec` depends on `Surface` (constructing the result of `Load`), on `ImageInfo` (the
result of `GetInfo`) and on `UnsupportedImageFeatureException`. It uses only the .NET base class
library (`System.IO`). No new NuGet package is introduced.

#### Conformance Testing

The codec is verified with a test-only JPEG 2000 encoder (`Jpeg2000TestEncoder`) that produces
streams exercising each coding feature, the ITU-T T.88 MQ coder test sequence, and robustness
suites (every truncation, random bit flips, header byte substitutions, huge counts, hostile
boxes). No third-party JPEG 2000 corpus is checked in.

#### Callers

`Jpeg2000Codec` is a public API entry point for consumers of the CanvasNet package. Within the
repository it is also called by `PdfDocument` in `DemaConsulting.CanvasNet.Pdf` to decode
`/JPXDecode` image XObjects.
