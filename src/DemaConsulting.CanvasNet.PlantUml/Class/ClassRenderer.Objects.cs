// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <content>
///     The object and map boxes (PlantUML's <c>EntityImageObject</c>, <c>EntityImageMap</c>, and
///     <c>TextBlockMap</c>).
/// </content>
internal static partial class ClassRenderer
{
    /// <summary>The space beside the name of an object or a map (<c>xMarginCircle</c>).</summary>
    private const double ObjectMarginX = 5;

    /// <summary>The padding around the name of an object or a map (the <c>object</c> padding of the skin).</summary>
    private const double ObjectPadding = 2;

    /// <summary>The width of the fields of an object that has none (<c>TextBlockEmpty(10, 16)</c>).</summary>
    private const double EmptyFieldsWidth = 10;

    /// <summary>The height of the fields of an object that has none.</summary>
    private const double EmptyFieldsHeight = 16;

    /// <summary>The margin of a map cell beside its text.</summary>
    private const double MapCellMarginX = 5;

    /// <summary>The margin of a map cell above and below its text.</summary>
    private const double MapCellMarginY = 2;

    /// <summary>
    ///     Builds the image of an object: its centered stereotypes and name, then its fields below a line.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The image.</returns>
    private static ClassImage ObjectImage(Painter painter, ClassPortions portions)
    {
        var (name, stereotype, titleWidth, titleHeight) = MeasureObjectTitle(painter, portions);
        var emptyFields = portions.Fields && painter.Entity.Body.Count == 0;
        List<Compartment> fields = portions.Fields && !emptyFields ? Blocks(painter, painter.Entity.Body, methodsByLine: false) : [];
        var fieldsWidth = fields.Count == 0 ? 0 : fields.Max(c => c.Width);
        var fieldsHeight = fields.Sum(c => c.Height);
        if (emptyFields)
        {
            (fieldsWidth, fieldsHeight) = (EmptyFieldsWidth, EmptyFieldsHeight);
        }
        var width = Math.Max(fieldsWidth, titleWidth + (2 * ObjectMarginX));
        var height = titleHeight + fieldsHeight;
        return new ClassImage(width, height, (x, y) =>
        {
            var items = new List<SceneItem>
            {
                painter.Path(SceneShapes.Rect(x, y, width, height, painter.Style.RoundCorner / 2), "class", painter.Fill, painter.Stroke(painter.Style.LineWidth)),
            };
            items.AddRange(DrawObjectTitle(painter, name, stereotype, x, y, width));
            var top = y + titleHeight;
            if (emptyFields)
            {
                items.Add(painter.Path(SceneShapes.Line(x + 1, top, x + width - 1, top), "class-separator", null, painter.Stroke(painter.Style.LineWidth)));
            }

            foreach (var compartment in fields)
            {
                items.AddRange(DrawCompartment(painter, compartment, x, top, width));
                top += compartment.Height;
            }

            return items;
        });
    }

    /// <summary>
    ///     Builds the image of a map: its centered stereotypes and name, then one row per entry with the key centered
    ///     in the first column and the value in the second, ruled by lines one pixel wide.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The image.</returns>
    private static ClassImage MapImage(Painter painter, ClassPortions portions)
    {
        var (name, stereotype, titleWidth, titleHeight) = MeasureObjectTitle(painter, portions);
        var rows = painter.Entity.Entries
            .Select(e =>
            {
                var key = ClassMember.VisibilityOf(e.Key) == MemberVisibility.None ? e.Key : e.Key[1..];
                return (Key: painter.Layout(key, painter.MemberStyle), Value: painter.Layout(e.Value, painter.MemberStyle));
            })
            .ToList();
        var columnA = rows.Count == 0 ? 0 : rows.Max(r => r.Key.Width) + (2 * MapCellMarginX);
        var columnB = rows.Count == 0 ? 0 : rows.Max(r => r.Value.Width) + (2 * MapCellMarginX);
        var rowHeights = rows.Select(r => Math.Max(r.Key.Height, r.Value.Height) + (2 * MapCellMarginY)).ToList();
        var width = Math.Max(columnA + columnB, titleWidth + (2 * ObjectMarginX));
        var height = titleHeight + rowHeights.Sum();
        return new ClassImage(width, height, (x, y) =>
        {
            var items = new List<SceneItem>
            {
                painter.Path(SceneShapes.Rect(x, y, width, height, painter.Style.RoundCorner / 2), "class", painter.Fill, painter.Stroke(painter.Style.LineWidth)),
            };
            items.AddRange(DrawObjectTitle(painter, name, stereotype, x, y, width));
            var top = y + titleHeight;
            for (var i = 0; i < rows.Count; i++)
            {
                var (key, value) = rows[i];
                var rowHeight = rowHeights[i];
                items.Add(painter.Path(SceneShapes.Line(x, top, x + width, top), "map-separator", null, painter.Stroke(1)));
                var keyBlock = key.Width + (2 * MapCellMarginX);
                items.AddRange(painter.DrawText(key, x + ((columnA - keyBlock) / 2) + MapCellMarginX, top + MapCellMarginY, TextAlign.Left, "map-key"));
                items.AddRange(painter.DrawText(value, x + columnA + MapCellMarginX, top + MapCellMarginY, TextAlign.Left, "map-value"));
                items.Add(painter.Path(SceneShapes.Line(x + columnA, top, x + columnA, top + rowHeight), "map-separator", null, painter.Stroke(1)));
                top += rowHeight;
            }

            return items;
        });
    }

    /// <summary>
    ///     Measures the title of an object or a map: its name with a padding of 2, and its stereotypes above it.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The name, the stereotypes (or null), and the size of the title.</returns>
    private static (TextBlock Name, TextBlock? Stereotype, double Width, double Height) MeasureObjectTitle(Painter painter, ClassPortions portions)
    {
        var name = painter.Layout(painter.Entity.Display, painter.Style);
        var labels = painter.Entity.Stereotypes.Where(s => s.Length > 0).ToList();
        var stereotype = portions.Stereotype && labels.Count > 0
            ? painter.Layout(string.Join("\n", labels.Select(s => "\u00AB" + s + "\u00BB")), painter.StereotypeStyle)
            : null;
        var width = Math.Max(name.Width + (2 * ObjectPadding), stereotype?.Width ?? 0);
        var height = name.Height + (2 * ObjectPadding) + (stereotype?.Height ?? 0);
        return (name, stereotype, width, height);
    }

    /// <summary>
    ///     Draws the title of an object or a map, centered.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="name">The name.</param>
    /// <param name="stereotype">The stereotypes, or null.</param>
    /// <param name="x">The left of the box.</param>
    /// <param name="y">The top of the box.</param>
    /// <param name="width">The width of the box.</param>
    /// <returns>The items.</returns>
    private static IEnumerable<SceneItem> DrawObjectTitle(Painter painter, TextBlock name, TextBlock? stereotype, double x, double y, double width)
    {
        var top = y;
        if (stereotype is not null)
        {
            foreach (var item in painter.DrawText(stereotype, x + ((width - stereotype.Width) / 2), top, TextAlign.Center, "class-stereotype"))
            {
                yield return item;
            }

            top += stereotype.Height;
        }

        var nameBlock = name.Width + (2 * ObjectPadding);
        foreach (var item in painter.DrawText(name, x + ((width - nameBlock) / 2) + ObjectPadding, top + ObjectPadding, TextAlign.Center, "class-name"))
        {
            yield return item;
        }
    }
}
