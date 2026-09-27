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

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // Path data ("d" attribute) mini-language parsing
    // ================================================================================================

    /// <summary>Builds a <c>path</c> element's outline from its <c>d</c> attribute.</summary>
    /// <param name="element">The <c>path</c> element.</param>
    /// <param name="workBudget">The shared geometry-parsing work budget, charged once per parsed command.</param>
    /// <returns>The local-space path, empty if <c>d</c> is absent or blank.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>d</c> is present but not valid path data, or parsing its commands pushes
    ///     the combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
    ///     budget.
    /// </exception>
    private static Path BuildPathDataPath(XElement element, GeometryWorkBudget workBudget)
    {
        var d = (string?)element.Attribute("d");
        return string.IsNullOrWhiteSpace(d) ? new PathBuilder().Build() : new PathDataParser(d, workBudget).Parse();
    }

    /// <summary>
    ///     A single-use, stateful parser for one SVG <c>path</c> element's <c>d</c> attribute
    ///     mini-language (<c>M/m L/l H/h V/v C/c S/s Q/q T/t A/a Z/z</c>, both absolute and
    ///     relative, including every command's "implicit repeat" shorthand for additional
    ///     argument groups following the same command letter).
    /// </summary>
    /// <remarks>
    ///     Kept as its own nested class, rather than a set of methods threading many <c>ref</c>
    ///     parameters through <see cref="SvgCodec"/> directly, so that each SVG command becomes a
    ///     small, single-responsibility instance method operating on private fields - the same
    ///     rationale documented for <see cref="RenderContext"/> above.
    /// </remarks>
    private sealed class PathDataParser
    {
        /// <summary>The full <c>d</c> attribute text being parsed.</summary>
        private readonly string _text;

        /// <summary>The builder accumulating the parsed path's commands.</summary>
        private readonly PathBuilder _builder = new();

        /// <summary>
        ///     The shared geometry-parsing work budget, charged once per emitted path command so a
        ///     single pathological <c>d</c> string throws partway through parsing rather than
        ///     after its entire (unbounded) content has already been scanned. Stored as an
        ///     ordinary field (rather than a <c>ref int</c>, the convention used elsewhere in this
        ///     class) because a <c>ref</c> parameter cannot be assigned into an instance field of
        ///     an ordinary class - see <see cref="GeometryWorkBudget"/>'s own remarks.
        /// </summary>
        private readonly GeometryWorkBudget _workBudget;

        /// <summary>The current scan position within <see cref="_text"/>.</summary>
        private int _position;

        /// <summary>The current point (the end point of the most recently issued command).</summary>
        private Vector2 _current;

        /// <summary>The point the current subpath began at, restored by a <c>Z</c>/<c>z</c> command.</summary>
        private Vector2 _subpathStart;

        /// <summary>
        ///     The most recent cubic Bezier's second control point, used to compute the implicit
        ///     reflected control point for a following <c>S</c>/<c>s</c> command; <see langword="null"/>
        ///     if the previous command was not a cubic Bezier (in which case <c>S</c>/<c>s</c>
        ///     reflects about the current point itself, per spec).
        /// </summary>
        private Vector2? _lastCubicControl;

        /// <summary>
        ///     The most recent quadratic Bezier's control point, used analogously to
        ///     <see cref="_lastCubicControl"/> for a following <c>T</c>/<c>t</c> command.
        /// </summary>
        private Vector2? _lastQuadControl;

        /// <summary><see langword="true"/> once the first command has been read.</summary>
        private bool _started;

        /// <summary>Initializes a new parser over <paramref name="text"/>.</summary>
        /// <param name="text">The <c>d</c> attribute's full raw text.</param>
        /// <param name="workBudget">The shared geometry-parsing work budget to charge as commands are parsed.</param>
        public PathDataParser(string text, GeometryWorkBudget workBudget)
        {
            _text = text;
            _workBudget = workBudget;
        }

        /// <summary>Parses the whole <c>d</c> attribute and builds its path.</summary>
        /// <returns>
        ///     The resulting local-space path; an empty path (see <see cref="PathBuilder.Build"/>)
        ///     if relative-coordinate accumulation or a smooth-curve reflection (see
        ///     <see cref="RequireFinite(Vector2)"/>/<see cref="RequireFinite(float)"/>) overflows
        ///     an individually-finite pair of literals to a non-finite result partway through
        ///     parsing - a tolerant "skip this element" outcome, matching this class's existing
        ///     tolerant handling of a composed non-finite transform elsewhere in this codec,
        ///     because a non-finite point cannot be rendered meaningfully and, left unchecked,
        ///     would stall <see cref="Drawing.DashSplitter"/>'s dash-interval walk (its existing
        ///     "huge-but-finite" double-widening fix does not cover a genuinely
        ///     <c>Infinity</c>-valued coordinate).
        /// </returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the text is not valid path data, or parsing its commands pushes the
        ///     combined geometry-parsing work total past <see cref="GeometryWorkBudget"/>'s fixed
        ///     budget.
        /// </exception>
        public Path Parse()
        {
            try
            {
                while (true)
                {
                    SkipSeparators(_text, ref _position);
                    if (_position >= _text.Length)
                    {
                        break;
                    }

                    var command = _text[_position];
                    if (!IsCommandLetter(command))
                    {
                        throw new InvalidDataException($"Malformed path data: expected a command letter at position {_position}.");
                    }

                    if (!_started && char.ToUpperInvariant(command) != 'M')
                    {
                        throw new InvalidDataException("Malformed path data: the first command must be a moveto (M/m).");
                    }

                    _position++;
                    ExecuteCommand(command);
                    _started = true;
                }

                return _builder.Build();
            }
            catch (OverflowException)
            {
                // Tolerant skip: RequireFinite raises this private-to-this-parser sentinel (the
                // BCL's own OverflowException, reused rather than a bespoke exception type, so no
                // catch clause elsewhere in this codec's Load/GetInfo boundary can mistake it for
                // an actual arithmetic-overflow bug) when a non-finite accumulated/reflected point
                // is produced - see this method's own remarks above for why an empty path is
                // returned rather than the exception propagating further
                return new PathBuilder().Build();
            }
        }

        /// <summary>Determines whether <paramref name="ch"/> is one of the recognized path command letters.</summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is a recognized command letter.</returns>
        private static bool IsCommandLetter(char ch) => "MmLlHhVvCcSsQqTtAaZz".Contains(ch);

        /// <summary>Dispatches one command letter (and its full run of implicitly repeated argument groups).</summary>
        /// <param name="command">The command letter just consumed.</param>
        /// <exception cref="InvalidDataException">Thrown when <paramref name="command"/> is not recognized.</exception>
        private void ExecuteCommand(char command)
        {
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    ExecuteMoveTo(command);
                    break;
                case 'L':
                    ExecuteLineTo(command);
                    break;
                case 'H':
                    ExecuteHorizontal(command);
                    break;
                case 'V':
                    ExecuteVertical(command);
                    break;
                case 'C':
                    ExecuteCubic(command);
                    break;
                case 'S':
                    ExecuteSmoothCubic(command);
                    break;
                case 'Q':
                    ExecuteQuadratic(command);
                    break;
                case 'T':
                    ExecuteSmoothQuadratic(command);
                    break;
                case 'A':
                    ExecuteArc(command);
                    break;
                case 'Z':
                    ExecuteClose();
                    break;
                default:
                    throw new InvalidDataException($"Unrecognized path command '{command}'.");
            }
        }

        /// <summary>Executes an <c>M</c>/<c>m</c> command and any implicitly repeated <c>L</c>/<c>l</c>-equivalent groups.</summary>
        /// <param name="command">The literal command letter (<c>M</c> or <c>m</c>).</param>
        private void ExecuteMoveTo(char command)
        {
            var isRelative = char.IsLower(command);
            _current = ReadPoint(isRelative, _current);
            _subpathStart = _current;
            _builder.MoveTo(_current);
            ClearReflectionState();
            _workBudget.Charge(1);

            while (TryPeekNumber())
            {
                _current = ReadPoint(isRelative, _current);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            }
        }

        /// <summary>Executes an <c>L</c>/<c>l</c> command and any implicitly repeated argument groups.</summary>
        /// <param name="command">The literal command letter (<c>L</c> or <c>l</c>).</param>
        private void ExecuteLineTo(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                _current = ReadPoint(isRelative, _current);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>H</c>/<c>h</c> (horizontal line) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>H</c> or <c>h</c>).</param>
        private void ExecuteHorizontal(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var x = ReadNumber();
                _current = new Vector2(isRelative ? RequireFinite(_current.X + x) : x, _current.Y);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>V</c>/<c>v</c> (vertical line) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>V</c> or <c>v</c>).</param>
        private void ExecuteVertical(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var y = ReadNumber();
                _current = new Vector2(_current.X, isRelative ? RequireFinite(_current.Y + y) : y);
                _builder.LineTo(_current);
                ClearReflectionState();
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>C</c>/<c>c</c> (cubic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>C</c> or <c>c</c>).</param>
        private void ExecuteCubic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control1 = ReadPoint(isRelative, _current);
                var control2 = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                _builder.CubicBezierTo(control1, control2, end);
                _current = end;
                _lastCubicControl = control2;
                _lastQuadControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>S</c>/<c>s</c> (smooth cubic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>S</c> or <c>s</c>).</param>
        private void ExecuteSmoothCubic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control2 = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                var control1 = _lastCubicControl.HasValue ? Reflect(_lastCubicControl.Value, _current) : _current;
                _builder.CubicBezierTo(control1, control2, end);
                _current = end;
                _lastCubicControl = control2;
                _lastQuadControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>Q</c>/<c>q</c> (quadratic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>Q</c> or <c>q</c>).</param>
        private void ExecuteQuadratic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var control = ReadPoint(isRelative, _current);
                var end = ReadPoint(isRelative, _current);
                _builder.QuadraticBezierTo(control, end);
                _current = end;
                _lastQuadControl = control;
                _lastCubicControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>T</c>/<c>t</c> (smooth quadratic Bezier) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>T</c> or <c>t</c>).</param>
        private void ExecuteSmoothQuadratic(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var end = ReadPoint(isRelative, _current);
                var control = _lastQuadControl.HasValue ? Reflect(_lastQuadControl.Value, _current) : _current;
                _builder.QuadraticBezierTo(control, end);
                _current = end;
                _lastQuadControl = control;
                _lastCubicControl = null;
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes an <c>A</c>/<c>a</c> (elliptical arc) command and any implicitly repeated groups.</summary>
        /// <param name="command">The literal command letter (<c>A</c> or <c>a</c>).</param>
        private void ExecuteArc(char command)
        {
            var isRelative = char.IsLower(command);
            do
            {
                var radius = new Vector2(MathF.Abs(ReadNumber()), MathF.Abs(ReadNumber()));
                var rotationDegrees = ReadNumber();
                var largeArc = ReadFlag();
                var sweep = ReadFlag();
                var end = ReadPoint(isRelative, _current);
                AppendArc(radius, rotationDegrees, largeArc, sweep, end);
                _workBudget.Charge(1);
            } while (TryPeekNumber());
        }

        /// <summary>Executes a <c>Z</c>/<c>z</c> (close path) command. Takes no arguments and is never repeated.</summary>
        private void ExecuteClose()
        {
            _builder.Close();
            _current = _subpathStart;
            ClearReflectionState();
            _workBudget.Charge(1);
        }

        /// <summary>
        ///     Converts one elliptical arc segment to cubic Beziers (via <see cref="Geometry.SvgArcConverter"/>)
        ///     and appends them to the builder, advancing the current point and clearing the
        ///     smooth-curve reflection state.
        /// </summary>
        /// <param name="radius">The arc's x- and y-radii.</param>
        /// <param name="rotationDegrees">The arc's x-axis rotation, in degrees.</param>
        /// <param name="largeArc">The SVG arc "large-arc-flag".</param>
        /// <param name="sweep">The SVG arc "sweep-flag".</param>
        /// <param name="end">The arc's end point.</param>
        /// <exception cref="OverflowException">
        ///     Thrown when an extreme-but-individually-finite radius/rotation/start/end
        ///     combination causes <see cref="Geometry.SvgArcConverter"/>'s internal rotation/trig
        ///     arithmetic to overflow one of its emitted control points or endpoints to a
        ///     non-finite value. Caught (private to this parser) by <see cref="Parse"/>.
        /// </exception>
        private void AppendArc(Vector2 radius, float rotationDegrees, bool largeArc, bool sweep, Vector2 end)
        {
            var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
            SvgArcConverter.ToBeziers(_current, radius, rotationDegrees, largeArc, sweep, end, segments);
            foreach (var segment in segments)
            {
                _builder.CubicBezierTo(
                    RequireFinite(segment.Control1),
                    RequireFinite(segment.Control2),
                    RequireFinite(segment.End));
            }

            _current = end;
            ClearReflectionState();
        }

        /// <summary>Clears both smooth-curve reflection fields, called after any non-Bezier command.</summary>
        private void ClearReflectionState()
        {
            _lastCubicControl = null;
            _lastQuadControl = null;
        }

        /// <summary>Reflects <paramref name="point"/> through <paramref name="center"/> (<c>2*center - point</c>).</summary>
        /// <param name="point">The point to reflect.</param>
        /// <param name="center">The center of reflection.</param>
        /// <returns>The reflected point.</returns>
        /// <exception cref="OverflowException">
        ///     Thrown when the reflection arithmetic overflows an individually-finite
        ///     <paramref name="point"/>/<paramref name="center"/> pair to a non-finite result - an
        ///     independent overflow path into the same <see cref="Drawing.DashSplitter"/> hang
        ///     risk as relative-coordinate accumulation, reached via the <c>S</c>/<c>s</c> and
        ///     <c>T</c>/<c>t</c> smooth-curve commands. Caught (private to this parser) by
        ///     <see cref="Parse"/>.
        /// </exception>
        private static Vector2 Reflect(Vector2 point, Vector2 center) => RequireFinite((2 * center) - point);

        /// <summary>Reads one <c>x,y</c> coordinate pair, resolving it against <paramref name="reference"/> if relative.</summary>
        /// <param name="isRelative">Whether the pair is relative to <paramref name="reference"/>.</param>
        /// <param name="reference">The reference point for a relative pair (ignored if absolute).</param>
        /// <returns>The resolved, absolute point.</returns>
        /// <exception cref="InvalidDataException">Thrown when a valid number cannot be read.</exception>
        /// <exception cref="OverflowException">
        ///     Thrown when a relative pair's offset accumulation overflows an
        ///     individually-finite <paramref name="reference"/>/offset pair to a non-finite
        ///     result. Caught (private to this parser) by <see cref="Parse"/>.
        /// </exception>
        private Vector2 ReadPoint(bool isRelative, Vector2 reference)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return isRelative ? RequireFinite(reference + new Vector2(x, y)) : new Vector2(x, y);
        }

        /// <summary>
        ///     Validates that <paramref name="value"/>'s components are both finite, throwing
        ///     <see cref="OverflowException"/> otherwise - called at every point produced by
        ///     arithmetic (relative-offset accumulation or smooth-curve reflection), never at a
        ///     point built directly from two already-finite parsed literals (which cannot itself
        ///     overflow, and so needs no check). <see cref="OverflowException"/> (a BCL type,
        ///     rather than a bespoke exception) is caught only within this parser's own
        ///     <see cref="Parse"/> method - it never escapes to this class's top-level
        ///     <c>Load</c>/<c>GetInfo</c> boundary, and so cannot be confused there with an actual
        ///     arithmetic-overflow bug elsewhere in this codec.
        /// </summary>
        /// <param name="value">The point to validate.</param>
        /// <returns><paramref name="value"/> unchanged, when both components are finite.</returns>
        /// <exception cref="OverflowException">Thrown when either component of <paramref name="value"/> is not finite.</exception>
        private static Vector2 RequireFinite(Vector2 value)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            {
                throw new OverflowException("Path-data relative-coordinate accumulation overflowed to a non-finite value.");
            }

            return value;
        }

        /// <summary>
        ///     The scalar overload of <see cref="RequireFinite(Vector2)"/>, used by
        ///     <see cref="ExecuteHorizontal"/>/<see cref="ExecuteVertical"/>'s relative branch,
        ///     where only a single axis is accumulated.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns><paramref name="value"/> unchanged, when finite.</returns>
        /// <exception cref="OverflowException">Thrown when <paramref name="value"/> is not finite.</exception>
        private static float RequireFinite(float value)
        {
            if (!float.IsFinite(value))
            {
                throw new OverflowException("Path-data relative-coordinate accumulation overflowed to a non-finite value.");
            }

            return value;
        }

        /// <summary>Reads one number, advancing past it.</summary>
        /// <returns>The parsed number.</returns>
        /// <exception cref="InvalidDataException">Thrown when a valid number cannot be read at the current position.</exception>
        private float ReadNumber()
        {
            if (!TryReadNumber(_text, ref _position, out var value))
            {
                throw new InvalidDataException("Malformed path data: expected a number.");
            }

            return value;
        }

        /// <summary>Reads one SVG arc flag (a bare <c>0</c> or <c>1</c> digit), advancing past it.</summary>
        /// <returns><see langword="true"/> for <c>1</c>; <see langword="false"/> for <c>0</c>.</returns>
        /// <exception cref="InvalidDataException">Thrown when the current position is not a <c>0</c> or <c>1</c> digit.</exception>
        private bool ReadFlag()
        {
            SkipSeparators(_text, ref _position);
            if (_position < _text.Length && (_text[_position] == '0' || _text[_position] == '1'))
            {
                var value = _text[_position] == '1';
                _position++;
                return value;
            }

            throw new InvalidDataException("Malformed path data: expected an arc flag ('0' or '1').");
        }

        /// <summary>Determines whether another number follows at the current position, without consuming it.</summary>
        /// <returns><see langword="true"/> if another argument group should be read (implicit command repeat).</returns>
        private bool TryPeekNumber()
        {
            var probe = _position;
            return TryReadNumber(_text, ref probe, out _);
        }
    }
}
