// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
// cspell:ignore aliceblue antiquewhite blanchedalmond blueviolet burlywood cadetblue cornflowerblue
// cspell:ignore cornsilk darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta
// cspell:ignore darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue
// cspell:ignore darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray
// cspell:ignore dimgrey dodgerblue floralwhite forestgreen gainsboro ghostwhite greenyellow hotpink
// cspell:ignore indianred lavenderblush lawngreen lemonchiffon lightcoral lightcyan
// cspell:ignore lightgoldenrodyellow lightgray lightgreen lightpink lightsalmon lightseagreen
// cspell:ignore lightskyblue lightslategray lightslategrey lightsteelblue lightyellow limegreen
// cspell:ignore mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue
// cspell:ignore mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose
// cspell:ignore navajowhite oldlace olivedrab orangered palegoldenrod palegreen paleturquoise
// cspell:ignore palevioletred papayawhip peachpuff powderblue rebeccapurple rosybrown royalblue
// cspell:ignore saddlebrown sandybrown seagreen skyblue slateblue slategray slategrey springgreen
// cspell:ignore steelblue whitesmoke yellowgreen
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // Transform attribute parsing
    // ================================================================================================

    /// <summary>
    ///     Parses an element's <c>transform</c> attribute (a space/comma-separated list of
    ///     <c>translate</c>/<c>scale</c>/<c>rotate</c>/<c>skewX</c>/<c>skewY</c>/<c>matrix</c>
    ///     function calls) into a single combined matrix, folding the functions left-to-right in
    ///     the order they appear (each function's effect is applied before those to its right).
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>
    ///     The combined transform, or <see cref="Matrix3x2.Identity"/> if the attribute is absent
    ///     or blank.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but its syntax cannot be parsed as a function list.
    /// </exception>
    private static Matrix3x2 ParseTransformAttribute(XElement element) =>
        ParseTransformList((string?)element.Attribute("transform"));

    /// <summary>
    ///     Parses a <c>gradientTransform</c> attribute, in exactly the same function-list syntax
    ///     as the ordinary <c>transform</c> attribute.
    /// </summary>
    /// <param name="element">The gradient element to inspect.</param>
    /// <returns>The combined transform, or <see cref="Matrix3x2.Identity"/> if absent or blank.</returns>
    /// <exception cref="InvalidDataException">Thrown when the attribute is present but malformed.</exception>
    private static Matrix3x2 ParseGradientTransform(XElement element) =>
        ParseTransformList((string?)element.Attribute("gradientTransform"));

    /// <summary>
    ///     Parses a transform function-list value (shared by the <c>transform</c> and
    ///     <c>gradientTransform</c> attributes). Per the SVG specification, a function list is
    ///     equivalent to the matrix product of its individual functions in the order listed, and
    ///     - because that product is applied to a point as a column-vector left-multiply - the
    ///     <em>last</em>-listed function is the one actually applied to a point first, with the
    ///     <em>first</em>-listed function applied last (for example <c>"translate(10,20) rotate(30)"</c>
    ///     rotates a point first, then translates the rotated result).
    /// </summary>
    /// <param name="raw">The raw attribute value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The combined transform, or <see cref="Matrix3x2.Identity"/> if <paramref name="raw"/>
    ///     is <see langword="null"/> or blank.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="raw"/> is present but its syntax cannot be parsed as a
    ///     function list.
    /// </exception>
    private static Matrix3x2 ParseTransformList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Matrix3x2.Identity;
        }

        var result = Matrix3x2.Identity;
        var position = 0;
        while (TryReadTransformFunction(raw, ref position, out var function))
        {
            // Prepending (rather than appending) each newly-read function reproduces the SVG
            // spec's "rightmost function applied first" composition rule under this class's
            // row-vector Matrix3x2 convention (Vector2.Transform(p, A * B) applies A first, then
            // B) - see this method's remarks.
            result = function * result;
        }

        return result;
    }

    /// <summary>
    ///     Checks whether every component of <paramref name="transform"/> is a finite value.
    /// </summary>
    /// <param name="transform">The composed transform to check.</param>
    /// <returns>
    ///     <see langword="true"/> if all six components (<see cref="Matrix3x2.M11"/>,
    ///     <see cref="Matrix3x2.M12"/>, <see cref="Matrix3x2.M21"/>, <see cref="Matrix3x2.M22"/>,
    ///     <see cref="Matrix3x2.M31"/>, <see cref="Matrix3x2.M32"/>) are finite;
    ///     <see langword="false"/> if any is <c>NaN</c> or an infinity.
    /// </returns>
    /// <remarks>
    ///     Every individual transform-function literal parsed by <see cref="ParseTransformList"/>
    ///     is already validated finite on its own (via <see cref="TryReadNumber"/>), but composing
    ///     several individually-finite transforms across nested elements (each cross-element
    ///     product, not any single function's own arguments) can still overflow to a non-finite
    ///     result - this helper lets each composition call site re-validate its own product before
    ///     letting it reach a <see cref="Drawing"/>-namespace constructor's own finiteness check,
    ///     which throws an uncaught <see cref="ArgumentOutOfRangeException"/> rather than this
    ///     codec's documented <see cref="InvalidDataException"/> contract.
    /// </remarks>
    private static bool IsFiniteTransform(Matrix3x2 transform) =>
        float.IsFinite(transform.M11) && float.IsFinite(transform.M12) &&
        float.IsFinite(transform.M21) && float.IsFinite(transform.M22) &&
        float.IsFinite(transform.M31) && float.IsFinite(transform.M32);

    /// <summary>
    ///     Attempts to read one <c>name(arguments)</c> transform function starting at
    ///     <paramref name="position"/>, advancing past it (and any trailing separators) on success.
    /// </summary>
    /// <param name="raw">The full <c>transform</c> attribute text.</param>
    /// <param name="position">The current scan position, advanced past the parsed function.</param>
    /// <param name="function">The parsed function's matrix, if this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> if a function was read; <see langword="false"/> if only
    ///     whitespace/separators remained (end of input).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when non-whitespace content remains but does not form a valid, recognized
    ///     transform function call.
    /// </exception>
    private static bool TryReadTransformFunction(string raw, ref int position, out Matrix3x2 function)
    {
        SkipSeparators(raw, ref position);
        if (position >= raw.Length)
        {
            function = Matrix3x2.Identity;
            return false;
        }

        var nameStart = position;
        while (position < raw.Length && char.IsLetter(raw[position]))
        {
            position++;
        }

        var name = raw[nameStart..position];
        SkipSeparators(raw, ref position);
        if (name.Length == 0 || position >= raw.Length || raw[position] != '(')
        {
            throw new InvalidDataException($"Malformed transform attribute near position {nameStart}.");
        }

        var closeIndex = raw.IndexOf(')', position);
        if (closeIndex < 0)
        {
            throw new InvalidDataException("Malformed transform attribute: unterminated function call.");
        }

        var argumentsText = raw[(position + 1)..closeIndex];
        position = closeIndex + 1;

        var arguments = ParseNumberList(argumentsText);
        function = BuildTransformFunction(name, arguments);
        return true;
    }

    /// <summary>
    ///     Builds the matrix for one named transform function given its parsed argument list.
    /// </summary>
    /// <param name="name">The function name (<c>translate</c>/<c>scale</c>/<c>rotate</c>/<c>skewX</c>/<c>skewY</c>/<c>matrix</c>).</param>
    /// <param name="arguments">The function's parsed numeric arguments.</param>
    /// <returns>The function's matrix.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="name"/> is not recognized, or <paramref name="arguments"/>
    ///     does not have one of the argument counts that function accepts.
    /// </exception>
    private static Matrix3x2 BuildTransformFunction(string name, List<float> arguments) => name switch
    {
        "translate" => BuildTranslate(arguments),
        "scale" => BuildScale(arguments),
        "rotate" => BuildRotate(arguments),
        "skewX" => arguments.Count == 1
            ? new Matrix3x2(1f, 0f, MathF.Tan(DegreesToRadians(arguments[0])), 1f, 0f, 0f)
            : throw new InvalidDataException("Malformed skewX transform function: expected exactly one argument."),
        "skewY" => arguments.Count == 1
            ? new Matrix3x2(1f, MathF.Tan(DegreesToRadians(arguments[0])), 0f, 1f, 0f, 0f)
            : throw new InvalidDataException("Malformed skewY transform function: expected exactly one argument."),
        "matrix" => arguments.Count == 6
            ? new Matrix3x2(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5])
            : throw new InvalidDataException("Malformed matrix transform function: expected exactly six arguments."),
        _ => throw new InvalidDataException($"Unrecognized transform function '{name}'.")
    };

    /// <summary>Builds a <c>translate(tx[, ty])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or two numbers).</param>
    /// <returns>The translation matrix.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or two.</exception>
    private static Matrix3x2 BuildTranslate(List<float> arguments) => arguments.Count switch
    {
        1 => Matrix3x2.CreateTranslation(arguments[0], 0f),
        2 => Matrix3x2.CreateTranslation(arguments[0], arguments[1]),
        _ => throw new InvalidDataException("Malformed translate transform function: expected one or two arguments.")
    };

    /// <summary>Builds a <c>scale(sx[, sy])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or two numbers).</param>
    /// <returns>The scale matrix.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or two.</exception>
    private static Matrix3x2 BuildScale(List<float> arguments) => arguments.Count switch
    {
        1 => Matrix3x2.CreateScale(arguments[0], arguments[0]),
        2 => Matrix3x2.CreateScale(arguments[0], arguments[1]),
        _ => throw new InvalidDataException("Malformed scale transform function: expected one or two arguments.")
    };

    /// <summary>Builds a <c>rotate(angle[, cx, cy])</c> function's matrix.</summary>
    /// <param name="arguments">The function's parsed arguments (one or three numbers, angle in degrees).</param>
    /// <returns>The rotation matrix, about the origin or about <c>(cx, cy)</c>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the argument count is not one or three.</exception>
    private static Matrix3x2 BuildRotate(List<float> arguments)
    {
        switch (arguments.Count)
        {
            case 1:
                return Matrix3x2.CreateRotation(DegreesToRadians(arguments[0]));
            case 3:
                var cx = arguments[1];
                var cy = arguments[2];
                return Matrix3x2.CreateTranslation(-cx, -cy)
                    * Matrix3x2.CreateRotation(DegreesToRadians(arguments[0]))
                    * Matrix3x2.CreateTranslation(cx, cy);
            default:
                throw new InvalidDataException("Malformed rotate transform function: expected one or three arguments.");
        }
    }

    /// <summary>Converts an angle in degrees to radians.</summary>
    /// <param name="degrees">The angle, in degrees.</param>
    /// <returns>The angle, in radians.</returns>
    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);

    // ================================================================================================
    // Shape geometry builders (each returns a Path in local, untransformed user-space coordinates)
    // ================================================================================================

    /// <summary>Builds a <c>rect</c> element's outline, including optional rounded corners.</summary>
    /// <param name="element">The <c>rect</c> element.</param>
    /// <param name="state">The cascaded render state, supplying the current viewport for percentage resolution.</param>
    /// <returns>
    ///     The local-space path, empty if the rectangle has no positive area, or if its corner-arc
    ///     construction overflows to a non-finite value (see this method's remarks).
    /// </returns>
    /// <remarks>
    ///     A rounded corner's arc-to-Bezier conversion (via <see cref="AppendArcTo"/>) can overflow
    ///     to a non-finite control point or endpoint for an extreme-but-individually-finite
    ///     combination of the rectangle's position/size and corner radii, even though every raw
    ///     literal parsed from the element's attributes is itself finite. Tolerantly skips (returns
    ///     an empty path) in that case, mirroring <see cref="PathDataParser.Parse"/>'s identical
    ///     "catch <see cref="OverflowException"/>, return an empty path" convention for the same
    ///     class of arithmetic-overflow risk in path <c>d</c> data.
    /// </remarks>
    private static Path BuildRectPath(XElement element, RenderState state)
    {
        var x = GetFloatAttribute(element, "x", state, PercentageAxis.Horizontal);
        var y = GetFloatAttribute(element, "y", state, PercentageAxis.Vertical);
        var width = GetFloatAttribute(element, "width", state, PercentageAxis.Horizontal);
        var height = GetFloatAttribute(element, "height", state, PercentageAxis.Vertical);
        var builder = new PathBuilder();
        if (width <= 0f || height <= 0f)
        {
            return builder.Build();
        }

        var (rx, ry) = ResolveRectRadii(
            GetFloatAttribute(element, "rx", state, PercentageAxis.Horizontal, float.NaN),
            GetFloatAttribute(element, "ry", state, PercentageAxis.Vertical, float.NaN),
            width,
            height);

        if (rx <= 0f || ry <= 0f)
        {
            AppendRectOutline(builder, x, y, width, height);
        }
        else
        {
            try
            {
                AppendRoundedRectOutline(builder, x, y, width, height, rx, ry);
            }
            catch (OverflowException)
            {
                // Tolerant skip: see this method's remarks
                return new PathBuilder().Build();
            }
        }

        return builder.Build();
    }

    /// <summary>Appends a sharp-cornered rectangle's outline to <paramref name="builder"/>.</summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's top edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    private static void AppendRectOutline(PathBuilder builder, float x, float y, float width, float height)
    {
        builder.MoveTo(new Vector2(x, y));
        builder.LineTo(new Vector2(x + width, y));
        builder.LineTo(new Vector2(x + width, y + height));
        builder.LineTo(new Vector2(x, y + height));
        builder.Close();
    }

    /// <summary>Appends a rounded-corner rectangle's outline to <paramref name="builder"/>.</summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's top edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <param name="rx">The corner radius along x, already clamped to at most half the width.</param>
    /// <param name="ry">The corner radius along y, already clamped to at most half the height.</param>
    private static void AppendRoundedRectOutline(
        PathBuilder builder,
        float x,
        float y,
        float width,
        float height,
        float rx,
        float ry)
    {
        var radius = new Vector2(rx, ry);
        var topRightStart = new Vector2(x + width - rx, y);
        var rightBottomStart = new Vector2(x + width, y + height - ry);
        var bottomLeftStart = new Vector2(x + rx, y + height);
        var leftTopStart = new Vector2(x, y + ry);

        builder.MoveTo(new Vector2(x + rx, y));
        builder.LineTo(topRightStart);
        AppendArcTo(builder, topRightStart, radius, new Vector2(x + width, y + ry));
        builder.LineTo(rightBottomStart);
        AppendArcTo(builder, rightBottomStart, radius, bottomLeftStart);
        builder.LineTo(bottomLeftStart);
        AppendArcTo(builder, bottomLeftStart, radius, leftTopStart);
        builder.LineTo(leftTopStart);
        AppendArcTo(builder, leftTopStart, radius, new Vector2(x + rx, y));
        builder.Close();
    }

    /// <summary>
    ///     Resolves a <c>rect</c> element's effective corner radii from its (possibly absent)
    ///     <c>rx</c>/<c>ry</c> attributes, per the SVG rule that either one alone implies an equal
    ///     value for the other, then clamps both to at most half the rectangle's corresponding
    ///     side length.
    /// </summary>
    /// <param name="rx">The parsed <c>rx</c> attribute, or <see cref="float.NaN"/> if absent.</param>
    /// <param name="ry">The parsed <c>ry</c> attribute, or <see cref="float.NaN"/> if absent.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <returns>The resolved, clamped <c>(rx, ry)</c> pair; both zero if neither was specified/valid.</returns>
    private static (float Rx, float Ry) ResolveRectRadii(float rx, float ry, float width, float height)
    {
        if (float.IsNaN(rx) && float.IsNaN(ry))
        {
            return (0f, 0f);
        }

        if (float.IsNaN(rx))
        {
            rx = ry;
        }
        else if (float.IsNaN(ry))
        {
            ry = rx;
        }

        if (rx < 0f || ry < 0f)
        {
            return (0f, 0f);
        }

        return (Math.Min(rx, width / 2f), Math.Min(ry, height / 2f));
    }

    /// <summary>Builds a <c>circle</c> or <c>ellipse</c> element's outline from four quarter-arcs.</summary>
    /// <param name="element">The <c>circle</c> or <c>ellipse</c> element.</param>
    /// <param name="isCircle">
    ///     <see langword="true"/> to read the single <c>r</c> radius attribute (<c>circle</c>);
    ///     <see langword="false"/> to read separate <c>rx</c>/<c>ry</c> attributes (<c>ellipse</c>).
    /// </param>
    /// <param name="state">The cascaded render state, supplying the current viewport for percentage resolution.</param>
    /// <returns>
    ///     The local-space path, empty if either radius is not positive, or if its quarter-arc
    ///     construction overflows to a non-finite value - see <see cref="BuildRectPath"/>'s
    ///     remarks for the identical tolerant-skip convention applied here.
    /// </returns>
    private static Path BuildEllipsePath(XElement element, bool isCircle, RenderState state)
    {
        var cx = GetFloatAttribute(element, "cx", state, PercentageAxis.Horizontal);
        var cy = GetFloatAttribute(element, "cy", state, PercentageAxis.Vertical);
        var radius = isCircle
            ? new Vector2(GetFloatAttribute(element, "r", state, PercentageAxis.Diagonal), GetFloatAttribute(element, "r", state, PercentageAxis.Diagonal))
            : new Vector2(GetFloatAttribute(element, "rx", state, PercentageAxis.Horizontal), GetFloatAttribute(element, "ry", state, PercentageAxis.Vertical));

        var builder = new PathBuilder();
        if (radius.X <= 0f || radius.Y <= 0f)
        {
            return builder.Build();
        }

        var right = new Vector2(cx + radius.X, cy);
        var bottom = new Vector2(cx, cy + radius.Y);
        var left = new Vector2(cx - radius.X, cy);
        var top = new Vector2(cx, cy - radius.Y);

        try
        {
            builder.MoveTo(right);
            AppendArcTo(builder, right, radius, bottom);
            AppendArcTo(builder, bottom, radius, left);
            AppendArcTo(builder, left, radius, top);
            AppendArcTo(builder, top, radius, right);
            builder.Close();
        }
        catch (OverflowException)
        {
            // Tolerant skip: see BuildRectPath's remarks
            return new PathBuilder().Build();
        }

        return builder.Build();
    }

    /// <summary>Builds a <c>line</c> element's two-point open path.</summary>
    /// <param name="element">The <c>line</c> element.</param>
    /// <param name="state">The cascaded render state, supplying the current viewport for percentage resolution.</param>
    /// <returns>The local-space path.</returns>
    private static Path BuildLinePath(XElement element, RenderState state)
    {
        var start = new Vector2(GetFloatAttribute(element, "x1", state, PercentageAxis.Horizontal), GetFloatAttribute(element, "y1", state, PercentageAxis.Vertical));
        var end = new Vector2(GetFloatAttribute(element, "x2", state, PercentageAxis.Horizontal), GetFloatAttribute(element, "y2", state, PercentageAxis.Vertical));

        var builder = new PathBuilder();
        builder.MoveTo(start);
        builder.LineTo(end);
        return builder.Build();
    }

    /// <summary>Builds a <c>polyline</c> or <c>polygon</c> element's path from its <c>points</c> attribute.</summary>
    /// <param name="element">The <c>polyline</c> or <c>polygon</c> element.</param>
    /// <param name="closed"><see langword="true"/> for <c>polygon</c>; <see langword="false"/> for <c>polyline</c>.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget, charged incrementally as each coordinate pair
    ///     is parsed (see <see cref="ParsePointList"/>).
    /// </param>
    /// <returns>The local-space path, empty if fewer than two points are present.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved point count pushes the combined geometry-parsing work total
    ///     past <see cref="GeometryWorkBudget"/>'s fixed budget.
    /// </exception>
    private static Path BuildPolyPath(XElement element, bool closed, GeometryWorkBudget workBudget)
    {
        var points = ParsePointList((string?)element.Attribute("points"), workBudget);
        var builder = new PathBuilder();
        if (points.Count < 2)
        {
            return builder.Build();
        }

        builder.MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            builder.LineTo(points[i]);
        }

        if (closed)
        {
            builder.Close();
        }

        return builder.Build();
    }

    /// <summary>Parses a <c>points</c> attribute's flat number list into coordinate pairs.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <param name="workBudget">
    ///     The shared geometry-parsing work budget, charged with <c>1</c> unit immediately after
    ///     each coordinate pair is parsed - before the next pair is read - using the same
    ///     <see cref="SkipSeparators"/>/<see cref="TryReadNumber"/> primitives
    ///     <see cref="ParseNumberList"/> itself uses, rather than delegating to
    ///     <see cref="ParseNumberList"/> and charging the whole resolved count in one batch at the
    ///     end. This mirrors <see cref="PathDataParser"/>'s own per-command incremental charging,
    ///     so a single pathologically large <c>points</c> string throws partway through parsing -
    ///     without ever materializing the full coordinate list - rather than only after its entire
    ///     (otherwise unbounded) content has already been scanned and allocated.
    /// </param>
    /// <returns>The parsed points, in document order. A trailing unpaired number is dropped.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a token is not a valid number (matching <see cref="ParseNumberList"/>'s own
    ///     "malformed token" message convention), or when the resolved point count pushes the
    ///     combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
    ///     budget.
    /// </exception>
    private static List<Vector2> ParsePointList(string? raw, GeometryWorkBudget workBudget)
    {
        var points = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return points;
        }

        var position = 0;
        while (true)
        {
            // Read the pair's first coordinate - reaching end-of-input here (rather than a
            // malformed token) means every preceding pair has already been fully parsed and
            // charged, so this is the normal, successful end of the list
            SkipSeparators(raw, ref position);
            if (position >= raw.Length)
            {
                break;
            }

            if (!TryReadNumber(raw, ref position, out var x))
            {
                throw new InvalidDataException($"Malformed number list: unexpected character at position {position}.");
            }

            // A lone trailing number with nothing left to pair it with is silently dropped, not
            // an error - this is the only case end-of-input is reached between a pair's two
            // coordinates rather than before the pair starts
            SkipSeparators(raw, ref position);
            if (position >= raw.Length)
            {
                break;
            }

            if (!TryReadNumber(raw, ref position, out var y))
            {
                throw new InvalidDataException($"Malformed number list: unexpected character at position {position}.");
            }

            // Charge this pair immediately, before continuing to scan the next one, so a hostile
            // huge points string is rejected as soon as the budget is exceeded rather than after
            // the whole string has already been scanned
            points.Add(new Vector2(x, y));
            workBudget.Charge(1);
        }

        return points;
    }

    /// <summary>
    ///     Appends one arc segment (converted to cubic Beziers via <see cref="Geometry.SvgArcConverter"/>)
    ///     to <paramref name="builder"/>, using the fixed <c>rotation=0, largeArc=false, sweep=true</c>
    ///     parameters every quarter-arc built by this codec's own shape builders (rounded-rect
    ///     corners, circles, ellipses) needs.
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="start">The arc's start point (must match the builder's current point).</param>
    /// <param name="radius">The arc's x/y radii.</param>
    /// <param name="end">The arc's end point.</param>
    private static void AppendArcTo(PathBuilder builder, Vector2 start, Vector2 radius, Vector2 end)
    {
        var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        SvgArcConverter.ToBeziers(start, radius, 0f, false, true, end, segments);
        foreach (var segment in segments)
        {
            builder.CubicBezierTo(
                RequireFiniteArcPoint(segment.Control1),
                RequireFiniteArcPoint(segment.Control2),
                RequireFiniteArcPoint(segment.End));
        }
    }

    /// <summary>
    ///     Validates that <paramref name="value"/>'s components are both finite, throwing
    ///     <see cref="OverflowException"/> otherwise - used by <see cref="AppendArcTo"/> to guard
    ///     <see cref="Geometry.SvgArcConverter"/>'s output against an extreme-but-individually-
    ///     finite radius/start/end combination whose internal rotation/trig arithmetic overflows
    ///     to a non-finite control point or endpoint. Mirrors
    ///     <see cref="PathDataParser.RequireFinite(Vector2)"/>'s identical tolerant-skip
    ///     convention - the same BCL <see cref="OverflowException"/> sentinel type, reused here
    ///     rather than a bespoke exception, so <see cref="BuildRectPath"/>/
    ///     <see cref="BuildEllipsePath"/>'s own narrowly-scoped catch clauses can identify it
    ///     without risking confusion with an actual arithmetic-overflow bug elsewhere.
    /// </summary>
    /// <param name="value">The point to validate.</param>
    /// <returns><paramref name="value"/> unchanged, when both components are finite.</returns>
    /// <exception cref="OverflowException">Thrown when either component of <paramref name="value"/> is not finite.</exception>
    private static Vector2 RequireFiniteArcPoint(Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new OverflowException("Arc-to-Bezier conversion overflowed to a non-finite value.");
        }

        return value;
    }
}
