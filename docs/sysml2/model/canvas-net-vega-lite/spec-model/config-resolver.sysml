package CanvasNet {
    part def ConfigResolver {
        doc /* The internal static ConfigResolver with the ResolvedConfig, ConfigNode, and ConfigNodeKind types: it builds the effective configuration of a chart as one immutable tree merged from the Vega guide defaults, the Vega-Lite defaults (background, padding, view size of 300 by 300 and step 20, mark, bar, rect, tick, scale, header, and spacing configuration), the config of the specification, and the Config of the caller's render options, in that order of increasing precedence, as a true deep merge in which arrays and scalars are replaced; the default values are ported from Vega and Vega-Lite with attribution (the source file carries the provenance header). The ResolvedConfig gives typed dotted-path accessors and the precedence chains of Vega-Lite for mark properties (style, mark-type configuration, mark configuration, Vega styles, Vega mark defaults), view properties (cell style, view configuration), title properties (config.title, group-title and group-subtitle styles), and styles, plus the rule constants of the point-like marks (reduced default opacity, default step, default stroke width) and the filled-by-default rule. An invalid caller configuration text (not a JSON object, or nested deeper than 64 levels) is an ArgumentException, which the render options already reject when they are created. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.VegaLite/SpecModel/ConfigResolver.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/ConfigResolverTests.cs */
        comment designRef /* Design: docs/design/canvas-net-vega-lite/spec-model/config-resolver.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-vega-lite/spec-model/config-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-vega-lite/spec-model/config-resolver.yaml */
    }
}
