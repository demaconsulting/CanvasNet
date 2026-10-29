package CanvasNet {
    part def TitleBuilder {
        doc /* The internal TitleBuilder (a stateless sealed class) and the TitleBlock class: Build creates the title and the optional subtitle text items (roles title-text and title-subtitle) from the title of the specification (a string, an array of lines, or an object with text, subtitle, anchor, orient, offset, frame, limit, dx, dy, align, baseline, angle, and the font, color, and line-height properties and their subtitle counterparts), falling back to config.title and then the group-title and group-subtitle styles (13 pixel bold for the title, 12 pixel for the subtitle, orient top, anchor middle, offset 4, subtitle padding 3), splits text at line feeds, rotates left and right titles, assigns the group or bounds frame as Vega-Lite does (a composition anchors at the start), measures both items with the TextMeasurer, charges the scene budget for the title group and each text before creating it, and returns a TitleBlock whose Extent is the room the title needs. TitleBlock.Place positions the title group against the data rectangle or the bounds of the view's content (Vega's title layout) once the axes are measured. A missing or empty title yields no block; an expression title is reported by the FeatureCatalog, not evaluated. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/Guides/TitleBuilder.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TitleBuilderTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/guides/title-builder.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/guides/title-builder.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/guides/title-builder.yaml */
    }
}
