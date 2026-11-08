using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The resource limits that bound reading and rendering a Vega-Lite specification.
/// </summary>
/// <remarks>
///     <para>
///         Specifications and their data are untrusted input, so every resource the pipeline can consume is limited
///         and exceeding a limit fails closed (with an <see cref="InvalidDataException"/> for oversized source, a
///         <see cref="VegaLiteSpecException"/> for structural limits or a <see cref="VegaLiteRenderException"/> that
///         carries the location of the construct for render-time limits) rather than exhausting memory, time or the
///         call stack. The defaults are generous enough for realistic charts and small enough to stop a runaway
///         specification within seconds (the per-stage costs are calibrated so that each limit stops its stage in about ten seconds on
///         a current desktop processor).
///     </para>
///     <para>
///         The limits bound the work, not the clock: a rendering that stays within every limit can still take longer than that on a slower machine or on
///         data that is near several limits at once, and the only wall-clock bound is the cancellation token of the call. The result of a rendering never depends on the speed of the
///         machine, with one documented exception: a regular expression is stopped by a wall-clock timeout (100 milliseconds when it needs the backtracking engine).
///     </para>
///     <para>
///         Each limit bounds one stage of the pipeline and is enforced by that stage: the source and JSON depth limits
///         when a document is opened, the others when the corresponding data, transform, scale, layout or painting
///         stage runs. Image size is bounded separately by <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>.
///     </para>
///     <para>
///         Every property is validated when it is set (a value outside its documented range throws
///         <see cref="ArgumentOutOfRangeException"/>) and the instance is immutable after initialization, so a limits
///         object can be shared between threads and calls. <see cref="Default"/> holds the default values.
///     </para>
/// </remarks>
public sealed class VegaLiteLimits
{
    /// <summary>The largest accepted <see cref="MaxJsonDepth"/>; recursion over the specification is bounded by it.</summary>
    private const int MaxJsonDepthCeiling = 256;

    /// <summary>The largest accepted <see cref="MaxCompositionDepth"/>.</summary>
    private const int MaxCompositionDepthCeiling = 64;

    /// <summary>The largest accepted <see cref="MaxExpressionDepth"/>; the expression evaluator's recursion is bounded by it.</summary>
    private const int MaxExpressionDepthCeiling = 256;

    /// <summary>The backing field of <see cref="MaxSourceLength"/>.</summary>
    private readonly int _maxSourceLength = 4_194_304;

    /// <summary>The backing field of <see cref="MaxJsonDepth"/>.</summary>
    private readonly int _maxJsonDepth = 64;

    /// <summary>The backing field of <see cref="MaxDataFiles"/>.</summary>
    private readonly int _maxDataFiles = 64;

    /// <summary>The backing field of <see cref="MaxDataBytes"/>.</summary>
    private readonly long _maxDataBytes = 64L * 1024 * 1024;

    /// <summary>The backing field of <see cref="MaxDataRows"/>.</summary>
    private readonly long _maxDataRows = 1_000_000;

    /// <summary>The backing field of <see cref="MaxFieldCount"/>.</summary>
    private readonly int _maxFieldCount = 1_024;

    /// <summary>The backing field of <see cref="MaxViews"/>.</summary>
    private readonly int _maxViews = 1_000;

    /// <summary>The backing field of <see cref="MaxCompositionDepth"/>.</summary>
    private readonly int _maxCompositionDepth = 16;

    /// <summary>The backing field of <see cref="MaxSceneItems"/>.</summary>
    private readonly long _maxSceneItems = 2_000_000;

    /// <summary>The backing field of <see cref="MaxScaleDomainSize"/>.</summary>
    private readonly int _maxScaleDomainSize = 100_000;

    /// <summary>The backing field of <see cref="MaxTicks"/>.</summary>
    private readonly int _maxTicks = 1_000;

    /// <summary>The backing field of <see cref="MaxExpressionLength"/>.</summary>
    private readonly int _maxExpressionLength = 4_096;

    /// <summary>The backing field of <see cref="MaxExpressionDepth"/>.</summary>
    private readonly int _maxExpressionDepth = 64;

    /// <summary>The backing field of <see cref="MaxEvaluationSteps"/>.</summary>
    private readonly long _maxEvaluationSteps = 50_000_000;

    /// <summary>The backing field of <see cref="MaxStringLength"/>.</summary>
    private readonly int _maxStringLength = 1_048_576;

    /// <summary>The backing field of <see cref="MaxDiagnostics"/>.</summary>
    private readonly int _maxDiagnostics = 1_000;

    /// <summary>The backing field of <see cref="MaxImages"/>.</summary>
    private readonly int _maxImages = 256;

    /// <summary>The backing field of <see cref="MaxPaintedPixels"/>.</summary>
    private readonly long _maxPaintedPixels = 250_000_000;

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static VegaLiteLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum length of the specification text in UTF-16 characters; 4,194,304 by default.
    /// </summary>
    /// <remarks>
    ///     Streams and files are read in chunks as bytes and reading stops one chunk past the longest input that could
    ///     still fit (three bytes per permitted character), so a stream that never ends cannot consume unbounded memory.
    ///     The bytes must be valid UTF-8: malformed input is rejected, never decoded with replacement characters.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxSourceLength
    {
        get => _maxSourceLength;
        init => _maxSourceLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of JSON arrays and objects; 64 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxJsonDepth
    {
        get => _maxJsonDepth;
        init => _maxJsonDepth = RequireRange(value, 1, MaxJsonDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of data files one rendering may read; 64 by default.
    /// </summary>
    /// <remarks>
    ///     Counted per measure or render call: each distinct file that is found counts once however often the chart
    ///     refers to it, and a file that does not exist does not count. Once the limit is reached, a file that has not
    ///     been read yet is refused before it is opened or the resolver callback is asked for it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxDataFiles
    {
        get => _maxDataFiles;
        init => _maxDataFiles = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum size in bytes of one data file; 67,108,864 (64 MiB) by default.
    /// </summary>
    /// <remarks>
    ///     The bytes are counted as they are read (from a file or from a resolver callback's stream), and reading stops one
    ///     chunk past the limit, so a stream that never ends cannot exhaust memory. A file of exactly this size is accepted.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxDataBytes
    {
        get => _maxDataBytes;
        init => _maxDataBytes = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of data rows, in total across all tables of one rendering; 1,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     Counted per measure or render call across inline values, files, named datasets, sequences and lookup sources; the
    ///     rows are counted before they are allocated. The rows of a table that a transform derives count as well: a transform
    ///     that can produce more rows than it reads (<c>fold</c>, <c>flatten</c>) reserves its output in this budget while its
    ///     input still counts, and the rows a filter, sample or pivot drops are given back. A derived or generated table is also
    ///     bounded by the 2^25 (33,554,432) cells that one table may hold, whatever this limit is: a generated sequence, the points of a sampled
    ///     curve and the key values of an impute are refused when they would exceed it, before they are allocated. A count that no 64-bit integer
    ///     holds is refused whatever the limit is.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxDataRows
    {
        get => _maxDataRows;
        init => _maxDataRows = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct fields in one data table, including the tables that the row transforms
    ///     derive (calculated, folded, flattened, pivoted and looked-up fields); 1,024 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxFieldCount
    {
        get => _maxFieldCount;
        init => _maxFieldCount = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of views one specification may have (the root view, its layers, the children of a concatenation, the body of a facet, the copies a
    ///     <c>repeat</c> makes, and the views that a composite mark such as <c>boxplot</c> stands for); 1,000 by default.
    /// </summary>
    /// <remarks>
    ///     Enforced when the views of a specification are normalized, before any data is read: exceeding it is a <see cref="VegaLiteRenderException"/> located at the offending view.
    ///     The copies of a <c>repeat</c> are counted before any of them is made. The cells a facet makes from the values of its data are counted when they are made.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxViews
    {
        get => _maxViews;
        init => _maxViews = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of view compositions (layers, concatenations, facets and repeats inside each other); 16 by default.
    /// </summary>
    /// <remarks>
    ///     Enforced when the views of a specification are normalized: the root layered view, concatenation, facet or repeat is level one, and exceeding the limit is a
    ///     <see cref="VegaLiteRenderException"/> located at the offending <c>layer</c>, concatenation, <c>facet</c> or <c>repeat</c>. The views a composite mark expands to are not nesting levels.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 64.</exception>
    public int MaxCompositionDepth
    {
        get => _maxCompositionDepth;
        init => _maxCompositionDepth = RequireRange(value, 1, MaxCompositionDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of scene items (marks, guide parts and text items) one rendering may create;
    ///     2,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxSceneItems
    {
        get => _maxSceneItems;
        init => _maxSceneItems = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of discrete values in one scale domain; 100,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxScaleDomainSize
    {
        get => _maxScaleDomainSize;
        init => _maxScaleDomainSize = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of ticks on one axis; 1,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTicks
    {
        get => _maxTicks;
        init => _maxTicks = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum length of one expression that a user writes (a <c>calculate</c>, a <c>filter</c> text, a <c>labelExpr</c>, an <c>{"expr": ...}</c>) in UTF-16 characters; 4,096 by default.
    /// </summary>
    /// <remarks>
    ///     Checked when the document is opened, for every expression that is written in the specification (a longer one is a
    ///     <see cref="VegaLiteSpecException"/> located at the expression). The text that the renderer writes from a predicate (a long membership list
    ///     as a condition or a time-aware filter) or for a composite mark is not user-written: it is bound by eight times <see cref="MaxSourceLength"/>, and a membership
    ///     list by <see cref="MaxScaleDomainSize"/> values.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxExpressionLength
    {
        get => _maxExpressionLength;
        init => _maxExpressionLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of one expression; 64 by default.
    /// </summary>
    /// <remarks>
    ///     Checked when the document is opened, as the nesting of groups, brackets, call arguments, array and object
    ///     literals, conditional branches and unary operands (a deeper one is a <see cref="VegaLiteSpecException"/>).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxExpressionDepth
    {
        get => _maxExpressionDepth;
        init => _maxExpressionDepth = RequireRange(value, 1, MaxExpressionDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of expression evaluation steps in one rendering; 50,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     One step is one executed instruction of a compiled expression (plus a step per 64 characters or elements of the
    ///     work of a function). The work of the transforms that loop over many elements (a loess, a density, a polynomial regression, a bootstrap, a window frame) is charged in
    ///     elements that are weighted to about three nanoseconds each, so that the default stops each of them in about ten seconds on a current desktop processor. The budget is shared by the parameters, the transforms and the row-bound mark values of one
    ///     measure or render call; exhausting it is a <see cref="VegaLiteRenderException"/> located at the expression.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxEvaluationSteps
    {
        get => _maxEvaluationSteps;
        init => _maxEvaluationSteps = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum length of a string value in UTF-16 characters; 1,048,576 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxStringLength
    {
        get => _maxStringLength;
        init => _maxStringLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct diagnostics and unsupported-feature reports that are kept; further
    ///     reports are collapsed into a single "suppressed" notice and rendering continues. 1,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxDiagnostics
    {
        get => _maxDiagnostics;
        init => _maxDiagnostics = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of pictures that one rendering may load through <see cref="VegaLiteRenderOptions.ImageResolver"/>; 256 by default.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Counted per <c>Render</c> call: every call of the resolver counts once, however the picture turns out (a resolver that returns nothing counts too). Within one call the
    ///         result for a link at a device size is kept and shared by every <c>image</c> mark item that draws it, so a chart that shows one icon for every point asks once, not once per point. The call that
    ///         would exceed the limit is not made: the render fails with a <see cref="VegaLiteRenderException"/> located at the mark, so a specification cannot make the host load pictures without
    ///         bound. <see cref="VegaLiteDocument.Measure"/> loads nothing. The size of each picture is bounded by the resolver (<see cref="DemaConsulting.CanvasNet.ImageSources.FolderImageResolver"/>
    ///         has a pixel budget) and by <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxImages
    {
        get => _maxImages;
        init => _maxImages = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of device pixels that one rendering may paint; 250,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="MaxSceneItems"/> counts items, not the area they cover, and the cost of painting an item is proportional to its area: a few thousand rectangles that each cover the whole
    ///         image, symbols with a size of a million square pixels, strokes that are thousands of pixels wide or pictures without a size are cheap to describe and take minutes to paint. This
    ///         limit bounds that work. The painter counts, before it paints an item, the pixels of its box on the image (a fill, and a stroke grown by half its width, are one count each; text is
    ///         estimated from its characters and font size where each line is drawn, and so is not charged when it is cut off, off the image, or not drawn at all; a picture counts its box; the layer of a clipped group and the layers of a translucent trail count their size) and fails when the total would
    ///         pass the limit. An item that lies outside the image counts nothing, and a box is never counted beyond the image (or the clip layer it is drawn on), so a chart on a large image is
    ///         charged for the pixels it can have.
    ///     </para>
    ///     <para>
    ///         The limit is on the pixels of the image after <see cref="VegaLiteRenderOptions.Scale"/> is applied. A realistic chart paints a small multiple of its own size: an
    ///         image of 4,096 by 4,096 pixels (16,777,216 pixels) with a dozen overlapping layers is about 200 million. The default stops the slowest painting that a specification can ask for (about
    ///         40 nanoseconds a pixel, measured on a circle with a stroke that is thousands of pixels wide) in about ten seconds. The render fails with a <see cref="VegaLiteRenderException"/> that names
    ///         <c>VegaLiteLimits.MaxPaintedPixels</c> and is located at the mark whose item did not fit (the nearest construct that has a location when the item itself has none), before any pixel of that item is painted.
    ///         <see cref="VegaLiteDocument.Measure"/> paints nothing and is not affected. Wall-clock time is otherwise bounded only by these limits and by the cancellation token.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxPaintedPixels
    {
        get => _maxPaintedPixels;
        init => _maxPaintedPixels = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Validates a value that must be at least one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <returns>The value, unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than one.</exception>
    private static long RequireAtLeastOne(long value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, "The value must be at least 1.");
        }

        return value;
    }

    /// <summary>
    ///     Validates a value that must lie in an inclusive range.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <param name="maximum">The largest accepted value.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <returns>The value, unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range.</exception>
    private static int RequireRange(int value, int minimum, int maximum, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, string.Create(CultureInfo.InvariantCulture, $"The value must be between {minimum} and {maximum}."));
        }

        return value;
    }
}
