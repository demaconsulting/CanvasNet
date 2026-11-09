using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of the SequenceRenderer class: the markers and strokes of every arrow, the stick figure, the paint order,
///     the paint of notes and activations, the central-connection circles, the sequence numbers and the text shown.
/// </summary>
public class SequenceRendererTests
{
    /// <summary>
    ///     Validates the markers of every arrow at the start and the end of the line, by Mermaid's marker names.
    /// </summary>
    /// <param name="arrow">The arrow.</param>
    /// <param name="start">The marker at the start, or empty.</param>
    /// <param name="end">The marker at the end, or empty.</param>
    [Theory]
    [InlineData("->", "", "")]
    [InlineData("-->", "", "")]
    [InlineData("->>", "", "arrowhead")]
    [InlineData("-->>", "", "arrowhead")]
    [InlineData("<<->>", "arrowhead", "arrowhead")]
    [InlineData("<<-->>", "arrowhead", "arrowhead")]
    [InlineData("-x", "", "crosshead")]
    [InlineData("--x", "", "crosshead")]
    [InlineData("-)", "", "filled-head")]
    [InlineData("--)", "", "filled-head")]
    [InlineData("-|\\", "", "solidTopArrowHead")]
    [InlineData("-|/", "", "solidBottomArrowHead")]
    [InlineData("-\\\\", "", "stickTopArrowHead")]
    [InlineData("-//", "", "stickBottomArrowHead")]
    [InlineData("--|\\", "", "solidTopArrowHead")]
    [InlineData("--//", "", "stickBottomArrowHead")]
    [InlineData("/|-", "solidBottomArrowHead", "")]
    [InlineData("\\|-", "solidTopArrowHead", "")]
    [InlineData("//-", "stickBottomArrowHead", "")]
    [InlineData("\\\\-", "stickTopArrowHead", "")]
    [InlineData("/|--", "solidBottomArrowHead", "")]
    [InlineData("\\\\--", "stickTopArrowHead", "")]
    public void SequenceRenderer_DrawMessage_Arrow_HasMermaidsMarkers(string arrow, string start, string end)
    {
        // Act
        var message = Message(SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    A{arrow}B: x\n")), "i0");

        // Assert
        Assert.Equal(start, message.Children.FirstOrDefault(child => child.Role == "marker-start")?.Id ?? string.Empty);
        Assert.Equal(end, message.Children.FirstOrDefault(child => child.Role == "marker-end")?.Id ?? string.Empty);
    }

    /// <summary>
    ///     Validates the strokes of the message lines: 1.5 wide in the signal color, and dashed 3, 3 when dotted.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_Lines_AreSolidOrDashed()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: solid\n    B-->>A: dotted\n"));
        var solid = Line(Message(items, "i0"));
        var dotted = Line(Message(items, "i1"));

        // Assert
        Assert.Equal(new SceneStroke(new Rgba32(0x33, 0x33, 0x33, 255), 1.5), solid.Stroke);
        Assert.Equal([3.0, 3.0], dotted.Stroke!.Dash);
        Assert.Equal(1.5, dotted.Stroke.Width);
    }

    /// <summary>
    ///     Validates that the arrowhead is placed with its reference point at the end of the line, pointing along it, and
    ///     that a marker scaled by the stroke width (the filled head) grows with it.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_Markers_FollowTheLine()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: right\n    B->>A: left\n    A-)B: async\n"));
        var right = Bounds(Marker(Message(items, "i0"), "marker-end"));
        var left = Bounds(Marker(Message(items, "i1"), "marker-end"));
        var filled = Bounds(Marker(Message(items, "i2"), "marker-end"));

        // Assert: the tip past the end of the line by 10 - 7.9 and its stroke; mirrored for the left arrow; the filled
        // head 9 units wide scaled by 1.5
        var rightEnd = Line(Message(items, "i0")).Segments[^1].X;
        var leftEnd = Line(Message(items, "i1")).Segments[^1].X;
        Assert.InRange(right.Right - rightEnd, 2.1, 3.5);
        Assert.InRange(right.Height, 10, 11.5);
        Assert.InRange(leftEnd - left.X, 2.1, 3.5);
        Assert.Equal(9 * 1.5, filled.Width, 3);
    }

    /// <summary>
    ///     Validates the stick figure of an <c>actor</c>: four limbs and a head of radius 15, 65 high, with the label below.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawActor_Actor_IsAStickFigure()
    {
        // Act
        var figure = (SceneGroup)SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    actor A\n    A->>A: x\n")).Find("actor-top", "A")!;

        // Assert
        Assert.Equal(4, figure.Children.Count(child => child.Role == "actor-line"));
        var head = Bounds(figure.Children.Single(child => child.Role == "actor-head"));
        Assert.Equal(new SceneRect(60, -5, 30, 30), head);
        var shape = Scene.BoundsOf([.. figure.Children.OfType<ScenePath>()])!.Value;
        Assert.Equal(65, shape.Height, 6);
        Assert.Single(figure.Children.OfType<SceneText>());
    }

    /// <summary>
    ///     Validates Mermaid's paint order: the mirrored participants, then each participant's lifeline and box, lowered
    ///     to the back in reverse order, then the notes, then the messages.
    /// </summary>
    [Fact]
    public void SequenceRenderer_Items_PaintOrder_IsMermaids()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: x\n    Note over A: n\n"));

        // Assert
        Assert.Equal(
            ["actor-bottom:B", "actor-bottom:A", "lifeline:B", "actor-top:B", "lifeline:A", "actor-top:A", "note:i1", "message:i0"],
            items.Order);
    }

    /// <summary>
    ///     Validates the paint of notes and activations: the theme's note and activation colors over Mermaid's attributes.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawNoteAndActivation_UseTheThemeColors()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>+B: x\n    Note over A: n\n    B-->>-A: y\n"));
        var note = (ScenePath)((SceneGroup)items.Find("note", "i2")!).Children[0];
        var activation = (ScenePath)items.WithRole("activation").Single();

        // Assert
        Assert.Equal(new Rgba32(0xFF, 0xF5, 0xAD, 255), note.Fill);
        Assert.Equal(new Rgba32(0xAA, 0xAA, 0x33, 255), note.Stroke!.Color);
        Assert.Equal("activation0", activation.Id);
        Assert.Equal(new Rgba32(0xF4, 0xF4, 0xF4, 255), activation.Fill);
        Assert.Equal(new Rgba32(0x66, 0x66, 0x66, 255), activation.Stroke!.Color);
    }

    /// <summary>
    ///     Validates the central-connection circles: radius 5 on the lifelines they name, filled with the text color.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_CentralConnection_DrawsCircles()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A()->>()B: both\n"));
        var circles = ((SceneGroup)items.Find("connection-circles", "i0")!).Children;

        // Assert
        Assert.Equal([new SceneRect(70, 104, 10, 10), new SceneRect(270, 104, 10, 10)], circles.Select(Bounds));
        Assert.All(circles, circle => Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), ((ScenePath)circle).Fill));
    }

    /// <summary>
    ///     Validates a sequence number: a circle of radius 12 (the marker scaled by the 2-pixel line width) at the
    ///     sender's side, and the number in white, smaller for longer numbers.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_SequenceNumber_IsACircleWithTheNumber()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    autonumber 1000\n    A->>B: x\n"));
        var number = (SceneGroup)items.Find("sequence-number", "i1")!;
        var text = (SceneText)number.Children[1];

        // Assert
        Assert.Equal(new SceneRect(63, 97, 24, 24), Bounds(number.Children[0]));
        Assert.Equal(("1000", 9.0), (text.Runs[0].Text, text.Runs[0].Font.Size));
        Assert.Equal(new Rgba32(255, 255, 255, 255), text.Runs[0].Color);
    }

    /// <summary>
    ///     Validates the frame of a block: four sides dashed 2, 2 and a separator dashed 3, 3 per section, all 2 pixels
    ///     wide in the label box border color, the label box with its cut corner, and the keyword, title and section title.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawLoop_Frame_HasDashedSidesSeparatorsAndLabel()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    alt Yes\n        A->>B: x\n    else No\n        B->>A: y\n    end\n"));
        var frame = (SceneGroup)items.Find("frame", "i4")!;
        var lines = frame.Children.Where(child => child.Role == "frame-line").Cast<ScenePath>().ToList();
        var box = (ScenePath)frame.Children.Single(child => child.Role == "frame-label-box");
        var texts = frame.Children.OfType<SceneText>().Select(text => string.Concat(text.Runs.Select(run => run.Text)));

        // Assert
        Assert.Equal(5, lines.Count);
        Assert.All(lines.Take(4), line => Assert.Equal([2.0, 2.0], line.Stroke!.Dash));
        Assert.Equal([3.0, 3.0], lines[4].Stroke!.Dash);
        Assert.All(lines, line => Assert.Equal((new Rgba32(0x93, 0x70, 0xDB, 255), 2.0), (line.Stroke!.Color, line.Stroke.Width)));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), box.Fill);
        Assert.Equal(6, box.Segments.Count);
        Assert.Equal(["alt", "[Yes]", "[No]"], texts);
    }

    /// <summary>
    ///     Validates that a block with nothing inside fails at its start: Mermaid gives it no size and lays the rest out
    ///     at coordinates that are not numbers.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawLoop_EmptyBlock_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => SceneQuery.Layout("sequenceDiagram\n    A->>B: x\n    loop Never\n    end\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(3, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that the background of a <c>rect</c> block is behind everything, in its color, without a stroke or a
    ///     shadow, and that a color the browser does not accept takes the inherited root fill.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawBackgroundRect_IsBehindEverythingWithoutStroke()
    {
        // Act
        var colored = SceneQuery.Layout("sequenceDiagram\n    rect rgb(1, 2, 3)\n        A->>B: x\n    end\n");
        var invalid = SceneQuery.Layout("sequenceDiagram\n    rect nonsense\n        A->>B: x\n    end\n");
        var rect = (ScenePath)colored.Items[0];

        // Assert
        Assert.Equal("rect", rect.Role);
        Assert.Equal(new Rgba32(1, 2, 3, 255), rect.Fill);
        Assert.Null(rect.Stroke);
        Assert.Null(rect.Shadow);
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), ((ScenePath)invalid.Items[0]).Fill);
    }

    /// <summary>
    ///     Validates that a box is drawn behind everything with the theme's node border and its title, and that its color
    ///     may be a system color, which takes the browser's value.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawBox_IsBehindWithBorderTitleAndColor()
    {
        // Act
        var scene = SceneQuery.Layout("sequenceDiagram\n    box ButtonFace Team\n        participant A\n    end\n    A->>B: x\n");
        var box = (SceneGroup)scene.Items[0];
        var rect = (ScenePath)box.Children[0];
        var title = (SceneText)box.Children[1];

        // Assert
        Assert.Equal("box", box.Role);
        Assert.Equal(new Rgba32(240, 240, 240, 255), rect.Fill);
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), rect.Stroke!.Color);
        Assert.Equal((-20.0, -5.0), (Bounds(rect).X, Bounds(rect).Y));
        Assert.Equal("Team", title.Runs[0].Text);
    }

    /// <summary>
    ///     Validates where the lifeline of each participant type starts (below the box, the figure, or the database's
    ///     label).
    /// </summary>
    /// <param name="type">The participant type.</param>
    /// <param name="top">The expected top of the lifeline.</param>
    [Theory]
    [InlineData("participant", 65)]
    [InlineData("actor", 80)]
    [InlineData("boundary", 80)]
    [InlineData("control", 75)]
    [InlineData("entity", 75)]
    [InlineData("database", 75)]
    [InlineData("collections", 65)]
    [InlineData("queue", 65)]
    public void SequenceRenderer_DrawActor_Type_StartsTheLifelineBelowIt(string type, double top)
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    participant A@{{ \"type\": \"{type}\" }}\n    A->>B: x\n"));
        var lifeline = (ScenePath)items.Find("lifeline", "A")!;

        // Assert
        Assert.Equal(top, lifeline.Segments[0].Y);
        Assert.NotNull(items.Find("actor-top", "A"));
        Assert.NotNull(items.Find("actor-bottom", "A"));
    }

    /// <summary>
    ///     Validates that a participant of an unknown type draws nothing, as in Mermaid.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawActor_UnknownType_DrawsNothing()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    participant A@{ \"type\": \"robot\" }\n    A->>B: x\n"));

        // Assert
        Assert.Null(items.Find("actor-top", "A"));
        Assert.Null(items.Find("lifeline", "A"));
        Assert.NotNull(items.Find("actor-top", "B"));
    }

    /// <summary>
    ///     Validates that a control carries the <c>filled-head-control</c> arrowhead on its circle, in the participant
    ///     colors.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawActor_Control_HasTheFilledHeadControl()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    participant A@{ \"type\": \"control\" }\n    A->>B: x\n"));
        var marker = (ScenePath)((SceneGroup)items.Find("actor-top", "A")!).Children.Single(child => child.Role == "actor-marker");

        // Assert
        Assert.Equal("filled-head-control", marker.Id);
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), marker.Fill);
        Assert.Equal((new Rgba32(0x93, 0x70, 0xDB, 255), 2.4), (marker.Stroke!.Color, marker.Stroke.Width));
    }

    /// <summary>
    ///     Validates that a <c>properties</c> class replaces the <c>actor</c> class of a participant's box: the box falls
    ///     back to its attributes (the note fill), unless the class has a rule of its own.
    /// </summary>
    /// <param name="value">The class as JSON.</param>
    /// <param name="r">The expected red of the fill.</param>
    /// <param name="g">The expected green of the fill.</param>
    /// <param name="b">The expected blue of the fill.</param>
    [Theory]
    [InlineData("\"internal\"", 0xED, 0xF2, 0xAE)]
    [InlineData("5", 0xED, 0xF2, 0xAE)]
    [InlineData("\"note\"", 0xFF, 0xF5, 0xAD)]
    [InlineData("\"\"", 0xEC, 0xEC, 0xFF)]
    public void SequenceRenderer_DrawActor_PropertiesClass_ReplacesTheActorPaint(string value, byte r, byte g, byte b)
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    participant A\n    properties A: {{\"class\": {value}}}\n    A->>B: x\n"));
        var rect = (ScenePath)((SceneGroup)items.Find("actor-top", "A")!).Children[0];

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), rect.Fill);
    }

    /// <summary>
    ///     Validates that a menu is drawn hidden (an empty group) without <c>forceMenus</c>, and that its size still grows
    ///     the view box: the panel below the box's height, 20 pixels and 30 per link.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawPopup_Hidden_IsEmptyAndGrowsTheViewBox()
    {
        // Act
        var scene = SceneQuery.Layout("sequenceDiagram\n    participant A\n    links A: {\"a\": \"u\", \"b\": \"u\", \"c\": \"u\", \"d\": \"u\", \"e\": \"u\", \"f\": \"u\"}\n");
        var menu = (SceneGroup)SceneQuery.SequenceItems(scene).Find("menu", "actor0_popup")!;

        // Assert
        Assert.Empty(menu.Children);
        Assert.Equal(65 + 20 + (6 * 30) + 10 + 1, scene.ViewBox.Height);
    }

    /// <summary>
    ///     Validates that <c>forceMenus</c> draws the menu: the panel in the participant fill with its drop shadow, and one
    ///     line per link.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawPopup_Forced_DrawsThePanelAndLinks()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    forceMenus: true\n---\nsequenceDiagram\n    participant A\n    link A: Home @ https://example.com/\n    link A: Help @ https://example.com/help\n    A->>B: x\n"));
        var menu = (SceneGroup)items.Find("menu", "actor0_popup")!;
        var panel = (ScenePath)menu.Children[0];

        // Assert
        Assert.Equal(new SceneRect(0, 65, 150, 80), Bounds(panel));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), panel.Fill);
        Assert.NotNull(panel.Shadow);
        Assert.Equal(["Home", "Help"], menu.Children.OfType<SceneText>().Select(text => text.Runs[0].Text));
        Assert.All(menu.Children.OfType<SceneText>(), text => Assert.Equal(10, text.X));
    }

    /// <summary>
    ///     Validates that the lines of one menu link label share a baseline when the participant font size has a unit, as
    ///     Mermaid 11.17.2 draws them: its menu <c>byTspan</c> spaces the lines by the raw <c>actorFontSize</c>, so
    ///     <c>18px</c> (used because the top-level <c>fontSize</c> of 0 does not replace it) gives a <c>dy</c> of NaN
    ///     that the browser rejects, while the next link stays 30 pixels below. The boxes are the reference renderer's.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawPopup_LabelSizeWithUnit_LinesShareABaseline()
    {
        // Act: a two-line link label and a second link, with the participant font size written with a unit
        var (panel, texts) = MenuTexts("\"fontSize\": 0, \"sequence\": {\"forceMenus\": true, \"actorFontSize\": \"18px\"}");

        // Assert: the panel and Mermaid's text boxes, the two lines of the first label on one baseline
        Assert.Equal(new SceneRect(0, 65, 150, 80), panel);
        Assert.Equal(["First", "second line", "Other"], texts.Select(text => text.Runs[0].Text));
        AssertBox(new SceneRect(10, 85, 35.988, 20), texts[0].Box);
        AssertBox(new SceneRect(10, 85, 91.078, 20), texts[1].Box);
        AssertBox(new SceneRect(10, 115, 45.031, 20), texts[2].Box);
        Assert.Equal(texts[0].Baseline, texts[1].Baseline);
        Assert.Equal(30, texts[2].Baseline - texts[0].Baseline, 6);
    }

    /// <summary>
    ///     Validates that the lines of one menu link label are spaced by, and drawn at, a top-level <c>fontSize</c>
    ///     written as a number, as Mermaid 11.17.2 draws them: setConf copies 18 into <c>actorFontSize</c>, the menu
    ///     <c>byTspan</c> writes it without a unit into the inline style, which the browser accepts on SVG text as 18
    ///     pixels (also with a <c>themeVariables.fontSize</c> of <c>10px</c> on the diagram root), and spaces the lines
    ///     18 pixels apart, with the next link 30 pixels below. The boxes are the reference renderer's.
    /// </summary>
    /// <param name="themeVariables">The <c>themeVariables</c> setting, or an empty text for none.</param>
    [Theory]
    [InlineData("")]
    [InlineData("\"themeVariables\": {\"fontSize\": \"10px\"}, ")]
    public void SequenceRenderer_DrawPopup_NumericSize_LinesSpacedBySize(string themeVariables)
    {
        // Act: the same menu with the top-level font size as a number
        var (panel, texts) = MenuTexts($"\"fontSize\": 18, {themeVariables}\"sequence\": {{\"forceMenus\": true}}");

        // Assert: the panel, the 18-pixel font and Mermaid's text boxes, the lines 18 pixels apart
        AssertMenuAt18Pixels(panel, texts);
    }

    /// <summary>
    ///     Validates that a menu link label is drawn at a participant font size given as a number or as a string of
    ///     digits, without a unit, with the top-level <c>fontSize</c> of 0 (which keeps the section's size), as Mermaid
    ///     11.17.2 draws it: the browser accepts the bare size in the inline style of the SVG text as 18 pixels, and
    ///     the lines are 18 pixels apart. The boxes are the reference renderer's, the same as for a top-level 18.
    /// </summary>
    /// <param name="actorFontSize">The <c>actorFontSize</c> as written in the JSON.</param>
    [Theory]
    [InlineData("18")]
    [InlineData("\"18\"")]
    public void SequenceRenderer_DrawPopup_ActorSizeWithoutUnit_DrawnAtThatSize(string actorFontSize)
    {
        // Act: the menu with the participant font size without a unit
        var (panel, texts) = MenuTexts($"\"fontSize\": 0, \"sequence\": {{\"forceMenus\": true, \"actorFontSize\": {actorFontSize}}}");

        // Assert: the panel, the 18-pixel font and Mermaid's text boxes, the lines 18 pixels apart
        AssertMenuAt18Pixels(panel, texts);
    }

    /// <summary>
    ///     Validates that a fractional participant font size written as a string without a unit (<c>15.5</c>) draws the
    ///     menu link label at that size, as Mermaid 11.17.2 draws it: the browser accepts the bare size on SVG text as
    ///     15.5 pixels, and the lines are spaced by the raw size. The boxes are the reference renderer's.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawPopup_FractionalActorSize_DrawnAtThatSize()
    {
        // Act: the menu with a fractional participant font size as a string without a unit
        var (panel, texts) = MenuTexts("\"fontSize\": 0, \"sequence\": {\"forceMenus\": true, \"actorFontSize\": \"15.5\"}");

        // Assert: the panel, the 15.5-pixel font and Mermaid's text boxes
        Assert.Equal(new SceneRect(0, 65, 150, 80), panel);
        Assert.Equal(["First", "second line", "Other"], texts.Select(text => text.Runs[0].Text));
        Assert.All(texts, text => Assert.Equal(15.5, text.Runs[0].Font.Size, 6));
        AssertBox(new SceneRect(10, 78.75, 30.823, 17), texts[0].Box);
        AssertBox(new SceneRect(10, 94.25, 78.795, 17), texts[1].Box);
        AssertBox(new SceneRect(10, 116.5, 39.604, 17), texts[2].Box);
        Assert.Equal(15.5, texts[1].Baseline - texts[0].Baseline, 6);
    }

    /// <summary>
    ///     Validates that fractional font sizes written as strings without a unit draw the participant, message and note
    ///     texts at those sizes, as Mermaid 11.17.2 draws them: <c>parseFontSize</c> keeps <c>15.5</c>, <c>18.5</c> and
    ///     <c>17.5</c> as the CSS sizes (they are not their integer alone), and the browser accepts a bare number in the
    ///     inline style of SVG text as pixels. The boxes are the reference renderer's.
    /// </summary>
    [Fact]
    public void SequenceRenderer_Layout_FractionalSizesWithoutUnit_TextsDrawnAtThoseSizes()
    {
        // Arrange: fractional sizes for the participants, the messages and the notes
        const string Diagram =
            "%%{init: {\"fontSize\": 0, \"sequence\": {\"actorFontSize\": \"15.5\", \"messageFontSize\": \"18.5\", " +
            "\"noteFontSize\": \"17.5\"}}}%%\nsequenceDiagram\n    participant A as Alice\n    participant B as Bob\n" +
            "    A->>B: hello there\n    Note over A,B: a note\n";

        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(Diagram));
        var actor = ((SceneGroup)items.Find("actor-top", "A")!).Children.OfType<SceneText>().Single();
        var message = items.WithRole("message-text").OfType<SceneText>().Single();
        var note = items.WithRole("note-text").OfType<SceneText>().Single();

        // Assert: the sizes and Mermaid's text boxes
        Assert.Equal([15.5, 18.5, 17.5], new[] { actor, message, note }.Select(text => text.Runs[0].Font.Size));
        AssertBox(new SceneRect(57.195, 24, 34.976, 17), actor.Box);
        AssertBox(new SceneRect(130.797, 86.391, 86.406, 21), message.Box);
        AssertBox(new SceneRect(150.672, 138.125, 48.922, 20), note.Box);
    }

    /// <summary>
    ///     Validates that links on a participant type without a box fail at the participant, as Mermaid fails.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawPopup_ActorWithLinks_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => SceneQuery.Layout("sequenceDiagram\n    actor A\n    link A: Home @ https://example.com/\n    A->>B: x\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(2, exception.Location.Line);
    }

    /// <summary>
    ///     Validates the <c>old</c> text placement: one text per label, as written, below the middle of the box, in the
    ///     root font, filled and stroked by the <c>.actor</c> rule.
    /// </summary>
    [Fact]
    public void SequenceRenderer_TextPlacementOld_DrawsStrokedLabels()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    textPlacement: old\n---\nsequenceDiagram\n    participant A as One<br/>Two\n    A->>B: x\n"));
        var label = ((SceneGroup)items.Find("actor-top", "A")!).Children.OfType<SceneText>().Single();

        // Assert
        Assert.Equal("One<br/>Two", label.Runs[0].Text);
        Assert.Equal((75.0, 37.5, SceneTextAnchor.Middle), (label.X, label.Baseline, label.Anchor));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), label.Runs[0].Color);
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), label.Stroke!.Color);
    }

    /// <summary>
    ///     Validates the text shown for a text of the model: Mermaid's entity placeholders become their characters.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DisplayText_DecodesEntityPlaceholders()
    {
        // Act / Assert
        Assert.Equal("I \u2665 it", SequenceRenderer.DisplayText("I \ufb02\u00b0\u00b09829\u00b6\u00df it"));
        Assert.Equal("a & b", SequenceRenderer.DisplayText("a \ufb02\u00b0amp\u00b6\u00df b"));
        Assert.Equal("plain", SequenceRenderer.DisplayText("plain"));
    }

    /// <summary>
    ///     Validates the geometry helpers: a rounded rectangle's box, and a circle's box.
    /// </summary>
    [Fact]
    public void SequenceRenderer_Segments_HaveTheirBoxes()
    {
        // Act
        var rect = new ScenePath(SequenceRenderer.RectSegments(1, 2, 30, 40, 3, 3)) { Role = "r" };
        var circle = new ScenePath(SequenceRenderer.CircleSegments(10, 20, 5)) { Role = "c" };

        // Assert
        Assert.Equal(new SceneRect(1, 2, 30, 40), Bounds(rect));
        Assert.Equal(new SceneRect(5, 15, 10, 10), Bounds(circle));
    }

    /// <summary>
    ///     Gets the group of a message.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The message id.</param>
    /// <returns>The group.</returns>
    private static SceneGroup Message(SceneQuery.SequenceScene items, string id) => (SceneGroup)items.Find("message", id)!;

    /// <summary>
    ///     Gets the line of a message group.
    /// </summary>
    /// <param name="message">The group.</param>
    /// <returns>The line.</returns>
    private static ScenePath Line(SceneGroup message) => (ScenePath)message.Children.First(child => child.Role == "message-line");

    /// <summary>
    ///     Gets a marker of a message group.
    /// </summary>
    /// <param name="message">The group.</param>
    /// <param name="role">The marker's role.</param>
    /// <returns>The marker.</returns>
    private static ScenePath Marker(SceneGroup message, string role) => (ScenePath)message.Children.First(child => child.Role == role);

    /// <summary>
    ///     Gets the bounds of an item, rounded to thousandths.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect Bounds(SceneItem item)
    {
        var box = Scene.BoundsOf([item])!.Value;
        return new SceneRect(Math.Round(box.X, 3), Math.Round(box.Y, 3), Math.Round(box.Width, 3), Math.Round(box.Height, 3));
    }

    /// <summary>
    ///     Lays out a participant with a forced menu of a two-line link label and a one-line link label.
    /// </summary>
    /// <param name="init">The members of the init directive.</param>
    /// <returns>The bounds of the menu panel and the menu texts.</returns>
    private static (SceneRect Panel, List<SceneText> Texts) MenuTexts(string init)
    {
        var diagram =
            $"%%{{init: {{{init}}}}}%%\nsequenceDiagram\nparticipant A\n" +
            "link A: First<br>second line @ https://a.example\nlink A: Other @ https://b.example\nA->>A: hi\n";
        var menu = (SceneGroup)SceneQuery.SequenceItems(SceneQuery.Layout(diagram)).Find("menu", "actor0_popup")!;
        return (Bounds(menu.Children.OfType<ScenePath>().Single(child => child.Role == "menu-panel")), [.. menu.Children.OfType<SceneText>()]);
    }

    /// <summary>
    ///     Asserts the menu of <see cref="MenuTexts"/> at an 18-pixel participant font as the reference renderer draws it:
    ///     the panel, the font size, the text boxes, and the lines of the first label 18 pixels apart.
    /// </summary>
    /// <param name="panel">The panel.</param>
    /// <param name="texts">The link texts.</param>
    private static void AssertMenuAt18Pixels(SceneRect panel, List<SceneText> texts)
    {
        Assert.Equal(new SceneRect(0, 65, 150, 80), panel);
        Assert.Equal(["First", "second line", "Other"], texts.Select(text => text.Runs[0].Text));
        Assert.All(texts, text => Assert.Equal(18, text.Runs[0].Font.Size, 6));
        AssertBox(new SceneRect(10, 76, 35.988, 20), texts[0].Box);
        AssertBox(new SceneRect(10, 94, 91.078, 20), texts[1].Box);
        AssertBox(new SceneRect(10, 115, 45.031, 20), texts[2].Box);
        Assert.Equal(18, texts[1].Baseline - texts[0].Baseline, 6);
    }

    /// <summary>
    ///     Asserts that a box matches an expected box within the golden text tolerance (0.02 pixel).
    /// </summary>
    /// <param name="expected">The expected box.</param>
    /// <param name="box">The box.</param>
    private static void AssertBox(SceneRect expected, SceneRect box) =>
        Assert.True(
            Math.Abs(expected.X - box.X) <= 0.02 && Math.Abs(expected.Y - box.Y) <= 0.02 &&
            Math.Abs(expected.Width - box.Width) <= 0.02 && Math.Abs(expected.Height - box.Height) <= 0.02,
            $"expected {expected}, got {box}");
}
