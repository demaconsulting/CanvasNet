## GitGraph Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `GitGraph` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.GitGraph`), containing two units: `GitGraphParser` (the lexer and the grammar of
Mermaid's gitGraph language, the value converters, the git database with its operations, errors, and generated ids,
the diagram metadata, the located errors, the node and label limits, and the rerun of the statements for a render's
main branch) and `GitGraphRenderer` (the drawing of Mermaid's git graph renderer: the branch lines and labels, the
commit positions, the arrows with their rerouting lanes, the bullets, the commit labels and tags, the title, the paint,
and the view box).

### Verification Approach

The `GitGraph` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`git-graph/`) verify each unit, including the 297 upstream vectors recorded from the pinned Mermaid release. The
subsystem-level scenarios in `GitGraphTests` verify the two units working together through the document: a whole text
is opened as a `MermaidDocument` (preprocessed, detected, parsed, and run against the git database), and then asked
for its metadata, its located errors, or its laid-out scene under a render configuration. The original fixtures in
`Fixtures/gitgraph/` (18 diagrams written for this project, one for each area of the language and the settings:
generated ids, a branch and a merge, the commit types, cherry-picks, rerouted arrows, top to bottom, bottom to top,
parallel commits in each direction, labels that are not rotated, hidden branches and commit labels, many branches
with orders and a renamed main branch, a title with the accessibility texts, labels with markup and entities, the
dark, forest, and neutral themes with the `neo` look, and theme variables with `themeCSS`) are all opened and laid
out. The reference layout of the fixtures against the goldens drawn by Mermaid and the located limits of whole
documents under the default limits are verified at the system level by the reference layout and hardening tests.

The expectations are stated in the tests as values worked out from Mermaid's documented semantics and confirmed
against the pinned Mermaid release: the commits and arrows of every fixture, the metadata of a document, the branch
labels, commit labels, tags, and arrows of a known text, the generated ids of Mermaid's fixed pseudo-random sequence,
the paint of each theme, and the location and code of an invalid text. The scenarios use no mocking or stubbing: the
preprocessor, the detector, the configuration resolver, the git graph style sheet, the text metrics, and the bundled
fonts are real. The subsystem is internal, so its tests reach the diagram and the scene through the test assembly's
`InternalsVisibleTo` access (the helper `SceneQuery`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: The original diagrams in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/gitgraph/*.mmd`
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test opens its own document; the fixtures are only read

### Acceptance Criteria

The `GitGraph` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-GitGraph-Language: Git Graph Texts Open As Mermaid Reads Them

**Tests**: `GitGraphTests.CanvasNetMermaid_GitGraph_Fixture_ParsesAndLaysOutConsistently`,
`GitGraphTests.CanvasNetMermaid_GitGraph_Text_GivesTheDiagramAndTheScene`

Every original fixture (a theory with one case for each of the 18 files) opens as a document whose diagram is a git
graph, and the commits of its database are exactly the commits the scene draws. The text with the title `Release`, the
commit `A` tagged `v1`, the branch `dev` of order 1 with the commit `B`, a checkout of `main`, a merge of `dev`, and
the commit `C` opens as a document of the type `gitGraph` with four commits, the merge among them with the generated
id `2-eb28011`. The exact commits, branches, drawing order, and generated ids of every kind of statement are verified
at the unit level against Mermaid's git database (see the GitGraphParser unit verification design and the upstream
vectors).

#### CanvasNetMermaid-GitGraph-Metadata: Metadata Without Rendering

**Tests**: `GitGraphTests.CanvasNetMermaid_GitGraph_Text_GivesTheDiagramAndTheScene`

The text above gives, without being rendered, the diagram type `gitGraph`, a node count of 4 (its commits, the merge
included), an edge count of 4 (the links to their parents: `A` to `B`, `A` and `B` to the merge, and the merge to
`C`), the direction `LR`, and the title `Release` of its `title` statement. That there are no subgraphs, the accessible
title and description, and the rule that only a `title` statement with a value replaces the frontmatter title are
verified at the unit level (see the GitGraphParser unit verification design).

#### CanvasNetMermaid-GitGraph-FailClosed: Invalid Text Is Refused At Its Place

**Tests**: `GitGraphTests.CanvasNetMermaid_GitGraph_InvalidText_FailsClosedWithLocation`

A commit whose id is not quoted (`commit id: a`) after a valid commit and a frontmatter block is refused when the
document is opened with a `MermaidSyntaxException` of code `mermaid-syntax-error` in the named file `g.mmd`, located
at line 6 of the original text, frontmatter lines included, with the expected token `STRING` and a hint; a checkout of
a branch that does not exist (`checkout dev`) is refused with the same code at line 3, its statement, with Mermaid's
message `Trying to checkout branch which is not yet created. (Help try using "branch dev")` and a hint; and a third
commit over a `MaxNodes` of 3 (the main branch counted) is refused with `mermaid-limit-exceeded` at line 4, its
statement. No document is returned. The other grammar errors, the lexical errors, the other operations Mermaid's
database rejects, and the label limits are verified at the unit level (see the GitGraphParser unit verification
design) and at the system level by the hardening tests.

#### CanvasNetMermaid-GitGraph-Configuration: The Settings Change The Drawing

**Tests**: `GitGraphTests.CanvasNetMermaid_GitGraph_Configuration_ChangesTheDrawing`

The commits `A` and `B` draw one branch line, and their commit labels are rotated, under the default settings; with a
site configuration that sets `gitGraph.showBranches` and `gitGraph.showCommitLabel` to false no branch line and no
commit label is drawn; and with `gitGraph.rotateCommitLabel` set to false the commit labels carry no rotation. The
positions that `parallelCommits`, `titleTopMargin`, and `diagramPadding` give, and the rerun of the statements when the
render's configuration names another main branch or main-branch order, with its located `mermaid-render-failed`, are
verified at the unit level (see the GitGraphParser and GitGraphRenderer unit verification designs) and at the system
level by the integration and reference layout tests.

#### CanvasNetMermaid-GitGraph-Drawing: Git Graphs Are Drawn As Mermaid Draws Them

**Tests**: `GitGraphTests.CanvasNetMermaid_GitGraph_Fixture_ParsesAndLaysOutConsistently`,
`GitGraphTests.CanvasNetMermaid_GitGraph_Text_GivesTheDiagramAndTheScene`,
`GitGraphTests.CanvasNetMermaid_GitGraph_Themes_PaintTheCommitsAndBranches`

Every fixture lays out into a scene with a bullet for every commit and an arrow for every link to a parent, its items
lying within the view box, and lays out a second time into the same view box and the same number of items. The text
of the first scenario draws the branch labels `main` and `dev` in their order, the commit labels `A`, `B`, and `C`
(the merge, whose id was generated, has none), the tag `v1`, the title `Release`, and the arrows `A->B`,
`A->2-eb28011`, `B->2-eb28011`, and `2-eb28011->C` in that order. A commit of the main branch is filled `#0000EC`
(`git0`) in the default theme and another color in the dark theme, and the branch line is dashed 2 on and 2 off. The
branch and commit positions, the rerouting lanes, the rotated labels, the title, the view box, the paint of the style
sheet, and the work charged are verified at the unit level (see the GitGraphRenderer unit verification design), the
git graph style sheet by the Configuration subsystem, and the positions, sizes, and paint against Mermaid's goldens by
the system-level reference layout tests.
