namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <summary>
///     The <c>Scenegraph</c> namespace is the last stage of the pipeline and the only one that produces pixels: an
///     immutable description of what to draw (<c>SceneModel</c>: groups, paths with fill and stroke, text lines of styled
///     runs and pictures, in Mermaid's SVG coordinates with the view box of the diagram) and the painter that draws a scene
///     onto a core <c>Surface</c> (<c>ScenePainter</c>).
/// </summary>
/// <remarks>
///     <para>
///         The scene is also the assertion surface of the package: every item carries a role such as <c>node</c>,
///         <c>edge</c> or <c>cluster</c> and Mermaid's element id, so a test or the reference comparison can find an item and
///         compare its geometry with a number instead of comparing pixels.
///     </para>
///     <para>
///         The namespace is internal to the package. The scene types are immutable and safe for concurrent use; a painter
///         paints one scene on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
