### SvekLayout Unit Verification Design

This document describes the unit-level verification strategy for the `SvekLayout` unit in the
`DemaConsulting.CanvasNet.PlantUml.Svek` namespace, including its work meter `PlantUmlLayoutWorkMeter`.

#### Verification Approach

The unit is verified with small diagrams built in the tests from test entity images and cluster images, laid out with
the dagre layout of the CanvasNetLayout system (used as is), asserting the boxes, routes, labels, notes, clusters, and
the work and failures of the layout. No mocking is required. Unit tests reside in `Svek/SvekLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A box, a route, a relation between places (below, left
of, inside, overlapping), a count, a code, or a message that differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Svek-SvekLayout-Ranks: Ranks And Spacing

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_TopToBottom_SecondEndBelow`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LeftToRight_SecondEndRight`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_Length_AddsRanks`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_RankSep_ChangesTheSpace`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_EmptyDiagram_HasNoSize`

Asserts that a link of length 2 puts its second end a rank below the first, at least 60 below it, with a route from the
bottom border of the first to the top border of the second; asserts the second end right of the first in a left to right
diagram; asserts that a link of length 3 reaches lower than one of length 2; asserts that the diagram's space between
ranks replaces PlantUML's; asserts that an empty diagram has no size.

##### CanvasNetPlantUml-Svek-SvekLayout-Hints: Direction Hints And Hidden Links

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_Hints_UpReversesLeftRightSameRank`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_HiddenLink_LaidOutNotDrawn`

Asserts that up puts the second end above the first with the route still from the first end to the second, and that
left and right keep both ends in one rank; asserts that a hidden link gives its ends ranks and is not drawn.

##### CanvasNetPlantUml-Svek-SvekLayout-SameRank: Links In One Rank

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_LengthOne_SameRankStraightWithLabelAbove`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LengthOne_ChainAndCycle_OrderedRows`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LinkInOneRankAcrossANode_IsAnArc`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_ParallelLinksInARow_AreSpreadApart`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_RowLabels_ClearOfBothBoxesAndTheLine`

Asserts that a link of length 1 keeps its ends in one rank, the first end left of the second, with a straight route
between their borders and its label above the middle of the line; asserts that a chain of one-rank links written out of
order keeps every first end left of its second at least 35 apart and that a cycle of one-rank links gives one row
without overlaps; asserts that a link in one rank that would cross another node is an arc over it; asserts that of three
links in a row between the same two nodes the first is straight, the second an arc at least `ParallelArcStep` below, and
the third an arc at least `ParallelArcStep` above, with the label of the second above the middle of its curve, clear
of it by `LabelClearance` and within 10 pixels more. Asserts that a wide label between two narrow entities of a row
is at least 8 pixels from each and `LabelClearance` above the line.

##### CanvasNetPlantUml-Svek-SvekLayout-Clusters: Clusters And Regions

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_ClusterWithoutCrossingLinks_LaidOutOnItsOwn`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_CompoundCluster_HeaderBandAndCutLinks`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_NestedCompoundClusters_NoOverlaps`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_ConcurrentRegions_ArrangedWithSeparators`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LabelOfLinkFromCompoundCluster_StaysOutside`

Asserts that a cluster that no link crosses encloses its members with its padding and header and that a link to it
ends on its border; asserts that a cluster that links cross is tight around its members with its padding and header
band, overlaps nothing outside it, and cuts a link to it at its border; asserts the header bands of nested clusters
and that no two entities overlap; asserts stacked regions with horizontal separators, regions side by side with
vertical separators, and a link between two regions left unrouted; asserts that the label of a link that leaves a
cluster that links cross does not overlap the cluster.

##### CanvasNetPlantUml-Svek-SvekLayout-Notes: Notes

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_AttachedNotes_OnEachSideWithPointers`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_AttachedNotes_KeepClearOfLinks`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_AttachedNotesLeftToRight_KeepTheirSides`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_FloatingNoteWithOneLink_PointsAtTheEntity`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_NoteOnLink_BelowTheLabel`

Asserts the left and right notes of an entity 18 from it in its rank and the top and bottom notes at least a rank space
above and below it, each with a pointer from the side facing the entity to its border; asserts that with a link between
two entities that have notes on every side the route crosses no note and no note overlaps an entity; asserts that in a
left to right diagram the top and bottom notes are 18 above and below the entity and the left and right ones are before
and after it; asserts that a floating note with one link to an entity points at it and does not draw the link while a
note with two links keeps them; asserts that a note on a link is in the link's label slot below its label.

##### CanvasNetPlantUml-Svek-SvekLayout-Routes: Routes And Labels

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_SelfAndParallelLinks_AreRouted`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_ClipOutlines_RoutesEndOnThem`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_Labels_PlacedBesideTheRoute`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LinksFromBelow_EndOnTheSidesNotTheCorners`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LabelOfLinkIntoARow_ClearOfTheDrawnCurve`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_UnlabeledLinksBetweenTheSameTwoNodes_AreKeptApart`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LabelWithNoClearPlace_StaysWhereItWasPlaced`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LinksCrowdingOneSide_EndApartWithTheMultiplicityClear`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_EndLabelBetweenTwoHeads_OverlapsNoHead`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_LeftToRightLinksBothWays_EndApartOnOneSide`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_FanOutWithOneLabel_LabelClearOfEveryLine`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_EndLabelsAtOneNode_OverlapNoOtherLabelOrHead`

Asserts that a link from an entity to itself and two parallel links have routes that start and end on the borders and
that the parallel routes differ; asserts that routes end on an ellipse outline and on a circle inside a larger box;
asserts the middle label beside its route with its triangle pointing along the link and the end labels near their ends
off the entities. Lays out an entity above three entities spread wider than it (the class-ends shape) and asserts that
every link ends on the bottom side of the entity above and on the top side of the entity below, at least 5 pixels from
the corners, that the link to the entity below the middle runs straight down at the middle of the entity above, and
that the end label at the entity above is within 12 pixels of its end and off the entity. Lays out the state-labels
shape (a labeled link from an entity above into an entity beside another in a row, whose drawn curve runs to the
entity's place in the row) and asserts that the label is at least `LabelClearance` from the drawn curve, measured
along it, and overlaps no entity. Lays out two unlabeled links from one entity to another and one back, and asserts
distinct routes whose curve middles are at least 5 pixels apart and whose ends are on the borders. Lays out a labeled
loop on an entity beside a large entity of its row, so that no position within 32 pixels is clear, and asserts that
the label stays at its place right of the loop. Lays out an entity above four entities wider than it, linked by an
arrow with the multiplicity 1 and three extensions, and asserts that the four ends are on the bottom side of the entity
above, at least 5 pixels from its corners and at least `EndSpacing` apart, and that the multiplicity is within 12
pixels of its end and overlaps no triangle of the other links. Lays out the arrow with the multiplicity between four
extensions on a side too short to keep them `EndSpacing` apart, and asserts that the multiplicity overlaps no
triangle, is within 24 pixels of its end, and that the five ends are spread evenly over the part of the side the
corner insets leave. Lays out, from left to right, three links leaving an entity and one arriving at it, and asserts
that the four ends at the entity are on its border and at least 10 pixels apart. Lays out an entity above four
entities linked by arrows, the third with a label, and asserts that the label is at least `LabelClearance` from the
curve and the arrowhead of every link. Lays out an entity above four entities linked by a composition, an aggregation,
an arrow with a label, and an extension, with multiplicities at both ends, and asserts that no two of the seven end
labels overlap and that none overlaps the middle label or a decoration of another link.

##### CanvasNetPlantUml-Svek-SvekLayout-Limits: Work, Limits, And Failures

**Tests**: `SvekLayoutTests.PlantUmlSvekLayout_Run_Work_IsDeterministic`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_SmallBudget_LocatedLimitExceeded`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_Canceled_Throws`,
`SvekLayoutTests.PlantUmlSvekLayout_Run_InconsistentDiagram_Throws`,
`SvekLayoutTests.PlantUmlLayoutWorkMeter_Failures_AreLocatedPlantUmlErrors`

Asserts that two layouts of a diagram charge the same positive work; asserts a `plantuml-limit-exceeded` error located
at the diagram that names `PlantUmlLimits.MaxLayoutWork` past a small budget; asserts that a canceled call throws
`OperationCanceledException`; asserts that an inconsistent diagram throws `ArgumentException`; asserts the meter's limit
failure with its code, message, hint, and location, its layout failure with `plantuml-render-failed`, and that it
recognizes a `PlantUmlRenderException` and no other exception as its failure.
