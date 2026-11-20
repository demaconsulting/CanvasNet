// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The deployment symbols of <see cref="DescriptionRenderer"/>: artifact, storage, file, queue, stack, agent,
///     hexagon, person, boundary, control, entity, collections, and label, and the outlines their containers share.
/// </summary>
internal static partial class DescriptionRenderer
{
    /// <summary>The corner radius of a storage (PlantUML's corner of 70).</summary>
    private const double StorageRadius = 35;

    /// <summary>The control distance of the cubic approximation of a quarter circle, as a fraction of the radius.</summary>
    private const double Kappa = 0.5522847498;

    /// <summary>The size of the folded corner of a file (<c>USymbolFile</c>).</summary>
    private const double FileCorner = 10;

    /// <summary>The width of each ledge of a stack (<c>USymbolStack</c>).</summary>
    private const double StackBorder = 15;

    /// <summary>The offset of the back box of a collections symbol (<c>USymbolCollections</c>).</summary>
    private const double CollectionsDelta = 4;

    /// <summary>The space around the circle of a boundary, control, or entity (<c>Boundary</c>, <c>Control</c>, <c>EntityDomain</c>).</summary>
    private const double SymbolMargin = 4;

    /// <summary>The radius of the circle of a boundary, control, or entity.</summary>
    private const double SymbolRadius = 12;

    /// <summary>The length of the bar of a boundary, from its line to its circle.</summary>
    private const double BoundaryBar = 17;

    /// <summary>
    ///     Builds the image of an artifact (<c>USymbolArtifact</c>): a box with the document icon at its top right and
    ///     margins of 10, 20, 13, and 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage ArtifactImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 30, p.TextHeight + 23);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x, y, w, h, p.Radius), "description"));
            ArtifactIcon(items, x + w, y, (segments, role) => p.Path(segments, role));
            p.DrawTexts(items, x + 10, y + 13);
        });
    }

    /// <summary>
    ///     Adds the document icon of an artifact at the top right of a box: a sheet of 12 by 14 with a folded corner of
    ///     6, 5 from the right and the top.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="right">The right of the box.</param>
    /// <param name="top">The top of the box.</param>
    /// <param name="path">Creates a path item with a role.</param>
    internal static void ArtifactIcon(List<SceneItem> items, double right, double top, Func<IReadOnlyList<SceneSegment>, string, SceneItem> path)
    {
        var (x, y) = (right - 17, top + 5);
        items.Add(path(SceneShapes.Polygon((x, y), (x, y + 14), (x + 12, y + 14), (x + 12, y + 6), (x + 6, y)), "description-icon"));
        items.Add(path(SceneShapes.Line(x + 6, y, x + 6, y + 6), "description-icon"));
        items.Add(path(SceneShapes.Line(x + 12, y + 6, x + 6, y + 6), "description-icon"));
    }

    /// <summary>
    ///     Builds the image of a storage (<c>USymbolStorage</c>): a box with corners of 70 and margins of 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage StorageImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(StorageOutline(x, y, w, h), "description"));
            p.DrawTexts(items, x + 10, y + 10);
        });
    }

    /// <summary>
    ///     Builds the outline of a storage: a box with corners of radius 35, each radius cut to half the side it runs
    ///     along (as SVG cuts <c>rx</c> and <c>ry</c>), so a small storage is an ellipse.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <returns>The closed outline.</returns>
    internal static IReadOnlyList<SceneSegment> StorageOutline(double x, double y, double w, double h)
    {
        var (rx, ry) = (Math.Min(StorageRadius, w / 2), Math.Min(StorageRadius, h / 2));
        var (right, bottom) = (x + w, y + h);
        return
        [
            SceneSegment.Move(x + rx, y),
            SceneSegment.Line(right - rx, y),
            Corner((right - rx, y), (right, y), (right, y + ry)),
            SceneSegment.Line(right, bottom - ry),
            Corner((right, bottom - ry), (right, bottom), (right - rx, bottom)),
            SceneSegment.Line(x + rx, bottom),
            Corner((x + rx, bottom), (x, bottom), (x, bottom - ry)),
            SceneSegment.Line(x, y + ry),
            Corner((x, y + ry), (x, y), (x + rx, y)),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds the image of a file (<c>USymbolFile</c>): a sheet with a folded top right corner of 10 and margins of
    ///     10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage FileImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(FileOutline(x, y, w, h, p.Radius), "description"));
            items.Add(p.Path(FileFold(x, y, w, p.Radius), "description-detail"));
            p.DrawTexts(items, x + 10, y + 10);
        });
    }

    /// <summary>
    ///     Builds the outline of a file: a box with rounded corners whose top right corner is cut off by the fold.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <param name="r">The corner radius, zero for square corners.</param>
    /// <returns>The closed outline.</returns>
    internal static IReadOnlyList<SceneSegment> FileOutline(double x, double y, double w, double h, double r)
    {
        if (r <= 0)
        {
            return SceneShapes.Polygon((x, y), (x, y + h), (x + w, y + h), (x + w, y + FileCorner), (x + w - FileCorner, y));
        }

        return
        [
            SceneSegment.Move(x, y + r),
            SceneSegment.Line(x, y + h - r),
            Corner((x, y + h - r), (x, y + h), (x + r, y + h)),
            SceneSegment.Line(x + w - r, y + h),
            Corner((x + w - r, y + h), (x + w, y + h), (x + w, y + h - r)),
            SceneSegment.Line(x + w, y + FileCorner),
            SceneSegment.Line(x + w - FileCorner, y),
            SceneSegment.Line(x + r, y),
            Corner((x + r, y), (x, y), (x, y + r)),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds the fold of a file: down from the top and right to the side, with a rounded inner corner.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="w">The width.</param>
    /// <param name="r">The corner radius, zero for a square corner.</param>
    /// <returns>The open path.</returns>
    internal static IReadOnlyList<SceneSegment> FileFold(double x, double y, double w, double r)
    {
        var fold = x + w - FileCorner;
        if (r <= 0)
        {
            return SceneShapes.Polyline((fold, y), (fold, y + FileCorner), (x + w, y + FileCorner));
        }

        return
        [
            SceneSegment.Move(fold, y),
            SceneSegment.Line(fold, y + FileCorner - r),
            Corner((fold, y + FileCorner - r), (fold, y + FileCorner), (fold + r, y + FileCorner)),
            SceneSegment.Line(x + w, y + FileCorner),
        ];
    }

    /// <summary>
    ///     Builds the image of a queue (<c>USymbolQueue</c>): a horizontal cylinder (the sequence diagram's queue) with
    ///     margins of 5, 15, 5, and 5.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage QueueImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 10);
        return Painter.Box(w, h, (x, y, items) =>
        {
            var paths = SequenceRenderer.Queue(x, y, w - 10, h, p.Fill, p.Stroke).ToList();
            items.Add(p.Own(paths[0], "description"));
            items.Add(p.Own(paths[1], "description-detail"));
            p.DrawTexts(items, x + 5, y + 5);
        });
    }

    /// <summary>
    ///     Builds the image of a stack (<c>USymbolStack</c>): a filled box without an outline between two ledges of 15
    ///     whose line runs down its sides and along its bottom, margins of 25, 25, 10, and 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage StackImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 50, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x + StackBorder, y, w - (2 * StackBorder), h, p.Radius), "description") with { Stroke = null });
            items.Add(p.Path(StackLedges(x, y, w, h, p.Radius), "description-detail", filled: false));
            p.DrawTexts(items, x + 25, y + 10);
        });
    }

    /// <summary>
    ///     Builds the line of a stack: from the left ledge down the left side, along the bottom, and up to the right
    ///     ledge, with rounded corners.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <param name="r">The corner radius, zero for square corners.</param>
    /// <returns>The open path.</returns>
    internal static IReadOnlyList<SceneSegment> StackLedges(double x, double y, double w, double h, double r)
    {
        var (left, right, bottom) = (x + StackBorder, x + w - StackBorder, y + h);
        if (r <= 0)
        {
            return SceneShapes.Polyline((x, y), (left, y), (left, bottom), (right, bottom), (right, y), (x + w, y));
        }

        return
        [
            SceneSegment.Move(x, y),
            SceneSegment.Line(left - r, y),
            Corner((left - r, y), (left, y), (left, y + r)),
            SceneSegment.Line(left, bottom - r),
            Corner((left, bottom - r), (left, bottom), (left + r, bottom)),
            SceneSegment.Line(right - r, bottom),
            Corner((right - r, bottom), (right, bottom), (right, bottom - r)),
            SceneSegment.Line(right, y + r),
            Corner((right, y + r), (right, y), (right + r, y)),
            SceneSegment.Line(x + w, y),
        ];
    }

    /// <summary>
    ///     Builds the image of a hexagon (<c>USymbolHexagon</c>): the texts 5 from the top and a quarter of the width
    ///     from the left, in a box twice as wide as them and 10 higher, outlined by the hexagon Graphviz draws for such
    ///     a node (its top and bottom sides span the middle half of the box).
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage HexagonImage(Painter p)
    {
        var (w, h) = (p.TextWidth * 2, p.TextHeight + 10);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(HexagonOutline(x, y, w, h, w / 4), "description"));
            p.DrawTexts(items, x + (w / 4), y + 5);
        });
    }

    /// <summary>
    ///     Builds a hexagon with corners in the middles of the left and right sides of a box.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <param name="dx">The distance from each side to the nearest corner of the top and the bottom.</param>
    /// <returns>The closed outline.</returns>
    internal static IReadOnlyList<SceneSegment> HexagonOutline(double x, double y, double w, double h, double dx) =>
        SceneShapes.Polygon((x, y + (h / 2)), (x + dx, y), (x + w - dx, y), (x + w, y + (h / 2)), (x + w - dx, y + h), (x + dx, y + h));

    /// <summary>
    ///     Builds the image of a person (<c>USymbolPerson</c>): a head over a body, the body a box around the texts with
    ///     margins of 10 and corners as round as the head, whose size is 0.42 times the square root of the body's area.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage PersonImage(Painter p)
    {
        var (bodyWidth, bodyHeight) = (p.TextWidth + 20, p.TextHeight + 20);
        var head = Math.Sqrt(bodyWidth * bodyHeight) * 0.42;
        return Painter.Box(bodyWidth, bodyHeight + head, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Ellipse(x + (bodyWidth / 2), y + (head / 2), head / 2, head / 2), "description"));
            items.Add(p.Path(SceneShapes.Rect(x, y + head, bodyWidth, bodyHeight, head / 2), "description-detail"));
            p.DrawTexts(items, x + 10, y + head + 10);
        });
    }

    /// <summary>
    ///     Builds the image of a boundary, a control, or an entity (<c>USymbolBoundary</c>, <c>USymbolControl</c>,
    ///     <c>USymbolEntityDomain</c>): the stereotypes, the symbol of the sequence diagram (a circle of radius 12 with
    ///     its mark, 4 from the sides of its box), and the name below it, each centered.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <param name="kind">The kind of symbol.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage SymbolImage(Painter p, ParticipantKind kind)
    {
        var symbolWidth = (2 * SymbolRadius) + (2 * SymbolMargin) + (kind == ParticipantKind.Boundary ? BoundaryBar : 0);
        var symbolHeight = (2 * SymbolRadius) + (2 * SymbolMargin);
        var stereoHeight = p.Stereotypes?.Height ?? 0;
        var name = p.Name;
        var w = Math.Max(Math.Max(p.StereotypeWidth, symbolWidth), name.Width);
        var h = stereoHeight + symbolHeight + name.Height;
        return Painter.Box(w, h, (x, y, items) =>
        {
            // The sequence symbol is centered on its circle, a boundary's line and bar on its left
            var circleCenter = x + ((w - symbolWidth) / 2) + SymbolMargin + (kind == ParticipantKind.Boundary ? BoundaryBar : 0) + SymbolRadius;
            var center = kind == ParticipantKind.Boundary ? circleCenter - 8.5 : circleCenter;
            foreach (var path in SequenceRenderer.Symbol(kind, center, y + stereoHeight + SymbolMargin, p.Fill, p.Stroke))
            {
                var circle = path.Segments.Any(s => s.Kind == SceneSegmentKind.CubicTo);
                var item = p.Own(path, circle ? "description" : "description-detail");

                // The arrow of a control is outlined one pixel wide (UStroke.simple)
                items.Add(kind == ParticipantKind.Control && !circle && item.Stroke is { } s ? item with { Stroke = s with { Width = 1 } } : item);
            }

            if (p.Stereotypes is { } stereo)
            {
                items.AddRange(p.DrawText(stereo, x + ((w - p.StereotypeWidth) / 2) + 1, y, TextAlign.Center, "description-stereotype"));
            }

            items.AddRange(p.DrawText(name, x + ((w - name.Width) / 2), y + stereoHeight + symbolHeight, TextAlign.Left, "description-name"));
        });
    }

    /// <summary>
    ///     Builds the image of a collections symbol (<c>USymbolCollections</c>): a box over another one 4 to the right
    ///     and below, the texts 8 from the front box's corner, margins of 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage CollectionsImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) =>
        {
            var (boxWidth, boxHeight) = (w - CollectionsDelta, h - CollectionsDelta);
            items.Add(p.Path(SceneShapes.Rect(x + CollectionsDelta, y + CollectionsDelta, boxWidth, boxHeight, p.Radius), "description"));
            items.Add(p.Path(SceneShapes.Rect(x, y, boxWidth, boxHeight, p.Radius), "description-detail"));
            p.DrawTexts(items, x + 10 - (CollectionsDelta / 2), y + 10 - (CollectionsDelta / 2));
        });
    }

    /// <summary>
    ///     Builds the image of a label (<c>USymbolLabel</c>): its texts with margins of 10 and no outline.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage LabelImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) => p.DrawTexts(items, x + 10, y + 10));
    }

    /// <summary>
    ///     Builds a cubic approximation of a quarter circle that rounds a corner: from a point on one side of the corner
    ///     to a point on the other side, both one radius from it.
    /// </summary>
    /// <param name="from">The start (the current point of the path).</param>
    /// <param name="corner">The corner.</param>
    /// <param name="to">The end.</param>
    /// <returns>The segment.</returns>
    private static SceneSegment Corner((double X, double Y) from, (double X, double Y) corner, (double X, double Y) to) =>
        SceneSegment.Cubic(
            from.X + (Kappa * (corner.X - from.X)),
            from.Y + (Kappa * (corner.Y - from.Y)),
            to.X + (Kappa * (corner.X - to.X)),
            to.Y + (Kappa * (corner.Y - to.Y)),
            to.X,
            to.Y);
}
