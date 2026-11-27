package CanvasNetViewer_HelpSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetViewer_CanvasNetHelp::*;

    part def Help {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Viewer.Help namespace that carries the help about the drawing and file formats as data a host reads programmatically, without the command-line tool: short Markdown cards with YAML front matter, embedded in the library next to their tiny example files, about what to write in a file, how to make it look good, how files compose, the diagnostics, the limits, what the host controls, and which files a document may read. Every card has tags from a closed vocabulary (the format tags svg, mermaid, plantuml, mathml, scad, vega-lite, pdf, pptx, vsdx, and raster, and the scope tags authoring, design, composition, diagnostics, limits, hosting, and file-access, at least one scope tag per card); a host takes a view of the cards with some tags and without others, in which a hidden card is never found, listed, or linked. The cards link each other with neutral links ([[a/b]], [[a/b#example]], [[#example]]) that the library writes as "the card `a/b`" and a host writes in its own words. A host layers its own cards and overlays (extends: <path>) on the library cards in a source of its own that imports the library source; the canvasnet tool adds its commands, flags, and result documents this way, so the library cards carry no command-line text. The CanvasNetHelp unit names the library source, fills in the live limits, and builds the library catalog with the HelpTags vocabulary; reading, validating, filtering, linking, extending, and rendering the cards is the OTS library DemaConsulting.HelpCards. The catalog is immutable and thread-safe. Its constituent unit is CanvasNetHelp. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpSubsystemTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/help.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/help.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/help.yaml");
        }
        comment /* The Help subsystem uses the OTS library DemaConsulting.HelpCards (a NuGet package dependency of the Viewer package, part of its public API), documented in docs/design/ots/help-cards.md. */

        part canvasNetHelp : CanvasNetHelp;
    }
}
