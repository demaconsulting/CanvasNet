package CanvasNet {
    part def PlantUmlSvekRenderer {
        doc /* The internal renderer of the PlantUML Svek subsystem (SvekRenderer.cs, SvekRenderer.Clusters.cs, SvekRenderer.Notes.cs). Render draws a placed diagram with PlantUML's margins (6, 12, 15, 16) over the theme's background in PlantUML's paint order: the cluster images with their region separators, the entity images at their places, the notes with the folded corner and pointer of PlantUML's Opale, and the links as curves shortened by their decorations, solid, dashed, dotted, or bold, with the decorations, the labels after the direction triangle of PlantUML's TextBlockArrow2, and the end labels. ClusterImage and SvekClusterImage draw PlantUML's container symbols (folder, rectangle, frame, node, database, cloud with the pseudo-random bubbles of PlantUML's USymbolCloud, card) with their titles and stereotypes, header bands, paddings, and minimum widths. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekRenderer.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekRenderer.Clusters.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekRenderer.Notes.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/svek/svek-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/svek/svek-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/svek/svek-renderer.yaml */

        // The renderer draws the placement of the layout with the geometry, the look, and the text layout
        dependency usesSvekLayout from PlantUmlSvekRenderer to PlantUmlSvekLayout;
        dependency usesSvekGeometry from PlantUmlSvekRenderer to PlantUmlSvekGeometry;
        dependency usesSvekModel from PlantUmlSvekRenderer to PlantUmlSvekModel;
        dependency usesSkinTheme from PlantUmlSvekRenderer to PlantUmlSkinTheme;
        dependency usesTextLayout from PlantUmlSvekRenderer to PlantUmlTextLayout;
        dependency usesSceneModel from PlantUmlSvekRenderer to PlantUmlSceneModel;
    }
}
