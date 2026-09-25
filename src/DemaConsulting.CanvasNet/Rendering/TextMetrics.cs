namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     Metrics returned by <see cref="TextRenderer.MeasureText"/>: total advance width plus the
///     ascender and descender heights of the source font, all scaled to the requested text size
///     in the same local-space units used by the anchor coordinates passed to
///     <see cref="TextRenderer.DrawText"/>.
/// </summary>
public readonly struct TextMetrics
{
    /// <summary>Total advance width of the measured run, including inter-glyph kerning.</summary>
    public float Width { get; }

    /// <summary>Font ascender, in the same units as <see cref="Width"/>.</summary>
    public float Ascent { get; }

    /// <summary>
    ///     Font descender, in the same units as <see cref="Width"/>. Positive-valued for a font
    ///     whose descender field is negative in design units (the sign is flipped here so callers
    ///     can add <see cref="Descent"/> directly to a baseline y-coordinate to find the bottom
    ///     of the descent).
    /// </summary>
    public float Descent { get; }

    /// <summary>Initializes a new <see cref="TextMetrics"/> value.</summary>
    /// <param name="width">Total advance width.</param>
    /// <param name="ascent">Font ascender height (positive).</param>
    /// <param name="descent">Font descender depth (positive).</param>
    public TextMetrics(float width, float ascent, float descent)
    {
        Width = width;
        Ascent = ascent;
        Descent = descent;
    }
}
