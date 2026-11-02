package CanvasNet {
    part def Packaging {
        doc /* Hardened packaging subsystem for reading untrusted Open Packaging Conventions (ECMA-376 Part 2) packages and untrusted XML: the OpcPackage unit (a read-only, size-bounded view of an OPC ZIP package with content-type and relationship resolution and bounded part reads, with its OpcFormatException) and the SafeXml unit (XML loading with no DTD processing, no external resolution and a character bound). Depends only on the .NET base class library. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Packaging/OpcPackageTests.cs, test/DemaConsulting.CanvasNet.Tests/Packaging/SafeXmlTests.cs */
        comment designRef /* Design: docs/design/canvas-net/packaging.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/packaging.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/packaging.yaml */

        part opcPackage : OpcPackage;
        part safeXml : SafeXml;
    }
}
