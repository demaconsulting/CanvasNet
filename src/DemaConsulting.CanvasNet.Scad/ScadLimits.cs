namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The resource limits that bound the evaluation of an OpenSCAD-language program.
/// </summary>
/// <remarks>
///     <para>
///         Evaluation runs untrusted programs, so every resource it can consume is limited and exceeding a limit
///         fails closed with a <see cref="ScadEvaluationException"/> that carries the source location, rather than
///         exhausting memory, time or the call stack. The defaults are generous enough for realistic models and
///         small enough to stop a runaway program within seconds.
///     </para>
///     <para>
///         Every property is validated when it is set (a value outside its documented range throws
///         <see cref="ArgumentOutOfRangeException"/>) and the instance is immutable after initialization, so a limits
///         object can be shared between threads and evaluations. <see cref="Default"/> holds the default values.
///     </para>
/// </remarks>
public sealed class ScadLimits
{
    /// <summary>The smallest accepted <see cref="MaxRecursionDepth"/>.</summary>
    private const int MinRecursionDepth = 16;

    /// <summary>The largest accepted <see cref="MaxRecursionDepth"/>; the evaluator's large-stack thread is sized for it.</summary>
    private const int MaxRecursionDepthCeiling = 16_384;

    /// <summary>The largest accepted <see cref="MaxIncludeDepth"/>.</summary>
    private const int MaxIncludeDepthCeiling = 64;

    /// <summary>The backing field of <see cref="MaxRecursionDepth"/>.</summary>
    private readonly int _maxRecursionDepth = 5_000;

    /// <summary>The backing field of <see cref="MaxEvaluationSteps"/>.</summary>
    private readonly long _maxEvaluationSteps = 50_000_000;

    /// <summary>The backing field of <see cref="MaxLoopIterations"/>.</summary>
    private readonly long _maxLoopIterations = 10_000_000;

    /// <summary>The backing field of <see cref="MaxListLength"/>.</summary>
    private readonly int _maxListLength = 1_000_000;

    /// <summary>The backing field of <see cref="MaxStringLength"/>.</summary>
    private readonly int _maxStringLength = 1_048_576;

    /// <summary>The backing field of <see cref="MaxIncludeDepth"/>.</summary>
    private readonly int _maxIncludeDepth = 16;

    /// <summary>The backing field of <see cref="MaxIncludedFiles"/>.</summary>
    private readonly int _maxIncludedFiles = 1_000;

    /// <summary>The backing field of <see cref="MaxInstances"/>.</summary>
    private readonly int _maxInstances = 1_000_000;

    /// <summary>The backing field of <see cref="MaxOutputLength"/>.</summary>
    private readonly int _maxOutputLength = 4_194_304;

    /// <summary>The backing field of <see cref="MaxDiagnostics"/>.</summary>
    private readonly int _maxDiagnostics = 1_000;

    /// <summary>
    ///     Gets the shared instance that holds the default limits.
    /// </summary>
    public static ScadLimits Default { get; } = new();

    /// <summary>
    ///     Gets the maximum depth of nested evaluation frames (expressions, statements, module bodies); 5,000 by default.
    /// </summary>
    /// <remarks>
    ///     Recursion is counted in evaluation frames, not in language-level calls: a typical non-tail recursive
    ///     function costs about three frames per level. Tail-recursive function calls reuse their frame.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 16 to 16,384.</exception>
    public int MaxRecursionDepth
    {
        get => _maxRecursionDepth;
        init => _maxRecursionDepth = RequireRange(value, MinRecursionDepth, MaxRecursionDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of evaluation steps (one per expression, statement and loop turn, more for
    ///     built-ins that process lists or strings and for the multiply-add work of matrix products);
    ///     50,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxEvaluationSteps
    {
        get => _maxEvaluationSteps;
        init => _maxEvaluationSteps = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum total number of loop iterations across the whole evaluation (<c>for</c> statements,
    ///     comprehensions and C-style loops); 10,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public long MaxLoopIterations
    {
        get => _maxLoopIterations;
        init => _maxLoopIterations = RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of elements in one list, and in the result of a matrix product (rows times
    ///     columns); 1,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxListLength
    {
        get => _maxListLength;
        init => _maxListLength = (int)RequireAtLeastOne(value);
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
    ///     Gets the maximum nesting depth of <c>include</c> directives; 16 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to 64.</exception>
    public int MaxIncludeDepth
    {
        get => _maxIncludeDepth;
        init => _maxIncludeDepth = RequireRange(value, 1, MaxIncludeDepthCeiling);
    }

    /// <summary>
    ///     Gets the maximum number of file inclusions and library loads in one evaluation; 1,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxIncludedFiles
    {
        get => _maxIncludedFiles;
        init => _maxIncludedFiles = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of module instances in the instantiation tree; 1,000,000 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxInstances
    {
        get => _maxInstances;
        init => _maxInstances = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum total length in characters of all <c>echo</c> output lines; 4,194,304 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is less than one.</exception>
    public int MaxOutputLength
    {
        get => _maxOutputLength;
        init => _maxOutputLength = (int)RequireAtLeastOne(value);
    }

    /// <summary>
    ///     Gets the maximum number of distinct warnings that are kept; further warnings are collapsed into a single
    ///     "suppressed" notice and evaluation continues. 1,000 by default.
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
            throw new ArgumentOutOfRangeException(propertyName, value, $"The value must be between {minimum} and {maximum}.");
        }

        return value;
    }
}
