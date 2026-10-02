# System Verification Design

<!-- cspell:ignore Zapf Noto -->

This document describes the system-level verification strategy for CanvasNetPdf.

## Verification Approach

The CanvasNetPdf system is verified through system-level integration tests that exercise the
library as a whole from the perspective of a consumer. Tests instantiate the library using its
public API and assert on observable outputs, without relying on knowledge of internal
implementation details. No mocking or stubbing is required at the system level — the entire
integrated system is exercised as it would be used by a real caller.

System tests reside in `PdfSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Pdf.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network or service interfaces requiring simulation - no HTTP calls,
databases, or remote services are involved. It does support local file-path-based I/O through its
public API (`Open(string path)` reads a PDF document from a local file path), which system tests
exercise against the hand-authored fixture files in `PdfFixtures/`. System tests call the public
API directly with controlled inputs and verify returned values and thrown exceptions.

## System-Level Test Scenarios

### Integration: Pdf Open Returns Expected Page Count

**Test**: `CanvasNetPdf_SystemIntegration_PdfOpen_ReturnsExpectedPageCount`

Exercises end-to-end system behavior for `Open`/`PageCount`/`GetPageInfo`/`Render`: opens a
multi-page fixture once through the public API, asserts `PageCount`, then calls `GetPageInfo` for
two different pages and `Render` for two different pages against the *same* opened instance.
Confirms the "parse once, reused across every subsequent call" property — one no stateless
design could express — and serves as the shared platform-proof test referenced by every
`CanvasNetPdf-Platform-*` requirement.

### Integration: Pdf Get Page Info Reports Rotated Dimensions

**Test**: `CanvasNetPdf_SystemIntegration_PdfGetPageInfo_ReportsRotatedDimensions`

Exercises end-to-end system behavior for rotation-adjusted page sizing: opens a fixture
containing a page with a non-zero `/Rotate` value through the public API, then calls
`GetPageInfo`. Asserts the reported `Width`/`Height` are swapped relative to the page's raw
`/MediaBox` and `Rotation` reports the effective normalized value, confirming page-tree
inheritance and rotation normalization integrate correctly with the public API.

### Integration: Pdf Render Returns Blank Sized Surface

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ReturnsBlankSizedSurface`

Exercises end-to-end system behavior for `Render`: calls the public `Render` API for a valid page
index and caller-chosen size, against a fixture with no `/Contents`. Asserts the returned
`Surface` has exactly the requested dimensions and every pixel is the default (fully transparent)
value, confirming the documented "a page with no content renders blank" behavior is honored at
the system's own public entry point.

### Integration: Pdf Render Filled Rectangle And Stroked Line Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_FilledRectangleAndStrokedLine_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 2 content-stream interpreter: calls the public
`Render` API against a hand-authored fixture containing a filled rectangle and a stroked vertical
line. Asserts specific opaque-black pixels inside the rectangle and along the stroked line, and
specific fully-transparent pixels away from both, confirming real path geometry is rasterized to
the correct device-pixel positions through the system's own public entry point (not merely that
"something non-blank" was painted).

### Integration: Pdf Render Rotated Page Maps Geometry To Correct Pixel Position

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RotatedPage_MapsGeometryToCorrectPixelPosition`

Exercises end-to-end system behavior for the base CTM's rotation handling: calls the public
`Render` API against a hand-authored, `/Rotate 90` fixture containing a deliberately asymmetric
filled rectangle, at the fixture's own rotation-swapped display size. Asserts the rectangle's
interior is opaque black at its mathematically correct rotated position, and asserts a specific
pixel where a 270-instead-of-90 rotation-sign regression would incorrectly paint it is untouched,
confirming `/MediaBox`/`/Rotate`-to-device-pixel-space correctness through the system's own public
entry point.

### Integration: Pdf Validation Null Null Stream Throws Argument Null Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfValidationNull_NullStreamThrowsArgumentNullException`

Exercises end-to-end system behavior for null-argument validation: calls the public `Open` API
with a null stream. Asserts `ArgumentNullException` is thrown, confirming the documented
validation contract is honored at the system's own public entry point, before any parsing is
attempted.

### Integration: Pdf Dispose Object Disposed Exception After Dispose

**Test**: `CanvasNetPdf_SystemIntegration_PdfDispose_ObjectDisposedExceptionAfterDispose`

Exercises end-to-end system behavior for disposal: opens a fixture through the public API, calls
`Dispose()` twice (confirming idempotency), then calls `PageCount`, `GetPageInfo`, and `Render`.
Asserts each throws `ObjectDisposedException`, confirming the documented disposal contract is
honored across every other public member.

### Integration: Pdf Encrypt Detection Encrypted Trailer Throws Unsupported Image Feature Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfEncryptDetection_EncryptedTrailerThrowsUnsupportedImageFeatureException`

Exercises end-to-end system behavior for `/Encrypt` detection: calls the public `Open` API
against a fixture whose trailer contains an `/Encrypt` key. Asserts
`Codecs.UnsupportedImageFeatureException` is thrown with `Feature == "pdf-encrypted"`, confirming
an encrypted document is rejected rather than silently mis-parsed at the system's own public
entry point.

### Integration: Pdf Open Encrypted Rc4 Correct User Password Decrypts And Renders Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfOpen_EncryptedRc4_CorrectUserPassword_DecryptsAndRendersExpectedPixels`

Exercises end-to-end system behavior for genuinely successful decryption: a synthetic, in-memory
RC4 128-bit (`/V 2`/`/R 3`) encrypted PDF is built with a file key derived from a real, non-empty
user password, then opened through the public `Open(Stream, string?)` API with that correct
password supplied and rendered through the public `Render` API. Asserts the encrypted content
stream's rectangle-fill operator actually painted the expected opaque-black device pixel (and a
pixel outside its footprint remains transparent), confirming the encrypted bytes were genuinely
decrypted to real plaintext content and rendered - not merely that `Open` accepted the password
without throwing.

### Integration: Pdf Render Colored Rectangle Fill Paints Expected Rgb Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ColoredRectangleFill_PaintsExpectedRgbPixels`

Exercises end-to-end system behavior for the Phase 3 device-color operators: calls the public
`Render` API against a hand-authored fixture using `rg` to fill a rectangle opaque red. Asserts a
specific interior pixel is opaque red and a specific exterior pixel remains transparent,
confirming real device color (not merely solid opaque black) is rasterized through the system's
own public entry point.

### Integration: Pdf Render Image X Object Placement Composites Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_ImageXObjectPlacement_CompositesExpectedPixels`

Exercises end-to-end system behavior for the Phase 3 image-XObject pipeline: calls the public
`Render` API against a hand-authored fixture placing a 2x2 `DeviceRGB` `FlateDecode` image
XObject via `cm`/`Do`. Asserts the four composited device pixels match the fixture's known source
image quadrants, and a pixel outside the placed image's device-space footprint remains
transparent, confirming stream decoding, sample-to-color conversion, and unit-square-to-device
compositing all integrate correctly through the system's own public entry point.

### Integration: Pdf Render Form X Object Paints Nested Content Stream

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_FormXObject_PaintsNestedContentStream`

Exercises end-to-end system behavior for the `/Subtype /Form` XObject pipeline: calls the public
`Render` API against a synthetic, in-memory fixture whose page content stream invokes `/Fm0 Do`,
where `/Fm0` is a genuine `/Type /XObject /Subtype /Form` XObject carrying its own nested
rectangle-filling content stream. Asserts the Form's nested content actually painted the expected
opaque-black device pixel (and a pixel outside its footprint remains transparent), confirming the
Form XObject is genuinely executed end-to-end through the system's own public entry point - not
merely that `Do` fails to throw.

### Integration: Pdf Render Embedded True Type Font Text Paints Glyph Strokes Not Counters

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_EmbeddedTrueTypeFontText_PaintsGlyphStrokesNotCounters`

Exercises end-to-end system behavior for the Phase 4 text-rendering pipeline: calls the public
`Render` API against a hand-authored fixture with a real, embedded (`/FontDescriptor/FontFile2`)
copy of the shared `OpenSans-Regular.ttf` production font, drawing `"HO"` at font size 60 with
the default `/WinAnsiEncoding` via `BT`/`Tf`/`Td`/`Tj`/`ET`. Rather than hardcoding font-specific
pixel numbers, the test independently loads the same real font through
`Fonts.TrueTypeFont` and re-derives the expected device-pixel positions of real glyph ink from
the font's own outline/metrics via the documented text-rendering-matrix formula. Asserts a pixel
inside `'H'`'s left stroke is opaque, a pixel at the exact center of `'O'`'s bounding box (its
hollow counter) is transparent, and the canvas's far corners remain transparent, confirming
font-dictionary resolution, `/Encoding` mapping, glyph-outline transformation, and glyph painting
all integrate correctly through the system's own public entry point with a real font file (not a
hand-rolled synthetic one).

### Integration: Render Type0 Composite Font Paints Expected Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderType0CompositeFont_PaintsExpectedGlyphInk`

Exercises end-to-end system behavior for the Phase 9 Type0/Identity-H/CIDFontType2 composite-font
text-rendering pipeline: calls the public `Render` API against a hand-authored fixture with a
real, embedded (descendant `/FontDescriptor/FontFile2`) copy of the shared `OpenSans-Regular.ttf`
production font, a non-identity `/CIDToGIDMap` stream remapping CID 1/2 to the glyph indices of
`'H'`/`'O'`, and an explicit `/W` array declaring their advance widths, drawing the 2-byte
Identity-H codes `0001 0002` at font size 60 via `BT`/`Tf`/`Td`/`Tj`/`ET`. Rather than hardcoding
font-specific pixel numbers, the test independently loads the same real font through
`Fonts.TrueTypeFont` and re-derives the expected device-pixel positions of real glyph ink from
the font's own outline, looked up directly by glyph index (bypassing `cmap` entirely, exactly as
the composite code path does), combined with the fixture's own declared `/W` advance widths
(composite fonts never fall back to the font's own metrics). Asserts a pixel inside `'H'`'s left
stroke is opaque, a pixel at the exact center of `'O'`'s bounding box (its hollow counter) is
transparent, a pixel one full CID-2 advance width past `'O'`'s own origin is transparent, and the
canvas's far corners remain transparent, confirming Type0 font-dictionary resolution,
`/CIDToGIDMap` remapping, `/W`-declared advance widths, 2-byte code decoding, glyph-outline
transformation, and glyph painting all integrate correctly through the system's own public entry
point with a real font file (not a hand-rolled synthetic one).

### Integration: Render Cid Font Type0 Composite Font Paints Expected Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderCidFontType0CompositeFont_PaintsExpectedGlyphInk`

Exercises end-to-end system behavior for the Phase 12 `/CIDFontType0` composite-font text-
rendering pipeline: calls the public `Render` API against a hand-authored, entirely synthetic
fixture whose descendant font's `/FontDescriptor/FontFile3` is a synthetic, non-CID-keyed,
`/OpenType`-wrapped CFF program (built via `SyntheticFontBuilder.Cff` - no third-party font
asset), with no `/CIDToGIDMap` declared (identity CID-to-glyph-index is used unconditionally for
this subtype). The content stream draws the 2-byte Identity-H code `0001` (CID 1, resolving to
GID 1, a filled square glyph) at font size 20 on a 100x100 `/MediaBox`. Asserts the glyph's
expected design-space square is painted as real ink at its correctly transformed device-pixel
position, and the canvas's far corners remain transparent, confirming `/CIDFontType0` resolution
and CFF/Type2-charstring outline decoding integrate correctly through the system's own public
entry point.

### Integration: Render Embedded Type1 Font Paints Expected Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderEmbeddedType1Font_PaintsExpectedGlyphInk`

Exercises end-to-end system behavior for the Phases B/C/D `/Subtype /Type1` simple-font text-
rendering pipeline: calls the public `Render` API against a hand-authored, entirely synthetic
fixture built via `SyntheticFontBuilder.Type1` (no third-party font asset), whose embedded
classic PostScript `/FontDescriptor/FontFile` program's `A` glyph is a filled square, drawn at
font size 20 on a 100x100 `/MediaBox` - deliberately matching the `/CIDFontType0` composite-font
scenario's own glyph design/placement convention. Asserts the glyph's expected design-space
square is painted as real ink at its correctly transformed device-pixel position, and the
canvas's far corners remain transparent, confirming embedded classic Type 1 font-program
resolution and Type 1 charstring outline decoding integrate correctly through the system's own
public entry point.

### Integration: Pdf Render Lzw Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_LzwDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `LZWDecode` content-stream filter: calls the
public `Render` API against a hand-authored fixture whose `/Contents` stream is PDF-variant
LZW-compressed. Asserts the same interior-opaque-red/exterior-transparent pixel pattern as the
uncompressed device-color fixture, confirming the decoded operator text is parsed and rendered
identically to an uncompressed content stream through the system's own public entry point.

### Integration: Pdf Render Ascii85 Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_Ascii85DecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `ASCII85Decode` content-stream filter: calls
the public `Render` API against a hand-authored fixture whose `/Contents` stream is base-85
armored (terminated by `~>`). Asserts the same interior-opaque-red/exterior-transparent pixel
pattern, confirming `ASCII85Decode` decoding integrates correctly through the system's own public
entry point.

### Integration: Pdf Render Ascii Hex Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_AsciiHexDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `ASCIIHexDecode` content-stream filter:
calls the public `Render` API against a hand-authored fixture whose `/Contents` stream is
hex-digit-pair armored (terminated by `>`). Asserts the same interior-opaque-red/
exterior-transparent pixel pattern, confirming `ASCIIHexDecode` decoding integrates correctly
through the system's own public entry point.

### Integration: Pdf Render Run Length Decode Content Stream Paints Expected Pixels

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RunLengthDecodeContentStream_PaintsExpectedPixels`

Exercises end-to-end system behavior for the Phase 7 `RunLengthDecode` content-stream filter:
calls the public `Render` API against a hand-authored fixture whose `/Contents` stream is a
single PackBits-style literal run. Asserts the same interior-opaque-red/exterior-transparent
pixel pattern, confirming `RunLengthDecode` decoding integrates correctly through the system's
own public entry point.

### Integration: Render Standard14 Helvetica Without Embedded Font Paints Visible Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderStandard14HelveticaWithoutEmbeddedFont_PaintsVisibleGlyphInk`

Exercises end-to-end system behavior for the Phase 6 font-fallback substitution path: calls the
public `Render` API against a synthetic, in-memory single-page document declaring a
`/BaseFont /Helvetica` font resource with no embedded `/FontFile2`. Since the actual substitute
font (a matching system font, or the bundled Liberation Sans fallback) genuinely varies across
the Windows/Linux/macOS CI matrix, asserts the strongest platform-independent property: at least
one visibly-painted (non-transparent) pixel appears somewhere on the canvas, confirming the
fallback path genuinely renders a substitute glyph rather than merely not throwing.

### Integration: Render Symbol Font Without Embedded Font Paints Visible Glyph Ink

**Test**: `CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_PaintsVisibleGlyphInk`

Exercises end-to-end system behavior for the Phase 6 font-fallback substitution path: calls the
public `Render` API against a synthetic, in-memory single-page document declaring a
`/BaseFont /Symbol` font resource with no embedded `/FontFile2` and no `/FontDescriptor` entries
at all (PDF 32000-1 §9.6.2.2 permits an entirely absent/empty descriptor). Since Symbol/
ZapfDingbats no longer fail closed and instead resolve via the bundled Noto substitute font
union, asserts the same platform-independent property as the Standard-14 Helvetica fallback test
above: at least one visibly-painted (non-transparent) pixel appears somewhere on the canvas,
confirming the Noto substitute glyph was actually rendered through the system's own public entry
point.

### Integration: Noto Sans Regular Bundled Font Resolves Symbol Alpha To Nonzero Glyph Index

**Test**: `CanvasNetPdf_SystemIntegration_NotoSansRegularBundledFont_ResolvesSymbolAlphaToNonzeroGlyphIndex`

Proves, via the full production round-trip (`SystemFontCatalog.LoadBundledFallback`'s
embedded-resource-loading path, loading `NotoSans-Regular.ttf` the same way
`PdfDocument.FontFallback.cs`'s `ResolveSymbolicNotoFallback` does for the `Symbol` substitute's
primary font) that Symbol code `0x61` ('alpha', per PDF 32000-1 Appendix D's Symbol encoding,
mapped to Unicode U+03B1) resolves to a nonzero, valid glyph index, confirming the bundled
substitute font genuinely carries a real Greek alpha glyph, not merely that the pipeline
declines to throw.

### Integration: Render Other Symbolic Font Without Embedded Font Throws Unsupported Image Feature Exception

**Test**: `CanvasNetPdf_SystemIntegration_RenderOtherSymbolicFontWithoutEmbeddedFont_ThrowsUnsupportedImageFeatureException`

Regression guard exercising end-to-end system behavior for the Phase 6 font-fallback fail-closed
boundary: calls the public `Render` API against a synthetic, in-memory single-page document
declaring a non-Symbol/ZapfDingbats symbolic font resource (`/FontDescriptor/Flags` declaring the
`Symbolic` bit without also declaring `Nonsymbolic`) with no embedded `/FontFile2`. Asserts
`Codecs.UnsupportedImageFeatureException` is thrown with `Feature == "pdf-font-symbolic-not-embedded"`,
confirming the new Symbol/ZapfDingbats Noto-substitution path did not loosen the fail-closed
policy for any other symbolic font through the system's own public entry point.

### Integration: Pdf Validation Empty Path Empty Path Throws Argument Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfValidationEmptyPath_EmptyPathThrowsArgumentException`

Exercises end-to-end system behavior for empty-path-argument validation: calls the public
`Open(string)` API with `string.Empty`. Asserts `ArgumentException` is thrown, confirming the
documented validation contract is honored at the system's own public entry point, before any
file access is attempted.

### Integration: Pdf Unsupported Format Validation Malformed Content Stream Throws Invalid Data Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfUnsupportedFormatValidation_MalformedContentStreamThrowsInvalidDataException`

Exercises end-to-end system behavior for content-stream-level malformed-data validation: calls
the public `Render` API against a hand-authored fixture whose otherwise well-formed
document/xref/page-tree structure has a `/Contents` stream containing a lexically malformed
operator (a `re` given only 2 of its 4 required operands). Asserts `InvalidDataException` is
thrown, confirming a content-stream-level malformation (distinct from the structural
malformations already covered by `malformed-startxref.pdf`/`cyclic-page-tree.pdf`) is rejected at
the system's own public entry point.

### Integration: Pdf Get Page Info Validation Out Of Range Page Index Throws Argument Out Of Range Exception

**Test**: `CanvasNetPdf_SystemIntegration_PdfGetPageInfoValidation_OutOfRangePageIndexThrowsArgumentOutOfRangeException`

Exercises end-to-end system behavior for out-of-range-page-index validation: calls the public
`GetPageInfo` API against a single-page fixture with an out-of-range page index. Asserts
`ArgumentOutOfRangeException` is thrown, confirming the documented validation contract is honored
at the system's own public entry point.

### Integration: Axial Shading Pattern Fill Paints Visibly Varying Colors

**Test**: `CanvasNetPdf_SystemIntegration_AxialShadingPatternFill_PaintsVisiblyVaryingColors`

Exercises end-to-end system behavior for the Phase 14 `/Pattern` color-space axial-shading
pipeline: calls the public `Render` API against a synthetic, in-memory single-page PDF (no binary
fixture) declaring a `/Pattern`-color-space fill driven by a `/ShadingType 2` shading with a
`/FunctionType 2` (exponential interpolation) function interpolating black to white along its
axis. Asserts the painted gradient is near-black at the axis's start coordinate and near-white at
its end coordinate, confirming `/Pattern` color-space resolution, axial-shading coordinate
mapping, and function evaluation all integrate correctly through the system's own public entry
point (not merely a single solid fill color).

### Integration: Colored Tiling Pattern Fill Paints Repeating Tile Colors

**Test**: `CanvasNetPdf_SystemIntegration_ColoredTilingPatternFill_PaintsRepeatingTileColors`

Exercises end-to-end system behavior for the Phase 14 `/Pattern` color-space colored-tiling
pipeline: calls the public `Render` API against a synthetic, in-memory single-page PDF (no binary
fixture) declaring a `/Pattern`-color-space fill driven by a `/PatternType 1`/`/PaintType 1`
10x10 pattern cell (left half opaque red, right half opaque blue) tiled across a 100x100 fill.
Asserts both tile colors appear at multiple repeated tile offsets at their exactly expected
device pixels, confirming tiling-pattern cell execution, `/XStep`/`/YStep` tile-offset repetition,
and the nested-execution machinery shared with `/Subtype /Form` XObjects all integrate correctly
through the system's own public entry point.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return value
constitutes a failure. Collectively, these scenarios cover the complete current CanvasNetPdf
feature set: document structure parsing, the content-stream interpreter, device color (including
`/CalRGB`, `/ICCBased`, and `/Indexed` color spaces), image XObjects, `/Subtype /Form` XObjects,
the full `FlateDecode`/`LZWDecode`/`ASCII85Decode`/`ASCIIHexDecode`/`RunLengthDecode`/
`CCITTFaxDecode`/`DCTDecode` stream-filter and image-decoding set, embedded-TrueType/Type1/Type1C/
Type3 and Type0/CIDFontType2/CIDFontType0 composite-font text rendering with automatic
system/bundled-fallback substitution, `/Pattern` color-space axial/radial shading and
colored/uncolored tiling pattern fills, RC4/AES-encrypted document decryption, and the documented
validation-contract/fail-closed exception boundaries.
