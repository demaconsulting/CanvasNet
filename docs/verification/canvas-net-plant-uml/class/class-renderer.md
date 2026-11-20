### ClassRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `ClassRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.Class` namespace.

#### Verification Approach

The unit is verified by building the images of entities read by `ClassParser` with the text layout of the package
and drawing them at a known place, asserting their sizes and the items of the scene: roles, segments, strokes, fills,
fonts, and texts. The sizes and offsets are the ones the PlantUML jar draws for the same entities. No mocking is
required. Unit tests reside in `Class/ClassRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A size, a point, a stroke, a fill, a font, or a text that
differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Class-ClassRenderer-ClassBox: The Class Box

**Tests**: `ClassRendererTests.PlantUmlClassRenderer_EmptyClass_HasPlantUmlSize`,
`ClassRendererTests.PlantUmlClassRenderer_Header_DrawsStereotypesGenericAndSpot`

Asserts the box of a class without members: the circle block of 26 by 32 beside the name with a margin of 3 and two
empty compartments 8 high; asserts the stereotypes in guillemets in italic 12 above the name, the generic box dashed
and white over the top-right corner, a spot written in a stereotype, and no circle when it is hidden.

##### CanvasNetPlantUml-Class-ClassRenderer-Spot: The Circled Character

**Tests**: `ClassRendererTests.PlantUmlClassRenderer_Spot_HasTheKindsCharacterAndColor`

Asserts the character and the color of the spot of every kind and the italic name of an abstract class and an
interface.

##### CanvasNetPlantUml-Class-ClassRenderer-Members: Icons And Modifiers

**Tests**: `ClassRendererTests.PlantUmlClassRenderer_Members_DrawIconsAndModifiers`,
`ClassRendererTests.PlantUmlClassRenderer_IconSizeZero_WritesTheCharacters`

Asserts the shape of every visibility icon, hollow for a field and filled for a method, in the colors of the skin, the
icon column of 14, the underlined static member, and the italic abstract one; asserts that an icon size of 0 writes the
visibility characters.

##### CanvasNetPlantUml-Class-ClassRenderer-Separators: Separators

**Tests**: `ClassRendererTests.PlantUmlClassRenderer_Separators_DrawTheirLinesAndTitles`

Asserts a solid line of 1 for `--`, a dotted line for `..`, a double line for `==`, a thin line for `__`, and a
title centered between two halves of the line.

##### CanvasNetPlantUml-Class-ClassRenderer-Objects: Objects And Maps

**Tests**: `ClassRendererTests.PlantUmlClassRenderer_Object_DrawsNameAndFields`,
`ClassRendererTests.PlantUmlClassRenderer_Map_DrawsATableOfEntries`

Asserts the centered name of an object with a padding of 2, its fields below a line 6 from the side, and the 16 pixels
of an object without fields; asserts a row per map entry, the keys centered in their column, the values 5 from theirs,
and lines one pixel wide.
