<!-- cspell:ignore embedder -->

### FolderImageResolver Unit Verification Design

This document describes the unit-level verification strategy for the `FolderImageResolver` unit: the resolver and its
`FolderImageResolverOptions`, in the `DemaConsulting.CanvasNet.ImageSources` namespace.

#### Verification Approach

The unit is verified by calling `Resolve`, `Render(path)`, and `Render(Stream)` directly and asserting on what the
sources
received, on the returned surfaces, and on `Diagnostics`, `Echo`, and `Embedded`. The sources are `StubImageSource`
instances
(in `ImageSourceTestSupport.cs`), real implementations of `IImageSource` that claim what the test says, record every
request,
and draw what a delegate says (a solid surface, a failure, or a nested `Resolve` of a link taken from the text of the
file, which
is how a chain, a cycle, and the depth are built without any real format). The files are real files in a per-test
temporary
folder (`ImageTestFolder`), so the sandbox, the folder logic, and the link refusal are proven against the real file
system. Tests
reside in `FolderImageResolverTests.cs`, `FolderImageResolverHrefTests.cs` (query, fragment, and encoding), and
`FolderImageResolverBudgetTests.cs` (the pixel budget) in the `ImageSources` folder of the
`DemaConsulting.CanvasNet.Tests` project. No
timing is
asserted.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None; the stub sources are real implementations
- **Files**: per-test temporary folders under the system temporary folder, deleted by the test; no checked-in file is
  read
- **Capabilities**: the symbolic-link scenario needs permission to create a link and skips otherwise; nothing else
  depends on the host
- **Isolation**: each test creates its own resolver, as one render does

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A
source that was called for a refused link, a file read outside the folder, a missing warning, a wrong limit, a wrongly
wrapped or
unwrapped exception, or a cached picture constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-ImageSources-FolderImageResolver-Confinement: Links Stay Inside The Folder

**Tests**: `FolderImageResolver_Resolve_RelativeFile_PassesCompleteRequestToSource`,
`FolderImageResolver_Resolve_EscapingReference_IsUnresolvedWarning`,
`FolderImageResolver_Resolve_SymbolicLink_IsUnresolvedWarning`,
`FolderImageResolver_Resolve_MissingAndUnknownFiles_AreWarnings`

Asserts, as a theory over `..`, `sub/../..`, `..%2F`, `%2e%2e/`, backslash escapes, absolute paths, drive and UNC paths,
an
alternate data stream, an encoded NUL, `http`, `https`, `ftp`, and a `file:` URI with a host, that a valid file above
the folder
is never drawn, that the source is never called, and that the one note is the warning with the stable code and the link;
that a
link through a symbolic link is refused; and that a missing file is a warning.

##### CanvasNet-ImageSources-FolderImageResolver-FileUri: File URIs And Percent-Encoding

**Tests**: `FolderImageResolver_Resolve_FileUriAndEncodedPath_InsideFolderIsDrawnOutsideIsNot`,
`FolderImageResolver_Resolve_FileUriWithControlCharacter_IsUnresolvedWarning`,
`FolderImageResolver_Resolve_FileScheme_IsHandledByResolverNotSources`

Asserts that a `file:` URI outside the folder is a warning, one inside is drawn, `my%20img.stub` is decoded, a `file:`
URI with a
control character is a warning, and that a source that claims another scheme never sees a `file:` link.

##### CanvasNet-ImageSources-FolderImageResolver-UriReference: Query, Fragment, And Percent-Encoding

**Tests**: `FolderImageResolver_Resolve_RelativeLinkWithQueryOrFragment_NamesTheFile`,
`FolderImageResolver_Resolve_EncodedHash_IsLiteralCharacterOfTheFileName`,
`FolderImageResolver_Resolve_EncodedQuestionMark_IsLiteralCharacterNotAQuery`,
`FolderImageResolver_Resolve_FileUriWithQueryAndFragment_NamesTheFile`,
`FolderImageResolver_Resolve_FileUriOutsideFolderWithQuery_IsStillUnresolved`,
`FolderImageResolver_Resolve_OnlyQueryOrFragment_IsUnresolved`,
`FolderImageResolver_Resolve_EscapingReferenceWithQueryOrFragment_IsStillUnresolved`,
`FolderImageResolver_Resolve_SchemeLinkWithQueryAndFragment_IsPassedAsIs`,
`FolderImageResolver_Resolve_UnclaimedSchemeWithQuery_IsUnresolved`,
`FolderImageResolver_Resolve_NestedLinkWithQueryAndFragment_ResolvesFromFolderOfEmbeddingFile`,
`FolderImageResolver_Resolve_QuestionMarkAfterHash_BelongsToFragment`,
`FolderImageResolver_Render_PathWithHash_IsNotInterpretedAsFragment`

These tests are in `FolderImageResolverHrefTests.cs` and `FolderImageResolverFragmentTests.cs`. They assert, as a
theory,
that `a.stub?cache=1`, `a.stub#view`,
`a.stub?x=1#frag`, `a.stub#frag?not-a-query`, `sub/b.STUB?v=2`, an encoded name with a query and a fragment, and
`a.stub#/../../outside` all draw the named file (the stub source declares no fragment parameter, so the only notes are
`image-fragment-unknown-parameter` warnings for a fragment that has one), and that the source receives the link as
written in
`Href` and the name of the file that was read in `FileName`; that `a%23b.stub` (with and without a fragment) reads the
file `a#b.stub` while the unencoded `a#b.stub` names `a` and is unresolved; that `a%3Fb.stub` is a literal question
mark of the name and never reads `a.stub` (the file `a?b.stub` is created where the file system allows it, and on
Windows the link is unresolved instead); that a `file:` URI inside the folder may carry a query and a fragment and an
encoded hash in it stays literal, and one outside the folder or leaving it with `..` stays unresolved; that a link
that is only a query or fragment (`?cache=1`, `#view`, `?`, `#`, `?a#b`) is the warning "the reference is empty"
without a source call; as a theory over 18 spellings (`..%2f` and `%2e%2e` with every kind of suffix, backslashes,
absolute, drive, and UNC paths, an encoded NUL, an alternate data stream) that stripping opens no way out; that a
claimed scheme link with a query and a fragment reaches its source untouched while a path whose query contains a colon
is still a path; that an unclaimed scheme with a query is a warning; and that a link inside an embedded file is
stripped and resolved from the folder of that file; and that a `?` after the first `#` belongs to the fragment, while a
path given to a direct `Render` with a `#` in it is used as given.

##### CanvasNet-ImageSources-FolderImageResolver-Fragment: The Fragment Reaches The Source

**Tests**: `FolderImageResolver_Resolve_FileLinkWithFragment_FragmentReachesSource`,
`FolderImageResolver_Resolve_LinkWithoutFragment_RequestHasEmptyFragment`,
`FolderImageResolver_Resolve_FragmentOverLimits_IsInvalidAndSourceNotCalledAndNotCounted`,
`FolderImageResolver_Resolve_SchemeLink_ReceivesRawHrefAndFragmentWithoutWarnings`,
`FolderImageResolver_Resolve_SchemeLinkFragmentOverLimit_IsInvalidAndSourceNotCalled`,
`FolderImageResolver_Render_PathWithOptions_PassesOptionsToSourceOnly`,
`FolderImageResolver_Render_StreamWithAndWithoutOptions_PassesOptionsOrNull`

Asserts that the request of a file link carries the parsed fragment (raw text and parameters) while `Href` and the path
are unchanged, that a link without a fragment has the empty one, that a fragment over 2048 characters or 32 parameters
is one `image-fragment-invalid` note with the source not called and the file not counted as an image, that a scheme link
gets the raw link and the parsed fragment (and no warning without the interface, warnings with it), and that the options
of a direct render reach the source in `Options` of that request only and are `null` for a link and for the overloads
without options.

##### CanvasNet-ImageSources-FolderImageResolver-FragmentDiagnostics: Unknown And Duplicate Parameters

**Tests**: `FolderImageResolver_Resolve_UnknownParameter_WarnsAndStillDraws`,
`FolderImageResolver_Resolve_SourceWithoutInterface_EveryParameterIsUnknown`,
`FolderImageResolver_Resolve_DuplicateParameter_WarnsOnlyWhenValuesDiffer`,
`FolderImageResolver_Resolve_AliasParameters_AreNotDuplicates`,
`FolderImageResolver_Resolve_UnknownRepeatedParameter_IsOnlyReportedAsUnknown`,
`FolderImageResolver_Resolve_HostileParameterName_IsSanitizedInMessage`,
`FolderImageResolver_Resolve_SchemeLinkWithInterface_WarnsAboutUnknownParameters`

Asserts that a parameter the source does not declare is one warning and the picture is drawn, that every parameter is
unknown for a source without the interface, that two values for one name warn once and the same value twice does not,
that aliases are not duplicates, that a repeated unknown parameter is reported as unknown only, and that a hostile name
with control characters and a long text is shown quoted, cleaned, and cut.

##### CanvasNet-ImageSources-FolderImageResolver-FragmentPrefixes: Prefixed Parameters Are Understood

**Tests**: `FolderImageResolver_Resolve_DeclaredPrefix_UnderstandsEveryParameterWithIt`,
`FolderImageResolver_Resolve_DeclaredPrefix_DuplicatesCompareTheNameAsWritten`,
`FolderImageResolver_Resolve_NoDeclaredPrefix_PrefixedParameterIsUnknown`

These tests are in `ImageFragmentPrefixTests.cs`. A stub source that declares `page` and the prefix `def.` is given
`a.stub#def.width=20&DEF.Label=x&page=2&pag=3&def.`: the picture is drawn and exactly two unknown-parameter warnings are
reported, for `pag` and for the bare `def.`, while the source receives all five parameters. Given
`def.a=1&def.A=2&def.b=1&DEF.b=2&def.c=3&DEF.c=3`
only `def.b` is reported, as a duplicate whose last value is `2`. A source that declares no prefix (no interface, a null
list, a null
entry, an empty entry) gets the unknown-parameter warning for `def.width` and the picture is drawn.

##### CanvasNet-ImageSources-FolderImageResolver-PageRefusal: A Source Declines A Page

**Tests**: `FolderImageResolver_Resolve_SourceRefuses_UnsupportedNoteNothingDrawnBudgetRefunded`,
`FolderImageResolver_Resolve_SourceReadsInvalidPage_IsInvalidFragmentNote`,
`FolderImageResolver_Resolve_NestedRefusal_HasOriginOfEmbeddingFile`,
`FolderImageResolver_Render_SourceRefuses_ExceptionPropagatesUnwrapped`,
`FolderImageResolver_Render_SourceRefusesAfterCharging_BudgetIsRefunded`,
`FolderImageResolver_Resolve_SourceNoteWithDetail_KeepsDetailLocationAndPage`

Asserts that an `ImageSourceRefusedException` is one unsupported note with the code of the exception, nothing drawn, the
charged pixels given back, and the render carried on; that a source that reads an unusable page value yields
`image-fragment-invalid`; that the note of a nested refusal has the embedding file as origin; that a direct render lets
the
exception propagate unwrapped and refunds the budget; and that the `Detail`, `Location`, and `Page` of a note a source
returns are forwarded unchanged.

##### CanvasNet-ImageSources-FolderImageResolver-Dispatch: Extension Dispatch

**Tests**: `FolderImageResolver_Resolve_RelativeFile_PassesCompleteRequestToSource`,
`FolderImageResolver_Resolve_LongestSuffixAndCaseInsensitive_PicksSource`,
`FolderImageResolver_Resolve_MissingAndUnknownFiles_AreWarnings`

Asserts that the source receives the link, the bytes, the relative name, the folder, the size, the switch, and the token
of the
request, that `a.vl.json` and `C.VL.JSON` go to the source that claims `.vl.json` while `b.json` goes to the one that
claims
`.json`, and that a file of an unclaimed extension is a warning that lists the claimed extensions.

##### CanvasNet-ImageSources-FolderImageResolver-Precedence: The Later Source Wins

**Tests**: `FolderImageResolver_Resolve_TwoSourcesClaimExtension_LaterWins`,
`FolderImageResolver_Resolve_ImageSourceSetWithAddedSource_OverridesEarlierSource`

Asserts that for two sources that claim one extension the one later in the sequence draws, in both orders, and that an
`ImageSourceSet` extended with `Add` overrides the source it was built from.

##### CanvasNet-ImageSources-FolderImageResolver-Schemes: URI Schemes

**Tests**: `FolderImageResolver_Resolve_ClaimedScheme_GivesSourceRawHrefWithoutFileAccess`,
`FolderImageResolver_Resolve_NoFolder_FilesAreUnresolvedButSchemesWork`,
`FolderImageResolver_Resolve_HttpAndHttps_UnresolvedByDefaultButClaimable`,
`FolderImageResolver_Resolve_UnclaimedScheme_IsUnresolvedWarning`,
`FolderImageResolver_Constructor_InvalidOrReservedScheme_ThrowsArgumentException`

Asserts that a claimed scheme link reaches the source as written (including the case and odd characters) with no
content, no name,
no folder, the size, and no file access (the folder does not exist), that without a folder files are unresolved but
schemes work,
that `http` and `https` are warnings unless a source claims them (also in other case), that an unclaimed scheme is a
warning naming
it, and, as a theory, that a source that claims `file`, `data`, a single letter, or a malformed scheme is refused at
construction
with its name in the message.

##### CanvasNet-ImageSources-FolderImageResolver-NestedScope: Links Of Embedded Files

**Tests**: `FolderImageResolver_Resolve_NestedRelativeLink_ResolvesFromFolderOfEmbeddingFile`,
`FolderImageResolver_Resolve_ClaimedSchemeNestedRelativeLink_ResolvesFromRootFolder`

Asserts that a link inside `sub/outer.stub` resolves from `sub`, that a link inside a scheme-claimed picture resolves
from the
root folder, and the depths of the embedded images.

##### CanvasNet-ImageSources-FolderImageResolver-NaturalSize: The Kind Of A Picture

**Tests**: `ImageSourceResult_IsNaturalSize_IsSetByTheRasterSourceAndFalseByDefault`,
`FolderImageResolver_Resolve_ReportsTheKindOfThePictureNotItsSize`,
`FolderImageResolver_ScopedResolver_ReportsTheKindOfThePicture` (in `FolderImageResolverNaturalSizeTests.cs`)

Asserts that the raster source sets `IsNaturalSize` and a result does not by default; that a 40 by 40 PNG asked for at
exactly 40 by 40 is reported as natural size while a stub that draws for the request at the same size is not, and a
missing file reports `false` with no picture; and that the resolver that a source receives reports the same through
`IImageNaturalSizeResolver` for a PNG and for a drawn picture embedded by a file, while the outer call reports `false`.

##### CanvasNet-ImageSources-FolderImageResolver-Limits: Cycle, Depth, And Count

**Tests**: `FolderImageResolver_Resolve_Cycles_TerminateWithWarning`,
`FolderImageResolver_Resolve_TooDeep_TerminatesWithWarning`,
`FolderImageResolver_Resolve_TooMany_StopsAtLimitWithWarnings`,
`FolderImageResolver_Resolve_ClaimedSchemeCycleAndDepth_AreWarnings`,
`FolderImageResolver_Resolve_ClaimedSchemeLinks_CountAgainstImageLimit`,
`FolderImageResolver_Resolve_ImageCountExhausted_NeverReadsTheEmbeddedFile`,
`FolderImageResolver_Render_PathAndStream_SandboxFileSlotsMatchTheImageLimit`

Asserts that a file that links itself and two files that link each other stop with one cycle warning, that a chain
longer than the
depth stops after exactly as many files as the depth allows with a warning about the depth, that more links than the
image limit
are drawn only up to the limit with a warning for each further one, and that scheme-claimed pictures are subject to the
cycle,
depth, and count limits; that with `MaxImages = 0` a file larger than the 16 MiB sandbox limit is refused with the
"more than 0 images" reason and never read (the sandbox would otherwise report its size limit) and its source is not
called; and that, with links that are read but refused for the pixel budget (so they are not counted as images) and a
limit of two images, a render by path and a render of a stream both read exactly two embedded files before the sandbox
refuses the third (the top-level file of a path render has a slot of its own).

##### CanvasNet-ImageSources-FolderImageResolver-PixelBudget: Pixel Budget

**Tests**: `FolderImageResolver_Resolve_HugeRasterAtTinyPlacement_IsRefusedBeforeDecoding`,
`FolderImageResolver_Resolve_ManyRasters_ExhaustBudgetAndLaterOnesAreRefused`,
`FolderImageResolver_Resolve_SourceNeverCharges_ReturnedSurfaceIsChargedAndRefusedWhenOverBudget`,
`FolderImageResolver_Resolve_SourceReturnsMoreThanRequested_ChargesReturnedSize`,
`FolderImageResolver_Resolve_SourceRefusesBeforeDrawing_IsWarningAndNotCharged`,
`FolderImageResolver_Resolve_PlacementLargerThanBudget_IsRefusedWithoutCallingSource`,
`FolderImageResolver_Resolve_RequestedSize_IsClampedPerSide`,
`FolderImageResolver_Resolve_RefusedForBudget_DoesNotCountAsLoadedImage`,
`FolderImageResolver_Resolve_NestedPictures_ShareTheBudget`,
`FolderImageResolver_Render_OverBudget_ThrowsBudgetException`,
`FolderImageResolver_Render_SourceReturnsTooMuch_ThrowsAndDiscardsSurface`,
`FolderImageResolver_Constructor_MaxDecodedPixels_DefaultNegativeAndZero`,
`FolderImageResolver_Resolve_NormalRaster_IsUnaffectedByTheBudget`,
`FolderImageResolver_Resolve_NestedAndLargerSurface_ShareLiveBudget`,
`FolderImageResolver_Resolve_LargerSurfaceThatFitsAfterNestedPicture_IsAcceptedAndSpendsTheBudget`,
`FolderImageResolver_Resolve_SourceChargingExactlyTheBudget_IsNotChargedTwice`,
`FolderImageResolver_Resolve_OwnSurfaceAfterNestedCharge_IsRefusedByLiveRemaining`,
`FolderImageResolver_Resolve_NestedSiblings_AccumulateCharges`,
`FolderImageResolver_Resolve_SourceFailsAfterCharging_RefundsTheCharge`,
`FolderImageResolver_Render_SourceFailsAfterCharging_RefundsTheCharge`,
`FolderImageResolver_RemainingPixels_StartsFullAndDecreasesAfterEmbed`

These tests are in `FolderImageResolverBudgetTests.cs`. They use `PngHeaderFixtures` (a valid 33-byte PNG file that only
declares a size, with no pixel data, so decoding it would fail) and real solid PNG files. They assert that an 8192 by
8192 declaration placed in 1 by 1 is refused by the real `RasterImageSource`: the result is `null`, the one note is the
warning `svg-image-too-large` (source `svg`, severity `Warning`), no image is listed as loaded, no exception is thrown
(a decode would have thrown `InvalidDataException`), and the allocation measured with
`GC.GetAllocatedBytesForCurrentThread` stays below 8 MiB (the decoded picture would be 256 MiB); that five embeds of a
1000 by 1000 PNG against a budget of 3,500,000 pixels draw three and refuse two with a warning each (the pictures are
placed at 1 by 1 and still cost their own size); that a stub that never calls `EnsureWithinBudget` and returns 400
pixels is charged 400 and not the 4 it was asked for (the live remaining its next request sees drops from 1000 to 600,
nothing is charged before the source runs, and `ChargedPixels` stays 0), and the one that no longer fits is
disposed (`ObjectDisposedException` on access) and refused with the warning; that a stub that calls
`EnsureWithinBudget` and is refused leaves the budget as it was (both requests see the whole 500,000 and only the drawn
10 by 10 picture is charged) and that the warning
names the pixels needed; that a placement of 8192 by 8192 (67,108,864) exceeds the default and never reaches the
source; that a requested size of 100,000 by `int.MaxValue` reaches the source as 8192 by 8192 and 0 or negative as 1;
that a refusal does not use up `MaxImages`; that a picture embedded by another picture draws on the same budget; that a
direct render throws `ImageSourceBudgetException` with the numbers (also for a stream and for a returned surface that
does not fit, which is disposed); that the default is 64,000,000, a negative value is `ArgumentOutOfRangeException`,
and zero refuses everything; that a normal raster is returned at its natural size with no note; that with a budget of
20,000 a source that charges its own 100 by 100 surface sees 20,000 and then 10,000 remaining, a nested 60 by 100
picture fits (4,000 remain live), a second nested 100 by 100 picture is refused with `svg-image-too-large`, and a
150 by 100 surface (5,000 more than the source charged) is refused by the resolver, which gives back the source's
10,000 (14,000 remain: the nested picture stays charged and the source's `ChargedPixels` is 0 again), while a 140 by 100
surface is accepted and leaves nothing; that a source that charges exactly the whole budget is not charged a second
time and one more pixel is refused with the live numbers; that a source which resolves a 6,000-pixel nested picture
(14,000 of 20,000 remain live) and then charges an 18,000-pixel surface of its own is refused before it allocates
anything (the link is an `svg-image-too-large` warning naming 18,000 pixels, the source's `ChargedPixels` is 0 and
14,000 remain); that three sibling pictures of 400 pixels each on a budget of 1,000 after the parent's 100 see 900, 500,
and 100 remaining, so the first two are drawn and the third is refused; that a source which throws after it charged
(a link: `ImageSourceException` wrapping the original; a direct render too) or is refused for a second surface gets its
charge back (the whole 5,000 remain); and that `RemainingPixels` of the resolver is the whole budget at the start, drops
by what an embed is charged, and is unchanged by a refusal.

##### CanvasNet-ImageSources-FolderImageResolver-Cancellation: Cancellation

**Tests**: `FolderImageResolver_Resolve_CanceledToken_ThrowsBeforeCallingSource`,
`FolderImageResolver_Resolve_SourceThrowsCancellation_IsNotWrapped`

Asserts that a canceled token stops the resolver before another source call, that the token reached the source, and that
a
cancellation thrown by a source stays `OperationCanceledException`.

##### CanvasNet-ImageSources-FolderImageResolver-Diagnostics: Notes, Echo, And Embedded Images

**Tests**: `FolderImageResolver_Resolve_SourceWithNotes_ForwardsDiagnosticsEchoAndEmbedded`,
`FolderImageResolver_Resolve_EscapingReference_IsUnresolvedWarning`,
`FolderImageResolver_Resolve_TooDeep_TerminatesWithWarning`

Asserts that a note keeps its source, severity, and code and gets the link in front of its message, that the printed
text is
collected, that the embedded image has the format the source reported, the size of the surface, and the depth, and that
the
unresolved warnings have the stable code, source, and severity.

##### CanvasNet-ImageSources-FolderImageResolver-DiagnosticOrigin: Which File A Note Is About

**Tests**: `FolderImageResolver_Resolve_NestedSourceNote_OriginListsEmbeddingChain`,
`FolderImageResolver_Resolve_UnresolvedLinkInEmbeddedFile_OriginIsTheContainingFile`,
`FolderImageResolver_Resolve_TooLargeLinkInEmbeddedFile_OriginIsTheContainingFile`,
`FolderImageResolver_Resolve_SchemeLinkNote_OriginIsTheLinkAsWritten`,
`ImageSourceDiagnostic_Origin_DefaultsToEmptyAndIsComparedByEntries`

Asserts that a note about a link of the top-level document has an empty origin and that a note a source reported for a
file embedded by another file lists the chain of files, outermost first, with the message still prefixed by the link
as before; that a link that is not drawn, and a link whose picture does not fit in the pixel budget, found inside an
embedded file, name that file as the origin; that a note for a scheme link names the link as written; and that the
origin takes part in value equality, compared entry by entry, and is empty by default.

##### CanvasNet-ImageSources-FolderImageResolver-Failures: Failed Files

**Tests**: `FolderImageResolver_Resolve_SourceThrows_WrapsInImageSourceException`,
`FolderImageResolver_Resolve_NestedSourceThrows_KeepsInnermostEmbedderAndHref`,
`FolderImageResolver_Resolve_SourceThrowsCancellation_IsNotWrapped`

Asserts that a failure of a source is wrapped with the link, no embedder at the top level, and the original exception
inside, that
a failure deep in a chain keeps the innermost link and embedder, and that cancellation is not wrapped.

##### CanvasNet-ImageSources-FolderImageResolver-NoCaching: No Caching

**Tests**: `FolderImageResolver_Resolve_SameLinkTwice_InvokesSourceEachTimeWithoutCaching`

Asserts that two calls for one link call the source twice and return two different surfaces (a source that draws a new
color on
every call), as a live source requires.

##### CanvasNet-ImageSources-FolderImageResolver-DirectRender: Rendering A Top-Level File

**Tests**: `FolderImageResolver_RenderPath_File_ReturnsResultWithFormat`,
`FolderImageResolver_RenderPath_SourceWithoutFormat_UsesSourceName`,
`FolderImageResolver_RenderPath_Failures_ThrowSpecificExceptions`,
`FolderImageResolver_RenderPath_FileLinkingItself_StopsAsCycle`,
`FolderImageResolver_RenderStream_PassesBytesAndFolder_AndRejectsUnknownOrHuge`,
`FolderImageResolver_RenderStream_NoFolder_SourceHasNoDirectory`

Asserts that the requested size reaches the source, that the top-level file does not count against the image limit, that
the
format is filled in, the exceptions for a missing file, a refused path, an unknown extension (naming the extensions), a
failing
source, a resolver without a folder, and a null path, that a file that links itself stops at once, and for a stream the
bytes, the
name, the folder (none without a folder), the size, an unknown extension, and a stream larger than 16 MiB.

##### CanvasNet-ImageSources-FolderImageResolver-Configuration: Lists And Arguments

**Tests**: `FolderImageResolver_ExtensionsAndSchemes_AreSortedUnionOfSources`,
`FolderImageResolver_Collections_AreReadOnly`,
`FolderImageResolver_Constructor_InvalidArguments_Throw`,
`FolderImageResolver_Constructor_MaxDecodedPixels_DefaultNegativeAndZero`

Asserts the sorted union of extensions and schemes, that the `Extensions`, `Schemes`, `Diagnostics`, `Echo`, and
`Embedded` lists are read-only (not an array or `List<T>`; `Add` through `IList<T>` throws `NotSupportedException`),
and the rejection of a null sequence, negative limits, an empty or white-space
folder (a null folder stays allowed), an
invalid claim, and a null link.
