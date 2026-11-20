### DescriptionRenderer

![Description Structure](PlantUmlDescriptionView.svg)

The `DescriptionRenderer` unit is the third software unit in the `Description` subsystem. Its source files are
`Description/DescriptionRenderer.cs` (the image type, the dispatch, the component, the interface, the rectangle, and
the shared painter), `Description/DescriptionRenderer.Symbols.cs` (the other leaf symbols and the component container
image), and `Description/DescriptionRenderer.UseCase.cs` (the actor, the use case, and the enclosing circle).

#### Purpose

`DescriptionRenderer` measures and paints the elements and containers of description diagrams as PlantUML's
`EntityImageDescription` and `USymbol` classes draw them. The unit's behavior is specified by the
`CanvasNetPlantUml-Description-DescriptionRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/description/description-renderer.yaml`.

#### Data Model

- `DescriptionImage` - an `ISvekEntityImage`: the width, the height, the outline the links end on (`SvekClip`), and the
  painter.
- `DescriptionComponentContainerImage` - an `ISvekClusterImage` for a component container: the laid-out title and
  stereotypes, the fill and stroke, and the sizes of a rectangle cluster with the same texts (`SvekClusterImage`).
- The private `Painter` - the style of the element's symbol (`StyleOf`: `Component`, `Interface`, `Node`, `Rectangle`,
  `Folder` for a folder and a package, `Frame`, `Cloud`, `Database`, `Card`, `Actor` for an actor and a business actor,
  `UseCase` for a use case and a business use case, and `Description` for the others), its
  fill, stroke, and corner radius (the element's colors replace the style's), and the laid-out name or description,
  the name in bold (a package's tab), and the stereotypes (`«name»` per line, italic, in the style's font).

#### Key Methods

- `Image(element, theme, text, sink)` - the image of a leaf element; the texts are the stereotypes (with a margin of 1
  on each side, centered) over the name or description (its lines aligned left), the two blocks centered on each other
  (`DrawTexts`):
  - component (`USymbolComponent2`) - the texts with margins of 15, 25, 20, and 10, a rectangle with corners of 5, and
    the icon (`ComponentIcon`): a box of 15 by 10 20 from the right and 5 from the top, and two tabs of 4 by 2 22 from
    the right and 7 and 11 from the top;
  - interface (`USymbolInterface`, `CircleInterface2`) - a circle of radius 8 in a box of 18, the name 8 below the box
    and the stereotypes 8 above it, centered; the image is as tall above the circle as below it, so the circle is the
    center of the layout node (as PlantUML's shield makes it), and the links end on the circle's box (an ellipse
    clip);
  - node (`USymbolNode`) - margins of 15, 25, 20, and 10, the outline with its 3D top and right side 10 deep and its
    three inner lines;
  - rectangle (`USymbolRectangle`) - margins of 10 and corners of 5; every symbol without its own drawing
    (`IsDrawn` false) is drawn this way;
  - folder and package (`USymbolFolder`) - margins of 10, 20, 13, and 10 around the tab's text and the texts, a tab as
    wide as its text and 6, sloping 7 to the body (`SvekRenderer.FolderOutline`), and the line under the tab; a
    folder's tab is empty (40 by 15); a package shows its name in bold in the tab (10 from the left, 3 from the top,
    with margins of 6) and, below, its stereotypes and a display that differs from its name;
  - frame (`USymbolFrame`) - margins of 15, 25, 20, and 10, a rectangle with corners of 5, and an empty title corner a
    third as wide and 12 high with a cut of 7;
  - cloud (`USymbolCloud`) - margins of 15 and PlantUML's bubbly outline (`SvekRenderer.CloudOutline`);
  - database (`USymbolDatabase`) - margins of 10, 10, 24, and 5, the cylinder (`SvekRenderer.DatabaseOutline`) and
    the front curve of its top;
  - card (`USymbolCard`) - margins of 10, 10, 3, and 3 and a rectangle with corners of 5;
  - actor and business actor (`USymbolSimpleAbstract` with `ActorStickMan`) - the stereotypes, the stick figure, and
    the name (its lines aligned left) stacked and centered on each other; the figure is 26 wide and 59 high plus twice
    the line width, drawn by the Sequence subsystem's `SequenceRenderer.StickFigure` moved down by the line width less
    half a pixel (the head of radius 8 is the `description` outline, filled; the body, arms, and legs one
    `description-detail` path); a business actor's head is crossed by the chord between the angles 45 + 59.06 and
    45 - 59.06 degrees (`21 * pi / 64`); the links end on the box;
  - use case and business use case (`USymbolUsecase`, `TextBlockInEllipse`) - the stereotypes (with a margin of 1 on
    each side) over the name, every line centered; a business use case adds a margin of 7 on each side. The footprint
    of the texts (`Footprint`) is the corners of every line, from 1.5 below the top of its ascent to 1.5 below its
    baseline, of the stereotypes' margin box, and of a business use case's margin box; `ContainingEllipse` divides
    the y of every point by the ratio of the texts' height to their width (`EllipseRatio`: between 0.2 and 0.8, 1
    for an empty text), finds the smallest enclosing circle (`EnclosingCircle`: the incremental algorithm over the
    points in a fixed shuffled order, expected linear, with a tolerance for rounding and the largest diameter circle
    for three points on a line), and scales it back to an ellipse, grown by 6; the texts are placed so that the
    footprint's center is the ellipse's center, 2 higher. The links end on the ellipse (an ellipse clip). A business
    use case's slash (`BusinessSlash`) is the chord of the ellipse shrunk by 1 % between the angle of -20 degrees and
    the other angle where the ellipse turned by 45 degrees meets the vertical through the first point
    (`RotatedEllipse.getOtherTheta`).
- `ContainerImage(element, theme, text, sink)` - the image of a container: a component container is a
  `DescriptionComponentContainerImage` (the outline with corners of 5, one pixel wide and unfilled in the `Container`
  style, the component icon, the stereotypes 13 from the top and the bold title below them, both centered; the header,
  narrowest width, and padding of a rectangle cluster); the other containers are `SvekRenderer.ClusterImage` with the
  symbol of their keyword (package and folder a folder, node, frame, cloud, database, card; the others a rectangle)
  and stereotypes of 14.
- `ContainerColors(element, theme)` - the fill and line of a container: its own colors, else those that a skin
  parameter of its symbol sets (compared with the description theme's defaults: PlantUML's style of a symbol applies
  to its groups too), else none.
- `IsDrawn(symbol, container)`, `StyleOf(symbol)` - whether a symbol has its own drawing, and the style element of a
  symbol.

#### Error Handling

Null arguments throw `ArgumentNullException`; measuring and painting do not fail otherwise (texts with markup the text
layout cannot draw are reported through the sink of the call).

#### Dependencies

The `DescriptionModel` unit (the elements), the Svek subsystem (`ISvekEntityImage`, `ISvekClusterImage`,
`SvekClusterImage`, `SvekRenderer.ClusterImage` and its outlines), the Styling subsystem (`SkinTheme`), the
Scenegraph subsystem (`ScenePath`, `SceneText`, `SceneShapes`, `TextLayout`, `TextBlock`), and the Sequence
subsystem (`SequenceRenderer.StickFigure`).

#### Callers

The `DescriptionModel` unit (lowering), the `DescriptionParser` unit (`IsDrawn`), and the tests.
