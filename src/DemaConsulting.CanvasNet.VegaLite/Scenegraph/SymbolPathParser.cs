// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Parses the SVG path data of a custom symbol shape (a <c>shape</c> that is not a symbol name) and draws it scaled to a symbol size, the way
///     vega-scenegraph's <c>customSymbol</c> does.
/// </summary>
/// <remarks>
///     <para>
///         <b>Source.</b> <c>vega-scenegraph/src/path/parse.js</c> (the command pattern, the parameter counts, the number and flag patterns, the implicit
///         repetition and the implicit line-to after a move-to), <c>path/render.js</c> (the drawing of every command including the reflected control points of
///         <c>s</c>, <c>S</c>, <c>t</c> and <c>T</c>), <c>path/arc.js</c> (the arc to Bezier conversion), <c>path/symbols.js</c> (<c>customSymbol</c>: the parsed
///         path is drawn with the scale <c>sqrt(size) / 2</c> on both axes, so the coordinates of the path are in units of the radius) and
///         <c>bound/boundContext.js</c> (the bounds of the drawn path, with the extrema of the curves).
///     </para>
///     <para>
///         <b>Fidelity notes.</b> Text before the first command letter is skipped, as vega's regular expression skips it; a text that has no command draws
///         nothing. An unknown letter is part of the parameters of the command before it, so it is an error, as in vega. Vega keeps the control point of the
///         previous command whatever its type, so <c>s</c> after a line reflects the last control point (the SVG rule would use the current point); this is
///         reproduced. When the symbol is scaled, vega's scaled copy of the command is the very array that it later reads as the previous command, so a
///         <c>t</c> then always sees <c>t</c> as the previous command; this is reproduced. A <c>t</c> as the first command throws in vega and is invalid here.
///         The canvas rules for the context are applied: a drawing command without a current point starts the path at its own point and every operation that
///         has a non-finite number is skipped.
///     </para>
///     <para>
///         <b>Deviation.</b> Vega throws for a path that does not parse and the chart does not render; the renderer reports <c>vl-shape-path-invalid</c> and
///         draws a circle. A path longer than <see cref="MaxCommands"/> commands is invalid.
///     </para>
/// </remarks>
internal sealed class SymbolPathParser
{
    /// <summary>The largest number of commands (after the implicit repetition) that a path may have.</summary>
    public const int MaxCommands = 4096;

    /// <summary>The largest number of paths that are kept parsed.</summary>
    private const int MaxCachedPaths = 256;

    /// <summary>Vega's <c>Epsilon</c> (<c>util/constants.js</c>).</summary>
    private const double Epsilon = 1e-14;

    /// <summary>The parsed paths by text; null for a text that does not parse.</summary>
    private static readonly Dictionary<string, SymbolPathParser?> Cache = new(StringComparer.Ordinal);

    /// <summary>The commands: the letter and the parameters.</summary>
    private readonly List<(char Letter, double[] Params)> _commands;

    /// <summary>Guards the memo of the last drawing (the symbols of a chart mostly share one size).</summary>
    private readonly object _memoLock = new();

    /// <summary>The size of the memoized drawing.</summary>
    private double _memoSize;

    /// <summary>The memoized path; null before the first drawing.</summary>
    private Path? _memoPath;

    /// <summary>The bounds of the memoized path.</summary>
    private SceneBounds _memoBounds;

    /// <summary>
    ///     Initializes the parsed path.
    /// </summary>
    /// <param name="commands">The commands.</param>
    private SymbolPathParser(List<(char Letter, double[] Params)> commands)
    {
        _commands = commands;
    }

    /// <summary>Gets the number of commands, after the implicit repetition.</summary>
    public int CommandCount => _commands.Count;

    /// <summary>
    ///     Finds out whether a text is SVG path data that vega can draw (a text that is a symbol name is not asked).
    /// </summary>
    /// <param name="text">The shape text.</param>
    /// <returns><see langword="true"/> when the text parses.</returns>
    public static bool IsValid(string text) => TryGet(text) is not null;

    /// <summary>
    ///     Gets the parsed path of a shape text, from a small cache of parsed paths.
    /// </summary>
    /// <param name="text">The shape text.</param>
    /// <returns>The parsed path; <see langword="null"/> when the text does not parse.</returns>
    public static SymbolPathParser? TryGet(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (Cache)
        {
            if (Cache.TryGetValue(text, out var cached))
            {
                return cached;
            }
        }

        var parsed = TryParse(text, out _);
        lock (Cache)
        {
            if (Cache.Count >= MaxCachedPaths)
            {
                Cache.Clear();
            }

            Cache[text] = parsed;
        }

        return parsed;
    }

    /// <summary>
    ///     Parses SVG path data the way vega's <c>parse.js</c> does.
    /// </summary>
    /// <param name="text">The path data.</param>
    /// <param name="error">The reason when the text does not parse; otherwise <see langword="null"/>.</param>
    /// <returns>The parsed path; <see langword="null"/> when the text does not parse.</returns>
    public static SymbolPathParser? TryParse(string text, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        error = null;
        var commands = new List<(char Letter, double[] Params)>();
        var i = 0;
        while (i < text.Length)
        {
            // /[mlhvzcsqta]([^mlhvzcsqta]*)/gi: skip to the next command letter
            if (ParamCount(text[i]) < 0)
            {
                i++;
                continue;
            }

            var letter = text[i];
            var end = i + 1;
            while (end < text.Length && ParamCount(text[end]) < 0)
            {
                end++;
            }

            var type = char.ToLowerInvariant(letter);
            var paramCount = ParamCount(type);
            var segment = TrimJs(text.AsSpan(i + 1, end - i - 1));
            var values = ParseParams(type, paramCount, segment, out error);
            if (values is null)
            {
                return null;
            }

            if (values.Count < paramCount || (values.Count > 0 && values.Count % paramCount != 0))
            {
                error = "Invalid SVG path, incorrect parameter count";
                return null;
            }

            commands.Add((letter, values.GetRange(0, paramCount).ToArray()));
            if (values.Count > paramCount)
            {
                // an extended parameter list repeats the command; a move-to repeats as a line-to
                var repeat = letter;
                if (type == 'm')
                {
                    repeat = letter == 'M' ? 'L' : 'l';
                }

                for (var p = paramCount; p < values.Count; p += paramCount)
                {
                    commands.Add((repeat, values.GetRange(p, paramCount).ToArray()));
                }
            }

            if (commands.Count > MaxCommands)
            {
                error = $"The path has more than {MaxCommands} commands.";
                return null;
            }

            i = end;
        }

        if (commands.Count > 0 && commands[0].Letter == 't')
        {
            // vega reads the previous command of the first one, which is null: a TypeError
            error = "Invalid SVG path, a relative smooth quadratic curve cannot be the first command";
            return null;
        }

        return new SymbolPathParser(commands);
    }

    /// <summary>
    ///     Draws the path scaled to a symbol size, the way <c>customSymbol</c> does.
    /// </summary>
    /// <param name="size">The symbol area in square pixels; positive.</param>
    /// <param name="bounds">The bounds of the drawn path in the coordinates of the symbol's center (empty when the path draws no point).</param>
    /// <returns>The path.</returns>
    public Path Render(double size, out SceneBounds bounds)
    {
        lock (_memoLock)
        {
            if (_memoPath is not null && _memoSize.CompareTo(size) == 0)
            {
                bounds = _memoBounds;
                return _memoPath;
            }
        }

        var path = Draw(size, out bounds);
        lock (_memoLock)
        {
            _memoSize = size;
            _memoPath = path;
            _memoBounds = bounds;
        }

        return path;
    }

    /// <summary>
    ///     Draws the path for a size.
    /// </summary>
    /// <param name="size">The symbol area in square pixels.</param>
    /// <param name="bounds">The bounds of the drawn path.</param>
    /// <returns>The path.</returns>
    private Path Draw(double size, out SceneBounds bounds)
    {
        var scale = Math.Sqrt(size) / 2;
        var canvas = new PathCanvas();
        var scaled = scale.CompareTo(1d) != 0;
        double x = 0, y = 0, controlX = 0, controlY = 0;
        double tempControlX = double.NaN, tempControlY = double.NaN;
        double anchorX = 0, anchorY = 0;
        var previous = '\0';
        foreach (var (letter, source) in _commands)
        {
            var c = scaled ? Scale(letter, source, scale) : source;
            double tx, ty;
            switch (letter)
            {
                case 'l':
                    x += c[0];
                    y += c[1];
                    canvas.LineTo(x, y);
                    break;
                case 'L':
                    x = c[0];
                    y = c[1];
                    canvas.LineTo(x, y);
                    break;
                case 'h':
                    x += c[0];
                    canvas.LineTo(x, y);
                    break;
                case 'H':
                    x = c[0];
                    canvas.LineTo(x, y);
                    break;
                case 'v':
                    y += c[0];
                    canvas.LineTo(x, y);
                    break;
                case 'V':
                    y = c[0];
                    canvas.LineTo(x, y);
                    break;
                case 'm':
                    x += c[0];
                    y += c[1];
                    anchorX = x;
                    anchorY = y;
                    canvas.MoveTo(x, y);
                    break;
                case 'M':
                    x = c[0];
                    y = c[1];
                    anchorX = x;
                    anchorY = y;
                    canvas.MoveTo(x, y);
                    break;
                case 'c':
                    tx = x + c[4];
                    ty = y + c[5];
                    controlX = x + c[2];
                    controlY = y + c[3];
                    canvas.CubicTo(x + c[0], y + c[1], controlX, controlY, tx, ty);
                    x = tx;
                    y = ty;
                    break;
                case 'C':
                    x = c[4];
                    y = c[5];
                    controlX = c[2];
                    controlY = c[3];
                    canvas.CubicTo(c[0], c[1], controlX, controlY, x, y);
                    break;
                case 's':
                    tx = x + c[2];
                    ty = y + c[3];
                    controlX = (2 * x) - controlX;
                    controlY = (2 * y) - controlY;
                    canvas.CubicTo(controlX, controlY, x + c[0], y + c[1], tx, ty);
                    controlX = x + c[0];
                    controlY = y + c[1];
                    x = tx;
                    y = ty;
                    break;
                case 'S':
                    tx = c[2];
                    ty = c[3];
                    controlX = (2 * x) - controlX;
                    controlY = (2 * y) - controlY;
                    canvas.CubicTo(controlX, controlY, c[0], c[1], tx, ty);
                    x = tx;
                    y = ty;
                    controlX = c[0];
                    controlY = c[1];
                    break;
                case 'q':
                    tx = x + c[2];
                    ty = y + c[3];
                    controlX = x + c[0];
                    controlY = y + c[1];
                    canvas.QuadraticTo(controlX, controlY, tx, ty);
                    x = tx;
                    y = ty;
                    break;
                case 'Q':
                    tx = c[2];
                    ty = c[3];
                    canvas.QuadraticTo(c[0], c[1], tx, ty);
                    x = tx;
                    y = ty;
                    controlX = c[0];
                    controlY = c[1];
                    break;
                case 't':
                    tx = x + c[0];
                    ty = y + c[1];

                    // The scaled copy of the command is the array that vega later reads as the previous command
                    var before = scaled ? 't' : previous;
                    if (before is not ('Q' or 'q' or 'T' or 't'))
                    {
                        controlX = x;
                        controlY = y;
                    }
                    else if (before == 't')
                    {
                        controlX = (2 * x) - tempControlX;
                        controlY = (2 * y) - tempControlY;
                    }
                    else if (before == 'q')
                    {
                        controlX = (2 * x) - controlX;
                        controlY = (2 * y) - controlY;
                    }

                    tempControlX = controlX;
                    tempControlY = controlY;
                    canvas.QuadraticTo(controlX, controlY, tx, ty);
                    x = tx;
                    y = ty;
                    controlX = x + c[0];
                    controlY = y + c[1];
                    break;
                case 'T':
                    tx = c[0];
                    ty = c[1];
                    controlX = (2 * x) - controlX;
                    controlY = (2 * y) - controlY;
                    canvas.QuadraticTo(controlX, controlY, tx, ty);
                    x = tx;
                    y = ty;
                    break;
                case 'a':
                    DrawArc(canvas, x, y, c[0], c[1], c[2], c[3], c[4], c[5] + x, c[6] + y);
                    x += c[5];
                    y += c[6];
                    break;
                case 'A':
                    DrawArc(canvas, x, y, c[0], c[1], c[2], c[3], c[4], c[5], c[6]);
                    x = c[5];
                    y = c[6];
                    break;
                default: // z, Z
                    x = anchorX;
                    y = anchorY;
                    canvas.ClosePath();
                    break;
            }

            previous = letter;
        }

        return canvas.Finish(out bounds);
    }

    /// <summary>
    ///     Gets the number of parameters of a command letter (either case).
    /// </summary>
    /// <param name="letter">The character.</param>
    /// <returns>The count; -1 when the character is not a command letter.</returns>
    private static int ParamCount(char letter) => char.ToLowerInvariant(letter) switch
    {
        'm' or 'l' or 't' => 2,
        'h' or 'v' => 1,
        'z' => 0,
        'c' => 6,
        's' or 'q' => 4,
        'a' => 7,
        _ => -1,
    };

    /// <summary>
    ///     Finds out whether a character is JavaScript white space (<c>\s</c> and the set that <c>String.prototype.trim</c> removes).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for white space.</returns>
    private static bool IsJsSpace(char c) => c is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00a0' or '\u1680' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff'
        || (c >= '\u2000' && c <= '\u200a');

    /// <summary>
    ///     Removes the JavaScript white space from both ends.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static ReadOnlySpan<char> TrimJs(ReadOnlySpan<char> text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsJsSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsJsSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    /// <summary>
    ///     Reads the numbers of one command (<c>parseParams</c>).
    /// </summary>
    /// <param name="type">The lower-case command letter.</param>
    /// <param name="paramCount">The parameters of one repetition.</param>
    /// <param name="segment">The text after the letter, trimmed.</param>
    /// <param name="error">The reason for a failure.</param>
    /// <returns>The numbers; <see langword="null"/> when a parameter is missing or of the wrong kind.</returns>
    private static List<double>? ParseParams(char type, int paramCount, ReadOnlySpan<char> segment, out string? error)
    {
        error = null;
        var values = new List<double>();
        var index = 0;
        while (paramCount > 0 && index < segment.Length)
        {
            for (var i = 0; i < paramCount; i++)
            {
                var rest = segment[index..];
                var length = type == 'a' && (i == 3 || i == 4) ? FlagLength(rest) : NumberLength(rest);
                if (length == 0)
                {
                    error = "Invalid SVG path, incorrect parameter type";
                    return null;
                }

                values.Add(double.Parse(rest[..length], NumberStyles.Float, CultureInfo.InvariantCulture));
                index += length;
                index += SpaceLength(segment[index..]);
                if (values.Count > MaxCommands * 7)
                {
                    error = $"The path has more than {MaxCommands} commands.";
                    return null;
                }
            }
        }

        return values;
    }

    /// <summary>
    ///     Matches <c>^[01]</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>1 for a flag; 0 otherwise.</returns>
    private static int FlagLength(ReadOnlySpan<char> text) => text.Length > 0 && (text[0] == '0' || text[0] == '1') ? 1 : 0;

    /// <summary>
    ///     Matches <c>^((\s+,?\s*)|(,\s*))</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The length of the separator; 0 for none.</returns>
    private static int SpaceLength(ReadOnlySpan<char> text)
    {
        var i = 0;
        while (i < text.Length && IsJsSpace(text[i]))
        {
            i++;
        }

        if (i > 0)
        {
            if (i < text.Length && text[i] == ',')
            {
                i++;
                while (i < text.Length && IsJsSpace(text[i]))
                {
                    i++;
                }
            }

            return i;
        }

        if (text.Length > 0 && text[0] == ',')
        {
            i = 1;
            while (i < text.Length && IsJsSpace(text[i]))
            {
                i++;
            }
        }

        return i;
    }

    /// <summary>
    ///     Matches <c>^[+-]?(([0-9]*\.[0-9]+)|([0-9]+\.)|([0-9]+))([eE][+-]?[0-9]+)?</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The length of the number; 0 for none.</returns>
    private static int NumberLength(ReadOnlySpan<char> text)
    {
        var i = 0;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            i++;
        }

        var digitsStart = i;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            i++;
        }

        var integerDigits = i - digitsStart;
        if (i < text.Length && text[i] == '.')
        {
            var afterDot = i + 1;
            var j = afterDot;
            while (j < text.Length && text[j] is >= '0' and <= '9')
            {
                j++;
            }

            if (j > afterDot)
            {
                i = j; // [0-9]*\.[0-9]+
            }
            else if (integerDigits > 0)
            {
                i = afterDot; // [0-9]+\.
            }
            else
            {
                return 0;
            }
        }
        else if (integerDigits == 0)
        {
            return 0;
        }

        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            var j = i + 1;
            if (j < text.Length && (text[j] == '+' || text[j] == '-'))
            {
                j++;
            }

            var exponentStart = j;
            while (j < text.Length && text[j] is >= '0' and <= '9')
            {
                j++;
            }

            if (j > exponentStart)
            {
                i = j;
            }
        }

        return i;
    }

    /// <summary>
    ///     Scales the parameters of a command the way <c>render.js</c> does: the radii and the end point of an arc, the one number of <c>h</c> and <c>v</c>,
    ///     and every other number by x then y alternately.
    /// </summary>
    /// <param name="letter">The command letter.</param>
    /// <param name="source">The parameters.</param>
    /// <param name="scale">The scale of both axes.</param>
    /// <returns>The scaled copy.</returns>
    private static double[] Scale(char letter, double[] source, double scale)
    {
        var scaled = (double[])source.Clone();
        if (letter is 'a' or 'A')
        {
            scaled[0] *= scale;
            scaled[1] *= scale;
            scaled[5] *= scale;
            scaled[6] *= scale;
        }
        else
        {
            for (var i = 0; i < scaled.Length; i++)
            {
                scaled[i] *= scale;
            }
        }

        return scaled;
    }

    /// <summary>
    ///     Draws an elliptical arc as Bezier curves (<c>drawArc</c>, <c>segments</c> and <c>bezier</c> of <c>arc.js</c>).
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="ox">The x of the start.</param>
    /// <param name="oy">The y of the start.</param>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <param name="rotation">The rotation in degrees.</param>
    /// <param name="large">The large-arc flag.</param>
    /// <param name="sweep">The sweep flag.</param>
    /// <param name="x">The x of the end.</param>
    /// <param name="y">The y of the end.</param>
    private static void DrawArc(PathCanvas canvas, double ox, double oy, double rx, double ry, double rotation, double large, double sweep, double x, double y)
    {
        var th = rotation * (Math.PI / 180);
        var sinTh = Math.Sin(th);
        var cosTh = Math.Cos(th);
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        var px = (cosTh * (ox - x) * 0.5) + (sinTh * (oy - y) * 0.5);
        var py = (cosTh * (oy - y) * 0.5) - (sinTh * (ox - x) * 0.5);
        var pl = (px * px / (rx * rx)) + (py * py / (ry * ry));
        if (pl > 1)
        {
            pl = Math.Sqrt(pl);
            rx *= pl;
            ry *= pl;
        }

        var a00 = cosTh / rx;
        var a01 = sinTh / rx;
        var a10 = -sinTh / ry;
        var a11 = cosTh / ry;
        var x0 = (a00 * ox) + (a01 * oy);
        var y0 = (a10 * ox) + (a11 * oy);
        var x1 = (a00 * x) + (a01 * y);
        var y1 = (a10 * x) + (a11 * y);

        var d = ((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0));
        var sfactorSq = (1 / d) - 0.25;
        if (sfactorSq < 0)
        {
            sfactorSq = 0;
        }

        var sfactor = Math.Sqrt(sfactorSq);
        if (large.CompareTo(sweep) == 0)
        {
            sfactor = -sfactor;
        }

        var xc = (0.5 * (x0 + x1)) - (sfactor * (y1 - y0));
        var yc = (0.5 * (y0 + y1)) + (sfactor * (x1 - x0));
        var th0 = Math.Atan2(y0 - yc, x0 - xc);
        var th1 = Math.Atan2(y1 - yc, x1 - xc);
        var thArc = th1 - th0;
        if (thArc < 0 && sweep > 0.5)
        {
            thArc += Math.Tau;
        }
        else if (thArc > 0 && sweep < 0.5)
        {
            thArc -= Math.Tau;
        }

        var segments = Math.Ceiling(Math.Abs(thArc / ((Math.PI / 2) + 0.001)));
        for (var i = 0; i < segments; i++)
        {
            var th2 = th0 + (i * thArc / segments);
            var th3 = th0 + ((i + 1) * thArc / segments);
            var b = ArcBezier(xc, yc, th2, th3, rx, ry, sinTh, cosTh);
            canvas.CubicTo(b[0], b[1], b[2], b[3], b[4], b[5]);
        }
    }

    /// <summary>
    ///     Converts one arc segment to a cubic Bezier curve (<c>bezier</c> of <c>arc.js</c>).
    /// </summary>
    /// <returns>The two control points and the end point.</returns>
    private static double[] ArcBezier(double cx, double cy, double th0, double th1, double rx, double ry, double sinTh, double cosTh)
    {
        var a00 = cosTh * rx;
        var a01 = -sinTh * ry;
        var a10 = sinTh * rx;
        var a11 = cosTh * ry;
        var cosTh0 = Math.Cos(th0);
        var sinTh0 = Math.Sin(th0);
        var cosTh1 = Math.Cos(th1);
        var sinTh1 = Math.Sin(th1);
        var thHalf = 0.5 * (th1 - th0);
        var sinThH2 = Math.Sin(thHalf * 0.5);
        var t = 8.0 / 3 * sinThH2 * sinThH2 / Math.Sin(thHalf);
        var x1 = cx + cosTh0 - (t * sinTh0);
        var y1 = cy + sinTh0 + (t * cosTh0);
        var x3 = cx + cosTh1;
        var y3 = cy + sinTh1;
        var x2 = x3 + (t * sinTh1);
        var y2 = y3 - (t * cosTh1);
        return
        [
            (a00 * x1) + (a01 * y1), (a10 * x1) + (a11 * y1),
            (a00 * x2) + (a01 * y2), (a10 * x2) + (a11 * y2),
            (a00 * x3) + (a01 * y3), (a10 * x3) + (a11 * y3),
        ];
    }

    /// <summary>
    ///     The drawing context of vega's <c>render.js</c>: an HTML canvas path (a command without a current point starts a sub-path, a non-finite number
    ///     skips the command, a command after <c>closePath</c> starts from the start of the closed sub-path) that also keeps vega's bounds.
    /// </summary>
    private sealed class PathCanvas
    {
        /// <summary>The builder of the path.</summary>
        private readonly PathBuilder _builder = new();

        /// <summary>The smallest x of the bounds.</summary>
        private double _x1 = double.PositiveInfinity;

        /// <summary>The smallest y of the bounds.</summary>
        private double _y1 = double.PositiveInfinity;

        /// <summary>The largest x of the bounds.</summary>
        private double _x2 = double.NegativeInfinity;

        /// <summary>The largest y of the bounds.</summary>
        private double _y2 = double.NegativeInfinity;

        /// <summary>The last point of the bounds context (<c>lx</c>, <c>ly</c> of <c>boundContext.js</c>).</summary>
        private double _lastX, _lastY;

        /// <summary>Whether a sub-path is open.</summary>
        private bool _open;

        /// <summary>Whether the open sub-path was closed (the next command starts at <see cref="_start"/>).</summary>
        private bool _closed;

        /// <summary>The start of the current sub-path.</summary>
        private Vector2 _start;

        /// <summary>
        ///     Starts a sub-path.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void MoveTo(double x, double y)
        {
            _lastX = x;
            _lastY = y;
            AddBounds(x, y);
            if (!IsFinite(x, y))
            {
                return;
            }

            _start = new Vector2((float)x, (float)y);
            _builder.MoveTo(_start);
            _open = true;
            _closed = false;
        }

        /// <summary>
        ///     Draws a line.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void LineTo(double x, double y)
        {
            _lastX = x;
            _lastY = y;
            AddBounds(x, y);
            if (!IsFinite(x, y))
            {
                return;
            }

            if (!_open)
            {
                _start = new Vector2((float)x, (float)y);
                _builder.MoveTo(_start);
                _open = true;
                return;
            }

            Resume();
            _builder.LineTo(new Vector2((float)x, (float)y));
        }

        /// <summary>
        ///     Draws a quadratic curve.
        /// </summary>
        /// <param name="x1">The control x.</param>
        /// <param name="y1">The control y.</param>
        /// <param name="x2">The end x.</param>
        /// <param name="y2">The end y.</param>
        public void QuadraticTo(double x1, double y1, double x2, double y2)
        {
            AddQuadraticBounds(x1, y1, x2, y2);
            if (!IsFinite(x1, y1, x2, y2))
            {
                return;
            }

            EnsureSubpath(x1, y1);
            _builder.QuadraticBezierTo(new Vector2((float)x1, (float)y1), new Vector2((float)x2, (float)y2));
        }

        /// <summary>
        ///     Draws a cubic curve.
        /// </summary>
        /// <param name="x1">The first control x.</param>
        /// <param name="y1">The first control y.</param>
        /// <param name="x2">The second control x.</param>
        /// <param name="y2">The second control y.</param>
        /// <param name="x3">The end x.</param>
        /// <param name="y3">The end y.</param>
        public void CubicTo(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            AddCubicBounds(x1, y1, x2, y2, x3, y3);
            if (!IsFinite(x1, y1, x2, y2, x3, y3))
            {
                return;
            }

            EnsureSubpath(x1, y1);
            _builder.CubicBezierTo(new Vector2((float)x1, (float)y1), new Vector2((float)x2, (float)y2), new Vector2((float)x3, (float)y3));
        }

        /// <summary>
        ///     Closes the current sub-path.
        /// </summary>
        public void ClosePath()
        {
            if (_open && !_closed)
            {
                _builder.Close();
                _closed = true;
            }
        }

        /// <summary>
        ///     Gets the path and the bounds.
        /// </summary>
        /// <param name="bounds">The bounds; empty when no point was added.</param>
        /// <returns>The path.</returns>
        public Path Finish(out SceneBounds bounds)
        {
            bounds = _x1 > _x2 || _y1 > _y2 ? SceneBounds.Empty : new SceneBounds(_x1, _y1, _x2, _y2);
            return _builder.Build();
        }

        /// <summary>
        ///     Tests whether every number is finite.
        /// </summary>
        /// <param name="values">The numbers.</param>
        /// <returns><see langword="true"/> when none is NaN or infinite.</returns>
        private static bool IsFinite(params double[] values) => values.All(double.IsFinite);

        /// <summary>
        ///     Starts a new sub-path at the start of the closed one, when the last command closed the sub-path.
        /// </summary>
        private void Resume()
        {
            if (_closed)
            {
                _builder.MoveTo(_start);
                _closed = false;
            }
        }

        /// <summary>
        ///     Makes sure there is a current point: a curve without one starts at its first control point.
        /// </summary>
        /// <param name="x">The x of the first control point.</param>
        /// <param name="y">The y of the first control point.</param>
        private void EnsureSubpath(double x, double y)
        {
            if (!_open)
            {
                _start = new Vector2((float)x, (float)y);
                _builder.MoveTo(_start);
                _open = true;
                return;
            }

            Resume();
        }

        /// <summary>
        ///     Adds a point to the bounds.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        private void AddBounds(double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y))
            {
                return;
            }

            _x1 = Math.Min(_x1, x);
            _y1 = Math.Min(_y1, y);
            _x2 = Math.Max(_x2, x);
            _y2 = Math.Max(_y2, y);
        }

        /// <summary>
        ///     Adds the bounds of a quadratic curve: its end point and its extrema (<c>quadraticCurveTo</c> of <c>boundContext.js</c>).
        /// </summary>
        private void AddQuadraticBounds(double x1, double y1, double x2, double y2)
        {
            QuadraticExtrema(_lastX, x1, x2, v => AddBounds(v, _y1));
            QuadraticExtrema(_lastY, y1, y2, v => AddBounds(_x1, v));
            _lastX = x2;
            _lastY = y2;
            AddBounds(x2, y2);
        }

        /// <summary>
        ///     Adds the bounds of a cubic curve: its end point and its extrema (<c>bezierCurveTo</c> of <c>boundContext.js</c>).
        /// </summary>
        private void AddCubicBounds(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            CubicExtrema(_lastX, x1, x2, x3, v => AddBounds(v, _y1));
            CubicExtrema(_lastY, y1, y2, y3, v => AddBounds(_x1, v));
            _lastX = x3;
            _lastY = y3;
            AddBounds(x3, y3);
        }

        /// <summary>
        ///     Finds the extremum of one coordinate of a quadratic curve.
        /// </summary>
        private static void QuadraticExtrema(double x0, double x1, double x2, Action<double> add)
        {
            var t = (x0 - x1) / (x0 + x2 - (2 * x1));
            if (t > 0 && t < 1)
            {
                add(x0 + ((x1 - x0) * t));
            }
        }

        /// <summary>
        ///     Finds the extrema of one coordinate of a cubic curve.
        /// </summary>
        private static void CubicExtrema(double x0, double x1, double x2, double x3, Action<double> add)
        {
            var a = x3 - x0 + (3 * x1) - (3 * x2);
            var b = x0 + x2 - (2 * x1);
            var c = x0 - x1;
            double t0 = 0, t1 = 0;
            if (Math.Abs(a) > Epsilon)
            {
                var r = (b * b) + (c * a);
                if (r >= 0)
                {
                    r = Math.Sqrt(r);
                    t0 = (-b + r) / a;
                    t1 = (-b - r) / a;
                }
            }
            else
            {
                t0 = 0.5 * c / b;
            }

            if (t0 > 0 && t0 < 1)
            {
                add(Cubic(t0, x0, x1, x2, x3));
            }

            if (t1 > 0 && t1 < 1)
            {
                add(Cubic(t1, x0, x1, x2, x3));
            }
        }

        /// <summary>
        ///     Evaluates a cubic Bezier coordinate.
        /// </summary>
        private static double Cubic(double t, double x0, double x1, double x2, double x3)
        {
            var s = 1 - t;
            var s2 = s * s;
            var t2 = t * t;
            return (s2 * s * x0) + (3 * s2 * t * x1) + (3 * s * t2 * x2) + (t2 * t * x3);
        }
    }
}
