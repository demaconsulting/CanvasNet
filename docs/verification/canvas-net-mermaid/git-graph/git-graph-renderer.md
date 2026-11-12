### GitGraphRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `GitGraphRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.GitGraph` namespace: the internal sealed `GitGraphRenderer` (the positions, the
arrows, and the labels, in three partial files), which draws a `GitGraphDiagram` as Mermaid's git graph renderer draws
it.

#### Verification Approach

`GitGraphRendererTests` verifies the text helpers (`Rows` and `Decode`) directly, and the drawing by laying out whole
texts through the document (`SceneQuery.Layout`, the test assembly's helper over the internal layout) and asserting
the branch lines, the commit places (the center of each commit's first bullet shape), the path data of the arrows, the
rotation and place of a commit label, the label rectangles and tags, the title, the view box, the paint, and the
failures of Mermaid's placement, of the work limit, and of cancellation. The expected numbers are those Mermaid draws
in Chrome for the same text in the seeded reference renders of the pinned Mermaid release (the empty graph's view box
of (-95.78125, -20.5, 103.78125, 37) is the one `mmdc` writes). The positions, boxes, and paint of every element of the
18 fixtures against the goldens drawn by Mermaid are verified at the system level by the reference layout tests.

No mocking or stubbing is used; the preprocessor, the detector, the configuration resolver, the git graph style sheet,
the `GitGraphParser` unit, the text metrics, and the bundled fonts are real. The internal types are reached through
the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GitGraphRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`GitGraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/gitgraph/04-rerouting.mmd` for the rerouting lanes
- **Fonts**: The bundled fonts
- **Isolation**: Each test lays out its own text

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any coordinate, rotation, text, color, view box (beyond 0.01 pixel), or failure that differs from the
asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Positions: Branches And Commits Where Mermaid Puts Them

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_BranchPositions_FollowTheLabelsAndRotation`,
`GitGraphRendererTests.GitGraphRenderer_Draw_CommitPositions_StepFiftyInEachDirection`,
`GitGraphRendererTests.GitGraphRenderer_Draw_ReusedIdWithParallelCommits_ArrowsKeepTheFirstPass`,
`GitGraphRendererTests.GitGraphRenderer_Draw_ReusedIdBottomToTopParallel_FailsAsMermaid`

Asserts that the branch lines of `main` and `dev` start at the y of -2 and 88 (90 apart, 40 more for the rotated
commit labels), at -2 and 48 when `rotateCommitLabel` is false, and top to bottom at the x of 0 and 107.390625 (90 and
half the width of the `main` label, 34.78125, apart); that two commits are placed at (10, -2) and (60, -2) left to
right, at (0, 40) and (0, 90) top to bottom, and in reverse order at (0, 40) and (0, 90) bottom to top; that with
`parallelCommits` and the id `p` used again after the commit `q` on `dev`, the second pass places `q` at (60, 88) from
the first pass's place of its parent, while the arrow `p->q` keeps the first pass's place and ends at (10, 88), with
the view box (-95.78125, -20.5, 173.78125, 145.157), as Mermaid draws it; and that the same text bottom to top fails the
render with `MermaidRenderException`, the code `mermaid-render-failed`, at line 9 (the commit `q`), with Mermaid's
message `Closest parent not found for commit q`.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Arrows: Arrows On Mermaid's Lanes

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_ReroutedArrows_FindMermaidsLanes`,
`GitGraphRendererTests.GitGraphRenderer_Draw_ReusedIdWithParallelCommits_ArrowsKeepTheFirstPass`

Asserts that the five rerouted arrows of the fixture `04-rerouting` (those with a bend left of the x of 15) run on the
lanes Mermaid's `findLane` gives them, at the y of 43, 26.8, 16.432, 9.79648, and 70: a lane at least 10 pixels from
the lanes taken before, or, when the search reaches its depth cut, the last candidate, used without being taken; with
bends of radius 10 (the fourth arrow's first bend ends at the y of -0.20352); and that an arrow is drawn from the
first pass's places, as in the scenario above.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Labels: Branch Labels, Commit Labels, And Tags

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_RotatedLabel_TurnsAboutTheMovedCommitPlace`,
`GitGraphRendererTests.GitGraphRenderer_Draw_EmptyNameAndTag_HaveNoTextBox`,
`GitGraphRendererTests.GitGraphRenderer_TextHelpers_SplitRowsAndDecodePlaceholders`

Asserts that the label `A` (8 pixels wide) of a commit at (0, -2) is moved by (-14.34, 12.72) and turned -45 degrees
about the moved commit place (-14.34, 10.72), with its anchor at (-8.34, 35.72), as Mermaid's `translate` and `rotate`
of the label's group place it; that an empty branch name gives a label rectangle of 18 by 4 pixels at (-53, 86) and no
text, and an empty tag a tag shape without text, neither text counted in the view box; that the branch label
`a <BR/> b\nc`, a line feed, `<br><br>d` splits into the rows `a`, `b`, `c`, two empty rows, and `d` (split at line
feeds, written `\n`, and `<br>` tags, trimmed, the empty rows kept); and that Mermaid's placeholder of the entity
`#35;` in `Fish ... Chips` is shown as `#`.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Title: A Title Over The Drawing

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_FrontmatterTitle_IsCenteredOverTheDrawing`

Asserts that under the frontmatter title `Front` a bare `title` statement keeps that title, which is anchored in the
middle at the x of -18.890625, the center of the bounding box of the drawing, with its baseline at the y of -25 (the
default `titleTopMargin` above the origin of the drawing's coordinates), and that the view box (-95.78125, -49,
153.78125, 85.044) holds it, as Mermaid draws it.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-ViewBox: The Browser's Bounding Box

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_EmptyGraph_HasMermaidsViewBox`,
`GitGraphRendererTests.GitGraphRenderer_Draw_EmptyNameAndTag_HaveNoTextBox`

Asserts that the keyword alone has the view box (-95.78125, -20.5, 103.78125, 37): the main branch's label and its
line of no length, grown by the padding of 8; and that a graph with an empty branch name and an empty tag has the view
box (-95.78125, -20.5, 203.78125, 145.851), as Chrome's `getBBox` skips the texts that show nothing.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Paint: The Paint Of The Git Graph Style Sheet

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_Paint_FollowsTheStyleSheet`

Asserts that the cherry-pick of `B` onto `main` draws five shapes, its cherries filled white and their stems stroked
white, and its cherries filled black in the dark theme; that a commit whose id is `commit-reverse`, a class of the
sheet, is styled by that rule (a stroke width of 3); and that the `neo` look gives the branch label a drop shadow. The
paint of every theme, of the theme variables, and of `themeCSS` is verified with the style sheet (see the ThemeResolver
unit verification design).

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-RenderConfiguration: The Render's Main Branch

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_SiteMainBranchName_RunsTheStatementsAgain`

Asserts that a commit, the branch `dev`, and a commit, drawn under a site configuration that names the main branch
`trunk`, are drawn from the statements run again with that name: the branch labels are `trunk` and `dev`.

##### CanvasNetMermaid-GitGraph-GitGraphRenderer-Cost: Bounded And Cancelable

**Tests**: `GitGraphRendererTests.GitGraphRenderer_Draw_WorkLimitAndCancellation_StopTheRender`

Asserts that a `MaxLayoutWork` of 20, too small for the drawing of two branches and two commits, fails the render with
`mermaid-limit-exceeded`, and that a canceled token stops the render with an `OperationCanceledException`.
