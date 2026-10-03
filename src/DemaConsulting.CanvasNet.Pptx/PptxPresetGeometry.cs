using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore rtTriangle homePlate lnTo cubicBezTo quadBezTo prstGeom custGeom pptx prst fmla

/// <summary>
///     Builds the core <see cref="Path"/> geometry for a DrawingML preset shape
///     (<c>&lt;a:prstGeom prst="..."/&gt;</c>), for the ~24-name subset of OOXML's full preset
///     geometry catalog supported this phase - see <c>pptx-document.md</c>'s "Geometry and Paint
///     (Phase 1c)" design section for the exact supported list and the rationale for every
///     deferred name.
/// </summary>
/// <remarks>
///     <para>
///     Every builder below produces geometry sized to exactly <c>(0,0)</c>-<c>(w,h)</c> local
///     coordinates (the shape's own declared width/height) - a caller then applies the shape's
///     resolved <see cref="PptxShapeFrame.Transform"/> (via <see cref="Path.Transform(Matrix3x2)"/>)
///     to place it into parent coordinates, exactly like <see cref="PptxDocument.ResolveShapeGeometry"/>
///     does.
///     </para>
///     <para>
///     <b>Adjustment-value scope.</b> OOXML preset shapes are parameterized by one or more named
///     "adjustment values" (<c>&lt;a:avLst&gt;/&lt;a:gd name="adj" fmla="val NNNNN"/&gt;</c>) a
///     document may override to reshape a preset (for example a <c>roundRect</c>'s corner
///     radius). This phase does not parse <c>&lt;a:avLst&gt;</c> at all - every preset below is
///     built using a single, fixed, documented proportion approximating that preset's own OOXML
///     schema default adjustment value(s), deliberately not pixel-exact to every PowerPoint
///     rendering of a shape with customized adjustment handles. Parsing <c>&lt;a:avLst&gt;</c> is
///     deferred to a later phase.
///     </para>
/// </remarks>
internal static class PptxPresetGeometry
{
    /// <summary>
    ///     Builds the <see cref="Path"/> for the named preset geometry, sized to
    ///     <c>(0,0)</c>-<c>(w,h)</c> local coordinates.
    /// </summary>
    /// <param name="prst">The preset geometry name (<c>&lt;a:prstGeom prst="..."/&gt;</c>'s value).</param>
    /// <param name="w">The shape's own declared width, in EMU. Must be finite and non-negative.</param>
    /// <param name="h">The shape's own declared height, in EMU. Must be finite and non-negative.</param>
    /// <returns>The built <see cref="Path"/>, or <see cref="Path.Empty"/> when <paramref name="w"/>/<paramref name="h"/> is non-positive.</returns>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <paramref name="prst"/> is not one of the supported preset names (feature
    ///     token <c>"pptx-preset-geometry"</c>).
    /// </exception>
    internal static Path Build(string prst, float w, float h)
    {
        if (w <= 0f || h <= 0f)
        {
            return Path.Empty;
        }

        return prst switch
        {
            "rect" => Path.Rectangle(0, 0, w, h),
            "roundRect" => Path.RoundRectangle(0, 0, w, h, MathF.Min(w, h) * (1f / 6f)),
            "ellipse" => Ellipse(w / 2f, h / 2f, w / 2f, h / 2f),
            "triangle" => Polygon(new Vector2(w / 2f, 0), new Vector2(w, h), new Vector2(0, h)),
            "rtTriangle" => Polygon(new Vector2(0, 0), new Vector2(0, h), new Vector2(w, h)),
            "diamond" => Polygon(new Vector2(w / 2f, 0), new Vector2(w, h / 2f), new Vector2(w / 2f, h), new Vector2(0, h / 2f)),
            "parallelogram" => Parallelogram(w, h),
            "trapezoid" => Trapezoid(w, h),
            "hexagon" => Hexagon(w, h),
            "octagon" => Octagon(w, h),
            "pentagon" => RegularPolygon(w, h, 5),
            "chevron" => Chevron(w, h),
            "homePlate" => HomePlate(w, h),
            "pie" => Pie(w, h),
            "donut" => Donut(w, h),
            "plus" => Plus(w, h),
            "rightArrow" => RightArrow(w, h),
            "leftArrow" => Mirror(RightArrow(w, h), w),
            "upArrow" => UpArrow(w, h, pointingDown: false),
            "downArrow" => UpArrow(w, h, pointingDown: true),
            "leftRightArrow" => LeftRightArrow(w, h),
            "upDownArrow" => UpDownArrow(w, h),
            "star4" => Star(w, h, 4, 0.42f),
            "star5" => Star(w, h, 5, 0.42f),
            _ => throw new PptxUnsupportedFeatureException(
                "pptx-preset-geometry",
                $"Preset geometry '{prst}' is not supported."),
        };
    }

    /// <summary>Builds a closed polygon from the given vertices, in order.</summary>
    private static Path Polygon(params Vector2[] points)
    {
        var builder = new PathBuilder().MoveTo(points[0]);
        for (var i = 1; i < points.Length; i++)
        {
            builder.LineTo(points[i]);
        }

        return builder.Close().Build();
    }

    /// <summary>
    ///     Builds an axis-aligned ellipse centered at <c>(cx,cy)</c> with radii <c>(rx,ry)</c>,
    ///     approximated by four cubic Bezier quadrants (the same kappa=0.5522847498 constant
    ///     <see cref="Path.Circle"/> uses, generalized to independent x/y radii).
    /// </summary>
    /// <param name="cx">The ellipse's center x-coordinate.</param>
    /// <param name="cy">The ellipse's center y-coordinate.</param>
    /// <param name="rx">The ellipse's x-radius.</param>
    /// <param name="ry">The ellipse's y-radius.</param>
    /// <param name="reversed">
    ///     When <see langword="true"/>, the ellipse is traversed in the opposite winding order -
    ///     used by <see cref="Donut"/> to cut a hole out of an outer ellipse under the nonzero
    ///     fill rule.
    /// </param>
    private static Path Ellipse(float cx, float cy, float rx, float ry, bool reversed = false)
    {
        const float kappa = 0.5522847498f;
        var ox = rx * kappa;
        var oy = ry * kappa;

        var right = new Vector2(cx + rx, cy);
        var bottom = new Vector2(cx, cy + ry);
        var left = new Vector2(cx - rx, cy);
        var top = new Vector2(cx, cy - ry);

        var builder = new PathBuilder();
        if (!reversed)
        {
            builder.MoveTo(right)
                .CubicBezierTo(new Vector2(cx + rx, cy + oy), new Vector2(cx + ox, cy + ry), bottom)
                .CubicBezierTo(new Vector2(cx - ox, cy + ry), new Vector2(cx - rx, cy + oy), left)
                .CubicBezierTo(new Vector2(cx - rx, cy - oy), new Vector2(cx - ox, cy - ry), top)
                .CubicBezierTo(new Vector2(cx + ox, cy - ry), new Vector2(cx + rx, cy - oy), right);
        }
        else
        {
            builder.MoveTo(right)
                .CubicBezierTo(new Vector2(cx + rx, cy - oy), new Vector2(cx + ox, cy - ry), top)
                .CubicBezierTo(new Vector2(cx - ox, cy - ry), new Vector2(cx - rx, cy - oy), left)
                .CubicBezierTo(new Vector2(cx - rx, cy + oy), new Vector2(cx - ox, cy + ry), bottom)
                .CubicBezierTo(new Vector2(cx + ox, cy + ry), new Vector2(cx + rx, cy + oy), right);
        }

        return builder.Close().Build();
    }

    private static Path Parallelogram(float w, float h)
    {
        var slant = w * 0.25f;
        return Polygon(
            new Vector2(slant, 0), new Vector2(w, 0),
            new Vector2(w - slant, h), new Vector2(0, h));
    }

    private static Path Trapezoid(float w, float h)
    {
        var inset = w * 0.25f;
        return Polygon(
            new Vector2(inset, 0), new Vector2(w - inset, 0),
            new Vector2(w, h), new Vector2(0, h));
    }

    private static Path Hexagon(float w, float h)
    {
        var inset = w * 0.25f;
        return Polygon(
            new Vector2(inset, 0), new Vector2(w - inset, 0), new Vector2(w, h / 2f),
            new Vector2(w - inset, h), new Vector2(inset, h), new Vector2(0, h / 2f));
    }

    private static Path Octagon(float w, float h)
    {
        var insetX = w * 0.2929f;
        var insetY = h * 0.2929f;
        return Polygon(
            new Vector2(insetX, 0), new Vector2(w - insetX, 0),
            new Vector2(w, insetY), new Vector2(w, h - insetY),
            new Vector2(w - insetX, h), new Vector2(insetX, h),
            new Vector2(0, h - insetY), new Vector2(0, insetY));
    }

    /// <summary>Builds a regular N-gon inscribed in the (w,h) bounding box, apex pointing up.</summary>
    private static Path RegularPolygon(float w, float h, int sides)
    {
        var cx = w / 2f;
        var cy = h / 2f;
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
        {
            var angle = -MathF.PI / 2f + i * (2f * MathF.PI / sides);
            points[i] = new Vector2(cx + cx * MathF.Cos(angle), cy + cy * MathF.Sin(angle));
        }

        return Polygon(points);
    }

    private static Path Chevron(float w, float h)
    {
        var notch = w * 0.25f;
        return Polygon(
            new Vector2(0, 0), new Vector2(w - notch, 0), new Vector2(w, h / 2f),
            new Vector2(w - notch, h), new Vector2(0, h), new Vector2(notch, h / 2f));
    }

    private static Path HomePlate(float w, float h)
    {
        var notch = w * 0.25f;
        return Polygon(
            new Vector2(0, 0), new Vector2(w - notch, 0), new Vector2(w, h / 2f),
            new Vector2(w - notch, h), new Vector2(0, h));
    }

    /// <summary>
    ///     Builds a pie-slice sector of the (w,h) bounding ellipse, using the OOXML preset's own
    ///     schema default angles (start=0 degrees, sweep=270 degrees - a three-quarter pie).
    /// </summary>
    private static Path Pie(float w, float h)
    {
        var cx = w / 2f;
        var cy = h / 2f;
        var rx = w / 2f;
        var ry = h / 2f;

        const float startDeg = 0f;
        const float sweepDeg = 270f;
        var start = new Vector2(cx + rx * MathF.Cos(startDeg * MathF.PI / 180f), cy + ry * MathF.Sin(startDeg * MathF.PI / 180f));
        var end = new Vector2(
            cx + rx * MathF.Cos((startDeg + sweepDeg) * MathF.PI / 180f),
            cy + ry * MathF.Sin((startDeg + sweepDeg) * MathF.PI / 180f));

        return new PathBuilder()
            .MoveTo(new Vector2(cx, cy))
            .LineTo(start)
            .ArcTo(new Vector2(rx, ry), 0f, largeArc: sweepDeg > 180f, sweep: true, end)
            .Close()
            .Build();
    }

    /// <summary>
    ///     Builds a ring (outer ellipse with a concentric inner hole), using the OOXML preset's
    ///     own schema default hole-size adjustment value (25% of the outer radius).
    /// </summary>
    private static Path Donut(float w, float h)
    {
        var cx = w / 2f;
        var cy = h / 2f;
        var outer = Ellipse(cx, cy, w / 2f, h / 2f);
        var inner = Ellipse(cx, cy, w / 2f * 0.25f, h / 2f * 0.25f, reversed: true);
        return CombineSubpaths(outer, inner);
    }

    private static Path Plus(float w, float h)
    {
        var thickness = MathF.Min(w, h) * 0.3f;
        var vx0 = (w - thickness) / 2f;
        var vx1 = (w + thickness) / 2f;
        var vy0 = (h - thickness) / 2f;
        var vy1 = (h + thickness) / 2f;

        return Polygon(
            new Vector2(vx0, 0), new Vector2(vx1, 0), new Vector2(vx1, vy0),
            new Vector2(w, vy0), new Vector2(w, vy1), new Vector2(vx1, vy1),
            new Vector2(vx1, h), new Vector2(vx0, h), new Vector2(vx0, vy1),
            new Vector2(0, vy1), new Vector2(0, vy0), new Vector2(vx0, vy0));
    }

    private static Path RightArrow(float w, float h)
    {
        var headLen = w * 0.4f;
        var shaftY0 = h * 0.25f;
        var shaftY1 = h * 0.75f;

        return Polygon(
            new Vector2(0, shaftY0), new Vector2(w - headLen, shaftY0), new Vector2(w - headLen, 0),
            new Vector2(w, h / 2f),
            new Vector2(w - headLen, h), new Vector2(w - headLen, shaftY1), new Vector2(0, shaftY1));
    }

    private static Path LeftRightArrow(float w, float h)
    {
        var headLen = w * 0.3f;
        var shaftY0 = h * 0.25f;
        var shaftY1 = h * 0.75f;

        return Polygon(
            new Vector2(0, h / 2f),
            new Vector2(headLen, 0), new Vector2(headLen, shaftY0),
            new Vector2(w - headLen, shaftY0), new Vector2(w - headLen, 0),
            new Vector2(w, h / 2f),
            new Vector2(w - headLen, h), new Vector2(w - headLen, shaftY1),
            new Vector2(headLen, shaftY1), new Vector2(headLen, h));
    }

    /// <summary>Mirrors a path horizontally within the (w,0) bounding width - used to derive <c>leftArrow</c> from <c>rightArrow</c>.</summary>
    private static Path Mirror(Path path, float w) => path.Transform(new Matrix3x2(-1, 0, 0, 1, w, 0));

    /// <summary>
    ///     Maps a path built in a swapped-axis local box into the final (w,h) box via
    ///     <c>finalX = localY</c>, <c>finalY = h - localX</c> - used to derive
    ///     <c>upArrow</c>/<c>downArrow</c>/<c>upDownArrow</c> from their horizontal counterparts
    ///     (built pointing toward local +x) without duplicating their vertex geometry: local +x
    ///     (the horizontal arrow's own pointing direction) rotates to final -y ("up").
    /// </summary>
    private static Path RotateQuarter(Path path, float h)
    {
        var transform = new Matrix3x2(0, -1, 1, 0, 0, h);
        return path.Transform(transform);
    }

    /// <summary>Builds <c>upArrow</c>/<c>downArrow</c> by rotating a <see cref="RightArrow"/> built in a swapped-axis local box.</summary>
    private static Path UpArrow(float w, float h, bool pointingDown)
    {
        var swappedWidth = h;
        var swappedHeight = w;
        var arrow = RightArrow(swappedWidth, swappedHeight);
        if (pointingDown)
        {
            arrow = Mirror(arrow, swappedWidth);
        }

        return RotateQuarter(arrow, h);
    }

    /// <summary>Builds <c>upDownArrow</c> by rotating a <see cref="LeftRightArrow"/> built in a swapped-axis local box.</summary>
    private static Path UpDownArrow(float w, float h)
    {
        var swappedWidth = h;
        var swappedHeight = w;
        return RotateQuarter(LeftRightArrow(swappedWidth, swappedHeight), h);
    }

    /// <summary>
    ///     Builds a 2N-point star (N outer points, N inner points, alternating), apex pointing up.
    /// </summary>
    /// <param name="w">The bounding box width.</param>
    /// <param name="h">The bounding box height.</param>
    /// <param name="points">The number of star points (N).</param>
    /// <param name="innerRatio">The inner vertex radius as a fraction of the outer radius.</param>
    private static Path Star(float w, float h, int points, float innerRatio)
    {
        var cx = w / 2f;
        var cy = h / 2f;
        var vertices = new Vector2[points * 2];
        for (var i = 0; i < points * 2; i++)
        {
            var angle = -MathF.PI / 2f + i * (MathF.PI / points);
            var radius = (i % 2 == 0) ? 1f : innerRatio;
            vertices[i] = new Vector2(cx + cx * radius * MathF.Cos(angle), cy + cy * radius * MathF.Sin(angle));
        }

        return Polygon(vertices);
    }

    /// <summary>
    ///     Combines two already-built paths' subpaths into a single <see cref="Path"/> (used by
    ///     <see cref="Donut"/> to combine its outer and inner ellipse subpaths into one
    ///     nonzero-fill-rule ring).
    /// </summary>
    private static Path CombineSubpaths(Path first, Path second)
    {
        var builder = new PathBuilder();
        AppendSubpaths(builder, first);
        AppendSubpaths(builder, second);
        return builder.Build();
    }

    /// <summary>Replays every subpath of <paramref name="path"/> into <paramref name="builder"/>.</summary>
    private static void AppendSubpaths(PathBuilder builder, Path path)
    {
        foreach (var subpath in path.Subpaths)
        {
            builder.MoveTo(subpath.Start);
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(command.EndPoint);
                        break;
                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(command.Control1, command.EndPoint);
                        break;
                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(command.Control1, command.Control2, command.EndPoint);
                        break;
                    case PathCommandType.ArcTo:
                        builder.ArcTo(command.Radius, command.RotationDegrees, command.LargeArc, command.Sweep, command.EndPoint);
                        break;
                    case PathCommandType.Close:
                        builder.Close();
                        break;
                }
            }
        }
    }
}
