package CanvasNetPlantUml_PlantUmlSceneModel {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlSceneModel {
        doc /* The internal scene records of the PlantUML Scenegraph subsystem (SceneModel.cs): SceneRect, SceneSegmentKind and SceneSegment (move, line, cubic, close), SceneStroke (color, width, dashes), SceneFont, SceneTextRun (text, font, color, measured advance, underline, strike), the SceneItem base with its role, id, and location, ScenePath (segments, fill, stroke), SceneText (start, baseline, runs, box), and Scene (view box, items, background, and the diagram's own scale) with BoundsOf and ToPath; and the static SceneShapes builders of rectangles with square or rounded corners, ellipses (the four-arc cubic approximation), polygons, polylines, and lines; and the page texts (SceneModel.Page.cs): PageText and PageTexts (title, header, footer, caption, legend) and the static ScenePage, which places them around the body of a diagram as PlantUML does and moves scene items. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/SceneModel.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/SceneModel.Page.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Scenegraph/SceneModelTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/scenegraph/scene-model.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/scenegraph/scene-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/scenegraph/scene-model.yaml");
        }

        // Scene colors are core colors and shapes become core paths
        dependency usesCanvas from PlantUmlSceneModel to CanvasNet_CanvasSubsystem::Canvas;
        dependency usesGeometry from PlantUmlSceneModel to CanvasNet_GeometrySubsystem::Geometry;

        // The page texts are laid out with the text layout in the styles of the look
        dependency usesTextLayout from PlantUmlSceneModel to PlantUmlTextLayout;
        dependency usesSkinTheme from PlantUmlSceneModel to PlantUmlSkinTheme;
    }
}