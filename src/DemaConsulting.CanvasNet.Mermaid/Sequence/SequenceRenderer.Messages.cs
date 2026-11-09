// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     Messages, notes and activations: Mermaid's <c>drawMessage</c> (the text, the line or self loop, the markers,
///     the central-connection circles and the sequence number), the note box of <c>drawNote</c>, and
///     <c>drawActivation</c>. The SVG markers are placed as SVG places them: at the end of the line, turned with its
///     direction, scaled by its stroke width unless they are in user units, and clipped to their viewport.
/// </content>
internal sealed partial class SequenceRenderer
{
    /// <summary>The radius of a sequence number's circle in the marker's units (<c>SEQUENCE_NUMBER_RADIUS</c>).</summary>
    private const double SequenceNumberRadius = 6;

    /// <summary>How far a central-connection circle moves for a sequence number (<c>CENTRAL_CONNECTION_CIRCLE_OFFSET</c>).</summary>
    private const double CentralConnectionCircleOffset = 16.5;

    /// <summary>The <c>arrowhead</c> marker: a filled triangle in user units.</summary>
    private static readonly MarkerDefinition ArrowHead = new("arrowhead", 7.9, 5, 12, 12, true, true, MarkerPaint.Arrowhead, true, [(-1, 0), (10, 5), (0, 10)]);

    /// <summary>The <c>filled-head</c> marker: a filled dart scaled by the stroke width.</summary>
    private static readonly MarkerDefinition FilledHead = new("filled-head", 15.5, 7, 20, 28, false, false, MarkerPaint.Inherited, true, [(18, 7), (9, 13), (14, 7), (9, 1)]);

    /// <summary>The <c>crosshead</c> marker: two strokes scaled by the stroke width.</summary>
    private static readonly MarkerDefinition CrossHead = new("crosshead", 4, 4.5, 15, 8, false, false, MarkerPaint.Crosshead, false, [(1, 2), (6, 7), (6, 2), (1, 7)]);

    /// <summary>The <c>solidTopArrowHead</c> marker: the upper half of a triangle.</summary>
    private static readonly MarkerDefinition SolidTopArrowHead = new("solidTopArrowHead", 7.9, 7.25, 12, 12, true, true, MarkerPaint.Inherited, true, [(0, 0), (10, 8), (0, 8)]);

    /// <summary>The <c>solidBottomArrowHead</c> marker: the lower half of a triangle.</summary>
    private static readonly MarkerDefinition SolidBottomArrowHead = new("solidBottomArrowHead", 7.9, 0.75, 12, 12, true, true, MarkerPaint.Inherited, true, [(0, 0), (10, 0), (0, 8)]);

    /// <summary>The <c>stickTopArrowHead</c> marker: one black stroke above the line.</summary>
    private static readonly MarkerDefinition StickTopArrowHead = new("stickTopArrowHead", 7.5, 7, 12, 12, true, true, MarkerPaint.Stick, false, [(0, 0), (7, 7)]);

    /// <summary>The <c>stickBottomArrowHead</c> marker: one black stroke below the line.</summary>
    private static readonly MarkerDefinition StickBottomArrowHead = new("stickBottomArrowHead", 7.5, 0, 12, 12, true, true, MarkerPaint.Stick, false, [(0, 7), (7, 0)]);

    /// <summary>The kinds of paint of the markers' shapes.</summary>
    private enum MarkerPaint
    {
        /// <summary>The <c>[id$="-arrowhead"] path</c> rule: filled and stroked in the signal color.</summary>
        Arrowhead,

        /// <summary>The <c>[id$="-crosshead"] path</c> rule: stroked 1 pt in the signal color.</summary>
        Crosshead,

        /// <summary>No rule: filled with the fill the root inherits (the theme's text color).</summary>
        Inherited,

        /// <summary>The marker's own attributes: stroked black, 1.5 wide.</summary>
        Stick,
    }

    /// <summary>
    ///     Draws a message (Mermaid's <c>drawMessage</c>): its text above the line, the line (or the loop of a self
    ///     message) with its markers, the central-connection circles, and the sequence number.
    /// </summary>
    /// <param name="state">The message, with its model.</param>
    /// <param name="lineStartY">The y of the line.</param>
    /// <param name="actors">The participants, by id.</param>
    /// <param name="showSequenceNumbers">Whether the configuration numbers every message.</param>
    public void DrawMessage(SequenceMessageState state, double lineStartY, IReadOnlyDictionary<string, SequenceActorLayout> actors, bool showSequenceNumbers)
    {
        var model = state.MessageModel!;
        var msg = state.Source;
        var id = "i" + msg.Id;
        var location = msg.Location;
        var slot = Append();
        var (startx, stopx) = (model.StartX, model.StopX);

        // The text, centered between the ends
        var (texts, _) = DrawText(
            new SequenceTextData
            {
                Text = model.Message,
                X = Math.Min(startx, stopx),
                Y = model.StartY + 10,
                Width = Math.Abs(stopx - startx),
                Anchor = _conf.MessageAlign,
                Valign = "center",
                TextMargin = _conf.WrapPadding,
                DyEm = true,
                Font = _conf.MessageFont,
                Color = _styles.TextColor(null, ".messageText"),
            },
            "message-text",
            id,
            location);
        slot.Items.AddRange(texts);

        // The line, or the loop of a self message; a reverse or bidirectional loop starts after the number
        var numbered = model.SequenceVisible || showSequenceNumbers;
        var isReverse = IsReverse(model.Type);
        var isBidirectional = model.Type is SequenceLineType.BidirectionalSolid or SequenceLineType.BidirectionalDotted;
        var hasCentral = msg.CentralConnection is SequenceLineType.CentralConnection or SequenceLineType.CentralConnectionReverse or SequenceLineType.CentralConnectionDual;
        var isLine = !ColorMath.Same(startx, stopx);
        IReadOnlyList<SceneSegment> segments;
        double x1 = startx, x2 = stopx;
        if (!isLine)
        {
            var lineStartX = startx + (numbered && (isReverse || isBidirectional) ? 10 : 0);
            var textWidth = _labels.CalculateTextDimensions(model.Message, _conf.MessageFont).Width;
            segments = _conf.RightAngles
                ?
                [
                    SceneSegment.Move(lineStartX, lineStartY),
                    SceneSegment.Line(startx + SequenceLayout.JsMax(_conf.Width / 2, textWidth / 2), lineStartY),
                    SceneSegment.Line(startx + SequenceLayout.JsMax(_conf.Width / 2, textWidth / 2), lineStartY + 25),
                    SceneSegment.Line(startx, lineStartY + 25),
                ]
                :
                [
                    SceneSegment.Move(lineStartX, lineStartY),
                    SceneSegment.Cubic(lineStartX + 60, lineStartY - 10, startx + 60, lineStartY + 30, startx, lineStartY + 20),
                ];
        }
        else
        {
            segments = [];
        }

        // The sequence number moves the ends of a line (a path has no x1 and x2, so a self loop keeps its ends)
        double autonumberX = 0;
        if (numbered)
        {
            var reverseConnection = msg.CentralConnection is SequenceLineType.CentralConnectionDual or SequenceLineType.CentralConnectionReverse;
            if (isBidirectional)
            {
                var centralShift = hasCentral ? -5 : 0;
                var reverseShift = reverseConnection ? -7.5 : 0;
                x1 = startx < stopx ? startx + (SequenceNumberRadius * 2) : startx - SequenceNumberRadius + centralShift + reverseShift;
            }
            else if (isReverse)
            {
                x1 = startx;
                x2 = stopx > startx ? stopx - (2 * SequenceNumberRadius) : stopx - SequenceNumberRadius;
                if (stopx <= startx)
                {
                    x1 += reverseConnection ? -7.5 : 0;
                }

                x2 += hasCentral ? 15 : 0;
            }
            else
            {
                x1 = startx + SequenceNumberRadius;
            }

            // The number is at the sender's side, or at the receiver's for a reverse arrow (whose head is at the sender)
            var atLeft = !isLine || (startx <= stopx) != isReverse;
            autonumberX = atLeft ? model.FromBounds + 1 : model.ToBounds - 1;
        }

        if (isLine)
        {
            segments = LineSegments(x1, lineStartY, x2, lineStartY);
        }

        slot.Items.Add(MessageLine(model.Type, segments, isLine, id, location));
        if (hasCentral)
        {
            slot.Items.Add(CentralConnection(msg, model, lineStartY, actors, id));
        }

        if (numbered)
        {
            slot.Items.Add(SequenceNumber(model.SequenceIndex, autonumberX, lineStartY, id, location));
        }
    }

    /// <summary>
    ///     Draws a note (the drawing of Mermaid's <c>drawNote</c>): its text in the box with the note margin, and the box
    ///     around the measured text.
    /// </summary>
    /// <param name="startx">The left edge.</param>
    /// <param name="starty">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="message">The text.</param>
    /// <param name="msg">The note's message.</param>
    /// <returns>The height of the text, rounded.</returns>
    public double DrawNote(double startx, double starty, double width, string message, SequenceMessage msg)
    {
        var location = msg.Location;
        var id = "i" + msg.Id;
        var (texts, height) = DrawText(
            new SequenceTextData
            {
                Text = message,
                X = startx,
                Y = starty,
                Width = width,
                Anchor = _conf.NoteAlign,
                Valign = "center",
                TextMargin = _conf.NoteMargin,
                DyEm = true,
                Font = _conf.NoteFont,
                RuleWeight = _styles.Resolve(".noteText > tspan")["font-weight"],
                Color = _styles.TextColor(null, ".noteText", ".noteText > tspan"),
            },
            "note-text",
            id,
            location);
        var textHeight = ColorMath.JsRound(height);
        var paint = _styles.Shape(new ShapePaint(NoteRectFill, NoteRectStroke, 1), ".note");
        var children = new List<SceneItem> { Rect(startx, starty, width, textHeight + (2 * _conf.NoteMargin), 0, paint, "note-rect", null, location) };
        children.AddRange(texts);
        Append().Items.Add(new SceneGroup(0, 0, children) { Role = "note", Id = id, Location = location });
        return textHeight;
    }

    /// <summary>
    ///     Draws an activation into the group made when it started (Mermaid's <c>drawActivation</c>); its class cycles
    ///     through three with the number of activations still open on the participant.
    /// </summary>
    /// <param name="activation">The activation.</param>
    /// <param name="verticalPos">The bottom.</param>
    /// <param name="openActivations">The number of activations still open on the participant.</param>
    public void DrawActivation(SequenceActivation activation, double verticalPos, int openActivations)
    {
        var cssClass = "activation" + (openActivations % 3).ToString(CultureInfo.InvariantCulture);
        var paint = _styles.Shape(new ShapePaint(NoteRectFill, NoteRectStroke, 1), "." + cssClass);
        var slot = activation.Anchor ?? Append();
        slot.Items.Add(Rect(
            activation.StartX,
            activation.StartY,
            activation.StopX - activation.StartX,
            verticalPos - activation.StartY,
            0,
            paint,
            "activation",
            cssClass,
            default));
    }

    /// <summary>
    ///     Gets whether a line type is a reverse arrow (its head at the sender).
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsReverse(int type) => type is
        (>= SequenceLineType.SolidArrowTopReverse and <= SequenceLineType.StickArrowBottomReverse)
        or (>= SequenceLineType.SolidArrowTopReverseDotted and <= SequenceLineType.StickArrowBottomReverseDotted);

    /// <summary>
    ///     Gets whether a line type is dotted.
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsDotted(int type) => type is SequenceLineType.Dotted or SequenceLineType.DottedCross or SequenceLineType.DottedPoint
        or SequenceLineType.DottedOpen or SequenceLineType.BidirectionalDotted or (>= SequenceLineType.SolidTopDotted and <= SequenceLineType.StickArrowBottomReverseDotted);

    /// <summary>
    ///     Gets the markers of a line type.
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>The markers at the start and at the end, each null when there is none.</returns>
    private static (MarkerDefinition? Start, MarkerDefinition? End) Markers(int type) => type switch
    {
        SequenceLineType.SolidTop or SequenceLineType.SolidTopDotted => (null, SolidTopArrowHead),
        SequenceLineType.SolidBottom or SequenceLineType.SolidBottomDotted => (null, SolidBottomArrowHead),
        SequenceLineType.StickTop or SequenceLineType.StickTopDotted => (null, StickTopArrowHead),
        SequenceLineType.StickBottom or SequenceLineType.StickBottomDotted => (null, StickBottomArrowHead),
        SequenceLineType.SolidArrowTopReverse or SequenceLineType.SolidArrowTopReverseDotted => (SolidBottomArrowHead, null),
        SequenceLineType.SolidArrowBottomReverse or SequenceLineType.SolidArrowBottomReverseDotted => (SolidTopArrowHead, null),
        SequenceLineType.StickArrowTopReverse or SequenceLineType.StickArrowTopReverseDotted => (StickBottomArrowHead, null),
        SequenceLineType.StickArrowBottomReverse or SequenceLineType.StickArrowBottomReverseDotted => (StickTopArrowHead, null),
        SequenceLineType.Solid or SequenceLineType.Dotted => (null, ArrowHead),
        SequenceLineType.BidirectionalSolid or SequenceLineType.BidirectionalDotted => (ArrowHead, ArrowHead),
        SequenceLineType.SolidPoint or SequenceLineType.DottedPoint => (null, FilledHead),
        SequenceLineType.SolidCross or SequenceLineType.DottedCross => (null, CrossHead),
        _ => (null, null),
    };

    /// <summary>
    ///     Gets the direction of a path at its start or end, in degrees.
    /// </summary>
    /// <param name="segments">The segments of the path.</param>
    /// <param name="atStart">Whether the direction at the start is wanted.</param>
    /// <returns>The angle; zero for a path without length.</returns>
    private static double Direction(IReadOnlyList<SceneSegment> segments, bool atStart)
    {
        double dx, dy;
        if (atStart)
        {
            var first = segments[1];
            var (x, y) = first.Kind == SceneSegmentKind.CubicTo ? (first.X1, first.Y1) : (first.X, first.Y);
            (dx, dy) = (x - segments[0].X, y - segments[0].Y);
        }
        else
        {
            var last = segments[^1];
            var previous = segments[^2];
            var (x, y) = last.Kind == SceneSegmentKind.CubicTo ? (last.X2, last.Y2) : (previous.X, previous.Y);
            (dx, dy) = (last.X - x, last.Y - y);
        }

        return dx == 0 && dy == 0 ? 0 : Math.Atan2(dy, dx) * 180 / Math.PI;
    }

    /// <summary>
    ///     Draws the line of a message with its markers.
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <param name="segments">The geometry.</param>
    /// <param name="isLine">Whether it is a <c>line</c> element (a self loop is a path).</param>
    /// <param name="id">The message id.</param>
    /// <param name="location">The location of the message.</param>
    /// <returns>The group of the line and its markers.</returns>
    private SceneGroup MessageLine(int type, IReadOnlyList<SceneSegment> segments, bool isLine, string id, MermaidLocation location)
    {
        // The style's dash of a dotted line beats the class rule
        var dotted = IsDotted(type);
        var paint = _styles.MessageLine(dotted, isLine);
        IReadOnlyList<double>? dash = dotted ? [3, 3] : null;
        var stroke = Stroke(paint) is { } s ? s with { Dash = dash } : null;
        var children = new List<SceneItem> { new ScenePath(segments) { Role = "message-line", Location = location, Stroke = stroke } };
        var (start, end) = Markers(type);
        if (start is not null)
        {
            var angle = Direction(segments, atStart: true) + (start.AutoStartReverse ? 180 : 0);
            children.Add(PlaceMarker(start, segments[0].X, segments[0].Y, angle, paint.Width, "marker-start", location));
        }

        if (end is not null)
        {
            children.Add(PlaceMarker(end, segments[^1].X, segments[^1].Y, Direction(segments, atStart: false), paint.Width, "marker-end", location));
        }

        return new SceneGroup(0, 0, children) { Role = "message", Id = id, Location = location };
    }

    /// <summary>
    ///     Draws a marker as SVG places it: its reference point at the vertex, turned by the angle, scaled by the stroke
    ///     width unless it is in user units, and clipped to its viewport (a filled shape only).
    /// </summary>
    /// <param name="marker">The marker.</param>
    /// <param name="x">The vertex's x.</param>
    /// <param name="y">The vertex's y.</param>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="strokeWidth">The stroke width of the line.</param>
    /// <param name="role">The role.</param>
    /// <param name="location">The location of the message.</param>
    /// <returns>The marker's path.</returns>
    private ScenePath PlaceMarker(MarkerDefinition marker, double x, double y, double angle, double strokeWidth, string role, MermaidLocation location)
    {
        var scale = marker.UserSpace ? 1 : strokeWidth;
        var (sin, cos) = Math.SinCos(angle * Math.PI / 180);
        (double X, double Y) Map((double X, double Y) p)
        {
            var (px, py) = ((p.X - marker.RefX) * scale, (p.Y - marker.RefY) * scale);
            return (x + (px * cos) - (py * sin), y + (px * sin) + (py * cos));
        }

        // A filled shape stroked in its own color is its outline grown by half the stroke, so the viewport clips both
        var stroked = marker.Paint == MarkerPaint.Arrowhead;
        var segments = new List<SceneSegment>();
        if (marker.Closed)
        {
            var clipped = ClipToViewport(stroked ? GrowPolygon(marker.Points, 0.5 / scale) : marker.Points, marker.Width, marker.Height);
            for (var i = 0; i < clipped.Count; i++)
            {
                var (px, py) = Map(clipped[i]);
                segments.Add(i == 0 ? SceneSegment.Move(px, py) : SceneSegment.Line(px, py));
            }

            segments.Add(SceneSegment.Close());
        }
        else
        {
            // Open strokes: each pair of points is one subpath
            for (var i = 0; i + 1 < marker.Points.Count; i += 2)
            {
                var (ax, ay) = Map(marker.Points[i]);
                var (bx, by) = Map(marker.Points[i + 1]);
                segments.Add(SceneSegment.Move(ax, ay));
                segments.Add(SceneSegment.Line(bx, by));
            }
        }

        var signal = _styles.ArrowheadColor;
        var (fill, stroke) = marker.Paint switch
        {
            MarkerPaint.Arrowhead => (signal, (SceneStroke?)null),
            MarkerPaint.Crosshead => ((Rgba32?)null, _styles.CrossheadColor is { } c ? new SceneStroke(c, 4.0 / 3 * scale) : null),
            MarkerPaint.Stick => (null, new SceneStroke(Black, 1.5)),
            _ => (RootFill, null),
        };

        return new ScenePath(segments) { Role = role, Id = marker.Name, Location = location, Fill = fill, Stroke = stroke };
    }

    /// <summary>
    ///     Grows a polygon by a distance on every side, with mitered corners (the outline of its fill and its stroke).
    /// </summary>
    /// <param name="points">The polygon.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The grown polygon.</returns>
    private static List<(double X, double Y)> GrowPolygon(IReadOnlyList<(double X, double Y)> points, double distance)
    {
        // The outward side of each edge follows from the winding of the polygon
        var area = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Count]);
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        var outward = area > 0 ? distance : -distance;
        (double X, double Y) Normal((double X, double Y) a, (double X, double Y) b)
        {
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            return (dy / length * outward, -dx / length * outward);
        }

        // Each corner moves to where its two moved edges meet
        var grown = new List<(double X, double Y)>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var previous = points[(i + points.Count - 1) % points.Count];
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            var n1 = Normal(previous, current);
            var n2 = Normal(current, next);
            var (sx, sy) = (n1.X + n2.X, n1.Y + n2.Y);
            var dot = (sx * n1.X) + (sy * n1.Y);
            var scale = Math.Abs(dot) < 1e-12 ? 0 : distance * distance / dot;
            grown.Add((current.X + (sx * scale), current.Y + (sy * scale)));
        }

        return grown;
    }

    /// <summary>
    ///     Clips a polygon to a marker's viewport (Sutherland-Hodgman against the four edges).
    /// </summary>
    /// <param name="points">The polygon.</param>
    /// <param name="width">The viewport's width.</param>
    /// <param name="height">The viewport's height.</param>
    /// <returns>The clipped polygon.</returns>
    private static List<(double X, double Y)> ClipToViewport(IReadOnlyList<(double X, double Y)> points, double width, double height)
    {
        var polygon = points.ToList();
        var edges = new (Func<(double X, double Y), double> Inside, bool Vertical, double Value)[]
        {
            (p => p.X, true, 0), (p => width - p.X, true, width), (p => p.Y, false, 0), (p => height - p.Y, false, height),
        };
        foreach (var (inside, vertical, value) in edges)
        {
            var output = new List<(double X, double Y)>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                var currentIn = inside(current) >= 0;
                var previousIn = inside(previous) >= 0;
                if (currentIn != previousIn)
                {
                    var t = vertical ? (value - previous.X) / (current.X - previous.X) : (value - previous.Y) / (current.Y - previous.Y);
                    output.Add((previous.X + (t * (current.X - previous.X)), previous.Y + (t * (current.Y - previous.Y))));
                }

                if (currentIn)
                {
                    output.Add(current);
                }
            }

            polygon = output;
        }

        return polygon;
    }

    /// <summary>
    ///     Draws the circles of a central connection (Mermaid's <c>drawCentralConnection</c>) on the lifelines it
    ///     connects, moved aside for a sequence number.
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <param name="model">The message model.</param>
    /// <param name="lineStartY">The y of the line.</param>
    /// <param name="actors">The participants, by id.</param>
    /// <param name="id">The message id.</param>
    /// <returns>The group of the circles.</returns>
    private SceneGroup CentralConnection(SequenceMessage msg, SequenceMessageModel model, double lineStartY, IReadOnlyDictionary<string, SequenceActorLayout> actors, string id)
    {
        var fromActor = actors[msg.From!];
        var toActor = actors[msg.To!];
        var fromCenter = fromActor.X + (fromActor.Width / 2);
        var toCenter = toActor.X + (toActor.Width / 2);
        var isLeftToRight = fromCenter <= toCenter;
        var isReverse = IsReverse(msg.Type);
        double Offset(bool reverse) => (isLeftToRight ? CentralConnectionCircleOffset : -CentralConnectionCircleOffset) * (reverse ? -1 : 1);
        if (model.SequenceVisible)
        {
            switch (msg.CentralConnection)
            {
                case SequenceLineType.CentralConnection when isReverse:
                    toCenter += Offset(true);
                    break;
                case SequenceLineType.CentralConnectionReverse when !isReverse:
                    fromCenter += Offset(false);
                    break;
                case SequenceLineType.CentralConnectionDual:
                    if (isReverse)
                    {
                        toCenter += Offset(true);
                    }
                    else
                    {
                        fromCenter += Offset(false);
                    }

                    break;
            }
        }

        double[] centers = msg.CentralConnection switch
        {
            SequenceLineType.CentralConnection => [toCenter],
            SequenceLineType.CentralConnectionReverse => [fromCenter],
            _ => [fromCenter, toCenter],
        };
        var fill = RootFill;
        var circles = centers
            .Select(cx => (SceneItem)new ScenePath(CircleSegments(cx, lineStartY, 5)) { Role = "connection-circle", Location = msg.Location, Fill = fill })
            .ToList();
        return new SceneGroup(0, 0, circles) { Role = "connection-circles", Id = id, Location = msg.Location };
    }

    /// <summary>
    ///     Draws a sequence number: the <c>sequencenumber</c> marker of a zero-length line (scaled by the 2-pixel width
    ///     the <c>line</c> rule gives it) and the number on it.
    /// </summary>
    /// <param name="sequenceIndex">The number.</param>
    /// <param name="x">The x.</param>
    /// <param name="lineStartY">The y of the message line.</param>
    /// <param name="id">The message id.</param>
    /// <param name="location">The location of the message.</param>
    /// <returns>The group of the circle and the number.</returns>
    private SceneGroup SequenceNumber(double sequenceIndex, double x, double lineStartY, string id, MermaidLocation location)
    {
        var scale = _styles.Line(new StrokePaint(null, 0, []), "line").Width;
        var circle = new ScenePath(CircleSegments(x, lineStartY, SequenceNumberRadius * scale))
        {
            Role = "sequence-number-marker",
            Location = location,
            Fill = _styles.SequenceNumberMarkerColor,
        };

        // The number: 12 pixels, 9 above three characters, 7 above five
        var text = ColorMath.FormatNumber(sequenceIndex);
        var size = text.Length switch
        {
            > 5 => 7,
            > 3 => 9,
            _ => 12,
        };
        var color = _styles.TextColor(null, ".sequenceNumber") ?? Black;
        var number = TextLine(text, x, lineStartY + 4, SceneTextAnchor.Middle, new SceneFont("sans-serif", size, false, false), color, "sequence-number-text", id, location);
        return new SceneGroup(0, 0, [circle, number]) { Role = "sequence-number", Id = id, Location = location };
    }

    /// <summary>
    ///     An SVG marker of the sequence diagram: its reference point, viewport, units, orientation, paint, and shape (a
    ///     polygon, or pairs of points for strokes).
    /// </summary>
    /// <param name="Name">The marker id without the diagram's prefix.</param>
    /// <param name="RefX">The x of the reference point.</param>
    /// <param name="RefY">The y of the reference point.</param>
    /// <param name="Width">The viewport's width.</param>
    /// <param name="Height">The viewport's height.</param>
    /// <param name="UserSpace">Whether the marker is in user units (<c>markerUnits="userSpaceOnUse"</c>), else scaled by the stroke width.</param>
    /// <param name="AutoStartReverse">Whether the marker turns around at the start of a line (<c>auto-start-reverse</c>).</param>
    /// <param name="Paint">The paint.</param>
    /// <param name="Closed">Whether the points are one closed polygon, else pairs of stroke ends.</param>
    /// <param name="Points">The points.</param>
    private sealed record MarkerDefinition(
        string Name,
        double RefX,
        double RefY,
        double Width,
        double Height,
        bool UserSpace,
        bool AutoStartReverse,
        MarkerPaint Paint,
        bool Closed,
        IReadOnlyList<(double X, double Y)> Points);
}
