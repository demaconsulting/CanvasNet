package CanvasNet {
    part def PlantUmlTextLayout {
        doc /* The internal TextLayout of the PlantUML Scenegraph subsystem with the TextAlign enumeration and the TextLine, TextBlock, and CreolePiece records (TextLayout.cs, TextLayout.Creole.cs). It resolves PlantUML's families to the bundled Liberation fonts only (SansSerif, Arial, and unknown families to Liberation Sans; Serif and Times to Liberation Serif; Monospaced and Courier to Liberation Mono), measures a run as the sum of its glyph advances without kerning (with the bundled fallback fonts for a missing character), measures a line as Java's logical SansSerif font on Windows (ascent and descent at least 2059 and 450 units of 2048, plus the line gap: 17.609 pixels at 14 pixels), breaks lines at PlantUML's \n and real line feeds, wraps at spaces to a width, reads the creole subset (bold, italic, monospace, underline, strike, color, size, font, Unicode characters, the tilde escape), reports markup outside it as plantuml-markup-unsupported and missing glyphs as plantuml-text-missing-glyph, and draws a block as text lines with baselines at the line top plus the height minus the descent, aligned left, centered, or right. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/TextLayout.cs, src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/TextLayout.Creole.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Scenegraph/TextLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/scenegraph/text-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/scenegraph/text-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/scenegraph/text-layout.yaml */

        // The layout measures with the bundled fonts, produces scene text, and reads colors of the color tag
        dependency usesFonts from PlantUmlTextLayout to Fonts;
        dependency usesRendering from PlantUmlTextLayout to Rendering;
        dependency usesSceneModel from PlantUmlTextLayout to PlantUmlSceneModel;
        dependency usesSkinParams from PlantUmlTextLayout to PlantUmlSkinParams;
    }
}