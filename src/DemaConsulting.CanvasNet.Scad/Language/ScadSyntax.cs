namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     The base of every syntax tree node: it carries the location of the token that identifies the construct.
/// </summary>
/// <remarks>
///     Nodes are immutable records (init-only properties, read-only collections) so that a parsed tree can be
///     shared freely between threads and consumers. <see cref="Location"/> follows the rule that it is the token
///     that best identifies the construct (operator, keyword, opening bracket or name) so that later diagnostics
///     point somewhere useful. Record equality compares collections by reference; tests compare trees through a
///     canonical dump instead.
/// </remarks>
internal abstract record ScadNode
{
    /// <summary>
    ///     Gets the location of the token that identifies this node.
    /// </summary>
    public ScadSourceLocation Location { get; init; }
}

/// <summary>
///     The base of statement nodes (everything that can appear in a block, module body or at file scope).
/// </summary>
internal abstract record ScadStatement : ScadNode;

/// <summary>
///     The base of expression nodes.
/// </summary>
internal abstract record ScadExpression : ScadNode;

/// <summary>
///     The root of a parsed source file.
/// </summary>
/// <param name="Statements">The file-scope statements in source order (empty statements produce no node).</param>
internal sealed record ScadProgram(IReadOnlyList<ScadStatement> Statements) : ScadNode;

/// <summary>
///     A formal parameter of a module, function or function literal.
/// </summary>
/// <param name="Name">The parameter name (may start with <c>$</c>).</param>
/// <param name="Default">The default value expression, or null when the parameter has none.</param>
internal sealed record ScadParameter(string Name, ScadExpression? Default) : ScadNode;

/// <summary>
///     An actual argument of a call, instantiation, <c>echo</c> or <c>assert</c>.
/// </summary>
/// <param name="Name">The argument name for a named argument, or null for a positional argument.</param>
/// <param name="Value">The argument value.</param>
internal sealed record ScadArgument(string? Name, ScadExpression Value) : ScadNode;

/// <summary>
///     A <c>name = value</c> binding used by <c>for</c>, <c>let</c> and list comprehensions.
/// </summary>
/// <param name="Name">The bound name.</param>
/// <param name="Value">The bound value expression.</param>
internal sealed record ScadAssignment(string Name, ScadExpression Value) : ScadNode;

/// <summary>
///     The statement modifier characters; they may be combined.
/// </summary>
[Flags]
internal enum ScadModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>The <c>*</c> (disable) modifier.</summary>
    Disable = 1,

    /// <summary>The <c>!</c> (show only) modifier.</summary>
    ShowOnly = 2,

    /// <summary>The <c>#</c> (highlight) modifier.</summary>
    Highlight = 4,

    /// <summary>The <c>%</c> (background) modifier.</summary>
    Background = 8,
}

/// <summary>
///     The unary prefix operators.
/// </summary>
internal enum ScadUnaryOperator
{
    /// <summary>Unary plus.</summary>
    Plus,

    /// <summary>Arithmetic negation.</summary>
    Negate,

    /// <summary>Logical not.</summary>
    Not,
}

/// <summary>
///     The binary operators.
/// </summary>
internal enum ScadBinaryOperator
{
    /// <summary>Addition.</summary>
    Add,

    /// <summary>Subtraction.</summary>
    Subtract,

    /// <summary>Multiplication.</summary>
    Multiply,

    /// <summary>Division.</summary>
    Divide,

    /// <summary>Remainder.</summary>
    Modulo,

    /// <summary>Exponentiation.</summary>
    Power,

    /// <summary>Less than.</summary>
    Less,

    /// <summary>Less than or equal.</summary>
    LessEqual,

    /// <summary>Greater than.</summary>
    Greater,

    /// <summary>Greater than or equal.</summary>
    GreaterEqual,

    /// <summary>Equality.</summary>
    Equal,

    /// <summary>Inequality.</summary>
    NotEqual,

    /// <summary>Logical and.</summary>
    And,

    /// <summary>Logical or.</summary>
    Or,
}
