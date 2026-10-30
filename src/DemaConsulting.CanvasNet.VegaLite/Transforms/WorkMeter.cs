using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Charges work that is proportional to a number of elements to the evaluation-step budget of the call, one step for every 64
///     elements, carrying the remainder from one charge to the next.
/// </summary>
/// <remarks>
///     <see cref="ExpressionFunctionContext.ChargeSize"/> rounds down, so a loop that charged a few elements per row would pay
///     nothing. The meter accumulates the elements and pays whole blocks of 64, which also polls the cancellation token as the
///     step counter advances. A failure is an <see cref="ExpressionRuntimeException"/> that the transform converts into a located
///     <see cref="VegaLiteRenderException"/>. One meter serves one transform on one thread.
/// </remarks>
internal sealed class WorkMeter
{
    /// <summary>The elements that one step pays for.</summary>
    private const long ElementsPerStep = 64;

    /// <summary>The evaluation to charge.</summary>
    private readonly ExpressionFunctionContext _context;

    /// <summary>The elements counted but not yet paid for.</summary>
    private long _pending;

    /// <summary>
    ///     Initializes a meter.
    /// </summary>
    /// <param name="context">The evaluation whose budget is charged.</param>
    public WorkMeter(ExpressionFunctionContext context) => _context = context;

    /// <summary>
    ///     Counts elements of work and pays for every complete block of 64.
    /// </summary>
    /// <param name="elements">The number of elements touched.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested at a check point.</exception>
    public void Add(long elements)
    {
        if (elements <= 0)
        {
            return;
        }

        _pending += elements;
        if (_pending >= ElementsPerStep)
        {
            var whole = _pending - (_pending % ElementsPerStep);
            _pending -= whole;
            _context.ChargeSize(whole);
        }
    }
}
