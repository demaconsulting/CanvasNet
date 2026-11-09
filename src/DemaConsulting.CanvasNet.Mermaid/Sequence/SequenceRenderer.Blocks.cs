// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     Blocks, backgrounds, boxes and menus: Mermaid's <c>drawLoop</c> (the frame of a <c>loop</c>, <c>alt</c>,
///     <c>opt</c>, <c>par</c>, <c>critical</c> or <c>break</c> block with its label box, title and section titles),
///     <c>drawBackgroundRect</c> (a <c>rect</c> block), <c>drawBox</c> (a participant box with its title), and
///     <c>drawPopup</c> (a participant's menu of links).
/// </content>
internal sealed partial class SequenceRenderer
{
    /// <summary>
    ///     The colors a browser gives the CSS color keywords and system colors in a fill attribute (headless Chrome of the
    ///     reference, light color scheme), by case-insensitive name; <c>inherit</c>, <c>unset</c> and the reverts take the
    ///     inherited fill instead, and are not listed.
    /// </summary>
    private static readonly FrozenDictionary<string, Rgba32> KeywordColors = new Dictionary<string, Rgba32>(StringComparer.OrdinalIgnoreCase)
    {
        ["currentcolor"] = new(0, 0, 0, 255),
        ["initial"] = new(0, 0, 0, 255),
        ["AccentColor"] = new(0, 117, 255, 255),
        ["AccentColorText"] = new(255, 255, 255, 255),
        ["ActiveText"] = new(0, 102, 204, 255),
        ["ButtonBorder"] = new(0, 0, 0, 255),
        ["ButtonFace"] = new(240, 240, 240, 255),
        ["ButtonText"] = new(0, 0, 0, 255),
        ["Canvas"] = new(255, 255, 255, 255),
        ["CanvasText"] = new(0, 0, 0, 255),
        ["Field"] = new(255, 255, 255, 255),
        ["FieldText"] = new(0, 0, 0, 255),
        ["GrayText"] = new(109, 109, 109, 255),
        ["Highlight"] = new(0, 120, 215, 255),
        ["HighlightText"] = new(255, 255, 255, 255),
        ["LinkText"] = new(0, 102, 204, 255),
        ["Mark"] = new(255, 255, 0, 255),
        ["MarkText"] = new(0, 0, 0, 255),
        ["SelectedItem"] = new(25, 103, 210, 255),
        ["SelectedItemText"] = new(255, 255, 255, 255),
        ["VisitedText"] = new(0, 102, 204, 255),
        ["ActiveBorder"] = new(0, 0, 0, 255),
        ["ActiveCaption"] = new(255, 255, 255, 255),
        ["AppWorkspace"] = new(255, 255, 255, 255),
        ["Background"] = new(255, 255, 255, 255),
        ["ButtonHighlight"] = new(240, 240, 240, 255),
        ["ButtonShadow"] = new(240, 240, 240, 255),
        ["CaptionText"] = new(0, 0, 0, 255),
        ["InactiveBorder"] = new(0, 0, 0, 255),
        ["InactiveCaption"] = new(255, 255, 255, 255),
        ["InactiveCaptionText"] = new(128, 128, 128, 255),
        ["InfoBackground"] = new(255, 255, 255, 255),
        ["InfoText"] = new(0, 0, 0, 255),
        ["Menu"] = new(255, 255, 255, 255),
        ["MenuText"] = new(0, 0, 0, 255),
        ["Scrollbar"] = new(255, 255, 255, 255),
        ["ThreeDDarkShadow"] = new(0, 0, 0, 255),
        ["ThreeDFace"] = new(240, 240, 240, 255),
        ["ThreeDHighlight"] = new(0, 0, 0, 255),
        ["ThreeDLightShadow"] = new(0, 0, 0, 255),
        ["ThreeDShadow"] = new(0, 0, 0, 255),
        ["Window"] = new(255, 255, 255, 255),
        ["WindowFrame"] = new(0, 0, 0, 255),
        ["WindowText"] = new(0, 0, 0, 255),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Draws the frame of a block (Mermaid's <c>drawLoop</c>): the four dashed sides, a dashed separator above each
    ///     section, the label box with the block's keyword, the title, and the section titles.
    /// </summary>
    /// <param name="loop">The closed block, whose extent is set.</param>
    /// <param name="labelText">The keyword in the label box (<c>loop</c>, <c>alt</c>, <c>opt</c>, <c>par</c>, <c>critical</c> or <c>break</c>).</param>
    /// <param name="msg">The message that ends the block (its index is the frame's id).</param>
    public void DrawLoop(SequenceLoopModel loop, string labelText, SequenceMessage msg)
    {
        var id = "i" + msg.Id;
        var location = loop.Location;
        var startx = loop.StartX ?? double.NaN;
        var starty = loop.StartY ?? double.NaN;
        var stopx = loop.StopX ?? double.NaN;
        var stopy = loop.StopY ?? double.NaN;
        var children = new List<SceneItem>();

        // The sides (the .loopLine rule: 2 pixels, dashed 2 2) and the section separators (an inline 3 3 dash)
        var paint = _styles.LoopLine;
        ScenePath Line(double x1, double y1, double x2, double y2, IReadOnlyList<double> dash) =>
            new(LineSegments(x1, y1, x2, y2))
            {
                Role = "frame-line",
                Location = location,
                Stroke = Stroke(paint with { Dash = dash }),
            };
        children.Add(Line(startx, starty, stopx, starty, paint.Dash));
        children.Add(Line(stopx, starty, stopx, stopy, paint.Dash));
        children.Add(Line(startx, stopy, stopx, stopy, paint.Dash));
        children.Add(Line(startx, starty, startx, stopy, paint.Dash));
        children.AddRange((loop.Sections ?? []).Select(section => Line(startx, section.Y, stopx, section.Y, [3, 3])));

        // The label box: a rectangle with its lower right corner cut, and the keyword centered in it
        var font = _conf.MessageFont;
        var width = Math.Max(_conf.LabelBoxWidth, 50);
        var height = _conf.LabelBoxHeight is var configured && configured != 0 && !double.IsNaN(configured) ? configured : 20;
        const double Cut = 7;
        var labelBox = _styles.LabelBox;
        children.Add(new ScenePath(
        [
            SceneSegment.Move(startx, starty),
            SceneSegment.Line(startx + width, starty),
            SceneSegment.Line(startx + width, starty + height - Cut),
            SceneSegment.Line(startx + width - (Cut * 1.2), starty + height),
            SceneSegment.Line(startx, starty + height),
            SceneSegment.Close(),
        ])
        {
            Role = "frame-label-box",
            Location = location,
            Fill = labelBox.Fill,
            Stroke = Stroke(labelBox),
        });
        children.AddRange(DrawText(
            new SequenceTextData
            {
                Text = labelText,
                X = startx,
                Y = starty + (height / 2),
                Width = width,
                Anchor = "middle",
                Valign = "middle",
                TextMargin = _conf.BoxTextMargin,
                Font = font,
                Color = _styles.TextColor(null, ".labelText"),
            },
            "frame-label",
            id,
            location).Lines);

        // The title, centered in the block right of the label box (drawText leaves an anchor without a width in place)
        children.AddRange(DrawText(
            new SequenceTextData
            {
                Text = loop.Title ?? string.Empty,
                X = startx + (_conf.LabelBoxWidth / 2) + ((stopx - startx) / 2),
                Y = starty + _conf.BoxMargin + _conf.BoxTextMargin,
                Anchor = "middle",
                Valign = "middle",
                TextMargin = _conf.BoxTextMargin,
                Font = font,
                Color = _styles.TextColor(null, ".loopText", ".loopText > tspan"),
            },
            "frame-title",
            id,
            location).Lines);

        // The section titles, centered in the block below their separators
        for (var i = 0; i < (loop.SectionTitles?.Count ?? 0); i++)
        {
            var title = loop.SectionTitles![i];
            if (title.Message.Length == 0)
            {
                continue;
            }

            children.AddRange(DrawText(
                new SequenceTextData
                {
                    Text = title.Message,
                    X = startx + ((stopx - startx) / 2),
                    Y = loop.Sections![i].Y + _conf.BoxMargin + _conf.BoxTextMargin,
                    Anchor = "middle",
                    Valign = "middle",
                    TextMargin = _conf.BoxTextMargin,
                    Font = font,
                    Color = _styles.TextColor(null, ".sectionTitle"),
                },
                "frame-section-title",
                id,
                title.Source.Location).Lines);
        }

        Append().Items.Add(new SceneGroup(0, 0, children) { Role = "frame", Id = id, Location = location });
    }

    /// <summary>
    ///     Draws the background of a <c>rect</c> block (Mermaid's <c>drawBackgroundRect</c>): a rectangle in the block's
    ///     color behind everything drawn so far, without a stroke or a shadow (it is not in a group, so the
    ///     <c>g rect.rect</c> rule does not match it).
    /// </summary>
    /// <param name="loop">The closed <c>rect</c> block.</param>
    public void DrawBackgroundRect(SequenceLoopModel loop)
    {
        var startx = loop.StartX ?? double.NaN;
        var starty = loop.StartY ?? double.NaN;
        Lower().Items.Add(new ScenePath(RectSegments(startx, starty, (loop.StopX ?? double.NaN) - startx, (loop.StopY ?? double.NaN) - starty))
        {
            Role = "rect",
            Location = loop.Location,
            Fill = FillAttribute(loop.Fill ?? string.Empty),
        });
    }

    /// <summary>
    ///     Draws a box around participants (Mermaid's <c>drawBox</c> after the <c>draw</c> code that pads it): a rectangle
    ///     in the box's color with the theme's node border and drop shadow, and its title centered at its top, behind
    ///     everything drawn so far.
    /// </summary>
    /// <param name="box">The laid-out box, whose height is set.</param>
    public void DrawBox(SequenceBoxLayout box)
    {
        // The rectangle: padded by twice the box margin at the sides, half a margin at the top, 1.5 at the bottom
        var padding = _conf.BoxMargin * 2;
        var startx = box.X - padding;
        var starty = box.Y - (padding * 0.25);
        var location = box.Box.Location;
        var fill = FillAttribute(box.Box.Fill);
        var paint = _styles.Box(new ShapePaint(fill, new Rgba32(0, 0, 0, 128), 1));
        var shadow = _styles.BoxShadow;
        var children = new List<SceneItem>
        {
            new ScenePath(RectSegments(startx, starty, box.Width + (2 * padding), box.Height + (padding * 0.75)))
            {
                Role = "box-rect",
                Location = location,
                Fill = paint.Fill,
                Stroke = Stroke(paint),
                Shadow = shadow is { } s ? new SceneShadow(s.OffsetX, s.OffsetY, s.Blur, s.Color) : null,
            },
        };

        // The title, in the participants' font
        if (!string.IsNullOrEmpty(box.Name))
        {
            children.AddRange(ActorText(
                box.Name,
                box.X,
                box.Y + _conf.BoxTextMargin + (box.TextMaxHeight / 2),
                box.Width,
                0,
                new ActorTextStyle(_styles.TextColor(null) ?? Black, null, "box-title"),
                location));
        }

        Lower().Items.Add(new SceneGroup(0, 0, children) { Role = "box", Location = location });
    }

    /// <summary>
    ///     Draws a participant's menu of links (Mermaid's <c>drawPopup</c>): shown only when <c>forceMenus</c> is set
    ///     (else an empty, hidden group), a panel below the participant's box with one line per link; the size counts in
    ///     the diagram either way.
    /// </summary>
    /// <param name="actor">The participant, which has links.</param>
    /// <param name="minMenuWidth">The width the link labels need.</param>
    /// <returns>The height (the panel's bottom) and the width of the menu.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown for a participant whose drawing has no box (<c>actor</c>, <c>boundary</c>, <c>control</c>,
    ///     <c>entity</c>, or a type that is not drawn): Mermaid fails to draw its menu.
    /// </exception>
    public (double Height, double Width) DrawPopup(SequenceActorLayout actor, double minMenuWidth)
    {
        // Mermaid reads the box of the participant's drawing, which these types do not keep
        var rectData = actor.RectData ?? throw new MermaidRenderException(
            MermaidDiagnosticCodes.RenderFailed,
            $"participant '{actor.Actor.Name}' of type '{actor.Actor.Type}' has links, and Mermaid fails to draw its menu (the type has no box)",
            actor.Actor.Location,
            "remove the links of the participant, or give it a type with a box: participant, collections, queue or database");

        // The panel's place: below the box's height (not its y), at least as wide as the box
        var location = actor.Actor.Location;
        var menuWidth = rectData.Width > minMenuWidth ? rectData.Width : minMenuWidth;
        var links = actor.Actor.Links.Keys.ToList();
        var linkY = 20.0 + (30.0 * links.Count);
        var id = "actor" + actor.ActorCnt.ToString(CultureInfo.InvariantCulture) + "_popup";
        if (!_conf.ForceMenus)
        {
            Append().Items.Add(new SceneGroup(0, 0, []) { Role = "menu", Id = id, Location = location });
            return (rectData.Height + linkY, menuWidth);
        }

        // The panel: the box's attributes under its classes, then the .actorPopupMenuPanel rule with its drop shadow
        var selectors = ClassSelectors(rectData.Classes).Append(".actorPopupMenuPanel").ToArray();
        var paint = _styles.Shape(new ShapePaint(rectData.Fill, NoteRectStroke, 1), selectors);
        var shadow = _styles.MenuPanelShadow;
        var children = new List<SceneItem>
        {
            new ScenePath(RectSegments(rectData.X, rectData.Height, menuWidth, linkY, rectData.Radius, rectData.Radius))
            {
                Role = "menu-panel",
                Location = location,
                Fill = paint.Fill,
                Stroke = Stroke(paint),
                Shadow = shadow is { } s ? new SceneShadow(s.OffsetX, s.OffsetY, s.Blur, s.Color) : null,
            },
        };

        // One line per link, 30 pixels apart
        var y = 20.0;
        foreach (var label in links)
        {
            children.AddRange(MenuText(label, rectData.X + 10, rectData.Height + y, location));
            y += 30;
        }

        Append().Items.Add(new SceneGroup(0, 0, children) { Role = "menu", Id = id, Location = location });
        return (rectData.Height + linkY, menuWidth);
    }

    /// <summary>
    ///     Gets the CSS class selectors of a class attribute, one per class.
    /// </summary>
    /// <param name="classes">The class attribute.</param>
    /// <returns>The selectors (<c>.name</c>).</returns>
    private static IEnumerable<string> ClassSelectors(string classes) =>
        classes.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries).Select(name => "." + name);

    /// <summary>
    ///     Resolves the color of a fill attribute as a browser does: a CSS color, a color keyword or system color, else
    ///     (an invalid value, <c>inherit</c>, <c>unset</c> or a revert) the fill inherited from the root.
    /// </summary>
    /// <param name="text">The attribute.</param>
    /// <returns>The color.</returns>
    private Rgba32 FillAttribute(string text)
    {
        if (CssColor.TryParse(text, out var color))
        {
            return color;
        }

        return KeywordColors.TryGetValue(text.Trim(), out var keyword) ? keyword : RootFill;
    }

    /// <summary>
    ///     Draws a link label of a menu (Mermaid's menu <c>byTspan</c>, or its <c>byText</c> for the <c>old</c> text
    ///     placement): left-aligned, centered on the line 10 pixels below its y, in the participants' font as the
    ///     configuration writes it. Mermaid writes the raw <c>actorFontSize</c> into the inline style without
    ///     <c>parseFontSize</c>; the browser accepts a number without a unit in the inline style of an SVG text as pixels,
    ///     so an <c>actorFontSize</c> of 18 (or a top-level <c>fontSize</c> of 18, which setConf copies into it) draws the
    ///     text at 18 pixels, not at the diagram's inherited size.
    /// </summary>
    /// <param name="content">The label, with <c>&lt;br&gt;</c> tags between its lines.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="location">The location of the participant.</param>
    /// <returns>The lines.</returns>
    private List<SceneItem> MenuText(string content, double x, double y, MermaidLocation location)
    {
        if (_conf.TextPlacement == "old")
        {
            return [OldText(content, x, y, SceneTextAnchor.Start, new ActorTextStyle(null, ".actor", "menu-text"), location)];
        }

        var actorFont = _conf.ActorFont;

        // Mermaid's menu byTspan (svgDraw.js) spaces the lines by the raw actorFontSize, unlike the participant text,
        // which calls parseFontSize: a size with a unit (such as an actorFontSize of "18px", which setConf keeps when the
        // top-level fontSize is falsy) gives a dy of NaN, which the browser rejects, so the lines of one label share a
        // baseline. A top-level fontSize with a unit never gets here: the validation reports and ignores it. The raw size
        // is also the inline font-size, which FontPixels reads as the browser reads it on SVG text (a bare number is
        // pixels).
        var size = double.TryParse(actorFont.Size.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
        var font = new SceneFont(
            actorFont.Family,
            LabelBuilder.FontPixels(actorFont.Size.Text),
            StyleDeclarations.TryParseBold(actorFont.Weight.Text, out var bold) && bold,
            false);
        var color = _styles.TextColor(null, ".actor", "text.actor > tspan") ?? Black;
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var lines = LabelBuilder.SplitBreaks(content);
        var items = new List<SceneItem>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var dy = (i * size) - (size * (lines.Count - 1) / 2);
            var baseline = y + 10 + (double.IsNaN(dy) ? 0 : dy) + ((ascent - descent) / 2);
            var line = TextLine(DisplayText(lines[i]), x, baseline, SceneTextAnchor.Start, font, color, "menu-text", null, location);
            if (line.Runs.Count > 0)
            {
                items.Add(line);
            }
        }

        return items;
    }
}
