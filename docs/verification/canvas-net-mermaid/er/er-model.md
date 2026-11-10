### ErModel Unit Verification Design

This document describes the unit-level verification strategy for the `ErModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Er` namespace: the internal sealed `ErModel` (the database operations, the
completion, the metadata, the limits, `GetData`, the reports, and `Layout`, in two partial files) and its element
types (`ErEntity`, `ErAttribute`, `ErRelationshipSpec`, `ErRelationship`, `ErStyleClass`, and `ErSubgraph`).

#### Verification Approach

The unit is verified by opening short texts and inspecting the model, its layout data, its reports, or its scene, and
comparing the result with the values that Mermaid's `erDb.ts` and ER renderer give for the same input, stated in the
tests as literals: the ids, aliases, attributes, CSS classes, styles, and color indexes of entities; the ends and
specifications of relationships; the ids, titles, nodes, and directions of subgraphs; the text styles and compiled
styles; the counts and the direction; the limits and their locations; the nodes and edges of the layout data; the
reports; and the title and view box of the drawing. The model's agreement with Mermaid on whole texts is verified as
well by the upstream vectors of the ErParser unit, whose facts are the model's database facts and layout data.

No mocking or stubbing is used; the parser, the sanitizer, the configuration resolver, and the shared rendering are
real (the helper `ErFacts` parses a text into a model and resolves its default configuration). The internal types are
reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ErModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Er`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenario that lays out
- **Isolation**: Each test builds its own model; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any id, text, style, marker, location, count, node, edge, or coordinate that differs from the asserted
Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Er-ErModel-Entities: Entity Ids, Aliases, And Attributes

**Tests**: `ErModelTests.ErModel_AddEntity_IdsCountEntitiesAndBlocksAppend`

Asserts that an entity `A` with three attribute blocks (two before and one after a relationship to the quoted entity
`"Entry Log"`) gives the two entities `entity-A-0` and `entity-Entry Log-1`, numbered by the entities before them,
that the attributes of `A` are `one`, `two`, and `three` in order, the repeated blocks appending, and that the
relationship goes from `entity-A-0` to `entity-Entry Log-1`.

##### CanvasNetMermaid-Er-ErModel-Relationships: Relationships And Their Subgraph Ends

**Tests**: `ErModelTests.ErModel_AddRelationship_ToASubgraph_UsesItsIdAndAddsAHiddenEntity`,
`ErModelTests.ErModel_GetData_EntitiesAndRelationships_HaveMermaidsFields`

Asserts that after a subgraph `sub1` holding `X`, the relationship `sub1 ||--o| Y : roleA` gives the entities
`entity-X-0`, `entity-sub1-1`, and `entity-Y-2` (the entity named after the subgraph takes a number), the relationship
from `sub1` (the subgraph's id) to `entity-Y-2`, the color indexes 0, none, and 1, the layout nodes `sub1`,
`entity-X-0`, and `entity-Y-2` (the entity named after the subgraph is not drawn), and the one edge starting at
`sub1`; and that `p |o..|{ q : "owns"` maps to the edge `id_entity-p-0_entity-q-1_0` labeled `owns` with the start
marker `zero_or_one` (the left cardinality), the end marker `one_or_more` (the right one), and a dashed line, the type
`normal`, the curve `basis`, the class `relationshipLine`, the label position `c`, the thickness `normal`, and a
minimum length of 1, while `q u--o{ r : child` maps to an edge with the markers `md_parent` and `zero_or_more` and a
solid line.

##### CanvasNetMermaid-Er-ErModel-Subgraphs: Subgraph Nodes, Titles, And Order

**Tests**: `ErModelTests.ErModel_AddSubgraph_NodesAreUniqueAndNotInEarlierSubgraphs`,
`ErModelTests.ErModel_GetData_SubgraphsComeFirstInReverseOrder`

Asserts that a subgraph `" S "[" Sales area "]` holding `A`, the relationship `A ||--o{ B : r`, and a nested subgraph
`T` holding `B`, followed by a subgraph `U` holding `A` and `C`, gives the subgraphs `T`, `S`, and `U` in the order
they end, with the nodes `B` for `T`, `A` and `T` for `S` (the repeated `A` once, and `B` left to the nested `T` that
ended first), and `C` for `U` (`A` is already in `S`), the trimmed id `S`, and the trimmed title `Sales area`; and that
a subgraph `Outer["Outer area"]` holding `A` and a nested `Inner` (with `direction LR`) holding `B`, followed by a
subgraph `Other` holding `C`, gives the layout nodes `Other`, `Outer`, `Inner`, `entity-A-0`, `entity-B-1`, and
`entity-C-2` in that order (the clusters first, the last subgraph first) with the parents none, none, `Outer`,
`Outer`, `Inner`, and `Other`, the cluster `Outer` with the label `Outer area`, the shape `rect`, the group flag, and a
padding of 8, and the cluster `Inner` with the direction `LR`.

##### CanvasNetMermaid-Er-ErModel-Styles: Style Classes, Classes, And Styles

**Tests**: `ErModelTests.ErModel_GetData_StylesAndClasses_CompileLikeMermaid`

Asserts that `classDef hot fill:#f96,color:#036, fill-color:red` keeps the styles `fill:#f96`, `color:#036`, and
`fill-color:red` and the text styles `color:#036` and `bgFill-color:red` (only the styles with `color`, the first
`fill` written `bgFill`); that the subgraph `S` with `class S hot` and `style S stroke:#f00` maps to a cluster with the
CSS classes `hot`, the one inline style `stroke:#f00`, and the compiled styles `fill:#f96`, `color:#036`,
`fill-color:red`, `color:#036`, and `bgFill-color:red`; that the entity `A:::hot`, with `classDef default
stroke:#333`, has the compiled styles of the `default` class first (`stroke:#333`) and then those of `hot`; and that
`style B stroke-dasharray: 5 5` gives the entity `B` the inline style `stroke-dasharray:55`, its components joined
without white space.

##### CanvasNetMermaid-Er-ErModel-Metadata: Counts, Direction, And Titles

**Tests**: `ErModelTests.ErModel_Metadata_CountsEntitiesRelationshipsAndSubgraphs`

Asserts that a text with the accessible title `Shop`, a `direction LR` statement, a subgraph `Sales` holding `ORDER`,
the relationships `CUSTOMER ||--o{ ORDER : places` and `CUSTOMER ||--|| Sales : owns`, and a last `direction RL`
statement gives two nodes (the entity named after the subgraph `Sales` is not counted, although the model keeps three
entities), two edges, and one subgraph, the direction `RL` (the last top-level statement), the accessible title
`Shop`, and no title.

##### CanvasNetMermaid-Er-ErModel-Limits: The Node, Edge, And Joined Label Limits

**Tests**: `ErModelTests.ErModel_Limits_EntitiesAndSubgraphs_CountAgainstMaxNodes`,
`ErModelTests.ErModel_Limits_TooManyRelationships_ThrowsLimitExceeded`,
`ErModelTests.ErModel_Limits_JoinedTexts_ThrowLimitExceeded`,
`ErModelTests.ErModel_Limits_JoinedTextsAtTheLimit_AreAccepted`

Asserts that with `MaxNodes` of 3 (a theory with three cases) a fourth entity is `mermaid-limit-exceeded` at its
statement (line 5); that after a subgraph `S` holding `A`, the relationship `S ||--o{ B : r` is refused at line 5,
because the entity named after the subgraph counts too; and that a subgraph `S` holding `A` and a nested subgraph `T`
holding `B` is refused at the `subgraph` keyword of `S` (line 2), because subgraphs and entities count together; each
with a message that names `MermaidLimits.MaxNodes`. Asserts that with `MaxEdges` of 2 two relationships are accepted,
while a third is `mermaid-limit-exceeded` at its statement (line 4) with a message that names
`MermaidLimits.MaxEdges`. Asserts that with `MaxLabelLength` of 12 (a theory with four cases) the joined texts are
`mermaid-limit-exceeded`, located at the statement, with a message that names `MermaidLimits.MaxLabelLength`: the text
style `bgFill:colors` of `classDef c fill:colors` at line 2, the CSS classes `default abc def` of `class A abc,def` at
line 3, the subgraph classes `alpha charlie` of `class S alpha,charlie` at line 4, and the compiled styles
`stroke:red;fill:red` of `class A a,b` at line 5, checked when the model is completed and located at that `class`
statement. Asserts that with the same limit the texts exactly at it are accepted: the CSS classes `default c ab` of an
entity, its compiled style `fill:colo` (no text style, as it has no `color`), and the compiled styles `fill:colo` and
`s` of a subgraph.

##### CanvasNetMermaid-Er-ErModel-LayoutData: The Layout Data Of A Render

**Tests**: `ErModelTests.ErModel_GetData_EntitiesAndRelationships_HaveMermaidsFields`,
`ErModelTests.ErModel_GetData_SubgraphsComeFirstInReverseOrder`,
`ErModelTests.ErModel_GetData_SpacingAndDirection_FollowTheConfiguration`

Asserts that the entity `p["Person"]` with the attribute `string name PK "full"` maps to the node `entity-p-0` with
the label `p`, the shape `erBox`, the label type `markdown`, the CSS classes `default`, and an entity box with the
alias `Person` and the one attribute of the type `string`, the name `name`, the key `PK`, and the comment `full`; that
the relationship edges carry their markers, patterns, and fields (see the Relationships scenario); that the layout data
has the type `er`, the direction `TB`, the default spacing of 140 between nodes and 80 between ranks, and the four
markers `only_one`, `zero_or_one`, `one_or_more`, and `zero_or_more`; that the clusters come first in reverse order
with their parents, labels, paddings, and directions (see the Subgraphs scenario); and that `er.nodeSpacing` 100,
`er.rankSpacing` 60, and a `direction RL` statement give the layout data the direction `RL`, a node spacing of 100,
and a rank spacing of 60.

##### CanvasNetMermaid-Er-ErModel-Unsupported: Looks And Layouts Are Reported In Order

**Tests**: `ErModelTests.ErModel_ReportUnsupported_LookAndLayout_AreReportedInOrder`

Asserts that a configuration with the `handDrawn` look and the `elk` layout reports, through a sink that does not
throw, `mermaid-look-unsupported` at line 3 and then `mermaid-layout-unsupported` at line 4, the lines of their keys,
each with a hint.

##### CanvasNetMermaid-Er-ErModel-Drawing: The Frame Of The Drawing

**Tests**: `ErModelTests.ErModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox`

Asserts that a document with the frontmatter title `Shop`, an `er.titleTopMargin` of 30, and one entity lays out into
a scene with one title text, anchored in the middle at the baseline -30 (above the drawing at the configured title
margin), inside the view box, and that the view box is the box of the one entity node grown by 8 on the left, the
right, and the bottom (to three decimals).
