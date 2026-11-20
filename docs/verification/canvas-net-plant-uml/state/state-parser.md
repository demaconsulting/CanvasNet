### StateParser Unit Verification Design

This document describes the unit-level verification strategy for the `StateParser` unit in the
`DemaConsulting.CanvasNet.PlantUml.State` namespace.

#### Verification Approach

The unit is verified by parsing small original diagram bodies (framed by the test helpers and run through the
`Preprocessor` and `DiagramDetector` units) and asserting the model: the elements with their ids, kinds, displays,
descriptions, colors, and regions; the notes with their targets and sides; the transitions with their settings and
labels; the settings of the diagram; the reports with their codes and lines; and the located errors with their codes,
lines, and hints. The ids are the ones the PlantUML jar gives the same elements. No mocking is required. Unit tests
reside in `State/StateParserTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An id, a kind, a text, a setting, a code, or a line
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-State-StateParser-Declarations: Declarations And Descriptions

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_Declarations_ReadNamesAndDisplays`,
`StateParserTests.PlantUmlStateParser_Parse_Descriptions_AddLines`,
`StateParserTests.PlantUmlStateParser_Parse_Colors_AreRead`

Asserts a name, a quoted display with an alias in both orders, and a quoted name alone; the descriptions after a colon
in the declaration and written as `Name : text` (creating the state), and a description of a pseudo-state reported;
a fill, a line color with `##` or `line:`, and a line style that is reported.

##### CanvasNetPlantUml-State-StateParser-Transitions: Transitions

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_Transitions_ReadTheirArrows`,
`StateParserTests.PlantUmlStateParser_Parse_Notes_AreAttached`

Asserts that a transition creates its states and keeps its length, direction, style, color, and label as written, that
a reverse arrow puts the head at its first end, and that a transition may end at a floating note.

##### CanvasNetPlantUml-State-StateParser-PseudoStates: Pseudo-States

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_PseudoStates_AreScoped`,
`StateParserTests.PlantUmlStateParser_Parse_Stereotypes_ChooseTheKind`

Asserts one start and one end per composite state with PlantUML's ids, the history of the current composite state, and
the history of a named state (made composite); asserts the kind each stereotype chooses and that the border symbols are
reported and drawn as states.

##### CanvasNetPlantUml-State-StateParser-Composites: Composite States And Regions

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_CompositeStates_NestAndResolve`,
`StateParserTests.PlantUmlStateParser_Parse_ConcurrentRegions_AreNumbered`,
`StateParserTests.PlantUmlStateParser_IsConcurrentSeparator_RecognizesDashesAndBars`

Asserts the ids of nested members, the resolution of names in the composite states around and by a unique name, and
`end state` closing a block opened with `begin`; asserts the region names numbered across the diagram and the
arrangement chosen by the first separator; asserts that two or more dashes or bars, and nothing else, are separators.

##### CanvasNetPlantUml-State-StateParser-Notes: Notes

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_Notes_AreAttached`,
`StateParserTests.PlantUmlStateParser_Parse_ScopedNoteAlias_LinksTheNote`

Asserts the notes on each side of a state, on the last transition, floating with an alias and linked, on the last
element, and on a composite state (reported and drawn floating in its region); asserts that inside a composite state
`A` the short name `N` links the floating note `A.N`, that a state `N` of a nested composite state is nearer than the
note, and that the diagram lays out.

##### CanvasNetPlantUml-State-StateParser-Common: Common Statements

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_CommonStatements_AreRead`

Asserts `hide empty description` (other `hide` forms reported), the page texts, the direction, `scale`, the layout skin
parameters, and the state skin parameters.

##### CanvasNetPlantUml-State-StateParser-Unsupported: Reports

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_UnsupportedParts_AreReported`,
`StateParserTests.PlantUmlStateParser_Parse_Stereotypes_ChooseTheKind`,
`StateParserTests.PlantUmlStateParser_Parse_TransitionLeavingFirstRegion_IsReported`

Asserts `plantuml-statement-unsupported` at their lines for a cross at the start of a transition, a stereotype of a
transition, a concurrent separator outside a composite state, a `json` block, `remove`, the border symbols, and a
transition that leaves the first region of a composite state with concurrent regions (kept and drawn from the
border).

##### CanvasNetPlantUml-State-StateParser-Errors: Located Errors

**Tests**: `StateParserTests.PlantUmlStateParser_Parse_Errors_AreLocated`

Asserts a located `plantuml-syntax-error` with a hint at the expected line for an arrow that is not a transition, an
unclosed composite state, a closing brace without a composite state, a transition between concurrent regions, a
pseudo-state made composite, a statement of another diagram, a bad color, a state without a name, a state declared with
the alias of a floating note, and a floating note with the alias of a state.

##### CanvasNetPlantUml-State-StateParser-Limits: Limits

**Tests**: `StateParserTests.PlantUmlStateParser_Limits_FailClosedAtTheLine`

Asserts a located `plantuml-limit-exceeded` error that names the limit at the line that creates one state or note more
than `MaxNodes`, one transition more than `MaxEdges`, a text longer than `MaxLabelLength`, and a composite state deeper
than `MaxNestingDepth`.
