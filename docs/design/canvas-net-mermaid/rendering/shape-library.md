### ShapeLibrary

![Rendering Structure](MermaidRenderingView.svg)

The `ShapeLibrary` unit is the second software unit in the `Rendering` subsystem. It is the internal static partial
class `ShapeLibrary` in ten source files - `ShapeLibrary.cs` (the entry point, the style split, and the value
types), `ShapeLibrary.Basic.cs` (the classic and bracket shapes and the small undocumented shapes),
`ShapeLibrary.Extended.cs` (the extended shape set of Mermaid), `ShapeLibrary.Images.cs` (the image and icon
shapes), `ShapeLibrary.Intersect.cs` (the intersect functions), `ShapeLibrary.Parts.cs` (path data, part paint, and the
draft that collects a node), `ShapeLibrary.Clusters.cs` (the subgraph rectangle and the hidden `labelRect` node),
`ShapeLibrary.ClassBox.cs` (the class box of a class diagram), `ShapeLibrary.States.cs` (the state with descriptions,
the clusters of a state diagram, and the paint of the parts that rough.js draws), and `ShapeLibrary.ErBox.cs` (the
entity box of an entity relationship diagram) - with the value types `ShapePoint`, `NodeBox`,
`ShapeIntersect`, `NodePaint`, `ShapeTheme`, `ShapeContext`, `ShapeResult`, and `ClusterShape`. The unit also groups
the shape registry, the internal static class `ShapeNames` in `ShapeNames.cs`. All types are in the
`DemaConsulting.CanvasNet.Mermaid.Rendering` namespace.

#### Purpose

`ShapeLibrary` is Mermaid's node shapes (`rendering-util/rendering-elements/shapes/*.ts`) in the classic look: the 53
documented shapes with their aliases, the bracket shapes of the flowchart syntax, and the undocumented shapes a
flowchart accepts, each sized from its label and padding with Mermaid's formulas, drawn as scene paths, and given the
intersect function on which edges end. It also draws the class box of a class diagram (`shapes/classBox.ts` with the
text placement of the class diagram's `shapeUtil.ts`), the shapes of a state diagram (the state, the state with
descriptions of `shapes/rectWithTitle.ts`, the start and end states, the fork and join bars, the choice, and the note,
painted through the state style sheet), the entity box of an entity relationship diagram (`shapes/erBox.ts`), the
subgraph rectangle with its title (`clusters.js`'s `rect`), and the clusters of a state diagram (`clusters.js`'s
`roundedWithTitle`, `divider`, and `noteGroup`). `ShapeNames` is Mermaid's shape registry
(`rendering-elements/shapes.ts`): which names exist and which drawing
function each stands for. The unit's behavior is specified by the `CanvasNetMermaid-Rendering-ShapeLibrary-*`
requirements in `docs/reqstream/canvas-net-mermaid/rendering/shape-library.yaml`.

#### Data Model

- `ShapePoint(X, Y)` and `NodeBox(X, Y, Width, Height)` - a point and a placed node's box (center and size) as the
  intersect functions read it.
- `ShapeIntersect` - the delegate `(NodeBox node, ShapePoint point) => ShapePoint`: where the line from the node's
  center to a point leaves the shape (Mermaid's `node.intersect`).
- `ShapeContext` - the settings of one render that every shape reads: the effective `MermaidConfig`, the style sheet
  `Styles` (a `UnifiedStyles`: the flowchart's `FlowchartStyles`, the class diagram's `ClassStyles`, the state
  diagram's `StateStyles`, or the entity relationship diagram's `ErStyles`, read through the selectors the shared units
  name, such as `NodeLabel` and `NoteLabel`, through the sheet fact `CentersNodeSvgText`, and, for the parts a state
  diagram paints by selectors of its own, through `Resolve` of those selectors), the `MeasureLabel` delegate
  (`LabelBuilder.Build` in a render, a fixed box in the geometry tests), the `TextWidth` delegate (the width that
  Mermaid's `calculateTextWidth` gives a text in the configured font, which a class box wraps its texts at and which an
  entity box wraps its texts at and tests its minimum width with; set by the class and entity relationship diagrams to
  `LabelBuilder.CalculateTextWidth`, which charges the layout work meter, and null otherwise, when the class box texts
  never wrap and an entity without attributes is never widened to its minimum width), the `NodeImages` of the render
  (or null), the report sink, and the theme colors
  (`ShapeTheme`: the `mainBkg`, `nodeBorder`, `lineColor`, `clusterBorder`, and `edgeLabelBackground` variables, read
  once).
- `NodePaint(Css, User, Theme)` - the paint cascade of a node: the style sheet's node rule (`.node rect` and its
  siblings), the node's own non-label styles (applied as Mermaid's inline `!important` styles), and the theme colors
  that some parts use directly.
- The inputs read from `LayoutNode` beyond its shape and label: `TrustedStyles` (the node's styles are the diagram's
  own, such as a class diagram's note colors and the `opacity: 0;` of a hidden lollipop interface, and are applied
  without restriction and without reports) and `ClassBox` (a `LayoutClassBox` of the drawn name, the annotations, the
  attributes, the methods, and the class's own styles, each member a `LayoutClassMember` of its text and the style of
  its classifier: `font-style:italic;` for an abstract member, `text-decoration:underline;` for a static one).
- The inputs a state diagram sets on `LayoutNode`: `Rx` and `Ry` (Mermaid's radii of 10 on a state, with which a
  `rect` is drawn as a `roundedRect`), `CenterLabel` (`node.centerLabel`: the label is centered on the node whatever
  its kind), `Descriptions` (the descriptions a `rectWithTitle` state draws under its title, the first description
  being its `Label`), and `CssClasses` (whose `statediagram-state`, `statediagram-cluster`, and
  `statediagram-cluster-alt` classes decide which selectors of the state sheet apply). The input an entity
  relationship diagram sets: `ErBox` (a `LayoutErBox` of the entity's alias, drawn in place of its name when set, and
  its attributes, each a `LayoutErAttribute` of its type as written, its name, its keys, and its comment).
- `ShapeResult(Handler, Bounds, Items, Label, LabelOffset, Intersect)` - a drawn node: the drawing function that drew
  it, the box of everything drawn around the node's center (whose size is the node's `Width` and `Height` for the
  layout, as `insertMeasuredNode` takes it from `getBBox`), the items, the measured label and its translation
  (`LabelBox`), and the intersect function.
- `ClusterShape(Shape, Width, Height, Diff)` - a drawn subgraph: its rectangle and title, the widened width, the
  height, and the horizontal correction `diff` for a title wider than the layout's box.
- `ShapeNames` - a frozen table of every name (short names, aliases, internal aliases, and the undocumented shapes) to
  its drawing function, and the documented shapes in Mermaid's order.
- Private helpers: `ShapeInput` (a node being drawn with its split styles and paint, and its `MeasureLabel(width,
  assetShape, label, css)`, whose `css` is the text rule of the label - the style sheet's `NodeLabel` unless a shape
  names another, as the note names `NoteLabel`), `PathData` (SVG path commands written as Mermaid writes them, with
  exact arc bounds, and `RoughEllipse`, the ellipse of rough.js without roughness), `PartPaint`, `Draft` (collects the
  parts, the label, and the box of everything drawn; an item may add nothing to the box, as an empty HTML cell of an
  entity box), and for the
  class box `ClassTexts` (measures one text of the box), `ClassRow` (a measured text and its translation in its group,
  and whether it is underlined), and `ClassGroup` (the annotation, name, attribute, or method group with its rows and
  its translation, whose box is the `getBBox` of its rows). The constant `ClassTextRoom` is the 50 pixels Mermaid adds
  to the measured width of a class box text before it wraps. For the entity box, `ErPaint` (the paint of its parts),
  the constants `ErTextRoom` (the 100 pixels Mermaid adds to the measured width of an entity box text before it wraps),
  `ErDividerThickness` (0.0001 pixel), and `ErRoughStrokeWidth` (1.3), and the cell roles `er-attribute-type`,
  `er-attribute-name`, `er-attribute-keys`, and `er-attribute-comment`. `RoughStrokeWidth` (1.3) is the stroke width
  that rough.js writes on the parts it draws.

#### Key Methods

- `Draw(LayoutNode node, ShapeContext context)` - resolves the drawing function (`ShapeNames.HandlerOf`, or the
  legacy `ellipse`, or `classBox`, which is not a flowchart shape name; a `rect` whose `Rx` and `Ry` are both truthy in
  JavaScript's sense, a state's, is drawn as a `roundedRect`, as Mermaid's `insertNode` draws it), splits and parses
  the node's styles (with
  reports through the sink, or, for `TrustedStyles`, unrestricted and without a sink), and calls the shape: each shape
  measures its label first (Mermaid's `labelHelper`, its SVG text centered on its anchor only when the sheet's
  `CentersNodeSvgText` says so, as a flowchart's `.node .label text { text-anchor: middle }` does and the state and
  entity relationship sheets do not), computes its geometry from the label box and the padding (and
  from the direction of its graph, which turns a fork or join upright in a left-to-right graph), adds its parts and
  label to a draft, and returns the `ShapeResult` with its intersect function. The `note` shape measures its label in
  the style sheet's `NoteLabel` rule (a class diagram's `.noteLabel .nodeLabel`; a flowchart's node label). The label
  of `drawRect` (and so of `roundedRect`) is centered on the node when the node sets `CenterLabel`, as a state does
  (`labelHelper` then translates an SVG label by half its width), and its corner radius is the CSS radius of the state
  sheet's `.statediagram-state rect.basic` rule (5) for a node of class `statediagram-state`, else the attribute
  radius: a state around a 40 by 24 label with the padding of 8 is a rectangle of 56 by 40 pixels with corners of 5.
- The state shapes (`ShapeLibrary.Extended.cs`, `ShapeLibrary.Basic.cs`, and `ShapeLibrary.States.cs`) - Mermaid's
  state shapes, painted as Mermaid's elements compute:
  - `stateStart` - a circle of 14 pixels, an SVG `circle` of class `state-start`, painted by the sheet's `.node circle`
    rule and then, in a state diagram, by `.node circle.state-start` (`specialStateColor` fill and stroke, 1 pixel
    wide); the edges end on the circle.
  - `stateEnd` - two rough.js ellipses drawn by `PathData.RoughEllipse`, the exact port of rough.js's `ellipse`
    without roughness (`_computeEllipsePoints` with quarter steps from angle 0, one point before and two after, joined
    by the Catmull-Rom `_curve`, the default `curveStepCount` of 9): the outer ring of 14 pixels in `mainBkg`, stroked 2
    pixels wide in `lineColor`, and the inner circle of 5 pixels filled and stroked in `stateBorder` (else
    `nodeBorder`), 2 pixels wide. The last curve doubles back from the end point to the first and bulges past x = 7, so
    the outline's box is 14.013 by 14 pixels from x = -7, as Chrome's `getBBox` of Mermaid's path gives it; the node is
    that box (`updateNodeBounds`) and the edges end on a circle of half its width.
  - `forkJoin` (the `fork` and `join` shapes) - a bar of 70 by 10 pixels (10 by 70 in a graph of direction `LR`), filled
    and stroked in `lineColor`, 1.3 pixels wide. Mermaid's `forkJoin` adds half of `state.padding` to the node's size
    after `updateNodeBounds`, but `insertMeasuredNode` then overwrites the size with the `getBBox` of the drawn group,
    so the port does not grow the node: a fork is 70 by 10 pixels whatever its padding.
  - `choice` - a diamond of 28 pixels without a label, filled `mainBkg` and stroked `nodeBorder`, 1.3 pixels wide.
  - `note` - a rectangle with twice the padding around its label, filled `noteBkgColor` and stroked `noteBorderColor`,
    1.3 pixels wide.

  These parts (all but the start circle) are drawn by rough.js as paths with paint attributes, so they are painted by
  `RoughPaint`; a flowchart's or a class diagram's sheet, whose node rule covers `.node path` and `.node circle`, still
  paints them in its node colors, and a node's own styles win over both.
- `RectWithTitle` (`rectWithTitle`) - a state with descriptions: the title (the node's `Label`) and the descriptions
  (`Descriptions` joined with `<br/>`) are measured by `MeasurePlainLabel`; the wider starts at 0 and the narrower is
  centered on it, the descriptions placed the title's height plus half the padding plus 5 pixels under the title. The
  label group is centered by its size, the rectangle drawn with half the padding around it (the corner radius of the
  state sheet's `.statediagram-state .title-state` rule, 5), and the divider (role `state-divider`, stroked by
  `.divider` and `.statediagram-state .divider`, not filled) drawn across it under the title. A title of 61.094 pixels
  over a description of 80.938 pixels, both 24 high, gives a rectangle of 88.938 by 65 pixels from (-44.469, -32.5)
  with the divider at y = -4.5.
- `DrawStateCluster` - dispatched first by `DrawCluster` for the shapes `roundedWithTitle`, `divider`, and `noteGroup`
  (Mermaid's `insertCluster`), each drawn without the node's own styles:
  - `RoundedWithTitle` - a composite state: the title measured by `MeasurePlainLabel` in the sheet's
    `ClusterTitle(html)` rule; the outer rectangle (role `cluster`, painted by `.statediagram-cluster rect` and
    `.statediagram-cluster rect.outer`, whose radius is 5) of the layout's size, widened to the title plus the padding
    when the title is wider (with Mermaid's `diff` of half the growth less the padding, else minus the padding); the
    title a pixel under its top (3 pixels higher as SVG text); and the inner rectangle (role `cluster-inner`, radius 0)
    from 2 pixels under the title to 4 pixels above the bottom, painted by `.statediagram-cluster .inner` and
    `.statediagram-cluster.statediagram-cluster .inner` (`compositeBackground`, else `background`) or, for a cluster of
    class `statediagram-cluster-alt`, by `.statediagram-cluster.statediagram-cluster-alt .inner` (`altBackground`).
  - `DividerCluster` - a concurrent region: a rectangle of the layout's size painted by `.statediagram-cluster rect`
    and then `.statediagram-state rect.divider` (dashed 10,10, filled `altBackground`), without a title, its `diff`
    minus the padding.
  - `NoteGroup` - the group of a note and its state: an unpainted rectangle of the layout's size (a `ScenePath` with
    the role `cluster`, which the painter draws as nothing but which keeps the group's box in the scene); Mermaid
    leaves the group's `diff` unset, which reads as 0.
- `RoughPaint(input, fill, stroke, strokeWidth)` - the paint of a part that rough.js draws as a path with paint
  attributes: the sheet's `.node path` rule wins over each attribute it declares (a flowchart's and a class diagram's
  sheet have that rule; a state and an entity relationship diagram's do not), and the node's own styles win over both.
- `Rules(styles, selectors)`, `RulePaint`, and `CssRadius` - the cascade of the state sheet's selectors given in the
  order of their specificity, each only when its condition holds (a selector that names an ancestor class applies only
  when the node carries that class), the fill, stroke, width, and dashes of a cascaded rule, and the corner radius that
  the CSS `rx` and `ry` of a rule give a `rect` over its attribute (`rx`, an absent one taking the other; zero when
  either is zero; the attribute radius when the rule declares neither).
- `MeasurePlainLabel` - a label as Mermaid's `createLabel` creates it for a state's title and descriptions and a
  composite state's title: plain text (never markdown), never wrapped, not prepared again, not centered, as an HTML
  label or as SVG text by the configuration's `htmlLabels`.
- `ClassBox` (`ShapeLibrary.ClassBox.cs`) - Mermaid's `classBox` with the `textHelper` of the class diagram:
  - **Texts**: the first annotation as `«annotation»`, the name, the attributes, and the methods, each a markdown label
    wrapped at its `TextWidth` plus 50 pixels, in the style sheet's node label font with the class's own text styles
    over it; the name is bold and an abstract member italic unless the class's styles set the weight or the style,
    and the runs of a static member are marked `Underline`. A text's row is moved up by half the height of one of its
    lines; the rows of a group are stacked by their heights plus the text padding (0 for HTML labels, 3 for SVG
    labels).
  - **Labels**: HTML labels unless `ClassHtmlLabels` says otherwise; an SVG label drops a leading backslash, is not
    centered, and inherits the bold weight of the name (`LabelRequest.SvgInheritsWeight`).
  - **Placement**: the annotation and the name centered, the attributes two paddings below the name, and the methods two
    paddings below the attributes (half a padding stands in for an empty attribute group); the rectangle is the box of
    the groups grown by the padding (`class.padding`, 12 by default), one padding taller for an empty class and two
    for a class without methods. An empty class gets the extra empty compartment (two paddings taller, moved up one
    padding) unless `class.hideEmptyMembersBox` is set, which makes it one padding shorter and moves it down half a
    padding. The groups are then moved
    under the rectangle's top with the heights Mermaid reads back (less half a padding with the extra compartment),
    with Mermaid's quirks: the annotation group's translation reads as 0 (its one-argument `translate` does not match
    Mermaid's regular expression), and SVG labels are moved up 4 pixels.
  - **Drawing**: the rectangle twice (rough.js's fill path and stroke path, with the same paint), the rows as scene
    groups with the roles `class-annotation`, `class-label`, `class-member`, and `class-method`, and the two dividers
    (`class-divider`, stroked only) under the name and under the attributes, from `y` to `y + 0.001` across the
    rectangle, unless the class is empty without the extra compartment. The rectangle and the dividers take the
    style sheet's node paint with the class's own styles winning.
  - **Result**: the node's size, bounds, and intersect function (`IntersectRect`) are those of the rectangle alone, as
    Mermaid's `updateNodeBounds(node, rect)`; the label is the name's.
- `ClassHtmlLabels(MermaidConfig config)` - Mermaid's `evaluate(config.htmlLabels) ?? true`: only the top-level
  `htmlLabels` decides (the flowchart's does not); unset is true, and only `false`, `null`, and the texts `false`,
  `null`, and `0` turn HTML labels off.
- `ErBox` (`ShapeLibrary.ErBox.cs`) - Mermaid's `erBox`, which fails with a located `mermaid-render-failed` for a node
  without `ErBox` data (a flowchart node cannot name the shape, whose name is not lower case):
  - **Without attributes**: the name (or the alias) measured as `labelHelper` measures it (HTML unless `ErHtmlLabels`
    says otherwise, wrapped at `er.minEntityWidth` when the name is narrow, else at the flowchart's wrapping width, not
    centered) inside a `drawRect` rectangle with the diagram padding (`er.diagramPadding`, 20 by default) left and
    right and one and a half times it above and below, as wide as `er.minEntityWidth` (100 by default) when the
    `TextWidth` of the name and twice the padding are narrower, the name centered; the paddings never grow. A 40 by 24
    name whose text width is 10 gives a box of 100 by 84 pixels, one whose text width is 70 a box of 80 by 84.
  - **Texts**: the name and each attribute's type, name, keys (joined with commas), and comment are markdown labels in
    the sheet's node label font with the entity's text styles, wrapped at their `TextWidth` plus 100 pixels (so never in
    practice); a type with generics is written as the class diagram writes it (`ClassModel.ParseGenericTypes`: `~T~`
    becomes `<T>`) with its angle brackets kept as text (`&lt;` and `&gt;`), and the lines of a multi-line HTML cell
    start at its left, as Mermaid aligns them once the label is measured.
  - **Paddings**: `ErHtmlLabels` decides the labels and the paddings: unless the top-level `htmlLabels` is truthy (also
    when it is unset), the diagram padding and the entity padding (`er.entityPadding`, 15 by default) grow by a
    quarter, to 25 and 18.75.
  - **Table**: each column is as wide as its widest text plus the padding; a keys or comment column that no attribute
    uses is dropped; the width the name and twice the padding need beyond the columns is shared among the live
    columns; the rows are as high as their highest text plus the entity padding, under the name row of the name's
    height plus the entity padding. The table is as wide as the columns or as the box of the texts plus twice the
    padding. With SVG text an empty cell counts in the box as a point where it is placed (Chrome's `getBBox` includes
    an empty `text` element) and is kept in the scene as an unpainted point of the role `er-empty-text`, so an empty
    comment cell after the keys column can make the node wider than its table; an empty HTML cell (a `foreignObject` of
    no size) does not count.
  - **Drawing**: the outline twice (rough.js's fill path and stroke path), the rows (each a fill and a stroke path,
    `rowOdd` and `rowEven` in turn), the texts (the name centered in the top row as the group `er-name`, the cells in
    their columns as the groups `er-attribute-type`, `er-attribute-name`, `er-attribute-keys`, and
    `er-attribute-comment`), and the dividers (role `er-divider`) under the name, between the type and name columns,
    before the keys and comment columns that are drawn, and under the name again, as Mermaid draws its first row line:
    each a polygon 0.0001 pixel thick around the line, filled and then stroked.
  - **Paint** (`ErPaint`, Mermaid's `userNodeOverrides` over rough.js's attributes): the outline and the dividers take
    the entity's `fill` (else `mainBkg`), the odd rows `rowOdd`, the even rows `rowEven`; every stroke path the entity's
    `stroke` (else `nodeBorder`) at its `stroke-width` (else 1.3) with its dashes; a fill path is stroked only when the
    entity's styles set both a stroke and a stroke width (rough.js writes a stroke width of 0 on it); and an even row
    takes every style of the entity's own, its fill and opacity included, as Mermaid writes them on the even rows. In
    the default theme the name row is `#ECECFF`, the odd rows `#FFFFFF`, the even rows `#F1F1FF`, and the lines
    `#9370DB`.
  - **Result**: a table with an attribute `int x PK` and an attribute `int y`, every text 40 by 24, is 195 by 128.25
    pixels (three columns of 65, a name row and two rows of 42.75) with eight divider paths; the node's intersect
    function is the rectangle's, and its label is the name's.
- `ErHtmlLabels(MermaidConfig config)` - how an entity box reads the top-level `htmlLabels` as it is (not through
  `getEffectiveHtmlLabels`): whether the table's texts are HTML (unless the value is falsy; unset counts as HTML,
  `createText`'s default), whether the paddings grow (unless the value is truthy; unset grows them), and whether the
  name of an entity without attributes is HTML (when the value is truthy or `ClassHtmlLabels` keeps it). Unset gives
  (true, true, true), `true` gives (true, false, true), and `false` gives (false, true, false).
- `DrawCluster(LayoutNode node, double x, double y, double width, double height, ShapeContext context)` - a state
  diagram's cluster through `DrawStateCluster` when the node's shape is one of its three; otherwise the subgraph
  rectangle (`rect`, which also draws a class diagram's namespace and an entity relationship diagram's subgraph) of the
  layout's size, widened to its title plus the padding when the title is wider, with the title measured without
  wrapping (a markdown title wraps at the layout's width) in the sheet's `ClusterTitle(html)` rule and placed on the
  top edge.
- `SplitStyles(LayoutNode)` and `SplitStyles(IEnumerable<string>)` - Mermaid's `styles2String`: the class styles,
  then the inline styles, then the label style, each `key:value` with a later key winning in its first place, split into
  the label styles (`isLabelStyle`: color and text properties) and the node styles.
- `LabelFontOf(StyleDeclarationSet, TextPaint)` - the font a label starts from: the style sheet's text rule with the
  element's own `font-family`, `font-size`, `font-weight`, `font-style`, and `color` over it.
- `IntersectRect`, `IntersectEllipse`, `IntersectCircle`, `IntersectLine`, and `IntersectPolygon` - the intersect
  functions of `rendering-elements/intersect/` with their exact arithmetic (the half-denominator rounding offset of the
  Graphics Gems line intersection, and the second-segment test that never rejects a crossing beyond the point).
- `SegmentBounds` - the box of path segments as SVG's `getBBox` gives it, including the extremes of elliptical arcs
  and the radius scaling SVG requires for an arc too small to reach its end.
- `ShapeNames.IsKnown`, `HandlerOf`, `Nearest`, `ShortNames`, and `DocumentedCount` - the registry: a case-sensitive
  test (Mermaid's `isValidShape`), the drawing function of a name, the nearest name a flowchart accepts (lower case, no
  underscore) by edit distance, and the 53 documented short names.

**Preconditions and postconditions**: `Draw` is given a node that is not a subgraph; the result is a pure function of
the node, the configuration, the style sheet, the label measurement, and the text width. The class is stateless and
safe for concurrent use.

#### Error Handling

- A shape name that this version does not draw (the shapes of other diagram types, such as `requirementBox` or
  `kanbanItem`) throws a located `MermaidRenderException` (`mermaid-render-failed`) that suggests the nearest
  documented shape; an `erBox` node without the data of an entity throws the same located failure, with a hint to draw
  entities in an `erDiagram` and to use another shape for a flowchart node.
- A style declaration outside the supported CSS subset is reported as `mermaid-style-ignored` (once per distinct
  declaration, through the sink) by the style parser and is not applied. The own styles of a class box are reported
  at the node's location, the class's first mention. The styles of a node marked `TrustedStyles` are never reported.
- An image node whose picture cannot be loaded is drawn as its frame, with the warning `mermaid-image-unresolved`
  (reported by `NodeImages`, or here when the render has no `NodeImages`).
- The errors of the label measurement, of the text width (the layout work limit and cancellation of the meter it
  charges), and of `NodeImages` (the image limit) propagate unchanged.

#### Dependencies

- **Rendering subsystem** - `LabelBuilder` (through `ShapeContext.MeasureLabel` and `ShapeContext.TextWidth`; the
  request and result records; `SplitBreaks` for the lines of an HTML class box text), `NodeImages` (natural sizes of
  pictures), `LayoutNode`, `LayoutClassBox`, `LayoutClassMember`, `LayoutErBox`, `LayoutErAttribute`
- **Configuration subsystem** - `MermaidConfig` (with `Class.Padding`, `Class.HideEmptyMembersBox`,
  `Er.DiagramPadding`, `Er.EntityPadding`, `Er.MinEntityWidth`, the flowchart's wrapping width, and the theme
  variables through `ThemeVariable`), `UnifiedStyles` (with `Resolve`, `CentersNodeSvgText`, and `ClusterTitle`),
  `ShapePaint`, `TextPaint`, `StyleDeclarations`, `StyleDeclarationSet`, `ColorMath` (the number text of an
  `htmlLabels` value)
- **Class subsystem** - `ClassModel.ParseGenericTypes` (the generic types of entity attributes)
- **Scenegraph subsystem** - `ScenePath`, `SceneSegment`, `SceneImage`, `SceneGroup`, `SceneText`, `SceneTextRun`,
  `SceneItem`, `SceneRect`
- **Syntax subsystem** - `DiagramDetector.EditDistance` (for `ShapeNames.Nearest`)
- **Package root** - `MermaidRenderException`, `MermaidDiagnosticCodes`, `MermaidReportSink`
- **Core `CanvasNet`** - `Rgba32`
- **.NET base class library** - `System.Text.Json.Nodes` (the raw `htmlLabels` value)

#### Callers

`ClusterLayout` draws every node and subgraph through `Draw` and `DrawCluster`; `EdgeBuilder` uses `SplitStyles` and
`LabelFontOf` for the edge and terminal labels; `FlowchartModel` (the `Flowchart` subsystem) validates the names of
`@{ shape: ... }` with `ShapeNames.IsKnown` and `Nearest`; `ClassModel` (the `Class` subsystem) gives its classes the
`classBox` shape with their `LayoutClassBox`, marks its notes and lollipop interfaces `TrustedStyles`, and sets the
`TextWidth` of its `ShapeContext`. `StateModel` (the `State` subsystem) gives its states the `rect` shape with the radii
of 10, the padding of 8, `CenterLabel`, and the class `statediagram-state` (or `rectWithTitle` with their
`Descriptions`), its pseudo-states the `stateStart`, `stateEnd`, `fork`, `join`, and `choice` shapes, its notes the
`note` shape, and its composite states, concurrent regions, and note groups the `roundedWithTitle`, `divider`, and
`noteGroup` clusters with their classes. `ErModel` (the `Er` subsystem) gives its entities the `erBox` shape with their
`LayoutErBox` and its subgraphs the `rect` cluster, and sets the `TextWidth` of its `ShapeContext`. The tests in
`ShapeLibraryTests.cs`, `ShapeLibraryVectorTests.cs`, and
`ShapeNamesTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- The circles that Mermaid draws through rough.js (`f-circ`, `fr-circ`, `cross-circ`, `anchor`, and the icon circle)
  are drawn as true circles; rough.js's curve is up to 0.1 percent wider, at most 0.06 pixel. The end state
  (`stateEnd`) is the exception: its rough.js outline is ported exactly (`PathData.RoughEllipse`), because its box of
  14.013 by 14 pixels sizes the node in the layout.
- Icons are not drawn (the diagram model reports them); an icon shape keeps its frame and counts the icon as a square of
  its size, which is the size of the placeholder the browser draws, so the node sizes match.
- The legacy `ellipse` of the syntax `A(-text-)`, which the pinned Mermaid fails to render, is drawn as an ellipse
  with the radii of half the label width plus the padding and half the label height plus half the padding, with an
  elliptical intersect function, and without a report.
- An image node whose picture cannot be loaded, on which Mermaid's render fails, is drawn as its frame with a natural
  size of 0 by 0 and an aspect ratio of 1 (so `w` and `h` still size it) and reported as `mermaid-image-unresolved`.
- Mermaid applies the `font-*` styles of a subgraph title after it has measured the title; this library does not
  apply them (only the `color`, as for the HTML span), so the subgraph has Mermaid's size but its title keeps the style
  sheet's font.
- Only the shapes and the subgraph drawing that a flowchart, a class diagram, a state diagram, and an entity
  relationship diagram can reach are ported; subgraphs, namespaces, and entity relationship subgraphs are drawn as
  `clusters.js`'s `rect`, and the clusters of a state diagram as its `roundedWithTitle`, `divider`, and `noteGroup`
  (not `kanbanSection`).
- The corner radii of the state shapes and clusters come from the built-in rules of the state style sheet; an `rx` or
  `ry` in a node's own styles or in the `themeCSS` is not applied (it is reported, outside the supported CSS subset).
- The SVG text of an entity box cell takes the color of the sheet's `.label` rule (`nodeTextColor`, else `textColor`),
  as the class box does, where the browser gives it the root's `fill` (`textColor`); the two are the same in the
  built-in themes.
- The class box does not port the SVG generic fix-up of the class diagram's `shapeUtil.ts` (which rewrites the first
  `tspan` of a text that holds `&gt`); the port decodes the character references itself, so a one-line text gives the
  same characters.
- The own styles of a class box are reported at the class's first mention rather than at its `style` statement,
  because the layout data carries no locations of styles.
