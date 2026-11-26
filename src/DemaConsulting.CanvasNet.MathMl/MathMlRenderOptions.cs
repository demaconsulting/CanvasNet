using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     Options that control one measure or render call of a <see cref="MathMlDocument"/>.
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
///         the first unsupported construct into a <see cref="MathMlUnsupportedFeatureException"/>.
///     </para>
/// </remarks>
public sealed class MathMlRenderOptions
{
    /// <summary>The largest accepted <see cref="FontSize"/> in CSS pixels.</summary>
    private const double MaxFontSize = 4096;

    /// <summary>The backing field of <see cref="FontSize"/>.</summary>
    private readonly double _fontSize = 16;

    /// <summary>The backing field of <see cref="Scale"/>.</summary>
    private readonly double _scale = 1;

    /// <summary>The backing field of <see cref="Limits"/>.</summary>
    private readonly MathMlLimits _limits = MathMlLimits.Default;

    /// <summary>
    ///     Gets the font size of the <c>math</c> element in CSS pixels; 16 by default (the default font size of a
    ///     browser, so a rendering at scale 1 matches a browser's at 100% zoom).
    /// </summary>
    /// <remarks>
    ///     Every length of the layout follows from it: the glyph sizes, the <c>em</c> and <c>ex</c> units, and the
    ///     constants of the font's MATH table. The overload of <c>Render</c> that takes an explicit size uses it only for
    ///     the proportions, because the size decides the scale.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown on initialization when the value is not finite, is not greater than zero, or exceeds 4,096.
    /// </exception>
    public double FontSize
    {
        get => _fontSize;
        init
        {
            if (!double.IsFinite(value) || value <= 0 || value > MaxFontSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(FontSize),
                    value,
                    string.Create(CultureInfo.InvariantCulture, $"The font size must be a finite number greater than zero and at most {MaxFontSize}."));
            }

            _fontSize = value;
        }
    }

    /// <summary>
    ///     Gets the factor by which the natural size of the equation is multiplied; 1 by default.
    /// </summary>
    /// <remarks>
    ///     The natural size is the size of the equation in CSS pixels at <see cref="FontSize"/>. The scale is applied to
    ///     the whole equation, so a larger scale yields a larger, equally sharp image rather than a magnified one. The size
    ///     that results must lie within 1 and <see cref="Surface.MaxDimension"/> on both sides; this is checked when the
    ///     document is measured or rendered. The scale is not used by the overload of <c>Render</c> that takes an
    ///     explicit size, which decides the scale itself.
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
    ///     Gets the display mode to lay the equation out in, or <see langword="null"/> (the default) for the document's
    ///     own (<see cref="MathMlDocument.Display"/>, the <c>display</c> attribute of the <c>math</c> element).
    /// </summary>
    public MathMlDisplay? Display { get; init; }

    /// <summary>
    ///     Gets the text color of the equation (the CSS <c>color</c> of the <c>math</c> element); opaque black by
    ///     default. <c>mathcolor</c> attributes and <c>color</c> declarations of the equation override it for their
    ///     content.
    /// </summary>
    public Rgba32 Color { get; init; } = new(0, 0, 0, 255);

    /// <summary>
    ///     Gets the color the surface is filled with before the equation is drawn, or <see langword="null"/> (the default)
    ///     for a transparent picture, as a browser draws an equation over the page behind it.
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets the resource limits of the call; <see cref="MathMlLimits.Default"/> by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is <see langword="null"/>.</exception>
    public MathMlLimits Limits
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
    public Action<MathMlDiagnostic>? WarningHandler { get; init; }

    /// <summary>
    ///     Gets the callback that receives constructs that are recognized but not supported, or <see langword="null"/> to
    ///     drop them.
    /// </summary>
    /// <remarks>Delivered like <see cref="WarningHandler"/>.</remarks>
    public Action<MathMlUnsupportedFeature>? UnsupportedFeatureHandler { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the first unsupported construct aborts the call with a
    ///     <see cref="MathMlUnsupportedFeatureException"/>; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    ///     Set this for builds that must never accept an approximate equation. The report that caused the exception is
    ///     delivered to <see cref="UnsupportedFeatureHandler"/> first.
    /// </remarks>
    public bool ThrowOnUnsupportedFeature { get; init; }
}
