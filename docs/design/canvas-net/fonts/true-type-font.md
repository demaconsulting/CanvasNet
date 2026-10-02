### TrueTypeFont

![Fonts Structure](FontsView.svg)

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea maxp notdef -->
<!-- cspell:ignore codepoint codepoints subtable subtables -->
<!-- cspell:ignore charstring charstrings hintmask cntrmask hstemhm vstemhm callsubr callgsubr -->
<!-- cspell:ignore hhcurveto vvcurveto hvcurveto vhcurveto rlineto hlineto vlineto rmoveto -->
<!-- cspell:ignore hmoveto vmoveto rrcurveto endchar seac gsubr subrs subr ttcf numFonts faceIndex -->
<!-- cspell:ignore eexec lenIV hsbw dotsection hstem vstem callothersubr othersubr -->
<!-- cspell:ignore setcurrentpoint closepath pfb pfa cleartomark -->
<!-- cspell:ignore charsets ISOAdobe isoadobe -->
<!-- cspell:ignore bchar achar adx ady Agrave -->
<!-- cspell:ignore hflex flex1 hflex1 -->
<!-- cspell:ignore noaccess definefont currentfile closefile -->

The `TrueTypeFont` class is the first of two public software units in the `Fonts` subsystem
(alongside `SystemFontCatalog`). It provides
hand-rolled loading and querying of glyph-based TrueType SFNT fonts, CFF/OpenType
(`OTTO`-flavored) fonts, and classic PostScript Type 1 font programs (both as caller-supplied
cleartext/encrypted byte segments and as standalone auto-detected `.pfb`/`.pfa` files), including
selecting an individual face out of a TrueType Collection (`ttcf`) container, while documenting
the supporting internal `SfntContainer`, `CmapTable`, `GlyfLocaReader`, `CffTable`,
`CffCharstringInterpreter`, `Type1Table`, `Type1CharstringInterpreter`,
`Type1CharstringDecryption`, `Type1PfbReader`, `Type1PfaReader`, `Type1StandardGlyphNames`,
`HmtxHheaReader`, `KernTable`, `NameTable`, and `StyleTable` helpers inline because none has any
independent public behavior beyond supporting this unit.

#### Purpose

`TrueTypeFont` lets callers load a TrueType, CFF/OpenType, or classic PostScript Type 1 font from
a stream or file path (optionally selecting a specific face of a TrueType Collection), inspect
top-level metrics (`UnitsPerEm`, `Ascender`, `Descender`, `LineGap`, `GlyphCount`), map Unicode
codepoints to glyph indices, extract glyph outlines as `DemaConsulting.CanvasNet.Geometry.Path`,
query horizontal advance widths, read pairwise kerning adjustments from a classic `kern` format-0
subtable when present, and resolve the font's own name (`GetNameInfo()`) and bold/italic/fixed-pitch
style classification (`IsBold`/`IsItalic`/`IsFixedPitch`) from its `name`, `OS/2`, `head`, and
`post` tables. The class deliberately stops at raw font-design-unit geometry and scalar metrics:
it does not perform text shaping, line layout, point-size scaling, hint execution, or pixel
rendering. For Type 1 font programs specifically, the class does not support `seac`-composed
accented glyphs (composite glyphs built from two other glyphs plus fixed accent-placement
metrics) - a charstring using `seac` is rejected with `InvalidDataException` rather than being
resolved - and, for standalone `.pfb`/`.pfa` files (which carry no caller-suppliable text
encoding of their own), only a small curated built-in codepoint-to-glyph-name vocabulary covering
common Latin/ASCII characters is available as the default encoding.

#### Data Model

All parsed SFNT/Type 1 structures are read directly from the in-memory font byte array using
explicit big-endian byte composition. `TrueTypeFont` stores only immutable parsed state:
`HmtxHheaReader` for horizontal metrics, an `IGlyphOutlineSource` (`GlyfLocaReader` for eager
`loca` parsing plus lazy glyph decoding, `CffTable` for CFF structural parsing plus lazy Type 2
charstring decoding, or `Type1Table` for Type 1 font-program structural parsing plus lazy Type 1
charstring decoding) for outline access, `CmapTable` for codepoint mapping, `KernTable` for pair
lookups, `NameTable` for name-string resolution, and the `IsBold`/`IsItalic`/`IsFixedPitch`
booleans derived once at load time by `StyleTable`. `IGlyphOutlineSource` is a small internal
dispatch abstraction: all three outline backends implement it identically (a `GlyphCount`
property and a `GetGlyphOutline(int)` method), so `TrueTypeFont.GetGlyphOutline` never needs to
know or check which outline flavor the loaded font actually uses. A font loaded via `LoadType1`
or via standalone `.pfb`/`.pfa` auto-detection synthesizes its own `CmapTable` (via
`CmapTable.FromMap`) and `HmtxHheaReader` (via `HmtxHheaReader.FromAdvanceWidths`) directly from
the parsed `Type1Table`'s glyph names/advance widths and the caller's (or the built-in default's)
codepoint-to-glyph-name encoding, rather than reading those tables from any SFNT byte layout,
since a Type 1 font program has no `cmap`/`hmtx` tables of its own; its `NameTable` is
`NameTable.Empty`, its `KernTable` is `KernTable.Empty`, `UnitsPerEm` is fixed at `1000` (the
classic Type 1 convention), and `IsBold`/`IsItalic`/`IsFixedPitch` are all `false`.

##### SFNT Offset Table (12 bytes, big-endian)

| Offset | Size | Field           | Use                                        |
| ------ | ---- | --------------- | ------------------------------------------ |
| 0      | 4    | `sfntVersion`   | Accept `0x00010000`, `'true'`, or `'OTTO'` |
| 4      | 2    | `numTables`     | Number of directory entries                |
| 6      | 2    | `searchRange`   | Read but not otherwise interpreted         |
| 8      | 2    | `entrySelector` | Read but not otherwise interpreted         |
| 10     | 2    | `rangeShift`    | Read but not otherwise interpreted         |

`'OTTO'` is accepted only as a *candidate* flavor at this layer: `TrueTypeFont` still requires an
`OTTO`-tagged font to contain a `CFF` table (see Required vs. Optional Tables below), and any
`sfntVersion` that is neither a recognized TrueType version nor `'OTTO'` remains rejected exactly
as before.

##### `ttcf` TrueType Collection Header (12 + 4 × `numFonts` bytes, big-endian)

| Offset | Size           | Field         | Use                                                           |
| ------ | -------------- | ------------- | ------------------------------------------------------------- |
| 0      | 4              | `ttcTag`      | Must be `'ttcf'` for `TryReadTtcHeader` to recognize the file |
| 4      | 4              | `version`     | Read but not otherwise interpreted                            |
| 8      | 4              | `numFonts`    | Number of faces; zero is rejected                             |
| 12     | `4 * numFonts` | `offsetTable` | Each face's own SFNT offset table start, absolute from byte 0 |

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
| 44     | 2    | `macStyle`         | Bits 0/1 (bold/italic) always OR'd into `IsBold`/`IsItalic` by `StyleTable` |
| 50     | 2    | `indexToLocFormat` | Selects short (`0`) or long (`1`) `loca` parsing (glyf-flavored fonts only) |

##### `maxp` Table Fields Used (6-byte prefix)

| Offset | Size | Field       | Use in This Unit                                                           |
| ------ | ---- | ----------- | -------------------------------------------------------------------------- |
| 0      | 4    | `version`   | Must be `0x00010000` (glyf-flavored) or `0x00005000` (CFF-flavored)        |
| 4      | 2    | `numGlyphs` | Exposed as `GlyphCount`; for CFF fonts must equal `CffTable`'s glyph count |

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
advance widths, not left-side bearings. The trailing tail region is therefore optional: because
the reader never reads any of its bytes, a real-world font whose `hmtx` table omits that tail
entirely (rather than padding it out to the full glyph count) still parses successfully - only
the explicit `numOfLongHorMetrics` entries are required to be present.

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
| Region            | Layout                                                                                                                     | Use in This Unit                                                                                                                            |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Header            | major/minor version, `hdrSize`, `offSize`                                                                                  | `hdrSize` locates the Name INDEX; unsupported major versions are rejected                                                                   |
| Name INDEX        | CFF INDEX (see below)                                                                                                      | Read past but not otherwise interpreted                                                                                                     |
| Top DICT INDEX    | CFF INDEX of one DICT                                                                                                      | Supplies `CharStrings` (op `17`), `Private` (op `18`), `ROS` (op `12 30`), and `charset` (op `15`) operator values                          |
| String INDEX      | CFF INDEX                                                                                                                  | Captured for glyph-name resolution of SIDs `391` and above (see Charset Resolution below)                                                   |
| Global Subr INDEX | CFF INDEX                                                                                                                  | Global subroutines, addressed by `callgsubr` with bias `32768`/`1131`/`107` by count                                                        |
| Private DICT      | DICT at the Top DICT's `Private` offset/size                                                                               | Optional; supplies `Subrs` (op `19`), `defaultWidthX` (op `20`), `nominalWidthX` (op `21`), a Private-DICT-relative Local Subr INDEX offset |
| Local Subr INDEX  | CFF INDEX at `Private DICT start + Subrs`                                                                                  | Local subroutines, addressed by `callsubr` with the same bias scheme                                                                        |
| CharStrings INDEX | CFF INDEX of Type 2 charstring byte arrays                                                                                 | One entry per glyph; `CffTable.GlyphCount` is this INDEX's own count                                                                        |
| Charset table     | Format byte plus per-glyph/per-range SID data, at the Top DICT's `charset` byte offset (when present and greater than `2`) | Builds the glyph-name-to-glyph-index map (see Charset Resolution below)                                                                     |
<!-- markdownlint-enable MD013 -->

###### CFF INDEX Structure (used for every INDEX above)

| Region     | Layout                                 | Use                                                  |
| ---------- | -------------------------------------- | ---------------------------------------------------- |
| `count`    | uint16                                 | Zero means an empty INDEX (no offset array)          |
| `offSize`  | uint8 (present only if `count > 0`)    | Byte width (1-4) of each offset                      |
| `offset[]` | `count + 1` entries of `offSize` bytes | 1-based, relative to the byte after the offset array |
| `data`     | `offset[count] - 1` bytes              | Concatenated variable-length entries                 |

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

###### Charset Resolution (glyph name to glyph index)

A PDF simple font resolves a character code to a *glyph name* (for example `"A"`), not a glyph
index directly; `CffTable.TryGetGlyphIndex(string, out int)` resolves that name using the Top
DICT's `charset` operator (`15`):

<!-- markdownlint-disable MD013 -->
| `charset` operator value           | Meaning                                                                                                                                                                                                                                                             |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Absent, or explicit `0` (ISOAdobe) | Glyph `i`'s SID equals `i` itself, for every glyph `i` from `1` to `GlyphCount - 1`                                                                                                                                                                                 |
| `1` (Expert), `2` (ExpertSubset)   | Recognized but not resolved to glyph names: every non-`.notdef` glyph is assigned sentinel SID `-1`, so `TryGetGlyphIndex` returns `false` for every name (never throws) - a deliberate scope boundary, since no caller currently needs Expert-encoding glyph names |
| Greater than `2`                   | A byte offset (within the CFF table) to a custom charset table, in format `0`, `1`, or `2` below; any other format byte throws `InvalidDataException`                                                                                                               |
<!-- markdownlint-enable MD013 -->

<!-- markdownlint-disable MD013 -->
| Custom charset format | Layout | Decoding |
| ---------------------- | ------------------------------------------------------------------ | -------------------------------------------------------------------------------- |
| `0` | Format byte, then `GlyphCount - 1` 2-byte SIDs | Glyph `i`'s SID is the `(i - 1)`th entry |
| `1` | Format byte, then ranges of (2-byte first SID, 1-byte `nLeft`) | Each range covers `nLeft + 1` consecutive glyphs/SIDs starting at `first SID` |
| `2` | Format byte, then ranges of (2-byte first SID, 2-byte `nLeft`) | Same as format `1`, with a wider `nLeft` for large charsets |
<!-- markdownlint-enable MD013 -->

Once every glyph's SID is known, its name is resolved via the 391-entry CFF Standard Strings table
(Adobe Technical Note #5176 Appendix A, transcribed verbatim as `CffTable.StandardStrings`) for SID
`0`-`390`, or the font's own String INDEX (`stringIndex[SID - 391]`) for SID `391` and above,
building the combined name-to-glyph-index map `TryGetGlyphIndex` queries.

###### Type 2 Charstring Structure (`CffCharstringInterpreter`)

Each CharStrings INDEX entry is itself a sequence of operands (integers or 16.16 fixed-point
numbers, both stack-based) and single- or two-byte operator codes, executed against the same
running-point/current-subpath state a `glyf` glyph decoder would maintain, but limited to exactly
the operator subset in the table below:

<!-- markdownlint-disable MD013 -->
| Operator(s)                            | Code(s)      | Behavior                                                                  |
| -------------------------------------- | ------------ | ------------------------------------------------------------------------- |
| `hstem`, `vstem`, `hstemhm`, `vstemhm` | 1, 3, 18, 23 | Accumulate stem-hint count (operand pairs, plus any already on the stack) |
| `vmoveto`, `rlineto`, `hlineto`        | 4, 5, 6      | (`vmoveto` also below) `rlineto`/`hlineto` append line segments           |
| `vlineto`                              | 7            | Appends line segments, alternating axis with `hlineto`                    |
| `rrcurveto`, `hhcurveto`, `vvcurveto`  | 8, 27, 26    | Append cubic Bezier segments per operator-specific operand packing        |
| `callsubr`, `return`                   | 10, 11       | Invoke/resume a local subroutine, biased and depth/step bounded           |
| `endchar`                              | 14           | Finishes the outline; 4-operand seac-style composes a base/accent pair    |
| `hmoveto`                              | 22           | Starts a new subpath, horizontal-only offset                              |
| `vmoveto`                              | 4            | Starts a new subpath, vertical-only offset                                |
| `rmoveto`                              | 21           | Starts a new subpath, general XY offset                                   |
| `hintmask`, `cntrmask`                 | 19, 20       | Skip `ceil(stemCount / 8)` mask bytes                                     |
| `hvcurveto`, `vhcurveto`               | 31, 30       | Append cubic Bezier segments, alternating start/end tangent axis          |
| `callgsubr`                            | 29           | Invoke a global subroutine, biased and depth/step bounded                 |
| `rcurveline`, `rlinecurve`             | 24, 25       | Curve(s) then a trailing line, or line(s) then a trailing curve           |
| `hflex`, `flex`, `hflex1`, `flex1`     | 12 34-37     | Flex shortcuts: two cubic Bezier segments, forcing final point onto axis  |
| `dotsection`                           | 12 0         | Deprecated no-op; clears the operand stack only                           |
<!-- markdownlint-enable MD013 -->

Any operator outside this set - including the remaining two-byte escape operators (arithmetic/
logical and other Type 1 heritage operators) - is rejected with `InvalidDataException`,
as is a charstring that ends before its declared operand/operator data has been fully read.
`callsubr`/`callgsubr` apply the CFF-standard bias (`107` for a subroutine count under 1240,
`1131` for a count under 33900, otherwise `32768`, computed independently per local/global
subroutine INDEX from that INDEX's own entry count) and are bounded by both a maximum call depth
and a maximum total executed step count, mirroring `GlyfLocaReader`'s composite-glyph bounding
posture. The first stack-clearing operator in a charstring may carry one extra leading "width"
operand (recognized by its odd-numbered-out operand count rather than misread as a coordinate);
`CffCharstringInterpreter.Decode` returns that glyph's resolved advance width alongside its
outline - `nominalWidthX` plus the leading operand's value when present, else `defaultWidthX` -
per the Type 2 Charstring specification's width convention. For an SFNT/OpenType (`OTTO`-tagged)
font loaded via `Load`, this resolved width is superseded by that font's own `hmtx`-based
`TrueTypeFont.GetAdvanceWidth` and otherwise unused; for a bare Type1C/CFF program loaded via
`LoadType1C` (no `hmtx` table at all), `CffTable.GetAdvanceWidth(int glyphIndex)` - itself built on
this same `Decode` call - is the sole advance-width source.

##### Type 1 Font Program Structure (`Type1Table`)

A classic PostScript Type 1 font program is not an SFNT font at all: it is a PostScript program
consisting of a cleartext region (the first `length1` bytes) followed by an `eexec`-encrypted
binary region (the next `length2` bytes) containing the font's `/Subrs` and `/CharStrings`
dictionaries, each entry itself further encrypted as a nested "charstring" cipher layer.
`Type1Table.Parse` decrypts the `length2` region with `Type1CharstringDecryption` (`eexec` key
`R0 = 55665`), then scans the decrypted bytes for the literal tokens `/Subrs` and `/CharStrings`
using a token-agnostic binary-blob scanner: rather than requiring specific procedure names for
each entry's own encoding/decoding operators (which vary across font-generation tools - `RD`/`-|`
for reading an entry's raw bytes, `ND`/`|-`/`def` for defining a completed glyph procedure, `NP`/
`|` for defining a completed subroutine procedure), the scanner locates each entry by its `dup
<index> <byte-count>` (for `/Subrs`) or `/<name> <byte-count>` (for `/CharStrings`) prefix
followed by any single non-whitespace-delimited token, then reads exactly `byte-count` raw bytes
immediately after that token and a single delimiting space, decrypting each entry independently
with `Type1CharstringDecryption` (charstring key `R0 = 4330`) and discarding that entry's own
`lenIV` leading bytes (default `4`, overridable by an explicit `/lenIV` entry appearing before
`/CharStrings` in the decrypted region). A glyph named `.notdef` is always forced to glyph index
`0` regardless of its position in the `/CharStrings` dictionary, matching `glyf`/CFF-flavored
font convention; every other glyph is indexed in first-seen dictionary order. The `/CharStrings`
scan is bounded by that dictionary's own matching closing `end` keyword - tracked via `begin`/
`end` nesting depth, rather than scanning unconditionally to the end of the decrypted plaintext -
so trailing font-closing PostScript boilerplate some real-world producers emit immediately after
the dictionary closes (for example `end end readonly put noaccess put dup /FontName get exch
definefont pop mark currentfile closefile`) is never misinterpreted as further glyph entries.
`GetAdvanceWidth`
is derived without fully decoding a glyph's outline, by peeking only as far as that glyph's own
leading `hsbw`/`sbw` operator and its width operand.

###### Type 1 Charstring Structure (`Type1CharstringInterpreter`)

Each `/CharStrings` (or `/Subrs`) entry is a sequence of integer-only operands (Type 1 has no
16.16 fixed-point encoding; operator byte `255` is instead a plain big-endian 32-bit signed
integer) and single- or two-byte operator codes, interpreted against the same running-point/
current-subpath state model as `CffCharstringInterpreter`, but over a different, Type-1-specific
operator set and with no `callsubr`/`callgsubr` bias (Type 1 subroutine indices are used directly):

<!-- markdownlint-disable MD013 -->
| Operator(s) | Code(s) | Behavior |
| --------------------------------------------------- | --------------- | ---------------------------------------------------------------------------------- |
| `hstem`, `vstem`, `hstem3`, `vstem3`, `dotsection` | 1, 3, 12 2, 12 1, 12 0 | Consumed and discarded; this unit performs no hint execution |
| `hsbw` | 13 | Captures the glyph's left side bearing and advance width, sets the initial point |
| `sbw` | 12 7 | As `hsbw`, but with independent X and Y side bearing/advance components |
| `rmoveto`, `hmoveto`, `vmoveto` | 21, 22, 4 | Starts a new subpath at a general/horizontal-only/vertical-only offset |
| `rlineto`, `hlineto`, `vlineto` | 5, 6, 7 | Appends a line segment; `hlineto`/`vlineto` are axis-only |
| `rrcurveto`, `hvcurveto`, `vhcurveto` | 8, 31, 30 | Appends a cubic Bezier segment, `hvcurveto`/`vhcurveto` alternating start/end axis |
| `closepath` | 9 | Closes the current subpath |
| `callsubr`, `return` | 10, 11 | Invoke/resume a local subroutine by direct (unbiased) index, depth/step bounded |
| `div` | 12 12 | Pops the top two stack values regardless of stack depth, pushes their quotient |
| `callothersubr`, `pop`, `setcurrentpoint` | 12 16, 12 17, 12 33 | Implement real flex geometry (othersubr 1/2/0) and hint replacement (othersubr 3), detailed below |
| `endchar` | 14 | Finishes the outline |
<!-- markdownlint-enable MD013 -->

`seac` (escape operator `12 6`, the legacy accented-composite-glyph operator) is explicitly
rejected with `InvalidDataException` wherever encountered; any other operator outside this set is
likewise rejected with `InvalidDataException`, as is a charstring that ends before its declared
operand/operator data has been fully read. Flex is implemented as real curve geometry rather than
a no-op: `callothersubr` index `1` begins buffering up to seven subsequent `rmoveto` deltas and
marks the interpreter as "flexing" (during which no point is actually committed to the path
yet); index `2` marks an intermediate flex reference point (a no-op beyond continuing to buffer);
index `0` ends flex, requires exactly the three arguments `[flexHeight, finalX, finalY]` and
exactly seven buffered points, discards the first buffered point (an off-curve reference only),
and converts the remaining six into two `CubicBezierTo` calls - matching the real curve a flex
hint is a rendering-quality annotation for, rather than the polyline of straight `rmoveto` deltas
a naive pass-through would otherwise draw. After ending flex, the interpreter pushes `finalY` then
`finalX` onto a small internal PostScript-operand stack so that the charstring's own subsequent
`pop pop setcurrentpoint` sequence observes `[finalX, finalY]` in the order it expects. Hint
replacement (`callothersubr` index `3`) is treated as transparent pass-through: it pops exactly
one argument (a subroutine number) onto the same internal stack, and the charstring's own
subsequent `pop callsubr` sequence retrieves and invokes that subroutine exactly as if no hint
replacement had occurred, since this unit performs no hint execution of its own.

##### PFB Segmented Binary Structure (`Type1PfbReader`)

A `.pfb`-framed Type 1 font file wraps its cleartext and `eexec`-encrypted regions (plus a final
PostScript trailer) in a generic sequence of length-prefixed segments, each headed by
`0x80 <type-byte> <4-byte little-endian length>`, where `<type-byte>` is `0x01` (ASCII/cleartext
segment), `0x02` (binary/encrypted segment), or `0x03` (end-of-file marker, carrying no length or
payload). `Type1PfbReader.Read` loops generically over however many segments the file actually
contains (not a hard-coded three-segment assumption): ASCII segments encountered before the first
binary segment are concatenated, header-stripped, into the cleartext region (`length1`); binary
segments are concatenated, header-stripped, into the encrypted region (`length2`); any further
ASCII segment appearing after the encrypted region (the standard zero-fill/`cleartomark` trailer)
is read past and discarded rather than appended to either region; the loop stops at a `0x03`
marker or the end of the input. `TrySniff` recognizes a `.pfb` file by its leading `0x80` marker
byte. `Read` fails closed with `InvalidDataException` on a truncated segment header or payload, an
unrecognized segment type byte, or a file containing no binary (`0x02`) segment at all.

##### PFA Hex-Encoded ASCII Structure (`Type1PfaReader`)

A `.pfa`-framed Type 1 font file instead represents its entire content as printable ASCII,
beginning with `%!`, with the `eexec`-encrypted region hex-encoded (two ASCII hex digits per
encrypted byte, tolerating embedded whitespace/newlines within the hex run) immediately following
the literal keyword `eexec`. `Type1PfaReader.Read` locates that keyword, hex-decodes the run of
hex digits and whitespace immediately following it (any other byte ends the run), trims trailing
zero-padding bytes from the decoded result (a common `.pfa` convention before the trailer), and
takes the cleartext region (`length1`) to be the original file bytes from offset zero through the
end of the literal `eexec` keyword - the cleartext region's own content is never reinterpreted by
this reader, only its byte count matters to `Type1Table.Parse`. `TrySniff` recognizes a `.pfa`
file by its leading `%!` bytes. `Read` fails closed with `InvalidDataException` when the `eexec`
keyword is missing, the decoded hex-digit count is odd, a non-hex/non-whitespace byte is
encountered before any hex digit has been read, or zero bytes remain after trimming trailing
zero-padding.

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

##### `name` Table Layout Used

| Offset | Size         | Field          | Use in This Unit                                              |
| ------ | ------------ | -------------- | ------------------------------------------------------------- |
| 0      | 2            | `format`       | Read but not otherwise interpreted (0 and 1 both accepted)    |
| 2      | 2            | `count`        | Number of name records; sizes the record array                |
| 4      | 2            | `stringOffset` | Start of the string storage area, relative to table start     |
| 6      | `12 * count` | `nameRecords`  | Each record's platform/encoding/language/nameID/length/offset |

###### Name Record (12 bytes, big-endian)

| Offset | Size | Field        | Use in This Unit                                                         |
| ------ | ---- | ------------ | ------------------------------------------------------------------------ |
| 0      | 2    | `platformID` | `3` (Windows) or `1` (Macintosh) are decoded; all others are ignored     |
| 2      | 2    | `encodingID` | Windows: `1`/`10` (Unicode BMP/full); Macintosh: `0` (Roman)             |
| 4      | 2    | `languageID` | Preferred: Windows `0x0409` (en-US), Macintosh `0` (English)             |
| 6      | 2    | `nameID`     | Only `1`, `2`, `4`, `6`, `16`, `17` are consumed; all others are skipped |
| 8      | 2    | `length`     | String byte length; an out-of-bounds record is skipped, not the table    |
| 10     | 2    | `offset`     | String byte offset, relative to `stringOffset`                           |

`NameTable` resolves each of `FamilyName`/`SubfamilyName`/`FullName`/`PostScriptName` from
whichever qualifying record best matches, in order: a Windows-platform record in the preferred
language, else any Windows-platform record for that nameID; failing that, a Macintosh-platform
record in the preferred language, else any Macintosh-platform record. `FamilyName` and
`SubfamilyName` prefer the typographic nameID (`16`/`17`) over the standard nameID (`1`/`2`) when
both resolve; `FullName` (`4`) and `PostScriptName` (`6`) have no typographic alternate. A
completely absent, truncated, or otherwise unparsable `name` table yields `NameTable.Empty`, whose
resolved strings are all `null` (surfaced as an all-`null` `FontNameInfo`) rather than throwing.

##### `OS/2` Table Fields Used (partial; version-0 layout)

| Offset | Size | Field           | Use in This Unit                                                            |
| ------ | ---- | --------------- | --------------------------------------------------------------------------- |
| 4      | 2    | `usWeightClass` | `IsBold` is also `true` when this is `>= 600`, even if `fsSelection` is not |
| 62     | 2    | `fsSelection`   | Bit 0 (italic) and bit 5 (bold) are OR'd into `IsItalic`/`IsBold`           |

`OS/2` is entirely optional. `StyleTable` gates each field independently on the table being long
enough to contain it (`usWeightClass` needs at least 6 bytes; `fsSelection` needs at least 64), so
a present-but-truncated `OS/2` table contributes only the fields it can actually reach, and an
absent `OS/2` table simply leaves `IsBold`/`IsItalic` to whatever `head.macStyle` and `post`
independently indicate.

##### `post` Table Fields Used (partial; version 1.0/2.0/3.0 header)

| Offset | Size | Field          | Use in This Unit                                                  |
| ------ | ---- | -------------- | ----------------------------------------------------------------- |
| 4      | 4    | `italicAngle`  | `IsItalic` is also `true` when this fixed-point value is non-zero |
| 12     | 4    | `isFixedPitch` | `IsFixedPitch` is `true` when this is non-zero                    |

`post` is entirely optional; `IsFixedPitch` defaults to `false` and is set only when `post` is
present and at least 16 bytes long. `italicAngle` needs at least 8 bytes to be read; a shorter
`post` table simply does not contribute to `IsItalic`.

#### Key Methods

##### Load(Stream stream)

Copies the source stream into memory. First checks whether the bytes are a standalone Type 1 font
program framed as `.pfb` (`Type1PfbReader.TrySniff`) or `.pfa` (`Type1PfaReader.TrySniff`); if so,
parses it with `Type1Table.Parse` using the built-in `Type1StandardGlyphNames` default encoding
and returns that font directly. Otherwise, parses the SFNT container (transparently selecting
face 0 if the stream begins with a `ttcf` header), validates every required table for the font's
outline flavor, parses `head`, `maxp`, `hhea`, and `hmtx`, eagerly parses `loca` (glyf-flavored)
or the `CFF` table (CFF-flavored), and parses `cmap` / `kern` leniently when present.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the SFNT version is unrecognized, an `OTTO`-flavored font is missing
  its `CFF` table, a required table is missing or malformed, a table directory entry is out of
  bounds or would overflow ordinary arithmetic, the stream is truncated, (CFF-flavored only) the
  CFF data is CID-keyed, structurally malformed, or its CharStrings count disagrees with `maxp`,
  or (recognized as `.pfb`/`.pfa`-framed) the Type 1 font program data is structurally malformed

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

##### LoadType1(Stream stream, int length1, int length2, codepointToGlyphName)

Loads a classic PostScript Type 1 font program from its raw cleartext (`length1` bytes) and
`eexec`-encrypted (`length2` bytes) byte segments, exactly as they would be extracted from a
PDF Type1 font's own `FontFile` stream, together with a caller-supplied codepoint-to-glyph-name
encoding. Copies `stream` into memory, parses it with `Type1Table.Parse(bytes, length1, length2)`,
and builds a `TrueTypeFont` around the resulting `Type1Table` (via the shared internal
`BuildFromType1Table` helper also used by standalone `.pfb`/`.pfa` auto-detection), synthesizing
its `CmapTable` from `codepointToGlyphName` resolved against `Type1Table.TryGetGlyphIndex` and its
`HmtxHheaReader` from every glyph's own `GetAdvanceWidth`.

**Throws:**

- `ArgumentNullException` — `stream` or `codepointToGlyphName` is null
- `InvalidDataException` — `length1`/`length2` are negative, exceed the stream's own length, or
  the Type 1 font program data itself (the `/Subrs`/`/CharStrings` structure or any glyph's own
  charstring, including a charstring using `seac` or another unsupported operator) is malformed

##### LoadType1C(Stream stream, codepointToGlyphName)

Loads a bare Type1C/CFF font program (a raw CFF table with no SFNT/OpenType wrapper of its own, as
would be extracted from a PDF Type1 font's own `FontFile3` stream), together with a
caller-supplied codepoint-to-glyph-name encoding - the structural counterpart of `LoadType1` for
fonts whose embedded program is itself CFF-flavored rather than a classic Type 1 program. Copies
`stream` into memory, parses it with `CffTable.Parse(bytes, tableOffset: 0, tableLength: bytes.Length)`,
and builds a `TrueTypeFont` around the resulting `CffTable` (via the dedicated internal
`BuildFromCffTable` helper - deliberately not shared with `BuildFromType1Table`, since
`CffTable.GetAdvanceWidth` returns `double` while `Type1Table.GetAdvanceWidth` returns `int`,
requiring a rounding step this helper owns), synthesizing its `CmapTable` from
`codepointToGlyphName` resolved against `CffTable.TryGetGlyphIndex` and its `HmtxHheaReader` from
every glyph's own `GetAdvanceWidth`, with a fixed 1000-unit em square (`CffTable` carries no
`head`/`unitsPerEm` table of its own).

**Throws:**

- `ArgumentNullException` — `stream` or `codepointToGlyphName` is null
- `InvalidDataException` — the CFF table data is malformed (see `CffTable.Parse`), including a
  CID-keyed (`ROS`-declaring) CFF program, which `CffTable.Parse`'s existing CID-keyed rejection
  surfaces uncaught through this path with no new translation code

##### GetFaceCount(Stream stream) / GetFaceCount(string path)

Reads only enough of the stream to distinguish a `ttcf` container from an ordinary SFNT font: the
first 4 bytes (the tag), and - only if that tag is `ttcf` - the header's `numFonts` field. Returns
`1` for an ordinary SFNT font, for a standalone `.pfb`/`.pfa`-framed Type 1 font file (neither of
which begins with the `ttcf` tag), or the container's own declared face count for a `ttcf` file.
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
the last explicit advance-width entry for any tail glyph beyond `numOfLongHorMetrics`. For a font
loaded via `Load` (SFNT/OpenType, including CFF-flavored `OTTO`), `hmtx` is the font's own parsed
table (even for a CFF-flavored font, the CFF-native width resolved by `CffTable.GetAdvanceWidth`
described under Type 2 Charstring Structure above is unused, superseded by this required `hmtx`
table). For a font loaded via `LoadType1` or `LoadType1C` (neither of which carries its own
`hmtx`), `hmtx` is instead synthesized at load time via `HmtxHheaReader.FromAdvanceWidths` from
every glyph's own `Type1Table.GetAdvanceWidth`/`CffTable.GetAdvanceWidth`.

**Throws:**

- `ArgumentOutOfRangeException` — `glyphIndex` is outside `[0, GlyphCount)`

##### GetKerning(int leftGlyphIndex, int rightGlyphIndex)

Binary-searches the selected `kern` format-0 pair array.

**Throws:**

- Never throws; returns `0` for a missing pair, for a missing or unusable `kern` table, and even
  for an out-of-range glyph index supplied only for kerning lookup

##### GetNameInfo()

Returns a `FontNameInfo` populated from the loaded font's `name` table, resolving `FamilyName`,
`SubfamilyName`, `FullName`, and `PostScriptName` per the platform/nameID preference order
described under `name` Table Layout Used above.

**Throws:**

- Never throws; a member whose corresponding record cannot be resolved (including every member,
  for a font with no `name` table at all) is `null`

##### IsBold / IsItalic / IsFixedPitch

Read-only properties computed once at load time by `StyleTable` from whichever of `OS/2.fsSelection`
/ `OS/2.usWeightClass`, `head.macStyle`, and `post.italicAngle` / `post.isFixedPitch` are present,
per the derivation rules described under the `OS/2` and `post` table-layout subsections above.

**Throws:**

- Never throws; each property degrades to `false` when its underlying optional table(s) are
  absent or too short to contain the relevant field

##### Internal Helper Roles

- `SfntContainer` parses the offset table and table directory, enforces overflow-safe bounds
  checks, hosts the shared big-endian primitive readers, and recognizes/parses `ttcf` collection
  headers via `TryReadTtcHeader`
- `CmapTable` selects one supported format-4 or format-12 subtable and exposes a lookup delegate
- `GlyfLocaReader` (an `IGlyphOutlineSource`) eagerly parses `loca`, then lazily decodes simple
  and composite `glyf` glyphs
- `CffTable` (an `IGlyphOutlineSource`) parses the CFF Header/INDEXes/Private DICT/charset, then
  lazily decodes each glyph's Type 2 charstring via `CffCharstringInterpreter`, exposing
  `TryGetGlyphIndex` (glyph-name resolution via the parsed charset) and `GetAdvanceWidth` (the
  CFF-native `defaultWidthX`/`nominalWidthX`-resolved width)
- `CffCharstringInterpreter` executes a single glyph's Type 2 charstring bytecode against the
  supported operator subset, returning both the decoded outline and the glyph's resolved advance
  width
- `Type1Table` (an `IGlyphOutlineSource`) decrypts the `eexec` region, scans it for `/Subrs` and
  `/CharStrings` entries in a procedure-name-agnostic way, then lazily decodes each glyph's Type 1
  charstring via `Type1CharstringInterpreter`
- `Type1CharstringInterpreter` executes a single glyph's Type 1 charstring bytecode against the
  supported operator subset, including real flex geometry and hint-replacement pass-through
- `Type1CharstringDecryption` implements the shared `eexec`/charstring LCG decryption algorithm
  used by both the `eexec` region itself and each individual `/Subrs`/`/CharStrings` entry
- `Type1PfbReader` / `Type1PfaReader` auto-detect and reassemble a standalone `.pfb`/`.pfa`-framed
  Type 1 font file into the same cleartext/`length1`/`length2` byte-segment shape `Type1Table.Parse`
  and `LoadType1` already consume
- `Type1StandardGlyphNames` supplies the small, curated, built-in codepoint-to-glyph-name
  vocabulary used as the default encoding for standalone `.pfb`/`.pfa` auto-detection
- `HmtxHheaReader` parses top-level typographic metrics and advance widths (from `hmtx`, or
  synthesized via `FromAdvanceWidths` for a Type 1 font program or a bare Type1C/CFF program)
- `KernTable` tolerantly parses the first qualifying horizontal format-0 subtable, if any
- `NameTable` tolerantly parses the `name` table's records and resolves the platform/nameID
  preference order into `FamilyName`/`SubfamilyName`/`FullName`/`PostScriptName`
- `StyleTable` derives `IsBold`/`IsItalic`/`IsFixedPitch` from whichever of `OS/2`, `head.macStyle`,
  and `post` are present

#### Error Handling

##### Public Exception Contract

<!-- markdownlint-disable MD013 -->
| Member                 | Exception                                   | Condition                                                                                                        |
| ---------------------- | ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `Load(Stream)`         | `ArgumentNullException`                     | `stream` is null                                                                                                 |
| `Load(Stream)`         | `InvalidDataException`                      | Bad SFNT version, missing required table, truncation, invalid CFF data, or malformed Type 1 data                 |
| `Load(string)`         | `ArgumentNullException`                     | `path` is null                                                                                                   |
| `Load(string)`         | `ArgumentException`                         | `path` is empty                                                                                                  |
| `Load(string)`         | `InvalidDataException`                      | Same font-structure failures as `Load(Stream)`                                                                   |
| `Load(Stream, int)`    | `ArgumentNullException`                     | `stream` is null                                                                                                 |
| `Load(Stream, int)`    | `ArgumentOutOfRangeException`               | `faceIndex` is negative or not less than the file's own face count (always `1` for standalone Type 1)            |
| `Load(Stream, int)`    | `InvalidDataException`                      | As `Load(Stream)`, plus a malformed `ttcf` header                                                                |
| `Load(string, int)`    | `ArgumentNullException`/`ArgumentException` | Null/empty `path`, as `Load(string)`                                                                             |
| `Load(string, int)`    | `ArgumentOutOfRangeException`               | As `Load(Stream, int)`                                                                                           |
| `Load(string, int)`    | `InvalidDataException`                      | As `Load(Stream, int)`                                                                                           |
| `LoadType1(...)`       | `ArgumentNullException`                     | `stream` or `codepointToGlyphName` is null                                                                       |
| `LoadType1(...)`       | `InvalidDataException`                      | `length1`/`length2` negative or exceed stream length, or Type 1 data (including `seac`) is malformed             |
| `LoadType1C(...)`      | `ArgumentNullException`                     | `stream` or `codepointToGlyphName` is null                                                                       |
| `LoadType1C(...)`      | `InvalidDataException`                      | Malformed CFF table data, including a CID-keyed (`ROS`-declaring) CFF program                                    |
| `GetFaceCount(Stream)` | `ArgumentNullException`                     | `stream` is null                                                                                                 |
| `GetFaceCount(Stream)` | `InvalidDataException`                      | Stream too short for a tag, or a malformed `ttcf` header                                                         |
| `GetFaceCount(string)` | `ArgumentNullException`/`ArgumentException` | Null/empty `path`                                                                                                |
| `GetFaceCount(string)` | `InvalidDataException`                      | As `GetFaceCount(Stream)`                                                                                        |
| `GetGlyphIndex(int)`   | none                                        | Unmapped codepoints and unusable `cmap` data return `0`                                                          |
| `GetGlyphOutline(int)` | `ArgumentOutOfRangeException`               | `glyphIndex` is outside `[0, GlyphCount)`                                                                        |
| `GetGlyphOutline(int)` | `InvalidDataException`                      | Malformed/truncated glyph or charstring data, or a rejected composite-glyph/CFF-operator feature                 |
| `GetAdvanceWidth(int)` | `ArgumentOutOfRangeException`               | `glyphIndex` is outside `[0, GlyphCount)`                                                                        |
| `GetKerning(int, int)` | none                                        | Missing pair/table or bad kerning data returns `0`                                                               |
| `GetNameInfo()`        | none                                        | Unresolvable name members (including every member, absent `name` table) are `null`                               |
| `IsBold`               | none                                        | Degrades to `false` when `OS/2`/`head.macStyle` cannot indicate boldness                                         |
| `IsItalic`             | none                                        | Degrades to `false` when `OS/2`/`head.macStyle`/`post` cannot indicate italics                                   |
| `IsFixedPitch`         | none                                        | Degrades to `false` when `post` is absent or too short                                                           |
<!-- markdownlint-enable MD013 -->

##### Required vs. Optional Tables

`head`, `maxp`, and `hhea`/`hmtx` are required for every font regardless of outline flavor,
because `TrueTypeFont` cannot report top-level metrics without them. A glyf-flavored font (`maxp`
version `0x00010000`) additionally requires `loca` and `glyf`; a CFF-flavored (`OTTO`-tagged)
font (`maxp` version `0x00005000`) instead requires `CFF` and does **not** require `loca`/`glyf`
at all. By contrast, `cmap`, `kern`, `name`, `OS/2`, and `post` are all intentionally lenient for
either flavor. A missing or unusable `cmap` still leaves the font usable by glyph index directly,
so `GetGlyphIndex` degrades
to returning `0`. A missing or malformed `kern` table does not prevent metrics or outlines from
being queried, so `GetKerning` degrades to returning `0`. Similarly, a missing or malformed
`name`, `OS/2`, or `post` table never prevents the font from loading or being queried for
metrics/outlines: `GetNameInfo()` degrades to `null` members and `IsBold`/`IsItalic`/
`IsFixedPitch` degrade to `false` for whichever source table is absent or unusable.

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

##### CFF Seac-Style Accent Composition

The deprecated 4-operand form of `endchar` (`adx ady bchar achar endchar`, the Type 2 charstring
successor to the original Type 1 `seac` operator) composes an accented glyph from a "base" glyph
(e.g. `A`) and an "accent" glyph (e.g. `grave`), both identified by Adobe StandardEncoding code
rather than glyph index. `CffCharstringInterpreter.Decode` accepts an optional
`resolveStandardEncodedGlyph` callback; when supplied, a 4-operand `endchar` resolves `bchar` and
`achar` through it to obtain each component's own already-decoded outline, translates the accent
outline by `(adx, ady)`, and combines it with the base outline (at its own origin, untranslated) to
form the composite glyph's final outline. The composite's own advance width still follows the
normal width convention (its own leading width operand, or `defaultWidthX` if absent) and never
inherits the base/accent glyphs' own widths.

`CffTable` supplies this callback (`ResolveSeacComponent`) by mapping `bchar`/`achar` through
`CffStandardEncoding.CodeToGlyphName` to a glyph name, resolving that name to a glyph index in the
same font via `TryGetGlyphIndex`, and recursively decoding that glyph's own charstring -
deliberately *without* passing a resolver of its own. This means a seac component glyph that is
itself (illegally) defined using the seac-style form is rejected with `InvalidDataException`
rather than recursed into, structurally bounding seac composition to a single level without a
separate depth counter. The same exception is thrown when no resolver is supplied at all (the
form is otherwise unreachable), when `bchar`/`achar` is not a valid StandardEncoding code, or when
the resolved glyph name is not present in this font's charset.

##### Type 1 Subroutine Call Bounds and Fail-Closed Unsupported Operators

`Type1CharstringInterpreter` applies the same deterministic-limit posture as
`CffCharstringInterpreter`: a maximum `callsubr` nesting depth and a maximum total executed
charstring-operator step count, both charged per single glyph decode, catching self-recursive
subroutine cycles and excessively long/repetitive non-cyclic call chains alike. Unlike Type 2
charstrings, Type 1 `callsubr` addresses subroutines directly with no bias adjustment. `seac`
(the legacy accented-composite-glyph operator) and every operator outside the supported Type 1
set are rejected immediately with `InvalidDataException` the moment they are encountered, rather
than being silently skipped or approximated, so a font relying on either cannot produce a
silently-wrong outline.

#### Dependencies

`TrueTypeFont` depends only on the `Geometry` subsystem: `Path` as the public outline return
type, `PathBuilder` as the construction mechanism for decoded contours, and `PathCommand`
semantics as the command set a caller later walks if it needs to transform the returned outline.
The unit has no dependency on `Canvas`, `Drawing`, or any runtime NuGet package. This holds
equally for the Type 1 support added by `Type1Table`, `Type1CharstringInterpreter`,
`Type1CharstringDecryption`, `Type1PfbReader`, `Type1PfaReader`, and `Type1StandardGlyphNames`:
all six are pure byte-array/`Geometry` code with no additional dependency of their own.

#### Callers

`TrueTypeFont` is called directly by consumers of CanvasNet. Within this repository, its
immediate callers are the `Fonts` unit tests, the real-font integration tests in
`TrueTypeFontRealFontIntegrationTests.cs` (real `.ttf`, real CFF/OpenType `.otf`, and a
locally-assembled multi-face `.ttc`), and the two `CanvasNetTests.cs` system-integration tests
that scale and flip the returned outline before filling it through `Drawing.PathFiller` onto a
`Canvas.Surface`.
