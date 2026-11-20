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
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_DeploymentOutlines_AreTheSymbols`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_SymbolWithoutDrawing_IsARectangle`

Asserts the margins, the offset of the name, and the number of shapes of the node, rectangle, frame, cloud, database,
card, artifact, storage, file, queue, stack, agent, collections, and label (no shape); the folder's empty tab and the
package's bold title in its tab; a small storage's outline starting at half its width (an ellipse) and a long one's at
35, a stack's filled box without an outline and its unfilled ledges from the top-left corner, a collections symbol's
back box 4 to the right and below and 4 smaller, and an artifact's document icon of 12 by 14, 17 from the right and 5
from the top; and an action drawn as a rectangle with margins of 10 in the `Description` style (a process container is
not drawn either).

##### CanvasNetPlantUml-Description-DescriptionRenderer-Hexagons: Hexagons

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Hexagon_IsTwiceAsWideAsItsTexts`

Asserts a hexagon box twice as wide as its name and 10 higher, the name a quarter of the width from the left and 5
from the top, and an outline of six corners starting at the middle of the left side and turning at a quarter of the
width on the top.

##### CanvasNetPlantUml-Description-DescriptionRenderer-Persons: Persons

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_Person_HasAHeadOverItsBody`

Asserts PlantUML's head of 20.663 for the name `Person` (0.42 times the square root of the body's area), the image as
wide as the body and as high as the body and the head, the head centered at the top, the body below it, and the name
10 from the left and 10 below the head.

##### CanvasNetPlantUml-Description-DescriptionRenderer-SequenceSymbols: Boundaries, Controls, and Entities

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_Image_SequenceSymbols_AreThoseOfSequenceDiagrams`

Asserts, for a boundary, a control, and an entity, a box 49 or 32 wide and 32 plus the name high for a short name and
as wide as a long name, the circle of 24 four from the top and centered (a boundary's 8.5 right of the middle), two
paths, the name 32 from the top, and the mark outlined 0.5 wide (a control's arrow 1).

##### CanvasNetPlantUml-Description-DescriptionRenderer-Containers: Containers

**Tests**: `DescriptionRendererTests.PlantUmlDescriptionRenderer_ContainerImage_Component_HasIconAndTitle`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_ContainerImage_DeploymentSymbols_DrawTheirOutline`,
`DescriptionRendererTests.PlantUmlDescriptionRenderer_ContainerImage_Symbols_UseClusterOutlines`

Asserts the header and padding of a component container, its unfilled outline one pixel wide with the icon, the
stereotype 13 from the top, and the bold centered title below it; asserts, for artifact, storage, file, queue, stack,
and hexagon containers, the same header and padding, the number of shapes of the outline (4, 1, 2, 2, 2, 1), none
filled, one pixel wide (a stack's box without an outline), the stereotype at 2 (5 for a storage, 13 for a stack), and
the bold centered title below it (2 lower for a storage); asserts a node container as a Svek cluster image
with a stereotype of 14 and the fill of `nodeBackgroundColor`, a component container's own fill, and no colors for a
cloud without skin parameters.
