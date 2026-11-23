package CanvasNetViewer_HelpFrontMatter {
    private import SoftwareItemMetadata::*;

    part def HelpFrontMatter {
        doc /* The internal static class that splits a help card file into its YAML front matter and its Markdown body (the records FrontMatterDocument, with the properties in order, the body, and the line the body starts on, and FrontMatterValue, a scalar, a list, or a map with its line). It reads only the strict subset of YAML the cards use (key: value at column 0, lists of two-space-indented items, list items that are maps of four-space-indented entries, plain, single-quoted, and double-quoted one-line values, blank lines, and full-line comments) without a package dependency, and reports anything else, and a missing opening or closing --- line, as an InvalidOperationException whose message is Help card '<file>', line <n>: <reason>. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Viewer/Help/HelpFrontMatter.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Viewer.Tests/Help/HelpFrontMatterTests.cs");
            designFiles = ("docs/design/canvas-net-viewer/help/help-front-matter.md");
            verificationFiles = ("docs/verification/canvas-net-viewer/help/help-front-matter.md");
            requirementFiles = ("docs/reqstream/canvas-net-viewer/help/help-front-matter.yaml");
        }
    }
}
