<!-- cspell:ignore embedder -->

### ImageSourceContract Unit Verification Design

This document describes the unit-level verification strategy for the `ImageSourceContract` unit: `IImageSource`,
`ImageSourceRequest`, `ImageSourceResult`, `ImageSourceDiagnostic`, `ImageDiagnosticSeverity`, `IImageResolver`,
`EmbeddedImage`, `ImageSourceException`, and `ImageSourceSet`, in the `DemaConsulting.CanvasNet.ImageSources` namespace.

#### Verification Approach

The unit is verified by building the types directly and asserting on their behavior. `ImageSourceSet` is built from
`StubImageSource` instances (in `ImageSourceTestSupport.cs`), simple real implementations of `IImageSource` that claim
what
the test says, and from a small source that returns null lists, to prove the validation. `ImageSourceRequest.SizeFor` is
verified as a theory over a table of requested sizes against a picture of a fixed natural size, including the clamping.
`OpenContent` is verified over an array slice, a memory that is not array-backed (a `MemoryManager<byte>` of the test),
and an
empty file. Tests reside in `ImageSourceSetTests.cs` and `ImageSourceContractTests.cs` in the `ImageSources` folder of
the
`DemaConsulting.CanvasNet.Tests` project. No timing is asserted.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None; stub sources are real implementations
- **Files**: none
- **Isolation**: each test builds its own set and requests

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A
claim that is accepted although invalid, a wrong winner of a claim, a changed original set, a wrong size, or a wrong
exception
type constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-ImageSources-ImageSourceContract-SourceClaims: Claims, Precedence, And Lookup

**Tests**: `ImageSourceSet_Constructor_SameClaims_LaterSourceWins`,
`ImageSourceSet_Add_ReturnsNewSetAndLeavesOriginalUnchanged`,
`ImageSourceSet_ExtensionsAndSchemes_AreReadOnly`,
`ImageSourceSet_FindByExtension_LongestSuffixCaseInsensitive`, `ImageSourceSet_FindByScheme_CaseInsensitive`,
`ImageSourceSet_Constructor_InvalidExtension_ThrowsArgumentException`,
`ImageSourceSet_Constructor_InvalidSources_Throw`,
`ImageSourceSet_GetScheme_FollowsRfc3986WithDriveLetterException`

Asserts that of two sources that claim the same extension (in different cases) and scheme the later wins, that the lists
are
sorted lower-case unions without duplicates, that `ImageSourceSet.Extensions` and `Schemes` are read-only (not an array
or `List<string>`; `Add` through `IList<string>`
throws `NotSupportedException`), that `Add` returns a new set with the source last and leaves the original
unchanged, that the lookup by extension takes the longest suffix (`.vl.json` over `.json`), ignores case, and ignores
the folder
part (`dir.json/readme` is not a `.json`), that the lookup by scheme ignores case, that an extension without a dot, a
bare dot,
an empty one, and ones with a separator, a colon, or white space are refused with the name of the source in the message,
that a
null sequence, a null source, a nameless source, and a source that returns a null list are refused, and, as a theory,
that the
scheme syntax is RFC 3986 with the single-letter drive exception (including `secret.png::$DATA`, which has the scheme
`secret.png`).

##### CanvasNet-ImageSources-ImageSourceContract-Request: The Request Of A File And Of A Scheme

**Tests**: `ImageSourceRequest_OpenContent_FileRequest_ReturnsReadOnlySeekableStream`,
`ImageSourceRequest_OpenContent_SchemeRequest_ThrowsInvalidOperationException`,
`ImageSourceRequest_EnsureWithinBudget_ThrowsOnlyWhenOverRemainingPixels`,
`ImageSourceRequest_EnsureWithinBudget_ReservedPixelsAreAlreadyCharged`

Asserts that a request with a file name has content (also when the file is empty), opens as a read-only seekable stream
over the
exact bytes of an array slice and of a non-array memory, and that a request with only a link has no content, no name, no
folder,
no resolver, and the default switch and token, and refuses to open content with a message naming the link; that
`RemainingPixels` is `long.MaxValue` and `ReservedPixels` zero on a hand-built request; that `EnsureWithinBudget`
accepts a size equal to the remainder (and a size of zero pixels) and throws `ImageSourceBudgetException` for one pixel
more, with
the requested and remaining pixels on the exception and in its message; and that with 1000 reserved and 500 remaining a
size of up to 1500 pixels passes, 1510 throws, and the exception reports 1500 as the pixels the source could have used.

##### CanvasNet-ImageSources-ImageSourceContract-SizeRule: Sizing

**Tests**: `ImageSourceRequest_SizeFor_AppliesSizingRule`,
`ImageSourceRequest_SizeFor_InvalidOrHugeNaturalSize_ThrowsOrClamps`

Asserts, over a picture of 200 by 100 pixels, the natural size with no request (with and without fitting), one requested
dimension keeping the aspect ratio (including the minimum of one pixel), the requested box, the picture fitted inside
the box
(shrunk and enlarged), a requested size of zero or below as one pixel, the clamping of an oversize request to the
maximum surface
dimension, the clamping of an oversize natural size, and `ArgumentOutOfRangeException` for a natural size below one
pixel.

##### CanvasNet-ImageSources-ImageSourceContract-Result: Result And Value Types

**Tests**: `ImageSourceResult_Constructor_RequiresSurfaceAndDefaultsToEmpty`,
`ImageSourceContract_ValueTypes_HaveDocumentedShape`

Asserts that a null surface is refused, that the notes, the printed text, and the format default to empty and to null,
that what is
given is kept, the names of the severities, the members and default depth of `EmbeddedImage`, the stable unresolved
code, and
the defaults of the resolver options.

##### CanvasNet-ImageSources-ImageSourceContract-FailureReporting: The Exception Of A Source

**Tests**: `ImageSourceException_Constructor_KeepsHrefEmbedderAndInner`

Asserts that the message, the link, the embedder (and its absence at the top level), and the inner exception are kept.
