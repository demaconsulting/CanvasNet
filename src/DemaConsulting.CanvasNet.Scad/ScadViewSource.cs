namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     Where the camera of a render came from (see <see cref="ScadResolvedView.Source"/>).
/// </summary>
/// <remarks>
///     The members tolerate additions: consumers should handle unknown values defensively.
/// </remarks>
public enum ScadViewSource
{
    /// <summary>The default view: the requested (or default isometric) angles fitted to the model.</summary>
    Default,

    /// <summary>
    ///     The model is entirely two-dimensional and nothing chose a view, so it is drawn as a flat top-down
    ///     orthographic drawing.
    /// </summary>
    FlatDefault,

    /// <summary>The view angles were set explicitly in <see cref="ScadRenderOptions"/>.</summary>
    Options,

    /// <summary>The document (or a variable override) defined <c>$vpr</c>, <c>$vpt</c>, <c>$vpd</c> or <c>$vpf</c>.</summary>
    Document,

    /// <summary>The caller supplied the camera in <see cref="ScadRenderOptions.Camera"/>.</summary>
    Camera,
}
