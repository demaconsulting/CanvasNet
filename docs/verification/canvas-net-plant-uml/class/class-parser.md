### ClassParser Unit Verification Design

This document describes the unit-level verification strategy for the `ClassParser` unit in the
`DemaConsulting.CanvasNet.PlantUml.Class` namespace.

#### Verification Approach

The unit is verified by parsing small original diagram bodies (framed and preprocessed by the Syntax subsystem, used
as is) and asserting the model (entities, kinds, displays, generics, stereotypes, colors, bodies, packages, notes,
links, rules, page texts, and settings), the reports, and the errors with their codes, lines, and hints. No mocking is
required. Unit tests reside in `Class/ClassParserTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A value, a count, a code, a line, or a hint that differs
from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Class-ClassParser-Declarations: Declarations Give Their Kind

**Tests**: `ClassParserTests.PlantUmlClassParser_Declaration_ReadsTheKind`,
`ClassParserTests.PlantUmlClassParser_Declaration_ReadsDisplayGenericStereotypesAndColors`

Asserts the kind of every declaration keyword; asserts a declaration with a display written with `as`, generic
parameters, stereotypes with a spot, colors, and an empty body, and the other order of the display and the name.

##### CanvasNetPlantUml-Class-ClassParser-Bodies: Bodies, Members, And Map Entries

**Tests**: `ClassParserTests.PlantUmlClassParser_Body_KeepsMembersAndSeparators`,
`ClassParserTests.PlantUmlClassParser_Map_ReadsEntriesAndReportsEntryLinks`

Asserts that a body keeps its member and separator lines in order, that `Name : text` adds a member and creates the
class, that an object reads its fields the same way, and that a map reads its entries.

##### CanvasNetPlantUml-Class-ClassParser-Names: Qualified Names And Packages

**Tests**: `ClassParserTests.PlantUmlClassParser_QualifiedNames_CreateAndResolvePackages`,
`ClassParserTests.PlantUmlClassParser_Packages_ReadSymbolsStereotypesAndColors`

Asserts that a dotted name creates its packages, that a name in a package is relative to it unless it starts with a
package at the top, that a short name finds its entity, and that `set namespaceSeparator none` keeps dots in names;
asserts nested packages and namespaces with a title, a symbol chosen by a stereotype, other stereotypes, and a color.

##### CanvasNetPlantUml-Class-ClassParser-Links: Relationships Keep Their Parts

**Tests**: `ClassParserTests.PlantUmlClassParser_Links_ReadDecorationsStylesAndLabels`

Asserts the decorations at both ends, the style, the length and the direction hint, the end labels, the label with its
direction mark, the color, and the links of `extends` and `implements`.

##### CanvasNetPlantUml-Class-ClassParser-Notes: Notes Attach Or Float

**Tests**: `ClassParserTests.PlantUmlClassParser_Notes_AttachToEntitiesLinksOrFloat`

Asserts notes on a side of a named entity, of the last declared entity, on the last link, and floating with an alias
that links connect.

##### CanvasNetPlantUml-Class-ClassParser-HideShow: Hide And Show Rules

**Tests**: `ClassParserTests.PlantUmlClassParser_HideShow_ReadsRulesAndReportsTheRest`

Asserts that the rules are kept in order with their gender and the empty flag, and that the forms that are not
applied are reported.

##### CanvasNetPlantUml-Class-ClassParser-Common: Common Statements

**Tests**: `ClassParserTests.PlantUmlClassParser_CommonStatements_AreRead`

Asserts the direction, the scale, the layout skin parameters, and the page texts.

##### CanvasNetPlantUml-Class-ClassParser-Unsupported: What Is Not Drawn Is Reported

**Tests**: `ClassParserTests.PlantUmlClassParser_Links_ReportWhatIsNotDrawn`,
`ClassParserTests.PlantUmlClassParser_RecognizedStatements_AreReported`,
`ClassParserTests.PlantUmlClassParser_Map_ReadsEntriesAndReportsEntryLinks`

Asserts one `plantuml-statement-unsupported` report at the statement for a crow's foot, roles, qualifiers, a middle
decoration, `norank`, a link stereotype, a link to a member, and an association class, with the link drawn without
them; for `together`, `remove`, `allowmixing`, `skin`, a `scale` form, a `json` block, a `diamond`, a class
with a visibility, and a class line style; and for a link from a map entry, drawn from the map.

##### CanvasNetPlantUml-Class-ClassParser-Errors: Malformed Statements Fail Located

**Tests**: `ClassParserTests.PlantUmlClassParser_MalformedStatements_AreLocatedErrors`

Asserts a `plantuml-syntax-error` at the expected line with a hint for an unclosed body and package, an unopened
brace, an actor and a use case in a class diagram (`allowmixing`), an invalid color, an unknown statement, a note on
a link before any link, an `extends` without a name, a word after a class name, a map line that is not an entry, and a
class with the name of a package.

##### CanvasNetPlantUml-Class-ClassParser-Limits: Limits Fail Closed

**Tests**: `ClassParserTests.PlantUmlClassParser_Limits_FailClosedAtTheLine`

Asserts a `plantuml-limit-exceeded` error at the line that crosses `MaxNodes` (declared, noted, and created by links),
`MaxEdges`, `MaxLabelLength` (a member and a link label), and `MaxNestingDepth` (implicit packages of a dotted name
and nested packages).
