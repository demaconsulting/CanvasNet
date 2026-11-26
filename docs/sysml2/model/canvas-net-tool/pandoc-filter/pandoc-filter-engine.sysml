package CanvasNetTool_PandocFilterEngine {
    private import SoftwareItemMetadata::*;

    part def PandocFilterEngine {
        doc /* The engine of the Pandoc filter: runs the logic of the former Lua filter (version 16) once for a whole Pandoc JSON document. It plans every picture sequentially in the order Pandoc's Lua filter visited the elements (every Image, those in the metadata first, then every CodeBlock): it checks the attributes, locates the source in the resource path (a missing source of a kind it draws is an error, a missing SVG drawing is left to Pandoc), gives an equation without a size, width, or height the kept attribute width of its natural width (a probe that measures it without drawing it, on every run, not part of the key), hashes the dependencies, computes the cache key, and uses a cached picture, a .unsupported marker of a Mermaid or PlantUML diagram the tool does not draw, or a .plain marker of an SVG drawing with nothing to flatten; a picture that misses the cache becomes a job, and a later picture with the same key follows that job, so it is drawn once. It draws the jobs, up to the number of jobs at once, through the IPictureDrawer seam (ToolPictureDrawer calls RenderCommand.Execute and FlattenCommand.Execute in-process and leaves out the notes for information only, such as vega-lite-info), then rewrites the elements in document order: an image points at the cached picture or flattened copy with its kept attributes, and a Mermaid, PlantUML, MathML, or Vega-Lite code block, or an OpenSCAD code block marked draw (written to the cache as a .vl.json or .scad file, with the classes that made it a picture dropped; a plain openscad block is left as written), becomes a Figure with a canvasnet-markdown placeholder Span for its caption, or a Para. A Vega-Lite or OpenSCAD code block that names a local file (a data file, a picture, an include, or a use) stops the build before anything is written with pandoc-filter-error and a located message that says to link the source as a picture, since a block has no folder; the size-limit hint of a block names its classes, for example {.openscad .draw scale=0.5}. The warnings are returned in the metadata key canvasnet-warnings. A failure (drawing or flattening a picture failed, a denial in a restrictive file-access mode, or a planning error) is the one of the lowest position whatever the number of jobs, and the warnings of the earlier pictures are exposed as WarningsBeforeFailure; each job draws into a temporary file and the pictures and markers are committed into the cache strictly in document order up to the first failure, so the output and the cache files are the same for one job or many, also after a failure. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/PandocFilterEngine.cs",
                "src/DemaConsulting.CanvasNet.Tool/PandocFilter/PandocFilterEngine.Drawing.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PandocFilterEngineTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/pandoc-filter-engine.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/pandoc-filter-engine.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/pandoc-filter-engine.yaml");
        }
    }
}
