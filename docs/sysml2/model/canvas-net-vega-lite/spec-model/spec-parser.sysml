package CanvasNet {
    part def SpecParser {
        doc /* The internal SpecParser (a sealed partial class with static Parse entry points over text or UTF-8 bytes), the immutable tree types (SpecNode, SpecNodeKind, NodePosition, SpecTree), and the SpecMetadata record: a single-pass Utf8JsonReader-based reader with an explicit frame stack that builds the SpecNode tree with lazily computed RFC 6901 pointers and one-based UTF-16 line and column positions for every value and property key, enforces MaxSourceLength (InvalidDataException), MaxJsonDepth, and MaxStringLength (VegaLiteSpecException), applies the duplicate-key policy (last value wins at the position of the first, one vl-duplicate-key warning), and validates the shape of the known properties of views, titles, padding, autosize, mark definitions, channel definitions, scales, and axes with rule tables and generous sets of known property names taken from the Vega-Lite 6 JSON schema (a wrong type or value is a VegaLiteSpecException with the pointer of the value; an unknown key is a vl-unknown-property warning); it rejects a schema naming plain Vega, never walks inline data rows, and visits nested views with an explicit work stack. SpecMetadata reads the schema URL and major version, the title text, the marks, the data references, and the parameter names from a tree. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/SpecModel/SpecParser.cs, src/DemaConsulting.CanvasNet.VegaLite/SpecModel/SpecParser.Validation.cs, src/DemaConsulting.CanvasNet.VegaLite/SpecModel/SpecNode.cs, src/DemaConsulting.CanvasNet.VegaLite/SpecModel/SpecMetadata.cs, src/DemaConsulting.CanvasNet.VegaLite/SpecModel/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/SpecParserTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/spec-model/spec-parser.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/spec-model/spec-parser.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/spec-model/spec-parser.yaml */
    }
}
