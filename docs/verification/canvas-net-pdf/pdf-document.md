## PdfDocument Unit Verification Design

<!-- cspell:ignore xref startxref endobj endstream ObjStm MediaBox Zapf Nonsymbolic -->
<!-- cspell:ignore bfchar bfrange beginbfchar endbfchar beginbfrange endbfrange codepoints -->
<!-- cspell:ignore usecmap cidrange cidchar cidfonttype -->
<!-- cspell:ignore functiontype multiinput hival -->
<!-- cspell:ignore Noto registerserif radicalex dogfoods dogfooding -->
<!-- cspell:ignore fontfile quoteright Quoteright quotesingle EOFB -->

This document describes the unit-level verification strategy for the `PdfDocument` class.

`PdfDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core `DemaConsulting.CanvasNet`
package; its unit tests live in the sibling `DemaConsulting.CanvasNet.Pdf.Tests` project.

### Verification Approach

The `PdfDocument` unit is verified through unit tests that exercise its tokenizer, object model,
content-stream interpreter, and public API in isolation. Tokenizer and object-model tests
(`PdfDocumentTests.cs`) construct small, hand-written byte sequences directly (using the
`internal`, `InternalsVisibleTo`-exposed `PdfTokenizer`/`PdfObject` types) for controlled,
targeted coverage of individual lexical and structural rules without needing a full, valid PDF
file for every case. Content-stream interpreter tests (graphics-state stack, path-construction,
path-painting) build small, in-memory, single-page PDFs via a private test helper
(`BuildSinglePagePdf`) parameterized by `/MediaBox` size, optional `/Rotate`, and an arbitrary
content-stream string, then call `Render` through the public API and assert specific pixel
colors at specific `Surface` coordinates - every expected coordinate was independently confirmed
against the real rasterizer (not merely hand-derived) before being fixed into an assertion, since
stroke/curve antialiasing and the PDF-to-device y-axis flip make hand derivation error-prone.
Device-color, stream-filter-pipeline, and image-XObject tests (Phase 3) additionally use a
second private test helper, `BuildSinglePagePdfWithResources`, extending the same in-memory
construction with a page `/Resources` dictionary and 0 or more extra indirect objects (color-space
arrays, image-XObject streams) the resources reference by number - used whenever a test needs a
named `/Resources/ColorSpace` or `/Resources/XObject` entry that a bare content-stream string
cannot express.
Cross-reference form, linear-scan fallback, page-tree traversal/inheritance/cycle-rejection,
`/Encrypt` detection, and public-API (`Open`/`PageCount`/`GetPageInfo`/`Render`/`Dispose`) tests
exercise the same hand-authored, byte-exact fixture files under `PdfFixtures/` (see
`PdfFixtures/README.md` for provenance) through the public API only. Font-resolution/encoding/
text-operator tests (Phase 4) build a minimal synthetic embedded TrueType font in-memory (via the
shared `SyntheticFontBuilder` test helper, linked from `DemaConsulting.CanvasNet.Tests`) whenever
a test only needs controlled glyph outlines/`cmap` mappings; the one required end-to-end,
real-font pixel-level test instead reuses the shared, unmodified `OpenSans-Regular.ttf` fixture
(the same file `DemaConsulting.CanvasNet.Svg.Tests` links in), re-deriving its expected
device-pixel positions from the font's own outline/metrics rather than hardcoded numbers. Because
`PdfDocument`'s dependencies (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`,
`Geometry.PathBuilder`, `Drawing.PathFiller`/`PathStroker`, and, as of Phase 4, `Fonts.TrueTypeFont`)
are all sibling in-house types, not external services, no mocking or stubbing is required. Tests
assert on parsed token/object field values, on `PageCount`/`PdfPageInfo` field values, on
`Surface` pixel/dimension values, and on thrown exception types (and, for
`UnsupportedImageFeatureException`, its `Feature` token) - never on "no exception thrown" alone,
so every test can actually fail if the implementation is wrong. Encrypted-document tests
(`PdfDocumentEncryptionTests.cs`, Phase 16/17) build minimal, valid, encrypted single-page PDFs
entirely in-memory using test-only helper methods that independently re-derive the ISO 32000-1
Algorithm 1/2/3/4/5 and ISO 32000-2 Algorithm 2.A steps directly from the specification text
(a hand-rolled RC4 KSA/PRGA, since RC4 is symmetric and so the same helper both "encrypts" these
fixtures and is what production decryption itself implements, plus `Aes.Create()`-based AES-CBC
encryption) - deliberately not copy-pasted from `PdfDocument.Encryption.cs`'s own implementation,
so that a shared bug could not silently mask itself by passing against its own mirrored mistake.
Most fixtures still use the empty user password (Phase 16's original scope), but Phase 17 added
sibling fixture variants built from real, non-empty, and - for the two owner-password tests -
distinct user/owner passwords, exercising the production try-user-then-owner authentication order
end-to-end rather than only the empty-password shortcut.

Unit tests reside in `PdfDocumentTests.cs` within the `DemaConsulting.CanvasNet.Pdf.Tests`
project; encryption-specific tests reside in the sibling `PdfDocumentEncryptionTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `PdfDocument`'s dependencies are all in-house types
  (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`, `Geometry.PathBuilder`,
  `Drawing.PathFiller`/`PathStroker`)
- **Isolation**: Each test method builds its own byte sequence, its own in-memory single-page PDF
  (content-stream interpreter tests), or opens its own fixture file from `PdfFixtures/`; no shared
  state between tests

### Unit-Level Test Scenarios

#### CanvasNetPdf-PdfDocument-Tokenizer: Tokenizer Recognizes Every Lexical Construct

**Tests**: `PdfDocument_Tokenizer_Numbers_ParsesIntegerAndRealForms`,
`PdfDocument_Tokenizer_LiteralString_HandlesNestedParensAndEscapes`,
`PdfDocument_Tokenizer_HexString_DecodesFullBytes`,
`PdfDocument_Tokenizer_HexString_PadsOddDigitCountWithTrailingZero`,
`PdfDocument_Tokenizer_Name_DecodesHashEscapes`,
`PdfDocument_Tokenizer_Delimiters_RecognizesArrayAndDictionaryBrackets`,
`PdfDocument_Tokenizer_Comments_AreSkipped`, `PdfDocument_Tokenizer_Keywords_AreRecognizedVerbatim`,
`PdfDocument_Tokenizer_EndOfInput_ReturnsEndOfFileToken`

Feeds small, hand-written byte sequences directly to `PdfTokenizer`, asserting the returned
token's `Kind` and decoded value (number, string bytes, name text, or keyword text) for every
lexical construct the tokenizer must recognize, including edge cases (odd hex-digit count
padding, `#xx` name escapes, nested-parenthesis literal strings, comment skipping).

#### CanvasNetPdf-PdfDocument-ObjectModel: Object Model Parses Dictionaries, Arrays, and References

**Tests**: `PdfDocument_ObjectModel_Dictionary_ParsesNestedEntries`,
`PdfDocument_ObjectModel_Array_ParsesMixedElementTypes`,
`PdfDocument_ObjectModel_IndirectReference_ParsesObjectAndGenerationNumbers`,
`PdfDocument_ObjectModel_BareNumber_IsNotMisreadAsReference`

Feeds small, hand-written token sequences to `PdfDocument.ParseValue`, asserting the resulting
`PdfObject` tree's `Kind`/field values for nested dictionaries, mixed-kind arrays, indirect
references, and the bare-number-vs-reference disambiguation.

#### CanvasNetPdf-PdfDocument-ClassicXref: Classic Xref Table Resolves Root and Page

**Test**: `PdfDocument_Open_ClassicXref_ResolvesRootAndPage`

Opens `PdfFixtures/classic-xref-single-page.pdf` (a classic `xref` table + `trailer`) through the
public API. Asserts `PageCount` and the single page's `GetPageInfo` size/rotation match the
fixture's known content.

#### CanvasNetPdf-PdfDocument-XrefStream: Xref Stream Resolves Root and Page

**Test**: `PdfDocument_Open_XrefStream_ResolvesRootAndPage`

Opens `PdfFixtures/xref-stream-single-page.pdf` (a `/Type /XRef` cross-reference stream, no
classic table) through the public API. Asserts `PageCount` and the single page's `GetPageInfo`
size match the fixture's known content.

#### CanvasNetPdf-PdfDocument-ObjectStream: Object Stream Decompresses and Resolves Compressed Page

**Test**: `PdfDocument_Open_ObjectStream_DecompressesAndResolvesCompressedPage`

Opens `PdfFixtures/object-stream.pdf` (a `/Type /ObjStm` compressed-object stream holding the page
dictionary) through the public API. Asserts `PageCount` and the compressed page's `GetPageInfo`
size match the fixture's known content, confirming FlateDecode decompression and per-object
resolution within the stream both work correctly.

#### CanvasNetPdf-PdfDocument-HybridXref: Hybrid Xref Resolves Compressed Entry via XRefStm

**Test**: `PdfDocument_Open_HybridXref_ResolvesCompressedEntryViaXRefStm`

Opens `PdfFixtures/hybrid-xref.pdf` (a classic trailer whose own table marks one object free, plus
an `/XRefStm` link to a supplementary cross-reference stream that alone describes that object)
through the public API. Asserts `PageCount` and the page's `GetPageInfo` size match the fixture's
known content, confirming a hybrid entry correctly overrides a classic "free" marker for the same
object number.

#### CanvasNetPdf-PdfDocument-LinearScanFallback: Malformed Startxref Falls Back to Linear Scan

**Test**: `PdfDocument_Open_MalformedStartxref_FallsBackToLinearScan`

Opens `PdfFixtures/malformed-startxref.pdf` (no `startxref`/`xref`/`trailer` at all) through the
public API. Asserts `PageCount` and the page's `GetPageInfo` size match the fixture's known
content, confirming the linear-scan fallback successfully reconstructs an object-offset table and
locates the catalog without any cross-reference section present.

#### CanvasNetPdf-PdfDocument-PageTreeTraversal: Page Tree Traversal Reports All Pages

**Test**: `PdfDocument_PageTree_Traversal_ReportsAllPagesInDocumentOrder`

Opens `PdfFixtures/multi-page-mixed-mediabox-rotate.pdf` (three pages) through the public API.
Asserts `PageCount` equals 3, confirming every `/Type /Page` leaf in the page tree is reported.

#### CanvasNetPdf-PdfDocument-PageTreeInheritance: MediaBox/Rotate Inheritance and Rotation Swap

**Tests**: `PdfDocument_PageTree_Inheritance_ExplicitMediaBoxAndNoRotationIsUnchanged`,
`PdfDocument_PageTree_Inheritance_Rotation90SwapsDisplayedWidthAndHeight`,
`PdfDocument_PageTree_Inheritance_MediaBoxInheritedFromPagesNodeWhenPageOmitsIt`

Opens `PdfFixtures/multi-page-mixed-mediabox-rotate.pdf` through the public API and calls
`GetPageInfo` for each of its three pages. Asserts: a page with its own explicit `/MediaBox` and
no `/Rotate` reports that box unchanged; a `/Rotate 90` page reports swapped width/height relative
to its raw `/MediaBox`; a page declaring no `/MediaBox` of its own inherits its ancestor `/Pages`
node's box.

#### CanvasNetPdf-PdfDocument-PageTreeCycleRejection: Page Tree Cycle Throws InvalidDataException

**Test**: `PdfDocument_PageTree_CycleRejection_ThrowsInvalidDataException`

Opens `PdfFixtures/cyclic-page-tree.pdf` (a `/Kids` entry referencing an ancestor) through the
public API. Asserts `InvalidDataException` is thrown, confirming the unbounded-cycle guard stops
traversal rather than looping forever.

#### CanvasNetPdf-PdfDocument-EncryptDetection: Unsupported Encrypted-Document Shapes Throw Distinguishable UnsupportedImageFeatureException

**Tests**: `PdfDocument_Open_EncryptedTrailer_ThrowsUnsupportedImageFeatureException`,
`CanvasNetPdf_SystemIntegration_PdfEncryptDetection_EncryptedTrailerThrowsUnsupportedImageFeatureException`,
`PdfDocument_Open_EncryptedAesV3_R6_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Open_EncryptedRc4_WrongUserPasswordHash_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Open_EncryptedAesV3_WrongValidationHash_ThrowsUnsupportedImageFeatureException`

Opens `PdfFixtures/encrypted-trailer.pdf` (a trailer containing an `/Encrypt` key whose `/Filter`
is `/Adobe.PubSec`, not `/Standard`) through the public API and asserts
`UnsupportedImageFeatureException` is thrown with `Feature == "pdf-encrypted-filter-Adobe.PubSec"`
(both from `PdfDocumentTests.cs` and, identically, from `PdfSystemIntegrationTests.cs`'s own
end-to-end copy of the same assertion). Separately builds an in-memory `/V 5`/`/R 6` document and
asserts `Feature == "pdf-encrypted-r6-hardened-hash"` (AES-256's "hardened hash" key derivation is
out of scope). Separately builds an in-memory RC4 document with a well-formed `/O` but a
deliberately wrong `/U`, and an in-memory AESV3/R5 document with a deliberately wrong `/U`
validation hash, asserting both throw with `Feature == "pdf-encrypted-password-required"` when no
password is supplied (a real, non-empty password is genuinely required to open either document;
see `CanvasNetPdf-PdfDocument-EncryptionPasswordAuthentication` below for the distinguishable
`"pdf-encrypted-incorrect-password"`/`"pdf-encrypted-password-non-ascii"` tokens thrown when a
password *is* supplied but still fails to authenticate, or is malformed).

#### CanvasNetPdf-PdfDocument-EncryptionRc4: RC4-Encrypted Documents With an Empty User Password Open and Render

**Tests**: `PdfDocument_Open_EncryptedRc4_40Bit_DecryptsAndRenders`,
`PdfDocument_Open_EncryptedRc4_128Bit_DecryptsAndRenders`,
`PdfDocument_Open_Encrypted_EncryptDictionaryStringsAreNeverDecrypted`,
`PdfDocument_Open_Encrypted_CatalogOwnStringIsDecrypted`,
`PdfDocument_Open_Encrypted_ObjStmContainedObjects_AreNeverDoubleDecrypted_DocumentedByDesign`

Builds minimal, in-memory, `/Filter /Standard` encrypted single-page PDFs for `/V 1`/`/R 2`
(40-bit) and `/V 2`/`/R 3` (128-bit) RC4, each with a test-computed `/O`/`/U` (the `/R 3` case's
`/U` deliberately carries non-zero, arbitrary trailing padding bytes in its last 16 of 32 bytes,
proving production authentication compares only the first 16) and an RC4-encrypted `/Contents`
stream. Opens each through the public API with no password supplied and asserts `Render` produces
the expected opaque-black/transparent pixels for the plaintext content stream's filled rectangle,
proving the empty user password authenticated and every string/stream decrypted correctly.
`PdfDocument_Open_Encrypted_EncryptDictionaryStringsAreNeverDecrypted` re-asserts the same
RC4 40-bit scenario standalone as a dedicated regression anchor for the invariant that the
`/Encrypt` dictionary's own `/O`/`/U` strings are never mistakenly decrypted (relying on
`InitializeEncryption` resolving, and thereby caching, that object before any encryption key is
ever set - if this ordering were ever broken, the corrupted `/O`/`/U` bytes would make every
positive encryption test, including this one, fail authentication).
`PdfDocument_Open_Encrypted_CatalogOwnStringIsDecrypted` embeds an RC4-encrypted `/Lang (en-US)`
literal string directly in the Catalog's own body (object 1, encrypted with its own per-object
key exactly like every other string in the document), confirms the document still opens and
renders correctly, then - via reflection into the private `GetObject` method - re-fetches the
cached Catalog object and asserts its `/Lang` bytes are the plaintext `"en-US"`, proving that the
Catalog, resolved and cached pre-key by the constructor's own `IsValidCatalogRoot` check before
`InitializeEncryption` ever runs, is still correctly decrypted once `InitializeEncryption`'s own
`InvalidateObjectCacheExceptEncryptDictionary` step discards and forces re-resolution of every
object cached before the key existed (except the `/Encrypt` dictionary's own).
`PdfDocument_Open_Encrypted_ObjStmContainedObjects_AreNeverDoubleDecrypted_DocumentedByDesign`
constructs a synthetic `/Type /XRef` cross-reference-stream-based encrypted PDF whose compressed
object 6 (`<< /Greeting (Hello, Encrypted World!) >>`) lives inside object 7's `/Type /ObjStm`
container, itself RC4-encrypted as a whole (never re-encrypted per contained object), and asserts
both that the document still renders correctly (object 4's own encrypted `/Contents` stream
decrypts correctly) and - via the same reflection technique - that the compressed object's own
`/Greeting` string decompresses/decrypts to the correct plaintext, proving `GetObject`'s two
mutually exclusive code paths (`ParseIndirectObjectAt`, which decrypts strings, for a direct
cross-reference entry; `LoadCompressedObject`, which never does, for a compressed entry, relying
instead on the container stream's own single decrypt pass) produce a correct, single-pass result.

#### CanvasNetPdf-PdfDocument-EncryptionAesV2: AES-128 (AESV2) Documents With an Empty Password

**Test**: `PdfDocument_Open_EncryptedAesV2_128Bit_DecryptsAndRenders`

Builds a minimal, in-memory, `/Filter /Standard`/`/V 4`/`/R 4`/`/CF/StdCF/CFM /AESV2` encrypted
single-page PDF with a test-computed `/O`/`/U` and an AES-128-CBC/PKCS7-encrypted `/Contents`
stream (a per-object key derived via Algorithm 1's `sAlT`-suffixed branch, with a leading 16-byte
initialization vector prepended to the ciphertext). Opens it through the public API with no
password supplied and asserts `Render` produces the expected pixel colors, proving the AES branch
of Algorithm 1 decrypts correctly end-to-end.

#### CanvasNetPdf-PdfDocument-EncryptionAesV3R5: AES-256 R5 (AESV3) Documents With an Empty Password

**Test**: `PdfDocument_Open_EncryptedAesV3_R5_DecryptsAndRenders`

Builds a minimal, in-memory, `/Filter /Standard`/`/V 5`/`/R 5`/`/CF/StdCF/CFM /AESV3` encrypted
single-page PDF with a test-computed `/U` (hash/validation-salt/key-salt) and `/UE` (the raw
32-byte file key, AES-256-CBC-wrapped with a zero initialization vector and no padding under the
password-derived intermediate key) per ISO 32000-2 Algorithm 2.A, and an AES-256-CBC/PKCS7
encrypted `/Contents` stream using the file key directly (no further per-object derivation, as R5
specifies). Opens it through the public API with no password supplied and asserts `Render`
produces the expected pixel colors, proving Algorithm 2.A's validation-salt authentication,
`/UE` unwrapping, and direct-file-key stream decryption all work correctly end-to-end.

#### CanvasNetPdf-PdfDocument-EncryptionPasswordAuthentication: Caller-Supplied Password Tried as User, Then Owner

**Tests**: `PdfDocument_Open_EncryptedRc4_CorrectUserPassword_DecryptsAndRenders`,
`PdfDocument_Open_EncryptedAesV2_CorrectUserPassword_DecryptsAndRenders`,
`PdfDocument_Open_EncryptedAesV3_CorrectUserPassword_DecryptsAndRenders`,
`PdfDocument_Open_EncryptedRc4_CorrectOwnerPassword_DecryptsAndRenders`,
`PdfDocument_Open_EncryptedAesV3_CorrectOwnerPassword_DecryptsAndRenders`,
`PdfDocument_Open_Encrypted_IncorrectPassword_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Open_Encrypted_NonAsciiPassword_ThrowsUnsupportedImageFeatureException`

The three `*_CorrectUserPassword_*` tests build RC4 (`/V 2`/`/R 3`), AES-128 (`/V 4`/`/R 4`/
`/CFM /AESV2`), and AES-256 R5 (`/V 5`/`/R 5`/`/CFM /AESV3`) fixtures whose `/O`/`/U` (or `/U`/
`/UE`) are derived from a real, non-empty password (`"test"`) rather than the empty-password
padding constant, then call `PdfDocument.Open(stream, "test")` and assert `Render` produces the
expected pixel colors - proving the user-password authentication path correctly encodes/pads a
real password's bytes (Latin-1 for R2-R4, UTF-8 for R5) instead of always using the empty-password
constant.

`PdfDocument_Open_EncryptedRc4_CorrectOwnerPassword_DecryptsAndRenders` builds an `/R 3` RC4
fixture whose `/O` is computed from a distinct owner password and a different real user password
(ISO 32000-1 Algorithm 3's encrypt direction) while `/U` and the content stream are derived from
the real user password only; calling `Open(stream, ownerPassword)` necessarily fails the
user-password attempt first (the supplied string is not the real user password), then succeeds
via `RecoverPaddedUserPasswordAlgorithm3` recovering the padded user password from `/O` and
re-deriving/re-authenticating a candidate file key - proving the R2-R4 owner-password recovery
path end-to-end. `PdfDocument_Open_EncryptedAesV3_CorrectOwnerPassword_DecryptsAndRenders` builds
an R5 fixture whose `/U`/`/UE` are derived from an unrelated, empty user password (so the supplied
owner password fails the user-password attempt first) and whose `/O`/`/OE` are built via the
owner-password variant of ISO 32000-2 Algorithm 2.A - hashing/encrypting over
`password ‖ salt ‖ U` where `U` is the full 48-byte `/U` value - proving
`TryComputeFileKeyAlgorithm2AOwnerPassword` recovers the file key directly from `/OE` and that the
document then renders correctly.

`PdfDocument_Open_Encrypted_IncorrectPassword_ThrowsUnsupportedImageFeatureException` builds a
well-formed `/R 3` RC4 fixture with real, distinct, correct owner and user passwords, then calls
`Open(stream, "wrong-password")` and asserts `UnsupportedImageFeatureException` is thrown with
`Feature == "pdf-encrypted-incorrect-password"` - proving that a password which authenticates as
neither role is rejected with the new, distinguishable token (not the null-password
`"pdf-encrypted-password-required"` token, and not a silent wrong-key decrypt).

`PdfDocument_Open_Encrypted_NonAsciiPassword_ThrowsUnsupportedImageFeatureException` builds a
well-formed `/R 2` RC4 fixture (empty-user-password `/O`/`/U`, irrelevant to this test) and calls
`Open(stream, "caf\u00e9")` (containing `é`, outside ASCII 0-127), asserting
`UnsupportedImageFeatureException` is thrown with `Feature == "pdf-encrypted-password-non-ascii"`

- proving `EncodeR2R4PasswordBytes` rejects non-ASCII R2-R4 passwords before any RC4/MD5
authentication work is attempted, rather than silently deriving wrong key material from a
truncated or mis-encoded byte sequence.

#### CanvasNetPdf-PdfDocument-Open: Open Validates Null and Empty/Whitespace Arguments

**Tests**: `PdfDocument_Open_NullStream_ThrowsArgumentNullException`,
`PdfDocument_Open_NullPath_ThrowsArgumentNullException`,
`PdfDocument_Open_EmptyOrWhitespacePath_ThrowsArgumentException`

Calls `Open(Stream)`/`Open(string)` with a null stream, a null path, and an empty/whitespace-only
path. Asserts `ArgumentNullException`/`ArgumentException` respectively, confirming argument
validation precedes any parsing attempt.

#### CanvasNetPdf-PdfDocument-GetPageInfo: GetPageInfo Validates Page Index Range

**Test**: `PdfDocument_GetPageInfo_OutOfRangeIndex_ThrowsArgumentOutOfRangeException`
(`[Theory]`: a negative index and an index equal to `PageCount`)

Opens a single-page fixture and calls `GetPageInfo` with an out-of-range index. Asserts
`ArgumentOutOfRangeException` for both a negative index and one at/beyond `PageCount`.

#### CanvasNetPdf-PdfDocument-Render: Render Paints Content-Stream Geometry and Validates Arguments

**Tests**: `PdfDocument_Render_ValidPageIndex_ReturnsCorrectlySizedBlankSurface`,
`PdfDocument_Render_OutOfRangePageIndex_ThrowsArgumentOutOfRangeException`,
`PdfDocument_Render_InvalidWidth_PropagatesSurfaceArgumentOutOfRangeException`,
`PdfDocument_ContentStream_NoContents_RendersBlankSurface`

Calls `Render` with a valid page index and caller-chosen size against a fixture with no
`/Contents`, asserting the returned `Surface` has exactly the requested dimensions and every
pixel is the default (fully transparent) value. Calls `Render` with an out-of-range page index
and a non-positive width, asserting `ArgumentOutOfRangeException` in both cases (the latter
propagated unwrapped from `Surface`'s own constructor).

#### CanvasNetPdf-PdfDocument-ContentStreamDispatch: Unknown Operators Are Skipped, Malformed Recognized Operators Throw

**Tests**: `PdfDocument_ContentStream_UnknownOperator_IsSkippedWithoutThrowing`,
`CanvasNetPdf_SystemIntegration_PdfRender_FilledRectangleAndStrokedLine_PaintsExpectedPixels`

Renders a content stream containing an unrecognized operator (`gs`, ExtGState - still out of
scope in Phase 3) immediately followed by a recognized stroke operator, asserting the stroke
still painted its expected pixel (proving the unknown operator was silently skipped rather than
aborting the whole stream). The end-to-end system-integration test independently proves the same
dispatch loop against a real, hand-authored fixture containing both a filled rectangle and a
stroked line, asserting specific opaque-black and transparent pixels at specific coordinates.

#### CanvasNetPdf-PdfDocument-ContentsResolution: Contents Array Concatenates With a Space Separator

**Test**: `PdfDocument_ContentStream_ContentsArray_ConcatenatesStreamsWithSpaceSeparator`

Opens `PdfFixtures/contents-array-two-streams.pdf`, whose `/Contents` is a two-entry array (one
stream holding `"10 10 40"`, the other holding `"40 re f"`) deliberately chosen so an
omitted-separator regression would merge the two `"40"` tokens into `"4040"`, reducing `re`'s
operand count and (in this specific fixture) throwing `InvalidDataException` instead of rendering
the expected filled square. Asserts the expected filled-square pixel is opaque black and a pixel
outside it remains transparent, confirming the required space-separator concatenation.

#### CanvasNetPdf-PdfDocument-GraphicsStateStack: q/Q/cm Compose and Restore the Current Transform

**Tests**: `PdfDocument_GraphicsState_QPushCmThenQPop_RestoresPriorTransform`,
`PdfDocument_GraphicsState_NestedQQ_ComposesTransformsInOrder`,
`PdfDocument_GraphicsState_UnbalancedQWithNoMatchingPush_DoesNotThrow`,
`PdfDocument_GraphicsState_MalformedCmOperandCount_ThrowsInvalidDataException`

Renders a content stream that draws an identical line segment both inside a `q`/`cm`/`Q` block
and after the matching `Q`, asserting the post-`Q` segment lands at its unscaled device position
(proving the CTM was restored, not left scaled). Renders a nested `q`/`cm`/`q`/`cm`/`Q`/`Q` block
whose two `cm`s each scale a different single axis, asserting the drawn segment lands only at the
position both axes' composed scale produces, and explicitly asserting the two single-axis-only
positions a partial-composition bug would instead produce are *not* painted. Renders a stray `Q`
with no matching prior `q`, asserting no exception is thrown (a documented no-op leniency).
Renders a malformed `cm` (wrong operand count), asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-CtmDerivation: Base CTM Correctly Incorporates MediaBox/Rotate/Requested Size

**Test**: `CanvasNetPdf_SystemIntegration_PdfRender_RotatedPage_MapsGeometryToCorrectPixelPosition`

Opens `PdfFixtures/path-construction-rotated-page.pdf` (`/MediaBox [0 0 200 100]`,
`/Rotate 90`, a deliberately asymmetric filled rectangle near the raw MediaBox's bottom-left
corner) and renders it at its rotation-swapped display size (100x200). Asserts the rectangle's
interior is opaque black at its mathematically correct rotated device position, and asserts a
specific pixel where a 270-instead-of-90 rotation-sign regression would incorrectly paint it
instead remains transparent - a fixture and assertion pair specifically designed so a
rotation-sign or origin-offset defect produces a detectably wrong result rather than a merely
shifted-but-still-plausible one.

#### CanvasNetPdf-PdfDocument-PathConstruction: Path-Construction Operators Build Documented Geometry

**Tests**: `PdfDocument_PathOps_MoveLineRectCurve_BuildExpectedGeometry`,
`PdfDocument_PathOps_MalformedOperandCount_ThrowsInvalidDataException` (`[Theory]`, one case per
`m`/`l`/`c`/`v`/`y`/`re`), `PdfDocument_PathOps_DrawBeforeMoveTo_ThrowsInvalidDataException`

Renders, in turn: an explicit `m`/`l`/`h`-built square (filled), a `re`-built rectangle (filled),
a full `c` cubic Bezier (stroked), a `v` shorthand curve (whose first control point defaults to
the current point, stroked), and a `y` shorthand curve (whose second control point defaults to
the endpoint, stroked) - asserting the mathematically exact Bezier-midpoint (or interior/exterior)
pixel each construction is expected to paint. A `[Theory]` exercises a too-few/too-many operand
count for each path-construction operator, asserting `InvalidDataException` in every case.
Renders a bare `l` with no preceding `m`/`re`, asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-PathPainting: Path-Painting Operators Fill/Stroke/Clear as Documented

**Tests**: `PdfDocument_PathOps_FillNonZero_PaintsExpectedPixels`,
`PdfDocument_PathOps_FillEvenOdd_PaintsExpectedPixels`, `PdfDocument_PathOps_Stroke_PaintsExpectedPixels`,
`PdfDocument_PathOps_CloseAndFillAndStroke_PaintsExpectedPixels`,
`PdfDocument_PathOps_NoOp_DiscardsPathWithoutPainting`,
`PdfDocument_PathOps_PaintOperator_ClearsPathButPreservesGraphicsState`

Renders a filled rectangle (`f`), asserting an interior pixel is opaque black and an exterior
pixel remains transparent. Renders two nested, same-winding rectangles (`f*`), asserting the outer
ring is filled but the doubly-covered inner region is left as an even-odd "hole". Renders a
stroked vertical line (`S`), asserting a pixel on the line is opaque black and a pixel beside it
is not. Renders an *open* triangle (no `h`) via `b`, asserting both the filled interior and the
implicit closing edge are painted - directly contrasted, at the same coordinate, against the same
triangle painted with a plain `S` (no closing), which leaves that coordinate untouched. Renders a
rectangle followed by `n`, asserting the entire surface remains fully transparent. Renders two
rectangles filled under the same scaled `cm`, asserting the second path's fill lands only in its
own expected region - proving the first path was cleared after its own `f` rather than
accumulating into the second, while the surrounding CTM was preserved across the clear.

#### CanvasNetPdf-PdfDocument-Dispose: Dispose Is Idempotent

**Test**: `PdfDocument_Dispose_CalledTwice_DoesNotThrow`

Calls `Dispose()` twice on the same instance. Asserts neither call throws.

#### CanvasNetPdf-PdfDocument-ObjectDisposedException: Every Other Public Member Throws After Dispose

**Tests**: `PdfDocument_PageCount_AfterDispose_ThrowsObjectDisposedException`,
`PdfDocument_GetPageInfo_AfterDispose_ThrowsObjectDisposedException`,
`PdfDocument_Render_AfterDispose_ThrowsObjectDisposedException`

Disposes an opened document, then calls `PageCount`, `GetPageInfo`, and `Render`. Asserts each
throws `ObjectDisposedException`.

#### CanvasNetPdf-PdfDocument-DeviceColorOperators: Device Color Operators Set the Expected Fill/Stroke Color

**Tests**: `PdfDocument_Color_SetGrayFill_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetGrayStroke_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetRgbFill_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetRgbStroke_SetsExpectedRgbaColor`,
`PdfDocument_Color_SetCmykFill_ConvertsToExpectedRgbaColor`,
`PdfDocument_Color_SetCmykStroke_ConvertsToExpectedRgbaColor`,
`PdfDocument_Color_ComponentValuesOutsideZeroToOne_AreClamped` (`[Theory]`),
`PdfDocument_Color_MalformedOperandCount_ThrowsInvalidDataException` (`[Theory]`, one case per
`g`/`G`/`rg`/`RG`/`k`/`K`/`cs`/`CS`/`sc`/`SC`/`scn`/`SCN`)

Renders a filled rectangle/stroked line after each of `g`/`G`/`rg`/`RG`/`k`/`K`, asserting the
painted pixel matches the exact expected RGBA conversion (including the CMYK
`R = 255 * (1 - C) * (1 - K)` formula, hand-verified). A `[Theory]` renders `rg` with several
component values outside `[0, 1]`, asserting the painted color matches the clamped (not
rejected) result. A second `[Theory]` renders each operator with a malformed operand count,
asserting `InvalidDataException` in every case.

#### CanvasNetPdf-PdfDocument-ColorSpaceOperators: cs/CS/sc/scn Track and Apply the Current Color Space

**Tests**: `PdfDocument_Color_SetColorSpaceFill_DeviceNames_ResetsColorToBlack` (`[Theory]`),
`PdfDocument_Color_SetColorSpaceStroke_DeviceNames_ResetsColorToBlack` (`[Theory]`),
`PdfDocument_Color_SetColorFillUsingCurrentColorSpace_Sc_PaintsExpectedColor`,
`PdfDocument_Color_SetColorStrokeUsingCurrentColorSpace_SC_PaintsExpectedColor`,
`PdfDocument_Color_ScnWithPatternName_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Color_UnsupportedNamedColorSpace_ThrowsUnsupportedImageFeatureException` (`[Theory]`:
Separation/DeviceN/ICCBased/CalGray/Lab),
`PdfDocument_Color_CalRgbColorSpace_ResolvesToDeviceRgb`,
`CanvasNetPdf_SystemIntegration_PdfRender_ColoredRectangleFill_PaintsExpectedRgbPixels`

Renders a red-filled rectangle, then a `cs`/`CS` device-name switch (asserted, for all three
device names, to reset color to opaque black), then a second rectangle - proving the reset. Sets
color via `sc`/`SC` against the current (`DeviceRGB`) color space, asserting the expected painted
color. Renders `scn` with a trailing pattern name, asserting
`Codecs.UnsupportedImageFeatureException`. A `[Theory]` selects each unsupported named color
space (declared inline in a `/Resources/ColorSpace` dictionary built via
`BuildSinglePagePdfWithResources`) via `cs`, asserting
`Codecs.UnsupportedImageFeatureException` in every case - including an `/ICCBased` array
referencing object `4` (the page's own content stream, reused as a convenient already-present
stream object), whose dictionary has neither `/N` nor `/Alternate`. The end-to-end
system-integration test independently
proves `rg` painting a real, hand-authored fixture, asserting a specific interior pixel is opaque
red and an exterior pixel remains transparent.

#### CanvasNetPdf-PdfDocument-IccBasedColorSpace: /ICCBased Resolves via /Alternate or /N, Fails Closed Otherwise

**Tests**: `PdfDocument_Color_IccBasedN3_ResolvesAsDeviceRgb`,
`PdfDocument_Color_IccBasedWithAlternate_PrefersAlternateOverN`,
`PdfDocument_Color_IccBasedUnsupportedN_ThrowsUnsupportedImageFeatureException`

Declares `/CS0 [/ICCBased 5 0 R]` in `/Resources/ColorSpace`, where object `5` is a stream whose
dictionary is `<< /N 3 >>` (no `/Alternate`); selects it via `cs` and paints via `scn` with 3
operands, asserting the painted pixel matches exactly what `DeviceRGB` would paint - proving `/N
3` resolves as `DeviceRGB` (including that `scn`'s own operand-count validation reused
`ComponentCount` correctly for the resolved space, not the array's own shape). Repeats with
object `5`'s dictionary as `<< /N 1 /Alternate /DeviceRGB >>` (an /N that implies `DeviceGray`,
1 component, deliberately contradicting the /Alternate): `scn` with 3 operands succeeds and paints
red, which is only possible if `/Alternate` won over `/N` (had `/N` been used instead, 3 operands
would mismatch the expected 1 and throw `InvalidDataException`), making the precedence
deterministically auditable. Declares object `5`'s dictionary as `<< /N 2 >>` (no `/Alternate`,
and `2` maps to no device color space); asserts `cs` throws
`Codecs.UnsupportedImageFeatureException`.

#### CanvasNetPdf-PdfDocument-IndexedColorSpace: /Indexed Resolves as a Palette Lookup, Clamps Out-of-Range Indices

**Tests**: `PdfDocument_Images_DoOperator_IndexedColorSpace_PlacesExpectedPaletteColors`,
`PdfDocument_Color_IndexedColorSpace_Scn_PaintsExpectedPaletteColor`,
`PdfDocument_Color_IndexedColorSpace_OutOfRangeIndex_ClampsToHighestPaletteEntry`,
`PdfDocument_Color_IndexedColorSpace_UnsupportedBase_ThrowsUnsupportedImageFeatureException`

Places a 2x2 image XObject whose `/ColorSpace` is `[/Indexed /DeviceRGB 2 <...>]` (a 3-entry
red/green/blue palette) with `/BitsPerComponent 8` raw index samples (`0, 1, 2, 2`) via `Do`,
asserting the four composited device pixels match the corresponding palette RGB entries - proving
an image sample is interpreted as a raw, un-normalized palette index (not a `[0, 1]`-divided
component like every other supported `/ColorSpace`). Declares `/CS0 [/Indexed /DeviceRGB 1 <...>]`
(a 2-entry red/green palette) in `/Resources/ColorSpace`, selects it via `cs`, and paints a filled
rectangle via `scn 1` (1 numeric operand, the index), asserting the painted color is the green
palette entry - proving the same `/Indexed` resolution is shared identically between the image
XObject and `cs`/`scn` paths. Repeats with `scn 5` (an index past `Hival = 1`), asserting the
painted color is still the green (highest valid, `Hival`) palette entry rather than throwing.
Declares `/CS0 [/Indexed [/Separation /Spot /DeviceGray 4 0 R] 1 <...>]` (an unsupported base
color space); asserts `cs` throws `Codecs.UnsupportedImageFeatureException`, propagated from the
base color space's own resolution before `/Hival`/the lookup table are ever consulted.

#### CanvasNetPdf-PdfDocument-FilterPipeline: Filter Pipeline Reverses PNG/TIFF Predictors, Fails Closed Otherwise

**Tests**: `PdfDocument_Images_FlateDecodePngPredictor_DecodesExpectedPixels`,
`PdfDocument_Images_FlateDecodeTiffPredictor_DecodesExpectedPixels`,
`PdfDocument_Images_LzwDecodePngPredictor_DecodesExpectedPixels`,
`PdfDocument_Images_UnsupportedFilter_ThrowsUnsupportedImageFeatureException`

Builds a small (2x2 pixel), hand-computed `FlateDecode` stream with a PNG predictor (mixed
`None`/`Up` filter-type rows) and, separately, a TIFF predictor (per-row horizontal-difference
encoding) as an image XObject, rendering it via `Do` and asserting every decoded pixel matches
the value independently hand-derived from the PNG specification's own defilter formulas (not
merely re-deriving the implementation's own output). Repeats the PNG-predictor case with the raw
bytes compressed via `LZWDecode` instead of `FlateDecode` (using a test-only classic-LZW encoder,
never shipped in `src/`), proving predictor reversal is gated on filter name (`FlateDecode` or
`LZWDecode`) rather than merely on `/DecodeParms` presence. Renders an image XObject declaring an
unsupported filter (`/JPXDecode` - still genuinely unsupported, unlike `/LZWDecode`/
`/CCITTFaxDecode`, both of which this and a later phase respectively implement), asserting
`Codecs.UnsupportedImageFeatureException`.

#### CanvasNetPdf-PdfDocument-LzwDecodeFilter: LZWDecode Decodes the PDF-Variant Algorithm, Fails Closed on Malformed Input

**Tests**: `PdfDocument_Filters_LzwDecode_SpecExampleTable7_DecodesExpectedBytes`,
`PdfDocument_Filters_LzwDecode_EarlyChangeDefault_GrowsCodeWidthOneCodeEarly`,
`PdfDocument_Filters_LzwDecode_EarlyChangeZero_GrowsCodeWidthOneCodeLater`,
`PdfDocument_Filters_LzwDecode_ClearCodeMidStream_ReinitializesTable`,
`PdfDocument_Filters_LzwDecode_MissingEodCode_ThrowsInvalidDataException`,
`PdfDocument_Filters_LzwDecode_InvalidCode_ThrowsInvalidDataException`,
`PdfDocument_Images_DoOperator_DeviceGrayLzwDecode_PlacesExpectedPixels`,
`CanvasNetPdf_SystemIntegration_PdfRender_LzwDecodeContentStream_PaintsExpectedPixels`

Every unit test renders a `width`x1 `DeviceGray` image XObject scaled to exactly fill a
`width`x1 device surface (a 1:1 nearest-neighbor byte-to-pixel mapping), so each decoded byte is
directly inspectable as one device pixel. Decodes the ISO 32000-1/2 section 7.4.4.2 `Table
7`/`EXAMPLE 2` worked vector (`80 0B 60 50 22 0C 0C 85 01`), asserting the exact expected
`"-----A---B"` bytes. Decodes a synthetic (not spec-provided), deterministically generated
320-byte vector - long enough for the dictionary to cross dynamic code 511, the 9-to-10-bit
code-width growth boundary - encoded once with the default (`EarlyChange` absent, meaning `1`)
timing and once with `/EarlyChange 0`, asserting each round-trips correctly with its matching
timing and that decoding the same bytes with the opposite timing throws `InvalidDataException`
(proving the two timings are genuinely different, not merely two names for the same behavior).
Proves a mid-stream Clear code actually reinitializes the dictionary: a hand-packed vector
(`CLEAR, 'A', 'A', 'A', CLEAR, 'B', 260`) asserts `InvalidDataException`, since code `260` (which
would still mean `"AA"` if the table were *not* reset) is not yet present in a properly reset
table. Asserts `InvalidDataException` for a bit stream truncated before an EOD code, and for an
invalid/out-of-range code. Places a plain (unpredicted) `LZWDecode` `DeviceGray` image via `Do`,
asserting the composited pixels. The end-to-end system-integration test independently proves
`LZWDecode` decoding a real page `/Contents` stream, asserting a specific interior pixel of the
resulting filled rectangle is opaque red and an exterior pixel remains transparent.

#### CanvasNetPdf-PdfDocument-AsciiDecodeFilters: ASCII85Decode/ASCIIHexDecode Decode Per Spec, Fail Closed

**Tests**: `PdfDocument_Filters_Ascii85Decode_ZeroGroup_DecodesToFourZeroBytes`,
`PdfDocument_Filters_Ascii85Decode_FullGroup_DecodesExpectedBytes`,
`PdfDocument_Filters_Ascii85Decode_PartialFinalGroup_AppliesPaddingRule`,
`PdfDocument_Filters_Ascii85Decode_WhitespaceIgnored_DecodesExpectedBytes`,
`PdfDocument_Filters_Ascii85Decode_ZInMiddleOfGroup_ThrowsInvalidDataException`,
`PdfDocument_Filters_Ascii85Decode_ValueExceedsRange_ThrowsInvalidDataException`,
`PdfDocument_Filters_Ascii85Decode_MissingEodMarker_ThrowsInvalidDataException`,
`PdfDocument_Filters_AsciiHexDecode_HexDigitPairs_DecodesExpectedBytes`,
`PdfDocument_Filters_AsciiHexDecode_OddTrailingDigit_PadsWithZeroNibble`,
`PdfDocument_Filters_AsciiHexDecode_WhitespaceIgnored_DecodesExpectedBytes`,
`PdfDocument_Filters_AsciiHexDecode_InvalidCharacter_ThrowsInvalidDataException`,
`PdfDocument_Filters_AsciiHexDecode_MissingEodMarker_ThrowsInvalidDataException`,
`CanvasNetPdf_SystemIntegration_PdfRender_Ascii85DecodeContentStream_PaintsExpectedPixels`,
`CanvasNetPdf_SystemIntegration_PdfRender_AsciiHexDecodeContentStream_PaintsExpectedPixels`

Using the same `width`x1 `DeviceGray`-image inspection technique as the LZWDecode tests above,
decodes the `z` all-zero-group shorthand; a full 5-character group and the self-derived (per the
spec's own section 7.4.3 formula, since the ISO 32000-1 text has no isolated numbered ASCII85
example) `00 01 02 03` → `!!*-'` vector; the final-partial-group padding rule via the self-derived
2-byte (`4D 61` → `9jn`) and 3-byte (`4D 61 6E` → `9jqo`) vectors; and that interspersed PDF
white-space characters are ignored. Asserts `InvalidDataException` for a `z` occurring mid-group,
a 5-character group whose base-85 value exceeds `2^32 - 1` (`uuuuu`, all-`u` digits), and a
missing `~>` EOD marker. Decodes ASCIIHexDecode hex-digit pairs, an odd trailing digit (implicitly
padded with a zero nibble), and interspersed whitespace; asserts `InvalidDataException` for an
invalid character and a missing `>` EOD marker. The two end-to-end system-integration tests
independently prove each filter decoding a real page `/Contents` stream, asserting the expected
filled-rectangle pixels.

#### CanvasNetPdf-PdfDocument-RunLengthDecodeFilter: RunLengthDecode Implements the PackBits Scheme, Fails Closed on Truncation

**Tests**: `PdfDocument_Filters_RunLengthDecode_LiteralRun_CopiesBytesVerbatim`,
`PdfDocument_Filters_RunLengthDecode_RepeatRun_RepeatsSingleByte`,
`PdfDocument_Filters_RunLengthDecode_EodMarker_StopsDecoding`,
`PdfDocument_Filters_RunLengthDecode_TruncatedRun_ThrowsInvalidDataException`,
`CanvasNetPdf_SystemIntegration_PdfRender_RunLengthDecodeContentStream_PaintsExpectedPixels`

Using the same `width`x1 `DeviceGray`-image inspection technique, decodes a literal run (length
byte 0-127 copies the following `length + 1` bytes verbatim) and a repeat run (length byte
129-255 repeats the following single byte `257 - length` times), asserts the `128` EOD length
byte stops decoding and ignores any trailing bytes after it, and asserts `InvalidDataException`
for a literal run that declares more bytes than remain (truncated, no EOD reached). The
end-to-end system-integration test independently proves `RunLengthDecode` decoding a real page
`/Contents` stream, asserting the expected filled-rectangle pixels.

#### CanvasNetPdf-PdfDocument-CcittFaxDecodeFilter: CCITTFaxDecode Implements T.6 Group 4 MMR, Fails Closed Otherwise

**Tests**: `PdfDocument_Images_DoOperator_CcittFaxGroup4_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_CcittFaxBlackIs1Default_MapsZeroBitToBlack`,
`PdfDocument_Images_DoOperator_CcittFaxBlackIs1True_InvertsPolarity`,
`PdfDocument_Images_DoOperator_CcittFaxNonByteAlignedColumns_DecodesExpectedRowPadding`,
`PdfDocument_Images_CcittFaxGroup3K_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_CcittFaxCombinedWithAnotherFilter_ThrowsInvalidDataException`,
`PdfDocument_Images_CcittFaxEndOfLineTrue_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Filters_CcittFaxRunLengthTables_RoundTripEveryCode`,
`PdfDocument_Filters_CcittFaxModeCodeTable_RoundTripEveryCode`,
`PdfDocument_Images_DoOperator_CcittFaxPassModeElement_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_CcittFaxEncodedByteAlignTrue_SkipsRowPaddingBits`

Uses a small test-only Group 4 (T.6) MMR encoder (parameterized over an arbitrary `bool[,]` pixel
grid, mirroring the existing test-only classic-LZW encoder's precedent) to produce trustworthy
encoded bytes, cross-checked against a hand-derived, bit-level-documented literal encoding of a
known small pattern (see the code comment at the literal byte array's declaration for the full
worked-through reasoning) so that the round-trip tests cannot merely be checking an encoder bug
against a matching decoder bug. `PdfDocument_Images_DoOperator_CcittFaxGroup4_PlacesExpectedPixels`
places a Group-4-encoded image XObject via `cm`/`Do` and asserts the full-page-rendered device
pixels match the known source black/white pattern at specific `(x, y)` coordinates.
`PdfDocument_Images_DoOperator_CcittFaxBlackIs1Default_MapsZeroBitToBlack` and
`..._CcittFaxBlackIs1True_InvertsPolarity` assert the default (`/BlackIs1` absent or `false`) and
explicit `/BlackIs1 true` cases each produce the opposite pixel polarity for the identical encoded
bit stream, proving `/BlackIs1` is honored rather than ignored.
`..._CcittFaxNonByteAlignedColumns_DecodesExpectedRowPadding` uses `/Columns` values (`10`, `20`)
that do not divide evenly into whole bytes, asserting every decoded row is still exactly
`(Columns + 7) / 8` packed bytes and that the unused trailing padding bits of the final byte do
not leak into the next row's pixels. `..._CcittFaxGroup3K_ThrowsUnsupportedImageFeatureException`
asserts a non-negative `/K` (Group 3, including the `/K` default of `0` when `/DecodeParms` omits
it entirely) throws `UnsupportedImageFeatureException` with a clear message rather than being
misdecoded as Group 4. `..._CcittFaxCombinedWithAnotherFilter_ThrowsInvalidDataException` asserts
a `/Filter` array naming `CCITTFaxDecode` alongside another filter throws `InvalidDataException`,
mirroring the equivalent `DCTDecode`-combined-with-another-filter check.
`..._CcittFaxEndOfLineTrue_ThrowsUnsupportedImageFeatureException` asserts an explicit
`/EndOfLine true` is rejected rather than silently ignored (this implementation never scans for
EOL/EOFB/RTC bit patterns at all). `PdfDocument_Filters_CcittFaxRunLengthTables_RoundTripEveryCode`
is an internals-only table self-consistency test (enabled by `InternalsVisibleTo`): for every
(run length, code bits, code length) entry in both the White and Black Modified Huffman
run-length code tables, it writes the code's bits through `CcittBitReader` and asserts
`ReadVariableLengthCode` recovers the exact same run length, proving the prefix-free code tables
are internally consistent (no entry is a prefix of, or shares a prefix with a different length
than, another) independently of whether any single encoded test image happens to exercise that
particular code. `PdfDocument_Filters_CcittFaxModeCodeTable_RoundTripEveryCode` mirrors that same
internals-only table self-consistency pattern for `ModeCodeTable` (also made `internal` for this
purpose): for all nine `(mode code bits, code length)` entries (Pass, Horizontal, V0, VR1-3,
VL1-3), it writes the code's bits through `CcittBitReader` and asserts `ReadMode` recovers the
exact same mode, closing the gap that `VR1`, `VR3`, `VL2`, and `VL3` were otherwise never
exercised by any rendered test image.
`PdfDocument_Images_DoOperator_CcittFaxPassModeElement_PlacesExpectedPixels` uses a dedicated,
hand-derived literal encoded byte array (independently re-verified via a from-scratch Python
re-implementation of `ReadMode`/`FindB1B2`/`DecodeCcittRow`, stepped through to confirm it
actually decodes a `Pass` mode element) for an 8x2 pattern whose second row has no changing
element of its own, forcing the decoder down the `Pass` branch, and asserts the rendered pixels
match the known pattern. `PdfDocument_Images_DoOperator_CcittFaxEncodedByteAlignTrue_SkipsRowPaddingBits`
reuses that same pattern's bit encoding but inserts deliberately non-zero garbage padding bits
between row 0 and row 1 and sets `/EncodedByteAlign true`, asserting the decoder still recovers
the exact same expected pixels - proving `AlignToByte` is actually invoked to skip the garbage
bits before each row, rather than the implementation coincidentally tolerating zero padding only.

#### CanvasNetPdf-PdfDocument-ImageXObjects: Do Composites Images, Fails Closed on Unsupported Features

**Tests**: `PdfDocument_Images_DoOperator_DeviceGrayFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DeviceRgbFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DeviceCmykFlateDecode_PlacesExpectedPixels`,
`PdfDocument_Images_DoOperator_DctDecodeJpeg_PlacesExpectedPixels`,
`PdfDocument_Images_UnsupportedBitsPerComponent_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_UnsupportedColorSpace_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Images_DoOperator_UndefinedXObjectName_ThrowsInvalidDataException`,
`PdfDocument_Images_DoOperator_MalformedOperandCount_ThrowsInvalidDataException`,
`CanvasNetPdf_SystemIntegration_PdfRender_ImageXObjectPlacement_CompositesExpectedPixels`

Places a small, raw 8-bit `DeviceGray`/`DeviceRGB`/`DeviceCMYK` `FlateDecode` image XObject via
`cm`/`Do`, asserting the four composited device pixels match the image's four known source
pixels (proving both the sample-to-color conversion and the unit-square-to-device-space mapping,
including the PDF image-space row-0-is-top convention). Places a bare `DCTDecode` (JPEG) image
XObject (an 8x8 solid-color surface encoded via `Codecs.JpegCodec.Save` at test-run time, quality
100 - a flat color block's DCT has only a DC coefficient, so the round-trip reproduces it within
a small per-channel tolerance), asserting the decoded/composited pixel matches within that
tolerance. Renders an image XObject with an unsupported `/BitsPerComponent` (`1`) and, separately,
an unsupported `/ColorSpace` (`/Lab`), each asserting
`Codecs.UnsupportedImageFeatureException`. Renders `Do` with a name undeclared in
`/Resources/XObject`, asserting `InvalidDataException`. A `[Theory]`
renders `Do` with a malformed operand count/type, asserting `InvalidDataException` in every case.
The end-to-end system-integration test independently proves the same `Do` compositing against a
real, hand-authored fixture, asserting specific composited pixel colors at specific coordinates
matching the fixture's known 2x2 source image, and a pixel outside the placed image's
device-space footprint remains transparent.

#### CanvasNetPdf-PdfDocument-FormXObjects: Do Executes Nested Form XObject Content Streams

**Tests**: `PdfDocument_Images_DoOperator_FormXObject_PaintsNestedContentStream`,
`PdfDocument_Images_DoOperator_FormXObjectWithMatrix_AppliesMatrixToNestedContent`,
`PdfDocument_Images_DoOperator_FormXObjectWithoutOwnResources_FallsBackToInvokingResources`,
`PdfDocument_Images_DoOperator_FormXObjectWithOwnResources_TakesPrecedenceOverInvokingResources`,
`PdfDocument_Images_DoOperator_NestedFormXObjects_RestoresGraphicsStateAfterReturn`,
`PdfDocument_Images_DoOperator_FormXObjectExceedsMaxRecursionDepth_ThrowsInvalidDataException`

Places a `/Subtype /Form` XObject whose own content stream fills a rectangle, asserting the
rectangle's device footprint is painted and a pixel outside it remains blank - proving the Form's
decoded content bytes are actually executed, not skipped. Places a Form XObject declaring its own
`/Matrix` (a translation), asserting the rectangle lands at the matrix-translated device position
and that its un-translated position is left unpainted - proving `/Matrix` is concatenated into the
CTM using the same left-multiply convention as `cm`. Places a Form XObject with no `/Resources` of
its own that references a `/ColorSpace` name declared only in the invoking page's `/Resources`,
asserting the expected color is painted - proving resource-scope fallback. Places a Form XObject
whose own `/Resources` declares the same color-space name differently (a different
component-count color space) than the invoking page's `/Resources`, asserting the Form's own
definition is used (a wrong-arity `sc` would otherwise throw `InvalidDataException`) - proving
resource-scope precedence. Places a two-level-deep nested Form XObject (an outer Form invoked by
the page, itself invoking an inner Form, with a `cm` scale applied along the way), asserting the
inner Form's content paints at the correctly compounded transform and that the invoking page's own
painting performed after the outer `Do` returns lands at its own normal, unscaled position -
proving the nested CTM mutations do not leak back out. Renders a Form XObject that invokes itself
by name (falling back to the invoking page's `/Resources/XObject`), asserting
`InvalidDataException` once nesting exceeds the maximum supported depth, rather than hanging or
crashing the test process.

#### CanvasNetPdf-PdfDocument-FontResolution: Font Resolution Dispatches TrueType/Type1 Simple Fonts

**Tests**: `PdfDocument_Fonts_UnsupportedSubtype_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_UndefinedFontName_ThrowsInvalidDataException`,
`PdfDocument_BuildResolvedFont_EmbeddedFontFileTakesPriorityOverFallback`,
`PdfDocument_Fonts_Type1_DispatchesToSimpleFontResolution_PaintsGlyphInk`,
`PdfDocument_Fonts_Type1_NoFontFile_ResolvesViaFallback`,
`PdfDocument_Fonts_SimpleFont_NoFontDescriptorAtAll_ResolvesViaFallback`

A `[Theory]` builds a `/Resources/Font` dictionary declaring each excluded subtype
(`/MMType1`) in turn and selects it via `Tf`, asserting
`Codecs.UnsupportedImageFeatureException` in every case. Selects a font name absent from
`/Resources/Font` via `Tf`, asserting `InvalidDataException`. Builds a `/Subtype /TrueType` font
dictionary with both an embedded `/FontFile2` stream and a deliberately unmatched `/BaseFont`
name, asserting the embedded synthetic font's own known glyph shape is what gets rendered - not a
fallback font's differently-shaped glyph - proving an embedded font always wins over fallback
substitution. As of Phase 6, a `/Subtype /TrueType` font lacking an embedded `/FontFile2` no
longer fails closed here at all; see `CanvasNetPdf-PdfDocument-FontFallback` below for its
resolution. As of Phase 9, a `/Subtype /Type0` composite font no longer fails closed here either;
see `CanvasNetPdf-PdfDocument-CompositeFontResolution` immediately below for its own resolution
and remaining fail-closed cases. As of Phase B, `/Subtype /Type1` is no longer listed among the
excluded subtypes at all: a dedicated test proves a `/Type1`
font dictionary dispatches to `BuildResolvedSimpleFont` and paints its embedded Type 1 program's
own glyph ink, and a second proves a `/Type1` font with no `/FontFile` resolves via
`ResolveFallbackFont` exactly like an equivalent `/TrueType` font - see
`CanvasNetPdf-PdfDocument-Type1FontResolution` below for the full embedded-`/FontFile`
resolution and its own fail-closed cases. A further test builds a font dictionary with no
`/FontDescriptor` entry at all (as PDF 32000-1 §9.6.2.2 permits for the standard 14 fonts, and
several real-world producers - for example ReportLab - emit), asserting it resolves via
`ResolveFallbackFont` rather than throwing `InvalidDataException`. As of Phase D, `/Subtype
/Type3` is likewise no longer listed among the excluded subtypes (only `/MMType1` remains) - see
`CanvasNetPdf-PdfDocument-Type3FontResolution`/`CanvasNetPdf-PdfDocument-Type3GlyphPainting`
below for its own resolution and glyph-painting tests.

#### CanvasNetPdf-PdfDocument-CompositeFontResolution: Type0/Identity-H Composite Fonts Resolve, Fail Closed Otherwise

**Tests**: `PdfDocument_Fonts_Type0_IdentityHCidFontType2_ResolvesEmbeddedFont`,
`PdfDocument_Fonts_Type0_CidToGidMapIdentity_UsesCidAsGid`,
`PdfDocument_Fonts_Type0_CidToGidMapStream_RemapsCidToGid`,
`PdfDocument_Fonts_Type0_CidToGidMapStream_OutOfRangeCid_MapsToNotdef`,
`PdfDocument_Fonts_Type0_NonIdentityHEncoding_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_Type0_CidFontType0_NoFontFile3_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type0_CidFontType0_OpenTypeCff_ResolvesEmbeddedFont`,
`PdfDocument_Fonts_Type0_CidFontType0_NonStandardCidToGidMap_IsIgnored`,
`PdfDocument_Fonts_Type0_CidFontType0_Widths_WArrayIndividualForm_DeterminesAdvance`,
`PdfDocument_Fonts_Type0_CidFontType0_NonOpenTypeFontFile3Subtype_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_Type0_CidFontType0_MissingFontFile3Subtype_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_Type0_CidFontType0_CidKeyedCff_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type0_MissingDescendantFonts_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type0_DescendantFontsNotSingleElement_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type0_NoEmbeddedFontFile_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type0_Widths_DwDefault1000_DeterminesAdvance`,
`PdfDocument_Fonts_Type0_Widths_WArrayIndividualForm_DeterminesAdvance`,
`PdfDocument_Fonts_Type0_Widths_WArrayRangeForm_DeterminesAdvance`,
`PdfDocument_Fonts_Type0_Widths_MalformedWArray_ThrowsInvalidDataException`,
`PdfDocument_ShowText_Type0_OddByteLengthString_ThrowsInvalidDataException`,
`PdfDocument_ShowText_Type0_TwoByteCodes_ShowsEachGlyphAtCorrectPosition`,
`CanvasNetPdf_SystemIntegration_RenderType0CompositeFont_PaintsExpectedGlyphInk`,
`CanvasNetPdf_SystemIntegration_RenderCidFontType0CompositeFont_PaintsExpectedGlyphInk`

Builds a `/Subtype /Type0`/`/Encoding /Identity-H` font dictionary naming a single
`/Subtype /CIDFontType2` descendant font with an embedded `/FontFile2`, asserting resolution
succeeds and the embedded font is what gets used. Declares a descendant `/CIDToGIDMap` of the
name `/Identity`, asserting a shown CID maps to the identical glyph index; separately declares a
`/CIDToGIDMap` stream remapping a CID to a different glyph index, asserting the remapped (not
identical) glyph index is used, and that a CID beyond the stream's own table length maps to glyph
`0`/`.notdef`. A `[Theory]` declares `/Encoding` as an arbitrary other name and, separately,
`/Identity-V`, asserting `Codecs.UnsupportedImageFeatureException` in both cases. Declares a
`/Subtype /CIDFontType0` descendant whose `/FontDescriptor` has only `/FontFile2` (no
`/FontFile3`), asserting `InvalidDataException` (not `UnsupportedImageFeatureException` - the
subtype itself is now supported; only the missing embedded font program fails). Builds a
synthetic, non-CID-keyed, `/OpenType`-wrapped CFF `/FontFile3` (via `SyntheticFontBuilder.Cff`)
on a `/CIDFontType0` descendant, asserting resolution succeeds and real glyph ink paints via
identity CID-to-glyph-index (Phase 12); separately declares a non-standard `/CIDToGIDMap
/Identity` entry on such a descendant, asserting it is ignored (the identity map is used
regardless); separately proves the same `/DW`/`/W` width-resolution logic applies identically to
a `/CIDFontType0` descendant. A `[Theory]` declares the `/FontFile3` stream's own `/Subtype` as
`/Type1C` and, separately, `/CIDFontType0C` (a bare CFF stream, no SFNT wrapper), asserting
`Codecs.UnsupportedImageFeatureException` in both cases; a further test omits the `/Subtype` key
from the `/FontFile3` stream entirely, asserting the same exception (a missing `/Subtype` is not
guessed as `/OpenType`). Builds an `/OpenType`-wrapped `/FontFile3` whose embedded CFF program's
Top DICT declares `ROS` (CID-keyed CFF, via `SyntheticFontBuilder.Cff(..., includeRos: true)`),
asserting `InvalidDataException` (surfaced uncaught from `Fonts.CffTable.Parse`'s own existing
CID-keyed rejection, with no new translation code in the `Pdf` subsystem). Declares a Type0 font
dictionary with no `/DescendantFonts` and, separately, a `/DescendantFonts` array with two
elements, asserting `InvalidDataException` in both cases. Declares a descendant font with no
embedded `/FontFile2` (still `/CIDFontType2`), asserting `InvalidDataException` (not
`UnsupportedImageFeatureException` - a composite font has no fallback substitution path).
Declares a descendant font with no `/W` array, asserting the shown code's advance matches the
default `/DW` of `1000`; separately declares a `/W` array using the `c [w1 w2 ... wn]`
individual-width sub-form and, separately, the `cFirst cLast w` range sub-form, asserting the
shown code's advance matches the declared width in each case; declares a malformed `/W` array
shape, asserting `InvalidDataException`. Shows an odd-byte-length string against a composite
font via `Tj`, asserting `InvalidDataException`. Shows two 2-byte codes via `Tj`, asserting each
glyph is painted at the position its own declared/default width determines. The first
end-to-end system-integration test opens a hand-authored fixture
(`text-composite-truetype-identity-h.pdf`) declaring a real embedded Open Sans descendant font
with a deliberately non-identity `/CIDToGIDMap`, independently reloads the same font, and asserts
specific stroke/counter/corner pixels - proving the full 2-byte Identity-H code → CID → GID (via
the non-identity `/CIDToGIDMap`) → TrueType glyph outline → painted-pixel pipeline end to end.
The second (Phase 12) opens an entirely synthetic fixture
(`text-composite-cff-cidfonttype0-identity-h.pdf`) whose `/CIDFontType0` descendant embeds a
synthetic, non-CID-keyed, `/OpenType`-wrapped CFF program with no `/CIDToGIDMap` declared, and
asserts real glyph ink paints at the analytically-known position of its hand-designed square
glyph - proving the same end-to-end pipeline for the `/CIDFontType0` shape.

#### CanvasNetPdf-PdfDocument-Type1FontResolution: Embedded Classic Type 1 FontFile Resolves, Fails Closed Otherwise

**Tests**: `PdfDocument_Fonts_Type1_DispatchesToSimpleFontResolution_PaintsGlyphInk`,
`PdfDocument_Fonts_Type1_EmbeddedFontFileTakesPriorityOverFallback`,
`PdfDocument_Fonts_Type1_NoFontFile_ResolvesViaFallback`,
`PdfDocument_Fonts_Type1_FontFileMissingLength1_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type1_FontFileMissingLength2_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type1_FontFileNonNumericLength2_ThrowsInvalidDataException`,
`PdfDocument_Load_TextEmbeddedType1FontFixture_PaintsVisibleGlyphInk`,
`PdfDocument_Load_TextStandard14Type1NoFontFileFixture_PaintsVisibleSubstituteGlyphInk`,
`CanvasNetPdf_SystemIntegration_RenderEmbeddedType1Font_PaintsExpectedGlyphInk`

Builds a `/Subtype /Type1` font dictionary with an embedded, entirely synthetic, classic
PostScript `/FontDescriptor/FontFile` program (via `SyntheticFontBuilder.Type1`, declaring
`.notdef`/`space`/`A` glyphs, `A` a filled square outline), asserting the embedded font's own
square glyph paints at the position its font-design-space coordinates and text-space
placement determine - proving `LoadType1Font`'s decode/`Fonts.TrueTypeFont.LoadType1`/
`ResolveEncoding`-via-`CodepointToStandardGlyphName` pipeline resolves end to end. A second test
builds the same embedded program under a deliberately unmatched `/BaseFont` family name,
asserting the embedded font's own known glyph shape (not a fallback font's differently-shaped
glyph) is what gets painted - proving an embedded `/FontFile` always wins over fallback
substitution, exactly like the `/FontFile2` precedent. A `/Subtype /Type1` font dictionary with a
recognized Standard-14 `/BaseFont` and no `/FontFile`/`/FontFile2`/`/FontFile3` at all asserts
resolution succeeds via `ResolveFallbackFont` and paints visible substitute glyph ink, mirroring
the `/TrueType` equivalent in `CanvasNetPdf-PdfDocument-FontFallback`. As of Phase C, a `/Subtype
/Type1` descriptor declaring only `/FontFile3` (no `/FontFile`/`/FontFile2`) no longer fails
closed here at all; see `CanvasNetPdf-PdfDocument-Type1CFontResolution` immediately below for its
own resolution and fail-closed cases. An embedded `/FontFile` stream declaring only `/Length2` (no
`/Length1` at all) asserts
`InvalidDataException`; separately, a `BuildEmbeddedType1FontResources(omitLength2: true)`
stream declaring only `/Length1` asserts `InvalidDataException` too; separately, a stream
declaring a non-numeric `/Length2` value (`/NotANumber`) asserts `InvalidDataException` as well -
proving all three malformed-length cases fail closed, and that both lengths are read from the
`/FontFile` stream's own dictionary (never the descriptor's). Two fixture-conformance tests open
the new hand-authored, entirely synthetic `text-embedded-type1-font.pdf` and
`text-standard14-type1-no-fontfile.pdf` fixtures (see `PdfFixtures\README.md`) and assert visible
painted ink from each. The end-to-end system-integration test opens
`text-embedded-type1-font.pdf` and asserts the analytically-known painted-pixel position of its
hand-designed square glyph (matching `CanvasNetPdf-PdfDocument-CompositeFontResolution`'s own
`CIDFontType0` system-integration test's pixel-assertion convention exactly, since both fixtures
share the identical glyph design/placement), plus fully-transparent canvas corners.

#### CanvasNetPdf-PdfDocument-Type1CFontResolution: Embedded Bare Type1C/CFF FontFile3 Resolves, Fails Closed Otherwise

**Tests**: `PdfDocument_Fonts_Type1_FontFile3Type1C_ResolvesEmbeddedFont_PaintsGlyphInk`,
`PdfDocument_Fonts_Type1_FontFile3NonType1CSubtype_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_Type1_FontFile3MissingSubtype_ThrowsUnsupportedImageFeatureException`

Builds a `/Subtype /Type1` font dictionary with an embedded, entirely synthetic, bare Type1C/CFF
`/FontDescriptor/FontFile3` stream (via `SyntheticFontBuilder.Cff`, declaring a `/Subtype
/Type1C` stream dictionary, a custom charset mapping a filled-square `A` glyph, and no SFNT/
OpenType wrapper of any kind), asserting the embedded font's own square glyph paints at the
position its font-design-space coordinates and text-space placement determine - proving
`LoadType1CFont`'s decode/`Fonts.TrueTypeFont.LoadType1C`/`ResolveEncoding`-via-
`CodepointToStandardGlyphName` pipeline resolves end to end, reached only because `/FontFile` is
absent (`LoadType1Font`'s own loader never runs). A `[Theory]` declares the `/FontFile3` stream's
own `/Subtype` as `/OpenType` and, separately, `/CIDFontType0C`, asserting
`Codecs.UnsupportedImageFeatureException` with `Feature ==
"pdf-font-fontfile3-subtype-{subtype}"` in both cases - an SFNT-wrapped CFF stream is rejected
here exactly as firmly as any other non-`Type1C` value, since this simple-font path never
attempts to unwrap an SFNT container the way the composite `CIDFontType0` path does. A further
test omits `/Subtype` from the `/FontFile3` stream entirely, asserting the same exception with
`Feature == "pdf-font-fontfile3-subtype-missing"` (a missing `/Subtype` is not guessed as
`Type1C`), mirroring `CanvasNetPdf-PdfDocument-CompositeFontResolution`'s own `/FontFile3`-
`/Subtype`-validation precedent for `/CIDFontType0`.

#### CanvasNetPdf-PdfDocument-Type3FontResolution: Type3 Fonts Resolve Required Fields, Fail Closed on Malformed Ones

**Tests**: `PdfDocument_Fonts_Type3_MinimalFont_DifferencesAndCharProcs_PaintsGlyphInk`,
`PdfDocument_Fonts_Type3_Widths_ScaledViaFontMatrix_DeterminesAdvance`,
`PdfDocument_Fonts_Type3_UnmappedCode_NoDifferencesEntry_PaintsNothingButAdvances`,
`PdfDocument_Fonts_Type3_MappedGlyphNameAbsentFromCharProcs_PaintsNothingButAdvances`,
`PdfDocument_Fonts_Type3_MissingFontMatrix_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type3_MissingCharProcs_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type3_FontMatrixWrongArrayLength_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type3_FontMatrixNonNumberEntry_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type3_CharProcsNotADictionary_ThrowsInvalidDataException`

Builds a minimal synthetic `/Subtype /Type3` font (via the new `BuildType3FontResources` helper,
mirroring `BuildEmbeddedType1CFontResources`'s own established pattern) declaring a single glyph
(code `65`/`'A'`, mapped via an inline `/Encoding`/`/Differences` entry to glyph name `/Square`,
whose `/CharProcs` content stream paints a filled design-space rectangle) and asserts `Tj` paints
that rectangle's ink at the device location its default `/FontMatrix`
(`[0.001 0 0 0.001 0 0]`) scales it to - the identical pixel assertion the embedded Type1/
TrueType font tests already use for the same design-space rectangle, proving `BuildResolvedFont`'s
`/Type3` dispatch and `PaintType3Glyph`'s basic glyph-procedure execution end to end. A second
test shows two glyphs in one `Tj` under a deliberately non-conventional `/FontMatrix`
(`[0.0025 0 0 0.001 0 0]`) and a `/Widths` entry of `1000`, asserting the second glyph's device
position reflects the first glyph's `/FontMatrix`-scaled advance (`1000 * 0.0025 * 20pt = 50`
units) and explicitly asserting the second glyph does *not* appear at the position a
`/1000`-divided-width convention (the simple/composite-font convention) would have produced
instead - proving `ResolvedType3Font.Resolve`'s width computation is genuinely `/FontMatrix`-
scaled, not merely accepting and ignoring `/FontMatrix` in favor of the ordinary `/1000`
convention. Two further tests each show an unmapped glyph (one via a character code entirely
absent from `/Differences`, one via a code mapped to a glyph name absent from `/CharProcs`)
followed by a second, correctly-mapped glyph, asserting no exception is thrown, nothing paints
for the unmapped glyph, and the second glyph's device position still reflects the unmapped code's
own declared `/Widths` advance - proving both missing-glyph leniency cases advance correctly
without painting. Two further tests each build a `/Subtype /Type3` font dictionary missing (one)
`/FontMatrix` or (the other) `/CharProcs` entirely, asserting `InvalidDataException` in both
cases - proving these two required fields fail closed, unlike every other resolved font kind's
embedded-font-absent fallback-substitution path (a Type 3 font has no such fallback at all). A
further `[Theory]` test supplies a `/FontMatrix` array of the wrong length (5 or 7 elements
instead of exactly 6), and a companion test supplies a `/FontMatrix` array whose entries include a
name (`/Foo`) in place of a number, each asserting `InvalidDataException` - distinct from the
"entirely absent" case above, proving `ReadFontMatrix`'s own length and per-entry-type validation
both fail closed independently. A final test supplies `/CharProcs 42` (a number, not a
dictionary), asserting `InvalidDataException` - distinct from the "entirely absent" `/CharProcs`
case above, proving `BuildResolvedType3Font`'s own `charProcs.Kind != PdfKind.Dictionary` check
fails closed.

#### CanvasNetPdf-PdfDocument-Type3GlyphPainting: Glyph Procedures Execute Recursively and Isolate Graphics State

**Tests**: `PdfDocument_Fonts_Type3_NonDefaultFontMatrix_ScalesGlyphGeometry`,
`PdfDocument_Fonts_Type3_GlyphProc_GraphicsStateIsolated_DoesNotLeakOut`,
`PdfDocument_Fonts_Type3_D0Operator_ParsedButDoesNotAffectAdvanceWidth`,
`PdfDocument_Fonts_Type3_NoOwnResources_FallsBackToOuterPageResources`,
`PdfDocument_Fonts_Type3_SelfReferencingGlyphProc_ExceedsMaxNestingDepth_ThrowsInvalidDataException`,
`PdfDocument_Fonts_Type3_RenderMode3_SkipsGlyphProcedureButStillAdvances`,
`PdfDocument_Fonts_Type3_D1Operator_ParsedButDoesNotAffectAdvanceWidth`,
`PdfDocument_Fonts_Type3_StrayD0D1OutsideGlyphProc_NoExceptionNoEffect`,
`PdfDocument_Fonts_Type3_OwnResourcesTakePrecedenceOverPageResources_UsesFontResources`

Builds the same single-glyph filled-rectangle `/Subtype /Type3` font as
`CanvasNetPdf-PdfDocument-Type3FontResolution`'s own minimal-font test, but under a deliberately
non-default, anisotropic `/FontMatrix` (`[0.002 0 0 0.0015 0 0]` - different horizontal and
vertical scales, neither of which is the conventional `0.001`), asserting the rectangle paints at
the device location this `/FontMatrix` (empirically verified against the implementation's own
rasterization) scales it to, and explicitly asserting a device pixel that lies inside the region
the rectangle *would* occupy under the conventional (but, for this font, wrong) `0.001`
`/FontMatrix` scale - yet outside this font's own correctly-scaled region - is left blank: the
highest-risk proof in this feature's test suite, per the implementation plan, since it
distinguishes "`/FontMatrix` genuinely consulted for glyph-matrix composition" from "`/FontMatrix`
accepted but silently ignored in favor of a hardcoded assumption". A further test uses a glyph
procedure that applies `2 0 0 2 0 0 cm 1 0 0 rg` before painting its own rectangle (under an
identity `/FontMatrix`, so the glyph's own content-stream coordinates map 1:1 onto the same
device space a Form XObject's own BBox-space coordinates would), then asserts the page's own,
separately-painted rectangle - painted immediately after `Tj` returns, in the page's own default
black fill color, at its own normal unscaled position - is unaffected by the glyph procedure's
`cm`/`rg` mutations, mirroring
`PdfDocument_Images_DoOperator_NestedFormXObjects_RestoresGraphicsStateAfterReturn`'s own Form-
XObject state-isolation proof applied to `PaintType3Glyph`'s re-entrant execution instead. A
further test declares a glyph procedure beginning `2000 0 d0` (a `wx` operand wildly different
from the font's own declared `/Widths` entry) before painting, shows the same glyph twice in one
`Tj`, and asserts the second glyph's device position reflects the font's `/Widths`-declared
advance, not a d0-driven one - proving `OpType3SetWidth` validates and discards its operands
without feeding `wx` back into layout. A final test declares a glyph procedure that invokes a
Form XObject (`/Fm0`) declared only in the invoking page's own `/Resources` (the Type 3 font
itself declares no `/Resources` of its own), asserting the Form's own content paints at the
expected glyph-matrix-transformed location - proving `PaintType3Glyph` falls back to the
invoking content stream's own `/Resources` exactly like a `/Subtype /Form` XObject with no
`/Resources` of its own already does.

A further test declares a glyph procedure for code 65 that itself re-shows code 65 via a bare
`(A) Tj` (legal, since the nested graphics-state clone inherits the outer `Tf`-selected font),
asserting `InvalidDataException` once the fixed maximum Type3 glyph-procedure nesting depth is
exceeded - proving the bounded-recursion guard fails closed rather than overflowing the call
stack or hanging. A companion test selects render mode `3` (invisible) via `Tr` before showing two
glyphs, then switches back to render mode `0` before showing a third, asserting no ink paints at
either invisible glyph's own would-be-painted device location while the third, visible glyph
paints at the origin fully advanced by both invisible glyphs' declared widths - proving
`ShowGlyph`'s render-mode-3 guard skips `PaintType3Glyph` entirely while the shared, unconditional
advance logic still runs. A further test declares a glyph procedure using the `d1` operator (6
operands: `wx wy llx lly urx ury`) with a `wx` wildly different from the font's own declared
`/Widths` entry, mirroring the existing `d0` test's own structure, and asserts the second of two
shown glyphs lands at the `/Widths`-correct (not `d1`-driven) advance - proving `d1`'s own
6-operand path is parsed and discarded exactly like `d0`'s. A companion test issues a stray
`d0`/`d1` pair directly in an ordinary, non-Type3 page content stream (no font ever selected),
asserting no exception and that a subsequent filled rectangle paints normally - proving
`ExecuteOperators`' shared `d0`/`d1` dispatch is unconditional, not gated on "currently inside a
Type3 glyph procedure". A final test declares both the Type3 font's own `/Resources` and the
invoking page's own `/Resources` with an XObject of the same name (`/Fm0`) painting different
colors, asserting the glyph procedure's `/Fm0 Do` paints the font's own color (not the page's) -
proving `PaintType3Glyph`'s `_resources = font.Resources ?? _resources` fallback's non-null
branch (the font's own `/Resources` taking precedence) is exercised, distinct from the preceding
test's null-branch (fallback) proof.

#### CanvasNetPdf-PdfDocument-ToUnicodeCMap: /ToUnicode CMap Resolves to a Code-to-Codepoint Map

**Tests**: `PdfDocument_Fonts_ToUnicode_Absent_ResolvesNull`,
`PdfDocument_Fonts_ToUnicode_BfChar_MapsSingleCode`,
`PdfDocument_Fonts_ToUnicode_BfRangeHexForm_MapsConsecutiveCodes`,
`PdfDocument_Fonts_ToUnicode_BfRangeArrayForm_MapsEachCodeIndividually`,
`PdfDocument_Fonts_ToUnicode_UnsupportedArrayDestinationBfRange_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_ToUnicode_UnsupportedOperator_ThrowsUnsupportedImageFeatureException`

Builds a font dictionary with no `/ToUnicode` entry at all, asserting `ResolveToUnicodeMap`
resolves a null map. Builds a `/ToUnicode` CMap stream (wrapped in the standard Adobe CMap/
PostScript resource-management boilerplate, proving that boilerplate is tolerated rather than
merely a bare `bfchar`/`bfrange` block) declaring a single `beginbfchar`/`endbfchar` pair,
asserting the source code resolves to its destination's codepoint. Declares a
`beginbfrange`/`endbfrange` triple with a single hex-string destination, asserting consecutive
codes across the declared `[srcLo, srcHi]` range resolve to consecutive incrementing codepoints
starting at that destination's codepoint. Declares a `beginbfrange`/`endbfrange` triple with an
array-of-hex-strings destination, asserting each code in the range resolves to its own
corresponding array element. Declares a `beginbfrange`/`endbfrange` triple whose destination
array contains a nested array (the out-of-scope CIDSystemInfo-style "array of arrays"
destination sub-form), asserting `Codecs.UnsupportedImageFeatureException` (unlike the in-scope
array-of-hex-strings destination form, which succeeds). A `[Theory]` declares each of the
explicitly out-of-scope `usecmap`/`cidrange`/`cidchar` operators, asserting
`Codecs.UnsupportedImageFeatureException` in every case, rather than those operators being
silently ignored like the CMap's own PostScript resource-management wrapper keywords.

#### CanvasNetPdf-PdfDocument-FontFallback: Non-Embedded TrueType Fonts Are Substituted, Other Symbolic Fonts Fail Closed

**Tests**: `PdfDocument_BuildResolvedFont_Standard14NoEmbeddedFont_ResolvesViaFallback`,
`PdfDocument_BuildResolvedFont_NonStandard14FlagsOnlyUnmatchedFamily_ResolvesViaBundledFallback`,
`PdfDocument_BuildResolvedFont_SymbolicFlagWithoutEmbeddedFont_ThrowsSymbolicNotEmbeddedException`,
`CanvasNetPdf_SystemIntegration_RenderStandard14HelveticaWithoutEmbeddedFont_PaintsVisibleGlyphInk`,
`CanvasNetPdf_SystemIntegration_RenderOtherSymbolicFontWithoutEmbeddedFont_ThrowsUnsupportedImageFeatureException`

Builds a `/Subtype /TrueType` font dictionary with a recognized Standard-14 `/BaseFont` (for
example `Helvetica`) and no `/FontFile2`, asserting `BuildResolvedFont` returns a resolved font
whose underlying `Fonts.TrueTypeFont` is non-null rather than throwing. Builds a font dictionary
with a `/BaseFont` unmatched by any Standard-14 name or any plausible system font, relying only on
`/FontDescriptor/Flags`, asserting resolution still succeeds via the bundled Liberation fallback.
Builds a font
dictionary whose `/FontDescriptor/Flags` declares the `Symbolic` bit without the `Nonsymbolic`
bit (and whose `/BaseFont` is deliberately not `Symbol`/`ZapfDingbats`, so it cannot divert to the
Noto substitution path below), asserting `Codecs.UnsupportedImageFeatureException` with `Feature ==
"pdf-font-symbolic-not-embedded"` - the sole remaining fail-closed case this requirement covers,
since `Symbol`/`ZapfDingbats` themselves now resolve via
`CanvasNetPdf-PdfDocument-SymbolZapfDingbatsFallback` below instead. The two
`CanvasNetPdf_SystemIntegration_*` end-to-end tests render a full synthetic single-page PDF whose
font resource has no `/FontFile2`: the Standard-14 Helvetica case asserts at least one non-default
(non-transparent) pixel was painted somewhere in the glyph's expected device-space region
(a weak, OS-independent assertion, since the actual system/bundled font substituted varies by CI
operating system), and the other-symbolic-font case asserts the render throws
`Codecs.UnsupportedImageFeatureException` rather than silently painting an unrelated glyph shape -
a regression guard proving the new Symbol/ZapfDingbats substitution path did not loosen this
fail-closed policy for any other symbolic font.

#### CanvasNetPdf-PdfDocument-SymbolZapfDingbatsFallback: Symbol/ZapfDingbats Resolve via a Bundled Noto Union

**Tests**: `SymbolEncodingTable_RepresentativeSample_MapsToKnownCorrectCodepoints`,
`ZapfDingbatsEncodingTable_RepresentativeSample_MapsToKnownCorrectCodepoints`,
`PdfDocument_BuildResolvedFont_SymbolFont_ResolvesViaNotoSubstituteWithoutThrowing`,
`PdfDocument_BuildResolvedFont_ZapfDingbatsFont_ResolvesViaNotoSubstituteWithoutThrowing`,
`CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_PaintsVisibleGlyphInk`,
`CanvasNetPdf_SystemIntegration_NotoSansRegularBundledFont_ResolvesSymbolAlphaToNonzeroGlyphIndex`,
`BundledFonts_AllThreeNotoVariants_LoadSuccessfullyAndExposeExpectedName`

Two reflection-based unit tests (`PdfDocumentSymbolicEncodingTests`) spot-check representative,
known-correct entries of the new `SymbolEncodingTable`/`ZapfDingbatsEncodingTable` 256-entry
code-to-Unicode-codepoint tables, rather than re-asserting the entire table verbatim: Symbol's
`alpha` (code `0x61`) to U+03B1, `Alpha` (code `0x41`) to U+0391, the Private-Use-Area-fallback
glyph name `registerserif` (code `0xD2`) to the plain/generic U+00AE rather than a Private-Use-
Area codepoint, and the deliberately unmapped extensible-delimiter-piece glyph name `radicalex`
(code `0x60`) to `0`; ZapfDingbats' `space` (code `0x20`) to U+0020, `a1` (code `0x21`) to U+2701,
and the documented, accepted-fidelity-limitation uncovered case `a120` (circled digit one, code
`0xAC`) to U+2460 (the table itself still defines this mapping - only the bundled substitute font
does not cover this particular codepoint). Two unit-level `PdfDocument_BuildResolvedFont_*` tests
build `/BaseFont /Symbol` and, separately, `/BaseFont /ZapfDingbats` font dictionaries with no
embedded `/FontFile2` and asserting the render no longer throws (replacing the prior fail-closed
tests this requirement moved from `CanvasNetPdf-PdfDocument-FontFallback` above). The
`CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_PaintsVisibleGlyphInk`
end-to-end test renders a synthetic single-page PDF with a `/BaseFont /Symbol` font resource, no
`/FontFile2`, and no `/FontDescriptor` entries at all (PDF 32000-1 §9.6.2.2 permits an entirely
absent/empty descriptor), asserting at least one visibly-painted pixel - proving the fallback
path genuinely renders rather than merely not throwing. The
`CanvasNetPdf_SystemIntegration_NotoSansRegularBundledFont_ResolvesSymbolAlphaToNonzeroGlyphIndex`
test proves the full production round-trip by loading the exact same embedded resource
(`NotoSans-Regular.ttf`) `ResolveSymbolicNotoFallback` itself loads, and asserting Symbol code
`0x61` ('alpha', which the Symbol encoding table maps to Unicode U+03B1) resolves to a nonzero,
valid glyph index via `Fonts.TrueTypeFont.GetGlyphIndex` - proving the bundled substitute font
genuinely carries a real Greek alpha glyph, not merely that the pipeline declines to throw.
Finally, `BundledFonts_AllThreeNotoVariants_LoadSuccessfullyAndExposeExpectedName`
(`BundledNotoFontsTests`, in `DemaConsulting.CanvasNet.Tests`) dogfoods all 3 new bundled Noto
embedded-resource font files through `Fonts.TrueTypeFont.Load`
(via `Fonts.SystemFontCatalog.LoadBundledFallbackCore`, the same embedded-resource-loading path
production code uses), proving each is a genuine, well-formed, loadable TrueType font exposing a
sane, expected family name - mirroring `BundledLiberationFontsTests`'s own theory-per-file
dogfooding shape for the 12 pre-existing Liberation files.

#### CanvasNetPdf-PdfDocument-FontEncoding: Base Encodings and Differences Overrides Resolve Correctly

**Tests**: `PdfDocument_Fonts_UnrecognizedEncoding_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Fonts_DefaultEncoding_IsWinAnsiEncoding`,
`PdfDocument_Fonts_MacRomanEncoding_DiffersFromWinAnsiEncoding`,
`PdfDocument_Fonts_Differences_OverridesBaseEncodingCode`,
`PdfDocument_Fonts_Differences_Absent_LeavesBaseEncodingCodeUnmapped`,
`PdfDocument_Fonts_Differences_UnrecognizedGlyphName_FallsBackToBaseEncoding`,
`PdfDocument_Fonts_Differences_FfFfiFflLigatureNames_DoNotThrow`,
`PdfDocument_Fonts_Differences_NacuteName_DoesNotThrow`,
`PdfDocument_Fonts_Differences_NameBeforeStartingCode_ThrowsInvalidDataException`,
`PdfDocument_Fonts_StandardEncoding_DiffersFromWinAnsiEncoding`,
`PdfDocument_Fonts_StandardEncoding_Absent_DefaultWinAnsiDoesNotPaintQuoteright`

Selects a font declaring an unrecognized `/Encoding` base-encoding name, asserting
`Codecs.UnsupportedImageFeatureException`. Selects a font with no `/Encoding` key at all and,
separately, a font explicitly declaring `/MacRomanEncoding`, showing the byte code `0xE0` (which
diverges between the two base encodings - `WinAnsiEncoding` maps it to U+00E0, `MacRomanEncoding`
to U+2021) through a synthetic font whose `cmap` maps only U+00E0 to a real glyph, asserting the
default-encoding case paints ink and the MacRoman case does not (resolving to `.notdef`/no
paint) - proving the correct table is actually consulted, not merely that some table exists.
Declares an `/Encoding/Differences` array remapping a code to glyph name `agrave` (U+00E0) and
shows that code through a font whose `cmap` maps only U+00E0, asserting ink is painted where the
base encoding alone would not have resolved to that codepoint; separately shows a code with no
`/Differences` override present, asserting it keeps its base-encoding mapping unchanged. Declares
a `/Differences` array containing an unrecognized glyph name, asserting the affected code is
tolerated and falls back to its base-encoding mapping rather than rejecting the document (ink
still paints via the embedded font's own `cmap` mapping for the unchanged codepoint); a further
test declares a
`/Differences` array naming the `ff`/`ffi`/`ffl` Latin ligature glyphs (found via a real-world
pdfLaTeX Computer Modern font - previously only `fi`/`fl` were present in `StandardGlyphNames`),
asserting no exception; separately declares a
`/Differences` array beginning with a glyph name before any starting code number, asserting
`InvalidDataException` for that malformed array shape too. A further test declares a
`/Differences` array naming the `nacute` (Polish/Czech "ń") Latin Extended-A accented letter
(found via the same real-world pdfLaTeX Computer Modern font), asserting no exception. As of
Phase B, a font explicitly
declaring `/StandardEncoding` and showing byte code `0x27` (which diverges between the two base
encodings - `StandardEncoding` maps it to U+2019 "quoteright", `WinAnsiEncoding` maps it to
U+0027 "quotesingle" instead) through a synthetic font whose `cmap` maps only U+2019 to a real
glyph, asserts ink is painted; a companion test proves that without an explicit `/Encoding` entry
(the default `/WinAnsiEncoding` applies instead), the same code paints nothing against the same
font - proving `/StandardEncoding` and `/WinAnsiEncoding` genuinely resolve code `0x27`
differently, not merely that some encoding resolved.

#### CanvasNetPdf-PdfDocument-FontWidths: Advance-Width Resolution Follows the Documented Priority

**Tests**: `PdfDocument_Fonts_Widths_ExplicitEntry_DeterminesAdvance`,
`PdfDocument_Fonts_Widths_MissingWidthFallback_DeterminesAdvance`,
`PdfDocument_Fonts_Widths_FontOwnAdvanceFallback_DeterminesAdvance`

Shows two glyphs from a font declaring an explicit `/Widths` entry for the first code, asserting
the second glyph's painted device-x position matches the explicit width, not the font's own
metric. Declares a font whose `/Widths` array omits a shown code but whose `/FontDescriptor`
declares `/MissingWidth`, asserting the second glyph's position matches the `/MissingWidth`
value. Declares a font with neither `/Widths` nor `/MissingWidth` for a shown code, asserting the
second glyph's position matches the embedded font's own `GetAdvanceWidth`/`UnitsPerEm` metric.

#### CanvasNetPdf-PdfDocument-TextObjectState: BT/ET Reset Only Tm/Tlm; q/Q Save/Restore Text State

**Tests**: `PdfDocument_Text_BeginText_ResetsTextMatrixButPreservesFontAndTextState`,
`PdfDocument_Text_PushPopGraphicsState_RestoresFontSize`

Selects a font and size, moves the text position, ends the text object (`ET`), begins a new one
(`BT`) without re-selecting the font, and shows a glyph at a freshly-set position - asserting the
previously selected font/size is still in effect (painted ink appears, proving `Tf` was not lost)
while the text position reset to identity is honored (the glyph appears at the new position, not
offset by the first text object's position). Selects a font size, pushes the graphics state
(`q`), changes the font size, pops it (`Q`), and shows a glyph - asserting the glyph's painted
size matches the original (pre-`q`) font size, proving `q`/`Q` saves/restores text state exactly
like every other graphics-state parameter.

#### CanvasNetPdf-PdfDocument-TextStateOperators: Tc/Tw/Tz Apply Their Documented Spacing/Scaling Formulas

**Tests**: `PdfDocument_Text_Tc_AddsToGlyphAdvance`, `PdfDocument_Text_Tw_AppliesOnlyToCode32`,
`PdfDocument_Text_Tz_ScalesHorizontalShapeAndAdvance`,
`PdfDocument_ShowText_Type0_WordSpacingCode32_IsNotAppliedToCompositeFont`

Sets a nonzero `Tc` and shows two glyphs, asserting the second glyph's device-x position is
offset by the additional character spacing beyond its own advance width. Sets a nonzero `Tw` and
shows a string containing a code-32 (space) byte followed by a non-space byte, asserting only the
byte immediately after the space is offset by the word spacing (a non-space code is never
affected). Sets `Tz` to a value other than the default `100` and shows two glyphs, asserting both
each glyph's own painted horizontal shape (narrower/wider) and its advance are scaled
accordingly, while the vertical shape (unaffected by `Th`) is unchanged. Sets a large `Tw` against
a composite `/Type0`/`/Identity-H` font whose first 2-byte code numerically equals `32`, asserting
the following glyph lands at its ordinary (un-spaced) advance position rather than being displaced
far off-canvas - proving word spacing is gated on the font's code-byte-width, not merely the
decoded code's numeric value.

#### CanvasNetPdf-PdfDocument-Tf: Tf Resolves the Named Font Resource and Selects Its Size

**Tests**: `PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix`

Selects a font and size via `Tf`, then shows a glyph and asserts its painted device-pixel
position/size matches the composed text-rendering matrix formula independently re-derived from
the same font's own metrics (see this class's own pixel-math derivation, confirmed empirically
against the real rasterizer before being fixed into every position-dependent test's assertions).

#### CanvasNetPdf-PdfDocument-Tr: Tr Supports Fill/Stroke/Fill+Stroke/Invisible Modes, Fails Closed for Clip Modes

**Tests**: `PdfDocument_Text_RenderMode3_Invisible_DoesNotPaintGlyph`,
`PdfDocument_Text_RenderMode3_Invisible_StillAdvancesTextPosition`,
`PdfDocument_Text_RenderMode1_Stroke_PaintsStrokedOutlineNotSolidFill`,
`PdfDocument_Text_RenderMode2_FillAndStroke_PaintsBothFillAndStroke`,
`PdfDocument_Text_RenderMode1_StrokePattern_PaintsPatternStroke`,
`PdfDocument_Fonts_Type3_RenderMode1Or2_BehavesLikeRenderMode0`,
`PdfDocument_Text_RenderMode_UnsupportedDefinedMode_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Text_RenderMode_OutOfDefinedRange_ThrowsInvalidDataException`

Sets `Tr 3` (invisible) and shows a glyph, asserting no ink is painted at its expected position.
Sets `Tr 3`, shows a first glyph, then shows a second glyph with `Tr 0` (fill), asserting the
second glyph's position reflects the first (invisible) glyph's own advance - proving invisible
text still moves the text position. Sets `Tr 1` (stroke) with distinct fill/stroke colors and
shows a glyph, asserting a stroke-colored pixel appears along the outline's edge while the
glyph's geometric center (which fill would otherwise paint) remains unpainted - proving mode `1`
strokes without filling. Sets `Tr 2` (fill, then stroke) with distinct fill/stroke colors and
shows a glyph, asserting the center is filled and the edge is stroked on top of the fill,
matching the path-painting operators' own fill-then-stroke paint order. Sets `Tr 1` with a
`/Pattern` stroke color space resolving to a colored tiling pattern and shows a glyph, asserting
the stroked edge takes the pattern's own tile color rather than a flat stroke color - proving the
glyph stroke step shares the path-painting operators' own `/Pattern`-aware stroke-paint logic. A
`[Theory]` shows a Type3 glyph under `Tr 1` and `Tr 2` in turn, asserting its own content-stream
procedure's ink paints identically to `Tr 0` - proving Type3 glyphs (which have no outline and so
no fill/stroke distinction) are unaffected by the new mode-aware outline paint logic. A
`[Theory]` sets `Tr` to each of the defined clip modes (`4`/`5`/`6`/`7`) in turn, asserting
`Codecs.UnsupportedImageFeatureException` in every case; a separate `[Theory]` sets `Tr` to a
value outside the specification's defined `0`-`7` range, asserting `InvalidDataException`.

#### CanvasNetPdf-PdfDocument-TextPositioning: Td/TD/Tm/T* Compose the Text and Line Matrices Correctly

**Tests**: `PdfDocument_Text_Td_OffsetsLineMatrixNotLastTextMatrix`,
`PdfDocument_Text_TD_SetsLeadingToNegativeTy`, `PdfDocument_Text_Tm_ReplacesTextAndLineMatrix`,
`PdfDocument_Text_TStar_UsesCurrentLeading`

Issues two `Td` calls in sequence and shows a glyph after each, asserting the second glyph's
position reflects both displacements measured from the (unmoved) line matrix, not accumulated
relative to the first `Td`'s resulting text matrix. Issues `TD` and shows a glyph, then issues
`T*` (with no intervening `TL`) and shows a second glyph, asserting the second glyph's vertical
position reflects the leading `TD` implicitly set (`Leading = -ty`). Issues `Tm` with an explicit
matrix and shows a glyph, asserting its position matches that matrix directly (a replacement, not
a composition with any prior text/line matrix). Sets `TL` explicitly, issues `T*`, and shows a
glyph, asserting its position matches exactly `0 -TL Td`'s own effect.

#### CanvasNetPdf-PdfDocument-TextShowing: Tj/'/"/TJ Paint the Correct Glyphs at the Correct Positions

**Tests**: `PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix`,
`PdfDocument_Text_TJ_AppliesPositionAdjustments`,
`PdfDocument_Text_TJ_ArrayEntryNotStringOrNumber_ThrowsInvalidDataException`,
`PdfDocument_Text_QuoteOperator_MovesToNextLineThenShows`,
`PdfDocument_Text_DoubleQuoteOperator_SetsSpacingThenMovesAndShows`,
`CanvasNetPdf_SystemIntegration_PdfRender_EmbeddedTrueTypeFontText_PaintsGlyphStrokesNotCounters`

Shows a string via `Tj`, asserting the painted glyph's device-pixel position/size matches the
composed text-rendering matrix. Shows a `TJ` array containing a numeric position adjustment
between two strings, asserting the second string's glyphs are shifted by exactly that
adjustment's thousandths-of-text-space amount (scaled by `Tfs`/`Th`), beyond the preceding
glyph's own advance. A `TJ` array containing an element that is neither a string nor a number
asserts `InvalidDataException`. Issues `Td` then `'`, asserting the shown text is positioned
exactly as `T*` followed by `Tj` would place it. Issues `"` with explicit word/character spacing
operands, asserting both spacing parameters are applied to the subsequently shown text exactly as
setting `Tw`/`Tc` then issuing `'` would. The end-to-end system-integration test independently
proves the same glyph-painting pipeline against a real, hand-authored fixture embedding the real
`OpenSans-Regular.ttf` production font, re-deriving expected ink/counter/background pixel
positions from the font's own outline metrics rather than hardcoded numbers.

#### CanvasNetPdf-PdfDocument-TextErrorHandling: No-Font-Selected and Malformed-Operand Text Operators Fail Closed

**Tests**: `PdfDocument_Text_ShowText_NoFontSelected_ThrowsInvalidDataException`,
`PdfDocument_Text_MalformedOperandCount_ThrowsInvalidDataException`

A `[Theory]` shows text via each of `Tj`/`'`/`"` with no preceding `Tf` call, asserting
`InvalidDataException` in every case. A large `[Theory]` supplies a malformed operand count/type
to each text operator in turn (including a trailing garbage-operand `ET`, a wrong-arity `Tf`, and
out-of-range operand counts for `Td`/`Tm`), asserting `InvalidDataException` in every case,
matching every other operator family's own established convention.

#### CanvasNetPdf-PdfDocument-FunctionType0: /FunctionType 0 Sampled Function Resolves and Evaluates Correctly

**Tests**: `PdfDocument_Functions_Type0_InputOutsideDomain_ClampsToBoundary`,
`PdfDocument_Functions_Type0_EncodeAbsent_DefaultsToZeroToSizeMinusOne`,
`PdfDocument_Functions_Type0_ExplicitEncode_MapsDomainToCustomSampleIndexRange`,
`PdfDocument_Functions_Type0_Decode_MapsRawSampleToCustomOutputRange`,
`PdfDocument_Functions_Type0_NonIntegerSampleIndex_InterpolatesBetweenAdjacentSamples`,
`PdfDocument_Functions_Type0_EightBitSamples_EvaluatesCorrectly`,
`PdfDocument_Functions_Type0_SixteenBitSamples_EvaluatesCorrectly`,
`PdfDocument_Functions_Type0_UnsupportedFunctionType_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Functions_Type0_MultiInputDomain_ThrowsUnsupportedImageFeatureException`

Every test resolves a hand-built `/FunctionType 0` function stream (an indirect object appended to
a minimal single-page document, mirroring the `/ToUnicode` CMap tests' own "build a minimal
document, open it, resolve a reference into it" convention) via `ResolveFunction`, then calls
`SampledFunction.Evaluate` directly (unit-level, not through the content-stream interpreter - this
phase's own `Evaluate` is resolved-but-unconsumed groundwork, see the design documentation).
Asserts an input below/above `/Domain` clamps to the domain boundary before sample-index mapping.
Asserts an absent `/Encode` defaults to `[0, Size - 1]`, correctly mapping a domain midpoint to a
fractional sample index. Asserts an explicit `/Encode` overrides that default, selecting a
different (non-zero-valued) pair of samples than the default would have selected. Asserts
`/Decode` linearly remaps a raw sample value into its own declared range, distinct from (though
still clipped to) a wider `/Range`. Asserts a sample-index position that falls between two samples
linearly interpolates between them. Asserts both 8-bit and 16-bit `/BitsPerSample` widths read
their raw sample values correctly (16-bit via 2-byte big-endian packing). A `[Theory]` resolves a
`/FunctionType` `2`/`3`/`4` function, asserting `Codecs.UnsupportedImageFeatureException` (feature
`pdf-functiontype-{n}`) in every case. Resolves a 2-input `/FunctionType 0` function (a 4-element
`/Domain`), asserting `Codecs.UnsupportedImageFeatureException` (feature
`pdf-function-multiinput`).

#### CanvasNetPdf-PdfDocument-FunctionType2And3: /FunctionType 2/3 Evaluate Correctly, /FunctionType 4 Fails Closed

**Tests**: `PdfDocument_Functions_Type2_Exponential_EvaluatesExpectedInterpolatedOutput`,
`PdfDocument_Functions_Type3_Stitching_EvaluatesExpectedSubFunctionOutput`,
`PdfDocument_Functions_Type4_PostScriptCalculator_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Functions_Type3_Stitching_NestingDepthExceeded_ThrowsInvalidDataException`

Every test resolves a hand-built function stream via the new `ResolveFunctionGeneric` entry point
(through a new `ResolveTestFunctionGeneric` helper mirroring `ResolveTestFunction`'s own "build a
minimal document, open it, resolve a reference into it" convention), then calls `Evaluate`
directly. A `[Theory]` resolves a `/FunctionType 2` function with `C0=[0]`/`C1=[1]` at both a
linear (`N=1`) and a quadratic (`N=2`) exponent, asserting the literal spec formula's expected
interpolated output at a known input. A `[Theory]` resolves a `/FunctionType 3` stitching
function with 2 `/FunctionType 2` sub-functions split at a single `/Bounds` partition point,
asserting the correct sub-function is selected (and its own `/Encode` remap applied) for inputs
falling in each of the 2 partitions. A final test resolves a `/FunctionType 4` function through
`ResolveFunctionGeneric`, asserting `Codecs.UnsupportedImageFeatureException` (feature
`pdf-functiontype-4`) is still thrown (regression guard - `/FunctionType 4` remains unsupported
even through the new generic resolver). A final regression test resolves a self-referencing
`/FunctionType 3` object (its own `/Functions` array references itself) through
`ResolveFunctionGeneric`, asserting `InvalidDataException` is thrown once the new
`MaxFunctionRecursionDepth` (32) bound is reached, rather than the process' call stack being
exhausted.

#### CanvasNetPdf-PdfDocument-PatternColorSpace: /Pattern Color Space and scn/SCN Operands, Fail Closed Otherwise

**Tests**: `PdfDocument_Color_PatternColorSpace_Cs_ResolvesToPatternFamily`,
`PdfDocument_Color_PatternColorSpace_ArrayWithBase_ResolvesPatternBase`,
`PdfDocument_Color_PatternColorSpace_TooManyArrayElements_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Color_Scn_ColoredPatternOperand_NameAlone_ResolvesPattern`,
`PdfDocument_Color_Scn_UncoloredPatternOperand_WrongComponentCount_ThrowsInvalidDataException`,
`PdfDocument_Color_Scn_PatternOperand_MissingTrailingName_ThrowsInvalidDataException`,
`PdfDocument_Color_Scn_PatternOperand_UndeclaredPatternName_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Color_Scn_PatternOperand_UnsupportedPatternType_ThrowsUnsupportedImageFeatureException`

Every test builds a minimal single-page PDF (via `BuildSinglePagePdfWithResources`) with a
content stream selecting the `/Pattern` color space (`cs`, or a named `/CS0` resource entry for
the 2-element array form, since `cs`/`CS` only accept a single `Name` operand - an inline array
is not legal content-stream syntax), then calls `scn` and renders. Asserts bare `/Pattern cs`
resolves without throwing. Asserts a declared `/DeviceRGB` base space accepts exactly 3 leading
numeric operands before the pattern name and resolves successfully. Asserts a 3-element array
color space throws `Codecs.UnsupportedImageFeatureException` (feature `pdf-colorspace-Pattern`).
Asserts a bare pattern name (no base space, 0 leading numeric operands) resolves successfully.
Asserts a declared base space with the wrong leading numeric operand count throws
`InvalidDataException`. Asserts an all-numeric-operand `scn` call (no trailing pattern name)
throws `InvalidDataException`. Asserts an undeclared pattern name throws
`Codecs.UnsupportedImageFeatureException` (feature `pdf-pattern-not-declared`). Asserts a
`/PatternType 3` pattern throws `Codecs.UnsupportedImageFeatureException` (feature
`pdf-pattern-type-3`, a regression guard against any future `/PatternType` other than 1/2).

#### CanvasNetPdf-PdfDocument-ShadingPatternFill: Axial/Radial Shading Patterns Paint a Gradient, Fail Closed Otherwise

**Tests**: `PdfDocument_Patterns_ShadingPattern_AxialFunctionType2_FillsVisiblyVaryingGradient`,
`PdfDocument_Patterns_ShadingPattern_RadialFunctionType2_FillsVisiblyVaryingGradient`,
`PdfDocument_Patterns_ShadingPattern_UnsupportedShadingType_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Patterns_ShadingPattern_UnsupportedFunctionType4_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Patterns_ShadingPattern_FunctionArrayForm_ThreeOneOutputFunctions_BuildsRgbGradient`,
`CanvasNetPdf_SystemIntegration_AxialShadingPatternFill_PaintsVisiblyVaryingColors`

Every unit test builds a minimal single-page PDF with a declared `/Pattern` resource
(`/PatternType 2`) and renders a filled rectangle. Asserts an axial (`/ShadingType 2`) pattern
driven by a single `/FunctionType 2` black-to-white function paints near-black at one axis
endpoint and near-white at the other. Asserts a radial (`/ShadingType 3`) pattern with the same
function shape paints a visibly different color at the center versus the edge. A `[Theory]`
asserts `/ShadingType 1`/`4` both throw `Codecs.UnsupportedImageFeatureException` (feature
`pdf-shading-type-{n}`). Asserts a `/Function` entry resolving to a `/FunctionType 4` function
still throws `Codecs.UnsupportedImageFeatureException` (feature `pdf-functiontype-4`) when
reached through a shading pattern, not only through a direct function-resolution call. Asserts
the `/Function [fn0 fn1 fn2]` array-of-1-output-functions form builds a correct RGB gradient. The
system-integration test additionally proves the same axial-gradient fill end-to-end through the
public `Render` API using a fully synthetic, in-memory PDF (no binary fixture), asserting
near-black/near-white at the expected device pixel positions.

#### CanvasNetPdf-PdfDocument-TilingPatternFill: Colored/Uncolored Tiling Patterns Paint a Tile, Fail Closed Otherwise

**Tests**: `PdfDocument_Patterns_TilingPattern_ColoredPaintType1_FillsRepeatingTile`,
`PdfDocument_Patterns_TilingPattern_UncoloredPaintType2_AppliesSuppliedTintPreservingAlpha`,
`PdfDocument_Patterns_TilingPattern_OversizedTileDimensions_ThrowsUnsupportedImageFeatureException`,
`PdfDocument_Patterns_TilingPattern_ZeroXStep_ThrowsInvalidDataException`,
`PdfDocument_Patterns_TilingPattern_NestingDepthExceeded_ThrowsInvalidDataException`,
`CanvasNetPdf_SystemIntegration_ColoredTilingPatternFill_PaintsRepeatingTileColors`

Every unit test builds a minimal single-page PDF with a declared `/Pattern` resource
(`/PatternType 1`) and renders a filled rectangle spanning multiple tile repetitions. Asserts a
`/PaintType 1` (colored) 2-color tile paints both of its own colors at multiple, correctly offset
sample points (proving actual repetition, not merely 1 painted color). Asserts a `/PaintType 2`
(uncolored) tile whose own content paints partial-coverage ink takes the `scn`-supplied tint's
RGB on painted pixels while unpainted/transparent cell regions remain untouched (proving alpha is
preserved, not merely overwritten). Asserts a pathological `/XStep`/`/YStep` combined with an
extreme CTM scale that would allocate a tile surface exceeding `MaxTileSurfaceDimension` throws
`Codecs.UnsupportedImageFeatureException` (feature `pdf-pattern-tile-too-large`). Asserts a zero
`/XStep` throws `InvalidDataException` (malformed, not merely unsupported). Asserts a tiling
pattern whose own content stream nests tiling-pattern cell rendering deeply enough to exceed the
existing (reused, not duplicated) `MaxFormNestingDepth` guard throws `InvalidDataException` (no
dedicated correctness test of deep nested rendering itself - only that the guard still fires
through this new call path). The system-integration test additionally proves the same colored
tiling-pattern fill end-to-end through the public `Render` API using a fully synthetic, in-memory
PDF (no binary fixture), asserting both tile colors appear at multiple expected-offset device
pixel positions.

## Acceptance Criteria

A unit-level test run passes when all scenarios above pass without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong return/field
value constitutes a failure.
