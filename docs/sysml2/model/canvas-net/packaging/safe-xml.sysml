package CanvasNet_SafeXml {
    private import SoftwareItemMetadata::*;

    part def SafeXml {
        doc /* Stateless static class that loads XML from untrusted input with DTD processing prohibited, no external resolver and a caller-supplied character bound, as an XDocument from a stream or a string or as a forward-only XmlReader over a stream. Malformed, DTD-bearing or oversized input surfaces as XmlException for the caller to translate. Thread-safe: stateless. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Packaging/SafeXml.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Packaging/SafeXmlTests.cs");
            designFiles = ("docs/design/canvas-net/packaging/safe-xml.md");
            verificationFiles = ("docs/verification/canvas-net/packaging/safe-xml.md");
            requirementFiles = ("docs/reqstream/canvas-net/packaging/safe-xml.yaml");
        }
    }
}
