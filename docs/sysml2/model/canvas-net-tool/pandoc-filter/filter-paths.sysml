package CanvasNetTool_FilterPaths {
    private import SoftwareItemMetadata::*;

    part def FilterPaths {
        doc /* The path rules of the Pandoc filter: a stateless, thread-safe literal port of the path helpers of the Lua filter (version 16) and of the pandoc.path functions it used (Haskell's System.FilePath for the platform: both slash and backslash separate on Windows, where a drive letter is upper-cased and only a drive with a separator or a share is absolute), so that a picture's source is found, and the files of its dependencies are named in the cache key, exactly as before. It splits, joins, normalizes, and collapses paths (a rooted path stays at its root, a leading .. of a relative path is kept), gives the relative path and label of a dependency with forward slashes, reads hostless file: URIs and decides remote URLs by the tool's own rules, resolves data URLs and picture links relative to the folder of the file that holds them, checks containment by whole segments, finds a source in the resource path as written or percent-decoded, and provides the Lua text helpers (white space of %s, ASCII lower case, byte order, percent decoding). Only FileExists and ReadFile touch the file system, and they report a failure as false or null. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Tool/PandocFilter/FilterPaths.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/FilterPathsTests.cs");
            designFiles = ("docs/design/canvas-net-tool/pandoc-filter/filter-paths.md");
            verificationFiles = ("docs/verification/canvas-net-tool/pandoc-filter/filter-paths.md");
            requirementFiles = ("docs/reqstream/canvas-net-tool/pandoc-filter/filter-paths.yaml");
        }
    }
}
