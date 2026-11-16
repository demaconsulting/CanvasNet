package CanvasNet {
    part def PandocAst {
        doc /* The Pandoc document format of the Pandoc filter: reads, walks, and writes a Pandoc document in its JSON form, touching only the elements the filter handles. It reads a document of API version 1.23 (Pandoc 3) or a later 1.x version, up to 4096 levels deep, skipping a byte order mark; finds the Image and CodeBlock elements in the order Pandoc's Lua filter visits them (the metadata first, then the blocks, children before their parent) with an explicit stack; reads them into the supporting records PandocAttr, PandocImage, and PandocCodeBlock; rewrites only the source and key-value attributes of an image; builds the Image, Para, Plain, and Figure elements of a picture and the canvasnet-markdown placeholder Span of a caption; returns the warnings of a run in the metadata key canvasnet-warnings; and writes the document back as UTF-8 without a byte order mark, an unchanged document byte for byte. Input that is not a Pandoc document, an unsupported API version, and a malformed touched element are the error corrupt-input. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/PandocFilter/PandocAst.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PandocAstTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/pandoc-filter/pandoc-ast.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/pandoc-filter/pandoc-ast.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/pandoc-filter/pandoc-ast.yaml */
    }
}
