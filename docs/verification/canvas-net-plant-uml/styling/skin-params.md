### SkinParams Unit Verification Design

This document describes the unit-level verification strategy for the `SkinParams` unit in the
`DemaConsulting.CanvasNet.PlantUml.Styling` namespace.

#### Verification Approach

The unit is verified with small original texts preprocessed by the Syntax subsystem (used as is, because the unit
reads its located statements) and with literal color texts, asserting the entries, the theme values, the colors, and
the reports. No mocking is required. Unit tests reside in `Styling/SkinParamsTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An entry, a style value, a color, or a report that
differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Styling-SkinParams-Forms: Line And Block Forms

**Tests**: `SkinParamsTests.PlantUmlSkinParams_TryRead_LineAndBlockForms`

Asserts the names, values, and lines of a one-line parameter and of a block of two parameters.

##### CanvasNetPlantUml-Styling-SkinParams-Subset: The Supported Subset Changes The Theme

**Tests**: `SkinParamsTests.PlantUmlSkinParams_Apply_SupportedSubset_ChangesTheme`

Applies thirteen parameters (a transparent background, the default font size, the arrow thickness, the maximum message
size, the foot box, the response below arrow, the message alignment, a participant background in mixed case, the group
background, the note border thickness, the arrow color, the life line background, and the title font style) and
asserts each effect with no report.

##### CanvasNetPlantUml-Styling-SkinParams-GraphSubset: The Class Parameters In Graph Diagrams Only

**Tests**: `SkinParamsTests.PlantUmlSkinParams_Apply_GraphSubset_OnlyInGraphDiagrams`

Applies a class background, a class stereotype size, a member color, an object border, a package border width, an
arrow color, an icon size of 0, and a circle radius of 9 to a graph diagram and asserts each value on the theme with no
report, including the arrow color on the links; applies them to a sequence diagram and asserts seven
`plantuml-skinparam-ignored` reports and the arrow color applied to its messages.

##### CanvasNetPlantUml-Styling-SkinParams-StateSubset: The State Parameters In Graph Diagrams Only

**Tests**: `SkinParamsTests.PlantUmlSkinParams_Apply_StateSubset_OnlyInGraphDiagrams`

Applies a state background, border color, and font size, a description color, a start color, an end color, and an end
color that is not a color to a graph diagram and asserts the state fill, line, and size, the description color, the
start and end circles filled and outlined in their colors, and one report for the bad color; applies them to a sequence
diagram and asserts seven `plantuml-skinparam-ignored` reports.

##### CanvasNetPlantUml-Styling-SkinParams-Colors: PlantUML's Color Forms

**Tests**: `SkinParamsTests.PlantUmlSkinParams_TryParseColor_ReadsPlantUmlForms`

Asserts a color name, a name with a hash, six and three hexadecimal digits, a gray digit, `transparent`, a gradient,
and the refusal of a word that is not a color and of a blank.

##### CanvasNetPlantUml-Styling-SkinParams-Ignored: What Has No Effect Is Reported

**Tests**: `SkinParamsTests.PlantUmlSkinParams_Apply_Ignored_ReportsWarnings`,
`SkinParamsTests.PlantUmlSkinParams_TrySkipStyle_ReportsAndSkips`

Asserts three warnings at their lines for an unknown parameter, a size that is not a number, and a stereotyped
parameter, with the note style unchanged; and that a style block is reported once and skipped to `</style>`.
