package CanvasNet_ImagePosition {
    private import SoftwareItemMetadata::*;

    part def ImagePosition {
        doc /* The neutral model of one feature of a rendered picture: the immutable, thread-safe sealed record ImagePosition with Kind (a lowercase word [a-z][a-z0-9-]* such as node, edge, mark, or axis), Id (the source id or a deterministic synthetic id; may be empty, never null), and the optional facts Text, Line (1-based), View (the canonical view path of a chart, "" for a single view), ViewName, Mark (the mark type of a chart mark), From, To, and Index (0-based among the features of the same kind with the same ends), Values (a copy of the name and PositionValue pairs, sorted by name in ordinal order, with unique non-empty names), Data (the scalar data fields of the source row, copied and sorted the same way, at most MaxDataFields (32)), ChannelNames (the frozen set of names a selector compares with Values, kept as given; null by default), Box (a PositionBox), and Pixel (a PositionPoint); Box and Pixel are null only when a later image step could not follow them. The init setters validate every fact (ArgumentNullException, ArgumentException, ArgumentOutOfRangeException) and TryGetValue and TryGetData look a value or a data field up by its name. PositionValue is a typed scalar (the PositionValueKind Null, Boolean, Number - always finite, negative zero stored as zero - and String) compared with typed equality (the number 3 equals 3.0 but not the text "3"; text compared by ordinal comparison), created from the scalars of ImageParameterValues by TryFromObject. PositionBox (the geometry bounds without stroke, with Center, Corners clockwise from the top-left corner, Enclose of a set of points, and IsValid) and PositionPoint (with IsValid) are immutable record structs in the continuous pixel coordinates of the picture (pixel i spans [i, i + 1), y grows downward). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Positions/ImagePosition.cs",
                "src/DemaConsulting.CanvasNet/Positions/PositionBox.cs",
                "src/DemaConsulting.CanvasNet/Positions/PositionPoint.cs",
                "src/DemaConsulting.CanvasNet/Positions/PositionValue.cs",
                "src/DemaConsulting.CanvasNet/Positions/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Positions/ImagePositionTests.cs");
            designFiles = ("docs/design/canvas-net/positions/image-position.md");
            verificationFiles = ("docs/verification/canvas-net/positions/image-position.md");
            requirementFiles = ("docs/reqstream/canvas-net/positions/image-position.yaml");
        }
    }
}
