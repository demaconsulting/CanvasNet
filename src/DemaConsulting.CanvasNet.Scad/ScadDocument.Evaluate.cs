using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad;

/// <content>
///     The evaluation entry point of the document.
/// </content>
public sealed partial class ScadDocument
{
    /// <summary>
    ///     Evaluates the program: resolves <c>include</c>/<c>use</c> directives, runs the module and function calls,
    ///     and reports <c>echo</c> output, warnings and unsupported features.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Evaluation is deterministic: the same document and options always produce the same result, on every
    ///         platform and culture. It is bounded by <see cref="ScadEvaluationOptions.Limits"/> and fails closed with a
    ///         <see cref="ScadEvaluationException"/> when a limit is exceeded, an <c>assert</c> fails, an include
    ///         cycle or sandbox violation is found, or the program is otherwise invalid beyond recovery; it cannot
    ///         overflow the stack of the calling thread, whatever its size.
    ///     </para>
    ///     <para>
    ///         Files named by <c>include</c> and <c>use</c> are found only through the
    ///         <see cref="ScadOpenOptions"/> the document was opened with: relative to the document's
    ///         <see cref="ScadOpenOptions.BaseDirectory"/> and <see cref="ScadOpenOptions.LibraryPaths"/>, or
    ///         through its <see cref="ScadOpenOptions.FileResolver"/> (in which case the file system is not touched at
    ///         all). A path that would leave those directories is rejected. Environment variables are ignored.
    ///     </para>
    ///     <para>
    ///         The handlers in <paramref name="options"/> are invoked on the calling thread, in the order the events
    ///         occurred, after the evaluation has finished and before this method returns or throws (see
    ///         <see cref="ScadEvaluationOptions"/>). The file resolver callback is likewise only called on the calling
    ///         thread. The document is immutable and holds no state between calls, so it can be evaluated from several
    ///         threads at once; calls do not influence each other.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional evaluation options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the evaluation.</param>
    /// <returns>The echo lines, warnings and unsupported features of the evaluation and the size of its result.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during evaluation.</exception>
    /// <exception cref="ScadEvaluationException">
    ///     Thrown, with the source location, when a limit is exceeded, an <c>assert</c> fails, an include cycle or
    ///     sandbox violation is found, or an <c>include</c>/<c>use</c> cannot be resolved without a base directory,
    ///     library path or resolver.
    /// </exception>
    /// <exception cref="ScadUnsupportedFeatureException">
    ///     Thrown when <see cref="ScadEvaluationOptions.ThrowOnUnsupportedFeature"/> is set and the program uses an
    ///     unsupported feature.
    /// </exception>
    /// <exception cref="ScadSyntaxException">Thrown when an included or used file contains a syntax error.</exception>
    /// <exception cref="InvalidDataException">Thrown when an included or used file is longer than 4,194,304 characters.</exception>
    /// <exception cref="IOException">Thrown when an included or used file cannot be read.</exception>
    public ScadEvaluationResult Evaluate(ScadEvaluationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new ScadEvaluationOptions();
        cancellationToken.ThrowIfCancellationRequested();

        var settings = new ScadEvaluationSettings
        {
            Limits = effective.Limits,
            Time = effective.Time,
            Overrides = effective.Overrides,
            ThrowOnUnsupportedFeature = effective.ThrowOnUnsupportedFeature,
            Cancellation = cancellationToken,
        };
        var sink = new ScadEvaluationSink(effective.Limits);

        ScadEvaluationOutput output;
        try
        {
            output = ScadEvaluator.Evaluate(this, settings, sink);
        }
        catch
        {
            // The events recorded before the failure are still delivered, on this thread; a failing handler must
            // not hide the evaluation failure being reported
            DeliverIgnoringHandlerFailures(sink, effective);
            throw;
        }

        // On success a failing handler propagates, as documented
        sink.Deliver(effective.EchoHandler, effective.WarningHandler, effective.UnsupportedFeatureHandler);
        return new ScadEvaluationResult(
            Array.AsReadOnly(sink.EchoLines.ToArray()),
            Array.AsReadOnly(sink.Diagnostics.ToArray()),
            Array.AsReadOnly(sink.UnsupportedFeatures.ToArray()),
            output.InstanceCount);
    }

    /// <summary>
    ///     Delivers the buffered events after a failed evaluation, ignoring any exception a handler throws.
    /// </summary>
    /// <param name="sink">The sink that holds the events.</param>
    /// <param name="options">The options that carry the handlers.</param>
    private static void DeliverIgnoringHandlerFailures(ScadEvaluationSink sink, ScadEvaluationOptions options)
    {
        try
        {
            sink.Deliver(options.EchoHandler, options.WarningHandler, options.UnsupportedFeatureHandler);
        }
        catch (Exception)
        {
            // Documented: the evaluation failure takes precedence over a handler failure
        }
    }
}
