package CanvasNet {
    part def VegaLiteSpecModel {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.VegaLite.SpecModel namespace that holds everything the system knows about the specification text, with no data, pixels, file access, or network access: the SpecParser unit reads the UTF-8 JSON in one forward pass into an immutable SpecNode tree with RFC 6901 pointers and one-based line and column (explicit frame stack, so hostile nesting cannot overflow the call stack; limits on source length, nesting depth, and string length), applies the duplicate-key policy (last value wins, one warning), validates the shape of the known properties (a wrong type or value is a VegaLiteSpecException with the pointer; an unknown key is only a warning), and reads the descriptive metadata of the public document properties; the FeatureCatalog unit lists every recognized-but-unsupported construct with a stable vl- identifier and its location and is the single definition of the supported boundary; the ConfigResolver unit merges the Vega guide defaults, the Vega-Lite defaults, the specification's config, and the caller's Config (in that order of precedence) and answers the precedence chains of marks, views, titles, and styles. The subsystem is internal; its entry points are called by VegaLiteDocument and its tree and configuration types are read by every later stage. As of this release, its constituent units are SpecParser, FeatureCatalog, and ConfigResolver. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/ (the unit test classes SpecParserTests, FeatureCatalogTests, and ConfigResolverTests, and VegaLiteDocumentSpecModelTests) */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/spec-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/spec-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/spec-model.yaml */

        part specParser : SpecParser;
        part featureCatalog : FeatureCatalog;
        part configResolver : ConfigResolver;
    }
}
