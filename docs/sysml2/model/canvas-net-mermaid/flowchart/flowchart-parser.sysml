package CanvasNetMermaid_FlowchartParser {
    private import SoftwareItemMetadata::*;

    part def FlowchartParser {
        doc /* The internal sealed FlowchartParser (a partial class in four files) and the static FlowchartTokens (the jison terminal names, their printed names, and their jison numbers): Parse reads the preprocessed text of a flowchart into a new FlowchartModel. The white space after a closing brace is first removed up to the end of its line, as flowParser.ts does, on the text that stays mapped to the original. The lexer ports the 122 rules and the 21 start conditions of the lexer that jison generates from Mermaid's flow.jison, first match wins, with hand-written matchers in place of the regular expressions and per-run and per-line caches for the rules that look far ahead (the run of non-space characters and its last usable at sign for link ids, the line and the last direction word of each direction for direction statements anywhere on a line, and, through SpaceRunEnd, the end of the run of white space at the position for the link rules, which start with optional white space and are tried at every character of a run that the lexer reads one character at a time between statements, in link texts, and in ellipse labels), so lexing is linear in the length of the text, long runs of white space included; the header keyword consults the first-header flag of the model. Tokenize runs the lexer alone over a mapped text, without the grammar, with a parser and model of its own, and returns every token up to the end of the token stream with the steps of the white-space scans of the link rules (one per scan and one per character it passes), a deterministic measure of their work that the unit tests bound. A recursive-descent parser with one token of lookahead accepts the language of the LALR(1) grammar (header and direction, statements separated by line breaks or semicolons, vertices with every bracket shape and shape data, ampersand chains, links of every stroke, head, and length with text and ids, subgraphs with ids, titles, nesting, and direction statements, style, classDef, class, the class separator, linkStyle, every click form, and the accessibility statements) and calls the model where jison reduces, so DOM ids, edge ids, and subgraph members are Mermaid's. A token the grammar does not accept is a MermaidSyntaxException with code mermaid-syntax-error located in the original text, the expected token names recorded from the failed lookahead checks in jison's order (with the effects of jison's merged states emulated), and a hint; text no rule matches is a located lexical error; subgraphs nested deeper than MaxNestingDepth are refused with mermaid-limit-exceeded. Click actions and tooltips are read and ignored except for the clickable class. An instance parses one text on one thread. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Mermaid/Flowchart/FlowchartParser.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Flowchart/FlowchartParser.Lexer.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Flowchart/FlowchartParser.Statements.cs",
                "src/DemaConsulting.CanvasNet.Mermaid/Flowchart/FlowchartParser.Links.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Flowchart/FlowchartParserTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Flowchart/FlowchartVectorTests.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Flowchart/FlowchartFacts.cs",
                "test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/flowchart-parser.json"
            );
            designFiles = ("docs/design/canvas-net-mermaid/flowchart/flowchart-parser.md");
            verificationFiles = ("docs/verification/canvas-net-mermaid/flowchart/flowchart-parser.md");
            requirementFiles = ("docs/reqstream/canvas-net-mermaid/flowchart/flowchart-parser.yaml");
        }
    }
}
