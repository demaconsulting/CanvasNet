<!-- cspell:ignore Spoc Epoc Zppt Zppm Nsop -->

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
| `Jpeg2000Codec.Probe.cs`      | Incremental header reader behind `GetInfo`                          |
| `Jpeg2000Codec.Tier1.cs`      | EBCOT tier-1 decoding (all styles, ROI), dequantization on store    |
| `Jpeg2000Codec.Dwt.cs`        | Inverse 5-3 and 9-7 wavelet transforms                              |
| `Jpeg2000Codec.Decoder.cs`    | Orchestration, decode budget, inverse component transforms, scaling |
| `Jpeg2000DecoderLimits.cs`    | Public resource limits applied before any allocation                |

#### Public API

- `Load(Stream)` / `Load(string)` decode to a `Surface` (gray expanded to RGB, CMYK converted to
  RGB, alpha preserved).
- `GetInfo(Stream)` / `GetInfo(string)` return an `ImageInfo` from the container and SIZ header
  only, without decoding and without enforcing `Surface.MaxDimension`. They read incrementally
  (only the header, never the whole stream; see the notes below).
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
  about 4 seconds (Release) or 10 seconds (Debug, including JIT) of CPU instead of minutes (measured manually;
  no test asserts timings). The absolute ceiling
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
   Every marker-segment and JP2 box parser checks its payload length exactly and every field is
   range-checked, so no byte is silently dropped and no invalid value is accepted. Every failure is
   `InvalidDataException`, except valid-but-unsupported features, which are
   `UnsupportedImageFeatureException`; no other exception type is thrown. The rules, with each
   deliberate leniency, are:

   <!-- markdownlint-disable MD013 -->

   | Structure | Validation rule | Deliberate leniency |
   | --- | --- | --- |
   | SOC | First marker; a second SOC in the main header is rejected | None |
   | SIZ | Directly after SOC, at most once, in the main header only; exactly 38 + 3 x Csiz bytes; Csiz 1 to 16384 (more than 16 is unsupported); Xsiz, Ysiz, XTsiz, YTsiz non-zero; XRsiz, YRsiz non-zero; XOsiz < Xsiz and YOsiz < Ysiz; XTOsiz and YTOsiz at most the image offset and tile origin + size beyond it; Ssiz depth above 16 is unsupported; Rsiz Part 2 and HTJ2K bits are unsupported | Other Rsiz profile values are not checked |
   | COD / COC | Fixed part plus one precinct byte per resolution when precincts are declared, no trailing bytes; levels at most 32; code-block exponents 2 to 10 with xcb + ycb at most 12; precinct exponent 0 only at resolution 0; progression order at most 4; layers at least 1; style bits 6-7 and transform above 1 are unsupported; MCT above 1 is unsupported; MCT needs three equal components | Scod reserved bits 3-7 are ignored; a repeated COD replaces the earlier one (as OpenJPEG) |
   | QCD / QCC | Style 0 exactly 3 x levels + 1 entries (checked once the effective levels are known); style 1 exactly one 16-bit entry; style 2 a non-zero even number of bytes; style above 2 and an empty segment are rejected; derived exponents must stay in the bit-plane range | Style 2 extra entries are ignored; guard-bit count is not range-checked beyond the bit-plane limits |
   | RGN | Exactly 3 bytes; component index below Csiz; style must be 0 (else unsupported); shift at most 37 | None |
   | POC | A non-zero multiple of 7 bytes; RSpoc < REpoc, CSpoc < CEpoc, LYEpoc at least 1, progression at most 4; the progressions must cover every packet; at most the entry cap | A later entry may name a larger layer end (already-sent packets are skipped) |
   | SOT | Tile-part header only; Lsot exactly 10; Isot below the tile count; Psot 0 (last tile-part) or at least 14 and within the data; TPsot equals the tile-parts already seen | TNsot is advisory; Psot 1 to 13 is rejected, where OpenJPEG warns on 12 |
   | SOD | Must end every tile-part header; a tile-part without SOD fails | None |
   | EOC | Ends the codestream | A missing EOC, one trailing byte, and bytes after EOC are ignored |
   | PPM / PPT | At least the one-byte index; PPM chunks must tile the payload exactly; PPM in the main header only, PPT in tile-part headers only; a repeated Zppm or Zppt index is rejected | A gap in the index sequence is tolerated, as in OpenJPEG; PPM and PPT may be mixed |
   | TLM / PLM / CRG | Main header only (rejected in a tile-part header) | Skipped by their length field, content not interpreted |
   | PLT | Tile-part header only (rejected in the main header) | Skipped by length |
   | COM, unknown markers | Skipped by their length field | Content not interpreted |
   | SOP | In packet data only; a SOP marker in either header is rejected; when present Lsop exactly 4 | Presence is not required even when the Scod SOP bit is set, and Nsop is not checked (see below) |
   | Marker order | SIZ first; COD and QCD required in the main header; SOT before SOD; COD/COC/QCD/QCC/RGN only in the first tile-part of a tile | Marker order inside a header is otherwise free |
   | JP2 signature, jP | Must be the first box with content `0D 0A 87 0A` | None |
   | JP2 ftyp | Must be the second box; content a multiple of 4 and at least 8 bytes | Brand and compatibility list are not interpreted |
   | JP2 jp2h | Must precede jp2c and contain an ihdr; its child boxes are read in order | Several jp2h boxes: the first of each child wins; boxes after jp2c are not examined |
   | JP2 ihdr | Exactly 14 bytes; height and width must equal the SIZ image height and width | NC, BPC, C, UnkC and IPR are not compared with SIZ (OpenJPEG only warns); a second ihdr is ignored; ihdr need not be the first child |
   | JP2 bpcc | Not interpreted, depths come from SIZ | Always |
   | JP2 colr | At least 3 bytes; METH 1 is exactly 7 bytes except EnumCS 14 and 19, which carry extra parameters | METH 2 takes the rest of the box as an ICC profile; other METH values are ignored; a missing colr falls back to the heuristic below |
   | JP2 pclr | Exactly the header, depth bytes and entries x columns values; signed columns are unsupported | Out-of-range palette indexes are clamped |
   | JP2 cmap | A multiple of 4 bytes | Ignored without a pclr |
   | JP2 cdef | Exactly 2 + 6 x N bytes | A repeated channel association: the last wins |
   | JP2 res and unknown boxes | Skipped by their box length | Not interpreted |
   | JP2 box length | Truncated or oversized boxes are rejected | A length of 0 (to end of data) or 1 (extended 64-bit length) is accepted |
   | jp2c | Required, with a valid codestream | A raw codestream (no signature box) is accepted without any JP2 box |

   <!-- markdownlint-enable MD013 -->

   The required JP2 boxes (signature, `ftyp`, `jp2h` with an `ihdr`, `jp2c`) are the ones OpenJPEG
   also insists on; `colr` is not required because OpenJPEG decodes a file without one. The same
   rule applies to `Decode` and `GetInfo`.
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
- A JP2 signature box whose content is not `0D 0A 87 0A` is rejected as malformed, and a palette
  column declared signed (bit 7 of its depth byte) is rejected as unsupported.
- An enumerated color space other than sRGB, grayscale, sYCC and CMYK is treated as RGB (or
  unknown) without error; ICC profiles are reported but never applied.
- COD/COC/QCD/QCC/RGN markers are rejected in any tile-part after the first of a tile (the
  tile-parts share one coding state, so a later override would apply retroactively; ISO 15444-1
  A.6.1-A.6.4); POC and PPT remain accepted in every tile-part. A mix of PPM and PPT packed
  headers is accepted.
- SOP markers are not required to be present or numbered in sequence, and truncated tile data is
  never decoded leniently (it is InvalidDataException). A stricter SOP rule was considered and declined:
  the Scod SOP bit means markers *may* be present (T.800 A.6.1), not that they must be, and OpenJPEG
  only warns. A SOP marker that is present must still have Lsop 4.
- Bytes remaining after a tile's last packet are ignored rather than rejected (matches OpenJPEG).
- POC entries from later tile-parts are appended to the tile's progression list in tile-part order,
  as the standard specifies (covered by `Jpeg2000Codec_Decode_PocInLaterTilePart_RoundTripsExactly`).
- Memory: tile-part bodies (and PPM/PPT packed headers) are gathered per tile in `MemoryStream`s
  while parsing. The decoder reads them in place through `ByteCursor.FromStream`
  (`GetBuffer` plus the stream length), so no second copy is made, and `TileData.Release` drops
  each tile's buffers as soon as its packets have been read.
- GetInfo reads incrementally and never buffers the stream: for a raw codestream it reads the SOC
  and SIZ marker segments (at most 65,539 bytes); for JP2 it walks the box headers and walks the
  children of `jp2h` one by one (the superbox itself is never buffered, so a hostile declared
  length costs nothing): only child headers and the boxes the parsers interpret (`colr`, `pclr`,
  `cmap`, `cdef`) are read, each bounded to 1 MiB (the largest valid palette is about 510 KiB, so a
  larger one is `InvalidDataException`), `colr` is read for its first 256 bytes only (an ICC
  profile needs just its 128-byte header to classify the color space; a METH 1 box longer than that
  is malformed anyway), and `ihdr` is read for the same 14-byte and SIZ-dimension checks as `Decode`,
  `ftyp` is validated and
  skipped, and every other child (`bpcc`, `res`, unknown) is skipped. It stops after the SOC and SIZ
  segments at the start of the `jp2c` box.
  The existing box and marker parsers run on the bytes read. Boxes before `jp2c` that are not
  needed are skipped by `Seek` when the stream is seekable and read and discarded in 8 KiB chunks
  otherwise (so a huge skipped box is read on a non-seekable stream, but never buffered). The
  default 256 MiB input limit still applies: a seekable stream longer than the limit is rejected up
  front (as before) and a non-seekable stream is rejected once more than that many bytes have been
  consumed. Because the rest of the stream is not read, a codestream box that is longer than the
  data (a truncated file) is only detected on seekable streams; `Load` and `Decode` still buffer
  the whole input and reject it.
- ICC profiles: the `colr` METH 2 payload is held as a `ReadOnlyMemory<byte>` slice of the
  already-buffered input while the container is parsed, so no copy is made (and none before limits
  are checked). `Jpeg2000Image.IccProfile` keeps its `byte[]?` type (the PDF project may read it) and
  the one copy is made when the image is built, after the decode has succeeded. No
  `Jpeg2000DecoderLimits` member was added: the profile lies inside the input, which
  `MaxInputBytes` already bounds, so a cap could only reject files that are valid; a profile is
  therefore never rejected or dropped for its size. `GetInfo` keeps only the leading bytes.
- A `cmap` box without a `pclr` box is ignored: component mapping only applies to a palette
  (ISO 15444-1 I.5.3.5), and OpenJPEG does likewise. The reverse, a `pclr` without `cmap`, is
  `InvalidDataException`. Pinned by `Jpeg2000Codec_Decode_CmapWithoutPclr_IsIgnored`.
- Marker precedence follows ITU-T T.800 A.6.2 and A.6.4: a tile-part COC beats a tile-part COD,
  which beats a main COC, which beats the main COD (QCC/QCD likewise). `CloneForTile` therefore
  starts each tile from the main state, and a tile COD or QCD replaces the inherited parameters of
  every component, including those the main header set through a COC or QCC; a tile COC or QCC
  then overrides that for its component. Without a tile COD, a main COC keeps applying to its
  component. The tests pin each case using the test encoder.
- A style-0 (no quantization) QCD or QCC must carry exactly 3 x levels + 1 entries (T.800 A.6.4),
  where the levels come from the effective COD or COC of the component. The marker order inside a
  header is free (QCD may precede COD and COC may follow QCD), so the count is validated while the
  sub-band geometry of each tile component is built, when the effective levels are known, and not
  while the marker is parsed; both too many and too few entries are `InvalidDataException`. A
  main-header QCD that every tile overrides is therefore never checked against the main levels.
  Style 1 and 2 segments are unchanged (style 1 has one entry; style 2 extra entries are ignored).

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
