### SvekRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `SvekRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.Svek` namespace.

#### Verification Approach

The unit is verified with placements built in the tests (or laid out by `SvekLayout` from small diagrams) and with
cluster images built by `SvekRenderer.ClusterImage` with the text layout of the package, asserting the items of the
scene: their roles, ids, segments, strokes, fills, and texts; the folder is compared with a picture drawn by the
PlantUML jar. No mocking is required. Unit tests reside in `Svek/SvekRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A size, a point, a stroke, a fill, a text, or a count
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Svek-SvekRenderer-Scene: The Scene Of A Placement

**Tests**: `SvekRendererTests.PlantUmlSvekRenderer_Render_MarginsBackgroundAndOrder`

Asserts the view box of the placement with the margins 6, 12, 15, and 16, the theme's background, the paint order
(clusters, entities, notes, links), and the entity images drawn at their places shifted by the margins.

##### CanvasNetPlantUml-Svek-SvekRenderer-Links: Link Lines, Decorations, And Labels

**Tests**: `SvekRendererTests.PlantUmlSvekRenderer_DrawLink_LineStyles`,
`SvekRendererTests.PlantUmlSvekRenderer_DrawLink_DecorationsAndShortenedLine`,
`SvekRendererTests.PlantUmlSvekRenderer_DrawLink_NotDrawn_DrawsNothing`,
`SvekRendererTests.PlantUmlSvekRenderer_DrawLink_LabelsAndTriangle`

Asserts solid lines 1 wide, dashes of 7 and 7, dots of 1 and 3, bold lines 2 wide, a thickness, and a color; asserts the
decorations at both ends with their fills and the line stopping at their contact lengths; asserts that a hidden link, a
note pointer, and a link without a route draw nothing; asserts the middle label after its direction triangle and the
end labels, in the font of the link labels.

##### CanvasNetPlantUml-Svek-SvekRenderer-Clusters: Container Symbols

**Tests**: `SvekRendererTests.PlantUmlSvekRenderer_ClusterImage_Folder_MatchesPlantUml`,
`SvekRendererTests.PlantUmlSvekRenderer_ClusterImage_EverySymbol_DrawsInsideItsBox`,
`SvekRendererTests.PlantUmlSvekRenderer_ClusterImage_NoneAndCloud`,
`SvekRendererTests.PlantUmlSvekRenderer_ClusterImage_StereotypeSize_IsTheCallers`

Asserts the header band of a package's title, its tab as wide as the title plus 6 with its slope of 7, the line under
the tab, and the bold title 4 from the left against PlantUML's picture; asserts that the rectangle, frame, node,
database, cloud, and card draw their outlines inside their boxes with their titles, and the padding and extra header of
the node and the database; asserts that a symbol of `None` draws nothing and reserves no space and that a cloud is the
same for the same box; asserts italic stereotypes of 12 by default and of 14 when the caller asks for it, with a taller
header.

##### CanvasNetPlantUml-Svek-SvekRenderer-Notes: Notes And Pointers

**Tests**: `SvekRendererTests.PlantUmlSvekRenderer_DrawNote_PointerFromEachSide`

Asserts the folded corner, the text 6 from the left and 5 from the top, and, for a note without a pointer and a note
with a pointer from each side, that the outline touches the pointer's tip.
