package CanvasNet_Path {
    private import SoftwareItemMetadata::*;

    part def Path {
        doc /* Immutable vector path (ordered independent Subpath contours) and its mutable, fluent PathBuilder, including PathBuilder.AppendPath (append another path's subpaths, optionally transformed; the implementation of Path.Transform). Also covers the supporting Subpath, PathCommand, and PathCommandType data types inline; none has independent behavior of its own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Geometry/Path.cs",
                "src/DemaConsulting.CanvasNet/Geometry/PathBuilder.cs",
                "src/DemaConsulting.CanvasNet/Geometry/Subpath.cs",
                "src/DemaConsulting.CanvasNet/Geometry/PathCommand.cs",
                "src/DemaConsulting.CanvasNet/Geometry/PathCommandType.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Geometry/PathBuilderTests.cs");
            designFiles = ("docs/design/canvas-net/geometry/path.md");
            verificationFiles = ("docs/verification/canvas-net/geometry/path.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry/path.yaml");
        }
    }
}
