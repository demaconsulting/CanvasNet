using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The single channel through which the geometry builder (and the renderer mapping) reports warnings and
///     unsupported features.
/// </summary>
/// <remarks>
///     <para>
///         The reporter writes into the same <see cref="ScadEvaluationSink"/> the evaluation used, so events are
///         ordered (evaluation, geometry, renderer), de-duplicated and capped exactly like the evaluator's, and are
///         delivered to the caller's handlers once, on the calling thread. Strict mode behaves exactly like the
///         evaluator's host: the first report of a feature at a location throws a
///         <see cref="ScadUnsupportedFeatureException"/>, a repeated one is silent.
///     </para>
/// </remarks>
internal sealed class ScadReporter
{
    /// <summary>The sink that buffers the events.</summary>
    private readonly ScadEvaluationSink _sink;

    /// <summary>Whether a new unsupported feature aborts with an exception.</summary>
    private readonly bool _strict;

    /// <summary>
    ///     Initializes a reporter.
    /// </summary>
    /// <param name="sink">The sink that buffers the events.</param>
    /// <param name="strict">Whether a new unsupported feature throws a <see cref="ScadUnsupportedFeatureException"/>.</param>
    public ScadReporter(ScadEvaluationSink sink, bool strict)
    {
        _sink = sink;
        _strict = strict;
    }

    /// <summary>
    ///     Reports a warning (an invalid argument or a recoverable problem).
    /// </summary>
    /// <param name="message">The warning text, without location.</param>
    /// <param name="location">The location the warning is about.</param>
    public void Warn(string message, ScadSourceLocation location) => _sink.Warn(message, location);

    /// <summary>
    ///     Reports an unsupported feature and, in strict mode, throws for the first report of it.
    /// </summary>
    /// <param name="feature">The stable feature identifier.</param>
    /// <param name="message">The description, without location.</param>
    /// <param name="location">The location where the feature is used.</param>
    /// <exception cref="ScadUnsupportedFeatureException">Thrown in strict mode when the report is new.</exception>
    public void Unsupported(string feature, string message, ScadSourceLocation location)
    {
        // The first report of a feature at a location may abort the build; repeats are silent
        var isNew = _sink.Unsupported(feature, message, location);
        if (isNew && _strict)
        {
            throw new ScadUnsupportedFeatureException(feature, message, location);
        }
    }
}
