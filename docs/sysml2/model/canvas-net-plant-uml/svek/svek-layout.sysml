package CanvasNet {
    part def PlantUmlSvekLayout {
        doc /* The internal layout of the PlantUML Svek subsystem with its work meter (SvekLayout.cs, SvekLayout.Clusters.cs, SvekLayout.Edges.cs, PlantUmlLayoutWorkMeter.cs). Run validates a SvekDiagram and lays it out as PlantUML lays it out with Graphviz, over the dagre layered layout of the CanvasNetLayout system: PlantUML's spacing (nodesep 35 and ranksep 60 raised by the labels, or the diagram's), clusters that no link crosses laid out on their own and placed as one node, clusters that links cross as compound nodes made tight around their members with header bands inserted so that nothing overlaps, concurrent regions stacked or side by side with separators, links of length 1 kept in one rank as rows, direction hints, self loops, arcs over the units a straight link would cross, routes cut at cluster borders and clipped to the entity outlines (on the side of a rectangle that faces the ranks, never at a corner, and straight between the facing sides for a short link without a label), label slots with the direction triangle and the note on the link, end labels, attached notes with their pointers, and floating notes with one link drawn as pointers. PlantUmlLayoutWorkMeter derives the shared LayoutWorkMeter with located PlantUmlRenderException failures (plantuml-limit-exceeded naming PlantUmlLimits.MaxLayoutWork, plantuml-render-failed); every dagre run and the post-processing charge one meter per call, and the cancellation token is observed. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.Clusters.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekLayout.Edges.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/PlantUmlLayoutWorkMeter.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/svek/svek-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/svek/svek-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/svek/svek-layout.yaml */

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
