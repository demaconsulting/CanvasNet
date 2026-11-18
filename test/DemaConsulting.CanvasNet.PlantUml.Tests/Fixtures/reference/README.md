# PlantUML Reference Pictures

The `*.svg` files in `sequence/` and `activity/` are **goldens**: the pictures that the reference implementation draws
for the original fixtures of `../sequence/` and `../activity/`. `PlantUmlReferenceLayoutTests` reads them with
`System.Xml` and compares CanvasNet's layout with them: the picture size (within 3 %) and the anchor of every text
(matched by its content, in document order; the runs of a formatted line are joined), and, each within 1.5 pixels, the
boxes of the participant heads and tails, the life lines, and the message lines of a sequence diagram, and the action
boxes, the circles, and the diamonds and hexagons of an activity diagram.

## Provenance

- **Inputs**: the `.puml` fixtures of `../sequence/` and `../activity/`, written for this repository; no input comes
  from PlantUML's documentation or tests.
- **Generator**: `pwsh tools/visual-diff/update-plantuml-goldens.ps1 -Java <java> -Jar <jar>` (developer machine, not
  CI). The script turns Graphviz off, so only the engines that do not need it run, as in CanvasNet.
- **Versions**: `plantuml-mit-1.2026.10.jar` (the MIT-licensed distribution of PlantUML, whose sequence engine is
  "teoz" and whose activity engine is the "ftile" composition of the new syntax) on Java 25.0.3 (OpenJDK, headless) on
  Windows. Each file names the PlantUML version in its
  `<?plantuml ...?>` instruction.
- **Fonts**: Java's logical `SansSerif` font on Windows is Arial, whose metrics CanvasNet's bundled Liberation Sans
  shares, so the texts are measured alike.
- **Ownership**: the pictures are PlantUML's output for inputs of this repository; they contain no PlantUML source.

## Updating

Regenerate the goldens after a fixture changes or to move to a newer PlantUML; review the diff of the goldens like
code, because it shows which coordinates moved.
