// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <content>
///     The header of a class box (PlantUML's <c>EntityImageClassHeader</c>, <c>HeaderLayout</c>, and
///     <c>CircledCharacter</c>) and the class box itself (<c>EntityImageClass</c>).
/// </content>
internal static partial class ClassRenderer
{
    /// <summary>
    ///     The distance from the center of the circled character to its baseline, as a fraction of its size (the
    ///     character is centered on the circle: half the cap height of the bold monospaced face).
    /// </summary>
    private const double SpotBaseline = 0.296;

    /// <summary>
    ///     Builds the image of a class box: the header above the compartments, as wide as the wider of them.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The image.</returns>
    private static ClassImage ClassBoxImage(Painter painter, ClassPortions portions)
    {
        var header = MeasureHeader(painter, portions);
        var body = ClassCompartments(painter, portions);
        var width = Math.Max(header.Width, body.Count == 0 ? 0 : body.Max(c => c.Width));
        var height = header.Height + body.Sum(c => c.Height);
        return new ClassImage(width, height, (x, y) =>
        {
            var items = new List<SceneItem>
            {
                painter.Path(SceneShapes.Rect(x, y, width, height, painter.Style.RoundCorner / 2), "class", painter.Fill, painter.Stroke(painter.Style.LineWidth)),
            };
            items.AddRange(DrawHeader(painter, header, x, y, width));
            var top = y + header.Height;
            foreach (var compartment in body)
            {
                items.AddRange(DrawCompartment(painter, compartment, x, top, width));
                top += compartment.Height;
            }

            return items;
        });
    }

    /// <summary>
    ///     Measures the header of a class box: the circled character, the stereotypes, the name, and the generic
    ///     parameters.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The measured header.</returns>
    private static ClassHeader MeasureHeader(Painter painter, ClassPortions portions)
    {
        var entity = painter.Entity;
        var italic = entity.Kind is ClassKind.AbstractClass or ClassKind.Interface;
        var name = painter.Layout(entity.Display, painter.Style, italic);
        var labels = entity.Stereotypes.Where(s => s.Length > 0).ToList();
        var stereotype = portions.Stereotype && labels.Count > 0
            ? painter.Layout(string.Join("\n", labels.Select(s => "\u00AB" + s + "\u00BB")), painter.StereotypeStyle)
            : null;
        var generic = entity.Generic is null ? null : painter.Layout(entity.Generic, painter.StereotypeStyle);
        var radius = painter.Theme.CircledCharacterRadius;
        var spot = portions.Circle
            ? new ClassSpot(entity.SpotCharacter ?? SpotCharacter(entity.Kind), entity.SpotColor ?? SpotColor(entity.Kind), radius)
            : null;
        return new ClassHeader(name, stereotype, generic, spot);
    }

    /// <summary>
    ///     Draws the header of a class box.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="header">The measured header.</param>
    /// <param name="x">The left of the box.</param>
    /// <param name="y">The top of the box.</param>
    /// <param name="width">The width of the box.</param>
    /// <returns>The items.</returns>
    private static IEnumerable<SceneItem> DrawHeader(Painter painter, ClassHeader header, double x, double y, double width)
    {
        // HeaderLayout: the spare width is split around the circle and the texts
        var stereoAndName = Math.Max(header.StereotypeWidth, header.NameWidth);
        var spare = Math.Max(0, width - header.CircleWidth - stereoAndName - header.GenericWidth);
        var h2 = Math.Min(header.CircleWidth / 4, spare * 0.1);
        var h1 = (spare - h2) / 2;
        if (header.Spot is { } spot)
        {
            var cx = x + h1 + 4 + spot.Radius;
            var cy = y + ((header.Height - header.CircleHeight) / 2) + 5 + spot.Radius;
            yield return painter.Path(SceneShapes.Ellipse(cx, cy, spot.Radius, spot.Radius), "class-spot", spot.Color, new SceneStroke(SkinTheme.LineColor, 1));
            var font = new SceneFont(SpotFontFamily, SpotFontSize, true, false);
            var letter = painter.Text.Layout(spot.Character.ToString(), font, painter.Style.FontColor, 0, painter.Sink, painter.Location);
            if (letter.Lines.Count == 1)
            {
                var line = letter.Lines[0];
                var top = cy + (SpotBaseline * SpotFontSize) - (line.Height - line.Descent);
                foreach (var item in painter.DrawText(letter, cx - (line.Width / 2), top, TextAlign.Left, "class-spot-text"))
                {
                    yield return item;
                }
            }
        }

        var diff = header.Height - header.StereotypeHeight - header.Name.Height;
        if (header.Stereotype is { } stereotype)
        {
            var sx = x + header.CircleWidth + ((stereoAndName - header.StereotypeWidth) / 2) + h1 + h2 + 1;
            foreach (var item in painter.DrawText(stereotype, sx, y + (diff / 2), TextAlign.Center, "class-stereotype"))
            {
                yield return item;
            }
        }

        var nx = x + header.CircleWidth + ((stereoAndName - header.NameWidth) / 2) + h1 + h2 + 3;
        foreach (var item in painter.DrawText(header.Name, nx, y + (diff / 2) + header.StereotypeHeight, TextAlign.Center, "class-name"))
        {
            yield return item;
        }

        if (header.Generic is { } generic)
        {
            // The generic box sits over the top-right corner: 4 right of and 4 above the box
            var gx = x + width - header.GenericWidth + 4;
            var gy = y - 4;
            yield return painter.Path(
                SceneShapes.Rect(gx + 1, gy + 1, generic.Width + 2, generic.Height + 2),
                "class-generic",
                SkinTheme.White,
                new SceneStroke(SkinTheme.LineColor, 1) { Dash = [2, 2] });
            foreach (var item in painter.DrawText(generic, gx + 2, gy + 2, TextAlign.Left, "class-generic-text"))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    ///     The circled character of a header.
    /// </summary>
    /// <param name="Character">The character.</param>
    /// <param name="Color">The fill of the circle.</param>
    /// <param name="Radius">The radius of the circle.</param>
    private sealed record ClassSpot(char Character, Rgba32 Color, double Radius);

    /// <summary>
    ///     A measured header (PlantUML's <c>HeaderLayout</c>): the circle with its margins (4 left, 5 above and below),
    ///     the stereotypes (1 each side), the name (3 each side), and the generic box (its text with a margin of 2).
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="Stereotype">The stereotypes, or null.</param>
    /// <param name="Generic">The generic parameters, or null.</param>
    /// <param name="Spot">The circled character, or null when it is hidden.</param>
    private sealed record ClassHeader(TextBlock Name, TextBlock? Stereotype, TextBlock? Generic, ClassSpot? Spot)
    {
        /// <summary>Gets the width of the circle with its margins.</summary>
        public double CircleWidth => Spot is null ? 0 : (2 * Spot.Radius) + 4;

        /// <summary>Gets the height of the circle with its margins.</summary>
        public double CircleHeight => Spot is null ? 0 : (2 * Spot.Radius) + 10;

        /// <summary>Gets the width of the name with its margins.</summary>
        public double NameWidth => Name.Width + 6;

        /// <summary>Gets the width of the stereotypes with their margins.</summary>
        public double StereotypeWidth => Stereotype is null ? 0 : Stereotype.Width + 2;

        /// <summary>Gets the height of the stereotypes.</summary>
        public double StereotypeHeight => Stereotype?.Height ?? 0;

        /// <summary>Gets the width of the generic box with its margins.</summary>
        public double GenericWidth => Generic is null ? 0 : Generic.Width + 4;

        /// <summary>Gets the width of the header.</summary>
        public double Width => CircleWidth + Math.Max(StereotypeWidth, NameWidth) + GenericWidth;

        /// <summary>Gets the height of the header.</summary>
        public double Height => Math.Max(Math.Max(CircleHeight, StereotypeHeight + Name.Height + 10), Generic is null ? 0 : Generic.Height + 4);
    }
}
