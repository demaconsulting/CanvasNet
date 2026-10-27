// cspell:ignore transmittance

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Accumulates the translucent surface layers of one sample front to back in integer arithmetic
///     and finally composites them over the background.
/// </summary>
/// <remarks>
///     <para>
///         <b>Under compositing.</b> The state is the accumulated opacity <c>O</c> (0 to 255, the complement of
///         the remaining transmittance <c>T = 255 - O</c>) and the weighted channel sums. A layer with color
///         <c>c</c> and alpha <c>a</c> contributes the weight <c>w = (T * a + 127) / 255</c>, which is
///         removed from the transmittance, and adds <c>w * c</c> to the sums. Accumulating the layers from
///         the nearest to the farthest this way is the same as compositing them back to front with the
///         ordinary "over" operator, but needs no storage per layer. A layer with alpha 255 takes the whole
///         remaining transmittance (<see cref="IsOpaque"/>), so nothing behind it can contribute and the
///         sweep may stop; a layer with alpha 0 contributes nothing.
///     </para>
///     <para>
///         <b>Finishing.</b> The remaining transmittance is filled with the background (its own alpha
///         honored): <c>wb = (T * ba + 127) / 255</c>, the result alpha is <c>O + wb</c> and each channel is
///         <c>(sum + wb * bg + A / 2) / A</c> (straight alpha; fully transparent black when <c>A</c> is zero).
///         A single opaque layer therefore returns its color exactly, which keeps opaque scenes unchanged.
///     </para>
///     <para>
///         All values are small integers, so the result is exact, deterministic and independent of the platform.
///         A mutable value type whose default value is the empty state.
///     </para>
/// </remarks>
internal struct LayerCompositor
{
    /// <summary>The fully opaque alpha value.</summary>
    private const int Opaque = 255;

    /// <summary>The accumulated opacity (255 minus the remaining transmittance).</summary>
    private int _opacity;

    /// <summary>The weighted red sum.</summary>
    private long _sumR;

    /// <summary>The weighted green sum.</summary>
    private long _sumG;

    /// <summary>The weighted blue sum.</summary>
    private long _sumB;

    /// <summary>Gets a value indicating whether the layers so far are fully opaque, i.e. nothing behind them can show.</summary>
    public readonly bool IsOpaque => _opacity >= Opaque;

    /// <summary>
    ///     Adds the next layer behind all layers added so far.
    /// </summary>
    /// <param name="color">The layer's straight-alpha color.</param>
    public void Add(Rgba32 color)
    {
        var transmittance = Opaque - _opacity;
        var weight = ((transmittance * color.A) + 127) / Opaque;
        _opacity += weight;
        _sumR += (long)weight * color.R;
        _sumG += (long)weight * color.G;
        _sumB += (long)weight * color.B;
    }

    /// <summary>
    ///     Composites the accumulated layers over the background.
    /// </summary>
    /// <param name="background">The background color (its alpha is honored).</param>
    /// <returns>The straight-alpha result; fully transparent black when nothing is visible at all.</returns>
    public readonly Rgba32 Finish(Rgba32 background)
    {
        var transmittance = Opaque - _opacity;
        var weight = ((transmittance * background.A) + 127) / Opaque;
        var alpha = _opacity + weight;
        if (alpha == 0)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var half = alpha / 2;
        return new Rgba32(
            (byte)((_sumR + ((long)weight * background.R) + half) / alpha),
            (byte)((_sumG + ((long)weight * background.G) + half) / alpha),
            (byte)((_sumB + ((long)weight * background.B) + half) / alpha),
            (byte)alpha);
    }
}
