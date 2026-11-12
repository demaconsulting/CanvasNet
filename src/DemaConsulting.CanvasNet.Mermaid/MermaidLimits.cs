// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The resource limits that bound reading and rendering a Mermaid diagram.
/// </summary>
/// <remarks>
///     <para>
///         Mermaid text is untrusted input, so every resource the pipeline can consume is limited and exceeding a limit
///         fails closed rather than exhausting memory, time or the call stack: oversized text with an
///         <see cref="InvalidDataException"/>, a structural limit met while the text is read with a located
///         <see cref="MermaidSyntaxException"/>, and a limit met while the diagram is laid out or painted with a located
///         <see cref="MermaidRenderException"/>; the code of both exceptions is
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/> and the message names the property. <see cref="MaxTextSize"/>
///         and <see cref="MaxEdges"/> are Mermaid's own limits (<c>maxTextSize</c> and <c>maxEdges</c>) with Mermaid's
///         defaults; the others are CanvasNet additions with no Mermaid equivalent, so a very large but valid diagram that
///         Mermaid draws can fail here.
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
public sealed class MermaidLimits
{
    /// <summary>The largest accepted <see cref="MaxNestingDepth"/>; recursion over nested blocks is bounded by it.</summary>
    private const int MaxNestingDepthCeiling = 256;

    /// <summary>The largest accepted <see cref="MaxYamlDepth"/>; recursion over YAML collections is bounded by it.</summary>
    private const int MaxYamlDepthCeiling = 256;

    /// <summary>The backing field of <see cref="MaxTextSize"/>.</summary>
    private readonly int _maxTextSize = 50_000;

    /// <summary>The backing field of <see cref="MaxEdges"/>.</summary>
    private readonly int _maxEdges = 500;

    /// <summary>The backing field of <see cref="MaxNodes"/>.</summary>
    private readonly int _maxNodes = 2_000;

    /// <summary>The backing field of <see cref="MaxLabelLength"/>.</summary>
    private readonly int _maxLabelLength = 4_096;

    /// <summary>The backing field of <see cref="MaxNestingDepth"/>.</summary>
    private readonly int _maxNestingDepth = 32;

    /// <summary>The backing field of <see cref="MaxYamlDepth"/>.</summary>
    private readonly int _maxYamlDepth = 32;

    /// <summary>The backing field of <see cref="MaxLayoutWork"/>.</summary>
    private readonly long _maxLayoutWork = 50_000_000;

    /// <summary>The backing field of <see cref="MaxImages"/>.</summary>
    private readonly int _maxImages = 256;

    /// <summary>The backing field of <see cref="MaxPaintedPixels"/>.</summary>
    private readonly long _maxPaintedPixels = 250_000_000;

    /// <summary>The backing field of <see cref="MaxDiagnostics"/>.</summary>
    private readonly int _maxDiagnostics = 1_000;

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static MermaidLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum length of the diagram text in UTF-16 characters; 50,000 by default (Mermaid's
    ///     <c>maxTextSize</c>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         As in Mermaid, the limit applies to the diagram text after the frontmatter, the directives and the
    ///         comment lines are removed, so they do not count. The whole text, as supplied, may be at most four times the
    ///         limit, so that a hostile text is refused before it is preprocessed; streams and files are read in chunks and
    ///         reading stops once that bound is certainly exceeded.
    ///     </para>
    ///     <para>
    ///         Text over the limit is an <see cref="InvalidDataException"/> (Mermaid draws a "Maximum text size in diagram
    ///         exceeded" picture instead).
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxTextSize
    {
        get => _maxTextSize;
        init => _maxTextSize = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of edges (links) of a diagram; 500 by default (Mermaid's <c>maxEdges</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxEdges
    {
        get => _maxEdges;
        init => _maxEdges = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of nodes of a diagram, subgraphs included (the section statements of a pie chart, the
    ///     task and section statements of a gantt chart, the nodes of a mindmap); 2,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxNodes
    {
        get => _maxNodes;
        init => _maxNodes = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum length of one label (the text of a node, an edge or a subgraph title, a pie chart's title,
    ///     labels and legend texts, every text of a gantt chart and its axis labels, a mindmap's ids, descriptions, icons,
    ///     classes and class lists, and its labels as they are drawn) in UTF-16 characters; 4,096 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxLabelLength
    {
        get => _maxLabelLength;
        init => _maxLabelLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of blocks of a diagram (subgraphs inside subgraphs, a mindmap node's ancestors);
    ///     32 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxNestingDepth
    {
        get => _maxNestingDepth;
        init => _maxNestingDepth = RequireRange(value, 1, MaxNestingDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum nesting depth of YAML collections (mappings and sequences) in the frontmatter and in shape
    ///     data; 32 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 256.</exception>
    public int MaxYamlDepth
    {
        get => _maxYamlDepth;
        init => _maxYamlDepth = RequireRange(value, 1, MaxYamlDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of metered steps of the layout of one diagram; 50,000,000 by default.
    /// </summary>
    /// <remarks>
    ///     The layout stages charge their iterative work (network-simplex pivots, ordering sweeps, coordinate passes, a
    ///     gantt chart's date passes, excluded days and ticks, and every step of a mindmap's cose-bilkent layout) to this
    ///     budget, so a pathological graph fails with <see cref="MermaidDiagnosticCodes.LimitExceeded"/> instead of
    ///     running for minutes; a mindmap of about 1,000 nodes or more can reach the default.
    ///     <see cref="MermaidDocument.Measure"/> lays the diagram out too and is bounded the same way.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxLayoutWork
    {
        get => _maxLayoutWork;
        init => _maxLayoutWork = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of pictures that one rendering may load through
    ///     <see cref="MermaidRenderOptions.ImageResolver"/>; 256 by default.
    /// </summary>
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
    ///     The painter counts, before it paints an item, the pixels of the item's box on the image (a fill and a stroke
    ///     count once each, text counts the box of each line, a picture its box), clipped to the image, and fails when the
    ///     total would pass the limit, before any pixel of that item is painted. The limit is on the pixels after
    ///     <see cref="MermaidRenderOptions.Scale"/> is applied. <see cref="MermaidDocument.Measure"/> paints nothing.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxPaintedPixels
    {
        get => _maxPaintedPixels;
        init => _maxPaintedPixels = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct warnings and unsupported-feature reports that are kept per call; further
    ///     reports are collapsed into a single <see cref="MermaidDiagnosticCodes.DiagnosticsSuppressed"/> notice and
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
