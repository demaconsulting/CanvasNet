package CanvasNet {
    part def PandocFilterShim {
        doc /* The Lua shim of the Pandoc filter (canvasnet.lua), embedded in the tool and kept in the repository as docs/filters/canvasnet.lua, and the protocol it speaks with the pandoc-filter command. The shim starts the tool only when the document has a code block with a class or a local picture that is not a PNG, JPEG, GIF, or WebP file; it runs CANVASNET_TOOL (split on white space, double quotes grouping) or dotnet canvasnet once with pandoc-filter --json --protocol 1, the filter path, the format, the Pandoc version, the resource path, the output file, and embed-resources, piping the whole document as JSON; it maps a start failure, the JSON error message and hint, or a bare exit code to a canvasnet.lua error, shows the warnings of the metadata key canvasnet-warnings through pandoc.log.warn, and replaces each canvasnet-markdown placeholder Span with its caption parsed as Markdown. The class holds the embedded bytes, accepts protocol 1 (MinProtocol to MaxProtocol) and otherwise raises usage-error naming the --write-shim command or the tool update, and writes the shim byte for byte through a partial file moved into place. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/PandocFilter/PandocFilterShim.cs, src/DemaConsulting.CanvasNet.Tool/PandocFilter/canvasnet.lua, docs/filters/canvasnet.lua */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PandocFilterShimTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/pandoc-filter/pandoc-filter-shim.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/pandoc-filter/pandoc-filter-shim.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/pandoc-filter/pandoc-filter-shim.yaml */
    }
}
