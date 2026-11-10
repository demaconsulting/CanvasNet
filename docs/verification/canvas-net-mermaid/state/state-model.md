### StateModel Unit Verification Design

This document describes the unit-level verification strategy for the `StateModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.State` namespace: the internal sealed `StateModel` (the translation, the database,
the data fetcher, the metadata, the limits, `GetData`, the reports, and `Layout`, in two partial files) and its element
types (`StateStatement`, `StateNote`, `StateRecord`, `StateRelation`, `StateStyleClass`, `StateLink`, `StateNode`,
`StateNodeKind`, and `StateEdge`).

#### Verification Approach

The unit is verified by opening short texts and inspecting the model, its statement tree, its database, its nodes and
edges, its layout data, its reports, or its scene, and comparing the result with the values that Mermaid's
`stateDb.ts`, `dataFetcher.ts`, and unified state renderer give for the same input, stated in the tests as literals:
the ids of start and end states and of regions; the shapes, parents, labels, and descriptions of nodes; the DOM ids of
the shared counter; the note groups and note links; the style classes, text styles, compiled styles, and styles;
Mermaid's error message; the counts; the limits and their locations; the fields of the nodes, edges, and diagram of
the layout data; the reports; and the title, view box, and located failure of the drawing. The model's agreement with
Mermaid on whole texts is verified as well by the upstream vectors of the StateParser unit, whose facts are the
model's statement tree, database facts, and layout data.

No mocking or stubbing is used; the parser, the sanitizer, the configuration resolver, and the shared rendering are
real (the helper `StateFacts` parses a text into a model and resolves its default configuration). The internal types
are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `StateModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `State`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test builds its own model; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any id, shape, parent, label, style, message, location, count, node, edge, or coordinate that differs from
the asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-State-StateModel-Translation: The Statement Tree Translated As Mermaid Translates It

**Tests**: `StateModelTests.StateModel_SetRootDocument_StartAndEndStates_TakeTheirParentsId`,
`StateModelTests.StateModel_SetRootDocument_TrailingDivider_KeepsTheStatements`,
`StateModelTests.StateModel_SetRootDocument_LegacyDiagram_AppliesStatementsBeforeTranslating`

Asserts that `[*] --> A`, a composite state `A` holding `[*] --> B` and `B --> [*]`, and `A --> [*]` give the nodes
`root_start`, `A`, `A_start`, `B`, `A_end`, and `root_end` with the shapes `stateStart`, `roundedWithTitle` (the state
`A`, used before its body, becomes a composite state), `stateStart`, `rect`, `stateEnd`, and `stateEnd`, the parents
none, none, `A`, `A`, `A`, and none, and the database states `root_start`, `A`, and `root_end`; that a composite state
`Busy` whose last statement is a divider keeps its statements `a`, `divider-id-1`, `b`, and `divider-id-2`, so the
divider `divider-id-1` is a plain node with the shape `divider`, not a group, with an empty label, in `Busy`; and that a
`stateDiagram` text detected with a `state.defaultRenderer` of `dagre-d3` keeps the Mermaid id `state` and applies its
statements before the translation, so the database states are `start1` of type `start`, `A` of type `default`, and
`start2` of type `start`, while its layout data has the nodes `root_start`, `A`, and `root_end` and the diagram type
`stateDiagram`.

##### CanvasNetMermaid-State-StateModel-States: States, Types, And Descriptions

**Tests**: `StateModelTests.StateModel_GetData_Descriptions_FirstIsTheLabelAndTheRestAreDescriptions`,
`StateModelTests.StateModel_GetData_StatesAndTransitions_HaveMermaidsFields`

Asserts that `S : one`, `S : two`, and `S : three`, `R : only`, and a choice state `c1` with the description `a test`
give the nodes `S` with the shape `rectWithTitle` and the label `one`, `R` with the shape `rect` and the label `only`,
and `c1` with the shape `rect` (the description makes the choice state a plain state) and the label `a test`, with the
descriptions `two` and `three` for `S` and none for `R`; and that the states of a composite state `Comp` holding
`A --> B : go` are mapped with Mermaid's fields (see the LayoutData scenario).

##### CanvasNetMermaid-State-StateModel-Notes: Notes In Note Groups With The Shared Counter

**Tests**: `StateModelTests.StateModel_GetData_Notes_AreGroupedAndLinkedWithTheSharedCounter`,
`StateModelTests.StateModel_GetData_NoteOnAnUnseenState_HasNoShape`

Asserts that `A --> B`, `note left of A : first`, and `note right of B : second` give the nodes `A`, `B`,
`A----parent`, `A----note-1`, `B----parent`, and `B----note-2`, with the DOM ids `state-A-1`, `state-B-2`,
`state-A----parent-1`, and `state-A----note-1` for the first four (the transition takes the counter value 0 and each
note the next, and a state takes the value of the last statement that set it); the note group `A----parent` with a
padding of 16, as a group, without a parent; the note `A----note-1` with the flowchart padding of 15, the shape `note`,
the parent `A----parent`, and the position `left of`; the edges `edge0`, `A----note-1-A` (from the note to the state
for `left of`), and `B-B----note-2` (from the state to the note otherwise); and the note link with the classes
`transition note-edge` and no end marker. Asserts that `note right of X : hello`, on a state `X` that appears nowhere
before, gives the node `X` an empty shape (Mermaid's undefined) and the label `X`.

##### CanvasNetMermaid-State-StateModel-Styles: Style Classes, Classes, And Styles

**Tests**: `StateModelTests.StateModel_GetData_StyleClassesAndStyles_ApplyLikeMermaid`

Asserts that `classDef hot fill:#f96,color:#036`, `A:::hot --> B`, and `style B stroke:#333;` give the one style class
`hot` with the styles `fill:#f96` and `color:#036` and the text style `fill:#036` (the attribute with `color`, its
`color` written `fill`); the node `A` with the CSS classes `hot statediagram-state` and the compiled styles `fill:#f96`
and `color:#036`; and the node `B` with the style `stroke:#333`, its semicolon dropped.

##### CanvasNetMermaid-State-StateModel-Links: Click Links Kept Only As Checked

**Tests**: `StateModelTests.StateModel_CheckLink_ClickUrl_KeepsOnlyTheCheckedUrl`,
`StateModelTests.StateModel_SetRootDocument_EncodedUnsafeClicks_KeepOnlyTheCheckedUrls`

Asserts that the check of a click URL as written (a theory with one case for each URL) removes the quotes and gives
sanitize-url's result: `google.com`, `./docs/page.html`, and `https://example.com/` (for `https://Example.com`) for
safe links; `about:blank` for an empty URL and for `javascript:`, `&#106;`, `&#0x6A;`, Mermaid's entity placeholder of
`&#106;`, `java&#9;script:`, a tab inside the scheme, `%6A`, `&colon;`, `&Tab;` with `&CoLoN;` in mixed case, an
upper-case `JAVASCRIPT:`, `data:` (also padded and in mixed case), and `vbscript:` (also with `&colon;`); and
`avascript:alert(1)` for `&#x6A;avascript:alert(1)`, which sanitize-url reads as U+0000 and removes. Asserts that a
diagram with ten click statements (`click` with a tooltip and `click ... href`) on ten states keeps, in its links and in
the click statements of its statement tree alike, `about:blank` for `&#106;`, a tab inside the scheme, `%6A`,
`&colon;`, `&Tab;` with `&CoLoN;` in mixed case, `data:`, and `vbscript:`; `&&x6A;avascript:alert(1)` and
`java&script:alert(1)` for `&#x6A;` and `java&#9;script:`, whose references Mermaid's entity encoding changes so that
the scheme is no longer `javascript`; `https://example.com/b%20c` for `https://Example.com/a/../b c`; and the tooltip
`"tip"` with its quotes, as Mermaid keeps it.

##### CanvasNetMermaid-State-StateModel-Errors: Mermaid's Database Error, Located

**Tests**: `StateModelTests.StateModel_SetRootDocument_CompositeWithDescription_ThrowsLocatedGroupError`

Asserts that `state "Title" as Comp { A }` followed by `Comp : another` is `mermaid-syntax-error` with Mermaid's message
"Group nodes can only have label. Remove the additional description for node [Comp]", located at the statement that
added the description (line 5, column 1 of the named file).

##### CanvasNetMermaid-State-StateModel-Metadata: Counts, Direction, And Titles

**Tests**: `StateModelTests.StateModel_Metadata_CountsStatesTransitionsAndComposites`

Asserts that a text with the accessible title `Keys`, the direction statements `direction LR` and later
`direction RL`, a start transition to a composite state `Active` with two concurrent regions each holding a start
transition, a fork `split` with a transition from `Active` and a transition to the end state, and a note on `Active`
gives seven nodes (the three start states, `NumOff`, `CapsOff`, the fork, and the end state; the composite state, the
regions, the note, and the note group are not counted), five edges (the transitions; the note link is not counted),
and one subgraph (the composite state), the metadata direction `RL` (the last top-level direction statement), the
accessible title `Keys`, and no title.

##### CanvasNetMermaid-State-StateModel-Limits: The Node, Edge, Nesting, And Joined Label Limits

**Tests**: `StateModelTests.StateModel_Limits_ClustersNotesAndGroups_CountAgainstMaxNodes`,
`StateModelTests.StateModel_Limits_TooManyStates_ThrowsLimitExceeded`,
`StateModelTests.StateModel_Limits_TooManyTransitions_ThrowsLimitExceeded`,
`StateModelTests.StateModel_Limits_ConcurrentRegions_CountAsNestingLevels`,
`StateModelTests.StateModel_Limits_JoinedTexts_ThrowLimitExceeded`,
`StateModelTests.StateModel_Limits_JoinedTextsAtTheLimit_AreAccepted`,
`StateModelTests.StateModel_SetCssClass_RepeatedIds_ThrowsLimitExceeded`,
`StateModelTests.StateModel_AddStyleClass_RepeatedDefinitions_ThrowsLimitExceeded`,
`StateModelTests.StateModel_GetData_CompiledStyles_AreCheckedAndShared`,
`StateModelTests.StateModel_GetData_StatesNestedThroughNames_ThrowsLimitExceeded`,
`StateModelTests.StateModel_GetData_NestingCycle_IsLeftToTheLayout`

Asserts (a theory with two cases) that a composite state `C` with the regions holding `a` and `b` has five nodes (the
composite state, the two regions, and the two states) and that a state `A` with a note has three nodes (the state, the
note group, and the note), each accepted with `MaxNodes` equal to its count and refused with one less (the drawn nodes,
counted together) as `mermaid-limit-exceeded` at the line of the node too many (line 5 and line 3), with a message that
ends `nodes and subgraphs (MermaidLimits.MaxNodes)`. Asserts that with `MaxNodes` of 3 the statement
`class a,b,c,d hot`, which names four states that are never drawn (the states of the database, counted apart from the
drawn nodes), is `mermaid-limit-exceeded` at line 2. Asserts that with `MaxEdges` of 1 a transition and a note on its
state are accepted with two edges (the note link is not counted), while a second transition is
`mermaid-limit-exceeded` at its line (line 3) with a message that names `MermaidLimits.MaxEdges`. Asserts that a
composite state `A` holding a composite state `B` with two regions is accepted with `MaxNestingDepth` of 3, with two
regions, and refused with `MaxNestingDepth` of 2 as `mermaid-limit-exceeded` at the region too deep (the divider at
line 5), with a message that names `MermaidLimits.MaxNestingDepth`. Asserts that with `MaxLabelLength` of 10,
where every token fits, three joined or derived texts are `mermaid-limit-exceeded` at their lines with a message that
names `MermaidLimits.MaxLabelLength` (a theory): the descriptions `yellow` and `purple` under the title `orange`, 17
characters when joined with `<br/>` (line 4); the start id `Abcdefgh_start` of 14 characters (line 3); and the text
style `bgFill fill` of 11 characters that `classDef c fill color` derives (line 2). Asserts that with the same limit
the descriptions `de` and `fgh` under the title `abc`, 10 characters when joined, and the start id `Abcd_start` of 10
characters are accepted. Asserts that with `MaxLabelLength` of 30 `class A,A,A,A hot` is refused at the statement
(line 2, column 1) with the message `a label is 34 characters long` (four classes and `statediagram-state`), while
`class A,A,A hot` is accepted and draws the node with the classes `hot hot hot statediagram-state` (a state listed
again gets the class again, as in Mermaid). Asserts that with `MaxLabelLength` of 20 a third `classDef c a:b,c:d` is
refused at its line (line 4, column 1), while two are accepted with the styles `a:b`, `c:d`, `a:b`, `c:d`, and that
`classDef c color:red,x:yyyyyy` is accepted with the text style `fill:red` (the styles and the text styles are
checked apart). Asserts that with `MaxLabelLength` of 22 a state given the classes `c` (`a:b,c:d`) and `d`
(`e:f,g:h,i:j`) and a note draws the compiled styles `a:b`, `c:d`, `e:f`, `g:h`, `i:j` in class order, the very list
its note group draws, while one more style in `d` (23 characters compiled) is refused at the statement that gave the
state its second class (line 5, column 1). Asserts that with `MaxNestingDepth` of 3 four composite states chained by
name (`state Si { S(i+1) }` at the top, the last holding `X`) make `GetData` fail with a `MermaidRenderException` of
code `mermaid-limit-exceeded` at `X` (line 12, column 1) with the message
`composite states and their concurrent regions are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)`, while
three give `X`, `S2`, and `S1` the parents `S2`, `S1`, and `S0`; and that composite states `A` and `B` named in each
other give `GetData` the parents `B` and `A` without a limit failure, while measuring the document fails with
`mermaid-render-failed` from the layout.

##### CanvasNetMermaid-State-StateModel-LayoutData: The Layout Data Of A Render

**Tests**: `StateModelTests.StateModel_GetData_StatesAndTransitions_HaveMermaidsFields`,
`StateModelTests.StateModel_GetData_Descriptions_FirstIsTheLabelAndTheRestAreDescriptions`,
`StateModelTests.StateModel_GetData_Notes_AreGroupedAndLinkedWithTheSharedCounter`,
`StateModelTests.StateModel_GetData_StyleClassesAndStyles_ApplyLikeMermaid`

Asserts that a text with the `classic` look and `state.nodeSpacing` 70 in its frontmatter, `direction LR`, and a
composite state `Comp` holding `A --> B : go` maps to layout data of type `stateDiagram` with the direction `LR`, a
node spacing of 70, the default rank spacing of 50, and the one marker `barb`; the composite state as the first node,
with the shape `roundedWithTitle`, as a group, with the direction `TB` and the CSS classes
`statediagram-state statediagram-cluster` with a space before and after them; the state `A` with the radius 10 and 10,
a padding of 8, a centered label,
the label `A` of type `markdown`, the DOM id `state-A-0`, the parent `Comp`, and the look `classic`; and the one edge
`edge0` with the label `go`, the end marker `arrow_barb`, the classes `transition`, a minimum length of 1, and the style
`fill:none`. Asserts the first description as the label and the others as descriptions (see the States scenario), the
note groups, notes, DOM ids, and note links with their padding (see the Notes scenario), and the CSS classes, compiled
styles, and styles of the nodes (see the Styles scenario).

##### CanvasNetMermaid-State-StateModel-Unsupported: The Legacy Renderer, Looks, And Layouts Are Reported In Order

**Tests**: `StateModelTests.StateModel_ReportUnsupported_LegacyLookAndLayout_AreReportedInOrder`

Asserts that a legacy `stateDiagram` text with the `handDrawn` look, the `elk` layout, and
`state.defaultRenderer: dagre-d3` in its frontmatter reports, through a sink that does not throw,
`mermaid-layout-unsupported` at line 5 (the `state` key) with a message that names `dagre-d3`, then
`mermaid-look-unsupported` at line 3 and `mermaid-layout-unsupported` at line 4, the lines of their keys, each with a
hint; and that a `stateDiagram-v2` text with the default configuration reports nothing.

##### CanvasNetMermaid-State-StateModel-Drawing: The Frame Of The Drawing And A State Without A Shape

**Tests**: `StateModelTests.StateModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox`,
`StateModelTests.StateModel_Layout_StateWithoutShape_FailsLocatedAtItsNote`

Asserts that a document with the frontmatter title `Lights` and one state lays out into a scene with one title text,
anchored in the middle at the baseline -25 (above the drawing at the default title margin of 25), inside the view box,
and that the view box is the box of the one node grown by 8 on the left, the right, and the bottom (to three
decimals). Asserts that a document with `A --> B` and `note right of X : hello`, where `X` appears nowhere before its
note, fails its layout with a `MermaidRenderException` of code `mermaid-render-failed`, located at the note (line 3,
column 5 of the named file), whose message contains Mermaid's "No such shape: undefined", with a hint.
