### DescriptionRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `DescriptionRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.Description` namespace.

#### Verification Approach

The unit is verified by drawing the images of elements and containers read by `DescriptionParser` from small original
bodies at the origin and asserting their sizes, outlines, clips, texts, and colors against the values of PlantUML's
pictures (the margins and offsets measured from the jar's SVG). No mocking is required. Unit tests reside in
`Description/DescriptionRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A size, an offset, a color, a count, or a role that
differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Description-DescriptionRenderer-Components: Components

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Component_HasPlantUmlBoxAndIcon`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Stereotypes_AreCenteredOverTheName`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Colors_ReplaceTheTheme`

Asserts the size of a component (the name and 40 by 30), the name 15 from the left with its baseline 34.533 from the
top, the fill, outline, and width, the three icon boxes, and a rectangle clip; asserts two stereotypes (a margin of 1)
centered over the name, as PlantUML places `«S1»`, `«S2»`, and `C4`; asserts that the colors of a declaration replace
the theme's on the box and the icon and on the text.

##### CanvasNetPlantUml-Description-DescriptionRenderer-Interfaces: Interfaces

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Interface_IsACircleWithItsNameBelow`

Asserts an ellipse clip of 18 at the center of the image, a circle of 16, the name 8 below the clip box and centered,
and the italic stereotype 8 above it.

##### CanvasNetPlantUml-Description-DescriptionRenderer-Actors: Actors

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Actor_IsAStickFigureWithItsNameBelow`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_BusinessActor_HasTheSlash`

Asserts the size of an actor with a stereotype (the widest of the texts and the figure of 27, two lines and 60 high),
a rectangle clip, the head of 16 centered 0.5 below the stereotype and filled `#F1F1F1` with the outline of the theme,
the body, arms, and legs 26 wide and 42 high below the head, the name centered with its baseline 60 below the figure's
top, and the italic stereotype centered with a margin of 1; asserts the three paths of a business actor and its slash
from (-1.944, 7.760) to (7.761, -1.944) of the head's center, as PlantUML draws it.

##### CanvasNetPlantUml-Description-DescriptionRenderer-UseCases: Use Cases

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_UseCase_HasPlantUmlEllipse`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_BusinessUseCase_HasTheSlash`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_EnclosingCircle_IsTheSmallest`

Asserts, against PlantUML's pictures, the size of the ellipse and the offsets of the first line of four use cases (a
name, a business use case with its margins, stereotypes over a name, and lines of different widths), the ellipse clip,
and the outline of the image's size; asserts the slash of a business use case at PlantUML's ends and none on a use
case, and the style elements of the business forms; asserts the smallest enclosing circle of a rectangle's corners,
of points on a line, of one point, and of no point, that the circle of 20 000 points on a circle holds them all, is no
larger than theirs, and does not depend on their order, and the ratio's bounds.

##### CanvasNetPlantUml-Description-DescriptionRenderer-Symbols: Symbols

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Symbols_HavePlantUmlMargins`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_FolderAndPackage_HaveTheirTab`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_SymbolWithoutDrawing_IsARectangle`

Asserts the margins, the offset of the name, and the number of shapes of the node, rectangle, frame, cloud, database,
and card; the folder's empty tab and the package's bold title in its tab; and an artifact drawn as a rectangle with
margins of 10 in the `Description` style.

##### CanvasNetPlantUml-Description-DescriptionRenderer-Containers: Containers

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_ContainerImage_Component_HasIconAndTitle`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_ContainerImage_Symbols_UseClusterOutlines`

Asserts the header and padding of a component container, its unfilled outline one pixel wide with the icon, the
stereotype 13 from the top, and the bold centered title below it; asserts a node container as a Svek cluster image
with a stereotype of 14 and the fill of `nodeBackgroundColor`, a component container's own fill, and no colors for a
cloud without skin parameters.
