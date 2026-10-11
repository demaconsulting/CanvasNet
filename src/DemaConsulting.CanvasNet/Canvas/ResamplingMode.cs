namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     Selects the reconstruction filter used by <see cref="Surface.Resize"/> and
///     <see cref="Surface.ResizeToFit"/>.
/// </summary>
/// <remarks>
///     Every mode other than <see cref="Nearest"/> works in premultiplied alpha and clamps its
///     result to the valid byte range. The smooth filters (<see cref="Bilinear"/>,
///     <see cref="Bicubic"/>, <see cref="Lanczos3"/>) clamp at the image edges (edge pixels are
///     replicated), whereas <see cref="Area"/> renormalizes its weights over the source pixels
///     actually covered. The smooth modes
///     (<see cref="Bilinear"/>, <see cref="Bicubic"/>, <see cref="Lanczos3"/>) widen their
///     kernel by the reduction ratio when an axis is shrunk, so they act as a true prefilter
///     and do not drop thin features the way point sampling does.
/// </remarks>
public enum ResamplingMode
{
    /// <summary>
    ///     Chooses a filter independently for each axis: <see cref="Area"/> when the axis is
    ///     reduced, <see cref="Bicubic"/> when it is enlarged. An axis that keeps its size is
    ///     copied exactly.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Point sampling: each output pixel copies the nearest source pixel. Fastest, keeps hard
    ///     edges (pixel art), but aliases and can drop thin features when shrinking.
    /// </summary>
    Nearest = 1,

    /// <summary>Triangle (linear) filter; smooth, with no overshoot.</summary>
    Bilinear = 2,

    /// <summary>
    ///     Catmull-Rom cubic filter; sharper than <see cref="Bilinear"/> and may overshoot
    ///     slightly near sharp edges (the overshoot is clamped).
    /// </summary>
    Bicubic = 3,

    /// <summary>
    ///     Lanczos windowed-sinc filter with three lobes; the sharpest smooth filter, with mild
    ///     ringing near hard edges (clamped to the valid range).
    /// </summary>
    Lanczos3 = 4,

    /// <summary>
    ///     Exact area averaging (box filter over the footprint of each output pixel). The best
    ///     choice for large reductions because it conserves total ink; when enlarging it behaves
    ///     like pixel replication with blended boundary pixels.
    /// </summary>
    Area = 5,
}
