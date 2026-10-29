namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The kind of an expression construct that the engine recognizes but does not execute.
/// </summary>
internal enum ExpressionConstructKind
{
    /// <summary>A call of a function that is not in the closed function table (reported as <c>vl-expr-function-{name}</c>).</summary>
    Function,

    /// <summary>A reference to view state: a signal, an event or an item (reported as <c>vl-expr-view-state</c>).</summary>
    ViewState,

    /// <summary>A syntactically valid construct whose execution is deferred (reported as <c>vl-expr-construct-{name}</c>).</summary>
    Deferred,
}

/// <summary>
///     One construct of an expression that the engine parses but does not support, evaluated as null.
/// </summary>
/// <remarks>
///     The record is the contract between the parser and the feature catalog: the catalog turns every construct into
///     the stable identifier <see cref="Identifier"/> at the location of the expression text, and the evaluator
///     substitutes null for the construct. Constructs are listed in order of appearance.
/// </remarks>
/// <param name="Kind">The kind of the construct.</param>
/// <param name="Name">The function name, the view-state name, or the name of the deferred construct.</param>
/// <param name="Offset">The zero-based UTF-16 offset of the construct within the expression text.</param>
internal readonly record struct ExpressionConstruct(ExpressionConstructKind Kind, string Name, int Offset)
{
    /// <summary>
    ///     Gets the stable identifier of the construct: <c>vl-expr-function-{name}</c>, <c>vl-expr-view-state</c> or
    ///     <c>vl-expr-construct-{name}</c>.
    /// </summary>
    public string Identifier => Kind switch
    {
        ExpressionConstructKind.Function => "vl-expr-function-" + Name,
        ExpressionConstructKind.ViewState => "vl-expr-view-state",
        _ => "vl-expr-construct-" + Name,
    };
}

/// <summary>
///     One reference from an expression to a named parameter.
/// </summary>
/// <remarks>
///     Any identifier that is not a constant, <c>datum</c>, a function name or a view-state name is a parameter
///     reference. The caller resolves it against the parameters that the specification declares (reporting an unknown
///     name as a located specification error) and uses the names to order parameter evaluation.
/// </remarks>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Offset">The zero-based UTF-16 offset of the first use within the expression text.</param>
internal readonly record struct ExpressionReference(string Name, int Offset);

/// <summary>
///     The exception that functions and the evaluator use to signal a runtime failure without a location.
/// </summary>
/// <remarks>
///     The evaluator converts it into a <see cref="VegaLiteRenderException"/> that carries the location of the
///     expression and the offset of the failing construct; it never reaches a public caller. Cancellation is not
///     reported with this type.
/// </remarks>
internal sealed class ExpressionRuntimeException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionRuntimeException"/> class.
    /// </summary>
    /// <param name="description">The description of the failure, without a location.</param>
    public ExpressionRuntimeException(string description)
        : base(description)
    {
    }
}
