package CanvasNet {
    part def SafeXml {
        doc /* Stateless static class that loads XML from untrusted input with DTD processing prohibited, no external resolver and a caller-supplied character bound, as an XDocument from a stream or a string or as a forward-only XmlReader over a stream. Malformed, DTD-bearing or oversized input surfaces as XmlException for the caller to translate. Thread-safe: stateless. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Packaging/SafeXml.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Packaging/SafeXmlTests.cs */
        comment designRef /* Design: docs/design/canvas-net/packaging/safe-xml.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/packaging/safe-xml.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/packaging/safe-xml.yaml */
    }
}
