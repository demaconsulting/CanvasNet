package CanvasNetViewer_HelpCatalog {
    private import SoftwareItemMetadata::*;
    private import CanvasNetViewer_HelpFrontMatter::*;

    part def HelpCatalog {
        doc /* The catalog of the help cards: the public sealed class HelpCatalog with its public types HelpCard (an immutable card: Id, Name, ParentId, Title, Summary, Tags, Aliases, Markdown, ChildIds, SeeAlsoIds, Examples), HelpExample (Name, Kind source, snippet, or command, Language, FileName, Description, Content), HelpLink (the target CardId, ExampleName, and ExampleCardId of a visible link a host writes), and HelpTags (the closed tag vocabulary as constants, FormatTags, ScopeTags, and All). HelpCatalog.Default is the library catalog, loaded once from the embedded resources DemaConsulting.CanvasNet.Viewer.Help/<path>.md and DemaConsulting.CanvasNet.Viewer.Help.Examples.<root>-<file> through a thread-safe lazy value; its sixteen roots are scad, vega-lite, mermaid, plantuml, mathml, svg, pdf, pptx, vsdx, raster, style, composition, positions, errors, limits, and files. Loading checks the keys, tags (each from HelpTags.All, none repeated, at least one scope tag), aliases, examples (a source or snippet with a file, or a command with a content), the tree (the roots, the children equal the files of a folder, related cards, unique names, aliases, and example names), replaces the {{name}} tokens from the constants of the code (Surface.MaxDimension, SourceLoader.MaxDpi, the ImageWriter qualities, the limits of the Scad, VegaLite, Mermaid, PlantUml, and MathMl libraries, ImageFragment, and the Positions limits), and resolves the links of every body outside code; every problem is an InvalidOperationException naming the file and the line. Find resolves a path of names or aliases ignoring case through hidden cards and returns null for a hidden or unknown card; Filter(include, exclude) gives a view in which a card is visible by its own tags, the roots and children are the nearest visible cards, related cards drop hidden targets, and a link to a hidden card is its title (an example link whose card is hidden but whose owner is visible names the owner); FormatMarkdown writes the visible links through a host callback; Extend layers the card files of a host assembly (overlays with extends: <path> appended to the card at that path, which may replace its title and summary; other cards new roots) with extra tokens, never on a filtered view and never changing the catalog it extends; RenderText, RenderJson, RenderIndexText, and RenderIndexJson write a card or the visible tree deterministically, the text wrapped at 78 columns. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpCatalog.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpCatalog.Load.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpCatalog.Links.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpCatalog.Render.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpCard.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpExample.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpLink.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/HelpTags.cs",
                "src/DemaConsulting.CanvasNet.Viewer/Help/Cards/",
                "src/DemaConsulting.CanvasNet.Viewer/Help/Examples/"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Content.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Library.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Guidance.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Positions.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Triggers.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.Bindings.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCatalogTests.MathMl.cs",
                "test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpCardText.cs"
            );
            designFiles = ("docs/design/canvas-net-viewer/help/help-catalog.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/help/help-catalog.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/help/help-catalog.yaml");
        }
        comment /* src/DemaConsulting.CanvasNet.Viewer/Help/Cards/: the embedded library cards */
        comment /* src/DemaConsulting.CanvasNet.Viewer/Help/Examples/: the embedded examples */

        dependency usesFrontMatter from HelpCatalog to HelpFrontMatter;
    }
}
