namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The kind of a <see cref="ScadValue"/>.
/// </summary>
/// <remarks>
///     <see cref="Undef"/> is the zero value so that <c>default(ScadValue)</c> is <c>undef</c>.
/// </remarks>
internal enum ScadValueKind : byte
{
    /// <summary>The <c>undef</c> value (also the result of every invalid operation).</summary>
    Undef = 0,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A double-precision number.</summary>
    Number,

    /// <summary>A string.</summary>
    String,

    /// <summary>A vector (list) of values.</summary>
    Vector,

    /// <summary>A lazily generated numeric range.</summary>
    Range,

    /// <summary>A function value (a function literal closure).</summary>
    Function,
}
