package CanvasNetVegaLite_VegaLiteScenePositions {
    private import SoftwareItemMetadata::*;

    part def VegaLiteScenePositions {
        doc /* The internal static ScenePositions of the Vega-Lite Scenegraph subsystem (stateless and thread-safe; the part def carries the VegaLite prefix because the Mermaid Scenegraph subsystem has a ScenePositions unit too): the positions of the features of a laid-out chart (titles, views, axes, legends, and marks) in the pixels of the surface a placement draws on, from the tags the chart builders set on the scene. Report does nothing when the render options have no PositionHandler and otherwise delivers what Collect returns; VegaLiteDocument calls it in both Render overloads before painting. ViewPath(LayerNode) gives every view a canonical path of one operator[index] segment per level below the root joined by '.' ("" for the root; layer for a layered parent, concat, hconcat, vconcat, repeat, or facet from the operator of the parent; the layers a composite mark or another expansion adds no segment), ViewName the name the specification gives the view, and FacetValues the row, column, or facet value of a facet cell. The ids are title for a title, the path for a view, the channel for an axis, the first channel for a legend, and <mark>-<n> for a mark, n counting the items of that mark type in its view from 0 in drawing order, each vertex of a line, area, or trail counted. A guide is reported from the ScenePositionTag of its group (kind, id, text, view, view name, and bounds; else the group's own rectangle: a view's data rectangle, a legend's frame), a mark item from the row its SceneItem.Datum records with the values of the SceneMarkContext of its marks group (the normalized values of the channels bound to a field or a datum, and the data fields of the row, as MarkBuilder records them) and the shared ChannelNames set of the Vega-Lite encoding channels, and a vertex from the SceneVertex list of a ScenePath or SceneTrail; a mark whose row was not recorded is not reported. Geometry gives a mark its geometry bounds without stroke and aims at the center of the box, except a rule at its midpoint, a symbol at its center with its shape as the box, a text at its anchor with its measured box, an arc at its mid-angle at mid-radius, and a vertex at itself with a box of zero size; box and pixel are mapped through the group translations and the placement. The order is titles, views, axes, and legends, then marks, each in drawing order, and a kind the PositionKindFilter rejects is skipped before its geometry is computed. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.VegaLite/Scenegraph/ScenePositions.cs");
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Scenegraph/ScenePositionsTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/VegaLiteDocumentPositionTests.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/scenegraph/scene-positions.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/scenegraph/scene-positions.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/scenegraph/scene-positions.yaml");
        }
    }
}
