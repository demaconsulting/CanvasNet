using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Svek.SvekTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Unit tests of the SvekLayout unit: <see cref="SvekLayout"/> (ranks, lengths, hints, links in one rank, clusters
///     laid out on their own and compound clusters, notes, clipping, labels, regions, spacing, limits) and
///     <see cref="PlantUmlLayoutWorkMeter"/>.
/// </summary>
public class SvekLayoutTests
{
    /// <summary>
    ///     Validates that a link of length 2 puts its second end one rank below the first, with a route from the bottom
    ///     border of the first to the top border of the second.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_TopToBottom_SecondEndBelow()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b)] };

        // Act
        var placement = Place(diagram);

        // Assert
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        Assert.True(boxB.Y >= boxA.Bottom + SvekLayout.MinRankSep - 1e-6);
        var route = LinkOf(placement, "l").Route;
        Assert.True(OnBorder(boxA, route[0]) && Math.Abs(route[0].Y - boxA.Bottom) < 1e-6);
        Assert.True(OnBorder(boxB, route[^1]) && Math.Abs(route[^1].Y - boxB.Y) < 1e-6);
        Assert.Equal((0, 0), (Math.Min(boxA.X, boxB.X), boxA.Y));
        Assert.True(placement.Width >= 80 && placement.Height >= boxB.Bottom);
    }

    /// <summary>
    ///     Validates that a left to right diagram puts the second end right of the first.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LeftToRight_SecondEndRight()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b)], RankDir = SvekRankDir.LeftToRight };

        // Act
        var placement = Place(diagram);

        // Assert
        Assert.True(BoxOf(placement, "B").X >= BoxOf(placement, "A").Right + SvekLayout.MinRankSep - 1e-6);
    }

    /// <summary>
    ///     Validates that each character of an arrow body adds a rank: a link of length 3 reaches lower than one of
    ///     length 2.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_Length_AddsRanks()
    {
        // Arrange
        var (a, b, c) = (Entity("A"), Entity("B"), Entity("C"));
        var diagram = new SvekDiagram { Entities = [a, b, c], Links = [new SvekLink("l1", a, b) { Length = 3 }, new SvekLink("l2", a, c)] };

        // Act
        var placement = Place(diagram);

        // Assert
        Assert.True(BoxOf(placement, "B").Y > BoxOf(placement, "C").Bottom);
    }

    /// <summary>
    ///     Validates that a link of length 1 keeps both ends in one rank, drawn straight between their borders, with its
    ///     label above the middle of the line.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LengthOne_SameRankStraightWithLabelAbove()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b) { Length = 1, Label = "lbl" }] };

        // Act
        var placement = Place(diagram);

        // Assert
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        Assert.Equal(boxA.Y, boxB.Y, 6);
        var link = LinkOf(placement, "l");
        Assert.Equal(2, link.Route.Count);
        Assert.Equal(link.Route[0].Y, link.Route[1].Y, 6);
        Assert.True(link.Label!.Box.Bottom <= link.Route[0].Y);
        Assert.Equal((link.Route[0].X + link.Route[1].X) / 2, link.Label.Box.X + (link.Label.Box.Width / 2), 6);
    }

    /// <summary>
    ///     Validates the order of a row: a chain of one-rank links written out of order keeps every first end left of
    ///     its second, and a cycle of one-rank links still gives one row without overlaps.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LengthOne_ChainAndCycle_OrderedRows()
    {
        // Arrange: E0-E1-...-E5 written from the last link to the first, and the cycle A-B-C-A
        var chain = Enumerable.Range(0, 6).Select(k => Entity("E" + k)).ToList();
        var (a, b, c) = (Entity("A"), Entity("B"), Entity("C"));
        var links = Enumerable.Range(0, 5).Reverse().Select(k => new SvekLink("e" + k, chain[k], chain[k + 1]) { Length = 1 }).ToList();
        links.AddRange([new SvekLink("ab", a, b) { Length = 1 }, new SvekLink("bc", b, c) { Length = 1 }, new SvekLink("ca", c, a) { Length = 1 }]);
        var diagram = new SvekDiagram { Entities = [.. chain, a, b, c], Links = links };

        // Act
        var placement = Place(diagram);

        // Assert: the chain left to right in one rank, the cycle in one rank without overlaps
        var boxes = chain.Select(e => BoxOf(placement, e.Id)).ToList();
        for (var k = 1; k < boxes.Count; k++)
        {
            Assert.Equal(boxes[0].Y, boxes[k].Y, 6);
            Assert.True(boxes[k].X >= boxes[k - 1].Right + SvekLayout.MinNodeSep - 1e-6);
        }

        var cycle = new[] { "A", "B", "C" }.Select(id => BoxOf(placement, id)).ToList();
        Assert.All(cycle, box => Assert.Equal(cycle[0].Y, box.Y, 6));
        Assert.True(cycle[0].Right <= cycle[1].X && cycle[1].Right <= cycle[2].X);
    }

    /// <summary>
    ///     Validates that the label of a link drawn in a row leaves room on both sides: at least 8 pixels to each of the
    ///     two units it is between and <see cref="SvekLayout.LabelClearance"/> above the line (PlantUML leaves about 10
    ///     and 4 pixels).
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_RowLabels_ClearOfBothBoxesAndTheLine()
    {
        // Arrange: a wide label between two narrow entities
        var (a, b) = (Entity("A", 40, 40), Entity("B", 40, 40));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b) { Length = 1, Label = "a long label here" }] };

        // Act
        var placement = Place(diagram);

        // Assert
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        var link = LinkOf(placement, "l");
        var label = link.Label!.Box;
        Assert.True(label.X - boxA.Right >= 8 && boxB.X - label.Right >= 8, $"label {label} between {boxA} and {boxB}");
        Assert.True(link.Route[0].Y - label.Bottom >= SvekLayout.LabelClearance - 1e-6);
    }

    /// <summary>
    ///     Validates that the label of a link into a unit of a row (the state-labels shape: Draft to Review, with Review
    ///     beside Rejected), which dagre places beside its own route, keeps <see cref="SvekLayout.LabelClearance"/> from
    ///     the drawn curve, which runs to the unit's place in the row, and overlaps no entity.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LabelOfLinkIntoARow_ClearOfTheDrawnCurve()
    {
        // Arrange
        var (start, draft, review, rejected, shipping) = (Entity("Start", 20, 20), Entity("Draft", 50, 50), Entity("Review", 66, 50), Entity("Rejected", 75, 50), Entity("Shipping", 285, 98));
        var diagram = new SvekDiagram
        {
            Entities = [start, draft, review, rejected, shipping],
            Links =
            [
                new SvekLink("start", start, draft),
                new SvekLink("submit", draft, review) { Label = "submit" },
                new SvekLink("declined", review, rejected) { Length = 1, Direction = LinkDirection.Left, Label = "declined" },
                new SvekLink("ship", review, shipping),
            ],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var link = LinkOf(placement, "submit");
        var label = link.Label!.Box;
        var curve = SvekGeometry.Flatten(link.Route, 16);
        var nearest = Enumerable.Range(1, curve.Count - 1).Min(k => RectToSegment(label, curve[k - 1], curve[k]));
        Assert.True(nearest >= SvekLayout.LabelClearance - 0.01, $"label {label} is {nearest} from the curve");
        Assert.All(placement.Entities, e => Assert.False(Overlap(label, e.Box), $"label {label} overlaps {e.Entity.Id}"));
    }

    /// <summary>
    ///     Gets the distance from a rectangle to a segment (zero when they touch or cross), measured at 64 points along
    ///     the segment.
    /// </summary>
    /// <param name="box">The rectangle.</param>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <returns>The distance.</returns>
    private static double RectToSegment(SceneRect box, SvekPoint a, SvekPoint b) =>
        Enumerable.Range(0, 65).Select(k =>
        {
            var (x, y) = (a.X + ((b.X - a.X) * k / 64), a.Y + ((b.Y - a.Y) * k / 64));
            var dx = Math.Max(0, Math.Max(box.X - x, x - box.Right));
            var dy = Math.Max(0, Math.Max(box.Y - y, y - box.Bottom));
            return Math.Sqrt((dx * dx) + (dy * dy));
        }).Min();

    /// <summary>
    ///     Validates the direction hints: up puts the second end above the first and keeps the route from the first end
    ///     to the second; left and right keep both ends in one rank.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_Hints_UpReversesLeftRightSameRank()
    {
        // Arrange
        var (a, b, c, d) = (Entity("A"), Entity("B"), Entity("C"), Entity("D"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c, d],
            Links =
            [
                new SvekLink("up", a, b) { Direction = LinkDirection.Up },
                new SvekLink("left", a, c) { Direction = LinkDirection.Left, Length = 1 },
                new SvekLink("right", a, d) { Direction = LinkDirection.Right, Length = 1 },
            ],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var boxA = BoxOf(placement, "A");
        Assert.True(BoxOf(placement, "B").Bottom < boxA.Y);
        var up = LinkOf(placement, "up").Route;
        Assert.True(OnBorder(boxA, up[0]) && Math.Abs(up[0].Y - boxA.Y) < 1e-6);
        Assert.Equal(boxA.Y, BoxOf(placement, "C").Y, 6);
        Assert.Equal(boxA.Y, BoxOf(placement, "D").Y, 6);
    }

    /// <summary>
    ///     Validates that a hidden link is laid out (its ends get ranks) but not drawn.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_HiddenLink_LaidOutNotDrawn()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b) { Style = LinkStyle.Hidden }] };

        // Act
        var placement = Place(diagram);

        // Assert
        Assert.False(LinkOf(placement, "l").Drawn);
        Assert.True(BoxOf(placement, "B").Y > BoxOf(placement, "A").Bottom);
    }

    /// <summary>
    ///     Validates that a link in one rank that would cross another node is drawn as an arc over it.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LinkInOneRankAcrossANode_IsAnArc()
    {
        // Arrange: A - B - C in one rank, and A - C
        var (a, b, c) = (Entity("A"), Entity("B"), Entity("C"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c],
            Links = [new SvekLink("ab", a, b) { Length = 1 }, new SvekLink("bc", b, c) { Length = 1 }, new SvekLink("ac", a, c) { Length = 1 }],
        };

        // Act
        var placement = Place(diagram);

        // Assert: the long link has a control point above the middle node
        var boxes = new[] { "A", "B", "C" }.Select(id => BoxOf(placement, id)).OrderBy(r => r.X).ToList();
        var arc = placement.Links.Where(l => l.Route.Count == 3).ToList();
        var route = Assert.Single(arc).Route;
        Assert.True(route[1].Y < boxes[1].Y);
        Assert.True(placement.Links.Where(l => l.Route.Count == 2).All(l => Math.Abs(l.Route[0].Y - l.Route[1].Y) < 1e-6));
    }

    /// <summary>
    ///     Validates that links drawn in a row between the same two units are kept apart: the first straight, the
    ///     second an arc below, the third an arc above, and the label of an arc at the middle of its curve, beside it and
    ///     clear of it by <see cref="SvekLayout.LabelClearance"/>.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_ParallelLinksInARow_AreSpreadApart()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Links =
            [
                new SvekLink("first", a, b) { Length = 1 },
                new SvekLink("second", a, b) { Length = 1, Label = "below" },
                new SvekLink("third", b, a) { Length = 1, Direction = LinkDirection.Left },
            ],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var middle = BoxOf(placement, "A").Y + (BoxOf(placement, "A").Height / 2);
        Assert.Equal(2, LinkOf(placement, "first").Route.Count);
        var second = LinkOf(placement, "second");
        var third = LinkOf(placement, "third");
        Assert.True(second.Route[1].Y > middle + SvekLayout.ParallelArcStep - 1e-6);
        Assert.True(third.Route[1].Y < middle - SvekLayout.ParallelArcStep + 1e-6);
        var apex = (second.Route[0].Y + (2 * second.Route[1].Y) + second.Route[2].Y) / 4;
        var label = second.Label!.Box;
        Assert.True(label.Bottom <= apex - SvekLayout.LabelClearance + 1e-6 && label.Bottom >= apex - SvekLayout.LabelClearance - 10, $"label {label} apex {apex}");
    }

    /// <summary>
    ///     Validates that unlabeled links between the same two units in neighboring ranks (two one way and one back) are
    ///     not all drawn on one straight line: their routes differ, the middles of their curves are at least 5 pixels
    ///     apart, and every route starts and ends on the borders.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_UnlabeledLinksBetweenTheSameTwoNodes_AreKeptApart()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Links = [new SvekLink("ab1", a, b), new SvekLink("ab2", a, b), new SvekLink("ba", b, a)],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        var routes = new[] { "ab1", "ab2", "ba" }.Select(id => LinkOf(placement, id).Route).ToList();
        var middles = routes.Select(r => SvekGeometry.Flatten(r, 16)).Select(c => c[c.Count / 2]).ToList();
        for (var i = 0; i < routes.Count; i++)
        {
            var (first, last) = i < 2 ? (boxA, boxB) : (boxB, boxA);
            Assert.True(OnBorder(first, routes[i][0]) && OnBorder(last, routes[i][^1]), $"route {i} ends off the borders");
            for (var j = i + 1; j < routes.Count; j++)
            {
                Assert.NotEqual(routes[i], routes[j]);
                var gap = Math.Sqrt(Math.Pow(middles[i].X - middles[j].X, 2) + Math.Pow(middles[i].Y - middles[j].Y, 2));
                Assert.True(gap >= 5, $"the middles of routes {i} and {j} are {gap} apart");
            }
        }
    }

    /// <summary>
    ///     Validates that a label with no clear place within 32 pixels (the label of a loop on a small entity, right of
    ///     which stands a large entity of its row) stays where it was placed: right of the loop, at the middle of the
    ///     entity.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LabelWithNoClearPlace_StaysWhereItWasPlaced()
    {
        // Arrange
        var (a, b) = (Entity("A", 40, 40), Entity("B", 200, 200));
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Links = [new SvekLink("row", a, b) { Length = 1 }, new SvekLink("loop", a, a) { Label = "loop label" }],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var boxA = BoxOf(placement, "A");
        var label = LinkOf(placement, "loop").Label!.Box;
        Assert.Equal(boxA.Right + 18 + 8 + SvekLayout.LabelOffset, label.X, 6);
        Assert.Equal(boxA.Y + (boxA.Height / 2), label.Y + (label.Height / 2), 6);
    }

    /// <summary>
    ///     Validates that the label of a link from a cluster that links cross, which dagre centers inside the cluster,
    ///     is placed beside the part of the route outside the cluster.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LabelOfLinkFromCompoundCluster_StaysOutside()
    {
        // Arrange
        var cluster = new SvekCluster("P") { Image = new TestClusterImage(20, 10, 0) };
        var (inside, outside, target) = (Entity("In", parent: cluster), Entity("Out"), Entity("T"));
        var diagram = new SvekDiagram
        {
            Entities = [inside, outside, target],
            Clusters = [cluster],
            Links = [new SvekLink("cross", outside, inside), new SvekLink("away", cluster, target) { Label = "leave" }],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var box = placement.Clusters[0].Box;
        var label = LinkOf(placement, "away").Label!.Box;
        Assert.False(Overlap(box, label));
    }

    /// <summary>
    ///     Validates the notes attached to each side of an entity: left and right ones in its rank, 18 from the entity
    ///     and centered on it, top and bottom ones a rank before and after it, each with a pointer from the side facing
    ///     the entity to the entity's border.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_AttachedNotes_OnEachSideWithPointers()
    {
        // Arrange
        var a = Entity("A");
        var notes = new[] { NotePosition.Left, NotePosition.Right, NotePosition.Top, NotePosition.Bottom }
            .Select(p => new SvekNote("N" + p, p.ToString()) { Target = a, Position = p })
            .ToList();
        var diagram = new SvekDiagram { Entities = [a], Notes = notes };

        // Act
        var placement = Place(diagram);

        // Assert
        var box = BoxOf(placement, "A");
        var placed = placement.Notes.ToDictionary(n => n.Note.Position);
        Assert.Equal(box.X - SvekLayout.NoteGap, placed[NotePosition.Left].Box.Right, 6);
        Assert.Equal(box.Right + SvekLayout.NoteGap, placed[NotePosition.Right].Box.X, 6);
        Assert.True(placed[NotePosition.Top].Box.Bottom <= box.Y - SvekLayout.MinRankSep + 1e-6);
        Assert.True(placed[NotePosition.Bottom].Box.Y >= box.Bottom + SvekLayout.MinRankSep - 1e-6);
        Assert.Equal<NotePosition?>(NotePosition.Right, placed[NotePosition.Left].PointerSide);
        Assert.Equal<NotePosition?>(NotePosition.Left, placed[NotePosition.Right].PointerSide);
        Assert.Equal<NotePosition?>(NotePosition.Bottom, placed[NotePosition.Top].PointerSide);
        Assert.Equal<NotePosition?>(NotePosition.Top, placed[NotePosition.Bottom].PointerSide);
        Assert.All(placed.Values, n => Assert.True(OnBorder(box, n.PointerTip)));
        Assert.All(placed.Values, n => Assert.False(Overlap(n.Box, box)));
    }

    /// <summary>
    ///     Validates that the notes attached above and below linked entities are nodes of their own that the link routes
    ///     keep clear of (no route crosses a note, no note overlaps an entity).
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_AttachedNotes_KeepClearOfLinks()
    {
        // Arrange
        var (reader, sensor) = (Entity("Reader"), Entity("Sensor"));
        var diagram = new SvekDiagram
        {
            Entities = [sensor, reader],
            Notes =
            [
                new SvekNote("N1", "left") { Target = sensor, Position = NotePosition.Left },
                new SvekNote("N2", "right") { Target = reader, Position = NotePosition.Right },
                new SvekNote("N3", "top note") { Target = sensor, Position = NotePosition.Top },
                new SvekNote("N4", "bottom note") { Target = reader, Position = NotePosition.Bottom },
            ],
            Links = [new SvekLink("l1", reader, sensor) { HeadDecor = LinkDecor.Arrow }],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var route = LinkOf(placement, "l1").Route;
        var entities = placement.Entities.Select(e => e.Box).ToList();
        foreach (var note in placement.Notes)
        {
            var inner = new SceneRect(note.Box.X + 0.5, note.Box.Y + 0.5, note.Box.Width - 1, note.Box.Height - 1);
            Assert.DoesNotContain(route.Zip(route.Skip(1)), s => SegmentCrosses(s.First, s.Second, inner));
            Assert.DoesNotContain(entities, e => Overlap(e, note.Box));
        }
    }

    /// <summary>
    ///     Validates that in a left to right diagram the notes above and below an entity share its rank (stacked with
    ///     it) and the notes left and right of it are a rank before and after it.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_AttachedNotesLeftToRight_KeepTheirSides()
    {
        // Arrange
        var a = Entity("A");
        var notes = new[] { NotePosition.Left, NotePosition.Right, NotePosition.Top, NotePosition.Bottom }
            .Select(p => new SvekNote("N" + p, p.ToString()) { Target = a, Position = p })
            .ToList();
        var diagram = new SvekDiagram { Entities = [a], Notes = notes, RankDir = SvekRankDir.LeftToRight };

        // Act
        var placement = Place(diagram);

        // Assert
        var box = BoxOf(placement, "A");
        var placed = placement.Notes.ToDictionary(n => n.Note.Position, n => n.Box);
        Assert.Equal(box.Y - SvekLayout.NoteGap, placed[NotePosition.Top].Bottom, 6);
        Assert.Equal(box.Bottom + SvekLayout.NoteGap, placed[NotePosition.Bottom].Y, 6);
        Assert.True(placed[NotePosition.Left].Right < box.X);
        Assert.True(placed[NotePosition.Right].X > box.Right);
    }

    /// <summary>
    ///     Decides whether a segment crosses the inside of a rectangle (sampled finely).
    /// </summary>
    /// <param name="a">The start.</param>
    /// <param name="b">The end.</param>
    /// <param name="box">The rectangle.</param>
    /// <returns>True when a sample of the segment is inside.</returns>
    private static bool SegmentCrosses(SvekPoint a, SvekPoint b, SceneRect box) =>
        Enumerable.Range(0, 201).Select(k => k / 200.0)
            .Any(t =>
            {
                var (x, y) = (a.X + (t * (b.X - a.X)), a.Y + (t * (b.Y - a.Y)));
                return x > box.X && x < box.Right && y > box.Y && y < box.Bottom;
            });

    /// <summary>
    ///     Validates that a floating note with a single link to an entity points at the entity instead of drawing the
    ///     link, and that a note with two links keeps them.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_FloatingNoteWithOneLink_PointsAtTheEntity()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var (single, shared) = (new SvekNote("N1", "one link"), new SvekNote("N2", "two links"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Notes = [single, shared],
            Links = [new SvekLink("l1", a, single) { Style = LinkStyle.Dashed }, new SvekLink("l2", shared, a), new SvekLink("l3", shared, b)],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        Assert.False(LinkOf(placement, "l1").Drawn);
        var note = placement.Notes.Single(n => n.Note.Id == "N1");
        Assert.NotNull(note.PointerSide);
        Assert.True(OnBorder(BoxOf(placement, "A"), note.PointerTip));
        Assert.True(OnBorder(note.Box, note.PointerBase));
        Assert.True(LinkOf(placement, "l2").Drawn && LinkOf(placement, "l3").Drawn);
        Assert.Null(placement.Notes.Single(n => n.Note.Id == "N2").PointerSide);
    }

    /// <summary>
    ///     Validates that a note on a link is placed in the label slot of the link, below its label.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_NoteOnLink_BelowTheLabel()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var link = new SvekLink("l", a, b) { Label = "label" };
        var diagram = new SvekDiagram { Entities = [a, b], Links = [link], Notes = [new SvekNote("N", "on link") { Link = link, Position = NotePosition.Bottom }] };

        // Act
        var placement = Place(diagram);

        // Assert
        var label = LinkOf(placement, "l").Label!.Box;
        var note = placement.Notes.Single().Box;
        Assert.Equal(label.Bottom, note.Y, 6);
        Assert.False(Overlap(note, BoxOf(placement, "A")) || Overlap(note, BoxOf(placement, "B")));
    }

    /// <summary>
    ///     Validates that a cluster that no link crosses is laid out on its own: it encloses its members with its padding
    ///     and header, and a link to the cluster ends on its border.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_ClusterWithoutCrossingLinks_LaidOutOnItsOwn()
    {
        // Arrange
        var image = new TestClusterImage(28, 16, 200);
        var package = new SvekCluster("P") { Image = image };
        var (a, b, c) = (Entity("A", parent: package), Entity("B", parent: package), Entity("C"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c],
            Clusters = [package],
            Links = [new SvekLink("inner", a, b), new SvekLink("outer", c, package)],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var box = placement.Clusters.Single().Box;
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        Assert.True(Contains(box, boxA) && Contains(box, boxB));
        Assert.Equal(box.Y + image.HeaderHeight + image.Padding, Math.Min(boxA.Y, boxB.Y), 6);
        Assert.True(box.Width >= 200 - 1e-6);
        Assert.True(OnBorder(box, LinkOf(placement, "outer").Route[^1]));
        Assert.False(Overlap(BoxOf(placement, "C"), box));
    }

    /// <summary>
    ///     Validates that a cluster that links cross is a compound node made tight around its members with its padding,
    ///     with a header band above them, overlapping nothing outside, and that a link to the cluster is cut at its
    ///     border.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_CompoundCluster_HeaderBandAndCutLinks()
    {
        // Arrange
        var image = new TestClusterImage(30, 16, 0);
        var package = new SvekCluster("P") { Image = image };
        var (a, b) = (Entity("A", parent: package), Entity("B", parent: package));
        var (x, y, z) = (Entity("X"), Entity("Y"), Entity("Z"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, x, y, z],
            Clusters = [package],
            Links = [new SvekLink("xa", x, a), new SvekLink("ab", a, b), new SvekLink("by", b, y), new SvekLink("zp", z, package)],
        };

        // Act
        var placement = Place(diagram);

        // Assert: the members inside with the header above them, nothing else inside
        var box = placement.Clusters.Single().Box;
        var members = new[] { BoxOf(placement, "A"), BoxOf(placement, "B") };
        Assert.All(members, m => Assert.True(Contains(box, m)));
        Assert.True(members.Min(m => m.Y) >= box.Y + image.HeaderHeight + image.Padding - 1e-6);
        Assert.All(new[] { "X", "Y", "Z" }, id => Assert.False(Overlap(BoxOf(placement, id), box)));
        Assert.True(OnBorder(box, LinkOf(placement, "zp").Route[^1], 1e-6));
    }

    /// <summary>
    ///     Validates that nested compound clusters each get their header band and that no two entities overlap.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_NestedCompoundClusters_NoOverlaps()
    {
        // Arrange
        var outer = new SvekCluster("O") { Image = new TestClusterImage(30, 16, 0) };
        var inner = new SvekCluster("O.I") { Parent = outer, Image = new TestClusterImage(20, 16, 0) };
        var (a, b, c, d) = (Entity("A", parent: inner), Entity("B", parent: outer), Entity("C"), Entity("D", parent: inner));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c, d],
            Clusters = [outer, inner],
            Links = [new SvekLink("ca", c, a), new SvekLink("ab", a, b), new SvekLink("bc", b, c), new SvekLink("ad", a, d) { Length = 1 }],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var (outerBox, innerBox) = (placement.Clusters[0].Box, placement.Clusters[1].Box);
        Assert.True(Contains(outerBox, innerBox));
        Assert.True(innerBox.Y >= outerBox.Y + 30 - 1e-6);
        Assert.True(BoxOf(placement, "A").Y >= innerBox.Y + 20 + 16 - 1e-6);
        var boxes = placement.Entities.Select(e => e.Box).ToList();
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                Assert.False(Overlap(boxes[i], boxes[j]));
            }
        }

        Assert.False(Overlap(BoxOf(placement, "C"), outerBox));
    }

    /// <summary>
    ///     Validates a link from an entity to itself and two parallel links: every route has points, starts and ends on
    ///     the borders, and the parallel routes differ.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_SelfAndParallelLinks_AreRouted()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Links = [new SvekLink("self", a, a), new SvekLink("p1", a, b) { Label = "one" }, new SvekLink("p2", a, b) { Label = "two" }],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var self = LinkOf(placement, "self").Route;
        Assert.True(self.Count >= 3);
        Assert.True(OnBorder(BoxOf(placement, "A"), self[0]) && OnBorder(BoxOf(placement, "A"), self[^1]));
        Assert.NotEqual(LinkOf(placement, "p1").Route, LinkOf(placement, "p2").Route);
    }

    /// <summary>
    ///     Validates that the routes end on the outline the image gives: an ellipse, and a circle inside a larger box.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_ClipOutlines_RoutesEndOnThem()
    {
        // Arrange: a use case ellipse and an interface circle with its name below
        var ellipse = Entity("U", 120, 40, clip: new SvekClip(ClipShape.Ellipse, new SceneRect(0, 0, 120, 40)));
        var circle = Entity("I", 60, 40, clip: new SvekClip(ClipShape.Ellipse, new SceneRect(22, 0, 16, 16)));
        var a = Entity("A");
        var diagram = new SvekDiagram { Entities = [a, ellipse, circle], Links = [new SvekLink("u", a, ellipse), new SvekLink("i", circle, a)] };

        // Act
        var placement = Place(diagram);

        // Assert
        var u = BoxOf(placement, "U");
        var end = LinkOf(placement, "u").Route[^1];
        Assert.Equal(1, (Math.Pow(end.X - (u.X + 60), 2) / 3600) + (Math.Pow(end.Y - (u.Y + 20), 2) / 400), 6);
        var i = BoxOf(placement, "I");
        var start = LinkOf(placement, "i").Route[0];
        Assert.Equal(8, Math.Sqrt(Math.Pow(start.X - (i.X + 30), 2) + Math.Pow(start.Y - (i.Y + 8), 2)), 6);
    }

    /// <summary>
    ///     Validates that links from a node above to three nodes below it, spread wider than the node, end on its bottom
    ///     side and on the top sides of the nodes below, never within 5 pixels of a corner, that the link to the node
    ///     below its middle runs straight down, and that the end label at the node above stays next to its end, off the
    ///     node (dagre's route through the label rank would end at a corner).
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LinksFromBelow_EndOnTheSidesNotTheCorners()
    {
        // Arrange: the class-ends shape: Item above Loan, Book, and Film, with the multiplicity 1 at Item
        var item = Entity("Item", 59, 48);
        var below = new[] { Entity("Loan", 63, 48), Entity("Book", 64, 48), Entity("Film", 58, 48) };
        var links = below.Select(e => new SvekLink(e.Id, item, e) { TailLabel = e.Id == "Loan" ? "1" : null }).ToList();
        var diagram = new SvekDiagram { Entities = [item, .. below], Links = links };

        // Act
        var placement = Place(diagram);

        // Assert
        var top = BoxOf(placement, "Item");
        foreach (var entity in below)
        {
            var route = LinkOf(placement, entity.Id).Route;
            var box = BoxOf(placement, entity.Id);
            Assert.Equal(top.Bottom, route[0].Y, 6);
            Assert.InRange(route[0].X, top.X + 5, top.Right - 5);
            Assert.Equal(box.Y, route[^1].Y, 6);
            Assert.InRange(route[^1].X, box.X + 5, box.Right - 5);
        }

        // The node below the middle of the node above is reached straight down, at the middle of the narrower box
        var straightDown = LinkOf(placement, "Book").Route;
        Assert.Equal(straightDown[0].X, straightDown[^1].X, 6);
        Assert.Equal(top.X + (top.Width / 2), straightDown[0].X, 6);

        var start = LinkOf(placement, "Loan").Route[0];
        var label = LinkOf(placement, "Loan").TailLabel!.Box;
        var dx = Math.Max(0, Math.Max(label.X - start.X, start.X - label.Right));
        var dy = Math.Max(0, Math.Max(label.Y - start.Y, start.Y - label.Bottom));
        Assert.True(Math.Sqrt((dx * dx) + (dy * dy)) <= 12, $"label {label} end {start}");
        Assert.False(Overlap(label, top));
    }

    /// <summary>
    ///     Validates that the ends of four links on the bottom side of a node, which all aim beyond its corners, are kept
    ///     <see cref="SvekLayout.EndSpacing"/> apart on that side, off its corners, and that the multiplicity of one of
    ///     them stays next to its end, off the triangles of the other links.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LinksCrowdingOneSide_EndApartWithTheMultiplicityClear()
    {
        // Arrange: Item above Loan (an arrow with the multiplicity 1 at Item) and three extensions, all wider than it
        var item = Entity("Item", 59, 48);
        var below = new[] { Entity("Loan", 63, 48), Entity("Book", 64, 48), Entity("Film", 58, 48), Entity("Disc", 59, 48) };
        var diagram = new SvekDiagram { Entities = [item, .. below], Links = LinksFrom(item, below) };

        // Act
        var placement = Place(diagram);

        // Assert: every link starts on the bottom side of Item, off its corners, at least EndSpacing from the others
        var top = BoxOf(placement, "Item");
        var starts = below.Select(e => LinkOf(placement, e.Id).Route[0]).ToList();
        Assert.All(starts, start =>
        {
            Assert.Equal(top.Bottom, start.Y, 6);
            Assert.InRange(start.X, top.X + 5, top.Right - 5);
        });
        AssertApart(starts, SvekLayout.EndSpacing);

        // The multiplicity is next to its end and on none of the triangles
        var loan = LinkOf(placement, "Loan");
        var label = loan.TailLabel!.Box;
        Assert.DoesNotContain(TriangleBoundsOf(placement, below), head => Overlap(label, head));
        Assert.True(DistanceTo(label, loan.Route[0]) <= 12, $"label {label} end {loan.Route[0]}");
    }

    /// <summary>
    ///     Validates that the multiplicity of a link between the triangles of four other links on one side of a node
    ///     overlaps none of them and stays near its end, and that the five ends are spread evenly over the side, which is
    ///     too short to keep them <see cref="SvekLayout.EndSpacing"/> apart.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_EndLabelBetweenTwoHeads_OverlapsNoHead()
    {
        // Arrange: Item above four extensions with the arrow of Loan, with the multiplicity 1, in the middle
        var item = Entity("Item", 59, 48);
        var below = new[] { Entity("A", 41, 48), Entity("B", 41, 48), Entity("Loan", 63, 48), Entity("C", 42, 48), Entity("D", 42, 48) };
        var diagram = new SvekDiagram { Entities = [item, .. below], Links = LinksFrom(item, below) };

        // Act
        var placement = Place(diagram);

        // Assert: the multiplicity is on none of the triangles and near its end
        var loan = LinkOf(placement, "Loan");
        var label = loan.TailLabel!.Box;
        Assert.DoesNotContain(TriangleBoundsOf(placement, below), head => Overlap(label, head));
        Assert.True(DistanceTo(label, loan.Route[0]) <= 24, $"label {label} end {loan.Route[0]}");

        // The five ends are spread evenly over the part of the side the corner insets leave
        var top = BoxOf(placement, "Item");
        AssertApart(below.Select(e => LinkOf(placement, e.Id).Route[0]).ToList(), (top.Width - 20) / 4);
    }

    /// <summary>
    ///     Validates that, from left to right, the ends of three links leaving a node and of one link arriving at it on
    ///     its right side are kept apart on that side.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_LeftToRightLinksBothWays_EndApartOnOneSide()
    {
        // Arrange: A links to B, C, and D (with a label), and B links back to A
        var (a, b, c, d) = (Entity("A", 50, 50), Entity("B", 50, 50), Entity("C", 50, 50), Entity("D", 50, 50));
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c, d],
            Links = [new SvekLink("ab", a, b), new SvekLink("ac", a, c), new SvekLink("ad", a, d) { Label = "go" }, new SvekLink("ba", b, a)],
            RankDir = SvekRankDir.LeftToRight,
        };

        // Act
        var placement = Place(diagram);

        // Assert: the four ends at A are on its border and spread over the 30 pixels its corner insets leave
        var boxA = BoxOf(placement, "A");
        var ends = new List<SvekPoint>
        {
            LinkOf(placement, "ab").Route[0],
            LinkOf(placement, "ac").Route[0],
            LinkOf(placement, "ad").Route[0],
            LinkOf(placement, "ba").Route[^1],
        };
        Assert.All(ends, end => Assert.True(OnBorder(boxA, end), $"{end} off A {boxA}"));
        AssertApart(ends, 10);
    }

    /// <summary>
    ///     Validates that the label of one link of a fan-out (Hub to A, B, C with a label, and D) keeps
    ///     <see cref="SvekLayout.LabelClearance"/> from every drawn line: the curves and arrowheads of all four links, so
    ///     that no neighboring link is drawn straight through it.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_FanOutWithOneLabel_LabelClearOfEveryLine()
    {
        // Arrange
        var hub = Entity("Hub", 50, 50);
        var below = new[] { Entity("A", 50, 50), Entity("B", 50, 50), Entity("C", 50, 50), Entity("D", 50, 50) };
        var links = below.Select(e => new SvekLink(e.Id, hub, e) { HeadDecor = LinkDecor.Arrow, Label = e.Id == "C" ? "uses" : null }).ToList();
        var diagram = new SvekDiagram { Entities = [hub, .. below], Links = links };

        // Act
        var placement = Place(diagram);

        // Assert
        var label = LinkOf(placement, "C").Label!.Box;
        foreach (var entity in below)
        {
            var nearest = LinesOf(LinkOf(placement, entity.Id)).Min(line => Enumerable.Range(1, line.Count - 1).Min(k => RectToSegment(label, line[k - 1], line[k])));
            Assert.True(nearest >= SvekLayout.LabelClearance - 0.01, $"label {label} is {nearest} from the link to {entity.Id}");
        }
    }

    /// <summary>
    ///     Validates that the end labels of four links at one node (multiplicities at both ends of three of them, a
    ///     label on one, and the decorations of composition, aggregation, arrow, and extension at the node) overlap no
    ///     other end label, no middle label, and no decoration of another link.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_EndLabelsAtOneNode_OverlapNoOtherLabelOrHead()
    {
        // Arrange: the multiplicities shape: Hub "1" *-- "many" A, Hub "1" o-- "many" B, Hub "2" <-- "3" C : uses, Hub "x" <|-- D
        var hub = Entity("Hub", 59, 48);
        var below = new[] { Entity("A", 50, 48), Entity("B", 50, 48), Entity("C", 50, 48), Entity("D", 50, 48) };
        var diagram = new SvekDiagram
        {
            Entities = [hub, .. below],
            Links =
            [
                new SvekLink("A", hub, below[0]) { TailDecor = LinkDecor.Composition, TailLabel = "1", HeadLabel = "many" },
                new SvekLink("B", hub, below[1]) { TailDecor = LinkDecor.Aggregation, TailLabel = "1", HeadLabel = "many" },
                new SvekLink("C", hub, below[2]) { TailDecor = LinkDecor.Arrow, TailLabel = "2", HeadLabel = "3", Label = "uses" },
                new SvekLink("D", hub, below[3]) { TailDecor = LinkDecor.Extends, TailLabel = "x" },
            ],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var ends = placement.Links.SelectMany(l => new[] { (l.Link.Id, Box: l.TailLabel?.Box), (l.Link.Id, Box: l.HeadLabel?.Box) })
            .Where(e => e.Box is not null).Select(e => (e.Id, Box: e.Box!.Value)).ToList();
        Assert.Equal(7, ends.Count);
        var middle = LinkOf(placement, "C").Label!.Box;
        for (var i = 0; i < ends.Count; i++)
        {
            Assert.False(Overlap(ends[i].Box, middle), $"end label {i} {ends[i].Box} overlaps the label {middle}");
            for (var j = i + 1; j < ends.Count; j++)
            {
                Assert.False(Overlap(ends[i].Box, ends[j].Box), $"end labels {i} {ends[i].Box} and {j} {ends[j].Box} overlap");
            }

            foreach (var other in placement.Links.Where(l => l.Link.Id != ends[i].Id))
            {
                Assert.DoesNotContain(LinesOf(other).Skip(1), head => Overlap(ends[i].Box, BoundsOf(head)));
            }
        }
    }

    /// <summary>
    ///     Gets the lines a placed link draws: the curve of its route, then the outlines of the decorations at its ends.
    /// </summary>
    /// <param name="link">The placed link.</param>
    /// <returns>The lines, as polylines.</returns>
    private static List<List<SvekPoint>> LinesOf(PlacedLink link)
    {
        var lines = new List<List<SvekPoint>> { SvekGeometry.Flatten(link.Route, 16) };
        foreach (var (decor, atEnd) in new[] { (link.Link.TailDecor, false), (link.Link.HeadDecor, true) })
        {
            var extremity = SvekGeometry.Extremity(decor, atEnd ? link.Route[^1] : link.Route[0], SvekGeometry.ArrivalAngle(link.Route, atEnd));
            foreach (var part in extremity?.Parts ?? [])
            {
                var outline = part.Segments.Where(s => s.Kind != SceneSegmentKind.Close).Select(s => new SvekPoint(s.X, s.Y)).ToList();
                if (part.Segments[^1].Kind == SceneSegmentKind.Close)
                {
                    outline.Add(outline[0]);
                }

                lines.Add(outline);
            }
        }

        return lines;
    }

    /// <summary>
    ///     Gets the bounds of a polyline.
    /// </summary>
    /// <param name="line">The points.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect BoundsOf(List<SvekPoint> line)
    {
        var (x0, y0) = (line.Min(p => p.X), line.Min(p => p.Y));
        return new SceneRect(x0, y0, line.Max(p => p.X) - x0, line.Max(p => p.Y) - y0);
    }

    /// <summary>
    ///     Validates the labels: the middle label beside its route with its direction triangle pointing along the link,
    ///     and the end labels near their ends, off the entities.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_Labels_PlacedBesideTheRoute()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var link = new SvekLink("l", a, b) { Label = "uses", LabelArrow = LinkLabelArrow.Forward, TailLabel = "1", HeadLabel = "0..*" };
        var diagram = new SvekDiagram { Entities = [a, b], Links = [link] };

        // Act
        var placement = Place(diagram);

        // Assert
        var placed = LinkOf(placement, "l");
        var (boxA, boxB) = (BoxOf(placement, "A"), BoxOf(placement, "B"));
        Assert.Equal(Math.PI / 2, placed.Label!.ArrowAngle ?? double.NaN, 6);
        Assert.True(placed.Label.Box.X >= placed.Route[0].X);
        Assert.False(Overlap(placed.TailLabel!.Box, boxA) || Overlap(placed.TailLabel.Box, boxB));
        Assert.False(Overlap(placed.HeadLabel!.Box, boxA) || Overlap(placed.HeadLabel.Box, boxB));
        Assert.True(placed.TailLabel.Box.Y < placed.HeadLabel.Box.Y);
        Assert.True(placed.TailLabel.Box.Y - boxA.Bottom < 30 && boxB.Y - placed.HeadLabel.Box.Bottom < 30);
    }

    /// <summary>
    ///     Validates the concurrent regions of a cluster: stacked regions with horizontal separators, regions side by
    ///     side with vertical separators, and a link between two regions left unrouted.
    /// </summary>
    /// <param name="arrangement">The arrangement (a <see cref="ClusterConcurrency"/> name).</param>
    [Theory]
    [InlineData("Stacked")]
    [InlineData("SideBySide")]
    public void PlantUmlSvekLayout_Run_ConcurrentRegions_ArrangedWithSeparators(string arrangement)
    {
        // Arrange
        var concurrency = Enum.Parse<ClusterConcurrency>(arrangement);
        var composite = new SvekCluster("S") { Concurrency = concurrency, Image = new TestClusterImage(20, 10, 0) };
        var (r1, r2) = (new SvekCluster("S.1") { Parent = composite }, new SvekCluster("S.2") { Parent = composite });
        var (a, b, c) = (Entity("A", parent: r1), Entity("B", parent: r1), Entity("C", parent: r2));
        var outside = Entity("X");
        var diagram = new SvekDiagram
        {
            Entities = [a, b, c, outside],
            Clusters = [composite, r1, r2],
            Links = [new SvekLink("ab", a, b), new SvekLink("cross", b, c), new SvekLink("in", outside, a)],
        };

        // Act
        var placement = Place(diagram);

        // Assert
        var box = placement.Clusters[0].Box;
        var separator = Assert.Single(placement.Clusters[0].Separators);
        var (regionA, regionC) = (placement.Clusters[1].Box, placement.Clusters[2].Box);
        if (concurrency == ClusterConcurrency.Stacked)
        {
            Assert.Equal((box.X + SvekLayout.SeparatorInset, box.Right - SvekLayout.SeparatorInset), (separator.From.X, separator.To.X));
            Assert.Equal(regionA.Bottom, separator.From.Y, 6);
            Assert.True(BoxOf(placement, "C").Y > BoxOf(placement, "B").Bottom);
        }
        else
        {
            Assert.Equal((box.Y + 20 + SvekLayout.SeparatorInset, box.Bottom - SvekLayout.SeparatorInset), (separator.From.Y, separator.To.Y));
            Assert.Equal(regionA.Right, separator.From.X, 6);
            Assert.True(BoxOf(placement, "C").X > BoxOf(placement, "B").Right);
        }

        Assert.True(Contains(box, regionA) && Contains(box, regionC));
        Assert.False(LinkOf(placement, "cross").Drawn);
        Assert.Empty(LinkOf(placement, "cross").Route);
        Assert.True(OnBorder(box, LinkOf(placement, "in").Route[^1]));
    }

    /// <summary>
    ///     Validates that the diagram's space between ranks replaces PlantUML's default.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_RankSep_ChangesTheSpace()
    {
        // Arrange
        var (a, b) = (Entity("A"), Entity("B"));
        var diagram = new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b)], RankSep = 150 };

        // Act
        var placement = Place(diagram);

        // Assert
        Assert.True(BoxOf(placement, "B").Y - BoxOf(placement, "A").Bottom >= 150 - 1e-6);
    }

    /// <summary>
    ///     Validates that an empty diagram has no size.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_EmptyDiagram_HasNoSize()
    {
        // Act
        var placement = Place(new SvekDiagram());

        // Assert
        Assert.Equal((0, 0), (placement.Width, placement.Height));
    }

    /// <summary>
    ///     Validates that the work of a layout is deterministic and counted by the meter.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_Work_IsDeterministic()
    {
        // Arrange
        static (long Used, SvekPlacement Placement) Run()
        {
            var entities = Enumerable.Range(0, 6).Select(k => Entity("E" + k)).ToList();
            var links = entities.Skip(1).Select((e, k) => new SvekLink("l" + k, entities[k / 2], e) { Label = "x" }).ToList();
            var meter = new PlantUmlLayoutWorkMeter(PlantUmlLimits.Default.MaxLayoutWork, default, CancellationToken.None);
            var placement = SvekLayout.Run(new SvekDiagram { Entities = entities, Links = links }, Context(), meter);
            return (meter.Used, placement);
        }

        // Act
        var (first, second) = (Run(), Run());

        // Assert
        Assert.True(first.Used > 0);
        Assert.Equal(first.Used, second.Used);
        Assert.Equal(first.Placement.Entities.Select(e => e.Box), second.Placement.Entities.Select(e => e.Box));
    }

    /// <summary>
    ///     Validates that a layout past a small work budget fails with a located limit error naming the limit.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_SmallBudget_LocatedLimitExceeded()
    {
        // Arrange
        var entities = Enumerable.Range(0, 8).Select(k => Entity("E" + k)).ToList();
        var links = entities.Skip(1).Select((e, k) => new SvekLink("l" + k, entities[k], e)).ToList();
        var location = new PlantUmlLocation("d.puml", 1, 1);
        var diagram = new SvekDiagram { Entities = entities, Links = links, Location = location };
        var options = new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 50 } };

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => SvekLayout.Run(diagram, Context(options)));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.LimitExceeded, location), (ex.Code, ex.Location));
        Assert.Contains("PlantUmlLimits.MaxLayoutWork", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled call stops the layout.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_Canceled_Throws()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = Context() with { CancellationToken = source.Token };
        var (a, b) = (Entity("A"), Entity("B"));

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => SvekLayout.Run(new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b)] }, context));
    }

    /// <summary>
    ///     Validates that an inconsistent diagram is refused before the layout.
    /// </summary>
    [Fact]
    public void PlantUmlSvekLayout_Run_InconsistentDiagram_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => Place(new SvekDiagram { Entities = [Entity("A"), Entity("A")] }));
    }

    /// <summary>
    ///     Validates the located failures of the PlantUML work meter: past the limit, a layout failure, and the
    ///     recognition of PlantUML's own failures.
    /// </summary>
    [Fact]
    public void PlantUmlLayoutWorkMeter_Failures_AreLocatedPlantUmlErrors()
    {
        // Arrange
        var location = new PlantUmlLocation("d.puml", 3, 1);
        var meter = new PlantUmlLayoutWorkMeter(10, location, CancellationToken.None);

        // Act
        meter.Charge(10);
        var limit = Assert.Throws<PlantUmlRenderException>(() => meter.Charge(1));
        var failure = meter.Failure("no route");

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.LimitExceeded, location, 10L), (limit.Code, limit.Location, meter.Used));
        Assert.Contains("PlantUmlLimits.MaxLayoutWork", limit.Message, StringComparison.Ordinal);
        Assert.NotNull(limit.Hint);
        Assert.Equal((PlantUmlDiagnosticCodes.RenderFailed, location), (failure.Code, failure.Location));
        Assert.Contains("no route", failure.Message, StringComparison.Ordinal);
        Assert.True(meter.IsFailure(failure));
        Assert.False(meter.IsFailure(new InvalidOperationException("dagre")));
    }

    /// <summary>
    ///     Gets the links from a node to the nodes below it: an arrow with the multiplicity 1 to Loan and extensions to
    ///     the others.
    /// </summary>
    /// <param name="from">The node above.</param>
    /// <param name="below">The nodes below.</param>
    /// <returns>The links, named after the nodes below.</returns>
    private static List<SvekLink> LinksFrom(SvekEntity from, SvekEntity[] below) =>
        below.Select(e => e.Id == "Loan"
            ? new SvekLink(e.Id, from, e) { TailDecor = LinkDecor.Arrow, TailLabel = "1" }
            : new SvekLink(e.Id, from, e) { TailDecor = LinkDecor.Extends }).ToList();

    /// <summary>
    ///     Gets the bounds of the triangles at the first ends of the extension links to the nodes below (all but Loan).
    /// </summary>
    /// <param name="placement">The placement.</param>
    /// <param name="below">The nodes below.</param>
    /// <returns>The bounds.</returns>
    private static List<SceneRect> TriangleBoundsOf(SvekPlacement placement, SvekEntity[] below) =>
        below.Where(e => e.Id != "Loan").Select(e =>
        {
            var route = LinkOf(placement, e.Id).Route;
            var head = SvekGeometry.Extremity(LinkDecor.Extends, route[0], SvekGeometry.ArrivalAngle(route, atEnd: false))!;
            var points = head.Parts.SelectMany(p => p.Segments).Where(s => s.Kind != SceneSegmentKind.Close).ToList();
            var (x0, y0) = (points.Min(s => s.X), points.Min(s => s.Y));
            return new SceneRect(x0, y0, points.Max(s => s.X) - x0, points.Max(s => s.Y) - y0);
        }).ToList();

    /// <summary>
    ///     Gets the distance from a box to a point (zero inside it).
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns>The distance.</returns>
    private static double DistanceTo(SceneRect box, SvekPoint point)
    {
        var dx = Math.Max(0, Math.Max(box.X - point.X, point.X - box.Right));
        var dy = Math.Max(0, Math.Max(box.Y - point.Y, point.Y - box.Bottom));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Asserts that every two points are at least a distance apart.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="spacing">The least distance.</param>
    private static void AssertApart(List<SvekPoint> points, double spacing)
    {
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                var distance = Math.Sqrt(Math.Pow(points[i].X - points[j].X, 2) + Math.Pow(points[i].Y - points[j].Y, 2));
                Assert.True(distance >= spacing - 1e-6, $"ends {i} and {j}: {points[i]} {points[j]}");
            }
        }
    }

    /// <summary>
    ///     A cluster image of the layout tests: a header, a padding, and a minimum width, drawing nothing.
    /// </summary>
    /// <param name="header">The header height.</param>
    /// <param name="padding">The padding.</param>
    /// <param name="minWidth">The minimum width.</param>
    private sealed class TestClusterImage(double header, double padding, double minWidth) : ISvekClusterImage
    {
        /// <inheritdoc/>
        public double HeaderHeight { get; } = header;

        /// <inheritdoc/>
        public double MinWidth { get; } = minWidth;

        /// <inheritdoc/>
        public double Padding { get; } = padding;

        /// <inheritdoc/>
        public IReadOnlyList<SceneItem> Draw(SceneRect box) => [];
    }
}
