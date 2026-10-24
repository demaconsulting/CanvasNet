package CanvasNet {
    part def ScadLanguage {
        doc /* The internal language front end in the DemaConsulting.CanvasNet.Scad.Language namespace that turns OpenSCAD-language source text into an immutable syntax tree: the ScadLexer tokenizes the whole source first (numbers, strings with escapes, identifiers, keywords, operators, comments, include/use directive tokens, one-based locations), then the ScadParser (recursive-descent statements and precedence-climbing expressions, name-agnostic) builds the tree, so a lexical error anywhere in the file is reported before any grammatical error. Parsing is fail-fast with a single ScadSyntaxException, nesting is limited to 256 levels by a recursion counter with a stack-space guard and by an iterative verification of the finished tree height, and the syntax tree is internal. As of this release, its constituent units are ScadLexer and ScadParser. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/Language/ScadLanguageTests.cs */
        comment designRef /* Design: docs/design/canvas-net-scad/scad-language.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad/scad-language.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad/scad-language.yaml */

        part scadLexer : ScadLexer;
        part scadParser : ScadParser;
    }
}
