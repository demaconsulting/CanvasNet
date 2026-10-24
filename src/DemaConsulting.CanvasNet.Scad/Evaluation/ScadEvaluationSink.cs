namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Buffers the side-effect output of one evaluation: echo lines, warnings and unsupported-feature reports.
/// </summary>
/// <remarks>
///     <para>
///         Evaluation never calls user callbacks directly. Everything it wants to report is recorded here, in the
///         order it happened, and the owner delivers it to the caller's handlers afterwards on the calling thread
///         (<see cref="Deliver"/>). This keeps handlers off the evaluator's large-stack fallback thread and makes a
///         restarted pass invisible: the owner calls <see cref="Reset"/> before re-running, so each event is
///         delivered exactly once.
///     </para>
///     <para>
///         The sink also enforces two limits. The total length of echo output is bounded by
///         <see cref="ScadLimits.MaxOutputLength"/> (exceeding it is an error). Warnings are de-duplicated by message
///         and location and bounded by <see cref="ScadLimits.MaxDiagnostics"/>: warnings beyond the cap are dropped
///         and replaced by a single "suppressed" notice, so a program that warns in a loop cannot exhaust memory.
///         Unsupported-feature reports are de-duplicated by feature and location.
///     </para>
///     <para>
///         The sink is not thread-safe; one evaluation uses it from one thread at a time (the calling thread, or the
///         fallback thread while the caller waits).
///     </para>
/// </remarks>
internal sealed class ScadEvaluationSink
{
    /// <summary>The limits that bound output and diagnostics.</summary>
    private readonly ScadLimits _limits;

    /// <summary>The buffered echo lines.</summary>
    private readonly List<string> _echo = [];

    /// <summary>The buffered warnings.</summary>
    private readonly List<ScadDiagnostic> _diagnostics = [];

    /// <summary>The buffered unsupported-feature reports.</summary>
    private readonly List<ScadUnsupportedFeature> _unsupported = [];

    /// <summary>The chronological record of what was buffered, so delivery keeps the original order.</summary>
    private readonly List<(EventKind Kind, int Index)> _events = [];

    /// <summary>The keys of the warnings already recorded.</summary>
    private readonly HashSet<(string Message, ScadSourceLocation Location)> _seenWarnings = [];

    /// <summary>The keys of the unsupported-feature reports already recorded.</summary>
    private readonly HashSet<(string Feature, ScadSourceLocation Location)> _seenUnsupported = [];

    /// <summary>The number of echo characters buffered so far.</summary>
    private long _echoLength;

    /// <summary>Whether the "further diagnostics suppressed" notice has been recorded.</summary>
    private bool _suppressionReported;

    /// <summary>
    ///     Initializes a sink.
    /// </summary>
    /// <param name="limits">The limits that bound output and diagnostics.</param>
    public ScadEvaluationSink(ScadLimits limits)
    {
        _limits = limits;
    }

    /// <summary>The kind of a buffered event.</summary>
    private enum EventKind
    {
        /// <summary>An echo line.</summary>
        Echo,

        /// <summary>A warning.</summary>
        Warning,

        /// <summary>An unsupported-feature report.</summary>
        Unsupported,
    }

    /// <summary>
    ///     Gets the buffered echo lines in order.
    /// </summary>
    public IReadOnlyList<string> EchoLines => _echo;

    /// <summary>
    ///     Gets the buffered warnings in order (including the suppression notice, when it was recorded).
    /// </summary>
    public IReadOnlyList<ScadDiagnostic> Diagnostics => _diagnostics;

    /// <summary>
    ///     Gets the buffered unsupported-feature reports in order.
    /// </summary>
    public IReadOnlyList<ScadUnsupportedFeature> UnsupportedFeatures => _unsupported;

    /// <summary>
    ///     Records an echo line.
    /// </summary>
    /// <param name="line">The complete line (including any <c>ECHO: </c> prefix, without a line break).</param>
    /// <param name="location">The location of the echo, used when the output limit is exceeded.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the total echo output would exceed <see cref="ScadLimits.MaxOutputLength"/>.</exception>
    public void Echo(string line, ScadSourceLocation location = default)
    {
        // The limit counts characters; a line that would cross it is rejected before it is stored
        if (_echoLength + line.Length > _limits.MaxOutputLength)
        {
            throw new ScadEvaluationException($"Echo output limit exceeded (limit {_limits.MaxOutputLength})", location);
        }

        _echoLength += line.Length;
        _events.Add((EventKind.Echo, _echo.Count));
        _echo.Add(line);
    }

    /// <summary>
    ///     Records a warning unless an identical one (same message and location) was already recorded or the
    ///     diagnostics cap has been reached.
    /// </summary>
    /// <remarks>
    ///     When the cap is reached, the first dropped warning is replaced by one notice
    ///     (<c>Further diagnostics suppressed (limit N)</c>, at that warning's location); later ones are silently
    ///     dropped. At most <see cref="ScadLimits.MaxDiagnostics"/> real warnings plus the notice are kept.
    /// </remarks>
    /// <param name="message">The warning text, without location or severity prefix.</param>
    /// <param name="location">The location the warning is about.</param>
    public void Warn(string message, ScadSourceLocation location)
    {
        // Duplicates are dropped without counting against the cap
        if (_seenWarnings.Contains((message, location)))
        {
            return;
        }

        if (_diagnostics.Count - (_suppressionReported ? 1 : 0) >= _limits.MaxDiagnostics)
        {
            ReportSuppression(location);
            return;
        }

        _seenWarnings.Add((message, location));
        AddWarning(new ScadDiagnostic(message, location));
    }

    /// <summary>
    ///     Records an unsupported-feature report unless the same feature at the same location was already recorded.
    /// </summary>
    /// <param name="feature">The stable feature identifier.</param>
    /// <param name="message">The description, without the location.</param>
    /// <param name="location">The location where the feature is used.</param>
    /// <returns><see langword="true"/> when the report is new; <see langword="false"/> when it was a duplicate.</returns>
    public bool Unsupported(string feature, string message, ScadSourceLocation location)
    {
        if (!_seenUnsupported.Add((feature, location)))
        {
            return false;
        }

        _events.Add((EventKind.Unsupported, _unsupported.Count));
        _unsupported.Add(new ScadUnsupportedFeature(feature, message, location));
        return true;
    }

    /// <summary>
    ///     Discards everything recorded so that the evaluation pass can be re-run from the start.
    /// </summary>
    public void Reset()
    {
        _echo.Clear();
        _diagnostics.Clear();
        _unsupported.Clear();
        _events.Clear();
        _seenWarnings.Clear();
        _seenUnsupported.Clear();
        _echoLength = 0;
        _suppressionReported = false;
    }

    /// <summary>
    ///     Delivers everything recorded to the supplied handlers, in the order it was recorded.
    /// </summary>
    /// <remarks>
    ///     Call this once after the evaluation has finished (or failed), on the calling thread. An exception thrown
    ///     by a handler propagates and stops delivery.
    /// </remarks>
    /// <param name="echo">The echo-line handler, or <see langword="null"/>.</param>
    /// <param name="warn">The warning handler, or <see langword="null"/>.</param>
    /// <param name="unsupported">The unsupported-feature handler, or <see langword="null"/>.</param>
    public void Deliver(Action<string>? echo, Action<ScadDiagnostic>? warn, Action<ScadUnsupportedFeature>? unsupported)
    {
        foreach (var (kind, index) in _events)
        {
            switch (kind)
            {
                case EventKind.Echo:
                    echo?.Invoke(_echo[index]);
                    break;
                case EventKind.Warning:
                    warn?.Invoke(_diagnostics[index]);
                    break;
                default:
                    unsupported?.Invoke(_unsupported[index]);
                    break;
            }
        }
    }

    /// <summary>
    ///     Records the suppression notice the first time a warning is dropped because of the cap.
    /// </summary>
    /// <param name="location">The location of the first dropped warning.</param>
    private void ReportSuppression(ScadSourceLocation location)
    {
        if (_suppressionReported)
        {
            return;
        }

        _suppressionReported = true;
        AddWarning(new ScadDiagnostic($"Further diagnostics suppressed (limit {_limits.MaxDiagnostics})", location));
    }

    /// <summary>
    ///     Appends a warning to the buffers.
    /// </summary>
    /// <param name="diagnostic">The warning.</param>
    private void AddWarning(ScadDiagnostic diagnostic)
    {
        _events.Add((EventKind.Warning, _diagnostics.Count));
        _diagnostics.Add(diagnostic);
    }
}
