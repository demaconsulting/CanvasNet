package CanvasNetPlantUml_PlantUmlScenegraphSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetPlantUml_PlantUmlSceneModel::*;
    public import CanvasNetPlantUml_PlantUmlTextLayout::*;
    public import CanvasNetPlantUml_PlantUmlScenePainter::*;

    part def PlantUmlScenegraph {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Scenegraph namespace that is the drawing stage of the PlantUML pipeline, independent of the diagram type. The PlantUmlSceneModel unit holds the immutable scene (paths of lines, rectangles, rounded rectangles, ellipses, and polygons with fills, strokes, and dashes; text lines of styled runs; the view box, the background, and the diagram's scale); the PlantUmlTextLayout unit measures text with the bundled fonts as PlantUML measures it with Java's logical fonts, breaks and wraps lines, reads the creole markup subset, and reports markup outside it and missing glyphs; the PlantUmlScenePainter unit paints a scene onto a core Surface with bounds checks, the painted-pixel budget, and cancellation. The subsystem depends on the core Canvas, Geometry, Drawing, Fonts, and Rendering subsystems and on the Styling subsystem's colors; it is called by PlantUmlDocument and by the diagram layouts. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Scenegraph/ScenegraphTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/scenegraph.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/scenegraph.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/scenegraph.yaml");
        }

        part sceneModel : PlantUmlSceneModel;
        part textLayout : PlantUmlTextLayout;
        part scenePainter : PlantUmlScenePainter;
    }
}