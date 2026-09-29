### TrueTypeFont

![Fonts Structure](FontsView.svg)

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea maxp notdef -->
<!-- cspell:ignore codepoint codepoints subtable subtables -->
<!-- cspell:ignore charstring charstrings hintmask cntrmask hstemhm vstemhm callsubr callgsubr -->
<!-- cspell:ignore hhcurveto vvcurveto hvcurveto vhcurveto rlineto hlineto vlineto rmoveto -->
<!-- cspell:ignore hmoveto vmoveto rrcurveto endchar seac gsubr subrs subr ttcf numFonts faceIndex -->

The `TrueTypeFont` class is the sole public software unit in the `Fonts` subsystem. It provides
hand-rolled loading and querying of glyph-based TrueType SFNT fonts and CFF/OpenType
(`OTTO`-flavored) fonts, including selecting an individual face out of a TrueType Collection
(`ttcf`) container, while documenting the supporting internal `SfntContainer`, `CmapTable`,
`GlyfLocaReader`, `CffTable`, `CffCharstringInterpreter`, `HmtxHheaReader`, and `KernTable`
helpers inline because none has any independent public behavior beyond supporting this unit.

#### Purpose

`TrueTypeFont` lets callers load a TrueType or CFF/OpenType font from a stream or file path
(optionally selecting a specific face of a TrueType Collection), inspect top-level metrics
(`UnitsPerEm`, `Ascender`, `Descender`, `LineGap`, `GlyphCount`), map Unicode codepoints to glyph
indices, extract glyph outlines as `DemaConsulting.CanvasNet.Geometry.Path`, query horizontal
advance widths, and read pairwise kerning adjustments from a classic `kern` format-0 subtable
when present. The class deliberately stops at raw font-design-unit geometry and scalar metrics:
it does not perform text shaping, line layout, point-size scaling, hint execution, pixel
rendering, or expose font name/style metadata (bold/italic/fixed-pitch).

#### Data Model

All parsed SFNT structures are read directly from the in-memory font byte array using explicit
big-endian byte composition. `TrueTypeFont` stores only immutable parsed state:
`HmtxHheaReader` for horizontal metrics, an `IGlyphOutlineSource` (either `GlyfLocaReader` for
eager `loca` parsing plus lazy glyph decoding, or `CffTable` for CFF structural parsing plus lazy
Type 2 charstring decoding) for outline access, `CmapTable` for codepoint mapping, and
`KernTable` for pair lookups. `IGlyphOutlineSource` is a small internal dispatch abstraction: both
outline backends implement it identically (a `GlyphCount` property and a `GetGlyphOutline(int)`
method), so `TrueTypeFont.GetGlyphOutline` never needs to know or check which outline flavor the
loaded font actually uses.

##### SFNT Offset Table (12 bytes, big-endian)

| Offset | Size | Field           | Use                                                  |
| ------ | ---- | --------------- | ---------------------------------------------------- |
| 0      | 4    | `sfntVersion`   | Accept `0x00010000`, `'true'`, or `'OTTO'`           |
| 4      | 2    | `numTables`     | Number of directory entries                          |
| 6      | 2    | `searchRange`   | Read but not otherwise interpreted                   |
| 8      | 2    | `entrySelector` | Read but not otherwise interpreted                   |
| 10     | 2    | `rangeShift`    | Read but not otherwise interpreted                   |

`'OTTO'` is accepted only as a *candidate* flavor at this layer: `TrueTypeFont` still requires an
`OTTO`-tagged font to contain a `CFF` table (see Required vs. Optional Tables below), and any
`sfntVersion` that is neither a recognized TrueType version nor `'OTTO'` remains rejected exactly
as before.

##### `ttcf` TrueType Collection Header (12 + 4 × `numFonts` bytes, big-endian)

| Offset            | Size             | Field         | Use                                                           |
| ----------------- | ---------------- | ------------- | ------------------------------------------------------------- |
| 0                 | 4                | `ttcTag`      | Must be `'ttcf'` for `TryReadTtcHeader` to recognize the file |
| 4                 | 4                | `version`     | Read but not otherwise interpreted                            |
| 8                 | 4                | `numFonts`    | Number of faces; zero is rejected                             |
| 12                | `4 * numFonts`   | `offsetTable` | Each face's own SFNT offset table start, absolute from byte 0 |

`SfntContainer.TryReadTtcHeader` returns `false` (not an exception) for any file whose first 4
bytes are not `'ttcf'`, letting `TrueTypeFont` treat that case as "ordinary single-face SFNT"
without an exception-driven control flow. Once the tag *is* recognized as `'ttcf'`, a truncated
header, a truncated per-face offset table, or a declared `numFonts` of zero are all structurally
invalid and throw `InvalidDataException` rather than returning `false`.

##### SFNT Table Directory Entry (16 bytes, big-endian)

| Offset | Size | Field      | Use                                                                            |
| ------ | ---- | ---------- | ------------------------------------------------------------------------------ |
| 0      | 4    | `tag`      | Names `head`, `maxp`, `hhea`, `hmtx`, `loca`, `glyf`, `cmap`, `kern`, or `CFF` |
| 4      | 4    | `checksum` | Recorded but never enforced                                                    |
| 8      | 4    | `offset`   | Bounds-checked in `long` arithmetic                                            |
| 12     | 4    | `length`   | Bounds-checked in `long` arithmetic                                            |

##### `head` Table Fields Used (54-byte minimum)

| Offset | Size | Field              | Use                                                                         |
| ------ | ---- | ------------------ | --------------------------------------------------------------------------- |
| 0      | 4    | `version`          | Present in the table prefix                                                 |
| 18     | 2    | `unitsPerEm`       | Exposed as `UnitsPerEm`; zero is rejected                                   |
| 50     | 2    | `indexToLocFormat` | Selects short (`0`) or long (`1`) `loca` parsing (glyf-flavored fonts only) |

##### `maxp` Table Fields Used (6-byte prefix)

| Offset | Size | Field       | Use in This Unit                                                              |
| ------ | ---- | ----------- | ----------------------------------------------------------------------------- |
| 0      | 4    | `version`   | Must be `0x00010000` (glyf-flavored) or `0x00005000` (CFF-flavored)           |
| 4      | 2    | `numGlyphs` | Exposed as `GlyphCount`; for CFF fonts must equal `CffTable`'s glyph count    |

##### `hhea` Table Fields Used (36 bytes)

| Offset | Size | Field                 | Use in This Unit                               |
| ------ | ---- | --------------------- | ---------------------------------------------- |
| 4      | 2    | `ascender`            | Exposed as `Ascender`                          |
| 6      | 2    | `descender`           | Exposed as `Descender`                         |
| 8      | 2    | `lineGap`             | Exposed as `LineGap`                           |
| 34     | 2    | `numOfLongHorMetrics` | Sizes the explicit `hmtx` advance-width prefix |

##### `hmtx` Table Layout

| Region                              | Element Size | Meaning                                              |
| ----------------------------------- | ------------ | ---------------------------------------------------- |
| First `numOfLongHorMetrics` entries | 4 bytes each | `advanceWidth` + `leftSideBearing`                   |
| Remaining tail entries              | 2 bytes each | `leftSideBearing` only; reuse the last advance width |

`HmtxHheaReader` stores only the explicit advance-width array because `TrueTypeFont` exposes
advance widths, not left-side bearings.

##### `loca` Table Layout

| Format                          | Entry Size | Stored Value | Resolved Glyph Offset |
| ------------------------------- | ---------- | ------------ | --------------------- |
| Short (`indexToLocFormat == 0`) | 2 bytes    | `Offset16`   | `Offset16 * 2`        |
| Long (`indexToLocFormat == 1`)  | 4 bytes    | `Offset32`   | `Offset32`            |

`GlyfLocaReader` parses `numGlyphs + 1` entries eagerly, validates they are monotonically
non-decreasing, and rejects any entry that exceeds the `glyf` table length.

##### `glyf` Glyph Header (10 bytes)

| Offset | Size | Field              | Use                                                   |
| ------ | ---- | ------------------ | ----------------------------------------------------- |
| 0      | 2    | `numberOfContours` | `>= 0` means simple glyph; `-1` means composite glyph |
| 2      | 2    | `xMin`             | Present in the header                                 |
| 4      | 2    | `yMin`             | Present in the header                                 |
| 6      | 2    | `xMax`             | Present in the header                                 |
| 8      | 2    | `yMax`             | Present in the header                                 |

##### Simple Glyph Body Layout

| Region                             | Layout                      | Use                                           |
| ---------------------------------- | --------------------------- | --------------------------------------------- |
| `endPtsOfContours`                 | `numberOfContours` x uint16 | Inclusive final point index for each contour  |
| `instructionLength` + instructions | uint16 + byte array         | Skipped; hint execution is out of scope       |
| Flags                              | run-length encoded bytes    | On-curve bits plus short/vector encoding bits |
| X coordinates                      | delta-encoded bytes/shorts  | Reconstructed to absolute X coordinates       |
| Y coordinates                      | delta-encoded bytes/shorts  | Reconstructed to absolute Y coordinates       |

The decoded point stream is converted directly to `PathBuilder` commands. Consecutive off-curve
points synthesize the implied on-curve midpoint required by the TrueType contour convention.

##### Composite Glyph Component Layout

<!-- markdownlint-disable MD013 -->
| Region             | Layout                                   | Use in This Unit                                              |
| ------------------ | ---------------------------------------- | ------------------------------------------------------------- |
| Component header   | `flags` (uint16) + `glyphIndex` (uint16) | Identifies the referenced glyph and transform encoding        |
| Arguments          | 2 or 4 bytes                             | Must be XY offsets; point-matched components are rejected     |
| Optional transform | 2, 4, or 8 bytes                         | Supports one scale, separate X/Y scales, or a full 2x2 matrix |
<!-- markdownlint-enable MD013 -->

Supported component transforms are translation, uniform scale, independent X/Y scale, and full
2x2 matrix transforms using F2Dot14 fixed-point values. When a component's `flags` set
`SCALED_COMPONENT_OFFSET` (and not `UNSCALED_COMPONENT_OFFSET`), the component's dx/dy
translation is itself transformed through that component's scale/2x2 matrix before being applied,
rather than applied unscaled; `UNSCALED_COMPONENT_OFFSET` takes precedence if both flags are set,
and the default (neither flag set) is unscaled.

##### `CFF` Table Structure

<!-- markdownlint-disable MD013 -->
| Region                | Layout                                         | Use in This Unit                                                                              |
| --------------------- | ---------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Header                | major/minor version, `hdrSize`, `offSize`      | `hdrSize` locates the Name INDEX; unsupported major versions are rejected                     |
| Name INDEX            | CFF INDEX (see below)                          | Read past but not otherwise interpreted                                                       |
| Top DICT INDEX        | CFF INDEX of one DICT                          | Supplies `CharStrings` (op `17`), `Private` (op `18`), and `ROS` (op `12 30`) operator values |
| String INDEX          | CFF INDEX                                      | Read past but not otherwise interpreted                                                       |
| Global Subr INDEX     | CFF INDEX                                      | Global subroutines, addressed by `callgsubr` with bias `32768`/`1131`/`107` by count          |
| Private DICT          | DICT at the Top DICT's `Private` offset/size   | Optional; supplies `Subrs` (op `19`), a Private-DICT-relative Local Subr INDEX offset         |
| Local Subr INDEX      | CFF INDEX at `Private DICT start + Subrs`      | Local subroutines, addressed by `callsubr` with the same bias scheme                          |
| CharStrings INDEX     | CFF INDEX of Type 2 charstring byte arrays     | One entry per glyph; `CffTable.GlyphCount` is this INDEX's own count                          |
<!-- markdownlint-enable MD013 -->

###### CFF INDEX Structure (used for every INDEX above)

| Region       | Layout                                     | Use                                                  |
| ------------ | ------------------------------------------ | ---------------------------------------------------- |
| `count`      | uint16                                     | Zero means an empty INDEX (no offset array)          |
| `offSize`    | uint8 (present only if `count > 0`)        | Byte width (1-4) of each offset                      |
| `offset[]`   | `count + 1` entries of `offSize` bytes     | 1-based, relative to the byte after the offset array |
| `data`       | `offset[count] - 1` bytes                  | Concatenated variable-length entries                 |

###### DICT Encoding (Top DICT and Private DICT)

Every DICT operand this unit reads is an integer, encoded as a fixed 5-byte sequence (`b0 = 29`
followed by a 4-byte big-endian signed 32-bit value) or one of the shorter single/two/three-byte
integer encodings defined by the CFF specification; DICT real-number operands are not used by any
operator this unit reads. A one- or two-byte operator code follows an operator's operands;
operator `12` is always a two-byte escape (for example `12 30` for `ROS`).

`CffTable` detects CID-keyed CFF data by the presence of the `ROS` operator (escape operator
`12 30`) in the Top DICT and fails closed with `InvalidDataException`: CID-keyed fonts use an
`FDArray`/`FDSelect`-based per-glyph Private DICT selection this unit does not implement, and
silently applying the single (non-CID) Private DICT lookup this unit does support would
misinterpret glyph data under the wrong scheme.

###### Type 2 Charstring Structure (`CffCharstringInterpreter`)

Each CharStrings INDEX entry is itself a sequence of operands (integers or 16.16 fixed-point
numbers, both stack-based) and single- or two-byte operator codes, executed against the same
running-point/current-subpath state a `glyf` glyph decoder would maintain, but limited to exactly
the operator subset in the table below:

<!-- markdownlint-disable MD013 -->
| Operator(s)                                    | Code(s)      | Behavior                                                                   |
| ---------------------------------------------- | ------------ | -------------------------------------------------------------------------- |
| `hstem`, `vstem`, `hstemhm`, `vstemhm`         | 1, 3, 18, 23 | Accumulate stem-hint count (operand pairs, plus any already on the stack)  |
| `vmoveto`, `rlineto`, `hlineto`                | 4, 5, 6      | (`vmoveto` also below) `rlineto`/`hlineto` append line segments            |
| `vlineto`                                      | 7            | Appends line segments, alternating axis with `hlineto`                     |
| `rrcurveto`, `hhcurveto`, `vvcurveto`          | 8, 27, 26    | Append cubic Bezier segments per operator-specific operand packing         |
| `callsubr`, `return`                           | 10, 11       | Invoke/resume a local subroutine, biased and depth/step bounded            |
| `endchar`                                      | 14           | Finishes the outline; legacy 4-operand seac-style form is rejected         |
| `hmoveto`                                      | 22           | Starts a new subpath, horizontal-only offset                               |
| `vmoveto`                                      | 4            | Starts a new subpath, vertical-only offset                                 |
| `rmoveto`                                      | 21           | Starts a new subpath, general XY offset                                    |
| `hintmask`, `cntrmask`                         | 19, 20       | Skip `ceil(stemCount / 8)` mask bytes                                      |
| `hvcurveto`, `vhcurveto`                       | 31, 30       | Append cubic Bezier segments, alternating start/end tangent axis           |
| `callgsubr`                                    | 29           | Invoke a global subroutine, biased and depth/step bounded                  |
<!-- markdownlint-enable MD013 -->

Any operator outside this set - including the two-byte flex escape operators (`12 34`/`12 35`/
`12 36`/`12 37`) and `rcurveline`/`rlinecurve` (24/25) - is rejected with `InvalidDataException`,
as is a charstring that ends before its declared operand/operator data has been fully read.
`callsubr`/`callgsubr` apply the CFF-standard bias (`107` for a subroutine count under 1240,
`1131` for a count under 33900, otherwise `32768`, computed independently per local/global
subroutine INDEX from that INDEX's own entry count) and are bounded by both a maximum call depth
and a maximum total executed step count, mirroring `GlyfLocaReader`'s composite-glyph bounding
posture. The first stack-clearing operator in a charstring may carry one extra leading "width"
operand (the glyph's CFF-encoded advance width, superseded by this unit's own `hmtx`-based
`GetAdvanceWidth`); it is recognized by its odd-numbered-out operand count and discarded rather
than misread as a coordinate.

##### `cmap` Subtables Used

###### Format 4 (segment mapping to delta values)

| Offset | Size | Field             | Use in This Unit                              |
| ------ | ---- | ----------------- | --------------------------------------------- |
| 0      | 2    | `format`          | Must be `4`                                   |
| 2      | 2    | `length`          | Used only for bounds-truncated parsing        |
| 6      | 2    | `segCountX2`      | Sizes the segment arrays                      |
| 14     | var  | `endCode[]`       | Segment upper bounds                          |
| ...    | 2    | `reservedPad`     | Skipped                                       |
| ...    | var  | `startCode[]`     | Segment lower bounds                          |
| ...    | var  | `idDelta[]`       | Applied directly when `idRangeOffset` is zero |
| ...    | var  | `idRangeOffset[]` | Indirect lookup into `glyphIdArray`           |
| ...    | var  | `glyphIdArray[]`  | Optional per-codepoint glyph IDs              |

###### Format 12 (segmented coverage)

| Offset | Size             | Field       | Use in This Unit                                          |
| ------ | ---------------- | ----------- | --------------------------------------------------------- |
| 0      | 2                | `format`    | Must be `12`                                              |
| 4      | 4                | `length`    | Used only for bounds-truncated parsing                    |
| 12     | 4                | `numGroups` | Sizes the group array                                     |
| 16     | `12 * numGroups` | `groups`    | Stores `startCharCode`, `endCharCode`, and `startGlyphId` |

`CmapTable` chooses exactly one supported subtable using this priority order: `(3,10)`
format 12, `(0,4)`/`(0,6)` format 12, `(3,1)` format 4, `(0,3)` format 4, then any remaining
`(0,x)` format 4.

##### `kern` Table Layout Used

###### Version-0 `kern` Table Header

| Offset | Size | Field     | Use in This Unit                                |
| ------ | ---- | --------- | ----------------------------------------------- |
| 0      | 2    | `version` | Must be `0`; any other header layout is skipped |
| 2      | 2    | `nTables` | Number of subtables to inspect                  |

###### Format-0 Subtable Header and Body

| Offset | Size         | Field           | Use in This Unit                                     |
| ------ | ------------ | --------------- | ---------------------------------------------------- |
| 0      | 2            | `version`       | Present; not otherwise interpreted                   |
| 2      | 2            | `length`        | Bounds-checks the subtable                           |
| 4      | 2            | `coverage`      | Must indicate format 0, horizontal, non-cross-stream |
| 6      | 2            | `nPairs`        | Sizes the pair array                                 |
| 8      | 2            | `searchRange`   | Present; not otherwise interpreted                   |
| 10     | 2            | `entrySelector` | Present; not otherwise interpreted                   |
| 12     | 2            | `rangeShift`    | Present; not otherwise interpreted                   |
| 14     | `6 * nPairs` | `pairs`         | Sorted `(left, right, value)` entries for search     |

#### Key Methods

##### Load(Stream stream)

Copies the source stream into memory, parses the SFNT container (transparently selecting face 0
if the stream begins with a `ttcf` header), validates every required table for the font's
outline flavor, parses `head`, `maxp`, `hhea`, and `hmtx`, eagerly parses `loca` (glyf-flavored)
or the `CFF` table (CFF-flavored), and parses `cmap` / `kern` leniently when present.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the SFNT version is unrecognized, an `OTTO`-flavored font is missing
  its `CFF` table, a required table is missing or malformed, a table directory entry is out of
  bounds or would overflow ordinary arithmetic, the stream is truncated, or (CFF-flavored only) the
  CFF data is CID-keyed, structurally malformed, or its CharStrings count disagrees with `maxp`

##### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions propagate uncaught

##### Load(Stream stream, int faceIndex)

As `Load(Stream)`, but selects an explicit `faceIndex` rather than defaulting to face 0. If the
stream begins with a `ttcf` header, `faceIndex` is validated against that header's own declared
face count and used to look up that face's own SFNT offset table start; otherwise the file is
treated as a single-face SFNT and only `faceIndex == 0` is accepted.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `ArgumentOutOfRangeException` — `faceIndex` is negative, or is not less than the file's own
  face count
- `InvalidDataException` — see `Load(Stream)`, plus a truncated or otherwise malformed `ttcf`
  header

##### Load(string path, int faceIndex)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream, int)`.

**Throws:** as `Load(Stream, int)`, plus `ArgumentNullException`/`ArgumentException` for a null
or empty `path` exactly as `Load(string)`.

##### GetFaceCount(Stream stream) / GetFaceCount(string path)

Reads only enough of the stream to distinguish a `ttcf` container from an ordinary SFNT font: the
first 4 bytes (the tag), and - only if that tag is `ttcf` - the header's `numFonts` field. Returns
`1` for an ordinary SFNT font, or the container's own declared face count for a `ttcf` file.
Neither overload parses any face's own table directory.

**Throws:**

- `ArgumentNullException` — `stream`/`path` is null (`GetFaceCount(string)` also rejects an empty
  `path` with `ArgumentException`)
- `InvalidDataException` — the stream is too short to contain a tag, or (for a `ttcf`-tagged
  file) the `ttcf` header itself is truncated or malformed

##### GetGlyphIndex(int codepoint)

Uses the selected `cmap` subtable to map a Unicode codepoint to a glyph index.

**Throws:**

- Never throws; returns `0` (`.notdef`) for an unmapped codepoint, for a missing `cmap` table,
  or when no supported `cmap` subtable can be parsed successfully

##### GetGlyphOutline(int glyphIndex)

Validates `glyphIndex`, then asks the loaded font's `IGlyphOutlineSource` (`GlyfLocaReader` or
`CffTable`) to lazily decode that glyph's outline. Glyf-flavored simple glyphs become `LineTo` /
`QuadraticBezierTo` commands and composite glyphs recursively reuse other glyphs' decoded
outlines after applying the component transform; CFF-flavored glyphs become `LineTo` /
`CubicBezierTo` commands decoded by `CffCharstringInterpreter` from that glyph's own Type 2
charstring.

**Throws:**

- `ArgumentOutOfRangeException` — `glyphIndex` is outside `[0, GlyphCount)`
- `InvalidDataException` — the glyph data is malformed or truncated, a composite glyph exceeds
  the nesting-depth or total-component bound, a point-matched component is encountered, a
  component glyph index is out of range, or (CFF-flavored) the glyph's charstring uses an operator
  outside the supported set, is truncated, exceeds the subroutine call depth/step bound, or
  references an out-of-range subroutine index

##### GetAdvanceWidth(int glyphIndex)

Validates `glyphIndex`, then returns the glyph's horizontal advance width from `hmtx`, reusing
the last explicit advance-width entry for any tail glyph beyond `numOfLongHorMetrics`.

**Throws:**

- `ArgumentOutOfRangeException` — `glyphIndex` is outside `[0, GlyphCount)`

##### GetKerning(int leftGlyphIndex, int rightGlyphIndex)

Binary-searches the selected `kern` format-0 pair array.

**Throws:**

- Never throws; returns `0` for a missing pair, for a missing or unusable `kern` table, and even
  for an out-of-range glyph index supplied only for kerning lookup

##### Internal Helper Roles

- `SfntContainer` parses the offset table and table directory, enforces overflow-safe bounds
  checks, hosts the shared big-endian primitive readers, and recognizes/parses `ttcf` collection
  headers via `TryReadTtcHeader`
- `CmapTable` selects one supported format-4 or format-12 subtable and exposes a lookup delegate
- `GlyfLocaReader` (an `IGlyphOutlineSource`) eagerly parses `loca`, then lazily decodes simple
  and composite `glyf` glyphs
- `CffTable` (an `IGlyphOutlineSource`) parses the CFF Header/INDEXes/Private DICT, then lazily
  decodes each glyph's Type 2 charstring via `CffCharstringInterpreter`
- `CffCharstringInterpreter` executes a single glyph's Type 2 charstring bytecode against the
  supported operator subset
- `HmtxHheaReader` parses top-level typographic metrics and advance widths
- `KernTable` tolerantly parses the first qualifying horizontal format-0 subtable, if any

#### Error Handling

##### Public Exception Contract

<!-- markdownlint-disable MD013 -->
| Member                  | Exception                                     | Condition                                                                                                        |
| ----------------------- | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `Load(Stream)`          | `ArgumentNullException`                       | `stream` is null                                                                                                 |
| `Load(Stream)`          | `InvalidDataException`                        | Bad SFNT version, missing required table (per outline flavor), bad table bounds, truncation, or invalid CFF data |
| `Load(string)`          | `ArgumentNullException`                       | `path` is null                                                                                                   |
| `Load(string)`          | `ArgumentException`                           | `path` is empty                                                                                                  |
| `Load(string)`          | `InvalidDataException`                        | Same font-structure failures as `Load(Stream)`                                                                   |
| `Load(Stream, int)`     | `ArgumentNullException`                       | `stream` is null                                                                                                 |
| `Load(Stream, int)`     | `ArgumentOutOfRangeException`                 | `faceIndex` is negative or not less than the file's own face count                                               |
| `Load(Stream, int)`     | `InvalidDataException`                        | As `Load(Stream)`, plus a malformed `ttcf` header                                                                |
| `Load(string, int)`     | `ArgumentNullException`/`ArgumentException`   | Null/empty `path`, as `Load(string)`                                                                             |
| `Load(string, int)`     | `ArgumentOutOfRangeException`                 | As `Load(Stream, int)`                                                                                           |
| `Load(string, int)`     | `InvalidDataException`                        | As `Load(Stream, int)`                                                                                           |
| `GetFaceCount(Stream)`  | `ArgumentNullException`                       | `stream` is null                                                                                                 |
| `GetFaceCount(Stream)`  | `InvalidDataException`                        | Stream too short for a tag, or a malformed `ttcf` header                                                         |
| `GetFaceCount(string)`  | `ArgumentNullException`/`ArgumentException`   | Null/empty `path`                                                                                                |
| `GetFaceCount(string)`  | `InvalidDataException`                        | As `GetFaceCount(Stream)`                                                                                        |
| `GetGlyphIndex(int)`    | none                                          | Unmapped codepoints and unusable `cmap` data return `0`                                                          |
| `GetGlyphOutline(int)`  | `ArgumentOutOfRangeException`                 | `glyphIndex` is outside `[0, GlyphCount)`                                                                        |
| `GetGlyphOutline(int)`  | `InvalidDataException`                        | Malformed/truncated glyph or charstring data, or a rejected composite-glyph/CFF-operator feature                 |
| `GetAdvanceWidth(int)`  | `ArgumentOutOfRangeException`                 | `glyphIndex` is outside `[0, GlyphCount)`                                                                        |
| `GetKerning(int, int)`  | none                                          | Missing pair/table or bad kerning data returns `0`                                                               |
<!-- markdownlint-enable MD013 -->

##### Required vs. Optional Tables

`head`, `maxp`, and `hhea`/`hmtx` are required for every font regardless of outline flavor,
because `TrueTypeFont` cannot report top-level metrics without them. A glyf-flavored font (`maxp`
version `0x00010000`) additionally requires `loca` and `glyf`; a CFF-flavored (`OTTO`-tagged)
font (`maxp` version `0x00005000`) instead requires `CFF` and does **not** require `loca`/`glyf`
at all. By contrast, `cmap` and `kern` are intentionally lenient for either flavor. A missing or
unusable `cmap` still leaves the font usable by glyph index directly, so `GetGlyphIndex` degrades
to returning `0`. A missing or malformed `kern` table does not prevent metrics or outlines from
being queried, so `GetKerning` degrades to returning `0`.

##### Overflow-Safe Structural Validation

Every table-directory bound is computed in `long` arithmetic with `checked` addition.
`SfntContainer` never performs a raw `uint + uint` end calculation, so malicious offsets and
lengths cannot overflow the validation logic itself. `GlyfLocaReader` applies the same posture to
`loca` sizing and per-glyph `glyf` bounds.

##### Big-Endian Primitive Reading

SFNT multi-byte fields are always big-endian regardless of host CPU endianness. `SfntContainer`
therefore composes every ushort, short, uint, int, and F2Dot14 value from individual bytes.
`BitConverter` and `BinaryPrimitives` are deliberately avoided so the parsing logic does not rely
on machine endianness or hidden span helpers.

##### Composite Glyph Bounds

`GlyfLocaReader` enforces three deterministic limits during composite decoding: maximum nesting
depth 10, maximum total resolved component visits 5000, and a total resolved point/command budget
of 200,000 charged across a single `GetGlyphOutline` call. The depth limit catches true cycles and
pathological nesting, but depth alone would not stop a non-cyclic composite tree from exploding
exponentially. The total-component limit bounds that exponential blow-up, but it counts only
component *visits*, not the amount of geometry each visit produces - a single large simple glyph
can cheaply encode tens of thousands of points, and a composite glyph referencing it many times
(well under the component-visit cap) could still copy hundreds of millions of path commands. The
total-point/command budget is the bound that actually caps this amplification: it is charged as
soon as the point/command count for a step is known, before the corresponding points/commands are
allocated or copied, so it remains effective even when the component-count cap alone is not.

##### Quadratic Contour Conversion

TrueType simple glyphs are natively quadratic, so `TrueTypeFont` converts them directly to
`PathBuilder.QuadraticBezierTo` rather than flattening or converting them to cubic curves.
Consecutive off-curve points synthesize the implied on-curve midpoint required by the format, and
a contour that begins off-curve is closed through an implied on-curve start point built from the
first and last control points.

##### CFF Subroutine Call Bounds

`CffCharstringInterpreter` enforces the same deterministic-limit posture `GlyfLocaReader` applies
to composite `glyf` glyphs, adapted to Type 2 charstring subroutine calls: a maximum `callsubr`/
`callgsubr` nesting depth, and a maximum total executed charstring-operator step count charged
across a single glyph's decode. The depth limit catches true self-recursive subroutine cycles; the
total-step budget catches non-cyclic but excessively long or repetitive subroutine call chains
that depth bounding alone would not stop. An out-of-range subroutine index (after bias
adjustment) is rejected immediately with `InvalidDataException` rather than silently clamped, so a
malformed or malicious CFF font cannot redirect execution into unrelated table bytes.

##### Cubic Contour Conversion

CFF/Type 2 charstrings are natively cubic, so `TrueTypeFont` (via `CffCharstringInterpreter`)
converts them directly to `PathBuilder.CubicBezierTo` rather than approximating with quadratic
segments - the reverse of the glyf-flavored decoder's natively-quadratic posture. Every subpath
opened by a moveto operator is closed either by an explicit path close or by `endchar`, matching
the fill-rule expectations `Drawing.PathFiller` already applies to `glyf`-flavored outlines.

#### Dependencies

`TrueTypeFont` depends only on the `Geometry` subsystem: `Path` as the public outline return
type, `PathBuilder` as the construction mechanism for decoded contours, and `PathCommand`
semantics as the command set a caller later walks if it needs to transform the returned outline.
The unit has no dependency on `Canvas`, `Drawing`, or any runtime NuGet package.

#### Callers

`TrueTypeFont` is called directly by consumers of CanvasNet. Within this repository, its
immediate callers are the `Fonts` unit tests, the real-font integration tests in
`TrueTypeFontRealFontIntegrationTests.cs` (real `.ttf`, real CFF/OpenType `.otf`, and a
locally-assembled multi-face `.ttc`), and the two `CanvasNetTests.cs` system-integration tests
that scale and flip the returned outline before filling it through `Drawing.PathFiller` onto a
`Canvas.Surface`.
