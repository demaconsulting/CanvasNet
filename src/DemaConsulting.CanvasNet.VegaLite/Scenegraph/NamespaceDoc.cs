namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     The <c>Scenegraph</c> namespace is the last stage of the pipeline and the only one that touches pixels: an
///     immutable description of what to draw (<c>SceneModel</c>: groups, rectangles, rules, paths, symbols and text with
///     resolved geometry and CSS paints), the paint rules for the CSS colors those paints use (<c>CssPaint</c>, on top of
///     the core <c>CssColor</c> parser) and the
///     painter that draws a scene onto a core <c>Surface</c> (<c>ScenePainter</c>).
/// </summary>
/// <remarks>
///     <para>
///         The scene is also the assertion surface of the package: every item carries a role such as
///         <c>mark-bar</c>, <c>axis-tick</c> or <c>title</c>, so a test can find an item and compare its geometry with a
///         hand-derived number instead of comparing pixels.
///     </para>
///     <para>
///         The namespace is internal to the package. Every type is immutable (the painter keeps no state between calls)
///         and safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
