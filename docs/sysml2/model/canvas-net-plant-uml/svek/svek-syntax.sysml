package CanvasNet {
    part def PlantUmlSvekSyntax {
        doc /* The internal statement readers of the PlantUML Svek subsystem (SvekSyntax.cs, SvekSyntax.Links.cs, SvekSyntax.Notes.cs) that the diagrams PlantUML lays out with Graphviz share: identifiers and names with their shorthands and pseudo-states, aliases, stereotypes, and colors; the arrow grammar of PlantUML's class and description links (every decoration of decors1 and decors2, the body with its length, the direction words, the style in brackets, the middle decoration); link statements with their ends tried from the longest as PlantUML's regular expression backtracks, end labels, roles, qualifiers, weight, color, URL, stereotypes, and label split as PlantUML splits it; every form of note; page texts, direction, scale, hide and show, and the layout skin parameters nodesep, ranksep, and linetype. Hand-written scanners without regular expressions on the input; a malformed recognized statement is a located plantuml-syntax-error. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekSyntax.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekSyntax.Links.cs, src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekSyntax.Notes.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekSyntaxTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/svek/svek-syntax.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/svek/svek-syntax.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/svek/svek-syntax.yaml */

        // The readers read preprocessed statements, colors, skin parameters, and page texts
        dependency usesPreprocessor from PlantUmlSvekSyntax to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlSvekSyntax to PlantUmlDiagramDetector;
        dependency usesSkinParams from PlantUmlSvekSyntax to PlantUmlSkinParams;
        dependency usesSceneModel from PlantUmlSvekSyntax to PlantUmlSceneModel;
        dependency usesSvekModel from PlantUmlSvekSyntax to PlantUmlSvekModel;
    }
}
