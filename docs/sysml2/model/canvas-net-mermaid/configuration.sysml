package CanvasNet {
    part def MermaidConfiguration {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Configuration namespace that turns the site configuration of the caller (the theme option and the configuration text of MermaidRenderOptions), the frontmatter config, and the init directives of a diagram text into the effective configuration of one measure or render call, and the theme of that configuration into the paint and fonts every element class of a flowchart, a sequence diagram, a class diagram, a state diagram, an entity relationship diagram, a pie chart, or a gantt chart is drawn with, as the pinned Mermaid release computes them. The MermaidConfigResolver unit ports Mermaid's configuration pipeline (initialize, setSiteConfig, processFrontmatter, detectInit, addDirective, updateCurrentConfig, assignWithDepth, lodash merge, and the sanitizers) with Mermaid's global and flowchart defaults, reports secure, unknown, and invalid keys as located warnings (mermaid-config-secure-key, mermaid-config-unknown-key, mermaid-config-invalid) and gives the typed view MermaidConfig. The ThemeResolver unit ports the five built-in themes default, base, dark, forest, and neutral over a JavaScript object model and builds the flowchart, sequence, class, state, ER, pie, and gantt style sheets from the theme variables and themeCSS. The ColorMath unit ports the subset of the khroma color library that the themes derive their colors with. The StyleDeclarations unit reads the CSS subset of the style, classDef, and linkStyle statements and of themeCSS, reporting mermaid-style-ignored and mermaid-theme-css-unsupported. No regular expression runs on configuration text and every loop that a value controls is bounded. The subsystem is internal; it is called by MermaidDocument and read by the Flowchart, Sequence, Class, State, Er, and Rendering subsystems. As of this release, its constituent units are MermaidConfigResolver, ThemeResolver, ColorMath, and StyleDeclarations. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Configuration/ConfigurationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/configuration.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/configuration.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/configuration.yaml */

        part mermaidConfigResolver : MermaidConfigResolver;
        part themeResolver : ThemeResolver;
        part colorMath : ColorMath;
        part styleDeclarations : StyleDeclarations;
    }
}
