namespace DemaConsulting.CanvasNet.Scad;

// cspell:ignore lexer lexing

/// <summary>
///     The <c>Scad</c> namespace is the parse-only front end for the OpenSCAD language: it reads
///     OpenSCAD-language source from a string, a stream or a file and produces a validated document
///     (<c>ScadDocument</c>) that reports file-scope <c>include</c>/<c>use</c> directives
///     (<c>ScadDirective</c>) and the names of file-scope modules and functions, or fails with a
///     <c>ScadSyntaxException</c> carrying a precise <c>ScadSourceLocation</c>.
/// </summary>
/// <remarks>
///     <para>
///         Open behavior is configured through <c>ScadOpenOptions</c>. The lexer, the syntax tree and the
///         parser are internal (the <c>Language</c> subsystem); only the document surface is public so that
///         the tree can evolve while evaluation and rendering are added in later phases.
///     </para>
///     <para>
///         Clean-room provenance: this package was written from general knowledge of the language only;
///         no GPL-licensed OpenSCAD source code, grammar, test corpus or manual text was consulted, copied
///         or paraphrased. Exact parity with any other implementation is a non-goal.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
