### SpecParser Unit Verification Design

This document describes the unit-level verification strategy for the `SpecParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the internal static `SpecParser` class (the `Parse` entry
points over text and UTF-8 bytes, the structural validation in the partial `SpecParser.Validation.cs`, and
`ParseSchemaMajorVersion`), and the types of the sibling files that carry its result — `SpecNode`, `SpecNodeKind`,
`NodePosition`, `SpecTree` (with `EnumerateViews`), and `SpecMetadata` (the description of the document read from the
tree).

#### Verification Approach

The unit is verified by parsing small, original specification strings and byte arrays and asserting on the returned
tree: node kinds and values, properties and elements in document order, the RFC 6901 JSON pointer of each node, and
the one-based line and column of the first character of each value (and of each property key). Every error path is
asserted as a `VegaLiteSpecException` (or, for oversized source, a plain `InvalidDataException`) with an exact
location, pointer, or message fragment. The unit depends only on the BCL `Utf8JsonReader`, `VegaLiteLimits`,
`VegaLiteLocation`, and `VegaLiteSpecException`, so no mocking or stubbing is required; the internal types are
reached through the test assembly's `InternalsVisibleTo` access. The unit's interaction with the feature catalog and
the configuration resolver is verified at the subsystem tier (see _SpecModel Subsystem Verification Design_).

Expected positions are derived by hand from the source text. For example, in a specification whose second line (after
a CRLF line break) is a tab followed by `"note": "😀", "mark": "bar"`, the value of `mark` is on line 2 at column
1 + 6 + 2 + 4 + 2 + 6 + 2 + 1: a tab is one column, `"note"` is six, the colon and the space after it are two, a
string holding one astral character is four UTF-16 code units (quote, two surrogates, quote), the comma and the space
are two, `"mark"` is six, the colon and the space are two, and the value starts on the next column. The test asserts
that sum, which pins the rule that columns count UTF-16 code units and not code points or bytes. Likewise the
duplicate-key, depth-limit, and unknown-property expectations are small texts whose lines, columns, and pointers can
be counted by eye.

Unit tests reside in `SpecParserTests.cs` (which also holds the tests of `SpecTree`, `SpecMetadata`, and `SpecNode`,
as they are the parser's result types) within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`SpecModel` folder; the large-inline-data test is in `VegaLiteDocumentSpecModelTests.cs` in the same folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own source strings or byte arrays; no files are read. The stack-safety scenario
  parses a source nested five thousand levels deep on the test thread and asserts a typed exception

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong exception type (in particular `InvalidDataException` where a located
`VegaLiteSpecException` is documented, or the reverse), wrong pointer, line, or column, or wrong warning identifier
constitutes a failure.

#### Unit-Level Test Scenarios

##### Valid Specification Becomes A Tree With Pointers And Positions

**Tests**: `SpecParserTests.SpecParser_Parse_ValidInlineSpecRecordsJsonPointerAndLineColumn`,
`SpecParserTests.SpecParser_Parse_AllValueKindsAndBomAreHandled`,
`SpecParserTests.SpecParser_Parse_LocationsCountUtf16CodeUnits`,
`SpecParserTests.SpecNode_ToString_DescribesKindPointerAndPosition`

Asserts that an inline specification becomes a tree whose nodes carry kind, value, pointer, and one-based line and
column in document order (with array indexes in pointers, and `~` and `/` escaped as `~0` and `~1`); that a property
also remembers the position of its key and a location carries the source name; that every value kind (object,
array, string, number, boolean, null) is read, and that a byte-order mark does not shift columns whether the input is a
string or bytes; that columns count UTF-16 code units; and that a node describes itself with its kind, pointer, and
position.

##### Malformed JSON And Non-Specifications Fail At A Location

**Tests**: `SpecParserTests.SpecParser_Parse_MalformedJsonReportsFirstLocation`,
`SpecParserTests.SpecParser_Parse_NotASpecificationThrowsLocatedSpecException`,
`SpecParserTests.SpecParser_Parse_InvalidUtf8BytesThrowSpecException`,
`SpecParserTests.SpecParser_Parse_MalformedUtf8InStringIsRejectedNotReplaced`,
`SpecParserTests.SpecParser_Parse_MalformedUtf8InNameOrStructureIsRejected`,
`SpecParserTests.SpecParser_Parse_ValidMultiByteUtf8IsAccepted`,
`SpecParserTests.SpecParser_Parse_UnpairedSurrogateInStringIsRejected`

Asserts that malformed JSON (a bare word at line 2, column 11 followed by a second problem on line 3) is reported at the
first problem as `bad.json:2:11: Malformed JSON: ...` with the reader's exception as inner exception and no internal
property names in the message; that empty text, whitespace, a non-object root, trailing content, a trailing comma, a
comment, and an unterminated string each throw a located `VegaLiteSpecException` with a line and column of at least 1;
and that bytes that are not valid UTF-8 throw a `VegaLiteSpecException` rather than another type. Every kind of
malformed UTF-8 is rejected, never replaced by U+FFFD: an overlong NUL (`C0 80`), overlong three- and four-byte forms
and `C1 BF`, an encoded high and low surrogate (`ED A0 80`, `ED BF BF`), a code point above U+10FFFF (`F4 90 80 80`), a
stray continuation byte, truncated two-, three-, and four-byte sequences and `FE`, in a string value, in a property
name, and outside a string; well-formed two-, three-, and four-byte text is read unchanged with and without a byte-order
mark; and a string with an unpaired high or low surrogate is rejected at its line and column (line 2, column 10 after an
astral character that counts as two columns), while a valid surrogate pair is accepted.

##### Wrong Types And Values Are Reported At The First Offending Pointer

**Tests**: `SpecParserTests.SpecParser_Parse_WrongTypeReportsPointer`,
`SpecParserTests.SpecParser_Parse_FirstWrongValueInDocumentOrderIsReported`,
`SpecParserTests.SpecParser_Parse_ValidFormsOfCheckedPropertiesAreAccepted`,
`SpecParserTests.SpecParser_Parse_Phase4DataFormsAreAccepted`

Asserts that a known property with the wrong type or value (a theory of about seventy rows over `$schema`, `title`,
`description`, `name`, `width`, `height`, `padding`, `autosize`, `background`, `config`, `datasets`, `mark`, `layer`,
`vconcat`, `spec`, `data`, `encoding`, `params`, and `transform`, including nested properties such as
`/encoding/y/axis/orient`, `/data/sequence/start`, `/data/sequence/step`, `/data/sequence/as`, `/data/format/delimiter`
(empty, longer than one character, a quote, a line break, or not text), `/data/format/parse` (an unknown word or a
number), `/data/format/parse/a` (an unknown type, a number, or null), and `/data/format/property`) throws a
`VegaLiteSpecException` whose pointer names the offending value and whose location has a text position and the file;
that when a nested view comes before a bad top-level mark in the text, the first problem in document order is the one
reported; and that an original specification that touches every checked property with a valid but unusual form
(including expression references) is accepted with no warnings, and that the data forms of the data stage - DSV
delimiters (a pipe and a tab), `parse` as `"auto"`, `null`, or a map of `number`, `boolean`, `date`, `string`, and custom
date formats (`date:'...'`, `utc:'...'`), a `property` path, sequences with a negative or a zero step and a field name,
a named dataset, and top-level datasets given as an array, as text, and as an object - are accepted without a warning.

##### Duplicate Keys: Last Wins, First Position, One Warning

**Tests**: `SpecParserTests.SpecParser_Parse_DuplicateKeyLastWinsAndWarns`

Asserts that when the key `mark` is written twice (lines 2 and 4) and a nested object repeats a key, the last value
wins, the property keeps the position of the first occurrence, one `vl-duplicate-key` warning is produced per
duplicate at the later key, and a large object (which uses a name index) follows the same policy.

##### Unknown Properties Warn Once, Valid Keys Never Warn

**Tests**: `SpecParserTests.SpecParser_Parse_UnknownPropertiesWarnOnceAndValidKeysNeverWarn`,
`SpecParserTests.SpecParser_Parse_ValidTopLevelNamesDoNotWarn`

Asserts that unknown properties at the view, mark, encoding, and channel-definition levels produce one
`vl-unknown-property` warning each, at the key and not the value, that valid but unusual keys elsewhere do not warn,
and, as a regression guard for the conservative policy, that every name of the Vega-Lite 6 sets used for the check is
accepted silently (a theory over the names).

##### Limits Fail Closed

**Tests**: `SpecParserTests.SpecParser_Parse_DepthAndSourceLimitsFailClosed`,
`VegaLiteDocumentSpecModelTests.SpecParser_Parse_LargeInlineDataIsReadInOnePass`

Asserts with a depth limit of 3 that `{"a":[[1]]}` is accepted and one more level throws a located
`VegaLiteSpecException` ("deeper than the limit of 3"); that 5,000 nested `{"layer":[` levels fail closed under the
default and under the ceiling limit without a stack overflow; that source over `MaxSourceLength` (from a string, from
bytes, and the default 4,194,304 limit) throws `InvalidDataException`, while a source of exactly the limit (and with a
byte-order mark) is accepted; that an overlong string value, key, or array element is a located error naming
`MaxStringLength` at its pointer; that a number outside the double range (`1e999`) is rejected at its pointer; and
that 50,000 inline rows are read in one pass with their pointers and row count intact.

##### Schema Version Parsing

**Tests**: `SpecParserTests.SpecParser_ParseSchemaMajorVersion_ParsesTheMajorVersion`

Asserts that the major version is read from schema URLs in the documented forms (`v5.json`, `v6.json`, `v5.17.0.json`,
`v12.json`, and an upper-case `V4.json`) and that a URL without a Vega-Lite version, with an empty version number, or
with an implausibly large one (`v99999`) has none.

##### Views, Metadata, And Data References

**Tests**: `SpecParserTests.SpecTree_EnumerateViews_ListsNestedViewsParentsFirst`,
`SpecParserTests.SpecMetadata_Read_DescribesSchemaTitleMarksDataAndParameters`,
`SpecParserTests.SpecMetadata_Read_TitleIsPlainText`,
`SpecParserTests.SpecMetadata_Read_SequenceComputesLength`,
`SpecParserTests.SpecMetadata_Read_InlineValuesTakePrecedenceOverName`

Asserts that the views of a composition are enumerated parents before children, with the encoding's `layer` property
not mistaken for a view composition; that the metadata lists the schema, title, marks in the order written (with
pointers and positions), data references by kind with row counts, URLs, and locations (views without a source add
none), and distinct parameter names in order; that the title text is plain text for each form; that a sequence's
length is computed, including degenerate sequences; and that inline values win over the other source keys of a data
object.
