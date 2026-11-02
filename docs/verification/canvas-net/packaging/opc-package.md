### OpcPackage Unit Verification Design

<!-- cspell:ignore ECMA ordinally -->

This document describes the unit-level verification strategy for the `OpcPackage` class and its
`OpcFormatException`.

#### Verification Approach

`OpcPackageTests.cs` builds each package in memory with `ZipArchive` over a `MemoryStream`
(`BuildZip`, parameterized by entry name and content pairs; `BuildValidPackage` adds a minimal valid
package's parts), so each test describes only the entries relevant to its scenario. Names, content
types and relationship targets are synthetic (`doc/main.xml`, `layouts/layout1.xml`), so nothing
depends on a real document. The size bounds are proved with a stream that never ends (the open must
stop at the package bound) and a zero-filled entry written in 64 KiB chunks that inflates to one byte
more than the part bound while compressing to a few hundred kilobytes (the read must stop at the
bound). The XML hardening is proved with a part that declares an external-entity DTD and a part
padded past the character bound. Tests assert on resolved paths and content types, on the exception
type and its message where the message is the contract, and on the inner `XmlException`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; all inputs are in-memory
- **Isolation**: Each test builds its own package; no shared state

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed for
each requirement ID passes across every target framework in this repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Packaging-OpcPackage-BoundedOpen: Open Is Bounded and Leaves the Stream Open

**Tests**: `OpcPackage_Open_MinimalValidPackage_OpensAndLeavesStreamOpen`,
`OpcPackage_Open_NonZipStream_ThrowsOpcFormatException`,
`OpcPackage_Open_NonTerminatingStream_ThrowsOpcFormatException`

Opens a minimal valid package and asserts a package-level relationship resolves and the input stream
is still readable; opens eight arbitrary bytes and asserts `OpcFormatException`; opens a stream that
never ends and asserts `OpcFormatException` naming the package bound.

##### CanvasNet-Packaging-OpcPackage-RequiredParts: Required Parts Are Present and Valid

**Tests**: `OpcPackage_Open_MissingContentTypes_ThrowsOpcFormatException`,
`OpcPackage_Open_MissingPackageRelationships_ThrowsOpcFormatException`,
`OpcPackage_Open_ContentTypesNotWellFormed_ThrowsOpcFormatException`,
`OpcPackage_Open_ContentTypesWrongRoot_ThrowsOpcFormatException`,
`OpcPackage_Open_ContentTypeEntryMissingAttribute_ThrowsOpcFormatException`,
`OpcPackage_Open_RelationshipsWrongRoot_ThrowsOpcFormatException`,
`OpcPackage_Open_RelationshipMissingAttribute_ThrowsOpcFormatException`,
`OpcPackage_Open_DuplicateRelationshipId_ThrowsOpcFormatException`

Opens packages lacking each required part (asserting the message names it), with an unterminated
`[Content_Types].xml`, with a wrong root element in either part, with each of the four `Default`/
`Override` elements missing one required attribute, with each of the three `Relationship` shapes
missing `Id`, `Type` or `Target`, and with two relationships sharing an `Id`; each is rejected with
`OpcFormatException`.

##### CanvasNet-Packaging-OpcPackage-PartNames: Part Names Compare Ordinally

**Tests**: `OpcPackage_ResolvePart_NamesDifferingOnlyByCase_AreDistinctParts`,
`OpcPackage_Open_WrongCaseContentTypesPartName_ThrowsOpcFormatException`,
`OpcPackage_Open_DuplicateEntryNames_ThrowsOpcFormatException`,
`OpcPackage_ContainsPart_ExactAndDifferentCaseNames_ReportsExistence`,
`OpcPackage_NormalizePartPath_LeadingSlash_StripsOnlyOne`

Declares `doc/Page.xml` and `doc/page.xml` with different `Override` types and asserts each resolves
to its own; opens a package whose reserved part is named `[content_types].xml` and one with two
identical entry names (asserting the message says duplicate); asserts `ContainsPart` is true for an
exact name with and without a leading slash and false for a name differing in case; asserts
`NormalizePartPath` strips exactly one slash (`//a.xml` becomes `/a.xml`).

##### CanvasNet-Packaging-OpcPackage-ContentTypes: Override Wins Over Default

**Tests**: `OpcPackage_ResolvePart_Override_TakesPrecedenceOverDefault`,
`OpcPackage_ResolvePart_NoOverride_UsesDefaultExtension`,
`OpcPackage_ResolvePart_UnknownPartOrNoContentType_ThrowsOpcFormatException`

Resolves a part that has both an `Override` and a `Default` (with and without a leading slash) and
asserts the override; resolves parts by `Default` including an upper-case extension; resolves a
missing part and a part whose extension has no mapping and asserts `OpcFormatException`.

##### CanvasNet-Packaging-OpcPackage-Relationships: Targets Resolve by the OPC Rules

**Tests**: `OpcPackage_ResolveRelationship_RelativeAbsoluteAndDotTargets_ResolveToPartPaths`,
`OpcPackage_ResolveRelationship_FragmentTargets_DropTheFragment`,
`OpcPackage_ResolveRelationship_TargetEscapesPackageRoot_ThrowsOpcFormatException`,
`OpcPackage_ResolveRelationship_UnknownIdOrExternalTarget_ThrowsOpcFormatException`,
`OpcPackage_TryResolveRelationship_PartWithoutRelationships_ReturnsNull`,
`OpcPackage_ResolveRelationshipByType_SuffixMatch_ResolvesNonExternalTarget`,
`OpcPackage_TryResolveRelationshipById_KnownUnknownAndExternal_ResolveOrReturnNull`,
`OpcPackage_GetRelationshipType_KnownAndUnknownId_ReturnsTypeOrThrows`,
`OpcPackage_ResolveRelationship_MalformedPartRelationships_ThrowsOnFirstUse`

Resolves a `../layouts/layout1.xml` target from `doc/main.xml` (to `layouts/layout1.xml`), an
absolute `/shared/absolute.xml` target, and a `./sub/../page.xml` target; resolves
`../layouts/layout1.xml#bookmark` to `layouts/layout1.xml` and a fragment-only `#top` to the source
part itself (`doc/main.xml`), through `ResolveRelationship` and `TryResolveRelationshipById`; asserts a `../../`
target from one directory deep is rejected with a message saying it escapes the package root, an
unknown identifier and an external target are rejected (the message says external), a part with no
relationships part yields `null` from the `Try` forms, the type-suffix search resolves a non-external
relationship, is case-sensitive and skips external ones and reports a missing type in its message,
the by-identifier search returns `null` for unknown and external ones, the type is reported by
identifier, and a malformed relationships part of a non-root part is reported on first use rather than
at open.

##### CanvasNet-Packaging-OpcPackage-XmlParts: XML Parts Are Hardened and Bounded

**Tests**: `OpcPackage_Open_OversizedContentTypesPart_ThrowsOpcFormatException`,
`OpcPackage_Open_ContentTypesWithDocumentType_ThrowsOpcFormatException`,
`OpcPackage_LoadPartXmlRoot_PresentMissingAndMalformedParts_ReturnRootOrThrow`

Pads `[Content_Types].xml` with a comment past `MaxXmlPartCharacters` and asserts
`OpcFormatException` whose inner exception is an `XmlException`; declares an external-entity DTD in
it and asserts the same; loads a present part with and without a leading slash and asserts its root,
and a missing part, an ill-formed part and a part with a DTD each throw.

##### CanvasNet-Packaging-OpcPackage-PartBytes: Part Reads Are Bounded

**Tests**: `OpcPackage_GetPartBytes_WellFormedPart_ReturnsExactBytes`,
`OpcPackage_GetPartBytes_OversizedDecompressedEntry_ThrowsOpcFormatException`

Reads a 256-byte part and asserts the exact bytes, with and without a leading slash, in distinct
arrays, and a missing part throws; writes a part of `MaxPartBytes + 1` zero bytes (a tiny compressed
entry) and asserts `OpcFormatException` whose message mentions a decompression bomb.

##### CanvasNet-Packaging-OpcPackage-Validation: Arguments and Disposal

**Tests**: `OpcPackage_Open_NullStream_ThrowsArgumentNullException`,
`OpcPackage_Open_UnreadableStream_ThrowsArgumentException`,
`OpcPackage_Methods_NullArguments_ThrowArgumentNullExceptionWithParameterName`,
`OpcPackage_Dispose_CalledTwiceThenUsed_DoesNotThrowUntilUsed`,
`OpcFormatException_Constructors_CarryMessageAndInnerException`

Passes a null and an unreadable stream to `Open` and asserts the exception types and the parameter
name `stream`; passes null to every instance method and asserts the parameter names; disposes twice
and asserts the second call is silent and `ContainsPart`, `ResolvePart`, `ResolveRelationship`,
`LoadPartXmlRoot` and `GetPartBytes` then throw `ObjectDisposedException`;
constructs the exception with each constructor and asserts its message and inner exception.
