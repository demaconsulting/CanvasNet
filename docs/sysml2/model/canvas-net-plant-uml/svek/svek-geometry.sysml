package CanvasNet {
    part def PlantUmlSvekGeometry {
        doc /* The internal geometry of the PlantUML Svek subsystem (SvekGeometry.cs, SvekGeometry.Extremities.cs): Spline draws a route as a line, the quadratic curve of three points raised to a cubic, or the clamped uniform cubic B-spline of more points converted to Bezier pieces by Boehm knot insertion (written from the mathematics, as Graphviz draws the edges of PlantUML's diagrams), and Flatten gives points along that curve; Clip and Contains for rectangles, ellipses, and diamonds; CutAtBox, Shorten, and ArrivalAngle for the ends of routes; Arc and Circle; and Extremity, which builds every drawn link decoration with the geometry, fill, stroke, and contact length of PlantUML's svek/extremity classes, turned to the direction in which the link arrives, with IsDrawn and the LinkDecor margins. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekGeometry.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekGeometry.Extremities.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekGeometryTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/svek/svek-geometry.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/svek/svek-geometry.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/svek/svek-geometry.yaml */

        // The geometry emits scene segments for the model's points and decorations
        dependency usesSceneModel from PlantUmlSvekGeometry to PlantUmlSceneModel;
        dependency usesSvekModel from PlantUmlSvekGeometry to PlantUmlSvekModel;
    }
}
