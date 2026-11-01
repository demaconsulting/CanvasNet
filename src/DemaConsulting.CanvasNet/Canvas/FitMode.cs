namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     How <see cref="Surface.FitInto"/> fits a raster image into a box of a different aspect ratio.
/// </summary>
public enum FitMode
{
    /// <summary>The image keeps its aspect ratio, is scaled to the largest size that fits, and is centered on a fill color.</summary>
    Contain,

    /// <summary>The image is resampled to exactly the box, changing its aspect ratio if needed.</summary>
    Stretch
}
