package CanvasNet {
    part def StyleDeclarations {
        doc /* The internal static StyleDeclarations with StyleDeclaration, StyleDeclarationSet, and StyleRule (StyleDeclarations.cs, original work of this repository): the CSS subset of Mermaid's style, classDef, and linkStyle statements, of the inline styles of labels, and of themeCSS. ParseItems reads the property:value items of a statement and ParseBlock a declaration block split at the semicolons outside quotes and parentheses; user text may set only fill, stroke, stroke-width, stroke-dasharray, color, font-weight, font-style, font-size, font-family, and opacity (names in any letter case, an optional !important), and any other property, a declaration without a colon, and an invalid value are reported as mermaid-style-ignored at the statement and left out. StyleDeclarationSet holds one declaration per property after the cascade (a later declaration wins unless the earlier one is important) and reads paints and lengths. ParseSheet removes comments and keeps the rules whose normalized selector is one of the diagram's known selectors; every other selector, at-rule, and nested rule is reported as the unsupported feature mermaid-theme-css-unsupported, and strict mode throws at the first. The value parsers read paints (the core CssColor grammar or none), lengths (px, unitless, em, rem, pt; negative refused), dash arrays, opacities, font weights, and font styles. The class is stateless and linear in the length of the text. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Configuration/StyleDeclarations.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/StyleDeclarationsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/configuration/style-declarations.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/configuration/style-declarations.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/configuration/style-declarations.yaml */
    }
}
