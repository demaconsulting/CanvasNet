namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <summary>
///     The <c>Scenegraph</c> namespace is the drawing stage of the pipeline, independent of the diagram type: the scene
///     a diagram's layout produces, the text layout that measures it, and the painter that turns it into pixels.
/// </summary>
/// <remarks>
///     <para>
///         <c>SceneModel</c> holds the immutable items (filled and stroked paths of lines, rectangles, rounded rectangles,
///         ellipses and polygons, with optional dashes, and lines of styled text runs) and the view box;
///         <c>TextLayout</c> measures text with the bundled fonts as PlantUML measures it with Java's logical fonts and
///         reads the creole markup subset; <c>ScenePainter</c> paints a scene onto a core <c>Surface</c> with bounds
///         checks, the painted-pixel budget and cancellation.
///     </para>
///     <para>
///         The namespace is internal to the package; scenes are immutable, and a text layout and a painter are used by
///         one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
