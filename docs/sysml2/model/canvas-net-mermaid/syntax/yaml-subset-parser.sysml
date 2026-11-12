package CanvasNet {
    part def YamlSubsetParser {
        doc /* The internal YamlSubsetParser of the Mermaid Syntax subsystem (a partial class in YamlSubsetParser.cs and YamlSubsetParser.Values.cs) and its value tree YamlNode, YamlScalar, YamlSequence, YamlMapping and YamlEntry. Parse reads the YAML that Mermaid reads with js-yaml's load and the JSON schema - the frontmatter and the shape data of nodes and edges - as a port of the js-yaml loader that Mermaid bundles: block and flow collections, plain, quoted and block scalars with folding and chomping, comments, duplicate-key detection, at most one document, and the implicit null, boolean, integer and float types of the JSON schema. Every node carries the location of its first character in the original text, its JavaScript truthiness and its JavaScript String conversion (numbers in ECMAScript's shortest form). Invalid YAML is a located mermaid-yaml-invalid with js-yaml's description; anchors, aliases, tags, explicit keys and directives are refused with mermaid-yaml-unsupported (a merge key is an ordinary key); collections nested deeper than MermaidLimits.MaxYamlDepth are a located mermaid-limit-exceeded MermaidSyntaxException at open time, which bounds the recursion so that no input can overflow the stack. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Syntax/YamlSubsetParser.cs, src/DemaConsulting.CanvasNet.Mermaid/Syntax/YamlSubsetParser.Values.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/YamlSubsetParserTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax/yaml-subset-parser.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax/yaml-subset-parser.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax/yaml-subset-parser.yaml */
    }
}
