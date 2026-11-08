using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Charges work that is proportional to a number of elements to the evaluation-step budget of the call, one step for every 64
///     elements, carrying the remainder from one charge to the next.
/// </summary>
/// <remarks>
/// <para>
///     <see cref="ExpressionFunctionContext.ChargeSize"/> rounds down, so a loop that charged a few elements per row would pay
///     nothing. The meter accumulates the elements and pays whole blocks of 64, which also polls the cancellation token as the
///     step counter advances. A failure is an <see cref="ExpressionRuntimeException"/> that the transform converts into a located
///     <see cref="VegaLiteRenderException"/>. One meter serves one transform on one thread.
/// </para>
/// <para>
///     <b>Calibration.</b> The default budget of 50,000,000 steps is 3,200,000,000 elements, and the callers weight an element so that it stands for about three nanoseconds of work: the budget then
///     stops every transform family in about ten seconds on a current desktop processor (measured weights: a loess weight evaluation 3, a kernel evaluation of a density 4, an element of a
///     polynomial regression fit 16, an element of a bootstrap 5, an element of a recomputed window frame 20; the row-wise transforms charge one element a row and are bounded by the row limit).
///     A change to a weight is a change to which specifications a default render accepts, so the weights are pinned by the transform tests.
/// </para>
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

        // The sum saturates: a wrapped (negative) pending count would make the work free
        _pending = elements > long.MaxValue - _pending ? long.MaxValue : _pending + elements;
        if (_pending >= ElementsPerStep)
        {
            var whole = _pending - (_pending % ElementsPerStep);
            _pending -= whole;
            _context.ChargeSize(whole);
        }
    }
}
