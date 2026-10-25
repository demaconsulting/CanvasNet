namespace DemaConsulting.CanvasNet.Scad;

// cspell:ignore lexer lexing

/// <summary>
///     The <c>Scad</c> namespace is the front end for the OpenSCAD language: it reads OpenSCAD-language source
///     from a string, a stream or a file and produces a validated document (<c>ScadDocument</c>) that reports
///     file-scope <c>include</c>/<c>use</c> directives (<c>ScadDirective</c>) and the names of file-scope modules
///     and functions, or fails with a <c>ScadSyntaxException</c> carrying a precise <c>ScadSourceLocation</c>. The
///     document can then be evaluated (<c>ScadDocument.Evaluate</c>) under resource limits (<c>ScadLimits</c>) to
///     obtain the program's <c>echo</c> output, warnings (<c>ScadDiagnostic</c>) and unsupported features
///     (<c>ScadUnsupportedFeature</c>), or a <c>ScadEvaluationException</c> with the failing location. The same
///     program can be turned into a Render3D scene (<c>ScadDocument.BuildScene</c>) or drawn onto a surface
///     (<c>ScadDocument.Render</c>) under the options of <c>ScadRenderOptions</c>.
/// </summary>
/// <remarks>
///     <para>
///         Open behavior is configured through <c>ScadOpenOptions</c> (which also defines the sandbox for
///         <c>include</c>/<c>use</c>), evaluation through <c>ScadEvaluationOptions</c> and rendering through
///         <c>ScadRenderOptions</c>. The lexer, the syntax tree, the parser, the evaluator and the geometry builder
///         are internal (the <c>Language</c>, <c>Evaluation</c> and <c>Geometry</c> subsystems); only the document
///         surface is public so that the internals can evolve.
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
