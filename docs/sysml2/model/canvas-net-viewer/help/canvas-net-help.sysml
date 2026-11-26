package CanvasNetViewer_CanvasNetHelp {
    private import SoftwareItemMetadata::*;

    part def CanvasNetHelp {
        doc /* The public entry point of the library help cards: the public static class CanvasNetHelp with the public static class HelpTags (the closed tag vocabulary as constants, FormatTags, ScopeTags, and All). SourceName is canvasnet-viewer, the name of the _source.md file of the cards; RootIds are the sixteen roots scad, vega-lite, mermaid, plantuml, mathml, svg, pdf, pptx, vsdx, raster, style, composition, positions, errors, limits, and files in that order; Source is the HelpCards source of the cards embedded in the Viewer assembly (the resources DemaConsulting.CanvasNet.Viewer.Help/<path> of the folder Help/Cards, the cards and their example files next to them) with the internal Tokens, the 36 live limits of the code (Surface.MaxDimension, SourceLoader.MaxDpi, the ImageWriter qualities, the limits of the Scad, VegaLite, Mermaid, PlantUml, and MathMl libraries, ImageFragment, and the Positions limits) formatted with the invariant culture; Configure adds Source to a HelpCards catalog builder with the vocabulary of HelpTags (at least one scope tag per card); and Catalog is the library catalog built once through a thread-safe lazy value with the roots expected. The reading, validation, filtering, linking, and rendering of the cards are the OTS library DemaConsulting.HelpCards; CanvasNet's stricter rules (budgets, column limits, folders that mirror the tree, unique example names, no command-line text) are applied by the unit tests through the HelpCards validator. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Viewer/Help/CanvasNetHelp.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpTags.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/Cards/"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Content.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Library.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Guidance.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Positions.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Triggers.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.Bindings.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/CanvasNetHelpTests.MathMl.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCardText.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/NoCommandLineTextRule.cs"
            );
            designFiles = ("docs/design/canvas-net-viewer/help/canvas-net-help.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/help/canvas-net-help.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/help/canvas-net-help.yaml");
        }
        comment /* src/DemaConsulting.CanvasNet.Viewer/Help/Cards/: the embedded library cards, their example files, and _source.md */
    }
}
