package CanvasNet {
    part def PlantUmlActivityParser {
        doc /* The internal ActivityParser of the PlantUML Activity subsystem (ActivityParser.cs, ActivityParser.Statements.cs). Parse reads the statements of an activity diagram of the new syntax with hand-written scanners and no regular expressions, as PlantUML's activity commands give them meaning: start, stop, end, kill, and detach; actions of one or several lines with a color; ifs with their tests and labels (then, is, and equals forms), elseif, else, and endif; while loops with their labels, backward actions, and exit labels; repeat loops with their start labels, tests, labels, and backward actions; forks with fork again, end fork with a label, and end merge; splits; arrows with labels, colors, and styles that lead into the next instruction; swimlanes with colors and display names; notes left and right, floating, of one or several lines; connectors; partitions and groups; page texts, scale, and skin parameters. It applies the skin parameters, keeps the reports in text order, and counts the arrows. Statements that are not drawn (switch, goto, label, box styles, package, rectangle, and card frames) are reported as plantuml-statement-unsupported; invalid statements throw a located plantuml-syntax-error with a hint; MaxNodes, MaxEdges, MaxLabelLength, and MaxNestingDepth throw plantuml-limit-exceeded. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityParser.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityParser.Statements.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityParserTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/activity/activity-parser.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/activity/activity-parser.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/activity/activity-parser.yaml */

        // The parser reads located statements, uses the detector's statement helpers, applies the skin parameters, and builds the model
        dependency usesPreprocessor from PlantUmlActivityParser to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlActivityParser to PlantUmlDiagramDetector;
        dependency usesSkinParams from PlantUmlActivityParser to PlantUmlSkinParams;
        dependency usesActivityModel from PlantUmlActivityParser to PlantUmlActivityModel;
    }
}
