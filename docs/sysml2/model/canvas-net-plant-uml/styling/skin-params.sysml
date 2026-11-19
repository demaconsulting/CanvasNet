package CanvasNet {
    part def PlantUmlSkinParams {
        doc /* The internal static SkinParams of the PlantUML Styling subsystem with the SkinParamEntry record (SkinParams.cs) and PlantUML's colors (SkinParams.Colors.cs). TryRead reads a skinparam statement written on one line or as a block whose inner names are joined to the block name; TrySkipStyle reports a style block as plantuml-style-unsupported and skips it; Apply applies the entries in order, without regard to case, to a SkinTheme: the document settings, the sequence settings, and the background, border, and font colors, font sizes, names, and styles, and border thicknesses of the element prefixes, and reports every other parameter, a stereotyped parameter, and a value that cannot be read as the warning plantuml-skinparam-ignored. TryParseColor reads HTML color names with or without a hash, #RRGGBB, #RRGGBBAA, #RGB, a single hexadecimal digit for a gray, transparent, and the first color of a gradient. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Styling/SkinParams.cs, src/DemaConsulting.CanvasNet.PlantUml/Styling/SkinParams.Colors.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Styling/SkinParamsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/styling/skin-params.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/styling/skin-params.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/styling/skin-params.yaml */

        // The parameters change a theme, read located statements, and name colors with the core's color table
        dependency usesSkinTheme from PlantUmlSkinParams to PlantUmlSkinTheme;
        dependency usesPreprocessor from PlantUmlSkinParams to PlantUmlPreprocessor;
        dependency usesCanvas from PlantUmlSkinParams to Canvas;
    }
}