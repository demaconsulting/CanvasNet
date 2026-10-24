namespace DemaConsulting.CanvasNet.Scad.Language;

// cspell:ignore lexer lexing

/// <summary>
///     The internal <c>Language</c> namespace (the <c>ScadLanguage</c> subsystem) turns source text into an
///     immutable syntax tree: <c>ScadLexer</c> converts characters into <c>ScadToken</c> values and
///     <c>ScadParser</c> builds the <c>ScadProgram</c> tree of statement and expression records.
/// </summary>
/// <remarks>
///     <para>
///         Parsing is fail-fast: the first lexical or grammatical error aborts with a
///         <c>ScadSyntaxException</c> carrying a one-based line and column. Hostile input is bounded by a
///         nesting-depth limit and a source-size limit so that parsing can never overflow the stack.
///     </para>
///     <para>
///         Clean-room provenance: written from general knowledge of the language only; no GPL-licensed
///         OpenSCAD source, grammar, test corpus or manual text was consulted, copied or paraphrased.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
