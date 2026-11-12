### GitGraphRenderer

![MermaidGitGraph Structure](MermaidGitGraphView.svg)

The `GitGraphRenderer` unit is the second software unit in the `GitGraph` subsystem. It comprises three source files in
the `DemaConsulting.CanvasNet.Mermaid.GitGraph` namespace: `GitGraphRenderer.cs` (the drawing order, the branch and
commit positions, the branches, the title, the view box, and the text helpers), `GitGraphRenderer.Arrows.cs` (the
arrows with their rerouting test and lane search), and `GitGraphRenderer.Labels.cs` (the branch labels, the commit
bullets, the commit labels, the tags, and the measuring and making of texts). `GitGraphRenderer` is an
`internal sealed partial` class, so the three files are one type. Each file carries the provenance header comment for
the parts of Mermaid it ports.

#### Purpose

`GitGraphRenderer` draws one render of a `GitGraphDiagram` into a scene as Mermaid's `gitGraphRenderer.ts` draws the
diagram (`draw`, `drawCommits`, `drawBranches`, `drawArrows`, and their helpers), with `insertTitle` and Mermaid's
shared view box setup, in the paint of the git graph style sheet of the render's theme. It exists so that the geometry,
the texts, the colors, and the size of a git graph are Mermaid's for the same text, configuration, and theme.

The unit's behavior is specified by the `CanvasNetMermaid-GitGraph-GitGraphRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/git-graph/git-graph-renderer.yaml`.

#### Data Model

**The constants of Mermaid's renderer**: the offset of a commit from its place (`LAYOUT_OFFSET`, 10), the step from a
parent with `parallelCommits` (`COMMIT_STEP`, 40), the paddings of labels and tags (`PX` 4 and `PY` 2), the number of
branch colors (`THEME_COLOR_LIMIT` of the renderer, 8), the first place top to bottom and bottom to top (`defaultPos`,
30), and, for the lane search, the spacing of 10 pixels between lanes and its cut after six tries (depths 0 to 5).

**A renderer instance** draws one render on one thread and holds the diagram, the `DiagramRenderContext` of the render,
the `GitGraphConfig` section of its configuration, the `GitGraphStyles` built from its configuration (reporting the
problems of `themeCSS` to its sink), a `LayoutWorkMeter` of `MaxLayoutWork` located at the diagram keyword and bound to
the cancellation token of the render, a `LabelBuilder` over the fonts of the render that charges that meter, and the
state that Mermaid keeps in its module and shares between the passes: the database of the render, the place and color
index of each branch (`branchPos`), the place of each commit (`commitPos`), the end of the commits (`maxPos`), the
lanes taken (`lanes`), whether the direction is vertical or bottom to top, and the bounding box of what is drawn so far.
**`Placement`** is the transform of an SVG element, `translate(tx, ty) rotate(angle, cx, cy)`, which maps a point or a
box (the box of its mapped corners, as the browser maps a child's box through its transform).

**The scene** has one group with the role `git-graph`, located at the diagram keyword, holding, in Mermaid's order: a
`git-branches` group (when `showBranches` is set) with, for each branch, its dashed line (`git-branch`), the rectangle
of its label (`git-branch-label`), and a `git-branch-label` group of the texts of its rows; a `git-arrows` group with
one `git-arrow` path per parent link (its id `{parent}->{child}`); a `git-commits` group with the shapes of each bullet
(`git-commit`, the commit id as the id); a `git-labels` group with a `git-commit-label` group per drawn commit label
(the rectangle and the text) and a `git-tag` group per tag (the polygon, the hole, and the text); and the title text
(`title`). Branch items are located at the statement that made the branch (the diagram keyword for the main branch),
and commit items at the statement that made the commit.

#### Key Methods

- **`Draw()`** follows Mermaid's `draw`:
  - It takes the database of the render from `GitGraphDiagram.Database` and the branches in Mermaid's drawing order.
  - It places the branches: each branch (charging the meter 1) at the position after the one before, which grows by 50,
    by 40 more when `rotateCommitLabel` is set, and by half the width of the branch's label top to bottom and bottom to
    top.
  - It places the commits in a first pass (`DrawCommits` without drawing), then draws the branches (`DrawBranches`,
    when `showBranches` is set), the arrows (`DrawArrows`, from the places of the first pass), the bullets, labels, and
    tags in a second pass (`DrawCommits` again, which places the commits again from the state the first pass left), and
    the title (`DrawTitle`).
  - It gives the scene the view box that Mermaid's shared view box setup gives: the bounding box of everything drawn,
    in single precision (`Box32`), grown by `diagramPadding` on every side.
- **`DrawCommits`** follows Mermaid's `drawCommits`: the commits in the order of their sequence numbers, reversed bottom
  to top (after `SetParallelBottomToTop` with `parallelCommits`); each commit (charging the meter 1) at its place,
  `pos + 10` from the start (`pos` is 0 left to right and 30 top to bottom and bottom to top), or with `parallelCommits`
  at the place `CalculatePosition` gives; left to right at that x and two pixels above its branch, else on its branch
  at that y. The place moves on by 50 (by 40 bottom to top with `parallelCommits`), and `maxPos` keeps the furthest.
  The second pass draws each commit's bullet, label, and tags at its place.
- **`CalculatePosition`**, **`ClosestParent`**, and **`SetParallelBottomToTop`** are Mermaid's `calculatePosition`,
  `findClosestParent`, and `setParallelBTPos`: with `parallelCommits`, a commit is 40 after its closest placed parent
  (the largest x left to right, the largest y top to bottom, the smallest y bottom to top, a later parent winning a
  tie, each parent looked at charging the meter 1), a root at the start, and a commit without a placed parent at 0;
  bottom to top, a root or a commit with a placed parent is 40 before the place it already has, and three loops
  (`SetParallelBottomToTop`, before the pass) place each commit below its closest parent, the roots below the lowest
  commit, and then each commit above its closest parent, and a commit whose parents have no place fails as Mermaid
  fails.
- **`DrawBranches`** follows Mermaid's `drawBranches`: for each branch, its dashed line with the classes `branch` and
  `branch{i % 8}` (left to right from 0 to `maxPos` two pixels above the branch; top to bottom from 30 to `maxPos`;
  bottom to top from `maxPos` to 30), the line's position taken as a lane; the rectangle of its label with rounded
  corners of 4 (the classes `branchLabelBkg` and `label{i % 8}`, and in the `neo` look the inline `filter` of the
  theme's `dropShadow`, drawn as a `SceneShadow`), 18 wider and 4 taller than the label (left to right moved left by 30
  more when commit labels are rotated; top to bottom at the top of the drawing and bottom to top at `maxPos`); and the
  label's rows.
- **`BranchLabel`** lays out a branch label as Mermaid's `drawText`: the name split into rows (**`Rows`**: at line
  feeds, written `\n`, and `<br>` tags, each row trimmed), tabs and line breaks as spaces and runs of spaces kept
  (`xml:space="preserve"`), an empty row left out (its `dy` does not apply, as in Chrome), and the k-th row on the
  baseline k em below the top, each measured with `LabelBuilder.SvgLineExtent` and `LabelBuilder.VerticalMetrics`.
- **`DrawArrows`** and **`DrawArrow`** follow Mermaid's `drawArrows` and `drawArrow`: for each commit in the order of
  the commit map, an arrow from each parent between the places of the first pass, charging the meter 1 and the number
  of commits. **`ShouldReroute`** is Mermaid's `shouldRerouteArrow`: a commit made between the parent and the commit on
  the branch the arrow bends to. A rerouted arrow runs on a lane with bends of radius 10; **`FindLane`** is Mermaid's
  `findLane`, which takes the middle between two coordinates when it is at least 10 pixels from every lane taken (and
  then takes it), else moves the far end a fifth closer and tries again, and after six tries returns the next candidate
  without taking it, each try charging the meter 1 and the number of lanes; as in Mermaid, both the horizontal and the
  vertical lane are searched for each rerouted arrow. Any other arrow bends once with radius 20 (at the commit's end,
  or at the parent's end for the second parent of a merge) or is straight. The arrow is painted with the classes
  `arrow` and `arrow{n % 8}` of the commit's branch, or of the parent's branch for the second parent of a merge and for
  a rerouted arrow whose commit is not further out than its parent (below it left to right, right of it top to bottom
  and bottom to top).
- **`DrawBullet`** is Mermaid's `drawCommitBullet`: the commit's symbol (the `type:` of a merge, else its type) draws
  two squares for a highlighted commit, a circle with two cherries and stems for a cherry-pick (white marks, or black
  in the dark theme, as presentation attributes that the sheet can override), or a circle with the inner circle of a
  merge or the cross of a reverse commit. The class attribute of every shape holds the commit id, as in Mermaid, so the
  style sheet sees the id's words as classes.
- **`DrawCommitLabel`** is Mermaid's `drawCommitLabel`, for a commit that is not a cherry-pick and not a merge without
  an id, when `showCommitLabel` is set: the id on a half-transparent rectangle 4 pixels wider and taller than the text,
  below the commit left to right and left of it top to bottom and bottom to top. With `rotateCommitLabel`, top to
  bottom and bottom to top the text and the rectangle each turn by -45 degrees about the commit, and left to right their
  group is moved by `(-7.5 - (w + 10) / 25 * 9.5, 10 + w / 25 * 8.5)` and turned by -45 degrees about the commit's place
  without its offset, as Mermaid does.
- **`DrawTags`** is Mermaid's `drawCommitTags`: the tags in reverse order (the last nearest the commit), each 20 pixels
  further from the commit than the one before, each on a polygon as wide as the widest tag with a hole of radius 1.5;
  top to bottom and bottom to top the polygons, holes, and texts are moved and turned by 45 degrees beside the commit.
- **`DrawTitle`** is Mermaid's `insertTitle`: the drawn title (`DrawnTitle`) centered over the bounding box of what is
  drawn before it, its baseline `titleTopMargin` above the top of the drawing's coordinates, in the font and color of
  `.gitTitleText`; an empty title draws nothing.
- **`Measure`** and **`Text`** make a text as the browser measures and shows it: the measured box is that of the text
  with Mermaid's entity placeholders, white space collapsed (the ink-extended advance of `LabelBuilder.SvgLineExtent`
  between the rounded ascent and descent of `LabelBuilder.VerticalMetrics`), which places the shapes; the drawn text is
  the text the browser shows (**`Decode`**: each placeholder decoded into its character, everything else as written),
  charging the meter 1 and its length each time. A turned text is a `SceneText` whose position and `SceneRotation`
  pivot include the translation of its transform (`translate(t) rotate(a, c)` is `rotate(a, c + t)` after the
  translation), and whose box is the box of its turned box; a turned rectangle, polygon, or hole is an outline whose
  points are already mapped through the transform. An empty text, or one at a position that is not a number, is not
  drawn.
- **`Include`** and **`Box32`** keep the bounding box as Chrome's `getBBox` gives it: the union of each element's box (a
  path's geometry, a line without area included, a text's `getBBox`, no stroke), mapped through its transform (the box
  of a turned group of the union of its children, turned), with empty texts and groups left out; `Box32` rounds each
  side to single precision and subtracts in single precision, as the browser keeps the box.
- **`Shape`** makes a scene path from a `GitGraphComputedStyle` of the style sheet's cascade (`GitGraphStyles.Compute`
  over the svg element, the groups, and the element): its fill and stroke with the folded opacity, its stroke width,
  dash, and line cap, and its shadow.

#### Error Handling

- `GitGraphDiagram.Database` throws a `MermaidRenderException`, located at a statement, with code
  `mermaid-render-failed` and Mermaid's message when the database rejects an operation under the render's main branch,
  and with code `mermaid-limit-exceeded` when a limit is exceeded in that run; both propagate.
- `SetParallelBottomToTop` throws a `MermaidRenderException` located at the commit's statement, with code
  `mermaid-render-failed` and Mermaid's message `Closest parent not found for commit {id}`, when a commit has no placed
  parent (bottom to top with `parallelCommits`, after a commit id is used again), as Mermaid's renderer throws.
- The layout work meter of the render throws a `MermaidRenderException` with code `mermaid-limit-exceeded`, located at
  the diagram keyword, when the work exceeds `MaxLayoutWork`, and an `OperationCanceledException` when the render is
  canceled; both propagate.
- `GitGraphStyles.Create` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported
  `themeCSS` rule in strict mode; it propagates.
- No other error can occur: a text at a position that is not a number and a box that is not finite are left out, as
  the browser draws and measures nothing for them, and a configuration value of the wrong type was already left out by
  the configuration with a warning.

#### Dependencies

- **GitGraphParser** - `GitGraphDiagram` (`Database`, `Direction`, `DrawnTitle`, and `KeywordLocation`),
  `GitGraphDatabase` (the commits in the order of the commit map, `Find`, and `BranchOrder`), `GitCommit`, and
  `GitBranch`.
- **Configuration subsystem** - `MermaidConfig` (the `GitGraph` section `GitGraphConfig` and the theme) and
  `GitGraphStyles` with `GitGraphElement` and `GitGraphComputedStyle` (the git graph style sheet and its cascade).
- **Rendering subsystem** - `LabelBuilder` (the browser's text extents `SvgLineExtent`, the vertical metrics
  `VerticalMetrics`, `CollapseSvgWhiteSpace`, `DecodeEntities`, and `MatchBreakTag`).
- **Syntax subsystem** - `Preprocessor.TrimJs` (JavaScript's `trim`) and `HtmlSanitizer.DecodeReferences` (the
  character references of a placeholder).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, `SceneItem`, `SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText`,
  `SceneRotation`, `SceneTextAnchor`, `SceneTextRun`, `SceneFont`, `SceneRect`, `SceneStroke`, and `SceneShadow`, the
  drawing.
- **System** - `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, and `MermaidRenderException`.

#### Callers

- **GitGraphParser** - `GitGraphDiagram.Layout` creates a renderer for each render and returns the scene of `Draw`.
- The unit tests call `Decode`, `Rows`, and `Box32` directly, and the unit and subsystem tests lay out whole texts.

#### Known Differences From Mermaid

- **No missing-glyph reports.** The texts of a git graph are drawn with the fonts of the render but give no
  `mermaid-text-missing-glyph` report, as the texts of a pie chart and a packet diagram give none.
- **Text metrics.** The widths and boxes of the texts are those of the `LabelBuilder`, which emulates the browser's SVG
  text measurement with the fonts of the render; the reference layout tests hold them to Mermaid's goldens. Its shared
  glyph-box rule misses Chrome's box by up to a pixel for a few glyphs at a few sizes, so a commit label or tag that
  ends in such a glyph is a little narrower or wider than Mermaid's (a 10 pixel label or tag that ends in `D` is up to
  0.8 pixel narrower, for example), and its rectangle or polygon with it.
- **Branch label rows.** A branch label's rows keep their spaces, and an empty row adds no line, as in Chrome.
- **Accessibility.** The picture carries no title or description element; the accessibility texts are metadata of the
  diagram.
