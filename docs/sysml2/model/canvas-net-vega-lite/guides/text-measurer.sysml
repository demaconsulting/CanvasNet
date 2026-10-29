package CanvasNet {
    part def TextMeasurer {
        doc /* The internal TextMeasurer (a sealed, thread-safe class) with the TextFont, FontSource, ResolvedFont, TextExtent, and FontMetrics types: it resolves a CSS family list, weight, and style to a face (the first family that resolves wins; sans-serif, system-ui, and the common sans families resolve to the bundled Liberation Sans, serif families to Liberation Serif, and monospace families to Liberation Mono without consulting the installed fonts; any other family is looked up by name in the additional directories, scanned lazily to depth 4 with at most 4,096 files, reparse points skipped, and then, only when UseSystemFonts is set, in the installed fonts; Liberation Sans is the last resort; a weight of 600 or more selects bold, italic or oblique the italic face), caches the faces per instance, and measures advance widths with kerning through the core TextRenderer. It also provides Vega's text rules: line height (font size plus 2), baseline offsets, trimming and binary-search truncation with an ellipsis, the extent of a block of lines, and the bounds of a SceneText (the 80 percent offset, alignment, and rotation about the position). Unusable font files are skipped, never thrown. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Guides/TextMeasurer.cs, src/DemaConsulting.CanvasNet.VegaLite/Guides/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TextMeasurerTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/guides/text-measurer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/guides/text-measurer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/guides/text-measurer.yaml */
    }
}
