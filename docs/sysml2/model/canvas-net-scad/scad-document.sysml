package CanvasNet {
    part def ScadDocument {
        doc /* The public API of CanvasNetScad in the DemaConsulting.CanvasNet.Scad namespace: the sealed, immutable, thread-safe ScadDocument with the static Parse(string), Open(string path), and Open(Stream) entry points (UTF-8 with byte-order-mark detection, source limited to 4,194,304 characters, the stream left open and a non-terminating stream bounded) and the SourceName, Directives (file-scope include/use directives in source order, recorded and never resolved), ModuleNames, and FunctionNames (file-scope definitions in source order, duplicates kept) properties; the immutable, validated ScadOpenOptions (BaseDirectory, LibraryPaths, FileResolver, SourceName); the ScadSourceLocation record struct (file, one-based line and column); the ScadSyntaxException (a FormatException carrying the Location of the first syntax error); and the ScadDirective record with its ScadDirectiveKind enumeration. It delegates all lexing and parsing to the internal ScadLanguage subsystem and never opens any file named by a directive. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Scad/ScadDocument.cs, src/DemaConsulting.CanvasNet.Scad/ScadOpenOptions.cs, src/DemaConsulting.CanvasNet.Scad/ScadSourceLocation.cs, src/DemaConsulting.CanvasNet.Scad/ScadSyntaxException.cs, src/DemaConsulting.CanvasNet.Scad/ScadDirective.cs, src/DemaConsulting.CanvasNet.Scad/ScadDirectiveKind.cs, src/DemaConsulting.CanvasNet.Scad/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Scad.Tests/ScadDocumentTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/ScadOpenOptionsTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/ScadSourceLocationTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/ScadSyntaxExceptionTests.cs, test/DemaConsulting.CanvasNet.Scad.Tests/ScadFixtures/ */
        comment designRef /* Design: docs/design/canvas-net-scad/scad-document.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-scad/scad-document.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-scad/scad-document.yaml */
    }
}
