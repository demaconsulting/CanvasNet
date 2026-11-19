// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     How a part of a link decoration is filled.
/// </summary>
internal enum ExtremityFill
{
    /// <summary>Not filled.</summary>
    None,

    /// <summary>Filled with the color of the link.</summary>
    Line,

    /// <summary>Filled with the background of the document (white by default).</summary>
    Background,
}

/// <summary>
///     One part of a link decoration: a closed shape or open lines, its fill, and its stroke width.
/// </summary>
/// <param name="Segments">The geometry in scene units.</param>
/// <param name="Fill">How it is filled.</param>
/// <param name="StrokeWidth">The stroke width, or null for the width of the link.</param>
internal sealed record ExtremityPart(IReadOnlyList<SceneSegment> Segments, ExtremityFill Fill, double? StrokeWidth);

/// <summary>
///     The drawing of a decoration at one end of a link, and how far the line stops before the end.
/// </summary>
/// <param name="Decor">The decoration.</param>
/// <param name="Parts">The parts in paint order.</param>
/// <param name="ContactLength">How far before the end the line stops (PlantUML's decoration length).</param>
internal sealed record SvekExtremity(LinkDecor Decor, IReadOnlyList<ExtremityPart> Parts, double ContactLength);

/// <summary>
///     The decorations of <see cref="SvekGeometry"/> (PlantUML's <c>svek/extremity</c> classes).
/// </summary>
internal static partial class SvekGeometry
{
    /// <summary>
    ///     Decides whether a decoration is drawn by this version (the crow's foot family and the redefinition
    ///     triangles are read but not drawn, so a diagram reports them).
    /// </summary>
    /// <param name="decor">The decoration.</param>
    /// <returns>True when <see cref="Extremity"/> draws it.</returns>
    public static bool IsDrawn(LinkDecor decor) => decor is not (LinkDecor.Crowfoot or LinkDecor.CircleCrowfoot
        or LinkDecor.CircleLine or LinkDecor.DoubleLine or LinkDecor.LineCrowfoot or LinkDecor.Redefines or LinkDecor.DefinedBy);

    /// <summary>
    ///     Gets the space a decoration needs at the end of a link in the layout (PlantUML's <c>LinkDecor</c> margin).
    /// </summary>
    /// <param name="decor">The decoration.</param>
    /// <returns>The margin in pixels.</returns>
    public static double Margin(LinkDecor decor) => decor switch
    {
        LinkDecor.Extends or LinkDecor.Redefines or LinkDecor.DefinedBy => 30,
        LinkDecor.Composition or LinkDecor.Aggregation => 15,
        LinkDecor.CircleCrowfoot => 14,
        LinkDecor.Crowfoot or LinkDecor.CircleLine or LinkDecor.LineCrowfoot or LinkDecor.Arrow or LinkDecor.ArrowTriangle => 10,
        LinkDecor.DoubleLine => 7,
        LinkDecor.NotNavigable => 1,
        LinkDecor.None => 2,
        _ => 0,
    };

    /// <summary>
    ///     Builds the decoration at one end of a link, with PlantUML's geometry.
    /// </summary>
    /// <remarks>
    ///     The decoration is drawn with its tip at the end of the link (on the outline of the entity) and turned to the
    ///     direction in which the link arrives there; the line itself stops <see cref="SvekExtremity.ContactLength"/>
    ///     before the tip. The sizes are PlantUML's: the arrow head 9 long and 8 wide with its line stopping 5 before the
    ///     tip, the extension triangle 18 long and 12 wide, the diamonds 12 long and 8 wide, the circles of radius 6
    ///     (stroke 1.5), the socket arc of radius 9 over 140 degrees, the square of 10, the circled plus of radius 8, the
    ///     cross 8 by 8 at 5 from the tip, and the half arrows 9 long.
    /// </remarks>
    /// <param name="decor">The decoration.</param>
    /// <param name="tip">The end of the link.</param>
    /// <param name="angle">The direction in which the link arrives at its end, in radians (y down).</param>
    /// <returns>The decoration, or null for <see cref="LinkDecor.None"/> and the decorations that are not drawn.</returns>
    public static SvekExtremity? Extremity(LinkDecor decor, SvekPoint tip, double angle)
    {
        if (!IsDrawn(decor) || decor == LinkDecor.None)
        {
            return null;
        }

        // The frame of the decoration: 'along' points into the entity, 'across' is perpendicular to it
        var along = new SvekPoint(Math.Cos(angle), Math.Sin(angle));
        var (ux, uy) = (along.X, along.Y);
        var across = new SvekPoint(-uy, ux);
        SvekPoint At(double a, double c) => new(tip.X + (a * along.X) + (c * across.X), tip.Y + (a * along.Y) + (c * across.Y));
        IReadOnlyList<SceneSegment> Polygon(params (double A, double C)[] points) =>
            SceneShapes.Polygon(points.Select(p => { var q = At(p.A, p.C); return (q.X, q.Y); }).ToArray());
        IReadOnlyList<SceneSegment> Line((double A, double C) p, (double A, double C) q)
        {
            var (a, b) = (At(p.A, p.C), At(q.A, q.C));
            return SceneShapes.Line(a.X, a.Y, b.X, b.Y);
        }

        var back = angle + Math.PI;
        return decor switch
        {
            LinkDecor.Arrow => new SvekExtremity(decor, [new(Polygon((0, 0), (-9, -4), (-5, 0), (-9, 4)), ExtremityFill.Line, null)], 5),
            LinkDecor.Extends => new SvekExtremity(decor, [new(Polygon((0, 0), (-18, -6), (-18, 6)), ExtremityFill.None, null)], 18),
            LinkDecor.ArrowTriangle => new SvekExtremity(decor, [new(Polygon((0, 0), (-8, -3), (-8, 3)), ExtremityFill.None, null)], 8),
            LinkDecor.Composition => new SvekExtremity(decor, [new(Polygon((0, 0), (-6, -4), (-12, 0), (-6, 4)), ExtremityFill.Line, null)], 12),
            LinkDecor.Aggregation => new SvekExtremity(decor, [new(Polygon((0, 0), (-6, -4), (-12, 0), (-6, 4)), ExtremityFill.None, null)], 12),
            LinkDecor.NotNavigable => new SvekExtremity(
                decor,
                [new(Line((-5, -4), (-13, 4)), ExtremityFill.None, null), new(Line((-5, 4), (-13, -4)), ExtremityFill.None, null)],
                8),
            LinkDecor.Circle or LinkDecor.CircleFill => new SvekExtremity(
                decor,
                [new(Circle(At(-6, 0).X, At(-6, 0).Y, 6), decor == LinkDecor.CircleFill ? ExtremityFill.Line : ExtremityFill.Background, 1.5)],
                12),
            LinkDecor.CircleConnect => new SvekExtremity(
                decor,
                [
                    new(Circle(tip.X, tip.Y, 6), ExtremityFill.Background, 1.5),
                    new(Arc(tip.X, tip.Y, 10, back - (Math.PI / 4), Math.PI / 2), ExtremityFill.None, 1.5),
                ],
                10),
            LinkDecor.Parenthesis => new SvekExtremity(
                decor,
                [new(Arc(tip.X, tip.Y, 9, back - (70 * Math.PI / 180), 140 * Math.PI / 180), ExtremityFill.None, 1.5)],
                10),
            LinkDecor.Square => new SvekExtremity(decor, [new(SceneShapes.Rect(tip.X - 5, tip.Y - 5, 10, 10), ExtremityFill.Background, 1.5)], 5),
            LinkDecor.Plus => new SvekExtremity(
                decor,
                [
                    new(Circle(At(-8, 0).X, At(-8, 0).Y, 8), ExtremityFill.Background, null),
                    new(Line((0, 0), (-16, 0)), ExtremityFill.None, null),
                    new(Line((-8, -8), (-8, 8)), ExtremityFill.None, null),
                ],
                16),
            LinkDecor.HalfArrowUp or LinkDecor.HalfArrowDown => new SvekExtremity(
                decor,
                [
                    new(Line((0, 0), (-9, decor == LinkDecor.HalfArrowUp ? -4 : 4)), ExtremityFill.None, null),
                    new(Line((0, 0), (-8, 0)), ExtremityFill.None, null),
                ],
                8),
            _ => null,
        };
    }
}
