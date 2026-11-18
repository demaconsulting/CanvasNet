### ActivityLayout

![Activity Structure](PlantUmlActivityView.svg)

The `ActivityLayout` unit is the third software unit in the `Activity` subsystem. Its source files are
`Activity/ActivityLayout.cs` (the entry point, the work meter, the text helpers, the drawing of one diagram, the
extent, and the compression), `ActivityLayout.Build.cs` (the tile of every instruction and the assembly of a list),
`ActivityLayout.Branches.cs` and `ActivityLayout.IfTiles.cs` (ifs), `ActivityLayout.Loops.cs` (while and repeat),
`ActivityLayout.Parallel.cs` (fork, merge, and split), `ActivityLayout.Notes.cs` (notes and groups),
`ActivityLayout.Leaves.cs` (boxes, circles, diamonds, hexagons, bars), `ActivityLayout.Tiles.cs` (the tile base and
its decorators), `ActivityLayout.Canvas.cs` (the canvas and the connections), `ActivityLayout.Shapes.cs` (the placed
shapes and the arrows), `ActivityLayout.Worm.cs` (the simplification of an arrow's path), and
`ActivityLayout.Swimlanes.cs` (the swimlanes).

#### Purpose

`ActivityLayout` places an activity diagram as PlantUML's activity engine does, so that the picture agrees with
PlantUML's element by element. The unit's behavior is specified by the `CanvasNetPlantUml-Activity-ActivityLayout-*`
requirements in `docs/reqstream/canvas-net-plant-uml/activity/activity-layout.yaml`.

#### Data Model

- `ActivityGeometry(Width, Height, Shapes)` - the placed diagram before the page texts, with the document margin.
- `ActivityShape` - a placed rectangle, ellipse, polygon, path, line, text, or empty space with its role, paint, and
  location, and the flags of the compression (ignored on x or y, counted for the extent, counted for the slots, kept
  centered in a span).
- `Tile` and `TileGeometry` - PlantUML's `Ftile` and `FtileGeometry`: a tile has a width, a height, the x of its
  entry and exit, the y of its entry and exit (none for a flow that ends), and the swimlanes it draws in; it draws onto
  a `TileCanvas` at an offset. The decorators (`MarginTile`, `VerticalMarginTile`, `MinWidthTile`, `FixedHeightTile`,
  `RightMarginTile`, `KilledTile`, `ConnectedTile`, `AssemblyTile`) change a tile as PlantUML's `FtileMarged*`,
  `FtileMinWidthCentered`, `FtileKilled`, `FtileWithConnection`, and `FtileAssemblySimple` do.
- `Snake` - an arrow as a polyline with its head, label, color, and style; snakes that continue each other are merged
  and their paths simplified (`WormSimplifier`, PlantUML's `Worm`).

#### Key Methods

- `Run(model, context)` - builds the root tile, draws it (with swimlanes or without), compresses the drawing, moves
  its extent 5 pixels from the 10-pixel document margin, and sizes the picture as the extent plus 35.
- **Leaves** - an action box is its text plus 10 on each side with corners of 25; start is a 20-pixel disc, stop a
  22-pixel ring around a 12-pixel disc, end a 20-pixel circle with a cross; a connector is a 20-pixel circle with its
  letter; a diamond is 24 pixels; an if or loop test is a hexagon around its text (12 pixels on each side); a fork bar
  is 6 pixels high.
- **Assembly** - a list is a column of tiles 35 pixels apart (plus the height of an arrow's label), joined by vertical
  arrows; a note is drawn beside its instruction (`NoteTile`, PlantUML's `FtileWithNoteOpale`) or alone in the flow.
- **Ifs** - the branches side by side below the test, their arrows down from the diamond's corners and joined on a
  merge diamond (`IfWithLinksTile`); a branch that only stops is drawn beside the diamond (`IfDownTile`); an elseif
  chain is a row of hexagons with the branches below them (`IfLongHorizontalTile`), as `ConditionalBuilder` chooses.
- **Loops** - a while loop has its test above its body, its back arrow on the right and its exit on the left
  (`WhileTile`, with a backward action on the back arrow); a repeat loop has a diamond above its body, its test below,
  and its back arrow on the right (`RepeatTile`).
- **Parallel** - the branches of a fork side by side between two bars, of a merge ending on a diamond, of a split
  between thin lines, each 14 pixels apart, with a label on the closing bar.
- **Groups** - a partition frame around its tile with its title in a tab (`GroupTile`, PlantUML's `FtileGroup`).
- **Swimlanes** - every swimlane draws only the shapes of its own tiles (PlantUML's `UGraphicInterceptorOneSwimlane`)
  at the x of its column; the columns are as wide as their drawings (or their titles) with dividers between them; the
  arrows that cross swimlanes are drawn last across the columns; the titles are centered above them, and the background
  of a colored swimlane spans its column.
- **Compression** - the empty bands of the drawing that no shape occupies are found on x and then on y (PlantUML's
  `SlotFinder`), each shrunk by 5 pixels at both ends, and removed; shapes, lines, and centered titles move or stretch
  with them. The extent of the drawing follows PlantUML's `LimitFinder`.
- **Work** - every tile, shape, and step of the compression counts as work against `MaxLayoutWork`, and the
  cancellation token is checked every 256 units and at the start.

#### Error Handling

More work than `MaxLayoutWork` throws `PlantUmlRenderException` with `plantuml-limit-exceeded`, located at the
construct the type was detected by; cancellation throws `OperationCanceledException`. Null arguments throw
`ArgumentNullException`.

#### Dependencies

The Styling subsystem (`SkinTheme`, `ElementStyle`), the Scenegraph subsystem (`TextLayout`, `TextBlock`, `SceneText`,
`SceneSegment`), and the model classes.

#### Callers

`ActivityModel.Layout`; the tests.
