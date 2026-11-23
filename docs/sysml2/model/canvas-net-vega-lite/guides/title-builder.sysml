package CanvasNetVegaLite_TitleBuilder {
    private import SoftwareItemMetadata::*;

    part def TitleBuilder {
        doc /* The internal TitleBuilder (a stateless sealed partial class) and the TitleBlock class: Build creates the title and the optional subtitle text items (roles title-text and title-subtitle) from the title of the specification (a string, an array of lines, or an object with text, subtitle, anchor, orient, offset, frame, limit, dx, dy, align, baseline, angle, and the font, color, and line-height properties and their subtitle counterparts), falling back to config.title and then the group-title and group-subtitle styles (13 pixel bold for the title, 12 pixel for the subtitle, orient top, anchor middle, offset 4, subtitle padding 3), splits text at line feeds, rotates left and right titles, assigns the group or bounds frame as Vega-Lite does (a composition anchors at the start), measures both items with the TextMeasurer, charges the scene budget for the title group and each text before creating it, and returns a TitleBlock whose Extent is the room the title needs. TitleBlock.Place positions the title group against the data rectangle or the bounds of the view's content (Vega's title layout) once the axes are measured. A missing or empty title yields no block; an expression title is reported by the FeatureCatalog, not evaluated. Phase 11 adds the headers of a facet: HeaderSettings resolves the header properties of a facet channel over the header of the field definition, config.headerRow, headerColumn or headerFacet, and config.header (Vega-Lite's getHeaderProperty; labels flag, title inclusion, header kind and side of an orient, title band, and the formatter that turns the value of a cell into its label); BuildHeaderBlock builds a label (guide-label, 10 pixel) or a channel title (guide-title, 11 pixel bold) with the label and title properties of the header and the alignment and baseline that an angle implies; BuildHeader builds the header, footer or title group (a HeaderGroup of the CompositionLayout) holding that block and the shared axes built in the header, with the bounds of both. A chart title's block has a position text, so its placed group carries the title position tag; a header block (facet headers) reports no position. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.VegaLite/Guides/TitleBuilder.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Guides/TitleBuilder.Header.cs",
                "src/DemaConsulting.CanvasNet.VegaLite/Guides/HeaderSettings.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TitleBuilderTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TitleBuilderHeaderTests.cs",
                "test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/HeaderSettingsTests.cs"
            );
            designFiles = ("docs/design/canvas-net-vega-lite/guides/title-builder.md");
            verificationFiles = ("docs/verification/canvas-net-vega-lite/guides/title-builder.md");
            requirementFiles = ("docs/reqstream/canvas-net-vega-lite/guides/title-builder.yaml");
        }
    }
}
