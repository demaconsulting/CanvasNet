// Portions derived from Vega-Lite (BSD-3-Clause): the default view size and padding below; see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The measuring and rendering entry points of the document and the (foundation-shell) pipeline behind them.
/// </content>
/// <remarks>
///     Foundation shell: the pipeline renders the empty view of a chart (its background at the natural size of a
///     default view plus padding) and reports the marks and remote data it cannot draw yet. The private pipeline
///     methods at the end of this file are the seam that the specification, data, scale, layout, mark and painting
///     stages replace; the public methods, their validation order and their event delivery are the stable contract.
/// </remarks>
public sealed partial class VegaLiteDocument
{
    /// <summary>The default width and height of the data rectangle of a view in pixels (Vega-Lite's default view size).</summary>
    private const int DefaultViewSize = 200;

    /// <summary>The default padding around the chart in pixels (Vega-Lite's default padding).</summary>
    private const int DefaultPadding = 5;

    /// <summary>The default background: opaque white.</summary>
    private static readonly Rgba32 DefaultBackground = new(255, 255, 255, 255);

    /// <summary>
    ///     Computes the size the chart renders at without drawing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The result is the size of the surface that <see cref="Render(VegaLiteRenderOptions?, CancellationToken)"/>
    ///         returns for the same options: the natural size of the chart (its data rectangle, axes, legends, title
    ///         and padding) multiplied by <see cref="VegaLiteRenderOptions.Scale"/> and rounded to whole pixels.
    ///         The same checks run and the same warnings and unsupported-feature reports are delivered as for
    ///         rendering, in the same order and on the calling thread, so measuring first never hides a problem that
    ///         rendering would report. Nothing is drawn.
    ///     </para>
    ///     <para>
    ///         The scaled size is validated before any other work. The document is immutable, so the method can be
    ///         called from several threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels of the surface that <c>Render</c> would return.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when the chart cannot be laid out.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public VegaLiteSize Measure(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var size = ValidateScaledSize(effective);
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, _ => size);
    }

    /// <summary>
    ///     Renders the chart onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="VegaLiteRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with the background (<see cref="VegaLiteRenderOptions.Background"/>, else white)
    ///         and the chart is drawn on it. Rendering is deterministic: two renders of the same document and options
    ///         are byte-identical, for every degree of parallelism and on every machine, because only the bundled
    ///         fonts are used unless <see cref="VegaLiteRenderOptions.Fonts"/> says otherwise.
    ///     </para>
    ///     <para>
    ///         Warnings and unsupported-feature reports go through the handlers of the options, in the order they
    ///         were found, on the calling thread, once, before the method returns or throws. The scaled size and the
    ///         parameter names are validated before any other work. The caller owns and must dispose the returned
    ///         surface; it is not retained by the document.
    ///     </para>
    ///     <para>
    ///         The document is immutable and holds no state between calls, so it can be rendered from several
    ///         threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new surface of the size that <see cref="Measure"/> reports.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when data is missing or invalid or a limit is exceeded.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public Surface Render(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var size = ValidateScaledSize(effective);
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, plan => Paint(plan, size.Width, size.Height, cancellationToken));
    }

    /// <summary>
    ///     Renders the chart onto a new surface of an exact size: the natural chart is scaled uniformly to fit and
    ///     centered over the background.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this overload when the destination size is fixed (a page, a slide, a command-line
    ///         <c>--width</c>/<c>--height</c>). The chart keeps its aspect ratio: it is scaled by the largest factor
    ///         that fits inside <paramref name="width"/> by <paramref name="height"/> and centered, and the margins
    ///         that remain are filled with the background (<see cref="VegaLiteRenderOptions.Background"/>, else
    ///         white). <see cref="VegaLiteRenderOptions.Scale"/> is not used, because the size decides the scale.
    ///     </para>
    ///     <para>
    ///         Events, determinism, ownership and thread safety are as for
    ///         <see cref="Render(VegaLiteRenderOptions?, CancellationToken)"/>. The size and the parameter names are
    ///         validated before any other work.
    ///     </para>
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when data is missing or invalid or a limit is exceeded.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public Surface Render(int width, int height, VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the cheap arguments before any work: the size first, then the options
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }

        var effective = options ?? new VegaLiteRenderOptions();
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, plan => Paint(plan, width, height, cancellationToken));
    }

    /// <summary>
    ///     Runs the pipeline and delivers the recorded events around the result.
    /// </summary>
    /// <remarks>
    ///     Events are delivered exactly once on the calling thread. After a failure the events recorded before it are
    ///     still delivered, but a failing handler cannot hide the failure; after success a failing handler
    ///     propagates and must not leak the result.
    /// </remarks>
    /// <typeparam name="T">The kind of result.</typeparam>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="finish">Produces the result from the plan of the chart.</param>
    /// <returns>The result.</returns>
    private T Run<T>(VegaLiteRenderOptions options, CancellationToken cancellationToken, Func<ChartPlan, T> finish)
    {
        var sink = new VegaLiteReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            result = finish(BuildPlan(options, sink, cancellationToken));
        }
        catch
        {
            sink.DeliverIgnoringHandlerFailures(options.WarningHandler, options.UnsupportedFeatureHandler);
            throw;
        }

        try
        {
            sink.Deliver(options.WarningHandler, options.UnsupportedFeatureHandler);
        }
        catch
        {
            (result as IDisposable)?.Dispose();
            throw;
        }

        return result;
    }

    /// <summary>
    ///     Validates that the natural size times the scale is a size a surface can have.
    /// </summary>
    /// <param name="options">The effective options.</param>
    /// <returns>The scaled size in whole pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a side is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static VegaLiteSize ValidateScaledSize(VegaLiteRenderOptions options)
    {
        var natural = NaturalSize();
        var width = ScaleDimension(natural.Width, options.Scale);
        var height = ScaleDimension(natural.Height, options.Scale);
        if (width < 1 || width > Surface.MaxDimension || height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Scale,
                $"The scale gives a size of {width} by {height}; each side must be between 1 and {Surface.MaxDimension}.");
        }

        return new VegaLiteSize((int)width, (int)height);
    }

    /// <summary>
    ///     Multiplies a dimension by the scale and rounds half away from zero.
    /// </summary>
    /// <param name="dimension">The natural dimension.</param>
    /// <param name="scale">The scale factor.</param>
    /// <returns>The scaled dimension, as a double so that huge results cannot overflow.</returns>
    private static double ScaleDimension(int dimension, double scale) =>
        Math.Round(dimension * scale, MidpointRounding.AwayFromZero);

    /// <summary>
    ///     Checks that every parameter override names a parameter the document declares.
    /// </summary>
    /// <param name="options">The effective options.</param>
    /// <exception cref="ArgumentException">Thrown when an override names an unknown parameter.</exception>
    private void ValidateParameters(VegaLiteRenderOptions options)
    {
        if (options.Parameters is null)
        {
            return;
        }

        // Ordinal order makes the reported name independent of the caller's dictionary order
        var known = new HashSet<string>(ParameterNames, StringComparer.Ordinal);
        var unknown = options.Parameters.Keys.Where(name => !known.Contains(name)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
        {
            throw new ArgumentException($"The specification declares no parameter named '{unknown}'.", nameof(options));
        }
    }

    /// <summary>
    ///     Gets the natural size of the chart: the size of the surface at a scale of one.
    /// </summary>
    /// <remarks>
    ///     Foundation shell: an empty view is a default data rectangle plus padding on every side (210 by 210
    ///     pixels). The layout stage replaces this with the measured size of the axes, legends and title.
    /// </remarks>
    /// <returns>The natural size.</returns>
    private static VegaLiteSize NaturalSize() =>
        new(DefaultViewSize + (2 * DefaultPadding), DefaultViewSize + (2 * DefaultPadding));

    /// <summary>
    ///     Builds the plan of the chart: everything the painter needs, and the reports about what cannot be drawn.
    /// </summary>
    /// <remarks>
    ///     Foundation shell: the plan is the natural size and the background; the reports are the parse-time warnings,
    ///     empty inline data, remote data (never fetched; the dataset is treated as empty) and every mark, because
    ///     marks are not drawn yet. Reports are recorded in that order, which is the document order within each kind.
    /// </remarks>
    /// <param name="options">The effective options.</param>
    /// <param name="sink">The sink that records the reports.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildPlan(VegaLiteRenderOptions options, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Versions other than 5 and 6 are rendered with the version 6 defaults but must be reported
        var version = _info.SchemaMajorVersion;
        if (_info.SchemaUrl is not null && version is not (5 or 6))
        {
            var found = version is null ? "does not name a Vega-Lite version" : $"names Vega-Lite version {version}";
            sink.Warn($"vl-schema-version: The $schema {found}; the specification is interpreted with the Vega-Lite 6 defaults.", _info.SchemaLocation);
        }

        // Data: inline data without rows draws an empty chart, and remote data is never fetched
        foreach (var reference in _info.DataReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reference.Kind == VegaLiteDataKind.Inline && reference.RowCount == 0)
            {
                sink.Warn("vl-data-empty: The inline data has no rows; the chart is empty.", reference.Location);
            }
            else if (reference.Kind == VegaLiteDataKind.Url && reference.Url is not null && VegaLiteSpecScanner.IsSchemeBearingUrl(reference.Url))
            {
                sink.Unsupported(
                    "vl-data-remote-url",
                    "Remote data is never fetched; download the file next to the specification and refer to it by a relative path. The dataset is treated as empty.",
                    reference.Location);
            }
        }

        // Marks are recognized but not drawn by the foundation shell, so each one is reported with its pointer
        foreach (var mark in _info.Marks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sink.Unsupported($"vl-mark-{mark.Type}", $"The mark type '{mark.Type}' is not rendered yet.", mark.Location);
        }

        return new ChartPlan(NaturalSize(), options.Background ?? DefaultBackground);
    }

    /// <summary>
    ///     Paints the plan onto a new surface of the given size.
    /// </summary>
    /// <remarks>
    ///     Foundation shell: the whole surface is the background. For an explicit size the natural chart is fitted
    ///     and centered, which for an empty view is visually the same as filling the surface; the painting stage
    ///     replaces this with the real fit (uniform scale, centered content, background margins).
    /// </remarks>
    /// <param name="plan">The plan.</param>
    /// <param name="width">The width of the surface in pixels.</param>
    /// <param name="height">The height of the surface in pixels.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The surface; the caller owns it.</returns>
    private static Surface Paint(ChartPlan plan, int width, int height, CancellationToken cancellationToken)
    {
        var surface = new Surface(width, height);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            surface.Clear(plan.Background);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     What the painter needs to draw the chart.
    /// </summary>
    /// <param name="Size">The natural size of the chart in pixels at a scale of one.</param>
    /// <param name="Background">The background color.</param>
    private readonly record struct ChartPlan(VegaLiteSize Size, Rgba32 Background);
}
