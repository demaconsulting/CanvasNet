## Packaging Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Packaging` subsystem
(the `OpcPackage` and `SafeXml` units).

### Verification Approach

The `Packaging` subsystem is verified through unit tests under
`test/DemaConsulting.CanvasNet.Tests/Packaging/`: `OpcPackageTests` and `SafeXmlTests`. No
dedicated subsystem test file exists; subsystem-level requirements reuse the unit tests. The
consumers of the subsystem (`PptxDocument`, `VsdxDocument`, `SvgCodec`) additionally keep tests in
their own test projects proving that they translate the subsystem's exceptions into their own
exception types with unchanged wording, and that their documents still open and render; those tests
are listed in the consumers' own verification designs.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Mocking**: None required; the subsystem uses only the base class library.
- **Fixtures**: Every package is built in memory with `ZipArchive` over a `MemoryStream`, using
  synthetic part names and content, and every XML document is an inline string, so the tests are
  deterministic and need no files. The oversized-input tests use a stream that never ends and a
  zero-filled ZIP entry that inflates to one byte over the part bound.

### Coverage Summary

- **OpcPackage**: bounded open (never-ending stream, non-ZIP bytes), required parts and their
  validation, part-name rules, content-type resolution, relationship resolution including target
  normalization and root escape, XML part loading with a DTD or an oversized part, byte reads with
  the decompression bound, argument validation, and disposal are covered in `OpcPackageTests`.
- **SafeXml**: DTD rejection (external entity and entity expansion), the character bound, malformed
  input, built-in entities still loading, the zero/negative bound, argument validation, and the
  streaming reader are covered in `SafeXmlTests`.

### Acceptance Criteria

The `Packaging` subsystem's verification passes when every requirement-tagged test named in the
`OpcPackage` and `SafeXml` unit verification designs (`packaging/opc-package.md`,
`packaging/safe-xml.md`) passes without error or unexpected exception, on every target framework in
this repository's CI matrix.

### Test Scenarios

This subsystem has no dedicated subsystem-level test scenarios of its own beyond the unit-level
scenarios it is composed from:

- **OpcPackage** - bounded open, required parts, part names, content types, relationships, XML
  parts, part bytes, and validation; see `packaging/opc-package.md` for the complete named-scenario
  list
- **SafeXml** - hardened loading, the character bound, the streaming reader, and validation; see
  `packaging/safe-xml.md` for the complete named-scenario list

### Traceability

Every requirement in `docs/reqstream/canvas-net/packaging.yaml` and in the unit requirement
documents `packaging/opc-package.yaml` and `packaging/safe-xml.yaml` links to one or more of these
tests.
