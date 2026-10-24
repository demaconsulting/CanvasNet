## ScadDocument Unit Verification Design

This document describes the unit-level verification strategy for the `ScadDocument` unit: the public types in
the `DemaConsulting.CanvasNet.Scad` namespace — `ScadDocument` (the `Parse` and `Open` entry points and the
`SourceName`, `Directives`, `ModuleNames`, and `FunctionNames` properties), `ScadOpenOptions`,
`ScadSourceLocation`, `ScadSyntaxException`, `ScadDirective`, and `ScadDirectiveKind`.

### Verification Approach

The unit is verified by calling the public API directly and asserting on returned values, thrown exceptions, and
the state of the document. The unit's only collaborator is the internal `ScadParser`, which is used for real
rather than mocked: the unit's job is to validate arguments, bound and decode the input, call the parser, and
expose the result, and the parser's own behavior is verified under the _ScadParser Unit Verification Design_.
Internals made visible to the test assembly through `InternalsVisibleTo` (the document's effective `Options`)
are used to verify the base-directory default. The error contract is asserted precisely: a syntax error is a
`ScadSyntaxException`, which is a `FormatException` (not an `InvalidDataException`), and source longer than
4,194,304 UTF-16 characters throws exactly `InvalidDataException`.

Stream handling is verified with real `MemoryStream` instances, and the unbounded-read defense is verified with
a test-local `EndlessBlankStream` that yields blanks forever and counts the bytes handed to the reader, so that
the test can assert that reading stopped within a small margin beyond the limit. File handling uses the original
`.scad` fixtures copied to the test output directory.

Unit tests reside in `ScadDocumentTests.cs`, `ScadOpenOptionsTests.cs`, `ScadSourceLocationTests.cs`, and
`ScadSyntaxExceptionTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests` project; the shared fixture and
syntax-error helpers are in `ScadTestHelpers.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the internal parser is exercised for real
- **Files**: The `ScadFixtures/` folder copied to the test output directory (read only); no test writes to the
  file system
- **Isolation**: Each test constructs its own sources, streams, and options

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, wrong parameter name, wrong location, or
wrong reported name or list constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetScad-ScadDocument-ParseString: Parse Handles Null, Empty, And BOM Source

**Tests**: `ScadDocumentTests.ScadDocument_Parse_NullSource_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Parse_EmptyOrCommentOnlySource_ReturnsEmptyDocument`,
`ScadDocumentTests.ScadDocument_Parse_LeadingBom_IsIgnored`

Asserts that a null source throws `ArgumentNullException` naming `source`; that empty, whitespace-only,
line-comment-only, block-comment-only, BOM-only, and semicolon-only sources parse to a document with no
directives, module names, or function names and a null `SourceName`; and that a leading byte-order mark
character is ignored when followed by a module definition.

#### CanvasNetScad-ScadDocument-OpenStream: Open Reads A Stream As UTF-8 And Bounds The Read

**Tests**: `ScadDocumentTests.ScadDocument_Open_NullStream_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Open_UnreadableStream_ThrowsArgumentException`,
`ScadDocumentTests.ScadDocument_Open_Utf8WithAndWithoutBom_Parses`,
`ScadDocumentTests.ScadDocument_Open_Stream_IsLeftOpen`,
`ScadDocumentTests.ScadDocument_Open_NonTerminatingStream_ThrowsInvalidDataException`

Asserts that a null stream throws `ArgumentNullException` and a disposed (unreadable) stream throws
`ArgumentException`, each naming `stream`; that UTF-8 input with and without a byte-order mark, containing
non-ASCII text in a comment and a string, parses identically; that reading starts at the stream's current
position, consumes the remainder, and leaves the stream readable; and that a stream that never ends throws
`InvalidDataException` (not a `ScadSyntaxException`) after reading no more than 65,536 bytes beyond the limit.

#### CanvasNetScad-ScadDocument-OpenPath: Open Reads A File By Path

**Tests**: `ScadDocumentTests.ScadDocument_Open_NullPath_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Open_WhitespacePath_ThrowsArgumentException`,
`ScadDocumentTests.ScadDocument_Open_FixtureFile_ParsesAndNamesSource`,
`ScadDocumentTests.ScadDocument_Open_MissingFile_ThrowsFileNotFoundException`,
`ScadDocumentTests.ScadDocument_Open_DefaultBaseDirectory_IsFileFolder`

Asserts that a null path throws `ArgumentNullException`; that empty and whitespace paths throw exactly
`ArgumentException` rather than the null subclass; that a fixture file parses and names its source with the path
as supplied; that a missing file throws `FileNotFoundException` and a missing folder throws
`DirectoryNotFoundException`; and that the effective base directory defaults to the folder of the file, can be
overridden through the options, and stays unset for a string.

#### CanvasNetScad-ScadDocument-SourceName: Source Name Reflects Its Supplier

**Tests**: `ScadDocumentTests.ScadDocument_SourceName_FromOptionsOrPath_ReflectsSupplier`,
`ScadDocumentTests.ScadDocument_Parse_SourceNameOption_AppearsInErrorLocation`

Asserts that `SourceName` is null for an unnamed string or stream, equals the `SourceName` option when one is
given, equals the path for a file, and is overridden by an explicit option; and that the supplied name appears
in the location (and therefore the message prefix) of a syntax error raised for that document.

#### CanvasNetScad-ScadDocument-Directives: Directives Are Recorded In Order Without Resolution

**Tests**: `ScadDocumentTests.ScadDocument_Directives_IncludeAndUse_AreRecordedInOrder`,
`ScadDocumentTests.ScadDocument_Directives_ReadOnlyList`,
`ScadDocumentTests.ScadDocument_Directives_FileResolverNotInvokedDuringParse`

Asserts that include and use directives are listed in source order with their kind, path, and location
(including a directive indented on a later line); that the directive, module-name, and function-name lists are
read-only and refuse mutation with `NotSupportedException`; and that a supplied `FileResolver` is never invoked
while parsing a document with two directives.

#### CanvasNetScad-ScadDocument-Definitions: Module And Function Names Are File Scope Only

**Tests**: `ScadDocumentTests.ScadDocument_ModuleAndFunctionNames_FileScopeOnly_InOrder`,
`ScadDocumentTests.ScadDocument_ModuleAndFunctionNames_Duplicates_AreKept`

Asserts that module and function names are reported in source order for file scope only, excluding a module and
a function nested in a module body and a module inside a bare block, and that duplicate definitions are each
kept.

#### CanvasNetScad-ScadDocument-SyntaxException: ScadSyntaxException Is A Located FormatException

**Tests**: `ScadSyntaxExceptionTests.ScadSyntaxException_IsFormatException`,
`ScadSyntaxExceptionTests.ScadSyntaxException_Location_MessageFormat`,
`ScadSyntaxExceptionTests.ScadSyntaxException_UnknownLocation_MessageHasNoPrefix`,
`ScadSyntaxExceptionTests.ScadSyntaxException_StandardConstructors_SetMessageAndInnerException`,
`ScadDocumentTests.ScadDocument_Parse_SyntaxError_ThrowsScadSyntaxExceptionWithLocation`

Asserts that the exception is assignable to `FormatException`; that a known location is exposed through
`Location` and prefixed to the message (`x.scad:4:9: Unexpected token ';'`); that a default (unknown) location
leaves the message bare; that the parameterless, message, and message-with-inner-exception constructors set the
message and inner exception and leave the location unknown; and that a syntax error in parsed source is reported
as this exception at the exact line and column with a message that begins with the location.

#### CanvasNetScad-ScadDocument-SourceLocation: ScadSourceLocation Is A Value Record

**Tests**: `ScadSourceLocationTests.ScadSourceLocation_Equality_SameComponents_AreEqual`,
`ScadSourceLocationTests.ScadSourceLocation_ToString_WithAndWithoutFile_FormatsCoordinates`,
`ScadSourceLocationTests.ScadSourceLocation_Default_IsUnknown`

Asserts value equality and hash equality of two locations with the same components and inequality when one
component differs; that `ToString` renders `file:line:column` with a file and `line:column` without one; and
that the default value has line and column zero and no file and renders as `?`.

#### CanvasNetScad-ScadDocument-OpenOptions: ScadOpenOptions Is Validated And Immutable

**Tests**: `ScadOpenOptionsTests.ScadOpenOptions_Defaults_AreUnsetAndEmpty`,
`ScadOpenOptionsTests.ScadOpenOptions_ValidValues_AreStored`,
`ScadOpenOptionsTests.ScadOpenOptions_InvalidValues_Throw`,
`ScadOpenOptionsTests.ScadOpenOptions_NullLibraryPaths_ThrowArgumentNullException`,
`ScadOpenOptionsTests.ScadOpenOptions_NullTextValues_AreAccepted`,
`ScadOpenOptionsTests.ScadOpenOptions_LibraryPaths_AreCopied`,
`ScadOpenOptionsTests.ScadOpenOptions_AcceptedButNotConsumedYet_DoNotAffectParse`,
`ScadOpenOptionsTests.ScadOpenOptions_Properties_AreInitOnly`

Asserts that a new instance has unset base directory, source name, and file resolver and an empty library path
list; that valid values are stored; that empty or whitespace base directories, source names, and library path
entries throw `ArgumentException` naming the property; that a null library path list or a null entry throws
`ArgumentNullException` naming `LibraryPaths` while null optional text values are accepted; that the library
paths are copied defensively and exposed read-only; that a nonexistent base directory, nonexistent library paths,
and a resolver that must never be called leave the parsed directives and module names unchanged; and, by
reflection, that the class is sealed and that all four public properties can be set only during initialization.

#### CanvasNetScad-ScadDocument-SourceLimit: Oversize Source Is Rejected

**Tests**: `ScadDocumentTests.ScadDocument_Parse_OverLimit_ThrowsInvalidDataException`,
`ScadDocumentTests.ScadDocument_Open_OverLimitStream_ThrowsInvalidDataException`

Asserts that a string of 4,194,305 characters and a stream of 4,194,305 bytes are each rejected with exactly
`InvalidDataException`, not a `ScadSyntaxException` (the exact-limit acceptance case is covered at the system
tier and in the parser unit's source-length scenarios).
