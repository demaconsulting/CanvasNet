package CanvasNet {
    part def Help {
        doc /* The subsystem in the DemaConsulting.CanvasNet.Viewer.Help namespace that carries the help about the drawing and file formats as data a host reads programmatically, without the command-line tool: short Markdown cards with YAML front matter, embedded in the library, about what to write in a file, how to make it look good, how files compose, the diagnostics, the limits, what the host controls, and which files a document may read, with tiny examples that render. Every card has tags from a closed vocabulary (the format tags svg, mermaid, plantuml, scad, vega-lite, pdf, pptx, vsdx, and raster, and the scope tags authoring, design, composition, diagnostics, limits, hosting, and file-access, at least one scope tag per card); a host takes a view of the cards with some tags and without others, in which a hidden card is never found, listed, or linked (a link to it is plain text, and the visible cards below a hidden card move up to its nearest visible ancestor or the roots). The cards link each other with neutral links ([[a/b]], [[a/b#example]], [[#example]]) that the library writes as "the card `a/b`" and a host writes in its own words. A host layers its own cards and overlays (extends: <path>) on the library catalog from the embedded resources of its assembly; the canvasnet tool adds its commands, flags, and result documents this way, so the library cards carry no command-line text. The HelpFrontMatter unit parses the front matter of a card file in a strict subset of YAML; the HelpCatalog unit loads, validates, filters, links, extends, and renders the cards. The catalog is immutable and thread-safe. Its constituent units are HelpCatalog and HelpFrontMatter. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Viewer/Help/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpSubsystemTests.cs */
        comment designRef /* Design: docs/design/canvas-net-viewer/help.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-viewer/help.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-viewer/help.yaml */

        part helpCatalog : HelpCatalog;
        part helpFrontMatter : HelpFrontMatter;
    }
}
