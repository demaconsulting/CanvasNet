namespace DemaConsulting.CanvasNet.Scad.Evaluation;

// cspell:ignore openscad

/// <summary>
///     The internal <c>Evaluation</c> namespace (the <c>ScadEvaluation</c> subsystem) turns a parsed OpenSCAD-language
///     program into values, diagnostics and a geometry-agnostic instantiation tree: <c>ScadValue</c> is the value
///     model, <c>ScadBuiltinFunctions</c> the built-in function library, <c>ScadFileResolver</c> the sandboxed
///     <c>include</c>/<c>use</c> loader and <c>ScadEvaluator</c> the interpreter.
/// </summary>
/// <remarks>
///     <para>
///         Evaluation is deterministic (invariant culture, its own number formatter and random generator) and
///         bounded: every resource is governed by <c>ScadLimits</c> so hostile input fails closed with a typed
///         exception instead of exhausting memory, time or the stack.
///     </para>
///     <para>
///         Clean-room provenance: written from general knowledge of the language only; no GPL-licensed
///         OpenSCAD source, grammar, test corpus or manual text was consulted, copied or paraphrased. The
///         semantics are CanvasNet's own dialect, pinned by in-repository tests; parity with other
///         implementations is a non-goal.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
