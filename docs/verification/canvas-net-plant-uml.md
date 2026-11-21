# System Verification Design

This document describes the system-level verification strategy for CanvasNetPlantUml.

## Verification Approach

`CanvasNetPlantUml` is verified at three tiers with xUnit v3 in the test project
`test/DemaConsulting.CanvasNet.PlantUml.Tests/`, which references only the PlantUml package and, through it, the core
`CanvasNet` package (no other format package, so the tests stay independent of the Viewer):

- **System tests** (described here) use only the public API, as a consumer would: they open original-work diagrams from
  in-memory text, streams, and temporary files with `PlantUmlDocument.Parse` and `PlantUmlDocument.Open`, supply
  `PlantUmlOpenOptions` and `PlantUmlLimits`, and assert on observable outputs: the metadata properties and the
  exceptions with their codes, locations, and hints, the sizes and sampled pixels of rendered surfaces, and the reports
  delivered to the handlers. The system test classes are `PlantUmlSystemIntegrationTests` (reading, metadata, errors,
  preprocessor, limits), `PlantUmlUnsupportedDiagramTests` (the types that are not drawn, strict mode),
  `PlantUmlSequenceIntegrationTests` (sequence diagrams from text to pixels), `PlantUmlActivityIntegrationTests`
  (activity diagrams from text to pixels), `PlantUmlClassIntegrationTests` (class and object diagrams from text to
  pixels), `PlantUmlStateIntegrationTests` (state diagrams from text to pixels), `PlantUmlDescriptionIntegrationTests`
  (use case, component, and deployment diagrams from text to pixels), `PlantUmlReferenceLayoutTests` (the layout of the
  original fixtures compared with the SVG goldens that PlantUML draws for them), `PlantUmlReferenceShapeTests` (the
  entity boxes, clusters, and links of the class, object, state, use case, component, and deployment fixtures compared
  with the goldens, positions aside), `PlantUmlIncludeTests` (include files and the image source of a linked diagram),
  `PlantUmlHardeningTests` (every limit and a seeded mutation fuzzer), and `PlantUmlGalleryTests` (the diagrams of the
  user guide gallery).
- **Subsystem tests** exercise each internal subsystem through its own entry points (see the subsystem verification
  documents, for example *Syntax Subsystem Verification Design*).
- **Unit tests** exercise each unit in isolation (see the unit verification documents).

The test methods of the units and subsystems are prefixed `PlantUml` (`PlantUmlPreprocessor_...`,
`PlantUmlSyntax_...`), and the system test methods `CanvasNetPlantUml_...`, so that the requirements trace never mixes
the evidence of this system with the Mermaid system's units of the same short names.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on Windows, Linux, and macOS, for net8.0, net9.0, net10.0, and
  net11.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: temporary files written and deleted by the tests, and the original fixtures and reference goldens of
  `Fixtures/` (see `Fixtures/reference/README.md` for their provenance); no network, no Java, no Graphviz
- **Mocking**: None

## Acceptance Criteria

A system-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted, on every platform and runtime of the platform requirements. A type, a count, a code, a location, or a hint
that differs from the expected literal constitutes a failure.

## Test Scenarios

### CanvasNetPlantUml-Open: The Entry Points Agree

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Open_StringStreamAndPath_Agree`

Opens the same sequence diagram (with a comment before `@startuml` and a variable) from a string, a stream, and a
temporary file, and asserts that all three are sequence diagrams.

### CanvasNetPlantUml-LocatedErrors: An Invalid Statement Fails Located

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Open_InvalidText_FailsLocated`

Parses a diagram with a statement no type accepts under a source name, and asserts the code `plantuml-syntax-error`,
the location `bad.puml:3:1`, and a hint.

**Tests**: `PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_SyntaxError_IsLocated`

Parses an activity diagram whose if is not closed and asserts `plantuml-syntax-error` at the line and column of the if
and a hint that names `endif`.

**Tests**: `PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_SyntaxErrors_AreLocated`

Parses a class diagram with an unclosed body and asserts `plantuml-syntax-error` at the line and column of the body
with a hint that names `}`; parses a class diagram with an actor and asserts the error at the actor with the
`allowmixing` hint.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_SyntaxErrors_AreLocated`

Parses a state diagram with an unclosed composite state and asserts `plantuml-syntax-error` at the line of its opening
with a hint that names `}`; parses a state diagram with a transition between two concurrent regions and asserts the
error at the transition with a message that names the concurrent regions.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_SyntaxErrors_AreLocated`

Parses a component diagram with a `class` statement and asserts `plantuml-syntax-error` at its line with the
`allowmixing` hint; parses a component diagram that declares a component with the name of an interface only written in
a link and asserts the error at the declaration with a message that the element is already defined; parses a component
diagram with an unclosed node and asserts the error at the line of its opening with a hint that names `}`.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_SyntaxErrors_AreLocated`

Parses a use case diagram with an actor that holds a use case and asserts `plantuml-syntax-error` at the actor with a
hint; parses a use case diagram that declares an actor with the name of a use case and asserts the error at the
declaration with a message that the element is already defined; parses a use case diagram with a `class` statement
and asserts the error at its line with the `allowmixing` hint.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_SyntaxErrors_AreLocated`

Parses a deployment diagram with a person that holds a node and asserts `plantuml-syntax-error` at the person with a
hint; parses a deployment diagram with an unclosed stack and asserts the error at the line of its opening; parses a
deployment diagram with a `class` statement and asserts the error at its line with the `allowmixing` hint.

### CanvasNetPlantUml-DiagramTypes: Every Type Is Recognized

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_OtherDiagramTypes_AreRecognized`

Opens a legacy activity diagram, a timing diagram, a mindmap, a gantt chart, and a JSON diagram,
and asserts the type name of each and that none is drawn by this version.

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_ComponentDiagram_IsDrawn`

Detects texts with a link between two components, a component keyword, an interface written `() HTTP`, and a component
in a node, and asserts the type `component` and that it is drawn.

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_UseCaseDiagram_IsDrawn`

Detects texts with a use case keyword, a link between use cases in parentheses, actors written `:Guest:` and
`:Account Owner: as AO`, and an actor with a boundary that holds a colored use case, and asserts the type `usecase` and
that it is drawn.

**Tests**: `DiagramDetectorTests.PlantUmlDiagramDetector_Detect_DeploymentDiagram_IsDrawn`

Detects texts with a node, an artifact linked to a node, a queue before a storage that holds a file, and a person
linked to a hexagon, and asserts the type `deployment` and that it is drawn.

**Tests**: `PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_BorderMessages_AreReported`

Opens and renders diagrams with messages to and from the diagram border (`[->` and `->]` after a declaration, `[o->`
after a message, and `->]` alone), and asserts the type `sequence`, the `plantuml-statement-unsupported` reports at the
lines of the border messages, and a drawn surface.

### CanvasNetPlantUml-Preprocessor: The Subset Is Applied

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Preprocessor_SubsetIsApplied`

Parses a diagram whose type is decided by a macro and a variable after a comment, and asserts the detected type.

**Tests**: `PlantUmlIncludeTests.CanvasNetPlantUml_Includes_NetworkIsNeverRead`

Renders a sequence diagram that includes two URLs and the standard library, and asserts three
`plantuml-include-unsupported` reports at their lines and the diagram drawn without them.

### CanvasNetPlantUml-Includes: Local Files Are Read Under The Policy

**Tests**: `PlantUmlIncludeTests.CanvasNetPlantUml_Includes_LocalFilesAreRead`,
`PlantUmlIncludeTests.CanvasNetPlantUml_Includes_FailuresAreLocated`

Opens the include fixture (a fragment with a variable and a skin parameter, the block of another diagram in a
subfolder that includes the fragment again, and a repeated include), and asserts the activity type, the title and the
six nodes, the same diagram when the text is parsed with the folder as its base directory, and a strict render without
reports; asserts `plantuml-include-failed` with a hint for a missing file (at `a.puml:3:1`), for a text without a
folder, and for a file outside the roots of the policy (at `main.puml:3:1`, with the `AccessDenied` refusal as the
inner exception).

### CanvasNetPlantUml-Limits: An Oversized Text Is Refused

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Limits_OversizedText_IsRefused`,
`PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_LayoutWorkLimit_Fails`

Parses a text of fifty messages under a size limit of one hundred characters and asserts an `InvalidDataException`;
measures the groups fixture under a layout work limit of 20 and asserts a `plantuml-limit-exceeded` render error.

**Tests**: `PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_StructuralLimits_AreLocated`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_TextSize_IsRefused`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_IncludeLimits_AreLocated`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_RenderLimits_FailClosed`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_LargeDiagramsWithinTheLimits_Render`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_DenseClassGraph_LayoutWorkIsBounded`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_DenseStateGraph_LayoutWorkIsBounded`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_DenseComponentGraph_LayoutWorkIsBounded`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_DenseUseCaseGraph_LayoutWorkIsBounded`,
`PlantUmlHardeningTests.CanvasNetPlantUml_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions`

For `MaxNodes`, `MaxEdges`, `MaxLabelLength`, and `MaxNestingDepth`, in sequence, activity, class, state, use case,
component, and deployment diagrams and in the preprocessor, asserts `plantuml-limit-exceeded` at the source name and
line where the text crosses the limit, naming the limit, with a hint, and that the text opens under the defaults;
asserts that a long text and a growing macro are refused under a size limit of 200; asserts that one include too many
fails at the directive of the main file and one level too deep at the directive of the included file; asserts
`plantuml-limit-exceeded` render errors under a layout work limit and a painted pixel limit, and two reports and one
suppression notice under a diagnostics limit of two; renders a chain of 400 actions, 32 nested ifs, and 200
participants; asserts that a dense class diagram of 120 links fails under a layout work limit of 1000 with a render
error at its first statement and is laid out under the default limit, that a dense state diagram with nested composite
states and concurrent regions does the same at its first statement, that a dense component diagram with nested
containers does the same at its first statement, and that a dense use case diagram with a boundary does the same at its
first statement and that a use case of 1300 lines is measured at least 182 pixels high at a scale of 0.01 (every line
inside its ellipse); and opens, measures, and renders mutants of every sequence, activity, class, object, state, use
case, component, deployment, and gallery fixture (truncated, deleted ranges, inserted tokens of the diagram languages
and of the preprocessor, duplicated and swapped lines, flipped characters) with a fixed seed, asserting that only
`PlantUmlSyntaxException` (with a documented code, the source name, a line, and a hint), a located
`plantuml-limit-exceeded` `PlantUmlRenderException`, and `InvalidDataException` are thrown, and never a layout defect.

### CanvasNetPlantUml-Sequence: Sequence Diagrams Are Drawn As PlantUML Draws Them

**Tests**: `PlantUmlReferenceLayoutTests.CanvasNetPlantUml_Sequence_ReferenceLayout_MatchesPlantUml`,
`PlantUmlReferenceLayoutTests.CanvasNetPlantUml_Sequence_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_Fixtures_RenderWithoutReports`,
`PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_Facts_CountParticipantsMessagesAndBoxes`

For every original sequence fixture (participants of every kind, arrows, activations, returns, numbering, notes,
groups, boxes, dividers, delays, spaces, page texts, and skin parameters), compares the layout with the SVG that
PlantUML 1.2026.10 draws for it (`Fixtures/reference/sequence`): the picture size within 3 %, and the anchor of every
text, the boxes of the participant heads and tails, the life lines, and the message lines within 1.5 pixels; asserts
that every fixture has a golden; renders every fixture in strict mode with no report at its measured size; and asserts
the title, node, edge, and subgraph counts of the page fixture.

**Tests**: `PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_ThickArrowsAndLargeLabels_LabelsClearTheArrows`

Lays out two messages with `sequenceArrowThickness 4` and `arrowFontSize 30`, with the response label above and
below its arrow, and asserts that no label box comes within 1 pixel of a message line's band or an arrow head's box,
each grown by half its stroke width.

### CanvasNetPlantUml-Activity: Activity Diagrams Are Drawn As PlantUML Draws Them

**Tests**: `PlantUmlReferenceLayoutTests.CanvasNetPlantUml_Activity_ReferenceLayout_MatchesPlantUml`,
`PlantUmlReferenceLayoutTests.CanvasNetPlantUml_Activity_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_Fixtures_RenderWithoutReports`,
`PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_Facts_CountNodesArrowsAndGroups`

For every original activity fixture (actions of one or several lines, ifs with elseif and else, nested ifs, while and
repeat loops with backward actions, forks, merges, splits, swimlanes, branches that change swimlane, notes, partitions,
arrows with labels and styles, kill and detach, connectors, page texts, and skin parameters), compares the layout with
the SVG that PlantUML 1.2026.10 draws for it (`Fixtures/reference/activity`): the picture size within 3 %, and the
anchor of every line of text, the action boxes, the circles, and the diamonds and hexagons within 1.5 pixels; asserts
that every fixture has a golden; renders every fixture in strict mode with no report at its measured size; and asserts
the title, node, edge, and subgraph counts of the page and partition fixtures.

### CanvasNetPlantUml-Class: Class And Object Diagrams Are Drawn With PlantUML's Shapes

**Tests**: `PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceEntities_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceClusters_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceLinks_MatchPlantUml`,
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_Fixtures_RenderWithoutReports`,
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Object_Fixtures_RenderWithoutReports`,
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_Facts_CountEntitiesLinksAndPackages`,
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Object_MapAndObject_AreDrawn`

For every original class and object fixture (every class kind, stereotypes, generics, members with every visibility
and modifier, separators with titles, relationships of every drawn decoration, style, length, and direction, end
labels and labels, packages and namespaces with their symbols, notes of every form, hide and show, page texts, the
direction, skin parameters, objects, and maps), compares the picture with the SVG that PlantUML 1.2026.10 draws for it
with its Smetana layout (`Fixtures/reference/class` and `Fixtures/reference/object`), positions aside: the same
entities, clusters, and notes; every entity box of the same size within 1.5 pixels or 3 %, with the same fill and line
colors, texts at the same offsets inside the box, spots, icons, and separators; every cluster title at the same offset
from its outline; every link matched by its ends with the same line style, decorations congruent with PlantUML's, and
labels; asserts that every fixture has a golden; renders every fixture in strict mode with no report at its measured
size; asserts the title, node, edge, and subgraph counts of class and object diagrams; and asserts that an object
diagram draws its objects without spots and its map as a table.

**Tests**: `PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_ArrowEnds_LandOnTheBoxSidesNotTheCorners`

Lays out a class above three classes spread wider than it, linked by an association with the multiplicity 1 and two
extensions, and asserts that every arrowhead tip lies on the bottom side of the upper class at least 5 pixels from its
corners, that every line ends on the top side of a lower class at least 5 pixels from its corners, and that the
multiplicity is within 12 pixels of its arrowhead tip.

### CanvasNetPlantUml-State: State Diagrams Are Drawn With PlantUML's Shapes

**Tests**: `PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_State_ReferenceElements_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_State_ReferenceLinks_MatchPlantUml`,
`PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_Fixtures_RenderWithoutReports`,
`PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_Facts_CountStatesTransitionsAndComposites`,
`PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_ConcurrentRegions_DrawDashedSeparators`

For every original state fixture (states declared in every form, descriptions with rules, composite states, concurrent
regions stacked and side by side, the start, end, choice, fork, join, and junction pseudo-states, histories, notes of
every form, `hide empty description`, the direction, arrows of every length, direction, and style, colors, and page
texts), compares the picture with the SVG that PlantUML 1.2026.10 draws for it with its Smetana layout
(`Fixtures/reference/state`), positions aside: the same states, pseudo-states, composite states, and notes (by
PlantUML's qualified ids; histories, forks, joins, and the states of `hide empty description`, which PlantUML draws
without a group, excepted); every state box of the same size within 1.5 pixels or 3 %, with the same fill and line
colors and the name and descriptions at the same offsets; every composite state with its header line, its title, and
its region separators where PlantUML draws them; the start and end circles with PlantUML's radii and colors; the
choice diamonds with PlantUML's size and colors; one drawn transition per PlantUML link, matched by its ends, with the
same line style, arrow heads congruent with PlantUML's, and labels; asserts that every fixture has a golden; renders
every fixture in strict mode with no report at its measured size; asserts the title, node, edge, and subgraph counts of
the composite, concurrent, notes, and page fixtures; and asserts that the separators of concurrent regions are dashed
8 and 10, 1.5 wide, across the composite state for `--` and down it for `||`.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_TransitionLabels_ClearOfLinesAndStates`

Lays out a state diagram with a labeled transition into a state beside another, a labeled transition between two
states of a row, and a labeled transition between two states of a composite state, and asserts that every transition
label is at least 3 pixels from every drawn transition line and arrowhead and at least 8 pixels from every state.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_TransitionsBetweenTheSameStates_AreDrawnApart`

Lays out a state diagram with two transitions from one state to another and one back, and asserts that halfway
between the two states the three transition lines are at least 4 pixels apart.

### CanvasNetPlantUml-Component: Component Diagrams Are Drawn With PlantUML's Shapes

**Tests**: `PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceElements_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceContainers_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceLinks_MatchPlantUml`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_Fixtures_RenderWithoutReports`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_Facts_CountElementsLinksAndContainers`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_Names_ResolveAsPlantUml`

For every original component fixture (components declared in every form, interfaces with the sockets of required and
provided interfaces, names only written in links, containers of every container symbol nested, links of every style,
decoration, label, and hint, notes of every form, stereotypes and colors, the leaf symbols left to right, page texts,
and skin parameters), compares the picture with the SVG that PlantUML 1.2026.10 draws for it with its Smetana layout
(`Fixtures/reference/component`), positions aside: the same elements, containers, and notes (by PlantUML's qualified
ids); every element with the same number of shapes, the same outline size within 1.5 pixels or 3 %, the same fill,
line color, and line width, and every text at the same offset from its outline; every container title and stereotype
at the same offset from its outline; one drawn link per PlantUML link, matched by its ends, with the same line style,
decorations congruent with PlantUML's, and labels; asserts that every fixture has a golden; renders every fixture in
strict mode with no report at its measured size; asserts the title, node, edge, and subgraph counts of the basic,
containers, notes, and page fixtures; and asserts that a short name from outside its container links its element and
that a name only written in a link is an interface circle.

### CanvasNetPlantUml-UseCase: Use Case Diagrams Are Drawn With PlantUML's Shapes

**Tests**: `PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceElements_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceContainers_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceLinks_MatchPlantUml`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_Fixtures_RenderWithoutReports`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_Facts_CountElementsLinksAndContainers`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_BareNames_BecomeActors`

For every original use case fixture (actors and use cases declared in every form and their business forms, multi-line
names, boundaries of rectangles and packages nested, colors, links with `<<include>>` and `<<extend>>` labels and
generalizations, names only written in links, stereotypes, notes of every form, left to right, page texts, and the actor
and use case skin parameters), compares the picture with the SVG that PlantUML 1.2026.10 draws for it with its Smetana
layout (`Fixtures/reference/usecase`), positions aside, as for the component fixtures: every actor (its head the
outline) and use case (its ellipse) with the same number of shapes, size, colors, and text offsets, every boundary title
at the same offset, and every link with the same style, decorations, and labels; asserts that every fixture has a
golden; renders every fixture in strict mode with no report at its measured size; asserts the title, node, edge, and
subgraph counts of the basic, boundary, notes, and page fixtures; and asserts that a name only written in a link is an
actor drawn as a stick figure and that a use case in a boundary is linked by its short name.

### CanvasNetPlantUml-Deployment: Deployment Diagrams Are Drawn With PlantUML's Shapes

**Tests**: `PlantUmlReferenceShapeTests.CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceElements_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceContainers_MatchPlantUml`,
`PlantUmlReferenceShapeTests.CanvasNetPlantUml_Description_ReferenceLinks_MatchPlantUml`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_Fixtures_RenderWithoutReports`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_Facts_CountElementsLinksAndContainers`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_Symbols_HaveTheirOwnDrawing`

For every original deployment fixture (nodes, artifacts, databases, and clouds; every leaf symbol; artifact, storage,
file, queue, stack, hexagon, and node containers nested with stereotypes; links between nested elements with labels,
styles, and a direction hint; boundary, control, and entity symbols; notes; colors and the artifact and queue skin
parameters; left to right with a title), compares the picture with the SVG that PlantUML 1.2026.10 draws for it with
its Smetana layout (`Fixtures/reference/deployment`), positions aside, as for the component fixtures: every element
with the same number of shapes, size, colors, and text offsets (a boundary, control, entity, and person by its circle,
a stack by its box without an outline; a label, which has no outline, and a hexagon, whose outline the Smetana layout
leaves out and CanvasNet draws, by the offsets of their texts from each other), every container title and stereotype at
the same offset, and every link with the same style, decorations, and labels; asserts that every fixture has a golden;
renders every fixture in strict mode with no report at its measured size; asserts the title, node, edge, and subgraph
counts of the nodes, containers, notes, and left-to-right fixtures; and asserts that every symbol of the symbols
fixture has its own outline but the label, with no report, and that a circle is drawn as an interface circle.

### CanvasNetPlantUml-Render: Measuring Reports What Rendering Reports

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_MeasureReportsAsRender`

Measures a mindmap and asserts the size of 20 by 20 and the one report that rendering delivers.

**Tests**: `PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_Render_ObservesCancellation`

Renders the packages class fixture with a canceled token and asserts an `OperationCanceledException`.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_Render_ObservesCancellation`

Renders the composite state fixture with a canceled token and asserts an `OperationCanceledException`.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_Render_ObservesCancellation`

Renders the containers component fixture with a canceled token and asserts an `OperationCanceledException`.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_Render_ObservesCancellation`

Renders the boundary use case fixture with a canceled token and asserts an `OperationCanceledException`.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_Render_ObservesCancellation`

Renders the containers deployment fixture with a canceled token and asserts an `OperationCanceledException`.

### CanvasNetPlantUml-UnsupportedTypes: An Empty Picture And A Report

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_RendersEmptyPictureAndReports`

Renders a legacy activity diagram and asserts a fully transparent picture of 20 by 20, no warning, and one
`plantuml-diagram-unsupported` report at `flow.puml:2:1` that names the type and has a hint.

### CanvasNetPlantUml-StrictMode: The First Unsupported Construct Throws

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_StrictThrows`

Renders a legacy activity diagram in strict mode and asserts the exception's feature and that its report was
delivered first.

**Tests**: `PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_UnsupportedStatements_AreReported`

Renders a sequence diagram with `newpage` and `ref over`, asserts their two reports at their lines and the drawn
picture, and asserts that strict mode throws for the first of them.

**Tests**: `PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_UnsupportedStatements_AreReported`

Renders an activity diagram with a box-style action ending and a `goto`, asserts their two reports at their lines and
the drawn picture, and asserts that strict mode throws for the first of them.

**Tests**: `PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_UnsupportedStatements_AreReported`

Renders a class diagram with a crow's foot, a role, and a `together` group, asserts their three
`plantuml-statement-unsupported` reports at their lines and the drawn picture, and asserts that strict mode throws.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_UnsupportedStatements_AreReported`

Renders a state diagram with an `<<entryPoint>>` state, a cross at the start of a transition, and `hide` of a state,
asserts their three `plantuml-statement-unsupported` reports at their lines and the drawn picture, and asserts that
strict mode throws.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_UnsupportedStatements_AreReported`

Renders a component diagram with a port in a component container, a middle decoration, and an artifact (drawn as a
rectangle), asserts their three `plantuml-statement-unsupported` reports at their lines and the drawn picture, and
asserts that strict mode throws.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_UnsupportedStatements_AreReported`

Renders a use case diagram with a stereotype of a link, a URL, and `hide`, asserts their three
`plantuml-statement-unsupported` reports at their lines, the `plantuml-skinparam-ignored` warning of `actorStyle`, and
the drawn picture, and asserts that strict mode throws.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_UnsupportedStatements_AreReported`

Lays out a deployment diagram with an action and a process container, asserts the deployment type, their two
`plantuml-statement-unsupported` reports at their lines saying they are drawn as rectangles, the action's outline, the
process's cluster, and the links, and asserts that strict mode throws.

### CanvasNetPlantUml-DefaultLook: PlantUML's Colors, Identical Pixels

**Tests**: `PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_Render_IsDeterministicAndScaled`

Renders a sequence diagram and asserts the white background, the grey-blue `#E2E2F0` inside a participant head, and the
pale yellow `#FEFFDD` inside a note; renders the groups fixture twice and asserts identical pixels, and asserts that
its `scale 2` doubles the measured size.

**Tests**: `PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlActivityIntegrationTests.CanvasNetPlantUml_Activity_Render_IsDeterministicAndScaled`

Renders an activity diagram and asserts the white background, the light gray `#F1F1F1` inside an action box, and the
dark `#222222` of the start node; renders the fork fixture twice and asserts identical pixels, and asserts that a
`scale 2` doubles the measured size.

**Tests**: `PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_Render_IsDeterministicAndScaled`

Renders a class diagram and asserts the white background, the light gray `#F1F1F1` inside a class, the green `#ADD1B2`
spot of a class, and the purple `#B4A7E5` spot of an interface; renders the relations fixture twice and asserts
identical pixels, and asserts that a `scale 2` doubles the measured size.

**Tests**: `PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_Render_IsDeterministicAndScaled`

Renders a state diagram and asserts the state type, the white background, the light gray `#F1F1F1` inside a state, and
the dark `#222222` of the start circle; renders the concurrent fixture twice and asserts identical pixels, and asserts
that a `scale 2` doubles the measured size.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_Render_IsDeterministicAndScaled`

Renders a component diagram and asserts the component type, the white background, and the light gray `#F1F1F1` inside
a component and inside an interface circle; renders the containers fixture twice and asserts identical pixels, and
asserts that a `scale 2` doubles the measured size.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_Render_IsDeterministicAndScaled`

Renders a use case diagram and asserts the use case type, the white background, and the light gray `#F1F1F1` inside an
actor's head and inside a use case ellipse; renders the boundary fixture twice and asserts identical pixels, and asserts
that a `scale 2` doubles the measured size.

**Tests**: `PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_DefaultSkin_PaintsPlantUmlColors`,
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_Render_IsDeterministicAndScaled`

Renders a deployment diagram and asserts the deployment type, that it is drawn, and the light gray `#F1F1F1` inside a
node, a queue, and a boundary's circle; renders the containers fixture twice and asserts identical pixels, and asserts
that a `scale 2` doubles the measured size.

**Tests**: `PlantUmlGalleryTests.CanvasNetPlantUml_Gallery_HoldsEveryDrawnDiagramType`,
`PlantUmlGalleryTests.CanvasNetPlantUml_Gallery_RendersStrictWithoutWarnings`

Opens every diagram of the user guide gallery (`docs/user_guide/examples/plantuml-*.puml`, at least 18) and asserts
that each is a supported sequence, activity, class, object, state, use case, component, or deployment diagram and that
every drawn type is shown;
renders each in strict mode and asserts no report, the measured size, and a picture that is not blank.

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_SkinParamBlock_ColorsTheElementInEveryType`,
`PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_SkinParamBlock_ColorsTheParticipants`

Lays out a diagram of every drawn type (sequence, activity, class, object, state, use case, component, and deployment)
that starts with a `skinparam element { BackgroundColor #DBEAFE }` block and asserts its type, no report in strict
mode, and a shape filled with `#DBEAFE`; lays out a sequence diagram with that block for `participant`, with and
without a participant declaration, and asserts the sequence type, no report, and every participant head filled with
`#DBEAFE`.

### CanvasNetPlantUml-ImageSource: A Linked Diagram Is Drawn

**Tests**: `PlantUmlIncludeTests.CanvasNetPlantUml_ImageSource_DrawsALinkedDiagram`

Draws the include fixture through a folder image resolver with the PlantUML image source and asserts the size the
document measures (its includes read in its own folder) and no note; draws an activity fixture into a placement of 100
by 100 and asserts that it fits; asserts that the resolver claims `.puml`.

### CanvasNetPlantUml-Platform: Every Platform And Runtime

**Tests**: `PlantUmlSequenceIntegrationTests.CanvasNetPlantUml_Sequence_DefaultSkin_PaintsPlantUmlColors` and
`PlantUmlClassIntegrationTests.CanvasNetPlantUml_Class_DefaultSkin_PaintsPlantUmlColors`, and
`PlantUmlStateIntegrationTests.CanvasNetPlantUml_State_DefaultSkin_PaintsPlantUmlColors`, and
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Component_DefaultSkin_PaintsPlantUmlColors`, and
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_UseCase_DefaultSkin_PaintsPlantUmlColors`, and
`PlantUmlDescriptionIntegrationTests.CanvasNetPlantUml_Deployment_DefaultSkin_PaintsPlantUmlColors` with the `windows@`,
`ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, `net10.0@`, and `net11.0@` source filters

The representative test is evidence for each platform and runtime only from a test run on that platform or runtime.
