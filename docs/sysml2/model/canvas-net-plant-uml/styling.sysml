package CanvasNetPlantUml_PlantUmlStylingSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    public import CanvasNetPlantUml_PlantUmlSkinParams::*;

    part def PlantUmlStyling {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Styling namespace that holds the look of the diagrams. The PlantUmlSkinTheme unit holds the style of every element (fonts, colors, line widths, corners, and paddings) with the values of PlantUML's default skin, and the document and sequence settings; the PlantUmlSkinParams unit reads the skinparam statements of a diagram (one line or a block), applies the supported subset to a theme, reads PlantUML's colors, and reports what has no effect as pending reports, including style blocks. The subsystem depends on the root namespace's codes, locations, and pending reports, on the Syntax subsystem's statements, and on the core Canvas subsystem's colors; it is called by the diagram parsers and by the text layout of the Scenegraph subsystem (for the color tag). */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Styling/StylingTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/styling.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/styling.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/styling.yaml");
        }

        part skinTheme : PlantUmlSkinTheme;
        part skinParams : PlantUmlSkinParams;
    }
}