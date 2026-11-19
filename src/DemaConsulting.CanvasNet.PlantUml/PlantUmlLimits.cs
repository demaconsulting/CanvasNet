using System.Globalization;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     The resource limits that bound reading and rendering a PlantUML diagram.
/// </summary>
/// <remarks>
///     <para>
///         PlantUML text is untrusted input, so every resource the pipeline can consume is limited and exceeding a limit
///         fails closed rather than exhausting memory, time or the call stack: oversized text with an
///         <see cref="InvalidDataException"/>, a structural limit met while the text is read with a located
///         <see cref="PlantUmlSyntaxException"/>, and a limit met while the diagram is laid out or painted with a located
///         <see cref="PlantUmlRenderException"/>; the code of both exceptions is
///         <see cref="PlantUmlDiagnosticCodes.LimitExceeded"/> and the message names the property. The default values
///         are those of the Mermaid package of CanvasNet, so the two diagram languages are bounded alike; PlantUML itself
///         has no such limits, so a very large but valid diagram that PlantUML draws can fail here.
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
public sealed class PlantUmlLimits
{
    /// <summary>The largest accepted <see cref="MaxNestingDepth"/>; recursion over nested blocks is bounded by it.</summary>
    private const int MaxNestingDepthCeiling = 256;

    /// <summary>The largest accepted <see cref="MaxIncludeDepth"/>; recursion over nested includes is bounded by it.</summary>
    private const int MaxIncludeDepthCeiling = 64;

    /// <summary>The backing field of <see cref="MaxTextSize"/>.</summary>
    private readonly int _maxTextSize = 50_000;

    /// <summary>The backing field of <see cref="MaxNodes"/>.</summary>
    private readonly int _maxNodes = 2_000;

    /// <summary>The backing field of <see cref="MaxEdges"/>.</summary>
    private readonly int _maxEdges = 500;

    /// <summary>The backing field of <see cref="MaxLabelLength"/>.</summary>
    private readonly int _maxLabelLength = 4_096;

    /// <summary>The backing field of <see cref="MaxNestingDepth"/>.</summary>
    private readonly int _maxNestingDepth = 32;

    /// <summary>The backing field of <see cref="MaxIncludeDepth"/>.</summary>
    private readonly int _maxIncludeDepth = 16;

    /// <summary>The backing field of <see cref="MaxIncludes"/>.</summary>
    private readonly int _maxIncludes = 64;

    /// <summary>The backing field of <see cref="MaxLayoutWork"/>.</summary>
    private readonly long _maxLayoutWork = 50_000_000;

    /// <summary>The backing field of <see cref="MaxPaintedPixels"/>.</summary>
    private readonly long _maxPaintedPixels = 250_000_000;

    /// <summary>The backing field of <see cref="MaxDiagnostics"/>.</summary>
    private readonly int _maxDiagnostics = 1_000;

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static PlantUmlLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum length of the diagram text in UTF-16 characters; 50,000 by default.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The limit applies to the statements of the diagram after the preprocessor removed the comments and the
    ///         directives and substituted the variables and macros, so comments do not count but text that a macro
    ///         produces does. The whole text, as supplied, may be at most four times the limit, so that a hostile text is
    ///         refused before it is preprocessed; streams and files are read in chunks and reading stops once that bound
    ///         is certainly exceeded.
    ///     </para>
    ///     <para>
    ///         Text over the limit is an <see cref="InvalidDataException"/>.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTextSize
    {
        get => _maxTextSize;
        init => _maxTextSize = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of nodes of a diagram (the participants of a sequence diagram, the instructions and
    ///     swimlanes of an activity diagram); 2,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxNodes
    {
        get => _maxNodes;
        init => _maxNodes = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of edges of a diagram (the messages of a sequence diagram, the arrows of an activity
    ///     diagram); 500 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxEdges
    {
        get => _maxEdges;
        init => _maxEdges = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum length of one label (the name of a participant, the text of a message, an action, a
    ///     condition, an arrow label, a note, a group header, a divider, a title or a legend) in UTF-16 characters;
    ///     4,096 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxLabelLength
    {
        get => _maxLabelLength;
        init => _maxLabelLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of blocks of a diagram (groups inside groups of a sequence diagram; ifs,
    ///     loops, forks, splits and partitions inside each other in an activity diagram); 32 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxNestingDepth
    {
        get => _maxNestingDepth;
        init => _maxNestingDepth = RequireRange(value, 1, MaxNestingDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum depth of nested <c>!include</c> directives (an included file that includes another file);
    ///     16 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 64.</exception>
    public int MaxIncludeDepth
    {
        get => _maxIncludeDepth;
        init => _maxIncludeDepth = RequireRange(value, 1, MaxIncludeDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of files that the <c>!include</c> directives of one diagram may read; 64 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxIncludes
    {
        get => _maxIncludes;
        init => _maxIncludes = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of metered steps of the layout of one diagram; 50,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     The layout charges its iterative work (the constraint passes that place the participants, the rows of the
    ///     messages, notes and blocks, and the lines of their texts) to this budget, so a pathological diagram fails with
    ///     <see cref="PlantUmlDiagnosticCodes.LimitExceeded"/> instead of running for minutes.
    ///     <see cref="PlantUmlDocument.Measure"/> lays the diagram out too and is bounded the same way.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxLayoutWork
    {
        get => _maxLayoutWork;
        init => _maxLayoutWork = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of device pixels that one rendering may paint; 250,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     The painter counts, before it paints an item, the pixels of the item's box on the image (a fill and a stroke
    ///     count once each, text counts the box of each line), clipped to the image, and fails when the total would pass
    ///     the limit, before any pixel of that item is painted. The limit is on the pixels after
    ///     <see cref="PlantUmlRenderOptions.Scale"/> is applied. <see cref="PlantUmlDocument.Measure"/> paints nothing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxPaintedPixels
    {
        get => _maxPaintedPixels;
        init => _maxPaintedPixels = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct warnings and unsupported-feature reports that are kept per call; further
    ///     reports are collapsed into a single <see cref="PlantUmlDiagnosticCodes.DiagnosticsSuppressed"/> notice and
    ///     rendering continues. 1,000 by default.
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
