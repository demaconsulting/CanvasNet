using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     Collects the warnings and unsupported-feature reports of one call, in the order they occur, and delivers them to
///     the caller's handlers once the work is done.
/// </summary>
/// <remarks>
///     <para>
///         Buffering instead of calling the handlers immediately keeps delivery on the thread that made the call, exactly
///         once and in a deterministic order, whatever the pipeline does internally, and lets the library decide what to do
///         when a handler throws: after a successful call the handler's exception propagates, after a failed call it is
///         ignored so that the original failure is the one the caller sees.
///     </para>
///     <para>
///         Identical reports (same code, message, location and hint) are recorded once. At most
///         <see cref="MermaidLimits.MaxDiagnostics"/> reports are kept; the first one beyond the cap is replaced by a single
///         <see cref="MermaidDiagnosticCodes.DiagnosticsSuppressed"/> notice and the rest are dropped, so a hostile diagram
///         cannot grow the buffer without bound. Strict mode is unaffected by the cap: the first unsupported feature is
///         always recorded, so it is delivered to the handler before the exception that it causes is thrown. An instance is
///         used by one call on one thread.
///     </para>
/// </remarks>
internal sealed class MermaidReportSink
{
    /// <summary>The recorded reports in order; each is a warning or an unsupported feature.</summary>
    private readonly List<(MermaidDiagnostic? Warning, MermaidUnsupportedFeature? Unsupported)> _events = [];

    /// <summary>The warnings recorded so far, for de-duplication.</summary>
    private readonly HashSet<MermaidDiagnostic> _seenWarnings = [];

    /// <summary>The unsupported features recorded so far, for de-duplication.</summary>
    private readonly HashSet<MermaidUnsupportedFeature> _seenUnsupported = [];

    /// <summary>The maximum number of reports that are kept.</summary>
    private readonly int _maxReports;

    /// <summary>Whether the first unsupported feature throws.</summary>
    private readonly bool _throwOnUnsupported;

    /// <summary>Whether the suppression notice has been recorded.</summary>
    private bool _suppressed;

    /// <summary>
    ///     Initializes a sink.
    /// </summary>
    /// <param name="maxReports">The maximum number of reports to keep; at least one.</param>
    /// <param name="throwOnUnsupported">Whether the first unsupported feature throws.</param>
    public MermaidReportSink(int maxReports, bool throwOnUnsupported)
    {
        _maxReports = maxReports;
        _throwOnUnsupported = throwOnUnsupported;
    }

    /// <summary>
    ///     Gets the number of reports that are currently recorded.
    /// </summary>
    public int Count => _events.Count;

    /// <summary>
    ///     Records a warning.
    /// </summary>
    /// <param name="code">The stable <c>mermaid-*</c> code.</param>
    /// <param name="message">The description of the problem.</param>
    /// <param name="location">The location it is about.</param>
    /// <param name="hint">The suggested fix, or null.</param>
    public void Warn(string code, string message, MermaidLocation location, string? hint = null)
    {
        var warning = new MermaidDiagnostic(code, message, location) { Hint = hint };
        if (_seenWarnings.Add(warning))
        {
            Add((warning, null));
        }
    }

    /// <summary>
    ///     Records an unsupported feature and, in strict mode, throws for it.
    /// </summary>
    /// <param name="feature">The stable <c>mermaid-*</c> identifier.</param>
    /// <param name="message">The description.</param>
    /// <param name="location">The location of the construct.</param>
    /// <param name="hint">The suggested way forward, or null.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown when strict mode is on (after the report is recorded).</exception>
    public void Unsupported(string feature, string message, MermaidLocation location, string? hint = null)
    {
        var report = new MermaidUnsupportedFeature(feature, message, location) { Hint = hint };
        if (_seenUnsupported.Add(report))
        {
            // Strict mode delivers the report that it throws for, even when the buffer is full of earlier reports
            Add((null, report), force: _throwOnUnsupported);
        }

        // Strict mode must stop at the first report even when the buffer is full
        if (_throwOnUnsupported)
        {
            throw new MermaidUnsupportedFeatureException(feature, message, location, hint);
        }
    }

    /// <summary>
    ///     Delivers the recorded reports, in order, to the handlers; an exception of a handler propagates.
    /// </summary>
    /// <param name="warningHandler">The warning handler, or null.</param>
    /// <param name="unsupportedHandler">The unsupported-feature handler, or null.</param>
    public void Deliver(Action<MermaidDiagnostic>? warningHandler, Action<MermaidUnsupportedFeature>? unsupportedHandler)
    {
        foreach (var (warning, unsupported) in _events)
        {
            if (warning is not null)
            {
                warningHandler?.Invoke(warning);
            }
            else if (unsupported is not null)
            {
                unsupportedHandler?.Invoke(unsupported);
            }
        }
    }

    /// <summary>
    ///     Delivers the recorded reports after a failure: each handler call is protected so that a failing handler cannot
    ///     hide the failure that is being reported.
    /// </summary>
    /// <param name="warningHandler">The warning handler, or null.</param>
    /// <param name="unsupportedHandler">The unsupported-feature handler, or null.</param>
    public void DeliverIgnoringHandlerFailures(Action<MermaidDiagnostic>? warningHandler, Action<MermaidUnsupportedFeature>? unsupportedHandler)
    {
        try
        {
            Deliver(warningHandler, unsupportedHandler);
        }
#pragma warning disable CA1031 // A handler's failure must not mask the failure that is already being reported
        catch (Exception)
#pragma warning restore CA1031
        {
            // Intentionally ignored: see the method documentation
        }
    }

    /// <summary>
    ///     Adds a report, applying the cap.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="force">Whether to keep the report beyond the cap (the report that strict mode throws for).</param>
    private void Add((MermaidDiagnostic?, MermaidUnsupportedFeature?) report, bool force = false)
    {
        if (force || _events.Count < _maxReports)
        {
            _events.Add(report);
            return;
        }

        // The first overflow leaves a single notice; everything after it is dropped
        if (!_suppressed)
        {
            _suppressed = true;
            _events.Add((
                new MermaidDiagnostic(
                    MermaidDiagnosticCodes.DiagnosticsSuppressed,
                    string.Create(CultureInfo.InvariantCulture, $"further diagnostics were suppressed after the limit of {_maxReports} (MermaidLimits.MaxDiagnostics)."),
                    default),
                null));
        }
    }
}
