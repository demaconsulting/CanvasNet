## Syntax Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Syntax` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Syntax`): the `Preprocessor` and `DiagramDetector` units working
together as the document uses them.

### Verification Approach

The subsystem is verified by running the preprocessor and the detector in sequence on full original texts, through
the test assembly's `InternalsVisibleTo` access, and asserting the detected type, its location in the original text,
and the statements the parsers would read. Both units are used as is; no mocking is required. Subsystem tests reside
in `Syntax/SyntaxTests.cs` within the `DemaConsulting.CanvasNet.PlantUml.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted. A type, a statement, or a location that differs from the expected literal constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Syntax-Framing, Detection, And Locations: A Full Text Is Read In Place

**Tests**: `SyntaxTests.PlantUmlSyntax_PreprocessAndDetect_FullText_KeepsOriginalLocations`

Reads a text with notes before `@startuml`, a comment, a macro, an empty line, and an indented participant, and
asserts the type `sequence`, the location `full.puml:6:3` of the participant, and the substituted message statement.

#### CanvasNetPlantUml-Syntax-Preprocessing: The Detector Reads The Substituted Statements

**Tests**: `SyntaxTests.PlantUmlSyntax_Detect_ReadsSubstitutedStatements`

Reads a diagram whose only statement is written with a macro that expands to `class`, and asserts the type `class` at
the line of the statement.

#### CanvasNetPlantUml-Syntax-Preprocessing: Included Statements Are Detected

**Tests**: `SyntaxTests.PlantUmlSyntax_Includes_AreReadBeforeDetection`

Preprocesses a text whose only statement includes a tagged file of the include fixture, and asserts that the detector
names an activity diagram at the first statement of the included file, located in that file.
