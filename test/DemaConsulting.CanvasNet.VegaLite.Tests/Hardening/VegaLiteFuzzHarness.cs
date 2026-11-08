using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     The result of one fuzz case.
/// </summary>
/// <param name="Outcome">How the case ended: <c>rendered</c>, <c>spec-error</c> (the specification was rejected when it was opened), <c>render-error</c> (a limit or an invalid value was reported when it was laid out or painted) or <c>unsupported</c> (strict mode refused the chart).</param>
/// <param name="Failure">The description of what is wrong with the library's behavior, or <see langword="null"/> when the case passed the oracle.</param>
/// <param name="Detail">The first line of the message that rejected the case (used for the statistics of the summary), or <see langword="null"/> when the case rendered.</param>
internal sealed record FuzzCaseResult(string Outcome, string? Failure, string? Detail = null);

/// <summary>
///     Runs one specification through the library and checks the oracle of the hardening tests: only documented exception types escape (and the ones that must carry a
///     location do), the call does not hang, memory stays bounded by the limits, and two runs of the same specification give the same outcome (same pixels and same reports,
///     or the same error).
/// </summary>
internal static class VegaLiteFuzzHarness
{
    /// <summary>
    ///     The most bytes one case may allocate on its thread, counted gross (garbage included, over the parse and the four passes of measuring and rendering): a rendering allocates about
    ///     thirty kilobytes per painted symbol (short-lived), so the bound is a runaway detector, and the time limit of the case is the second guard.
    /// </summary>
    public const long MaxAllocatedBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>The natural size (in pixels) up to which the chart is rendered at its natural size; larger charts are rendered into a fixed canvas.</summary>
    private const long NaturalRenderPixels = 1_000_000;

    /// <summary>The tight limits of a fuzz case: they keep a case fast and bound its memory whatever the specification asks for.</summary>
    public static readonly VegaLiteLimits CaseLimits = new()
    {
        MaxSourceLength = 1_000_000,
        MaxDataRows = 20_000,
        MaxSceneItems = 50_000,
        MaxEvaluationSteps = 5_000_000,
        MaxTicks = 200,
        MaxScaleDomainSize = 5_000,
        MaxViews = 60,
        MaxImages = 8,
        MaxPaintedPixels = 50_000_000,
    };

    /// <summary>
    ///     Runs one case.
    /// </summary>
    /// <param name="spec">The text of the specification.</param>
    /// <param name="baseDirectory">The folder that relative data files are read from.</param>
    /// <param name="strict">Whether unsupported features throw.</param>
    /// <param name="timeout">The longest time one case may take.</param>
    /// <param name="cancellationToken">The token of the test.</param>
    /// <returns>The result.</returns>
    public static FuzzCaseResult Run(string spec, string baseDirectory, bool strict, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);

        VegaLiteDocument document;
        try
        {
            document = VegaLiteDocument.Parse(spec, new VegaLiteOpenOptions { Limits = CaseLimits, BaseDirectory = baseDirectory });
        }
        catch (VegaLiteSpecException ex)
        {
            return new FuzzCaseResult("spec-error", ex.Location.IsKnown ? null : "Parse threw a VegaLiteSpecException without a location: " + ex.Message, ex.Message);
        }
        catch (InvalidDataException)
        {
            return new FuzzCaseResult("spec-error", null);
        }
        catch (Exception ex)
        {
            return new FuzzCaseResult("crash", "Parse threw an undocumented exception: " + ex);
        }

        var first = Attempt(document, strict, source.Token);
        var second = first.Failure is null ? Attempt(document, strict, source.Token) : first;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var failure = first.Failure ?? second.Failure;
        if (failure is null && first.Signature != second.Signature)
        {
            failure = "Two runs of the same specification gave different results:\n--- first\n" + first.Signature + "\n--- second\n" + second.Signature;
        }

        if (failure is null && allocated > MaxAllocatedBytes)
        {
            failure = $"The case allocated {allocated:N0} bytes (more than {MaxAllocatedBytes:N0}) although the limits are tight; elapsed {stopwatch.ElapsedMilliseconds} ms.";
        }

        return new FuzzCaseResult(first.Outcome, failure, first.Outcome == "rendered" ? null : first.Signature.Split('\n')[0]);
    }

    /// <summary>
    ///     Hashes the pixels of a surface together with its size.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The hash as lower case hexadecimal text.</returns>
    public static string PixelHash(Surface surface)
    {
        var header = System.Text.Encoding.ASCII.GetBytes(surface.Width.ToString(CultureInfo.InvariantCulture) + "x" + surface.Height.ToString(CultureInfo.InvariantCulture) + ":");
        var bytes = new byte[header.Length + (surface.Width * surface.Height * 4)];
        header.CopyTo(bytes, 0);
        for (var y = 0; y < surface.Height; y++)
        {
            surface.GetRowSpanBytes(y).CopyTo(bytes.AsSpan(header.Length + (y * surface.Width * 4)));
        }

        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    ///     Measures and renders a document once and describes what happened.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="strict">Whether unsupported features throw.</param>
    /// <param name="token">The token that stops the work.</param>
    /// <returns>The description.</returns>
    private static AttemptResult Attempt(VegaLiteDocument document, bool strict, CancellationToken token)
    {
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Limits = CaseLimits,
            ThrowOnUnsupportedFeature = strict,
            WarningHandler = d => reports.Add("W " + d),
            UnsupportedFeatureHandler = f => reports.Add("U " + f),
        };

        try
        {
            var size = document.Measure(options, token);
            string pixels;
            if ((long)size.Width * size.Height <= NaturalRenderPixels)
            {
                using var surface = document.Render(options, token);
                if (surface.Width != size.Width || surface.Height != size.Height)
                {
                    return new AttemptResult("crash", "Measure said " + size + " but Render made " + surface.Width + "x" + surface.Height, null);
                }

                pixels = PixelHash(surface);
            }
            else
            {
                using var surface = document.Render(400, 300, options, token);
                pixels = PixelHash(surface);
            }

            return new AttemptResult("rendered", "ok " + size + " " + pixels + "\n" + string.Join("\n", reports), null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new AttemptResult("hang", string.Empty, "The case did not finish in time (a hang, or a stage that does not poll the cancellation token).");
        }
        catch (VegaLiteRenderException ex)
        {
            return Rejected("render-error", ex, ex.Location, reports);
        }
        catch (VegaLiteSpecException ex)
        {
            return Rejected("render-error", ex, ex.Location, reports);
        }
        catch (VegaLiteUnsupportedFeatureException ex) when (strict)
        {
            return Rejected("unsupported", ex, ex.Location, reports);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "options")
        {
            // Documented: the natural size times the scale is not a size a surface can have
            return new AttemptResult("render-error", "size-limit " + ex.Message + "\n" + string.Join("\n", reports), null);
        }
        catch (Exception ex)
        {
            return new AttemptResult("crash", string.Empty, "An undocumented exception escaped Measure or Render: " + ex);
        }
    }

    /// <summary>
    ///     Describes a documented exception, which must carry a location.
    /// </summary>
    /// <param name="outcome">The outcome name.</param>
    /// <param name="ex">The exception.</param>
    /// <param name="location">The location of the exception.</param>
    /// <param name="reports">The reports delivered before it.</param>
    /// <returns>The description.</returns>
    private static AttemptResult Rejected(string outcome, Exception ex, VegaLiteLocation location, List<string> reports) =>
        new(outcome, ex.GetType().Name + ": " + ex.Message + "\n" + string.Join("\n", reports), location.IsKnown ? null : ex.GetType().Name + " without a location: " + ex.Message);

    /// <summary>
    ///     What one measure-and-render attempt did.
    /// </summary>
    /// <param name="Outcome">The outcome name.</param>
    /// <param name="Signature">The text that must be the same when the attempt is repeated.</param>
    /// <param name="Failure">What is wrong, or <see langword="null"/>.</param>
    private sealed record AttemptResult(string Outcome, string Signature, string? Failure);
}
