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
`ImageSourceRequest_EnsureWithinBudget_ChargesLiveBudgetAndThrowsOnlyWhenOver`,
`ImageSourceRequest_EnsureWithinBudget_SharedBudget_SeesChargesOfOtherRequests`,
`ImageBudget_Constructor_StartsFullAndRejectsNegative`,
`ImageSourceRequest_FileAccess_DefaultsToAllowAllAndRejectsNull`

Asserts that a request with a file name has content (also when the file is empty), opens as a read-only seekable stream
over the
exact bytes of an array slice and of a non-array memory, and that a request with only a link has no content, no name, no
folder,
no resolver, and the default switch and token, and refuses to open content with a message naming the link; that
`RemainingPixels` is `long.MaxValue` on a hand-built request without a budget and that `EnsureWithinBudget` still
records the size in `ChargedPixels`; that with an `ImageBudget` of 600 the calls charge the live budget (sizes of
several calls add up; a size of zero pixels is free; the last 20 pixels still fit exactly), that one pixel more throws
`ImageSourceBudgetException` with the requested and the free pixels on the exception and in its message and charges
nothing, and that `ChargedPixels`, `RemainingPixels`, and the budget agree afterwards; that two requests on one budget
of 1000 see each other's charge live (after 600 the other sees 400 and is refused 410) and keep their own
`ChargedPixels`; that a budget starts with all its pixels, accepts zero, and rejects a negative maximum; and that a
request carries `FileAccessPolicy.AllowAll` by default, keeps another policy, and refuses `null`.

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
`ImageSourceResult_Init_CopiesListsIntoReadOnlySnapshots`,
`ImageSourceContract_ValueTypes_HaveDocumentedShape`

Asserts that a null surface is refused, that the notes, the printed text, and the format default to empty and to null,
that what is
given is kept, that the notes and the printed text are copied into read-only snapshots (adding to the original lists
afterwards changes nothing, a cast cannot modify the values, null becomes empty, and `with` keeps working), the names of
the severities, the members and default depth of `EmbeddedImage`, the stable unresolved
code, and
the defaults of the resolver options.

##### CanvasNet-ImageSources-ImageSourceContract-FailureReporting: The Exception Of A Source

**Tests**: `ImageSourceException_Constructor_KeepsHrefEmbedderAndInner`

Asserts that the message, the link, the embedder (and its absence at the top level), and the inner exception are kept.

##### CanvasNet-ImageSources-ImageSourceContract-ImageFragment: The Parsed Fragment

**Tests**: `ImageFragment_Parse_NullOrEmpty_IsEmpty`, `ImageFragment_Parse_TwoParameters_KeepsOrderAndRaw`,
`ImageFragment_Parse_SingleParameter_SplitsBeforeDecoding`, `ImageFragment_Parse_EmptySegments_AreSkipped`,
`ImageFragment_Parse_HashAndQuestionMark_StayInTheText`, `ImageFragment_GetValue_CaseInsensitiveLastWins_AcrossAliases`,
`ImageFragment_GetValue_NullNames_ThrowsArgumentNullException`,
`ImageFragment_GetPositiveInt32_ValidValues_ReturnsNumber`,
`ImageFragment_GetPositiveInt32_UnusableValue_ThrowsRefusedWithInvalidCode`,
`ImageFragment_GetPositiveInt32_HostileValue_IsQuotedSafelyInMessage`,
`ImageFragment_Parse_TooLong_HasProblemAndNoParameters`,
`ImageFragment_Parse_AtTheLimits_IsParsed`, `ImageFragment_Parse_TooManyParameters_HasProblemAndNoParameters`,
`ImageFragment_Parameters_AreReadOnly`, `ImageSourceRequest_Fragment_DefaultsToEmptyAndNoOptions`

These tests are in `ImageFragmentTests.cs` and `ImageSourceContractTests.cs`. They assert that null and the empty text
are the empty fragment; that parameters keep their order and the raw text; that the text is split at `&` and at the
first `=` before it is decoded (`a=%26b` is the value `&b`, `a%3Db=1` the name `a=b`, `a=b=c` the value `b=c`, a name
without `=` has an empty value, and `+` stays); that empty segments are skipped and a second `#` and a `?` stay in the
text; that names match ignoring case, the last value wins across aliases, and a null name list is rejected; that a
whole number from 1 is read (`003` is 3) and `0`, `-1`, `+2`, a space, `2.5`, text, an empty value, and an overflow are
refused with `image-fragment-invalid`, with a quoted, cleaned, and cut message for a hostile value; that 2049
characters or 33 parameters set `Problem` and leave no parameter while exactly the limits parse; that the parameter
list is read-only; and that a request has the empty fragment and no options by default.

##### CanvasNet-ImageSources-ImageSourceContract-FragmentPrefix: Prefixed Parameters

**Tests**: `ImageFragment_GetPrefixed_ReturnsNamesWithoutPrefixInOrder`,
`ImageFragment_GetPrefixed_DuplicateName_LastValueWinsAtFirstPosition`,
`ImageFragment_GetPrefixed_EmptyFragmentOrBadPrefix`,
`IFragmentImageSource_FragmentParameterPrefixes_DefaultsToNone`

These tests are in `ImageFragmentPrefixTests.cs`. They assert that `GetPrefixed("def.")` of
`view=top&def.Width=20&DEF.label=%22A%22&def.&other=1&def.a%3Db=x` returns `Width`, `label` (value `"A"`), and `a=b`
(the
prefix matched without case, the names as written, decoded, in order, no `view`, no `other`, and no bare `def.`); that a
name given twice has its last value in the position of its first occurrence while `a` and `A` stay two names; that the
empty
fragment and a fragment over the limit have none and a null or empty prefix is rejected; and that an interface
implementation that does not override `FragmentParameterPrefixes` declares none.

##### CanvasNet-ImageSources-ImageSourceContract-ParameterValues: The Value Grammars Of Variables And Parameters

**Tests**: `ImageParameterValues_TryParseVariable_ReadsTheGrammarOfDashD`,
`ImageParameterValues_TryParseVariable_Vector_IsAListOfValues`,
`ImageParameterValues_TryParseVariable_BadVector_IsRefused`,
`ImageParameterValues_TryParseParameter_ReadsScalarsAndText`, `ImageParameterValues_TryParseParameter_Null_IsNull`,
`ImageParameterValues_TryParseParameter_JsonObjectAndArray_BecomePlainValues`,
`ImageParameterValues_TryParseParameter_BadJson_IsRefusedWithAReason`, `ImageParameterValues_TryParseParameter_Limits`,
`ImageParameterValues_ReadVariables_ReadsPrefixedValues`,
`ImageParameterValues_ReadVariables_BadVector_IsRefusedAsInvalidFragment`,
`ImageParameterValues_ReadVariables_Vector_IsReadAsAList`,
`ImageParameterValues_ReadParameters_ReadsJsonAndRefusesMalformedValues`

These tests are in `ImageFragmentPrefixTests.cs`. They assert the variable grammar (`true`, `false`, `20`, `-1.5`,
`1e3`, the
quoted texts `"a b"` and `'a b'`, unquoted text, the empty text, and the texts `NaN`, `1e999` and `null`, which are
text; a
value that starts with `[` is a vector: `[0, 0.5, [true, "a", null], -3e2]` is a list of doubles, flags, text, a null and
a nested list, and `[1,2`, `[1 2]`, `[{"a":1}]` and `[[1,[{}]]]` are refused with a reason that names the array or the
object) and the parameter grammar (the same scalars,
`null` as
a null value, `'EU west'` and `"EU west"` as text, a JSON string with escapes, objects and arrays as dictionaries and
lists
of doubles, strings, booleans and nulls; `{x`, `{"x":}`, `[1,`, `[1] 2`, `"abc`, `{"x":1e999}` and `[1,]` are refused
with a
one-line reason that has neither a line break nor the position of the JSON failure; 32 nested arrays and 65,536 values
are
accepted and 33 levels and 65,537 values refused). `ReadVariables` of
`view=top&def.width=20&def.label=%22A%26B%22&def.show=true`
gives a double, the text `A&B` (decoded after the split), and a boolean; `def.$vpt=[0,0,20]` is a list of three doubles
beside the double `$vpd`; a vector that is cut short is an `ImageSourceRefusedException`
with the code
`image-fragment-invalid` that names `'def.size'`, quotes the value, and says a vector must be a valid JSON array; and
`ReadParameters`
reads a number, text and the object `{"x":[2,6]}` and refuses `%7Bx` naming `'param.brush'`.

##### CanvasNet-ImageSources-ImageSourceContract-FragmentReview: One Review Of A Fragment For Links And Inputs

**Tests**: `ImageFragment_ReviewParameters_ReportsUnknownAndDuplicateParametersOnce`,
`ImageFragment_ReviewParameters_SourceWithoutDeclaration_ReportsEveryParameter`

These tests are in `ImageFragmentPrefixTests.cs` and use a source that declares `page`, `slide` and the prefix `def.`.
The fragment `pag=2&page=1&PAGE=2&slide=1&page=2&def.a=1&def.a=1&def.b=1&DEF.b=2&pag=3` gives three notes in this
order: `pag` is unknown (once, though it is given twice), `page` is given with different values (the last, `2`, is
named; `page` and `slide` are aliases and are never duplicates of each other), and `def.b` is given twice with
different values through two spellings of the prefix, while `def.a` is the same value twice and gives no note. The
messages start with the subject and the quoted link (`input 'x.pdf#...'`). The empty fragment gives no note, a null
argument is rejected, and a source that declares nothing has every parameter reported as unknown. The
`FolderImageResolver` tests of the unknown and duplicate warnings of a link (unchanged) prove that the resolver reports
the same notes.

##### CanvasNet-ImageSources-ImageSourceContract-Refusal: A Source Declines The Picture

**Tests**: `ImageSourceRefusedException_Constructor_KeepsCodeAndMessageAndValidates`

Asserts that the code and the message are kept and that a null or empty code and a null message are rejected.

##### CanvasNet-ImageSources-ImageSourceContract-DiagnosticDetail: Detail, Location, And Page Of A Note

**Tests**: `ImageSourceDiagnostic_Equals_ComparesDetailLocationAndPage`, `ImageDiagnosticLocation_Equality_IsByValue`

Asserts that two notes are equal only when `Detail`, `Location`, and `Page` are equal too (with equal hash codes), and
that
a location is compared by its four members.
