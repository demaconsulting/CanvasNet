// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     Draws the geometry of an activity diagram as a scene.
/// </summary>
/// <remarks>
///     <para>
///         The shapes are drawn in the order the layout placed them (PlantUML's order: the tiles from the top, each
///         tile's shapes before its children, then every arrow). Every item carries a role: <c>activity</c> and
///         <c>activity-text</c> for actions, <c>start</c>, <c>stop</c>, <c>end</c>, <c>connector</c> and
///         <c>connector-text</c> for the circles, <c>diamond</c> and <c>diamond-text</c> for the tests and merges,
///         <c>bar</c>, <c>bar-text</c> and <c>split</c> for forks and splits, <c>note</c> and <c>note-text</c>,
///         <c>partition</c> and <c>partition-text</c> for groups, <c>lane</c>, <c>lane-divider</c> and
///         <c>lane-title</c> for swimlanes, and <c>arrow</c>, <c>arrow-head</c> and <c>arrow-text</c> for arrows.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class ActivityRenderer
{
    /// <summary>
    ///     Draws an activity diagram.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="geometry">The geometry.</param>
    /// <returns>The scene of the body (without page texts), with its view box at the origin.</returns>
    public static Scene Render(ActivityModel model, ActivityGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(geometry);
        var items = new List<SceneItem>(geometry.Shapes.Count);
        foreach (var shape in geometry.Shapes)
        {
            if (ToItem(shape) is { } item)
            {
                items.Add(item);
            }
        }

        return new Scene(new SceneRect(0, 0, geometry.Width, geometry.Height), items) { Background = model.Theme.Background };
    }

    /// <summary>
    ///     Converts one placed shape into a scene item.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The item, or null for space that is not drawn.</returns>
    internal static SceneItem? ToItem(ActivityShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Kind != ActivityShapeKind.Text && shape.Fill is null && shape.Stroke is null)
        {
            // Space a shape occupies without paint (such as the background of the lane titles)
            return null;
        }

        IReadOnlyList<SceneSegment> segments;
        switch (shape.Kind)
        {
            case ActivityShapeKind.Rect:
                segments = SceneShapes.Rect(shape.X, shape.Y, Math.Max(0, shape.Width), Math.Max(0, shape.Height), shape.Radius);
                break;
            case ActivityShapeKind.Ellipse:
                segments = SceneShapes.Ellipse(shape.X + (shape.Width / 2), shape.Y + (shape.Height / 2), shape.Width / 2, shape.Height / 2);
                break;
            case ActivityShapeKind.Polygon or ActivityShapeKind.Path:
                segments = shape.Segments
                    .Select(s => s.Kind == SceneSegmentKind.Close ? s : s with { X = s.X + shape.X, Y = s.Y + shape.Y, X1 = s.X1 + shape.X, Y1 = s.Y1 + shape.Y, X2 = s.X2 + shape.X, Y2 = s.Y2 + shape.Y })
                    .ToList();
                break;
            case ActivityShapeKind.Line:
                segments = SceneShapes.Line(shape.X, shape.Y, shape.X2, shape.Y2);
                break;
            case ActivityShapeKind.Text when shape.Text is { } text:
                return ScenePage.Translate(text, shape.X - text.X, shape.Y - text.Baseline) with { Role = shape.Role, Location = shape.Location };
            default:
                return null;
        }

        return new ScenePath(segments)
        {
            Role = shape.Role,
            Fill = shape.Fill,
            Stroke = shape.Stroke,
            Location = shape.Location,
        };
    }
}
