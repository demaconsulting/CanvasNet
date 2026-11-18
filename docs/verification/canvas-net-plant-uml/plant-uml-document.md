## PlantUmlDocument Unit Verification Design

This document describes the unit-level verification strategy for the `PlantUmlDocument` unit: the public API of the
`DemaConsulting.CanvasNet.PlantUml` namespace (the entry points and metadata of `PlantUmlDocument`, the options, the
limits, the records, codes, and exceptions) and the internal report sink.

### Verification Approach

The unit is verified through its public API with small original texts in memory and in temporary files, and the
internal `PlantUmlReportSink` directly through the test assembly's `InternalsVisibleTo` access. The unit's
collaborators, the `Preprocessor` and `DiagramDetector` units of the Syntax subsystem, are used as is; no mocking is
required. Unit tests reside in `PlantUmlDocumentTests.cs`, `PlantUmlLimitsTests.cs`,
`PlantUmlRecordsAndExceptionsTests.cs`, and `PlantUmlReportSinkTests.cs` within the
`DemaConsulting.CanvasNet.PlantUml.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: temporary files in the system temporary folder, deleted by each test
- **Mocking**: None required

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any type, name, value, code, location, or delivery order that differs from the expected literal constitutes
a failure.

### Unit-Level Test Scenarios

#### CanvasNetPlantUml-PlantUmlDocument-ParseOpen: The Entry Points Read Alike

**Tests**: `PlantUmlDocumentTests.PlantUmlDocument_ParseAndOpen_SameText_Agree`,
`PlantUmlDocumentTests.PlantUmlDocument_Open_SourceName_DefaultsToPath`,
`PlantUmlDocumentTests.PlantUmlDocument_Open_Utf8_IsStrictAndDropsBom`,
`PlantUmlDocumentTests.PlantUmlDocument_EntryPoints_InvalidArguments_Throw`

Asserts that a string, a file, and a stream of the same text give the same type and metadata; that a file's source
name is its path unless the options name it and a stream is unnamed; that a byte-order mark is dropped and invalid
UTF-8 is a syntax error of the named source; and that null, blank, unreadable, and missing arguments throw the
documented exceptions.

#### CanvasNetPlantUml-PlantUmlDocument-TextLimit: An Oversized Text Is Refused

**Tests**: `PlantUmlDocumentTests.PlantUmlDocument_Parse_TextOverLimit_ThrowsInvalidData`

Asserts an `InvalidDataException` for a text over a small size limit, from a string and from a stream.

#### CanvasNetPlantUml-PlantUmlDocument-Metadata: A Type That Is Not Drawn Opens

**Tests**: `PlantUmlDocumentTests.PlantUmlDocument_Parse_UnsupportedType_OpensWithoutMetadata`

Asserts that a class diagram opens with the type `class`, not supported, no title, and zero counts.

#### CanvasNetPlantUml-PlantUmlDocument-Limits: The Limits Validate

**Tests**: `PlantUmlLimitsTests.PlantUmlLimits_Default_HasDocumentedValues`,
`PlantUmlLimitsTests.PlantUmlLimits_Init_BelowOne_Throws`,
`PlantUmlLimitsTests.PlantUmlLimits_Init_DepthOutsideRange_Throws`

Asserts every default value, that values below one are refused, and the ranges of the depth limits at their bounds.

#### CanvasNetPlantUml-PlantUmlDocument-Records: Records, Codes, And Exceptions

**Tests**: `PlantUmlRecordsAndExceptionsTests.PlantUmlLocation_ToString_Forms_AreFileLineColumn`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlLocation_IsKnown_DependsOnLine`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlDiagnostic_ToString_PrefixesLocationAndCode`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlUnsupportedFeature_ToString_IncludesFeatureAndLocation`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlSize_Equality_IsByValue`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlSyntaxException_Located_CarriesCodeLocationHintAndExpected`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlSyntaxException_StandardConstructors_UseGeneralCode`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlRenderException_Constructors_CarryCodeAndLocation`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlUnsupportedFeatureException_Constructors_CarryFeatureAndLocation`,
`PlantUmlRecordsAndExceptionsTests.PlantUmlDiagnosticCodes_AllCodes_AreDistinctPlantUmlIdentifiers`

Asserts the display forms of locations, diagnostics, and unsupported features with and without a location, value
equality, the codes, locations, hints, and base types of the three exceptions, and that the seventeen published codes
are distinct `plantuml-*` identifiers.

#### CanvasNetPlantUml-PlantUmlDocument-Reports: Reports Are Ordered, Unique, And Capped

**Tests**: `PlantUmlReportSinkTests.PlantUmlReportSink_Deliver_KeepsOrderAcrossKinds`,
`PlantUmlReportSinkTests.PlantUmlReportSink_Warn_Duplicate_IsRecordedOnce`,
`PlantUmlReportSinkTests.PlantUmlReportSink_BeyondCap_AddsSingleSuppressedNotice`,
`PlantUmlReportSinkTests.PlantUmlReportSink_Unsupported_Strict_RecordsAndThrows`,
`PlantUmlReportSinkTests.PlantUmlReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException`

Asserts that warnings and unsupported features are delivered in the order recorded, that a duplicate is recorded once,
that the reports beyond the cap become one suppression notice, that strict mode records the report and then throws,
and that a failing handler is ignored after a failure.
