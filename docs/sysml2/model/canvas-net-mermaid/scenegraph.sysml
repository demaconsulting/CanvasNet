package CanvasNet {
    part def MermaidScenegraph {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Scenegraph namespace that is the last stage of the Mermaid pipeline and the only one that produces pixels, in two steps. The MermaidSceneModel unit is the immutable scene of a laid-out diagram in Mermaid's SVG coordinates and the assertion surface of the tests: Scene (the view box, the items in paint order, and the picture loader of the scene) with the items SceneGroup (translated children), ScenePath (SVG path commands with fill, fill rule, and stroke with dashes), SceneText (one line of styled runs at an anchor), and SceneImage (a picture filling a box), each with a role, Mermaid's element id, and the location of its construct; Scene.BoundsOf gives bounds as an SVG bounding box does and Scene.ToPath converts geometry to a core Path. The MermaidScenePainter unit draws a scene onto a core Surface over a background at a uniform or fitted placement with the public CanvasNet drawing API: anti-aliased fills (nonzero or even-odd), strokes with scaled width and dashes (an invalid or too fine pattern drawn solid), text in the bundled fonts only (common family names mapped to the bundled families, no installed fonts), and pictures from the scene's loader at device size stretched to their box; it charges every item to the painted-pixel limit before painting it (a located mermaid-limit-exceeded beyond it), skips geometry that is not finite or far outside every surface, and observes cancellation. The subsystem is original work and is internal; the Rendering and Flowchart subsystems build scenes and MermaidDocument paints them. As of this release, its constituent units are MermaidSceneModel and MermaidScenePainter. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Scenegraph/ScenegraphTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/scenegraph.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/scenegraph.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/scenegraph.yaml */

        part mermaidSceneModel : MermaidSceneModel;
        part mermaidScenePainter : MermaidScenePainter;
    }
}
