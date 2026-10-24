package CanvasNet {
    part def CanvasNetScadSystem {
        doc /* CanvasNetScad is a clean-room front end for the OpenSCAD language, delivered incrementally as the separate DemaConsulting.CanvasNet.Scad NuGet package (not yet packable until Phase 6). Phase 4 is parse-only: the public ScadDocument unit parses OpenSCAD-language source from a string, a stream, or a file path and reports the first syntax error as a ScadSyntaxException with file, line, and column (fail-fast, no recovery), or, on success, the file-scope include/use directives (recorded, never resolved), module names, and function names; its ScadOpenOptions, ScadSourceLocation, ScadSyntaxException, ScadDirective, and ScadDirectiveKind types are the whole public surface. The internal ScadLanguage subsystem contains the ScadLexer unit (tokens with one-based locations, strict numbers, strings with escapes, identifiers, keywords, operators, comments, and include/use directive tokens) and the ScadParser unit (recursive-descent statements and precedence-climbing expressions into an immutable, name-agnostic syntax tree, with a 256-level nesting limit enforced both while parsing and on the finished tree, and a 4,194,304-character source limit). Evaluation, geometry generation, rendering, include/use resolution, packaging, hull, Minkowski, text, and file import remain explicitly deferred to later phases - see docs/design/canvas-net-scad.md's Design Constraints (Supported And Deferred Features, Deliberate Strictness And Deviations) and Implementation Phase Plan. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/ScadSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-scad.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad.yaml */
        comment /* Clean-Room Provenance: OpenSCAD is GPL-licensed and its manual is CC BY-SA. CanvasNetScad is an original implementation written from general language-design knowledge; no OpenSCAD source, grammar, manual text, example, test corpus, or library was consulted or copied - see the Clean-Room Provenance constraint in docs/design/canvas-net-scad.md. */
        comment /* CanvasNetScad references the sibling CanvasNet system (its Canvas subsystem, for the future Surface and Rgba32 output) and the sibling CanvasNetRender3D system (whose Scene the later phases build from evaluated geometry) through ProjectReferences in its csproj - modeled below as non-owning dependency relationships, since both systems live in this same git repository (each distributed as its own NuGet package) rather than being third-party (OTS) or Shared Package dependencies. In Phase 4 no code uses any type from either system: the references are architectural scaffolding, consumed from Phase 6, so that the project graph need not be restructured later. All lexing and parsing is its own code on the BCL. CanvasNetScad must never reference CanvasNetSvg, CanvasNetPdf, CanvasNetPptx, CanvasNetVsdx, or CanvasNetCharts - see the CanvasNetScad System Design document's Dependencies section. */

        part scadDocument : ScadDocument;
        part scadLanguage : ScadLanguage;

        dependency usesCanvas from CanvasNetScadSystem to Canvas;
        dependency usesRender3D from CanvasNetScadSystem to CanvasNetRender3DSystem;
    }
}
