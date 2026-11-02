### SafeXml Unit Verification Design

<!-- cspell:ignore XXE -->

This document describes the unit-level verification strategy for the `SafeXml` static class.

#### Verification Approach

`SafeXmlTests.cs` loads inline XML strings, wrapped in a UTF-8 `MemoryStream` for the stream
overloads, so no files are needed. The hardening tests use an external-entity DTD (a `SYSTEM`
entity pointing at a path that is never read, because the DTD is refused first) and a nested
entity-expansion document. The bound tests use a document longer than a small bound and the same
document under a larger one. Arguments are validated before reading, so the validation tests need no
valid document.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None
- **Isolation**: Each test builds its own input; no shared state

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed for
each requirement ID passes across every target framework in this repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Packaging-SafeXml-Hardened: A DTD Is Refused, Ordinary Input Loads

**Tests**: `SafeXml_Load_WellFormedStreamAndString_ReturnDocument`,
`SafeXml_Load_ExternalEntityDocumentType_ThrowsXmlException`,
`SafeXml_Load_EntityExpansionDocument_ThrowsXmlException`,
`SafeXml_Load_BuiltInEntitiesAndCharacterReferences_Load`

Loads the same small document from a stream and from a string and asserts equal trees and an open
stream; loads the external-entity and the entity-expansion documents through both overloads and asserts
`XmlException`; loads a document of 9,000 characters made of built-in entity and character references
and asserts it loads, proving the entity bound does not reject legitimate references.

##### CanvasNet-Packaging-SafeXml-Bounded: The Character Bound Is Enforced

**Tests**: `SafeXml_Load_DocumentLargerThanBound_ThrowsXmlException`,
`SafeXml_Load_MalformedXml_ThrowsXmlException`

Loads a 500-character element under a bound of 100 (from a string and from a stream) and asserts
`XmlException`, and under a bound of 600 and asserts it loads; loads mismatched and unterminated
markup and asserts `XmlException`.

##### CanvasNet-Packaging-SafeXml-Reader: The Streaming Reader Is Hardened

**Test**: `SafeXml_CreateReader_RootStartTag_IsBoundedAndRefusesDocumentType`

Reads a root attribute through the reader and asserts its value and that the stream is still open;
reads an oversized attribute under a small bound and a DTD-bearing document and asserts both raise
`XmlException` from the first read.

##### CanvasNet-Packaging-SafeXml-Validation: Arguments Are Validated Before Reading

**Tests**: `SafeXml_Load_NonPositiveBound_ThrowsArgumentOutOfRangeException`,
`SafeXml_Load_NullOrUnreadableInput_ThrowsWithParameterName`

Passes bounds of 0, -1 and `int.MinValue` to every method and asserts
`ArgumentOutOfRangeException` for `maxCharacters`; passes a null stream, a null string and a stream
that cannot be read and asserts the exception types and parameter names.
