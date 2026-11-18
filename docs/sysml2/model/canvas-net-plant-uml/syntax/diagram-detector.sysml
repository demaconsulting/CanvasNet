package CanvasNet {
    part def PlantUmlDiagramDetector {
        doc /* The internal static DiagramDetector of the PlantUML Syntax subsystem with its record DetectedDiagram. Detect names the type of a preprocessed diagram: an @start tag other than @startuml names its type directly (an unknown tag is a located plantuml-unknown-diagram error with the nearest known tag as its hint); inside @startuml, the diagram is a sequence diagram when every statement outside multi-line text blocks is a sequence statement, else an activity diagram of the new syntax when every statement is an activity statement, else the type of the first statement that only another type uses (class, object, use case, state, legacy activity, component, deployment, timing, gantt, ...), which is recognized but not drawn, else a located syntax error at the first statement the more likely type refuses. A diagram with nothing to draw is a syntax error. Statements are recognized by their leading keywords and arrows, without regular expressions. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Syntax/DiagramDetector.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Syntax/DiagramDetectorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/syntax/diagram-detector.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/syntax/diagram-detector.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/syntax/diagram-detector.yaml */

        // The detector reads the statements the preprocessor produced
        dependency usesPreprocessor from PlantUmlDiagramDetector to PlantUmlPreprocessor;
    }
}
