### DescriptionParser Unit Verification Design

This document describes the unit-level verification strategy for the `DescriptionParser` unit in the
`DemaConsulting.CanvasNet.PlantUml.Description` namespace.

#### Verification Approach

The unit is verified by reading small original diagram bodies (after the `Preprocessor`, with a `component` detected
type) and asserting the model it builds: the elements with their ids, symbols, displays, stereotypes, colors, and
containers, the links with their settings, the notes with their targets, the facts, the theme, and the reports; and
by asserting the located errors and limits. No mocking is required. Unit tests reside in
`Description/DescriptionParserTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An id, a symbol, a text, a setting, a count, a code, or
a line that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Description-DescriptionParser-Declarations: Declarations

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Declarations_ReadNamesAndDisplays`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_Shorthands_GiveTheirSymbols`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_MultilineDescriptions_ReplaceTheName`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_StereotypesAndColors_AreRead`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_UseCaseForms_ReadNamesAndSymbols`

Asserts the ids and texts of `component Web`, `"Payment Service" as PS`, `API as "Public API"`, `node Db as Store`,
`[Data Access] as DA`, a stereotype before `as`, and a quoted database name; the symbols of `[C]`, `() I`,
`() "Long I" as LI`, `(U)`, `(V)/`, `:A:`, `:B:/`, `"Disp" as X` (an actor), `actor/`, `usecase/`, and `node [N2]` (a
component); the descriptions in brackets (with text on the opening and closing lines) and in quotes over several
lines; several stereotypes, a fill, a fill with line and text colors, a line color, and a named color; and the use case
forms `:Account Owner: as AO`, `:Manager:/ as M`, a quoted two-line actor, `actor/`, `(Open Ticket) as OT`,
`(Archive)/ as AR`, `usecase Escalate as "..."`, and `usecase/` with their ids, texts, and symbols.

##### CanvasNetPlantUml-Description-DescriptionParser-Containers: Containers

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Containers_NestAndQualify`

Asserts the qualified ids of members of nested containers, an anonymous rectangle with an empty title, a node opened
twice, a component container with a stereotype, the depth and container of a member, and the facts.

##### CanvasNetPlantUml-Description-DescriptionParser-Names: Names

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Names_ResolveInScope`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_BareNames_BecomeInterfacesOrActors`

Asserts the ends of links inside and outside a container (a member of a container named by its short name from the
top, an element of the top named from inside a container, a qualified name) and that the names only written in links
are interfaces in a component diagram and actors in diagrams with an actor or a use case.

##### CanvasNetPlantUml-Description-DescriptionParser-Links: Links

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Links_ReadTheirArrows`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_UseCaseForms_ReadNamesAndSymbols`

Asserts the styles of `-->`, `..>`, `==>`, and `-[#red,dashed]->`, the decorations and end labels of `"1" o-- "many"`
with a label and its direction mark, a `left` hint, the sockets `)--`, `--(`, and `0)--`, a hidden link, a link of
length 1, the edge count, and no report; asserts the `<<include>>` and `<< extend >>` labels of a use case diagram
written `«include»` and `«extend»`, and a dashed `.>` of length 1.

##### CanvasNetPlantUml-Description-DescriptionParser-Notes: Notes

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Notes_AreAttached`

Asserts notes on the side of an element named plainly and in brackets, a multi-line note, a note on a link, a floating
note linked to an element, a note after a declaration, and a note on a container drawn floating in it and reported at
its line.

##### CanvasNetPlantUml-Description-DescriptionParser-Common: Common Statements

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_CommonStatements_AreRead`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_UseCaseForms_ReadNamesAndSymbols`

Asserts the title, the direction, the scale, `nodesep` and `ranksep`, and the component fill and node line of the
description skin parameters, with no report; asserts the actor fill of `actorBackgroundColor` and the use case line
of `usecaseBorderColor`.

##### CanvasNetPlantUml-Description-DescriptionParser-Unsupported: Unsupported Parts

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_UnsupportedParts_AreReported`,
`DescriptionParserTests.PlantUmlDescriptionParser_Parse_Notes_AreAttached`

Asserts the reports, at their lines and with `plantuml-statement-unsupported`, of a port and a link to it (left out), a
middle decoration, `together`, `hide`, `remove`, a process (drawn as a rectangle), an archimate element, a json
block, a URL, and a stereotype of a link, and the links that are kept.

##### CanvasNetPlantUml-Description-DescriptionParser-Errors: Located Errors

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Parse_Errors_AreLocated`

Asserts a located `plantuml-syntax-error` with a hint, at the expected line, for a component declared again as a node, a
name of a link declared as a component, an unclosed container, a stray `}`, a port at the top, `class` and `state`
statements, `[*]`, a description after a colon, a one-line description in brackets, a bad color, an interface with a
block, an actor with a block, a short name of elements in two containers, an unclosed multi-line description, a note
alias that names an element, an unknown statement, and a container declared with the name of a component.

##### CanvasNetPlantUml-Description-DescriptionParser-Limits: Limits

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Limits_FailClosedAtTheLine`

Asserts a located `plantuml-limit-exceeded` error naming the limit at the statement that crosses `MaxNodes` (with a
note), `MaxEdges`, `MaxLabelLength`, and `MaxNestingDepth`.
