using System.Globalization;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl;

/// <content>
///     The measuring and rendering entry points of the document and the pipeline behind them.
/// </content>
/// <remarks>
///     Pipeline of one call: the reports found while reading are recorded first; the style of the <c>math</c> element is
///     computed from the options, the layout engine lays the tree out into boxes over the bundled math font, and the
///     painter draws them. The picture is the union of the content box of the <c>math</c> element and the ink of
///     everything painted grown by an antialiasing allowance (one CSS pixel, and at least one device pixel), rounded out
///     to whole pixels, so ink that overhangs its box (an italic f, a stretched fence, the tail of a radical) is never
///     clipped and never reaches the outermost rows and columns of the picture.
///     The reports of the call are buffered and delivered once, on the calling thread, after the work.
/// </remarks>
public sealed partial class MathMlDocument
{
    /// <summary>A tolerance that keeps 20.0000000001 from becoming 21 when the picture is rounded out.</summary>
    private const double RoundingTolerance = 1e-6;

    /// <summary>
    ///     Computes the size and the baseline the equation renders at without drawing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The result is the size of the surface that <see cref="Render(MathMlRenderOptions?, CancellationToken)"/>
    ///         returns for the same options: the natural size of the equation in CSS pixels at
    ///         <see cref="MathMlRenderOptions.FontSize"/> multiplied by <see cref="MathMlRenderOptions.Scale"/> and rounded
    ///         out, with the row of the alphabetic baseline, so that a caller can place the picture in a line of text. The
    ///         natural size is the layout box of the equation united with its ink grown by an antialiasing allowance of one
    ///         CSS pixel (at least one device pixel), so a side where ink reaches the edge of the layout box grows by up to
    ///         that allowance; the baseline row stays the row of the origin of the <c>math</c> box. The
    ///         equation is laid out, and the same warnings and unsupported-feature reports are delivered as for rendering,
    ///         in the same order and on the calling thread. Nothing is drawn.
    ///     </para>
    ///     <para>
    ///         The document is immutable, so the method can be called from several threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels of the surface that <c>Render</c> would return, and its baseline.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="MathMlRenderException">Thrown, located, when a limit is exceeded (including a picture larger than <see cref="Surface.MaxDimension"/> on a side) or the layout fails.</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown when <see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the equation uses an unsupported feature.</exception>
    public MathMlSize Measure(MathMlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MathMlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, box =>
        {
            var frame = Frame(box, effective.Scale);
            return new MathMlSize(frame.Width, frame.Height, frame.Baseline);
        });
    }

    /// <summary>
    ///     Renders the equation onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="MathMlRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with <see cref="MathMlRenderOptions.Background"/>, or left transparent when it is null,
    ///         and the equation is drawn on it. Rendering is deterministic: two renders of the same document and options
    ///         have identical pixels, for every degree of parallelism and on every .NET version, because only the bundled
    ///         fonts are used.
    ///     </para>
    ///     <para>
    ///         Warnings and unsupported-feature reports go through the handlers of the options, in the order they were
    ///         found, on the calling thread, once, before the method returns or throws. The caller owns and must dispose the
    ///         returned surface. The document holds no state between calls, so it can be rendered from several threads at
    ///         once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new surface of the size that <see cref="Measure"/> reports.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="MathMlRenderException">Thrown, located, when a limit is exceeded (including a picture larger than <see cref="Surface.MaxDimension"/> on a side) or the layout fails.</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown when <see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the equation uses an unsupported feature.</exception>
    public Surface Render(MathMlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MathMlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, box =>
        {
            var frame = Frame(box, effective.Scale);
            var placement = new MathPlacement(effective.Scale, frame.OriginX, frame.OriginY);
            return Paint(box, frame.Width, frame.Height, placement, effective, cancellationToken);
        });
    }

    /// <summary>
    ///     Renders the equation onto a new surface of an exact size: the natural picture is scaled uniformly to fit and
    ///     centered over the background.
    /// </summary>
    /// <remarks>
    ///     Use this overload when the destination size is fixed (a page, a slide, a command-line <c>--width</c> and
    ///     <c>--height</c>). The equation keeps its aspect ratio; the margins are filled with the background (transparent
    ///     by default). <see cref="MathMlRenderOptions.Scale"/> is not used, because the size decides the scale. Events,
    ///     determinism, ownership and thread safety are as for
    ///     <see cref="Render(MathMlRenderOptions?, CancellationToken)"/>. The size is validated before any other work.
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="MathMlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown when <see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the equation uses an unsupported feature.</exception>
    public Surface Render(int width, int height, MathMlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the cheap arguments before any work
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }

        var effective = options ?? new MathMlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, box =>
            Paint(box, width, height, Fit(box, width, height), effective, cancellationToken));
    }

    /// <summary>
    ///     Computes the natural size of the picture without the <see cref="Surface.MaxDimension"/> check (internal; the
    ///     image source scales a picture larger than a surface down to the size it is placed at, and only its aspect ratio
    ///     matters then). The reports are delivered as for <see cref="Measure"/>.
    /// </summary>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels at <see cref="MathMlRenderOptions.Scale"/>, rounded out, each side at least 1 and at most <see cref="int.MaxValue"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="MathMlRenderException">Thrown, located, when a limit is exceeded, the layout fails, or the size is not finite.</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown when <see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the equation uses an unsupported feature.</exception>
    internal (int Width, int Height) MeasureNatural(MathMlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MathMlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, box =>
        {
            var (_, _, width, height) = RoundedExtent(box, effective.Scale);
            return ((int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue));
        });
    }

    /// <summary>
    ///     Lays the equation out under options and returns its box tree without drawing it (internal; the model the tests
    ///     and the reference comparison read). The reports are delivered as for <see cref="Measure"/>.
    /// </summary>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The box of the <c>math</c> element, in CSS pixels at the font size of the options.</returns>
    internal MathBox LayoutBox(MathMlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MathMlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, box => box);
    }

    /// <summary>
    ///     Gets the extent of the picture of a box in CSS pixels at a scale: its content box united with its ink grown on
    ///     every side by the antialiasing allowance (<see cref="InkAllowance(double)"/>), so that no ink, antialiasing
    ///     included, reaches the outermost rows and columns of pixels.
    /// </summary>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="scale">The device pixels per CSS pixel; greater than zero.</param>
    /// <returns>The extent relative to the origin of the box.</returns>
    internal static MathRect PictureExtent(MathBox box, double scale) => PictureExtent(box, box.InkBounds(), InkAllowance(scale));

    /// <summary>
    ///     Gets the antialiasing allowance around the ink of a picture at a scale: one CSS pixel, and at least one device
    ///     pixel at a scale below 1.
    /// </summary>
    /// <param name="scale">The device pixels per CSS pixel; greater than zero.</param>
    /// <returns>The allowance in CSS pixels.</returns>
    internal static double InkAllowance(double scale) => Math.Max(1, 1 / scale);

    /// <summary>
    ///     Gets the extent of the picture of a box with its ink and an allowance around the ink.
    /// </summary>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="ink">The ink bounds of the box, or null when it paints nothing.</param>
    /// <param name="allowance">The allowance around the ink in CSS pixels.</param>
    /// <returns>The extent relative to the origin of the box.</returns>
    private static MathRect PictureExtent(MathBox box, MathRect? ink, double allowance)
    {
        var content = box.ContentRect;
        if (ink is not { } bounds)
        {
            return content;
        }

        var grown = new MathRect(bounds.X - allowance, bounds.Y - allowance, bounds.Width + (2 * allowance), bounds.Height + (2 * allowance));
        return content.Union(grown);
    }

    /// <summary>
    ///     Decides whether an exception that escaped the layout or the painting is a defect to report as a located render
    ///     failure: malformed font data (<see cref="InvalidDataException"/>) or one of the runtime's programming-error
    ///     exceptions, never the documented failures (<see cref="MathMlRenderException"/>,
    ///     <see cref="MathMlUnsupportedFeatureException"/>) nor <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>True when the exception is a defect.</returns>
    internal static bool IsLayoutDefect(Exception ex) =>
        ex is InvalidDataException
            or NullReferenceException
            or IndexOutOfRangeException
            or KeyNotFoundException
            or InvalidCastException
            or ArgumentException
            or ArithmeticException
            or (InvalidOperationException and not MathMlRenderException and not ObjectDisposedException);

    /// <summary>
    ///     Computes the pixel frame of the picture at a scale: its size and the device position of the origin of the
    ///     <c>math</c> box, which lies on a whole pixel row so that the baseline is exact.
    /// </summary>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="MathMlRenderException">Thrown when the picture has no finite size or is larger than a surface can be.</exception>
    private PictureFrame Frame(MathBox box, double scale)
    {
        var (left, top, width, height) = RoundedExtent(box, scale);
        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            throw new MathMlRenderException(
                MathMlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the equation measures {width} by {height} pixels; each side must be at most {Surface.MaxDimension} (Surface.MaxDimension)"),
                Root.Location,
                "reduce MathMlRenderOptions.Scale or FontSize, or the lengths of the equation");
        }

        var baseline = (int)Math.Clamp(-top, 0, height);
        return new PictureFrame((int)width, (int)height, -left, -top, baseline);
    }

    /// <summary>
    ///     Rounds the extent of the picture of a box at a scale out to whole pixels around the origin.
    /// </summary>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The left and top edges and the size, each side at least 1.</returns>
    /// <exception cref="MathMlRenderException">Thrown when the picture has no finite size.</exception>
    private (double Left, double Top, double Width, double Height) RoundedExtent(MathBox box, double scale)
    {
        var extent = PictureExtent(box, scale);
        var left = Math.Floor((extent.X * scale) + RoundingTolerance);
        var top = Math.Floor((extent.Y * scale) + RoundingTolerance);
        var right = Math.Ceiling((extent.Right * scale) - RoundingTolerance);
        var bottom = Math.Ceiling((extent.Bottom * scale) - RoundingTolerance);
        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);
        if (!double.IsFinite(width) || !double.IsFinite(height))
        {
            throw new MathMlRenderException(MathMlDiagnosticCodes.RenderFailed, "the equation has no finite size", Root.Location);
        }

        return (left, top, width, height);
    }

    /// <summary>
    ///     Computes the placement that fits the picture of a box into a size, centered.
    /// </summary>
    /// <remarks>
    ///     The extent depends on the scale through the antialiasing allowance, so the scale is chosen first with the
    ///     allowance of one CSS pixel (correct at a scale of 1 or more); below 1 the allowance is one device pixel, which
    ///     widens each side by at most two device pixels, so the scale that fits the extent without allowance into the size
    ///     less two pixels fits the extent with it. A size of two pixels or less fits the extent without allowance.
    /// </remarks>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The placement.</returns>
    private static MathPlacement Fit(MathBox box, int width, int height)
    {
        var ink = box.InkBounds();
        var extent = PictureExtent(box, ink, InkAllowance(1));
        var scale = Ratio(extent, width, height);
        if (scale < 1)
        {
            var bare = PictureExtent(box, ink, 0);
            var reduced = Ratio(bare, width - 2, height - 2);
            if (reduced > 0)
            {
                scale = reduced;
                extent = PictureExtent(box, ink, InkAllowance(scale));
            }
            else
            {
                scale = Ratio(bare, width, height);
                extent = bare;
            }
        }

        var originX = ((width - (extent.Width * scale)) / 2) - (extent.X * scale);
        var originY = ((height - (extent.Height * scale)) / 2) - (extent.Y * scale);
        return new MathPlacement(scale, originX, originY);
    }

    /// <summary>
    ///     Computes the scale that fits an extent into a size: the smaller of the two ratios, or 1 for an empty extent (an
    ///     empty picture keeps its natural scale).
    /// </summary>
    /// <param name="extent">The extent in CSS pixels.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The scale; zero or less when the size is not positive.</returns>
    private static double Ratio(MathRect extent, double width, double height) =>
        extent.Width > 0 && extent.Height > 0 ? Math.Min(width / extent.Width, height / extent.Height) : 1;

    /// <summary>
    ///     Paints a box onto a new surface.
    /// </summary>
    /// <param name="box">The box of the <c>math</c> element.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="placement">The placement.</param>
    /// <param name="options">The options of the call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surface; the caller owns it.</returns>
    private static Surface Paint(MathBox box, int width, int height, MathPlacement placement, MathMlRenderOptions options, CancellationToken cancellationToken)
    {
        var surface = new Surface(width, height);
        try
        {
            MathMlPainter.Paint(surface, box, placement, options.Background, options.Limits.MaxPaintedGlyphs, cancellationToken);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Runs the pipeline and delivers the recorded reports around the result.
    /// </summary>
    /// <remarks>
    ///     Reports are delivered exactly once on the calling thread. After a failure the reports recorded before it are still
    ///     delivered, but a failing handler cannot hide the failure; after success a failing handler propagates and must not
    ///     leak the result. A defect of the layout or the painting becomes a located <see cref="MathMlRenderException"/>
    ///     (<see cref="MathMlDiagnosticCodes.RenderFailed"/>) with the original exception inside. Internal rather than
    ///     private so that a test can drive a defect through it.
    /// </remarks>
    /// <typeparam name="T">The kind of result.</typeparam>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="finish">Produces the result from the box of the <c>math</c> element.</param>
    /// <returns>The result.</returns>
    internal T Run<T>(MathMlRenderOptions options, CancellationToken cancellationToken, Func<MathBox, T> finish)
    {
        var sink = new MathMlReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            // The reports of reading come first, in text order (strict mode throws at the first unsupported one)
            foreach (var report in Reports)
            {
                report.Replay(sink);
            }

            try
            {
                var engine = new LayoutEngine(MathFont.Default, options.Limits, sink, cancellationToken);
                var style = engine.Styles.ResolveRoot(Root, options.FontSize, options.Color, options.Display);
                result = RunWithLayoutStack(() => finish(engine.Layout(Root, style)));
            }
            catch (Exception ex) when (IsLayoutDefect(ex))
            {
                throw new MathMlRenderException(
                    MathMlDiagnosticCodes.RenderFailed,
                    $"the layout failed unexpectedly ({ex.GetType().Name}: {ex.Message})",
                    Root.Location,
                    "simplify the equation, and report it to the CanvasNet project",
                    ex);
            }
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
    ///     The stack size of the layout thread. The layout recurses a few frames per nesting level, and the default
    ///     stack of a thread differs by platform (on macOS .NET 8 it overflowed at about 240 levels), so the layout gets
    ///     a stack far larger than <see cref="MathMlLimits.MaxNestingDepth"/> at its ceiling of 1,024 levels needs;
    ///     operating systems commit thread stack memory only as it is used.
    /// </summary>
    private const int LayoutStackSize = 64 * 1024 * 1024;

    /// <summary>
    ///     Runs the layout and painting on a thread with a stack of <see cref="LayoutStackSize"/>, so every nesting depth
    ///     the limits accept fits on every platform, and rethrows its exception on the calling thread unchanged.
    /// </summary>
    /// <typeparam name="T">The kind of result.</typeparam>
    /// <param name="work">The work.</param>
    /// <returns>The result of the work.</returns>
    private static T RunWithLayoutStack<T>(Func<T> work)
    {
        // A platform without threads runs the work on the calling thread
        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            return work();
        }

        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(
            () =>
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
            },
            LayoutStackSize)
        {
            IsBackground = true,
            Name = "CanvasNet MathML layout",
        };
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }

    /// <summary>
    ///     The pixel frame of a picture.
    /// </summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    /// <param name="OriginX">The device x of the origin of the <c>math</c> box.</param>
    /// <param name="OriginY">The device y of the baseline of the <c>math</c> box (a whole row).</param>
    /// <param name="Baseline">The baseline row, from 0 to the height.</param>
    private readonly record struct PictureFrame(int Width, int Height, double OriginX, double OriginY, int Baseline);
}
