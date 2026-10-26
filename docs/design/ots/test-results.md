## DemaConsulting.TestResults

This document describes the integration and usage design for the `DemaConsulting.TestResults` OTS software
item.

### Purpose

`DemaConsulting.TestResults` is DEMA Consulting's library for building test-result collections and serializing
them to the common result formats. It is chosen so that the tool's self-validation mode produces the same TRX
and JUnit XML files as the other DEMA Consulting tools, which ReqStream and the CI systems consume as
qualification evidence, without a hand-written serializer.

### Features Used

- `TestResults`, `TestResult`, and `TestOutcome` - accumulate the outcome, duration, and failure message of each
  self-test
- `TrxSerializer.Serialize` (namespace `DemaConsulting.TestResults.IO`) - writes MSTest TRX documents
- `JUnitSerializer.Serialize` (namespace `DemaConsulting.TestResults.IO`) - writes JUnit XML documents

### Integration Pattern

Unlike the repository's build-time OTS items, `DemaConsulting.TestResults` is a runtime dependency, referenced as
a standard `PackageReference` in `src/DemaConsulting.CanvasNet.Tool/DemaConsulting.CanvasNet.Tool.csproj`. It is
consumed only by the `Validation` unit; the library packages do not depend on it. It is part of the tool
package, where it is the package's only NuGet dependency. The `IO` serializers ship in the same package, so no
separate reference is needed. The unit creates one `TestResults` object per self-validation run, appends a
`TestResult` per self-test, and serializes the collection when a results file was requested. There is no
initialization or disposal requirement.
