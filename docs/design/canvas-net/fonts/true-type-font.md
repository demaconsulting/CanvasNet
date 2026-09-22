### TrueTypeFont

![Fonts Structure](FontsView.svg)

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea maxp notdef -->
<!-- cspell:ignore codepoint codepoints subtable subtables -->

The `TrueTypeFont` class is the sole public software unit in the `Fonts` subsystem. It provides
hand-rolled loading and querying of glyph-based TrueType SFNT fonts, while documenting the
supporting internal `SfntContainer`, `CmapTable`, `GlyfLocaReader`, `HmtxHheaReader`, and
`KernTable` helpers inline because none has any independent public behavior beyond supporting this
unit.

#### Purpose

`TrueTypeFont` lets callers load a TrueType font from a stream or file path, inspect top-level
metrics (`UnitsPerEm`, `Ascender`, `Descender`, `LineGap`, `GlyphCount`), map Unicode codepoints
to glyph indices, extract glyph outlines as `DemaConsulting.CanvasNet.Geometry.Path`, query
horizontal advance widths, and read pairwise kerning adjustments from a classic `kern` format-0
subtable when present. The class deliberately stops at raw font-design-unit geometry and scalar
metrics: it does not perform text shaping, line layout, point-size scaling, hint execution, or
pixel rendering.

#### Data Model

All parsed SFNT structures are read directly from the in-memory font byte array using explicit
big-endian byte composition. `TrueTypeFont` stores only immutable parsed state:
`HmtxHheaReader` for horizontal metrics, `GlyfLocaReader` for eager `loca` parsing plus lazy
glyph decoding, `CmapTable` for codepoint mapping, and `KernTable` for pair lookups.

##### SFNT Offset Table (12 bytes, big-endian)

| Offset | Size | Field           | Use                                              |
| ------ | ---- | --------------- | ------------------------------------------------ |
| 0      | 4    | `sfntVersion`   | Accept `0x00010000` or `'true'`; reject `'OTTO'` |
| 4      | 2    | `numTables`     | Number of directory entries                      |
| 6      | 2    | `searchRange`   | Read but not otherwise interpreted               |
| 8      | 2    | `entrySelector` | Read but not otherwise interpreted               |
| 10     | 2    | `rangeShift`    | Read but not otherwise interpreted               |

##### SFNT Table Directory Entry (16 bytes, big-endian)

| Offset | Size | Field      | Use                                                                     |
| ------ | ---- | ---------- | ----------------------------------------------------------------------- |
| 0      | 4    | `tag`      | Names `head`, `maxp`, `hhea`, `hmtx`, `loca`, `glyf`, `cmap`, or `kern` |
| 4      | 4    | `checksum` | Recorded but never enforced                                             |
| 8      | 4    | `offset`   | Bounds-checked in `long` arithmetic                                     |
| 12     | 4    | `length`   | Bounds-checked in `long` arithmetic                                     |

##### `head` Table Fields Used (54-byte minimum)

| Offset | Size | Field              | Use                                              |
| ------ | ---- | ------------------ | ------------------------------------------------ |
| 0      | 4    | `version`          | Present in the table prefix                      |
| 18     | 2    | `unitsPerEm`       | Exposed as `UnitsPerEm`; zero is rejected        |
| 50     | 2    | `indexToLocFormat` | Selects short (`0`) or long (`1`) `loca` parsing |

##### `maxp` Table Fields Used (6-byte prefix, version 1.0 only)

| Offset | Size | Field       | Use in This Unit                                       |
| ------ | ---- | ----------- | ------------------------------------------------------ |
| 0      | 4    | `version`   | Must be `0x00010000`; version `0x00005000` is rejected |
| 4      | 2    | `numGlyphs` | Exposed as `GlyphCount` and used to size `loca`/`hmtx` |

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
2x2 matrix transforms using F2Dot14 fixed-point values.

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

Copies the source stream into memory, parses the SFNT container, validates every required table,
parses `head`, `maxp`, `hhea`, and `hmtx`, eagerly parses `loca`, and parses `cmap` / `kern`
leniently when present.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the SFNT version is unrecognized, the font declares CFF/OpenType
  (`OTTO`) outlines, a required table is missing or malformed, a table directory entry is out of
  bounds or would overflow ordinary arithmetic, or the stream is truncated

##### Load(string path)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is an empty string
- `InvalidDataException` — see `Load(Stream)`
- Underlying file-system exceptions propagate uncaught

##### GetGlyphIndex(int codepoint)

Uses the selected `cmap` subtable to map a Unicode codepoint to a glyph index.

**Throws:**

- Never throws; returns `0` (`.notdef`) for an unmapped codepoint, for a missing `cmap` table,
  or when no supported `cmap` subtable can be parsed successfully

##### GetGlyphOutline(int glyphIndex)

Validates `glyphIndex`, then asks `GlyfLocaReader` to lazily decode that glyph's outline. Simple
glyphs become `LineTo` / `QuadraticBezierTo` commands; composite glyphs recursively reuse other
glyphs' decoded outlines after applying the component transform.

**Throws:**

- `ArgumentOutOfRangeException` — `glyphIndex` is outside `[0, GlyphCount)`
- `InvalidDataException` — the glyph data is malformed or truncated, a composite glyph exceeds
  the nesting-depth or total-component bound, a point-matched component is encountered, or a
  component glyph index is out of range

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
  checks, and hosts the shared big-endian primitive readers
- `CmapTable` selects one supported format-4 or format-12 subtable and exposes a lookup delegate
- `GlyfLocaReader` eagerly parses `loca`, then lazily decodes simple and composite glyphs
- `HmtxHheaReader` parses top-level typographic metrics and advance widths
- `KernTable` tolerantly parses the first qualifying horizontal format-0 subtable, if any

#### Error Handling

##### Public Exception Contract

<!-- markdownlint-disable MD013 -->
| Member                 | Exception                     | Condition                                                             |
| ---------------------- | ----------------------------- | --------------------------------------------------------------------- |
| `Load(Stream)`         | `ArgumentNullException`       | `stream` is null                                                      |
| `Load(Stream)`         | `InvalidDataException`        | Bad SFNT version, bad required table, bad table bounds, or truncation |
| `Load(string)`         | `ArgumentNullException`       | `path` is null                                                        |
| `Load(string)`         | `ArgumentException`           | `path` is empty                                                       |
| `Load(string)`         | `InvalidDataException`        | Same font-structure failures as `Load(Stream)`                        |
| `GetGlyphIndex(int)`   | none                          | Unmapped codepoints and unusable `cmap` data return `0`               |
| `GetGlyphOutline(int)` | `ArgumentOutOfRangeException` | `glyphIndex` is outside `[0, GlyphCount)`                             |
| `GetGlyphOutline(int)` | `InvalidDataException`        | Malformed/truncated glyph data or rejected composite-glyph feature    |
| `GetAdvanceWidth(int)` | `ArgumentOutOfRangeException` | `glyphIndex` is outside `[0, GlyphCount)`                             |
| `GetKerning(int, int)` | none                          | Missing pair/table or bad kerning data returns `0`                    |
<!-- markdownlint-enable MD013 -->

##### Required vs. Optional Tables

`head`, `maxp`, `hhea`, `hmtx`, `loca`, and `glyf` are required because `TrueTypeFont` cannot
report metrics or decode outlines without them. By contrast, `cmap` and `kern` are intentionally
lenient. A missing or unusable `cmap` still leaves the font usable by glyph index directly, so
`GetGlyphIndex` degrades to returning `0`. A missing or malformed `kern` table does not prevent
metrics or outlines from being queried, so `GetKerning` degrades to returning `0`.

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

`GlyfLocaReader` enforces two deterministic limits during composite decoding: maximum nesting
depth 10 and maximum total resolved component visits 5000. The depth limit catches true cycles
and pathological nesting, but depth alone would not stop a non-cyclic composite tree from
exploding exponentially. The total-component limit is therefore the stronger bound: it caps total
work even when every reference chain is acyclic.

##### Quadratic Contour Conversion

TrueType simple glyphs are natively quadratic, so `TrueTypeFont` converts them directly to
`PathBuilder.QuadraticBezierTo` rather than flattening or converting them to cubic curves.
Consecutive off-curve points synthesize the implied on-curve midpoint required by the format, and
a contour that begins off-curve is closed through an implied on-curve start point built from the
first and last control points.

#### Dependencies

`TrueTypeFont` depends only on the `Geometry` subsystem: `Path` as the public outline return
type, `PathBuilder` as the construction mechanism for decoded contours, and `PathCommand`
semantics as the command set a caller later walks if it needs to transform the returned outline.
The unit has no dependency on `Canvas`, `Drawing`, or any runtime NuGet package.

#### Callers

`TrueTypeFont` is called directly by consumers of CanvasNet. Within this repository, its
immediate callers are the `Fonts` unit tests and the two `CanvasNetTests.cs` system-integration
tests that scale and flip the returned outline before filling it through `Drawing.PathFiller`
onto a `Canvas.Surface`.
