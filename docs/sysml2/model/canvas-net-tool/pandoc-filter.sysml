package CanvasNet {
    part def PandocFilter {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Tool.PandocFilter namespace that draws the pictures of a Pandoc document in one process, for the Lua shim docs/filters/canvasnet.lua, which sends the whole document to the pandoc-filter command once per Pandoc run and applies what comes back. It is a literal port of the former Lua filter (version 16): the pictures of OpenSCAD models, Vega-Lite charts, Mermaid diagrams, and PDF, PPTX, and VSDX pages become PNG pictures in the cache folder generated/canvasnet beside the folder of the shim, an SVG drawing that links one becomes its flattened copy, a Mermaid code block becomes a figure or a paragraph, and every other element is unchanged. PandocAst reads and writes the JSON document; PictureAttributes reads the attributes of a picture; FilterPaths finds its source; DependencyHasher hashes every file it reads (64 levels, 512 files); FilterSettings holds the inputs of the run and the CANVASNET_* variables; PictureCache names the pictures by a SHA-1 key (version 17) and writes them atomically; PandocFilterEngine plans, draws up to a number of jobs at once through ToolPictureDrawer (RenderCommand.Execute and FlattenCommand.Execute in-process), and applies, always reporting the failure of the first picture; PandocFilterShim is the embedded shim and its protocol (1). A mistake in a picture is the error pandoc-filter-error. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PandocFilterSubsystemTests.cs */
        comment testSupportRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/FilterTestSupport.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/pandoc-filter.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/pandoc-filter.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/pandoc-filter.yaml */

        part pandocAst : PandocAst;
        part filterPaths : FilterPaths;
        part pictureAttributes : PictureAttributes;
        part dependencyHasher : DependencyHasher;
        part filterSettings : FilterSettings;
        part pictureCache : PictureCache;
        part pandocFilterEngine : PandocFilterEngine;
        part pandocFilterShim : PandocFilterShim;

        // The engine runs the filter with every other unit but the shim and draws with the commands; the other units
        // share the path rules, the hasher recognizes the kinds of linked pictures, and the cache and the shim reuse
        // the settings' environment and the cache's atomic file helpers
        dependency engineUsesAst from pandocFilterEngine to pandocAst;
        dependency engineUsesAttributes from pandocFilterEngine to pictureAttributes;
        dependency engineUsesPaths from pandocFilterEngine to filterPaths;
        dependency engineUsesHasher from pandocFilterEngine to dependencyHasher;
        dependency engineUsesSettings from pandocFilterEngine to filterSettings;
        dependency engineUsesCache from pandocFilterEngine to pictureCache;
        dependency hasherUsesPaths from dependencyHasher to filterPaths;
        dependency hasherUsesAttributes from dependencyHasher to pictureAttributes;
        dependency attributesUsesPaths from pictureAttributes to filterPaths;
        dependency settingsUsesPaths from filterSettings to filterPaths;
        dependency cacheUsesPaths from pictureCache to filterPaths;
        dependency cacheUsesSettings from pictureCache to filterSettings;
        dependency shimUsesCache from pandocFilterShim to pictureCache;
        dependency engineUsesRenderCommand from pandocFilterEngine to RenderCommand;
        dependency engineUsesFlattenCommand from pandocFilterEngine to FlattenCommand;
        dependency usesImaging from PandocFilter to Imaging;
    }
}
