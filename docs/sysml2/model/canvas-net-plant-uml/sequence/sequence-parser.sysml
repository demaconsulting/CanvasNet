package CanvasNet {
    part def PlantUmlSequenceParser {
        doc /* The internal SequenceParser of the PlantUML Sequence subsystem (SequenceParser.cs, SequenceParser.Statements.cs). Parse reads the statements of a sequence diagram by their leading keywords and arrows with a left-to-right scanner and no regular expressions: participant declarations of eight kinds with quoted names, aliases, colors, and order values; messages with a sender, an arrow (solid or dotted body, filled, open, half, or cross heads and circles on either end, a color), a receiver, the activation shorthands, and a label; activate, deactivate, destroy, and return; autonumber with a start, a step, a format, stop, and resume; notes left, right, over, across, and on a message's row, as notes, hexagons, or rectangles; groups with conditions, else, and end; boxes; dividers, delays, and spaces; page texts; hide footbox, scale, and skin parameters. It sorts the participants by their order values, attaches life changes to their messages, applies the skin parameters, and keeps the reports in text order. Statements that are not drawn are reported as plantuml-statement-unsupported; invalid statements throw a located plantuml-syntax-error with a hint; MaxNodes, MaxEdges, MaxLabelLength, and MaxNestingDepth throw plantuml-limit-exceeded. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceParser.cs, src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceParser.Statements.cs, src/DemaConsulting.CanvasNet.PlantUml/Sequence/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Sequence/SequenceParserTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/sequence/sequence-parser.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/sequence/sequence-parser.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/sequence/sequence-parser.yaml */

        // The parser reads located statements, uses the detector's statement helpers, applies the skin parameters, and builds the model
        dependency usesPreprocessor from PlantUmlSequenceParser to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlSequenceParser to PlantUmlDiagramDetector;
        dependency usesSkinParams from PlantUmlSequenceParser to PlantUmlSkinParams;
        dependency usesSequenceModel from PlantUmlSequenceParser to PlantUmlSequenceModel;
    }
}
