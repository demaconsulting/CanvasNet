using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     Options that control one measure or render call of a <see cref="PlantUmlDocument"/>.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and calls.
///     </para>
///     <para>
///         <b>Reporting</b> is uniform: warnings go to <see cref="WarningHandler"/> and constructs that are recognized but
///         not supported go to <see cref="UnsupportedFeatureHandler"/>, once, in the order they were found, on the
///         thread that made the call, before the method returns or throws. <see cref="ThrowOnUnsupportedFeature"/> turns
///         the first unsupported construct into a <see cref="PlantUmlUnsupportedFeatureException"/>.
///     </para>
/// </remarks>
public sealed class PlantUmlRenderOptions
{
    /// <summary>The backing field of <see cref="Scale"/>.</summary>
    private readonly double _scale = 1;

    /// <summary>The backing field of <see cref="Limits"/>.</summary>
    private readonly PlantUmlLimits _limits = PlantUmlLimits.Default;

    /// <summary>
    ///     Gets the factor by which the natural size of the diagram is multiplied; 1 by default.
    /// </summary>
    /// <remarks>
    ///     The natural size is the size of PlantUML's SVG (the bounds of the drawing plus the document margins). The scale
    ///     is applied to the whole diagram, text included, so a larger scale yields a larger, equally sharp image rather
    ///     than a magnified one. The size that results (the natural size times the scale, rounded to whole pixels) must
    ///     lie within 1 and <see cref="Surface.MaxDimension"/> on both sides; this is checked when the document is measured
    ///     or rendered. The scale is not used by the overload of <c>Render</c> that takes an explicit size, which decides
    ///     the scale itself. A <c>scale</c> statement of the diagram multiplies this factor.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown on initialization when the value is not finite, is not greater than zero, or exceeds
    ///     <see cref="Surface.MaxDimension"/>.
    /// </exception>
    public double Scale
    {
        get => _scale;
        init
        {
            if (!double.IsFinite(value) || value <= 0 || value > Surface.MaxDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Scale),
                    value,
                    string.Create(CultureInfo.InvariantCulture, $"The scale must be a finite number greater than zero and at most {Surface.MaxDimension}."));
            }

            _scale = value;
        }
    }

    /// <summary>
    ///     Gets the color the surface is filled with before the diagram is drawn, or <see langword="null"/> (the default)
    ///     for the diagram's own background: white, as PlantUML draws it, or the color of
    ///     <c>skinparam backgroundColor</c> (<c>transparent</c> gives a transparent picture).
    /// </summary>
    /// <remarks>
    ///     A host that composes the diagram over other content (an image source) passes a transparent color.
    /// </remarks>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets the resource limits of the call; <see cref="PlantUmlLimits.Default"/> by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is <see langword="null"/>.</exception>
    public PlantUmlLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(Limits));
    }

    /// <summary>
    ///     Gets the callback that receives warnings, or <see langword="null"/> to drop them.
    /// </summary>
    /// <remarks>
    ///     Invoked on the calling thread, in order, after the work finished and before the method returns. When the call
    ///     fails, the reports recorded before the failure are still delivered but an exception thrown by the handler is
    ///     ignored so that the original failure is the one reported.
    /// </remarks>
    public Action<PlantUmlDiagnostic>? WarningHandler { get; init; }

    /// <summary>
    ///     Gets the callback that receives constructs that are recognized but not supported, or <see langword="null"/> to
    ///     drop them.
    /// </summary>
    /// <remarks>Delivered like <see cref="WarningHandler"/>.</remarks>
    public Action<PlantUmlUnsupportedFeature>? UnsupportedFeatureHandler { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the first unsupported construct aborts the call with a
    ///     <see cref="PlantUmlUnsupportedFeatureException"/>; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    ///     Set this for builds that must never accept an approximate diagram. The report that caused the exception is
    ///     delivered to <see cref="UnsupportedFeatureHandler"/> first.
    /// </remarks>
    public bool ThrowOnUnsupportedFeature { get; init; }
}
