### TextLayout Unit Verification Design

This document describes the unit-level verification strategy for the `TextLayout` unit in the
`DemaConsulting.CanvasNet.PlantUml.Scenegraph` namespace.

#### Verification Approach

The unit is verified with the bundled fonts through the core `Fonts` subsystem (used as is: the fonts are the subject
of the measurement), comparing widths and line metrics with the values of PlantUML's SVG of the same texts (the
`textLength` attributes and the participant box heights), and asserting the lines, runs, styles, and reports of
laid-out texts. A report sink of the root namespace collects the reports. Unit tests reside in
`Scenegraph/TextLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A width more than 0.01 pixel away from PlantUML's, a
line metric more than 0.001 away, or a run, line, or report that differs from the expected literal constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Scenegraph-TextLayout-Metrics: PlantUML's Metrics

**Tests**: `TextLayoutTests.PlantUmlTextLayout_Width_AliceAt14_MatchesPlantUml`,
`TextLayoutTests.PlantUmlTextLayout_LineMetrics_FollowLogicalFont`,
`TextLayoutTests.PlantUmlTextLayout_Draw_PlacesBaselinesAndAlignment`

Asserts that `Alice` at 14 pixels is 30.345 wide and `hello` at 13 is 27.466 (PlantUML's own values), that a line of
14 pixels is 17.609 high with a descent of 3.076, and the baselines and the centered alignment of a drawn block.

##### CanvasNetPlantUml-Scenegraph-TextLayout-Fonts: The Bundled Families

**Tests**: `TextLayoutTests.PlantUmlTextLayout_CssFamily_MapsToBundledFamilies`

Asserts the mapping of `SansSerif`, `Arial`, an unknown family, `Times`, `Courier`, and `Monospaced`.

##### CanvasNetPlantUml-Scenegraph-TextLayout-Lines: Line Breaks And Wrapping

**Tests**: `TextLayoutTests.PlantUmlTextLayout_Layout_LineBreaks_StackLines`,
`TextLayoutTests.PlantUmlTextLayout_Layout_Wrap_BreaksAtSpaces`

Asserts the lines of a text with `\n`, a real line feed, and an escaped backslash, the block height, and that a
wrapped text keeps its words with no line wider than the width while a long word stays whole.

##### CanvasNetPlantUml-Scenegraph-TextLayout-Creole: The Creole Subset

**Tests**: `TextLayoutTests.PlantUmlTextLayout_Layout_CreoleSubset_StylesRuns`,
`TextLayoutTests.PlantUmlTextLayout_Layout_LoneMarkupCharacters_AreText`

Asserts the bold, italic, monospace, underline, strike, color, size, and font of the runs of one line, the escaped
markup, and that a lone dash pair and a URL stay text.

##### CanvasNetPlantUml-Scenegraph-TextLayout-Reports: Unsupported Markup And Missing Glyphs

**Tests**: `TextLayoutTests.PlantUmlTextLayout_Layout_UnsupportedMarkup_IsReportedAndKept`,
`TextLayoutTests.PlantUmlTextLayout_Layout_MissingGlyph_Warns`

Asserts that an image tag and a sprite are kept as text with one `plantuml-markup-unsupported` report at the given
location, and that an emoji is measured and reported as `plantuml-text-missing-glyph`.
