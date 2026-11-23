package CanvasNetPlantUml_PlantUmlSkinTheme {
    private import SoftwareItemMetadata::*;

    part def PlantUmlSkinTheme {
        doc /* The internal immutable record SkinTheme of the PlantUML Styling subsystem with the SkinElement enumeration and the ElementStyle record. Default holds the values of PlantUML's default skin (plantuml.skin): a white document; SansSerif 14 text in black; participants and their kin filled #E2E2F0, outlined #181818 half a pixel wide, padded 7, a participant with corners of 5; notes filled #FEFFDD; arrows with 13-pixel labels; black group frames 1.5 wide with bold 11-pixel conditions and a #EEEEEE header tab; dividers; dashed life lines; header and footer in #888888 at 10 pixels; a bold title; a legend filled #DDDDDD with corners of 15; and the activity elements (activities filled #F1F1F1 with corners of 25, diamonds, arrows, circles, bars, swimlanes with 18-pixel titles, partitions). It also holds the document background, shadowing, the maximum message size, the message alignment, the response-below-arrow flag, the foot box, and the arrow thickness. With returns a new theme with one element's style changed. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Styling/SkinTheme.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Styling/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Styling/SkinThemeTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/styling/skin-theme.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/styling/skin-theme.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/styling/skin-theme.yaml");
        }

        // The theme's colors are core colors
        dependency usesCanvas from PlantUmlSkinTheme to CanvasNet_CanvasSubsystem::Canvas;
    }
}