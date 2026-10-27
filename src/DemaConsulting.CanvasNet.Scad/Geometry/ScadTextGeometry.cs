// cspell:ignore notdef halign unioned
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the <c>text()</c> module: lays a string out with a TrueType font and turns the glyph outlines into 2D
///     regions.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a separate unit.</b> Text needs font resolution, layout, curve flattening and a contour policy that
///         none of the other primitives have, with its own failure modes (missing fonts, missing glyphs, damaged
///         outlines). The result is an ordinary 2D shape (see <see cref="ScadShape2D"/>), so everything that
///         consumes 2D shapes consumes text: transforms, 2D booleans, <c>hull</c>, <c>linear_extrude</c>,
///         <c>rotate_extrude</c>, the slab of a top-level 2D shape, <c>color</c> and the modifiers. Each glyph is one
///         region leaf of a union, so glyphs that touch or overlap one another (kerning, italics) are not a problem;
///         only the contours of a single glyph must form a valid region.
///     </para>
///     <para>
///         <b>Size.</b> The reference height of a font is the ink height of the glyph <c>H</c> in font design units
///         (fixed-tolerance flattening of its outline; when the font has no usable <c>H</c>, the larger of the absolute
///         ascender and the units per em). <c>size</c> is the height in model units of that reference, so an upper
///         case <c>H</c> is exactly <c>size</c> tall: <c>scale = size / referenceHeight</c>. This is a deterministic,
///         per-font cap-height rule; it is the documented approximation of the way other tools relate size to a font,
///         not a claim of identical results.
///     </para>
///     <para>
///         <b>Layout.</b> Glyphs are placed one Unicode scalar at a time with no shaping, ligatures or combining
///         marks (<c>language</c> and <c>script</c> are accepted and ignored). With <c>ltr</c> the pen advances in +X;
///         <c>rtl</c> lays the reversed scalar sequence out left to right (no bidirectional or Arabic shaping);
///         <c>ttb</c> and <c>btt</c> stack upright glyphs down or up along Y. For horizontal text the pen moves by
///         <c>kerning(previous, current) * scale + advance(current) * scale * spacing</c> (the glyph is placed at the
///         pen position after the kerning step, then the pen moves by the advance); vertical text applies the same
///         spacing to the advance on Y and no kerning. The baseline of the first glyph is Y = 0.
///     </para>
///     <para>
///         <b>Alignment</b> uses the measured ink extents of all placed glyphs, not the advance box: <c>halign</c>
///         <c>left</c> (the default) moves the minimum X to 0, <c>center</c> the middle of the X extent, <c>right</c>
///         the maximum X; <c>valign</c> <c>baseline</c> (the default) moves nothing, <c>top</c> the maximum Y to 0,
///         <c>center</c> the middle of the Y extent and <c>bottom</c> the minimum Y.
///     </para>
///     <para>
///         <b>Failure policy.</b> Invalid arguments are warnings and yield nothing; an empty string yields nothing
///         silently; control characters are skipped silently; a character the font does not map is drawn with the
///         font's <c>.notdef</c> glyph, or skipped with one warning when that glyph has no outline; a damaged glyph,
///         a mapped glyph whose subpaths are all degenerate (no area, so it is not an empty glyph like a space),
///         or a glyph whose contours cross or touch each other other than as a hole or an island or as the overlapping
///         solids of a composite glyph (which are unioned), is reported as the unsupported feature
///         <c>scad-text-invalid-outline</c> or <c>scad-text-overlapping-contours</c> and the whole instance is omitted.
///         The triangle budget is checked (with the lower bound of the slab of the vertices so far) after every glyph,
///         cancellation is polled at the same points, and nothing recurses.
///     </para>
///     <para>
///         The methods are stateless (the font resolver belongs to the context) and thread-safe.
///     </para>
/// </remarks>
internal static partial class ScadTextGeometry
{
    /// <summary>The default <c>size</c>.</summary>
    public const double DefaultSize = 10.0;

    /// <summary>The longest text (in UTF-16 units) that a non-string value is converted to.</summary>
    private const int MaxConvertedLength = 1 << 20;

    /// <summary>The parameter names of <c>text</c> in positional order.</summary>
    private static readonly string[] Parameters = ["text", "size", "font", "halign", "valign", "spacing", "direction", "language", "script"];

    /// <summary>
    ///     The text direction.
    /// </summary>
    private enum Direction
    {
        /// <summary>Left to right.</summary>
        LeftToRight,

        /// <summary>Right to left (reversed order, no shaping).</summary>
        RightToLeft,

        /// <summary>Top to bottom (upright glyphs stacked downwards).</summary>
        TopToBottom,

        /// <summary>Bottom to top (upright glyphs stacked upwards).</summary>
        BottomToTop,
    }

    /// <summary>
    ///     The validated arguments of one <c>text()</c>.
    /// </summary>
    /// <param name="Text">The text.</param>
    /// <param name="Size">The size.</param>
    /// <param name="Spacing">The advance multiplier.</param>
    /// <param name="HAlign">The horizontal alignment: -1 left, 0 center, 1 right.</param>
    /// <param name="VAlign">The vertical alignment: -1 bottom, 0 center, 1 top, 2 baseline.</param>
    /// <param name="Direction">The direction.</param>
    /// <param name="Font">The font request.</param>
    private sealed record Request(string Text, double Size, double Spacing, int HAlign, int VAlign, Direction Direction, ScadFontSpec Font);

    /// <summary>
    ///     One glyph placed on the line, still in model units and before alignment.
    /// </summary>
    /// <param name="Contours">The flattened contours of the glyph.</param>
    private sealed record Placed(Vector2D[][] Contours);

    /// <summary>
    ///     Builds a <c>text(text, size, font, halign, valign, spacing, direction, language, script)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A flat shape, or <see cref="ScadGeo.Empty"/> when the text is empty, an argument is invalid or the outlines are rejected.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the triangle budget would be exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the build is canceled.</exception>
    public static ScadGeo Build(ScadInstance node, ScadGeometryContext context)
    {
        // A text has no use for children; saying so beats silently dropping them
        if (node.Children.Count > 0)
        {
            context.Reporter.Warn($"{node.Name}() takes no child objects; ignoring them", node.Location);
        }

        var request = ReadRequest(ScadArguments.Bind(node, Parameters, context), node, context);
        if (request is null || request.Text.Length == 0)
        {
            return ScadGeo.Empty;
        }

        // Everything below is heavy compared with the checks above: give cancellation a chance first
        context.Poll();
        var resolved = context.FontResolver.Resolve(request.Font);
        if (resolved.Warning is not null)
        {
            context.Reporter.Warn($"{node.Name}: {resolved.Warning}", node.Location);
        }

        var placed = Layout(request, resolved.Font, node, context, out var invalid);
        if (invalid)
        {
            return ScadGeo.Empty;
        }

        return MakeShape(placed, request, node, context);
    }

    /// <summary>
    ///     Reads and validates the arguments.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The request, or <see langword="null"/> when an argument is invalid (a warning has been reported).</returns>
    private static Request? ReadRequest(ScadArguments args, ScadInstance node, ScadGeometryContext context)
    {
        // The text itself: a string, or anything else converted the way str() converts it
        if (!args.TryGet("text", out var textValue))
        {
            return Invalid(node, context, "text is required");
        }

        string text;
        if (textValue.Kind == ScadValueKind.String)
        {
            text = textValue.AsString;
        }
        else if (textValue.Kind == ScadValueKind.Function || !textValue.TryToStr(MaxConvertedLength, out text))
        {
            return Invalid(node, context, "text must be a string (or a value that converts to a reasonably short one)");
        }

        // size and spacing: finite numbers in range
        var size = DefaultSize;
        if (args.TryGet("size", out var sizeValue) && (!ScadArguments.TryNumber(sizeValue, out size) || size <= 0.0))
        {
            return Invalid(node, context, "size must be a finite number greater than zero");
        }

        var spacing = 1.0;
        if (args.TryGet("spacing", out var spacingValue) && (!ScadArguments.TryNumber(spacingValue, out spacing) || spacing < 0.0))
        {
            return Invalid(node, context, "spacing must be a finite number that is not negative");
        }

        // The keyword arguments, as indices into their keyword tables
        var halign = -1;
        if (args.TryGet("halign", out var halignValue))
        {
            if (!TryKeyword(halignValue, ["left", "center", "right"], out var index))
            {
                return Invalid(node, context, "halign must be \"left\", \"center\" or \"right\"");
            }

            // left, center, right = -1, 0, 1
            halign = index - 1;
        }

        var valign = 2;
        if (args.TryGet("valign", out var valignValue))
        {
            if (!TryKeyword(valignValue, ["bottom", "center", "top", "baseline"], out var index))
            {
                return Invalid(node, context, "valign must be \"baseline\", \"bottom\", \"center\" or \"top\"");
            }

            // bottom, center, top = -1, 0, 1 and baseline = 2
            valign = index == 3 ? 2 : index - 1;
        }

        var direction = Direction.LeftToRight;
        if (args.TryGet("direction", out var directionValue))
        {
            if (!TryKeyword(directionValue, ["ltr", "rtl", "ttb", "btt"], out var index))
            {
                return Invalid(node, context, "direction must be \"ltr\", \"rtl\", \"ttb\" or \"btt\"");
            }

            direction = (Direction)index;
        }

        // The font: a string; language and script are accepted and deliberately ignored
        var font = new ScadFontSpec(string.Empty, false, false);
        if (args.TryGet("font", out var fontValue))
        {
            if (fontValue.Kind != ScadValueKind.String)
            {
                return Invalid(node, context, "font must be a string");
            }

            font = ScadFontResolver.ParseSpec(fontValue.AsString);
        }

        return new Request(text, size, spacing, halign, valign, direction, font);
    }

    /// <summary>
    ///     Reads a keyword argument (case-insensitive, surrounding white space ignored) and returns its index in a table.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="keywords">The accepted keywords.</param>
    /// <param name="index">The index of the keyword in <paramref name="keywords"/>.</param>
    /// <returns><see langword="true"/> when the value is one of the keywords.</returns>
    private static bool TryKeyword(ScadValue value, string[] keywords, out int index)
    {
        index = 0;
        if (value.Kind != ScadValueKind.String)
        {
            return false;
        }

        for (var i = 0; i < keywords.Length; i++)
        {
            if (keywords[i].Equals(value.AsString.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Reports an invalid argument as a warning and returns <see langword="null"/>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="problem">What is wrong, without the module name.</param>
    /// <returns><see langword="null"/>.</returns>
    private static Request? Invalid(ScadInstance node, ScadGeometryContext context, string problem)
    {
        context.Reporter.Warn($"{node.Name}: {problem}; the instance is ignored", node.Location);
        return null;
    }

    /// <summary>
    ///     Lays the glyphs out: resolves each scalar to a glyph, flattens its outline and places it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="font">The font.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="invalid">Set when a damaged outline or an out-of-range coordinate was reported and the instance is to be omitted.</param>
    /// <returns>The placed glyphs that have an outline, in drawing order.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the triangle budget would be exceeded.</exception>
    private static List<Placed> Layout(Request request, TrueTypeFont font, ScadInstance node, ScadGeometryContext context, out bool invalid)
    {
        invalid = false;
        var result = new List<Placed>();

        // The scale from font units to model units, and the flattening tolerance in font units
        var scale = request.Size / ReferenceHeight(font, context);
        var fragments = Math.Max(ScadFragmentRule.MinimumFragments, ScadFragmentRule.Fragments(request.Size / 2.0, node.Fragments));
        var tolerance = request.Size / 2.0 * (1.0 - Math.Cos(Math.PI / fragments)) / scale;

        // The scalars in layout order: right to left text is simply laid out reversed
        var scalars = new List<int>();
        foreach (var rune in request.Text.EnumerateRunes())
        {
            scalars.Add(rune.Value);
        }

        if (request.Direction == Direction.RightToLeft)
        {
            scalars.Reverse();
        }

        var vertical = request.Direction is Direction.TopToBottom or Direction.BottomToTop;
        var cache = new Dictionary<int, Vector2D[][]>();
        var missing = new List<int>();
        var pen = 0.0;
        var previousGlyph = -1;
        long vertices = 0;
        foreach (var scalar in scalars)
        {
            // Control characters (line feeds, tabs, ...) have no shape in a single line of text
            if (Rune.IsControl(new Rune(scalar)))
            {
                continue;
            }

            var glyph = font.GetGlyphIndex(scalar);
            if (!cache.TryGetValue(glyph, out var contours))
            {
                if (!TryFlatten(font, glyph, tolerance, context, out contours, out var degenerate))
                {
                    ReportInvalid(node, context, ScadFeatureIds.TextInvalidOutline, $"the outline of the glyph for U+{scalar:X4} is damaged");
                    invalid = true;
                    return result;
                }

                // A mapped glyph that draws something but encloses nothing is unusable, which is not the same as a space
                if (degenerate && glyph != 0)
                {
                    ReportInvalid(node, context, ScadFeatureIds.TextInvalidOutline, $"the outline of the glyph for U+{scalar:X4} has no contour with an area");
                    invalid = true;
                    return result;
                }

                cache[glyph] = contours;
            }

            // A glyph without an outline: .notdef (a missing character) is skipped with a warning, a space just advances
            if (contours.Length == 0 && glyph == 0)
            {
                missing.Add(scalar);
                continue;
            }

            // Kerning moves the pen before the glyph is placed (horizontal text only), the advance after
            if (!vertical && previousGlyph >= 0)
            {
                pen += font.GetKerning(previousGlyph, glyph) * scale;
            }

            var origin = pen;
            var advance = font.GetAdvanceWidth(glyph) * scale * request.Spacing;
            pen += request.Direction == Direction.TopToBottom ? -advance : advance;
            previousGlyph = glyph;
            if (contours.Length == 0)
            {
                continue;
            }

            var placed = Place(contours, scale, origin, vertical);
            result.Add(new Placed(placed));
            foreach (var contour in placed)
            {
                vertices += contour.Length;
            }

            // The slab of what is placed so far must still fit the budget, and cancellation gets its chance
            context.EnsureTriangles(ScadPrimitives.SlabTrianglesLowerBound(vertices), node.Location);
            context.Poll();
        }

        if (missing.Count > 0)
        {
            var list = string.Join(", ", missing.Distinct().Take(8).Select(s => string.Create(CultureInfo.InvariantCulture, $"U+{s:X4}")));
            context.Reporter.Warn(
                $"{node.Name}: the font has no glyph for {list}{(missing.Distinct().Count() > 8 ? ", ..." : string.Empty)} and its .notdef glyph has no outline; those characters are skipped",
                node.Location);
        }

        return result;
    }

    /// <summary>
    ///     Scales a glyph's contours into model units and moves them to the pen position.
    /// </summary>
    /// <param name="contours">The flattened contours in font units.</param>
    /// <param name="scale">The font-unit to model-unit factor.</param>
    /// <param name="origin">The pen position along the text direction.</param>
    /// <param name="vertical">Whether the pen runs along Y instead of X.</param>
    /// <returns>New contours in model units.</returns>
    private static Vector2D[][] Place(Vector2D[][] contours, double scale, double origin, bool vertical)
    {
        var result = new Vector2D[contours.Length][];
        for (var c = 0; c < contours.Length; c++)
        {
            var source = contours[c];
            var target = new Vector2D[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var x = source[i].X * scale;
                var y = source[i].Y * scale;
                target[i] = vertical ? new Vector2D(x, y + origin) : new Vector2D(x + origin, y);
            }

            result[c] = target;
        }

        return result;
    }

    /// <summary>
    ///     Aligns the placed glyphs by their ink extents and makes the 2D shape.
    /// </summary>
    /// <param name="placed">The placed glyphs.</param>
    /// <param name="request">The request.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The shape, or <see cref="ScadGeo.Empty"/> when there is no outline or a region was rejected.</returns>
    private static ScadGeo MakeShape(List<Placed> placed, Request request, ScadInstance node, ScadGeometryContext context)
    {
        if (placed.Count == 0)
        {
            return ScadGeo.Empty;
        }

        // The measured ink extents of everything placed
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var glyph in placed)
        {
            foreach (var contour in glyph.Contours)
            {
                foreach (var point in contour)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }
        }

        // A size so large that the coordinates leave the supported range is refused before regions are built
        if (!new[] { minX, minY, maxX, maxY }.All(v => double.IsFinite(v) && Math.Abs(v) <= ScadGeo.MaxCoordinate / 4.0))
        {
            context.Reporter.Warn($"{node.Name}: the text is too large (a coordinate is outside the supported range); the instance is ignored", node.Location);
            return ScadGeo.Empty;
        }

        var shiftX = request.HAlign switch
        {
            -1 => -minX,
            0 => -(minX + maxX) / 2.0,
            _ => -maxX,
        };
        var shiftY = request.VAlign switch
        {
            -1 => -minY,
            0 => -(minY + maxY) / 2.0,
            1 => -maxY,
            _ => 0.0,
        };

        // One or more regions per glyph; a union of leaves keeps touching neighbors and overlapping components valid
        var regions = new List<PolygonRegion>(placed.Count);
        foreach (var glyph in placed)
        {
            context.Poll();
            var shifted = glyph.Contours.Select(c => c.Select(p => new Vector2D(p.X + shiftX, p.Y + shiftY)).ToArray()).ToArray();
            var made = TryMakeRegions(shifted, out var feature);
            if (made is null)
            {
                ReportInvalid(
                    node,
                    context,
                    feature!,
                    feature == ScadFeatureIds.TextOverlappingContours
                        ? "the contours of a glyph overlap or touch each other in a way that is not a hole or an island"
                        : "the outline of a glyph is not a valid planar shape (a contour crosses itself or is degenerate)");
                return ScadGeo.Empty;
            }

            regions.AddRange(made);
        }

        return Assemble(regions, node);
    }

    /// <summary>
    ///     Makes the regions of one glyph from its contours, telling an invalid contour from contours that overlap.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The region normalizes the winding by nesting, so holes and islands are fine whatever direction the font
    ///         drew them in, and ordinary glyphs are one region. It rejects a contour that crosses or touches itself,
    ///         and contours that cross or touch one another. Composite glyphs (an <c>A</c> with a ring, a <c>C</c>
    ///         with a cedilla) are drawn by fonts as overlapping solids, which the nonzero fill rule reads as their
    ///         union; so when the whole is rejected but every contour is valid alone, the glyph is split into
    ///         components: the contours that wind like the largest one are solids, the others are holes, every hole is
    ///         given to the smallest solid that contains it, and each solid with its holes is one region (the regions
    ///         are unioned by the caller). When a component is still rejected the contours overlap in a way that is
    ///         neither a hole nor an island.
    ///     </para>
    ///     <para>
    ///         The split does not tell overlapping solids from solids that merely touch: touching solids are accepted
    ///         as separate regions, like touching glyphs, and the contact is left to the union of the consumers
    ///         (documented limit; see the design of the unit).
    ///     </para>
    /// </remarks>
    /// <param name="contours">The contours of one glyph in model units.</param>
    /// <param name="feature">The feature to report when no regions can be made: <see cref="ScadFeatureIds.TextInvalidOutline"/> or <see cref="ScadFeatureIds.TextOverlappingContours"/>.</param>
    /// <returns>The regions (one for an ordinary glyph), or <see langword="null"/> when the contours were rejected.</returns>
    internal static IReadOnlyList<PolygonRegion>? TryMakeRegions(IReadOnlyList<Vector2D[]> contours, out string? feature)
    {
        feature = null;
        try
        {
            return [new PolygonRegion(contours)];
        }
        catch (ArgumentException)
        {
            // A contour that is invalid alone is an invalid outline; otherwise the contours conflict with one another
            foreach (var contour in contours)
            {
                try
                {
                    _ = new PolygonRegion([contour]);
                }
                catch (ArgumentException)
                {
                    feature = ScadFeatureIds.TextInvalidOutline;
                    return null;
                }
            }
        }

        // The overlapping solids of a composite glyph: split into components
        var components = SplitComponents(contours);
        if (components is not null)
        {
            try
            {
                return [.. components.Select(component => new PolygonRegion(component))];
            }
            catch (ArgumentException)
            {
                // Fall through: a component that is still invalid overlaps in a way no union can express
            }
        }

        feature = ScadFeatureIds.TextOverlappingContours;
        return null;
    }

    /// <summary>
    ///     Splits the contours of a glyph into solids, each with the holes that lie inside it.
    /// </summary>
    /// <param name="contours">The contours; each valid on its own.</param>
    /// <returns>One contour list per solid (the solid first), or <see langword="null"/> when a hole lies in no solid.</returns>
    private static List<List<Vector2D[]>>? SplitComponents(IReadOnlyList<Vector2D[]> contours)
    {
        // The largest contour is an outer boundary, and its winding is the winding of solids
        var areas = contours.Select(SignedArea).ToArray();
        var largest = Array.IndexOf(areas, areas.MaxBy(Math.Abs));
        var solidSign = Math.Sign(areas[largest]);
        var components = new List<(double Area, List<Vector2D[]> Contours)>();
        var holes = new List<(double Area, Vector2D[] Contour)>();
        for (var i = 0; i < contours.Count; i++)
        {
            if (Math.Sign(areas[i]) == solidSign)
            {
                components.Add((Math.Abs(areas[i]), [contours[i]]));
            }
            else
            {
                holes.Add((Math.Abs(areas[i]), contours[i]));
            }
        }

        // A hole belongs to the smallest solid that contains one of its points
        foreach (var (_, hole) in holes)
        {
            var owner = -1;
            for (var i = 0; i < components.Count; i++)
            {
                if (Contains(components[i].Contours[0], hole[0]) && (owner < 0 || components[i].Area < components[owner].Area))
                {
                    owner = i;
                }
            }

            if (owner < 0)
            {
                return null;
            }

            components[owner].Contours.Add(hole);
        }

        return [.. components.Select(c => c.Contours)];
    }

    /// <summary>
    ///     Computes the signed area of a polygon (positive for counter-clockwise).
    /// </summary>
    /// <param name="contour">The polygon.</param>
    /// <returns>The signed area.</returns>
    private static double SignedArea(Vector2D[] contour)
    {
        var sum = 0.0;
        for (var i = 0; i < contour.Length; i++)
        {
            sum += Vector2D.Cross(contour[i], contour[(i + 1) % contour.Length]);
        }

        return sum / 2.0;
    }

    /// <summary>
    ///     Tests whether a point is inside a polygon (even-odd ray casting).
    /// </summary>
    /// <param name="polygon">The polygon.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    private static bool Contains(Vector2D[] polygon, Vector2D point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    ///     Combines the glyph regions into one flat result.
    /// </summary>
    /// <param name="regions">The regions; at least one.</param>
    /// <param name="node">The instance.</param>
    /// <returns>A single region, or the union of the regions.</returns>
    private static ScadGeo Assemble(List<PolygonRegion> regions, ScadInstance node)
    {
        if (regions.Count == 1)
        {
            return ScadGeo.FromRegion(regions[0], node.Location);
        }

        var bounds = BoundingBox3D.Empty;
        foreach (var region in regions)
        {
            foreach (var vertex in region.Vertices)
            {
                bounds = bounds.Union(new Vector3D(vertex.X, vertex.Y, 0.0));
            }
        }

        var shape = ScadShape2D.CreateCsg(CsgOperation.Union, [.. regions.Select(r => ScadShape2D.CreateLeaf(r, node.Location))]);
        return ScadGeo.FromShape(shape, bounds, true, false, node.Location);
    }

    /// <summary>
    ///     Reports a rejected outline as an unsupported feature.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="feature">The feature identifier.</param>
    /// <param name="why">What was wrong.</param>
    private static void ReportInvalid(ScadInstance node, ScadGeometryContext context, string feature, string why) =>
        context.Reporter.Unsupported(feature, $"{node.Name}: {why}; the whole text is omitted", node.Location);
}
