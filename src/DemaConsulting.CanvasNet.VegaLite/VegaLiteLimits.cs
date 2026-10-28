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
///         specification within seconds.
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

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static VegaLiteLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum length of the specification text in UTF-16 characters; 4,194,304 by default.
    /// </summary>
    /// <remarks>
    ///     Streams and files are read in chunks and reading stops one chunk past the limit, so a stream that never
    ///     ends cannot consume unbounded memory.
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxDataFiles
    {
        get => _maxDataFiles;
        init => _maxDataFiles = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum size in bytes of one data file; 67,108,864 (64 MiB) by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxDataBytes
    {
        get => _maxDataBytes;
        init => _maxDataBytes = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of data rows, in total across all tables of one rendering; 1,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxDataRows
    {
        get => _maxDataRows;
        init => _maxDataRows = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct fields in one data table; 1,024 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxFieldCount
    {
        get => _maxFieldCount;
        init => _maxFieldCount = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of views (the cells of a concatenation, facet or repeat); 1,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxViews
    {
        get => _maxViews;
        init => _maxViews = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of view compositions (layers inside concatenations inside facets and so on);
    ///     16 by default.
    /// </summary>
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
    ///     Gets the maximum length of one expression in UTF-16 characters; 4,096 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxExpressionLength
    {
        get => _maxExpressionLength;
        init => _maxExpressionLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of one expression; 64 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxExpressionDepth
    {
        get => _maxExpressionDepth;
        init => _maxExpressionDepth = RequireRange(value, 1, MaxExpressionDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of expression evaluation steps in one rendering; 50,000,000 by default.
    /// </summary>
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
