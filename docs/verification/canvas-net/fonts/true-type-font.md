### TrueTypeFont Unit Verification Design

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea maxp notdef -->
<!-- cspell:ignore codepoint codepoints subtable charstring charstrings ttcf hintmask cntrmask -->
<!-- cspell:ignore hstemhm vstemhm callsubr callgsubr hhcurveto vvcurveto hvcurveto vhcurveto -->
<!-- cspell:ignore rlineto hlineto vlineto rmoveto hmoveto vmoveto rrcurveto endchar seac gsubr -->
<!-- cspell:ignore hflex flex1 hflex1 -->
<!-- cspell:ignore noaccess definefont currentfile closefile misparse misparsing subsetting -->
This document describes the unit-level verification strategy for the `TrueTypeFont` class and its
supporting internal helpers `SfntContainer`, `CmapTable`, `GlyfLocaReader`, `CffTable`,
`CffCharstringInterpreter`, `HmtxHheaReader`, and `KernTable`.

#### Verification Approach

The `TrueTypeFont` unit is verified through focused unit tests that exercise each helper directly
via `InternalsVisibleTo`, plus end-to-end `TrueTypeFont` tests that load a complete synthetic
font and drive the public API. Every fixture font used by this synthetic-fixture coverage - glyf-
flavored, CFF/OTTO-flavored, and `ttcf`-collection - is assembled in memory by the shared
`SyntheticFontBuilder` helper under `test/DemaConsulting.CanvasNet.Tests/TestSupport/`; no
third-party font files are used for this portion of the suite, so it avoids fixture licensing
concerns while still covering both well-formed and deliberately malformed SFNT/CFF structures.
Four additional integration tests in `TrueTypeFontRealFontIntegrationTests.cs` load real,
OFL-licensed production font fixtures (`OpenSans-Regular.ttf`, `SourceSans3-Regular.otf`, and the
locally-assembled `OpenSans-SourceSans3.ttc`) to prove the unit genuinely composes with the
`Drawing` pipeline on real-world glyph data - across all three outline-flavor/container
combinations this phase adds - complementing (rather than replacing) the synthetic-fixture
coverage above.

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

##### CanvasNet-Fonts-TrueTypeFont-RejectOttoWithoutCffTable: OTTO Fonts Without a CFF Table Are Rejected

**Tests**: `TrueTypeFont_Load_OttoFont_ThrowsInvalidDataException`,
`SfntContainer_Parse_OttoVersion_ThrowsInvalidDataException`

Exercises both the public `Load` path and the container parser directly with an `OTTO`-tagged
font that omits the required `CFF` table, asserting both reject it; an `OTTO`-tagged font that
*does* contain a well-formed `CFF` table is covered separately (see
`CanvasNet-Fonts-TrueTypeFont-LoadCffOutlines` below) and now succeeds.

##### CanvasNet-Fonts-TrueTypeFont-RejectMissingRequiredTable: Missing Required Tables Fail Load

**Tests**: `TrueTypeFont_Load_MissingRequiredTable_ThrowsInvalidDataException`

Builds a font that omits required tables and asserts `Load` throws `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-RejectUnsupportedGlyphOutlineVersion: Unsupported `maxp` Versions Fail Load

**Tests**: `TrueTypeFont_Load_MaxpVersion05_ThrowsInvalidDataException`

Builds a font whose `maxp.version` is `0x00005000` while still declaring itself glyf-flavored
(TrueType-tagged, not `OTTO`) and asserts `Load` rejects it; the same `maxp.version` on a
genuinely `OTTO`-tagged, CFF-backed font is the expected, accepted combination (see
`CanvasNet-Fonts-TrueTypeFont-LoadCffOutlines`).

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

##### CanvasNet-Fonts-TrueTypeFont-GlyphOutlineRejectsExcessivePoints: Excessive Total Point/Command Count Is Rejected

**Tests**: `GlyfLocaReader_CompositeGlyph_LargeSimpleGlyphReferencedTwice_ThrowsInvalidDataExceptionViaPointBudget`

Builds a composite glyph that references the same large simple glyph enough times to exceed the
total point/command budget while staying well under the total-component cap, and asserts the
point/command budget rejects it.

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
`HmtxHheaReader_Parse_MissingTrailingLsbTail_ParsesSuccessfully`,
`TrueTypeFont_Load_EndToEnd_GlyphIndexOutlineAdvanceAndKerning`

Verifies direct `hhea` / `hmtx` parsing, the last-entry tail reuse rule, and the same behavior
through the public `TrueTypeFont` API. Also verifies that an `hmtx` table omitting its trailing
left-side-bearing-only tail entirely (a shape produced by some real-world font subsetting tools)
still parses successfully, since that tail is never read.

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

##### CanvasNet-Fonts-TrueTypeFont-LoadCffOutlines: CFF/OpenType Fonts Load Through the Same Public API

**Tests**: `TrueTypeFont_Load_OttoFontWithCffTable_Succeeds`,
`SfntContainer_Parse_OttoVersionWithCffTable_Succeeds`,
`TrueTypeFont_RealSourceSans3OtfFont_RendersCffGlyphOutlineAsVisibleInk`

Builds a synthetic `OTTO`-tagged, `CFF`-backed font and asserts both the public `Load` path and
the container parser succeed and expose the expected metrics/outline, then confirms the same
behavior against a real production CFF/OpenType font fixture.

##### CanvasNet-Fonts-TrueTypeFont-RejectCffGlyphCountMismatch: CFF/`maxp` Glyph Count Disagreement Is Rejected

**Tests**: `TrueTypeFont_Load_CffGlyphCountMismatchesMaxp_ThrowsInvalidDataException`

Builds a synthetic CFF font whose CharStrings INDEX count disagrees with `maxp.numGlyphs` and
asserts `Load` rejects it with `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-RejectCidKeyedCff: CID-Keyed CFF Data Is Rejected

**Tests**: `CffTable_Parse_CidKeyedRos_ThrowsInvalidDataException`

Builds a synthetic CFF Top DICT declaring a `ROS` operator and asserts `CffTable` rejects it with
`InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-CffTableParsing: CFF Structural Parsing and Per-Glyph Isolation

**Tests**: `CffTable_Parse_WellFormedFont_ExposesGlyphCountAndOutlines`,
`CffTable_Parse_NoPrivateDict_SucceedsWithNoLocalSubrs`,
`CffTable_GetGlyphOutline_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`,
`CffTable_GetGlyphOutline_OneCorruptGlyph_DoesNotBreakOtherGlyphs`,
`CffTable_Parse_MissingCharStringsOperator_ThrowsInvalidDataException`,
`CffTable_Parse_UnsupportedMajorVersion_ThrowsInvalidDataException`,
`CffTable_Parse_TruncatedTable_ThrowsInvalidDataException`,
`CffTable_Parse_EmptyCharStringsIndex_ThrowsInvalidDataException`

Covers well-formed parsing (with and without a Private DICT/Local Subr INDEX), out-of-range glyph
index rejection, lazy per-glyph decoding such that one corrupt glyph's charstring does not break
any other glyph in the same font, and rejection of a missing `CharStrings` operator, an
unsupported CFF major version, a truncated table, and an empty CharStrings INDEX.

##### CanvasNet-Fonts-TrueTypeFont-CffStemHints: Stem Hint Counting and Hint Mask Byte Skipping

**Tests**: `CffCharstringInterpreter_HStem_VStem_AccumulateStemCountForHintMask`,
`CffCharstringInterpreter_HintMask_ImplicitVStem_CountsTowardMaskBytes`,
`CffCharstringInterpreter_HintMask_TruncatedMaskBytes_ThrowsInvalidDataException`

Verifies `hstem`/`vstem` accumulate the stem count `hintmask`/`cntrmask` uses to size their mask
byte skip, that operand pairs left on the stack before the first mask operator are counted as an
implicit final `vstem`, and that a truncated mask byte region is rejected.

##### CanvasNet-Fonts-TrueTypeFont-CffMoveTo: Moveto Operators and the Optional Leading Width Operand

**Tests**: `CffCharstringInterpreter_RMoveTo_LineTo_Endchar_ProducesClosedContour`,
`CffCharstringInterpreter_RMoveTo_WithLeadingWidth_IgnoresWidthOperand`,
`CffCharstringInterpreter_HMoveTo_VMoveTo_MoveAlongSingleAxis`,
`CffCharstringInterpreter_RMoveTo_WrongOperandCount_ThrowsInvalidDataException`

Verifies `rmoveto` starts a subpath and closes correctly with a following `lineto`/`endchar`, that
an optional leading width operand on the first stack-clearing operator is recognized and
discarded rather than misread as a coordinate, that `hmoveto`/`vmoveto` move along a single axis,
and that an incompatible operand count is rejected.

##### CanvasNet-Fonts-TrueTypeFont-CffLineTo: Lineto Operators and Alternating-Axis Packing

**Tests**: `CffCharstringInterpreter_HLineTo_VLineTo_AlternateAxes`,
`CffCharstringInterpreter_RLineTo_OddOperandCount_ThrowsInvalidDataException`

Verifies `hlineto`/`vlineto` alternate axis correctly across their operand list and that `rlineto`
rejects an odd operand count.

##### CanvasNet-Fonts-TrueTypeFont-CffCurveTo: Curve Operators and Their Operand-Packing Conventions

**Tests**: `CffCharstringInterpreter_RRCurveTo_ProducesCubicBezier`,
`CffCharstringInterpreter_HHCurveTo_VVCurveTo_ProduceCubicBeziers`,
`CffCharstringInterpreter_HHCurveTo_LeadingOperand_AppliesToFirstCurveOnly`,
`CffCharstringInterpreter_HVCurveTo_VHCurveTo_AlternateStartTangent`,
`CffCharstringInterpreter_HVCurveTo_TrailingOperand_SuppliesFinalAxisDelta`

Verifies `rrcurveto` produces a general cubic Bezier, `hhcurveto`/`vvcurveto` produce
axis-constrained-start cubic Beziers (including a leading cross-axis operand applying only to the
first curve), and `hvcurveto`/`vhcurveto` alternate start tangent per curve including a trailing
operand supplying the final curve's otherwise-implied-zero axis delta.

##### CanvasNet-Fonts-TrueTypeFont-CffRCurveLineRLineCurve: Combined Curve-and-Line Operators

**Tests**: `CffCharstringInterpreter_RCurveLine_OneCurveThenLine_ProducesCurveAndLine`,
`CffCharstringInterpreter_RCurveLine_TwoCurvesThenLine_ProducesCurvesAndLine`,
`CffCharstringInterpreter_RCurveLine_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_RCurveLine_WrongOperandCount_OneOver_ThrowsInvalidDataException`,
`CffCharstringInterpreter_RLineCurve_OneLineThenCurve_ProducesLineAndCurve`,
`CffCharstringInterpreter_RLineCurve_TwoLinesThenCurve_ProducesLinesAndCurve`,
`CffCharstringInterpreter_RLineCurve_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_RLineCurve_WrongOperandCount_OneOver_ThrowsInvalidDataException`

Verifies `rcurveline` appends one or more `rrcurveto`-style curves followed by exactly one
trailing line, `rlinecurve` appends one or more relative lines followed by exactly one trailing
`rrcurveto`-style curve (both with one- and two-segment variants), and that each operator rejects
an operand count that is not exactly `6n + 2` (`rcurveline`) or `2n + 6` (`rlinecurve`) for some
`n >= 1`, both one short of and one over the nearest valid count.

##### CanvasNet-Fonts-TrueTypeFont-CffFlex: Flex Shortcut Operators and `dotsection`

**Tests**: `CffCharstringInterpreter_HFlex_ProducesTwoCubicBeziersWithHandComputedCoordinates`,
`CffCharstringInterpreter_HFlex_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_HFlex_WrongOperandCount_OneOver_ThrowsInvalidDataException`,
`CffCharstringInterpreter_Flex_ProducesTwoCubicBeziersWithHandComputedCoordinates`,
`CffCharstringInterpreter_Flex_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_Flex_WrongOperandCount_OneOver_ThrowsInvalidDataException`,
`CffCharstringInterpreter_HFlex1_ProducesTwoCubicBeziersWithHandComputedCoordinates`,
`CffCharstringInterpreter_HFlex1_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_HFlex1_WrongOperandCount_OneOver_ThrowsInvalidDataException`,
`CffCharstringInterpreter_Flex1_DxDominant_ProducesHandComputedCoordinates`,
`CffCharstringInterpreter_Flex1_DyDominant_ProducesHandComputedCoordinates`,
`CffCharstringInterpreter_Flex1_WrongOperandCount_OneShort_ThrowsInvalidDataException`,
`CffCharstringInterpreter_Flex1_WrongOperandCount_OneOver_ThrowsInvalidDataException`,
`CffCharstringInterpreter_DotSection_IsNoOp_MatchesSameCharstringWithoutDotSection`

Verifies the two-byte flex escape operators `hflex`/`flex`/`hflex1`/`flex1` each produce exactly
two cubic Bezier segments at hand-computed coordinates - including `hflex1`/`flex1`'s "force the
final point back onto the starting axis" semantics rather than naive running-delta accumulation -
and that each rejects an operand count other than its own exact required count (one short of and
one over); and that the deprecated `dotsection` escape operator (`12 0`) is a pure no-op, verified
by asserting it produces a path identical to the same charstring with its `dotsection` bytes
omitted.

##### CanvasNet-Fonts-TrueTypeFont-CffSubroutines: Subroutine Calls, Bias, Recursion, and Depth Bounding

**Tests**: `CffCharstringInterpreter_CallSubr_AppliesBias_AndReturns`,
`CffCharstringInterpreter_CallGSubr_AppliesGlobalBias`,
`CffCharstringInterpreter_CallSubr_OutOfRangeIndex_ThrowsInvalidDataException`,
`CffCharstringInterpreter_CallSubr_ExceedsMaxDepth_ThrowsInvalidDataException`,
`CffTable_Parse_WithLocalSubrs_DecodesGlyphUsingCallSubr`,
`CffTable_Parse_WithGlobalSubrs_DecodesGlyphUsingCallGSubr`

Verifies `callsubr`/`callgsubr` apply the correct bias and resume via `return`, that an
out-of-range (post-bias) subroutine index is rejected, that a self-recursive subroutine chain is
rejected once it exceeds the maximum call depth, and that `CffTable` correctly wires both local
and global subroutine INDEXes through to the interpreter end to end.

##### CanvasNet-Fonts-TrueTypeFont-CffEndChar: `endchar` and Unexpected Operand Counts

**Tests**: `CffCharstringInterpreter_EmptyCharstring_ProducesEmptyPath`,
`CffCharstringInterpreter_EndChar_UnexpectedOperandCount_ThrowsInvalidDataException`

Verifies a charstring consisting only of `endchar` decodes as an empty path, and that an
unexpected leftover operand count (other than zero or the legacy seac-style 4-operand form,
verified separately below) is rejected with `InvalidDataException`.

##### CanvasNet-Fonts-TrueTypeFont-CffSeacComposition: Seac-Style Accent Composition

**Tests**: `CffCharstringInterpreter_EndChar_SeacStyleFourOperands_NoResolverSupplied_ThrowsInvalidDataException`,
`CffCharstringInterpreter_EndChar_SeacStyleFourOperands_WithResolver_ComposesBaseAndTranslatedAccent`,
`CffCharstringInterpreter_EndChar_SeacStyleFourOperands_ResolverOmittedOnComponentDecode_ThrowsInvalidDataException`,
`CffTable_GetGlyphOutline_SeacStyleEndChar_ComposesBaseAndTranslatedAccent`,
`CffTable_GetAdvanceWidth_SeacStyleEndChar_UsesOwnWidthNotComponentWidths`,
`CffTable_GetGlyphOutline_SeacStyleEndChar_UndefinedStandardEncodingCode_ThrowsInvalidDataException`,
`CffTable_GetGlyphOutline_SeacStyleEndChar_GlyphNameNotInCharset_ThrowsInvalidDataException`,
`CffTable_GetGlyphOutline_SeacStyleEndChar_ComponentItselfSeacStyle_ThrowsInvalidDataException`,
`CffStandardEncoding_CodeToGlyphName_HasExpectedLength`,
`CffStandardEncoding_CodeToGlyphName_SpotChecksKnownCodes`,
`CffStandardEncoding_CodeToGlyphName_UndefinedControlCodesAreNull`

Verifies the legacy 4-operand seac-style `endchar` form is rejected with `InvalidDataException`
when no resolver callback is supplied (including when a seac component glyph is itself defined
using the seac-style form - nested seac composition is not supported); that, with a resolver
supplied, the base and (translated) accent outlines are correctly composed; that `CffTable` wires
its own `CffStandardEncoding`-backed resolver through to the interpreter end to end, correctly
resolving the composite's outline and advance width (the latter independent of the base/accent
glyphs' own widths); that an undefined StandardEncoding code and a resolved glyph name absent from
the font's charset are both rejected with `InvalidDataException`; and that `CffStandardEncoding`'s
code-to-glyph-name table has the expected 256-entry length, correctly maps a handful of known
codes, and leaves undefined control codes as `null`.

##### CanvasNet-Fonts-TrueTypeFont-CffUnsupportedOperatorRejection: Unsupported Operators, Escapes, and Truncation

**Tests**: `CffCharstringInterpreter_UnsupportedOperator_ThrowsInvalidDataException`,
`CffCharstringInterpreter_UnsupportedEscapeOperator_ThrowsInvalidDataException`,
`CffCharstringInterpreter_FixedPointOperand_DecodesCorrectly`,
`CffCharstringInterpreter_TruncatedCharstring_ThrowsInvalidDataException`

Verifies an operator outside the supported set and a genuinely unsupported two-byte escape
operator are both rejected, that a 16.16 fixed-point operand decodes to the correct value, and
that a charstring
truncated before its declared operand/operator data is fully read is rejected.

##### CanvasNet-Fonts-TrueTypeFont-GetFaceCountPlainSfnt: `GetFaceCount` Reports 1 for an Ordinary SFNT Font

**Tests**: `TrueTypeFont_GetFaceCount_PlainSfnt_ReturnsOne`

Calls `GetFaceCount` against a synthetic ordinary (non-`ttcf`) font and asserts it returns `1`.

##### CanvasNet-Fonts-TrueTypeFont-GetFaceCountTtc: `GetFaceCount` Reports a `ttcf` Container's True Face Count

**Tests**: `TrueTypeFont_GetFaceCount_TtcContainer_ReturnsFaceCount`,
`TrueTypeFont_GetFaceCount_Path_ReadsFromFile`,
`SfntContainer_TryReadTtcHeader_WellFormedContainer_ReturnsFaceOffsets`,
`SfntContainer_TryReadTtcHeader_EachFaceIndependentlyParsable`

Verifies `GetFaceCount` against both a stream and a file path returns the container's own
declared face count, and that `TryReadTtcHeader` correctly resolves each face's own offset such
that each face can be parsed independently.

##### CanvasNet-Fonts-TrueTypeFont-LoadTtcDefaultsFaceZero: `Load` Defaults to Face 0 for a `ttcf` File

**Tests**: `TrueTypeFont_Load_TtcContainer_DefaultsToFaceZero`,
`SfntContainer_TryReadTtcHeader_NonTtcFont_ReturnsFalse`

Verifies `Load(Stream)`/`Load(string)` against a synthetic 2-face `ttcf` container transparently
resolves and loads face 0, and that `TryReadTtcHeader` returns `false` (not an exception) for a
file that is not `ttcf`-tagged.

##### CanvasNet-Fonts-TrueTypeFont-LoadTtcExplicitFaceIndex: Explicit Face Selection via `Load(..., int)`

**Tests**: `TrueTypeFont_Load_TtcContainer_ExplicitFaceIndex_SelectsThatFace`,
`TrueTypeFont_Load_TtcContainer_Path_FaceIndex_ReadsFromFile`,
`TrueTypeFont_RealTtcContainer_FaceOne_RendersCffGlyphOutlineAsVisibleInk`,
`TrueTypeFont_RealTtcContainer_FaceZero_RendersGlyfGlyphOutlineAsVisibleInk`

Verifies both new overloads (stream and path) correctly select and load a non-default face of a
synthetic `ttcf` container, then confirms the same explicit-selection behavior against the real,
locally-assembled two-face `.ttc` fixture for both its glyf-flavored and CFF-flavored faces.

##### CanvasNet-Fonts-TrueTypeFont-RejectFaceIndexOutOfRange: Out-of-Range Face Indices Are Rejected

**Tests**: `TrueTypeFont_Load_TtcContainer_FaceIndexOutOfRange_ThrowsArgumentOutOfRangeException`,
`TrueTypeFont_Load_NonTtcFont_FaceIndexOne_ThrowsArgumentOutOfRangeException`,
`TrueTypeFont_Load_NegativeFaceIndex_ThrowsArgumentOutOfRangeException`

Verifies a face index at or beyond a `ttcf` container's declared face count, a non-zero face
index against an ordinary (non-`ttcf`) font, and a negative face index are all rejected with
`ArgumentOutOfRangeException`.

##### CanvasNet-Fonts-TrueTypeFont-FaceIndexZeroBehavesAsLoad: `faceIndex = 0` Matches Plain `Load` for Non-Collection Fonts

**Tests**: `TrueTypeFont_Load_NonTtcFont_FaceIndexZero_BehavesIdenticallyToLoad`

Loads the same ordinary (non-`ttcf`) synthetic font through both `Load(Stream)` and
`Load(Stream, 0)` and asserts the two results expose identical metrics and outlines.

##### CanvasNet-Fonts-TrueTypeFont-RejectMalformedTtcHeader: Malformed `ttcf` Headers Are Rejected

**Tests**: `TrueTypeFont_Load_MalformedTtcHeader_ThrowsInvalidDataException`,
`SfntContainer_TryReadTtcHeader_TruncatedHeader_ThrowsInvalidDataException`,
`SfntContainer_TryReadTtcHeader_ZeroFonts_ThrowsInvalidDataException`,
`SfntContainer_TryReadTtcHeader_TruncatedFaceOffsetTable_ThrowsInvalidDataException`

Verifies a truncated `ttcf` header, a header declaring zero fonts, and a truncated per-face
offset table are all rejected with `InvalidDataException`, both through the public `Load` path
and directly against `TryReadTtcHeader`.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoResolvesTypographicNames: Typographic Names (16/17) Are Preferred When Present

**Tests**: `TrueTypeFont_GetNameInfo_TypographicNamesPresent_PrefersNameId16And17`

Builds a synthetic font whose `name` table carries both the standard (`1`/`2`) and typographic
(`16`/`17`) family/subfamily records with distinct values, and asserts `GetNameInfo()` resolves
`FamilyName`/`SubfamilyName` from the typographic records.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoFallsBackToStandardNames: Standard Names Resolve Without Typographic Records

**Tests**: `TrueTypeFont_GetNameInfo_TypographicNamesAbsent_FallsBackToNameId1And2`

Builds a synthetic font whose `name` table carries only the standard (`1`/`2`) family/subfamily
records, and asserts `GetNameInfo()` still resolves `FamilyName`/`SubfamilyName` from them.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoPlatformPreference: Windows-Platform Records Are Preferred Over Macintosh

**Tests**: `TrueTypeFont_GetNameInfo_WindowsAndMacintoshRecordsPresent_PrefersWindowsRecord`

Builds a synthetic font whose `name` table carries both a Windows-platform and a
Macintosh-platform record for the same nameID with distinct values, and asserts `GetNameInfo()`
resolves from the Windows-platform record.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoMacintoshFallback: Macintosh Records Resolve Without a Windows Record

**Tests**: `TrueTypeFont_GetNameInfo_OnlyMacintoshRecordPresent_ResolvesFromMacintoshRecord`

Builds a synthetic font whose `name` table carries only a Macintosh-platform (Mac Roman) record,
and asserts `GetNameInfo()` correctly decodes and resolves its string.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoMissingRecordTolerant: A Single Missing Record Does Not Affect the Others

**Tests**: `TrueTypeFont_GetNameInfo_PostScriptNameRecordMissing_ReturnsNullPostScriptName`

Builds a synthetic font whose `name` table omits the PostScript name (nameID `6`) record, and
asserts `GetNameInfo().PostScriptName` is `null` while `FamilyName`/`SubfamilyName`/`FullName`
still resolve normally.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoNoNameTableTolerant: A Completely Absent `name` Table Yields an All-Null Result

**Tests**: `TrueTypeFont_GetNameInfo_NoNameTable_ReturnsAllNullFontNameInfo`

Builds a synthetic font with no `name` table at all, and asserts `GetNameInfo()` returns a
`FontNameInfo` with every member `null` rather than throwing.

##### CanvasNet-Fonts-TrueTypeFont-GetNameInfoMalformedRecordTolerant: A Malformed Record Is Ignored, Not the Table

**Tests**: `TrueTypeFont_GetNameInfo_MalformedNameRecord_IgnoresRecordWithoutThrowing`

Builds a synthetic font whose `name` table contains one record whose declared string
offset/length falls outside the table's own bounds alongside other well-formed records, and
asserts `GetNameInfo()` ignores only the malformed record (without throwing) while the other
records still resolve.

##### CanvasNet-Fonts-TrueTypeFont-StyleFromOs2FsSelection: `OS/2.fsSelection` Bold/Italic Bits Drive `IsBold`/`IsItalic`

**Tests**: `TrueTypeFont_IsBold_Os2FsSelectionBoldBitSet_ReturnsTrue`,
`TrueTypeFont_IsItalic_Os2FsSelectionItalicBitSet_ReturnsTrue`

Builds synthetic fonts with `OS/2.fsSelection`'s bold bit (bit 5) and italic bit (bit 0) each set
independently, and asserts `IsBold`/`IsItalic` reflect them.

##### CanvasNet-Fonts-TrueTypeFont-StyleFromOs2WeightClass: `OS/2.usWeightClass >= 600` Also Implies `IsBold`

**Tests**: `TrueTypeFont_IsBold_Os2WeightClassAtLeast600_ReturnsTrue`

Builds a synthetic font with `OS/2.usWeightClass` set to a Semibold-or-heavier value and
`fsSelection`'s bold bit clear, and asserts `IsBold` is still `true`.

##### CanvasNet-Fonts-TrueTypeFont-StyleFromPostItalicAngle: A Non-Zero `post.italicAngle` Also Implies `IsItalic`

**Tests**: `TrueTypeFont_IsItalic_PostItalicAngleNonZero_ReturnsTrue`

Builds a synthetic font with a non-zero `post.italicAngle` and no other italic indicator set, and
asserts `IsItalic` is still `true`.

##### CanvasNet-Fonts-TrueTypeFont-StyleFallsBackToMacStyleWhenOs2Absent: `head.macStyle` Drives Style When `OS/2` Is Absent

**Tests**: `TrueTypeFont_Style_Os2TableAbsent_FallsBackToHeadMacStyle`

Builds a synthetic font with no `OS/2` table and `head.macStyle`'s bold/italic bits set, and
asserts `IsBold`/`IsItalic` are both `true`, proving the mandatory `head` table alone is
sufficient when the optional `OS/2` table is missing.

##### CanvasNet-Fonts-TrueTypeFont-IsFixedPitchFromPostTable: `post.isFixedPitch` Drives `IsFixedPitch`

**Tests**: `TrueTypeFont_IsFixedPitch_PostIsFixedPitchNonZero_ReturnsTrue`

Builds a synthetic font with `post.isFixedPitch` set non-zero, and asserts `IsFixedPitch` is
`true`.

##### CanvasNet-Fonts-TrueTypeFont-IsFixedPitchAbsentPostTableTolerant: Defaults to `false` Without a `post` Table

**Tests**: `TrueTypeFont_IsFixedPitch_PostTableAbsent_ReturnsFalseWithoutThrowing`

Builds a synthetic font with no `post` table at all, and asserts `IsFixedPitch` is `false`
without throwing.

##### CanvasNet-Fonts-TrueTypeFont-LoadType1FromSegments: Type 1 Font Programs Load From Caller-Supplied Byte Segments

**Tests**: `TrueTypeFont_LoadType1_WellFormedProgram_ExposesGlyphsAndMetrics`,
`TrueTypeFont_LoadType1_NegativeLength1_ThrowsInvalidDataException`,
`TrueTypeFont_LoadType1_Length2ExceedsStreamBounds_ThrowsInvalidDataException`,
`TrueTypeFont_LoadType1_NullStream_ThrowsArgumentNullException`,
`TrueTypeFont_LoadType1_NullEncoding_ThrowsArgumentNullException`,
`Type1Table_Parse_WellFormedFont_ExposesGlyphCountAndOutlines`,
`Type1Table_Parse_NotdefNotFirst_ReindexesToGlyphZero`,
`Type1Table_GetAdvanceWidth_ReturnsHsbwWidth`,
`Type1Table_TryGetGlyphIndex_UnknownName_ReturnsFalse`,
`Type1Table_GetGlyphOutline_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`,
`Type1Table_Parse_WithLocalSubrs_DecodesGlyphUsingCallSubr`,
`Type1Table_Parse_VariousProcNameTokens_ScannerIsProcedureNameAgnostic`,
`Type1Table_Parse_CustomLenIv_DecodesCorrectly`,
`Type1Table_Parse_SeacOperator_ThrowsInvalidDataException`,
`Type1Table_Parse_MissingCharStrings_ThrowsInvalidDataException`,
`Type1Table_Parse_NegativeLength_ThrowsInvalidDataException`,
`Type1Table_Parse_LengthsExceedFileBounds_ThrowsInvalidDataException`,
`Type1Table_Parse_EmptyCharStrings_ThrowsInvalidDataException`,
`Type1Table_Parse_TrailingBoilerplateAfterCharStringsEnd_DoesNotMisparse`,
`Type1CharstringInterpreter_EmptyCharstring_ProducesEmptyPathAndZeroWidth`,
`Type1CharstringInterpreter_Hsbw_CapturesWidthAndSideBearing`,
`Type1CharstringInterpreter_Sbw_CapturesWidthAndBothSideBearings`,
`Type1CharstringInterpreter_RLineTo_HLineTo_VLineTo_ProduceLines`,
`Type1CharstringInterpreter_RRCurveTo_ProducesCubicBezier`,
`Type1CharstringInterpreter_VhCurveTo_HvCurveTo_AlternateStartTangent`,
`Type1CharstringInterpreter_ClosePath_ClosesSubpath`,
`Type1CharstringInterpreter_HStem_VStem_Hstem3_Vstem3_Dotsection_ConsumedWithoutError`,
`Type1CharstringInterpreter_CallSubr_Return_NoBias_DirectIndex`,
`Type1CharstringInterpreter_CallSubr_OutOfRangeIndex_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_CallSubr_ExceedsMaxDepth_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_Div_ResultFeedsSubsequentOperator`,
`Type1CharstringInterpreter_Flex_EndToEnd_ProducesTwoRealCubicBeziers`,
`Type1CharstringInterpreter_HintReplacement_OtherSubr3_IsTransparentPassThrough`,
`Type1CharstringInterpreter_Seac_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_UnsupportedOperator_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_UnsupportedEscapeOperator_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_CallOtherSubr_UnsupportedIndex_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_RLineTo_WrongOperandCount_ThrowsInvalidDataException`,
`Type1CharstringInterpreter_FiveByteInteger_DecodesAsPlainInt32`,
`Type1CharstringInterpreter_TruncatedCharstring_ThrowsInvalidDataException`

Exercises `TrueTypeFont.LoadType1` end-to-end against a hand-authored synthetic Type 1 font
program (via `SyntheticFontBuilder.Type1`), asserting `GetGlyphIndex`/`GetGlyphOutline`/
`GetAdvanceWidth` behave equivalently to the glyf/CFF-flavored paths, and that malformed
`length1`/`length2` values and null arguments are rejected as documented. Separately verifies
`Type1Table`'s own `/Subrs`/`/CharStrings` scanner - including its procedure-name-agnostic
behavior across varied `RD`/`ND`/`NP`/`-|`/`|-` token fixtures, `.notdef`-reindexing,
custom-`lenIV` handling, and its fail-closed structural-malformation paths - and
`Type1CharstringInterpreter`'s full opcode set in isolation, including a geometry-asserting
end-to-end flex test that checks actual `Path` point data (not merely the absence of an
exception), a hint-replacement pass-through test, `seac` rejection, and subroutine call-depth
bounding. Also verifies that the `/CharStrings` scanner correctly stops at that dictionary's own
matching closing `end` keyword rather than misparsing trailing font-closing PostScript
boilerplate (`readonly put`/`noaccess put`/`definefont`/`currentfile closefile`) that some
real-world Type 1 producers emit immediately afterward as further glyph entries.

##### CanvasNet-Fonts-TrueTypeFont-LoadType1Outlines: Standalone `.pfb`/`.pfa` Type 1 Files Auto-Detect Through `Load`

**Tests**: `TrueTypeFont_Load_StandalonePfbFile_AutoDetectsAndRoundTrips`,
`TrueTypeFont_Load_StandalonePfaFile_AutoDetectsAndRoundTrips`,
`TrueTypeFont_Load_StandalonePfbFile_Path_ReadsFromFile`,
`TrueTypeFont_GetFaceCount_StandaloneType1File_ReturnsOne`,
`TrueTypeFont_Load_StandaloneType1File_FaceIndexOne_ThrowsArgumentOutOfRangeException`,
`TrueTypeFont_Load_NonType1NonSfntGarbage_ThrowsInvalidDataException`,
`Type1PfbReader_TrySniff_RecognizesPfbHeader`,
`Type1PfbReader_TrySniff_NonPfbData_ReturnsFalse`,
`Type1PfbReader_Read_TwoSegmentFile_ReassemblesLength1AndLength2`,
`Type1PfbReader_Read_MultiSegmentFile_ConcatenatesLikeTypedSegments`,
`Type1PfbReader_Read_TrailingAsciiSegment_DiscardsItAsTrailer`,
`Type1PfbReader_Read_NoBinarySegment_ThrowsInvalidDataException`,
`Type1PfbReader_Read_MissingEndOfFileMarker_ThrowsInvalidDataException`,
`Type1PfbReader_Read_UnrecognizedSegmentType_ThrowsInvalidDataException`,
`Type1PfbReader_Read_TruncatedHeader_ThrowsInvalidDataException`,
`Type1PfbReader_Read_TruncatedPayload_ThrowsInvalidDataException`,
`Type1PfbReader_Read_MissingSegmentMarkerByte_ThrowsInvalidDataException`,
`Type1PfaReader_TrySniff_RecognizesPercentBangPrefix`,
`Type1PfaReader_TrySniff_NonPfaData_ReturnsFalse`,
`Type1PfaReader_Read_WellFormedFile_ReassemblesLength1AndLength2`,
`Type1PfaReader_Read_MultiLineWhitespaceWrappedHex_ToleratesEmbeddedWhitespace`,
`Type1PfaReader_Read_TrailingZeroPadding_IsTrimmed`,
`Type1PfaReader_Read_MissingEexecKeyword_ThrowsInvalidDataException`,
`Type1PfaReader_Read_OddHexDigitCount_ThrowsInvalidDataException`,
`Type1PfaReader_Read_NonHexByteBeforeAnyHexDigit_ThrowsInvalidDataException`,
`Type1PfaReader_Read_ZeroBytesAfterTrim_ThrowsInvalidDataException`,
`Type1PfaReader_Read_LowercaseAndUppercaseHexDigits_AreBothAccepted`

Builds standalone `.pfb` and `.pfa` re-serializations of the same synthetic Type 1 font program
(via `SyntheticFontBuilder.Type1Pfb`/`Type1Pfa`) and asserts `Load(Stream)`/`Load(string)`
auto-detect and round-trip them using the built-in `Type1StandardGlyphNames` default encoding,
that `GetFaceCount` reports `1` and `Load(Stream, 1)` is rejected with
`ArgumentOutOfRangeException`, and that data recognized as neither SFNT/OpenType nor `.pfb`/`.pfa`
still throws `InvalidDataException`. Separately verifies `Type1PfbReader`'s generic
type-tagged-segment loop (two-segment, multi-segment, and trailing-ASCII-trailer cases) and
`Type1PfaReader`'s whitespace-tolerant hex decoding (well-formed, multi-line/whitespace-wrapped,
and trailing-zero-padding cases), and both readers' fail-closed behavior on truncated headers/
payloads, unrecognized segment types, a missing binary segment or end-of-file marker, a missing
`eexec` keyword, an odd hex-digit count, a non-hex byte before any hex digit, and zero decoded
bytes after trim.

##### CanvasNet-Fonts-TrueTypeFont-CffCharsetResolution: CFF Charset Resolves Glyph Names to Glyph Indices

**Tests**: `CffTable_Parse_AbsentCharset_DefaultsToIsoAdobeAndResolvesByName`,
`CffTable_Parse_PredefinedIsoAdobeCharsetId_MatchesAbsentCharset`,
`CffTable_Parse_PredefinedExpertOrExpertSubsetCharset_NeverResolvesByNameButNeverThrows`,
`CffTable_Parse_CustomCharsetFormat0_ResolvesEachGlyphBySid`,
`CffTable_Parse_CustomCharsetFormat1_ResolvesRangesOfGlyphs`,
`CffTable_Parse_CustomCharsetFormat2_ResolvesRangesWithTwoByteNLeft`,
`CffTable_Parse_CustomCharsetUnsupportedFormat_ThrowsInvalidDataException`,
`CffTable_Parse_CharsetOffsetOutOfBounds_ThrowsInvalidDataException`,
`CffTable_Parse_CustomStringIndex_ResolvesCustomGlyphNameBySid`,
`CffTable_StandardStrings_HasExpectedLengthAndSpotCheckedEntries`

Verifies that an absent `charset` operator and an explicit predefined-ISOAdobe (`0`) value both
resolve every glyph's SID to its own glyph index and that `TryGetGlyphIndex` then resolves a
Standard-Strings glyph name (for example `"space"`) back to the correct glyph index; that the
predefined Expert (`1`) and ExpertSubset (`2`) charset IDs are recognized but deliberately never
resolve any glyph name - `TryGetGlyphIndex` returns `false`, never throws - a documented scope
boundary covered explicitly rather than left implicit; that a custom charset table (a byte offset
greater than `2`) correctly decodes format 0 (flat 2-byte SID array), format 1 (2-byte-first-SID/
1-byte-count ranges), and format 2 (2-byte-first-SID/2-byte-count ranges) into the expected
per-glyph SID assignments; that an unrecognized format byte and an out-of-bounds charset offset
are both rejected with `InvalidDataException`; that a glyph name resolved via the font's own
String INDEX (SID 391 and above, not only the built-in Standard Strings) round-trips correctly;
and that the transcribed 391-entry CFF Standard Strings table itself has the documented length and
spot-checked entries (including SID `0` = `.notdef`, SID `1` = `space`, and SID `34` = `A`) match
Adobe Technical Note #5176 Appendix A.

##### CanvasNet-Fonts-TrueTypeFont-CffWidthResolution: CFF Glyph Advance Widths Resolve Through the Type 2 Width Convention

**Tests**: `CffCharstringInterpreter_Decode_RMoveToWithLeadingWidth_ReturnsNominalWidthXPlusDelta`,
`CffCharstringInterpreter_Decode_NoLeadingWidth_ReturnsDefaultWidthX`,
`CffCharstringInterpreter_Decode_HStemWithLeadingWidth_ReturnsNominalWidthXPlusDelta`,
`CffCharstringInterpreter_Decode_EndCharWithLeadingWidth_ReturnsNominalWidthXPlusDelta`,
`CffCharstringInterpreter_Decode_DefaultWidthParameters_AreBothZero`,
`CffTable_GetAdvanceWidth_NoWidthOperand_UsesDefaultWidthX`,
`CffTable_GetAdvanceWidth_WithWidthOperand_UsesNominalWidthXPlusDelta`,
`CffTable_GetAdvanceWidth_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`

Exercises `CffCharstringInterpreter.Decode`'s width-returning overload directly across every
stack-clearing operator that can carry the optional leading width operand (`rmoveto`, `hstem`,
`endchar`), asserting the returned width is `nominalWidthX` plus the leading operand's value when
present, and `defaultWidthX` when absent, including the case where both Private DICT width
operators are left at their own zero defaults. Separately verifies `CffTable.GetAdvanceWidth`
correctly surfaces that same per-glyph resolved width end to end for both the with-operand and
without-operand cases, and rejects an out-of-range glyph index with
`ArgumentOutOfRangeException`.

##### CanvasNet-Fonts-TrueTypeFont-LoadType1C: Bare Type1C/CFF Font Programs Load Through a Dedicated Entry Point

**Tests**: `TrueTypeFont_LoadType1C_WellFormedCff_ExposesGlyphsAndMetrics`,
`TrueTypeFont_LoadType1C_NullStream_ThrowsArgumentNullException`,
`TrueTypeFont_LoadType1C_NullEncoding_ThrowsArgumentNullException`,
`TrueTypeFont_LoadType1C_MalformedCff_ThrowsInvalidDataException`,
`TrueTypeFont_LoadType1C_CidKeyedCff_ThrowsInvalidDataException`

Exercises `TrueTypeFont.LoadType1C` end-to-end against a hand-authored synthetic bare-CFF program
(via `SyntheticFontBuilder.Cff`, with a custom charset mapping glyph names to glyph indices and a
caller-supplied codepoint-to-glyph-name encoding), asserting `GlyphCount`/`GetGlyphIndex`/
`GetGlyphOutline`/`GetAdvanceWidth` behave equivalently to every other supported outline flavor
under a fixed 1000-unit em square; and asserts null `stream`/`codepointToGlyphName` arguments, a
structurally malformed CFF table, and a CID-keyed (`ROS`-declaring) CFF program are all rejected
as documented (the last two both surfacing as `InvalidDataException`, the CID-keyed case via
`CffTable.Parse`'s own existing `ROS` rejection, not a second, separately-implemented check).
