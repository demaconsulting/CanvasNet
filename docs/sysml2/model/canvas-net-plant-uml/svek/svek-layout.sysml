package CanvasNetPlantUml_PlantUmlSvekLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetLayout_DagreLayout::*;
    private import CanvasNetLayout_Graph::*;
    private import CanvasNetLayout_LayoutWorkMeter::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlSvekGeometry::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlSvekLayout {
        doc /* The internal layout of the PlantUML Svek subsystem with its work meter (SvekLayout.cs, SvekLayout.Clusters.cs, SvekLayout.Edges.cs, PlantUmlLayoutWorkMeter.cs). Run validates a SvekDiagram and lays it out as PlantUML lays it out with Graphviz, over the dagre layered layout of the CanvasNetLayout system: PlantUML's spacing (nodesep 35 and ranksep 60 raised by the labels, or the diagram's), clusters that no link crosses laid out on their own and placed as one node, clusters that links cross as compound nodes made tight around their members with header bands inserted so that nothing overlaps, concurrent regions stacked or side by side with separators, links of length 1 kept in one rank as rows, direction hints, self loops, arcs over the units a straight link would cross, routes cut at cluster borders and clipped to the entity outlines, label slots with the direction triangle and the note on the link (kept 4 pixels clear of the drawn curves and decorations of the links and off the other labels, and set 20 pixels plus their width between two neighbors of a row), end labels, attached notes with their pointers, and floating notes with one link drawn as pointers. PlantUmlLayoutWorkMeter derives the shared LayoutWorkMeter with located PlantUmlRenderException failures (plantuml-limit-exceeded naming PlantUmlLimits.MaxLayoutWork, plantuml-render-failed); every dagre run and the post-processing charge one meter per call, and the cancellation token is observed. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.Clusters.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.Edges.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/PlantUmlLayoutWorkMeter.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/svek/svek-layout.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/svek/svek-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/svek/svek-layout.yaml");
        }

        // The layout runs dagre under the shared work meter, measures the labels and notes, and clips with the geometry
        dependency usesDagreLayout from PlantUmlSvekLayout to DagreLayout;
        dependency usesGraph from PlantUmlSvekLayout to Graph;
        dependency usesLayoutWorkMeter from PlantUmlSvekLayout to LayoutWorkMeter;
        dependency usesTextLayout from PlantUmlSvekLayout to PlantUmlTextLayout;
        dependency usesSkinTheme from PlantUmlSvekLayout to PlantUmlSkinTheme;
        dependency usesSvekModel from PlantUmlSvekLayout to PlantUmlSvekModel;
        dependency usesSvekGeometry from PlantUmlSvekLayout to PlantUmlSvekGeometry;
    }
}
