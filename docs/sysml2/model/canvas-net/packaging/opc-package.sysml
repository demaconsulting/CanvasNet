package CanvasNet {
    part def OpcPackage {
        doc /* Read-only, size-bounded view of an Open Packaging Conventions ZIP package (such as a .pptx or .vsdx file), with its OpcFormatException. Opens from a stream with a bounded copy, parses [Content_Types].xml and the package-level relationships, resolves a part's content type and the relationships between parts (relative, absolute and dot-segment targets; a target that leaves the package, an unknown relationship and an external target are rejected), and reads a part as hardened XML (through SafeXml, bounded in characters) or as bytes (bounded in decompressed size). Part names are compared ordinally and duplicate entry names are rejected. Not thread-safe: one instance per thread. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Packaging/OpcPackage.cs, src/DemaConsulting.CanvasNet/Packaging/OpcFormatException.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Packaging/OpcPackageTests.cs */
        comment designRef /* Design: docs/design/canvas-net/packaging/opc-package.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/packaging/opc-package.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/packaging/opc-package.yaml */
    }
}
