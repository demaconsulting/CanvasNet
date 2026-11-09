package CanvasNet {
    part def SourceScanner {
        doc /* The internal SourceScanner of the Mermaid Syntax subsystem with the ScanMatcher delegate, ScanRule and ScanToken (SourceScanner.cs), the grouped ScanPatterns matcher building blocks and JavaScript character classes (SourceScanner.Patterns.cs), and the grouped TokenCursor one-token lookahead (SourceScanner.Tokens.cs). It is the lexer infrastructure that the diagram parsers build on, with the semantics of the lexers that jison generates for Mermaid's grammars: at each position the first rule of the active start condition that matches wins (not the longest), an action may change the state, the token text or the position (less), a stack of start conditions supports begin, popState and topState, and the end-of-input rule runs once before the scanner returns the end of input repeatedly. Every token's positions are positions of a MappedText, so they locate in the original text. Text that no rule matches is a located mermaid-syntax-error that describes the character found; TokenCursor.Expect refuses a token the grammar does not accept with a located mermaid-syntax-error whose Expected lists the accepted tokens in the grammar's words. A rule set that makes no progress is an InvalidOperationException (a defect of a grammar port). Every rule is a hand-written matcher, linear in what it inspects. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Syntax/SourceScanner.cs, src/DemaConsulting.CanvasNet.Mermaid/Syntax/SourceScanner.Patterns.cs, src/DemaConsulting.CanvasNet.Mermaid/Syntax/SourceScanner.Tokens.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/SourceScannerTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/syntax/source-scanner.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/syntax/source-scanner.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/syntax/source-scanner.yaml */
    }
}
