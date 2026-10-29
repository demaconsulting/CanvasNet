namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The <c>Expressions</c> namespace is the expression engine of the Vega-Lite renderer: a bounded parser that
///     compiles the text of a Vega expression (<c>ExpressionParser</c>), a culture-invariant evaluator with JavaScript
///     coercion semantics (<c>ExpressionEvaluator</c>) and the closed table of functions that expressions may call
///     (<c>ExpressionFunctions</c>).
/// </summary>
/// <remarks>
///     <para>
///         The engine is a closed world. The language is a subset of JavaScript that has literals, <c>datum</c> access,
///         the arithmetic, comparison, logical and conditional operators and calls of whitelisted pure functions;
///         there is no reflection, <c>eval</c>, assignment, <c>new</c>, function expression, file or network access,
///         random number source or clock. Date and time, formatting, color, geographic, scale, statistical and data-flow
///         functions are deferred to later phases; calls of them, view state (<c>signal</c>, <c>event</c>,
///         <c>item()</c>) and the bitwise operators are parsed and collected so that the feature catalog can report them
///         with stable identifiers, and they evaluate to null.
///     </para>
///     <para>
///         Resource use is bounded: the text length and the nesting depth are checked by the parser, the evaluation runs
///         a non-recursive program under a step budget that also pays for work proportional to the size of strings
///         and arrays, strings are bounded by the string limit, regular expressions use the linear-time engine where
///         possible and a short timeout otherwise, and cancellation is checked at least every 1,024 steps. Parse
///         errors are <c>VegaLiteSpecException</c>s located at the expression and failures that depend on data are
///         <c>VegaLiteRenderException</c>s. The namespace is internal to the package; parsed expressions are immutable
///         and shareable, and an evaluator belongs to one call on one thread. All results are independent of the
///         current culture and time zone.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
