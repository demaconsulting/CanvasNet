namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     Selects horizontal text alignment relative to the anchor <c>x</c>-coordinate passed to
///     <see cref="TextRenderer.DrawText"/>.
/// </summary>
public enum TextAlign
{
    /// <summary>Anchor <c>x</c> is the left edge of the first glyph's advance.</summary>
    Left = 0,

    /// <summary>Anchor <c>x</c> is the horizontal center of the run's total advance width.</summary>
    Center = 1,

    /// <summary>Anchor <c>x</c> is the right edge of the last glyph's advance.</summary>
    Right = 2,
}
