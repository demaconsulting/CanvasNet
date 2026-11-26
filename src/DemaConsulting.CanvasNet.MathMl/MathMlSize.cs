namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     A size in whole pixels with the position of the baseline, used to report how large an equation renders and where
///     its alphabetic baseline lies, so that a caller can align it with surrounding text.
/// </summary>
/// <remarks>
///     The value is what <see cref="MathMlDocument.Measure(MathMlRenderOptions?, CancellationToken)"/> returns so a
///     caller can decide on a destination (a page, a slide, an inline position in a line of text) before it renders.
///     The type is an immutable value type with value equality and is safe for concurrent use.
/// </remarks>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Baseline">
///     The distance in pixels from the top of the picture to the alphabetic baseline of the <c>math</c> element; from
///     0 to <paramref name="Height"/>.
/// </param>
public readonly record struct MathMlSize(int Width, int Height, int Baseline);
