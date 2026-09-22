### TrueTypeFont Unit Verification Design

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea maxp notdef -->
<!-- cspell:ignore codepoint codepoints subtable -->
This document describes the unit-level verification strategy for the `TrueTypeFont` class and its
supporting internal helpers `SfntContainer`, `CmapTable`, `GlyfLocaReader`, `HmtxHheaReader`, and
`KernTable`.

#### Verification Approach

The `TrueTypeFont` unit is verified through focused unit tests that exercise each helper directly
via `InternalsVisibleTo`, plus end-to-end `TrueTypeFont` tests that load a complete synthetic
font and drive the public API. Every fixture font is assembled in memory by the shared
`SyntheticFontBuilder` helper under `test/DemaConsulting.CanvasNet.Tests/TestSupport/`; no
third-party font files are used, so the tests avoid fixture licensing concerns while still
covering both well-formed and deliberately malformed SFNT structures.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; every dependency is either an in-house helper or raw byte arrays
- **Isolation**: Each test constructs its own font bytes or writes its own temporary file path

#### Acceptance Criteria

A unit test run passes when every scenario below passes without error or unexpected exception, and
when every named test method listed for each requirement ID passes across every target framework.

#### Test Scenarios

##### CanvasNet-Fonts-TrueTypeFont-LoadStream: Load from Stream Exposes Top-Level Metrics

**Tests**: `TrueTypeFont_Load_WellFormedFont_ExposesMetrics`

Builds a complete synthetic font in memory, loads it from a `MemoryStream`, and asserts the
public metrics properties mirror the table data.

##### CanvasNet-Fonts-TrueTypeFont-LoadPath: Load from File Path Opens and Parses the Font

**Tests**: `TrueTypeFont_Load_Path_ReadsFromFile`

Writes a complete synthetic font to a temporary file, loads it through `Load(string)`, and
asserts the loaded object exposes the expected metrics.

##### CanvasNet-Fonts-TrueTypeFont-LoadNullStream: Null Stream Is Rejected

**Tests**: `TrueTypeFont_Load_NullStream_ThrowsArgumentNullException`

Calls `Load(Stream)` with `null` and asserts `ArgumentNullException` is thrown immediately.

##### CanvasNet-Fonts-TrueTypeFont-LoadNullPath: Null Path Is Rejected

**Tests**: `TrueTypeFont_Load_NullPath_ThrowsArgumentNullException`

Calls `Load(string)` with `null` and asserts `ArgumentNullException` is thrown immediately.

##### CanvasNet-Fonts-TrueTypeFont-LoadEmptyPath: Empty Path Is Rejected

**Tests**: `TrueTypeFont_Load_EmptyPath_ThrowsArgumentException`

Calls `Load(string)` with an empty path and asserts `ArgumentException` is thrown.

##### CanvasNet-Fonts-TrueTypeFont-RejectUnrecognizedVersion: Unsupported SFNT Versions Fail Fast

**Tests**: `SfntContainer_Parse_UnrecognizedVersion_ThrowsInvalidDataException`

Builds an otherwise minimal font with an arbitrary unrecognized `sfntVersion` and asserts
`InvalidDataException` is thrown before any table parsing proceeds.

##### CanvasNet-Fonts-TrueTypeFont-RejectCffOutlines: OTTO/CFF Fonts Are Rejected

**Tests**: `TrueTypeFont_Load_OttoFont_ThrowsInvalidDataException`,
`SfntContainer_Parse_OttoVersion_ThrowsInvalidDataException`

Exercises both the public `Load` path and the container parser directly with an `OTTO` font and
asserts both reject the unsupported outline flavor.

##### CanvasNet-Fonts-TrueTypeFont-RejectMissingRequiredTable: Missing Required Tables Fail Load

**Tests**: `TrueTypeFont_Load_MissingRequiredTable_ThrowsInvalidDataException`

Builds a font that omits required tables and asserts `Load` throws `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-RejectUnsupportedGlyphOutlineVersion: Unsupported `maxp` Versions Fail Load

**Tests**: `TrueTypeFont_Load_MaxpVersion05_ThrowsInvalidDataException`

Builds a font whose `maxp.version` is `0x00005000` and asserts `Load` rejects it.

##### CanvasNet-Fonts-TrueTypeFont-RejectInvalidUnitsPerEm: Zero Units-Per-Em Is Rejected

**Tests**: `TrueTypeFont_Load_ZeroUnitsPerEm_ThrowsInvalidDataException`

Builds a font with `head.unitsPerEm == 0` and asserts `Load` rejects it.

##### CanvasNet-Fonts-TrueTypeFont-RejectInvalidGlyphLocationFormat: Unsupported `loca` Formats Are Rejected

**Tests**: `TrueTypeFont_Load_InvalidIndexToLocFormat_ThrowsInvalidDataException`

Builds a font with an invalid `head.indexToLocFormat` value and asserts `Load` rejects it.

##### CanvasNet-Fonts-TrueTypeFont-RejectOutOfBoundsTable: Table Directory Bounds Are Overflow-Safe

**Tests**: `SfntContainer_Parse_TableDirectoryEntryOverflowsUint_ThrowsInvalidDataException`,
`SfntContainer_Parse_TableDirectoryEntryOutOfBounds_ThrowsInvalidDataException`

Constructs directory entries that either overflow ordinary arithmetic or point past the file end
and asserts the parser rejects both cases.

##### CanvasNet-Fonts-TrueTypeFont-RejectTruncatedStream: Truncation Is Rejected at Every Relevant Layer

**Tests**: `TrueTypeFont_Load_TruncatedStream_ThrowsInvalidDataException`,
`SfntContainer_Parse_TruncatedOffsetTable_ThrowsInvalidDataException`,
`SfntContainer_Parse_TruncatedTableDirectory_ThrowsInvalidDataException`

Exercises a truncated overall font, a truncated offset table, and a truncated directory, asserting
each failure is surfaced as `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-GlyphIndexBasicMultilingualPlane: BMP Codepoints Resolve Through Format 4

**Tests**: `CmapTable_Format4_BmpLookup_ReturnsMappedGlyphIndex`,
`TrueTypeFont_Load_EndToEnd_GlyphIndexOutlineAdvanceAndKerning`

Verifies a BMP codepoint maps correctly in isolation and again through the fully loaded public
API.

##### CanvasNet-Fonts-TrueTypeFont-GlyphIndexSupplementaryPlane: Supplementary-Plane Codepoints Resolve Through Format 12

**Tests**: `CmapTable_Format12_SupplementaryPlaneLookup_ReturnsMappedGlyphIndex`,
`CmapTable_Format12_GroupRange_ReturnsOffsetGlyphIndex`

Verifies a single supplementary-plane codepoint and a contiguous format-12 range map to the
expected glyph indices.

##### CanvasNet-Fonts-TrueTypeFont-GlyphIndexUnmappedFallback: Unmapped Codepoints Return `.notdef`

**Tests**: `CmapTable_Format4_UnmappedCodepoint_ReturnsZero`,
`TrueTypeFont_GetGlyphIndex_UnmappedCodepoint_NeverThrows`

Asserts both the raw `cmap` helper and the public API return glyph index `0` for unmapped
codepoints without throwing.

##### CanvasNet-Fonts-TrueTypeFont-GlyphIndexNoCharacterMap: Missing or Unsupported `cmap` Data Falls Back Cleanly

**Tests**: `CmapTable_NoSupportedSubtable_GetGlyphIndexReturnsZero`,
`CmapTable_Empty_GetGlyphIndexReturnsZero`,
`TrueTypeFont_Load_NoCmapOrKern_FallsBackGracefully`

Covers the absence of a usable `cmap` table at the helper and end-to-end levels, asserting
lookup falls back to `0` rather than failing load.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineSimple: Simple Glyphs Decode to Lines and Quadratics

**Tests**: `GlyfLocaReader_SimpleGlyph_AllOnCurve_ProducesLineSegments`,
`GlyfLocaReader_SimpleGlyph_ConsecutiveOffCurvePoints_ProducesImpliedMidpointQuadratics`,
`GlyfLocaReader_SimpleGlyph_MixedOnAndOffCurve_ProducesQuadraticToRealOnCurvePoint`

Covers all-on-curve contours, consecutive off-curve points, and mixed on/off-curve contours,
asserting the decoded `Path` uses the expected command shapes.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineEmpty: Empty Glyphs Return `Path.Empty`

**Tests**: `GlyfLocaReader_EmptyGlyph_ZeroContours_ProducesEmptyPath`,
`GlyfLocaReader_ZeroLengthGlyph_ProducesEmptyPath`

Asserts both a zero-contour simple glyph and a zero-length `loca` entry decode as an empty path.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineComposite: Composite Glyph Components Are Transformed Correctly

**Tests**: `GlyfLocaReader_CompositeGlyph_SingleComponent_ProducesTranslatedOutline`,
`GlyfLocaReader_CompositeGlyph_ScaledComponent_ScalesOutline`,
`GlyfLocaReader_CompositeGlyph_TwoByTwoTransform_TransformsOutline`,
`GlyfLocaReader_CompositeGlyph_MultipleComponents_ProducesMultipleSubpaths`,
`GlyfLocaReader_CompositeGlyph_ScaledComponentOffset_TransformsTranslation`

Verifies translation, uniform scaling, full 2x2 transforms, multiple components within a single
composite glyph, and that `SCALED_COMPONENT_OFFSET` transforms a component's dx/dy translation
through its own scale/2x2 matrix rather than applying it unscaled.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineNestedComposite: Nested Composites Resolve Recursively

**Tests**: `GlyfLocaReader_CompositeGlyph_NestedComposite_ResolvesRecursively`

Builds a composite glyph that references another composite glyph and asserts the final outline is
resolved with both transforms applied.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineRejectsCircularComposite: Cycles Are Rejected Deterministically

**Tests**: `GlyfLocaReader_CompositeGlyph_SelfReferentialCycle_ThrowsInvalidDataExceptionViaDepthCap`

Creates a self-referential composite glyph and asserts the deterministic depth cap rejects it
instead of hanging.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineRejectsExcessiveComposite: Exponential Composite Blow-Up Is Rejected

**Tests**: `GlyfLocaReader_CompositeGlyph_ExponentialBlowUp_ThrowsInvalidDataExceptionViaComponentCap`

Constructs an acyclic but explosively branching composite graph and asserts the total-component
cap rejects it.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineRejectsPointMatchedComponent: Point-Matched Components Are Rejected

**Tests**: `GlyfLocaReader_CompositeGlyph_PointMatchedComponent_ThrowsInvalidDataException`

Builds a composite glyph that uses point matching rather than XY offsets and asserts decoding
fails with `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineRejectsMalformedGlyphData: Malformed Glyph Bytes Are Rejected

**Tests**: `GlyfLocaReader_CompositeGlyph_OutOfRangeComponentGlyphIndex_ThrowsInvalidDataException`,
`GlyfLocaReader_TruncatedGlyphHeader_ThrowsInvalidDataException`,
`GlyfLocaReader_InvalidContourCount_ThrowsInvalidDataException`,
`GlyfLocaReader_Parse_LocaEntryExceedsGlyfBounds_ThrowsInvalidDataException`,
`GlyfLocaReader_Parse_NonMonotonicLocaEntries_ThrowsInvalidDataException`,
`GlyfLocaReader_Parse_TruncatedLoca_ThrowsInvalidDataException`

Covers out-of-range referenced glyphs, truncated glyph bytes, invalid contour counts, and invalid
`loca` tables, asserting each fails with `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-GlyphIndexValidation: `GetGlyphOutline` Rejects Out-of-Range Glyph Indices

**Tests**: `TrueTypeFont_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException`,
`TrueTypeFont_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException`,
`GlyfLocaReader_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException`,
`GlyfLocaReader_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException`

Exercises both the public API and the helper directly, asserting out-of-range outline requests are
rejected before any glyph bytes are read.

##### CanvasNet-Fonts-TrueTypeFont-AdvanceWidth: Horizontal Advance Widths Follow `hmtx` Semantics

**Tests**: `HmtxHheaReader_Parse_ExposesMetrics`,
`HmtxHheaReader_GetAdvanceWidth_TailGlyph_ReusesLastEntry`,
`TrueTypeFont_Load_EndToEnd_GlyphIndexOutlineAdvanceAndKerning`

Verifies direct `hhea` / `hmtx` parsing, the last-entry tail reuse rule, and the same behavior
through the public `TrueTypeFont` API.

##### CanvasNet-Fonts-TrueTypeFont-AdvanceWidthValidation: `GetAdvanceWidth` Rejects Out-of-Range Glyph Indices

**Tests**: `TrueTypeFont_GetAdvanceWidth_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException`

Calls `GetAdvanceWidth` with an invalid glyph index and asserts `ArgumentOutOfRangeException` is
thrown.

##### CanvasNet-Fonts-TrueTypeFont-KerningLookup: Known Kerning Pairs Are Returned

**Tests**: `KernTable_Format0_KnownPair_ReturnsValue`,
`TrueTypeFont_Load_EndToEnd_GlyphIndexOutlineAdvanceAndKerning`

Verifies a known format-0 pair in isolation and through the full `TrueTypeFont` load/query path.

##### CanvasNet-Fonts-TrueTypeFont-KerningMissOrAbsentFallback: Missing Kerning Data Returns Zero

**Tests**: `KernTable_Format0_UnknownPair_ReturnsZero`,
`KernTable_Empty_GetKerningReturnsZero`,
`TrueTypeFont_GetKerning_OutOfRangeGlyphIndex_NeverThrows`

Asserts missing pairs, missing tables, and out-of-range glyph indices supplied only for kerning
lookups all return `0` without throwing.

##### CanvasNet-Fonts-TrueTypeFont-KerningMalformedTableTolerant: Malformed or Unsupported `kern` Data Is Tolerated

**Tests**: `KernTable_NoPairs_ReturnsZero`, `KernTable_ZeroLengthTable_IsTolerant_ReturnsZero`,
`KernTable_TruncatedTable_IsTolerant_ReturnsZero`,
`KernTable_MalformedPairCount_IsTolerant_ReturnsZero`,
`KernTable_UnsupportedFormat2Subtable_IsTolerant_ReturnsZero`,
`KernTable_CrossStreamCoverage_IsSkipped_ReturnsZero`

Exercises empty, truncated, malformed, unsupported-format, and cross-stream kerning tables,
asserting the parser degrades to "no kerning data" rather than failing load.
