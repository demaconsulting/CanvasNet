using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The kind of a node of the instantiation tree.
/// </summary>
internal enum ScadInstanceKind
{
    /// <summary>A recognized built-in module (see <see cref="ScadBuiltinModules"/>).</summary>
    BuiltinModule,

    /// <summary>The implicit union of its children (a user module, a <c>for</c> loop or a modifier wrapper).</summary>
    Group,
}

/// <summary>
///     An evaluated argument of an instance.
/// </summary>
/// <param name="Name">The argument name, or <see langword="null"/> for a positional argument.</param>
/// <param name="Value">The evaluated value.</param>
internal readonly record struct ScadInstanceArgument(string? Name, ScadValue Value);

/// <summary>
///     The fragment-count settings in effect at an instance.
/// </summary>
/// <param name="Fn">The <c>$fn</c> value.</param>
/// <param name="Fa">The <c>$fa</c> value.</param>
/// <param name="Fs">The <c>$fs</c> value.</param>
internal readonly record struct ScadFragmentSettings(double Fn, double Fa, double Fs);

/// <summary>
///     A node of the geometry-agnostic instantiation tree produced by evaluation.
/// </summary>
/// <remarks>
///     <para>
///         The tree is the contract between evaluation and any later geometry phase: it carries everything needed to
///         build geometry (names, evaluated arguments, children, modifiers, fragment settings, locations) and nothing
///         about scopes or syntax. Nodes are immutable once evaluation has finished.
///     </para>
///     <list type="bullet">
///         <item><description><c>for</c> produces <c>Group("for")</c>; <c>intersection_for</c> produces the built-in <c>intersection</c> whose children are one <c>Group("intersection_for")</c> per iteration.</description></item>
///         <item><description>A user module call produces <c>Group(moduleName)</c> holding the instances of its body.</description></item>
///         <item><description><c>if</c>, braces, <c>let</c>, <c>echo</c>, <c>assert</c> and <c>children()</c> splice their instances into the parent and have no node.</description></item>
///         <item><description><c>!</c>, <c>#</c> and <c>%</c> are OR-ed onto the instance they modify (several instances are wrapped in <c>Group("group")</c>); <c>*</c> removes the statement before evaluation.</description></item>
///     </list>
/// </remarks>
/// <param name="Kind">The node kind.</param>
/// <param name="Name">The built-in module name; for a group, <c>for</c>, <c>intersection_for</c>, <c>group</c> or the user module name.</param>
/// <param name="Arguments">The evaluated arguments in call order (empty for a group).</param>
/// <param name="Children">The child instances in order.</param>
/// <param name="Modifiers">The modifiers (only <see cref="ScadModifiers.ShowOnly"/>, <see cref="ScadModifiers.Highlight"/> and <see cref="ScadModifiers.Background"/> are ever set).</param>
/// <param name="Fragments">The fragment settings; a named <c>$fn</c>, <c>$fa</c> or <c>$fs</c> argument overrides the dynamic value.</param>
/// <param name="Location">The location of the identifying token (module name or keyword).</param>
internal sealed record ScadInstance(
    ScadInstanceKind Kind,
    string Name,
    IReadOnlyList<ScadInstanceArgument> Arguments,
    IReadOnlyList<ScadInstance> Children,
    ScadModifiers Modifiers,
    ScadFragmentSettings Fragments,
    ScadSourceLocation Location);
