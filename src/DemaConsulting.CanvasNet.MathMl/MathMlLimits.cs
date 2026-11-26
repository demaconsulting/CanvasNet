using System.Globalization;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     The resource limits that bound reading and rendering a MathML equation.
/// </summary>
/// <remarks>
///     <para>
///         MathML text is untrusted input, so every resource the pipeline can consume is limited and exceeding a limit
///         fails closed rather than exhausting memory, time or the call stack: oversized text with an
///         <see cref="InvalidDataException"/>, a structural limit met while the text is read with a located
///         <see cref="MathMlSyntaxException"/>, and a limit met while the equation is laid out or painted with a located
///         <see cref="MathMlRenderException"/>; the code of both exceptions is
///         <see cref="MathMlDiagnosticCodes.LimitExceeded"/> and the message names the property. Browsers have no such
///         limits, so a very large but valid equation that a browser draws can fail here.
///     </para>
///     <para>
///         The limits bound the work, not the clock: the only wall-clock bound is the cancellation token of a call, and
///         the result of a rendering never depends on the speed of the machine.
///     </para>
///     <para>
///         Every property is validated when it is set (a value outside its documented range throws
///         <see cref="ArgumentOutOfRangeException"/>) and the instance is immutable after initialization, so a limits
///         object can be shared between threads and calls. <see cref="Default"/> holds the default values.
///     </para>
/// </remarks>
public sealed class MathMlLimits
{
    /// <summary>The largest accepted <see cref="MaxNestingDepth"/>; the recursion of the layout is bounded by it.</summary>
    private const int MaxNestingDepthCeiling = 1_024;

    /// <summary>The backing field of <see cref="MaxTextSize"/>.</summary>
    private readonly int _maxTextSize = 1_048_576;

    /// <summary>The backing field of <see cref="MaxElements"/>.</summary>
    private readonly int _maxElements = 100_000;

    /// <summary>The backing field of <see cref="MaxNestingDepth"/>.</summary>
    private readonly int _maxNestingDepth = 256;

    /// <summary>The backing field of <see cref="MaxTokenLength"/>.</summary>
    private readonly int _maxTokenLength = 10_000;

    /// <summary>The backing field of <see cref="MaxLayoutWork"/>.</summary>
    private readonly long _maxLayoutWork = 10_000_000;

    /// <summary>The backing field of <see cref="MaxPaintedGlyphs"/>.</summary>
    private readonly int _maxPaintedGlyphs = 1_000_000;

    /// <summary>The backing field of <see cref="MaxDiagnostics"/>.</summary>
    private readonly int _maxDiagnostics = 1_000;

    /// <summary>The backing field of <see cref="MaxAssemblyParts"/>.</summary>
    private readonly int _maxAssemblyParts = 1_000;

    /// <summary>The backing field of <see cref="MaxTableCells"/>.</summary>
    private readonly int _maxTableCells = 100_000;

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static MathMlLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum length of the MathML text in UTF-16 characters, as supplied (comments and markup included);
    ///     1,048,576 (1 Mi) by default.
    /// </summary>
    /// <remarks>
    ///     Streams and files are read in chunks, and reading stops once the bound (three bytes per character) is certainly
    ///     exceeded, so a stream that never ends cannot consume unbounded memory. Text over the limit is an
    ///     <see cref="InvalidDataException"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTextSize
    {
        get => _maxTextSize;
        init => _maxTextSize = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of elements of an equation: the MathML elements (the <c>math</c> element included) and
    ///     the elements that are skipped (of another namespace, or inside a token), each counted once; the content of a
    ///     skipped element and of an annotation is not counted. 100,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxElements
    {
        get => _maxElements;
        init => _maxElements = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of elements (the <c>math</c> element is depth one); 256 by default.
    /// </summary>
    /// <remarks>
    ///     The reader is iterative, but the layout recurses over the element tree, so this limit protects the call stack:
    ///     a deeper equation fails while it is read, never with a stack overflow.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 1,024.</exception>
    public int MaxNestingDepth
    {
        get => _maxNestingDepth;
        init => _maxNestingDepth = RequireRange(value, 1, MaxNestingDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum length of the text of one token element (<c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c>,
    ///     <c>ms</c>) in UTF-16 characters, before whitespace is collapsed; 10,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTokenLength
    {
        get => _maxTokenLength;
        init => _maxTokenLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of metered steps of the layout of one equation (one per element and one per character
    ///     of a token); 10,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     <see cref="MathMlDocument.Measure"/> lays the equation out too and is bounded the same way.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxLayoutWork
    {
        get => _maxLayoutWork;
        init => _maxLayoutWork = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of glyphs that one rendering may paint; 1,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     The painter counts the glyphs to paint before it paints any of them and fails when the count is over the limit,
    ///     so nothing is painted. <see cref="MathMlDocument.Measure"/> paints nothing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxPaintedGlyphs
    {
        get => _maxPaintedGlyphs;
        init => _maxPaintedGlyphs = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct warnings and unsupported-feature reports that are kept per call; further
    ///     reports are collapsed into a single <see cref="MathMlDiagnosticCodes.DiagnosticsSuppressed"/> notice and
    ///     rendering continues. 1,000 by default.
    /// </summary>
    /// <remarks>
    ///     The limits of the open options also bound the reports that reading finds (elements of other namespaces, links,
    ///     text outside a token), which the document keeps for every call: at most this many, followed by a single
    ///     suppression notice.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxDiagnostics
    {
        get => _maxDiagnostics;
        init => _maxDiagnostics = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of glyphs of one stretched operator (the parts of a glyph assembly, extenders repeated);
    ///     1,000 by default.
    /// </summary>
    /// <remarks>
    ///     A stretchy operator grows to cover its neighbors by repeating the extender parts of its glyph assembly, so the
    ///     number of glyphs grows with the size of the neighbors. The number is computed before any glyph is placed, and an
    ///     operator that needs more fails with a located <see cref="MathMlRenderException"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxAssemblyParts
    {
        get => _maxAssemblyParts;
        init => _maxAssemblyParts = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of grid slots of one table (<c>mtable</c>): its rows times its columns after the
    ///     <c>rowspan</c> and <c>columnspan</c> of its cells are applied; 100,000 by default.
    /// </summary>
    /// <remarks>
    ///     A few cells with large spans make a large grid, so the number of slots is checked as the columns grow, before
    ///     the grid is allocated, and a table that needs more fails with a located <see cref="MathMlRenderException"/>.
    ///     Every slot is also charged to <see cref="MaxLayoutWork"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTableCells
    {
        get => _maxTableCells;
        init => _maxTableCells = (int)RequireAtLeastOne(value);
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
