### SvekRenderer

![Svek Structure](PlantUmlSvekView.svg)

The `SvekRenderer` unit is the fifth software unit in the `Svek` subsystem. Its source files are
`Svek/SvekRenderer.cs` (the scene and the links), `Svek/SvekRenderer.Clusters.cs` (the container symbols and
`SvekClusterImage`), and `Svek/SvekRenderer.Notes.cs` (the notes).

#### Purpose

`SvekRenderer` draws a placed diagram as a scene with PlantUML's look: the link lines, decorations, and labels, the
container symbols of the clusters with their titles, the notes with their pointers, and the entity images at their
places. The unit's behavior is specified by the `CanvasNetPlantUml-Svek-SvekRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/svek/svek-renderer.yaml`.

#### Data Model

- `SvekRenderer` holds no state; its margins are `MarginLeft` 6, `MarginTop` 12, `MarginRight` 15, and
  `MarginBottom` 16, measured from the pictures the jar draws for original diagrams.
- `SvekClusterImage` (an `ISvekClusterImage`) - the symbol (`Folder`, `Rectangle`, `Frame`, `Node`, `Cloud`,
  `Database`, `Card`, or `None`), the id and location, the laid-out title and stereotypes, the fill, the stroke, and the
  corners; `HeaderHeight` = the title and stereotype height rounded down plus the symbol's extra height (5 for a node,
  15 for a database) plus 11, as PlantUML's Smetana pipeline reserves the cluster label (`ClusterHeader`,
  `CucaDiagramFileMakerSmetana`); `Padding` 16 (20 for a node, whose 3D side needs room); `MinWidth` = the title width
  rounded down plus the symbol's extra width (60 for a node) plus twice the padding; zero header, padding, and width for
  `None`.

#### Key Methods

- `Render(diagram, placement)` - the scene of the placement with the margins over the theme's background: the cluster
  images (parents first) and their dashed separators (`#181818`, 1.5 wide, dashes of 8 and 10), the entity images, the
  notes, and the links.
- `DrawLink(link, theme, dx, dy)` - nothing for a link that is not drawn; else the decorations of both ends
  (`SvekGeometry.Extremity`), the curve of the route shortened by their contact lengths (`Spline`), stroked in the
  link's color and width or the theme's (`SvekArrow`: `#181818`, 1; 2 for bold), dashed 7 and 7 or dotted 1 and 3, the
  decorations solid with their fills, the label after its direction triangle (PlantUML's `TextBlockArrow2`: a filled
  triangle of `(int)(0.8 × size)` in a square of the font size, its tip at the angle the layout gave, measured as
  PlantUML measures it from the y axis), and the end labels, every item with the link's id and location.
- `ClusterImage(symbol, id, title, stereotypes, theme, text, sink, location, fill, line)` - measures the title (bold,
  the style's size) and the stereotypes (`«name»`, italic 12) and builds the image in the `Package` style for a folder
  and the `Container` style otherwise.
- `SvekClusterImage.Draw(box)` - the outlines of PlantUML's `USymbol*` classes drawn "big": the folder with its tab as
  wide as the title plus 6 and as high as it plus 6, sloping 7 to the body, and the line under the tab, the title at
  (4, 2) (`USymbolFolder`); the rounded rectangle with a centered title at 2 below the stereotypes
  (`USymbolRectangle`); the frame with its cut tab of 10 and the title at (3, 1) (`USymbolFrame`); the node with its 3D
  top and side of 10 and the title centered 13 down (`USymbolNode`); the cylinder with ends of 10 and the title 22 down
  (`USymbolDatabase`); the cloud (`USymbolCloud`); the card with its line under the title (`USymbolCard`).
- `FolderOutline`, `DatabaseOutline`, `CloudOutline` - the outlines; the cloud follows PlantUML's construction (bubbles
  of 11, or a sixteenth of the larger side, along the sides 8 inside the box, each side bulging out at its middle, and
  one cubic bulge between every two points), with the pseudo-random values of `java.util.Random` seeded by the size of
  the box (the 48-bit linear congruential generator that the Java platform specifies), so the same box gets PlantUML's
  cloud.
- `DrawNote(note, theme, dx, dy)` - the outline of PlantUML's `Opale` with the folded corner of 10 and, when the note
  has a pointer, the pointer 8 wide where it leaves the side the layout chose (clamped as `Opale` clamps it), the fold,
  and the text at (6, 5), in the theme's note style (`#FEFFDD`, `#181818` half a pixel wide) or the note's fill.

#### Error Handling

Null arguments throw `ArgumentNullException`. A link without a route or not drawn draws nothing; a cluster without an
image draws nothing.

#### Dependencies

The `SvekModel`, `SvekLayout` (the placement records and the label and note margins), and `SvekGeometry` units, the
Styling subsystem (`SkinTheme`, `SkinElement`, `ElementStyle`), the Scenegraph subsystem (`Scene`, `ScenePath`,
`SceneText`, `SceneShapes`, `TextLayout`, `TextBlock`), and the core `Canvas` subsystem (`Rgba32`).

#### Callers

The diagram subsystems of the graph diagrams (the `ClassModel` unit of the Class subsystem) and the tests.
