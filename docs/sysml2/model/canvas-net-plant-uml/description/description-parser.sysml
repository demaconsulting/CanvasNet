package CanvasNet {
    part def PlantUmlDescriptionParser {
        doc /* The internal parser of the PlantUML Description subsystem (DescriptionParser.cs, DescriptionParser.Statements.cs). Parse reads the statements of a description diagram one by one by their leading keywords and with the shared Svek readers, without regular expressions, links first as PlantUML tries them: declarations with a symbol keyword (component, interface, actor, usecase, node, rectangle, folder, package, frame, cloud, database, card, artifact, storage, file, queue, stack, agent, hexagon, person, boundary, control, entity, collections, label, action, process, circle, and the business forms) or without one ([C], () I, (U), (U)/, :A:, :A:/, a name with as), where the shape of a name overrides the keyword; the three display forms with as; stereotypes and colors; multi-line descriptions in brackets or quotes; containers of the container symbols opened with a brace (anonymous ones included) and nested; ports (reported, inside an element only); links with their decorations, styles, lengths, hints, end labels, and labels; notes on elements, on links, and floating; the page texts, the direction, the scale, and the skin parameters applied to the description theme. Names resolve in the current container, the containers around it, or by a unique name (an ambiguous name is refused); a declaration that changes the symbol of an element is refused as PlantUML refuses it; a name only written in links becomes an actor when the diagram has a use case or an actor, else an interface. Parts it reads but does not draw are reported as plantuml-statement-unsupported; malformed statements and statements of other diagram types are located syntax errors; the nodes, edges, labels, and nesting are bounded by PlantUmlLimits. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionParser.cs, src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionParser.Statements.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionParserTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/description/description-parser.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/description/description-parser.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/description/description-parser.yaml */

        // The parser reads detected, preprocessed statements with the shared readers into the model
        dependency usesPreprocessor from PlantUmlDescriptionParser to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlDescriptionParser to PlantUmlDiagramDetector;
        dependency usesSvekSyntax from PlantUmlDescriptionParser to PlantUmlSvekSyntax;
        dependency usesSvekModel from PlantUmlDescriptionParser to PlantUmlSvekModel;
        dependency usesSvekGeometry from PlantUmlDescriptionParser to PlantUmlSvekGeometry;
        dependency usesSkinParams from PlantUmlDescriptionParser to PlantUmlSkinParams;
        dependency usesSkinTheme from PlantUmlDescriptionParser to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlDescriptionParser to PlantUmlSceneModel;
        dependency usesDescriptionModel from PlantUmlDescriptionParser to PlantUmlDescriptionModel;
        dependency usesDescriptionRenderer from PlantUmlDescriptionParser to PlantUmlDescriptionRenderer;
    }
}