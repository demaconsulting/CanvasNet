package CanvasNetPlantUml_PlantUmlSkinParams {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlPreprocessor::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;

    part def PlantUmlSkinParams {
        doc /* The internal static SkinParams of the PlantUML Styling subsystem with the SkinParamEntry record (SkinParams.cs) and PlantUML's colors (SkinParams.Colors.cs). TryRead reads a skinparam statement written on one line or as a block whose inner names are joined to the block name and to the names of the nested blocks around them; TrySkipStyle reports a style block as plantuml-style-unsupported and skips it; Apply applies the entries in order, without regard to case, to a SkinTheme: the document settings, the sequence settings, and the background, border, and font colors, font sizes, names, and styles, and border thicknesses of the element prefixes, and reports every other parameter, a stereotyped parameter, and a value that cannot be read as the warning plantuml-skinparam-ignored. TryParseColor reads HTML color names with or without a hash, #RRGGBB, #RRGGBBAA, #RGB, a single hexadecimal digit for a gray, transparent, and the first color of a gradient. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Styling/SkinParams.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Styling/SkinParams.Colors.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Styling/SkinParamsTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/styling/skin-params.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/styling/skin-params.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/styling/skin-params.yaml");
        }

        // The parameters change a theme, read located statements, and name colors with the core's color table
        dependency usesSkinTheme from PlantUmlSkinParams to PlantUmlSkinTheme;
        dependency usesPreprocessor from PlantUmlSkinParams to PlantUmlPreprocessor;
        dependency usesCanvas from PlantUmlSkinParams to CanvasNet_CanvasSubsystem::Canvas;
    }
}